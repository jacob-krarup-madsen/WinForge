namespace WingetStore.Tests;

public class ViewModelTests
{
    [Fact]
    public async Task SearchViewModel_LoadAndFilter()
    {
        var searchVM = App.Services.GetRequiredService<SearchViewModel>();
        var searchTask = searchVM.SearchAsync("git");
        Assert.True(searchVM.IsLoading);
        await searchTask;
        Assert.False(searchVM.IsLoading);
        Assert.NotEmpty(searchVM.SearchResults);
    }

    [Fact]
    public async Task SearchViewModel_Cancellation_RapidTyping()
    {
        var searchVM = App.Services.GetRequiredService<SearchViewModel>();
        var task1 = searchVM.SearchAsync("g");
        var task2 = searchVM.SearchAsync("gi");
        var task3 = searchVM.SearchAsync("git");
        await Task.WhenAll(task1, task2, task3);
        Assert.Equal("git", searchVM.SearchQuery);
        Assert.False(searchVM.IsLoading);
    }

    [Fact]
    public async Task InstalledViewModel_FiltersAutomatically()
    {
        var installedVM = App.Services.GetRequiredService<InstalledViewModel>();
        await installedVM.LoadPackagesAsync();
        installedVM.FilterQuery = "mock";
        foreach (var pkg in installedVM.FilteredPackages)
        {
            Assert.Contains("mock", (pkg.Name ?? "").ToLower() + (pkg.Id ?? "").ToLower() + (pkg.Publisher ?? "").ToLower());
        }
    }

    [Fact]
    public async Task ViewModels_SetErrorMessage_OnException()
    {
        var throwingService = new ThrowingWingetService();

        var searchVM = new SearchViewModel(throwingService);
        await searchVM.SearchAsync("test");
        Assert.True(searchVM.IsErrorOpen);
        Assert.Contains("Search failed", searchVM.ErrorMessage);

        var installedVM = new InstalledViewModel(throwingService);
        await installedVM.LoadPackagesAsync();
        Assert.True(installedVM.IsErrorOpen);
        Assert.Contains("Failed to load installed apps", installedVM.ErrorMessage);

        var updatesVM = new UpdatesViewModel(throwingService);
        await updatesVM.LoadUpgradesAsync();
        Assert.True(updatesVM.IsErrorOpen);
        Assert.Contains("Failed to load upgradable apps", updatesVM.ErrorMessage);

        var homeVM = new HomeViewModel(throwingService);
        await homeVM.LoadFeaturedContentAsync();
        Assert.True(homeVM.IsErrorOpen);
        Assert.Contains("Failed to load home content", homeVM.ErrorMessage);
    }

    [Fact]
    public async Task ViewModels_PropertyAndCommandCoverages()
    {
        await TestHelper.RunWithDispatcherAsync(async () =>
        {
            var searchVM = App.Services.GetRequiredService<SearchViewModel>();
            searchVM.FilterQuery = "mock";
            searchVM.SortOrder = "az";
            await searchVM.SearchAsync("mock");
            var installedVM = App.Services.GetRequiredService<InstalledViewModel>();
            await installedVM.LoadPackagesAsync();
            Assert.NotEmpty(installedVM.FilteredPackages);
            var mockPkg = new WingetPackage { Id = "Mock.App.Installed", Name = "Mock Installed App", Status = PackageStatus.Installed };
            installedVM.UpgradeCommand.Execute(mockPkg);
            installedVM.UninstallCommand.Execute(mockPkg);

            var updatesVM = App.Services.GetRequiredService<UpdatesViewModel>();
            await updatesVM.LoadUpgradesAsync();
            updatesVM.FilterQuery = "mock";
            updatesVM.SortOrder = "az";

            var upgradePkg = new WingetPackage { Id = "Mock.App", Name = "Mock App", Status = PackageStatus.Upgradable };
            updatesVM.UpgradeCommand.Execute(upgradePkg);

            upgradePkg.Status = PackageStatus.Installed;
            WeakReferenceMessenger.Default.Send<PackageStatusChangedMessage>(new PackageStatusChangedMessage(upgradePkg));

            var homeVM = App.Services.GetRequiredService<HomeViewModel>();
            await homeVM.LoadFeaturedContentAsync();
            homeVM.FilterQuery = "mock";
            homeVM.SortOrder = "az";
            homeVM.SortOrder = "za";
        });
    }

    [Fact]
    public async Task ViewModels_EdgeCasesAndDeepCoverage()
    {
        var mainWindowProp = typeof(App).GetProperty("MainWindow", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static)!;
#pragma warning disable SYSLIB0050
        var mockMainWindow = System.Runtime.Serialization.FormatterServices.GetUninitializedObject(typeof(MainWindow));

#pragma warning restore SYSLIB0050
        mainWindowProp.SetValue(null, mockMainWindow);

        await TestHelper.RunWithDispatcherAsync(async () =>
        {
            var searchVM = App.Services.GetRequiredService<SearchViewModel>();
            await searchVM.SearchAsync(null!);
            await searchVM.SearchAsync("   ");

            var searchResultsField = typeof(SearchViewModel).GetField("_allResults", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!;
            var testPkgs = new List<WingetPackage>
            {
                    new WingetPackage { Id = "WingetApp", Name = "Winget App", Source = "winget" },
                    new WingetPackage { Id = "OtherApp", Name = "Other App", Source = "other" }
            };
            searchResultsField.SetValue(searchVM, testPkgs);

            searchVM.FilterQuery = "";
            searchVM.SourceFilter = "winget";
            searchVM.ApplyFilter();
            Assert.Single(searchVM.FilteredResults);

            searchVM.SourceFilter = "all";
            searchVM.SortOrder = "az";
            searchVM.ApplyFilter();

            searchVM.SortOrder = "za";
            searchVM.ApplyFilter();

            searchVM.SortOrder = "default";
            searchVM.ApplyFilter();

            searchVM.SourceFilter = "unknown";
            searchVM.ApplyFilter();
            Assert.Empty(searchVM.FilteredResults);

            var nullSrc = new List<WingetPackage> { new() { Id = "ns", Source = null! } };
            searchResultsField.SetValue(searchVM, nullSrc);
            searchVM.SourceFilter = "winget";
            searchVM.ApplyFilter();
            Assert.False(searchVM.HasResults);

            var installedVM = App.Services.GetRequiredService<InstalledViewModel>();
            await installedVM.LoadPackagesAsync();
            var devOpts = installedVM.DeveloperOptions;
            Assert.NotEmpty(devOpts);

            var pkgToInstallable = new WingetPackage { Id = "Mock.Temp1", Name = "Mock Temp 1", Status = PackageStatus.Installed };
            var internalListField = typeof(InstalledViewModel).GetField("_allPackages", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!;
            var allPkgs = (List<WingetPackage>)internalListField.GetValue(installedVM)!;
            allPkgs.Add(pkgToInstallable);

            pkgToInstallable.Status = PackageStatus.Installable;
            WeakReferenceMessenger.Default.Send<PackageStatusChangedMessage>(new PackageStatusChangedMessage(pkgToInstallable));
            Assert.DoesNotContain(pkgToInstallable, allPkgs);

            var pkgToUpdate = new WingetPackage { Id = "Mock.Temp2", Name = "Mock Temp 2", Status = PackageStatus.Upgradable, Version = "1.0", AvailableVersion = "2.0" };
            allPkgs.Add(pkgToUpdate);
            pkgToUpdate.Status = PackageStatus.Installed;
            WeakReferenceMessenger.Default.Send<PackageStatusChangedMessage>(new PackageStatusChangedMessage(pkgToUpdate));
            Assert.Equal("2.0", pkgToUpdate.Version);

            var upgradableInList = new WingetPackage { Id = "Mock.Upgradable.InList", Name = "Mock Upgradable In List", Status = PackageStatus.Upgradable };
            allPkgs.Add(upgradableInList);
            allPkgs.Add(null!);
            var triggerInstalled = new WingetPackage { Id = "Mock.Trigger.Installed", Name = "Mock Trigger Installed", Status = PackageStatus.Upgradable };
            allPkgs.Add(triggerInstalled);
            triggerInstalled.Status = PackageStatus.Installed;
            WeakReferenceMessenger.Default.Send<PackageStatusChangedMessage>(new PackageStatusChangedMessage(triggerInstalled));
            allPkgs.Remove(upgradableInList);
            allPkgs.RemoveAll(p => p == null);

            installedVM.DeveloperFilter = "Mock Publisher";
            installedVM.SourceFilter = "winget";
            installedVM.SortOrder = "az";
            installedVM.ApplyFilter();

            installedVM.SortOrder = "za";
            installedVM.ApplyFilter();

            installedVM.DeveloperFilter = "Mock Publisher";
            installedVM.SourceFilter = "all";
            internalListField.SetValue(installedVM, new List<WingetPackage>
            {
                    new() { Id = "ep", Publisher = "", Source = "winget" },
                    new() { Id = "np", Publisher = null!, Source = "winget" },
                    new() { Id = "mp", Publisher = "Mock Publisher", Source = "winget" }
            });

            installedVM.ApplyFilter();
            Assert.Single(installedVM.FilteredPackages);

            installedVM.DeveloperFilter = "All Publishers";
            internalListField.SetValue(installedVM, new List<WingetPackage>
            {
                    new() { Id = "es", Source = "" },
                    new() { Id = "ns", Source = null! },
                    new() { Id = "ms", Source = "winget" }
            });
            installedVM.SourceFilter = "winget";
            installedVM.ApplyFilter();
            Assert.Single(installedVM.FilteredPackages);

            var updatesVM = App.Services.GetRequiredService<UpdatesViewModel>();
            await updatesVM.LoadUpgradesAsync();

            var upgradePkg1 = new WingetPackage { Id = "Mock.Upg1", Name = "Mock Upg 1", Status = PackageStatus.Upgradable, IsInstalling = true, InstallProgress = 40, Source = "winget" };
            var upgradePkg2 = new WingetPackage { Id = "Mock.Upg2", Name = "Mock Upg 2", Status = PackageStatus.Upgradable, IsInstalling = true, InstallProgress = 60, Source = "winget" };
            var upgradePkg3 = new WingetPackage { Id = "Mock.Upg3", Name = "Mock Upg 3", Status = PackageStatus.Upgradable, IsInstalling = false, InstallProgress = 0, Source = "winget" };

            updatesVM.Upgrades.Add(upgradePkg1);
            updatesVM.Upgrades.Add(upgradePkg2);
            updatesVM.Upgrades.Add(upgradePkg3);

            var internalUpgradesField = typeof(UpdatesViewModel).GetField("_allUpgrades", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!;
            var allUpgradesList = (List<WingetPackage>)internalUpgradesField.GetValue(updatesVM)!;
            allUpgradesList.Clear();
            allUpgradesList.Add(upgradePkg1);
            allUpgradesList.Add(upgradePkg2);
            allUpgradesList.Add(upgradePkg3);

            updatesVM.UpdateGlobalProgress();
            Assert.True(updatesVM.IsGlobalProgressVisible);
            Assert.Equal(50, updatesVM.GlobalProgressValue);
            Assert.Contains("2 apps", updatesVM.GlobalProgressStatusText);

            updatesVM.SourceFilter = "winget";
            Assert.Equal(3, updatesVM.FilteredUpgrades.Count);
            updatesVM.SourceFilter = "all";
            Assert.Equal(3, updatesVM.FilteredUpgrades.Count);



            internalUpgradesField.SetValue(updatesVM, new List<WingetPackage> { new() { Id = "nsu", Source = null! } });
            var savedUpgrades = updatesVM.Upgrades;
            updatesVM.Upgrades = new ObservableCollection<WingetPackage> { new() { Id = "nsu", Source = null! } };
            updatesVM.SourceFilter = "winget";
            updatesVM.ApplyFilter();
            Assert.Empty(updatesVM.FilteredUpgrades);
            var savedFilteredUpgrades = updatesVM.FilteredUpgrades;
            internalUpgradesField.SetValue(updatesVM, allUpgradesList);
            updatesVM.Upgrades = savedUpgrades;
            updatesVM.SourceFilter = "all";
            updatesVM.FilteredUpgrades = savedFilteredUpgrades;

            updatesVM.SortOrder = "az";
            updatesVM.SortOrder = "za";

            upgradePkg2.IsInstalling = false;
            updatesVM.UpdateGlobalProgress();
            Assert.Contains("Mock Upg 1", updatesVM.GlobalProgressStatusText);

            updatesVM.UpgradeAllCommand.Execute(null);
            updatesVM.UpgradeCommand.Execute(upgradePkg3);

            upgradePkg1.Status = PackageStatus.Installed;
            WeakReferenceMessenger.Default.Send<PackageStatusChangedMessage>(new PackageStatusChangedMessage(upgradePkg1));
            Assert.DoesNotContain(upgradePkg1, updatesVM.Upgrades);

            WeakReferenceMessenger.Default.Send<PackageStatusChangedMessage>(new PackageStatusChangedMessage(null!));

            installedVM.DevelopersList = null!;
            Assert.NotNull(installedVM.DeveloperOptions);

            var allPkgsList = (List<WingetPackage>)internalListField.GetValue(installedVM)!;
            allPkgsList.Add(null!);
            var populateMethod = typeof(InstalledViewModel).GetMethod("PopulateDevelopersList", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!;
            populateMethod.Invoke(installedVM, null);

            var searchVM2 = App.Services.GetRequiredService<SearchViewModel>();
            var allResultsField = typeof(SearchViewModel).GetField("_allResults", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!;

            var nullSourcePkg = new WingetPackage { Id = "null.src", Source = null! };
            var allResultsList = new List<WingetPackage> { nullSourcePkg };
            allResultsField.SetValue(searchVM2, allResultsList);
            searchVM2.SortOrder = "default";
            searchVM2.ApplyFilter();

            var pkgInstalling1 = new WingetPackage { Id = "inst1", IsInstalling = true, InstallProgress = 50.0 };
            var pkgInstalling2 = new WingetPackage { Id = "inst2", IsInstalling = false, InstallProgress = 0.0 };
            updatesVM.Upgrades = new ObservableCollection<WingetPackage> { pkgInstalling1, pkgInstalling2 };
            updatesVM.UpgradeAll();

            mainWindowProp?.SetValue(null, null);
            upgradePkg1.Status = PackageStatus.Installed;
            WeakReferenceMessenger.Default.Send<PackageStatusChangedMessage>(new PackageStatusChangedMessage(upgradePkg1));

            await App.Services.GetRequiredService<UpdatesViewModel>().LoadUpgradesAsync();
        });
        mainWindowProp?.SetValue(null, null);
    }

    [Fact]
    public void ViewModels_GeneratorBranchCoverage()
    {
        App.DispatcherOverride = action => action();
        try
        {
            var searchVM = App.Services.GetRequiredService<SearchViewModel>();
            Assert.Same(searchVM.SearchCommand, searchVM.SearchCommand);

            var installedVM = App.Services.GetRequiredService<InstalledViewModel>();
            Assert.Same(installedVM.UninstallCommand, installedVM.UninstallCommand);
            Assert.Same(installedVM.UpgradeCommand, installedVM.UpgradeCommand);
            Assert.Same(installedVM.LoadPackagesCommand, installedVM.LoadPackagesCommand);

            var updatesVM = App.Services.GetRequiredService<UpdatesViewModel>();
            Assert.Same(updatesVM.UpgradeAllCommand, updatesVM.UpgradeAllCommand);
            Assert.Same(updatesVM.UpgradeCommand, updatesVM.UpgradeCommand);
            Assert.Same(updatesVM.LoadUpgradesCommand, updatesVM.LoadUpgradesCommand);
        }
        finally
        {
            App.DispatcherOverride = null;
        }
    }

    [Fact]
    public void InstalledPage_GetUpdateVisibility_ReturnsExpected()
    {
        Assert.Equal(Microsoft.UI.Xaml.Visibility.Visible, Pages.InstalledPage.GetUpdateVisibility(PackageStatus.Upgradable));
        Assert.Equal(Microsoft.UI.Xaml.Visibility.Collapsed, Pages.InstalledPage.GetUpdateVisibility(PackageStatus.Installed));
        Assert.Equal(Microsoft.UI.Xaml.Visibility.Collapsed, Pages.InstalledPage.GetUpdateVisibility(PackageStatus.Installable));
    }

    [Fact]
    public async Task InstalledViewModel_DeveloperFilter_NullOrEmptyDefaultsToAll()
    {
        var vm = App.Services.GetRequiredService<InstalledViewModel>();
        await vm.LoadPackagesAsync();

        vm.DeveloperFilter = null!;
        vm.ApplyFilter();
        Assert.NotEmpty(vm.FilteredPackages);

        vm.DeveloperFilter = "";
        vm.ApplyFilter();
        Assert.NotEmpty(vm.FilteredPackages);

        vm.DeveloperFilter = "  ";
        vm.ApplyFilter();
        Assert.NotEmpty(vm.FilteredPackages);
    }

    [Fact]
    public void DummyAppsFleet_HandlesDiversePackagesAndFilters()

    {
        var diverseApps = new List<WingetPackage>
            {
                new WingetPackage { Id = "App.1", Name = "App One", Publisher = "Publisher A", Source = "winget", Status = PackageStatus.Installed },
                new WingetPackage { Id = "App.2", Name = "App Two", Publisher = "Publisher B", Source = "winget", Status = PackageStatus.Installed },
                new WingetPackage { Id = "App.3", Name = "App Three", Publisher = null!, Source = "winget", Status = PackageStatus.Installed },
                new WingetPackage { Id = "App.4", Name = "Special (x64) & Co.", Publisher = "Special & Co.", Source = "winget", Status = PackageStatus.Upgradable, AvailableVersion = "2.0" },
                new WingetPackage { Id = "App.5", Name = "Null Pub App", Publisher = "", Source = "winget", Status = PackageStatus.Installed }
            };

        for (int i = 6; i <= 50; i++)
        {
            diverseApps.Add(new WingetPackage
            {
                Id = $"Fleet.App.{i}",
                Name = $"Fleet Application {i}",
                Publisher = i % 2 == 0 ? "Fleet Devs" : "Open Source",
                Source = "winget",
                Status = i % 5 == 0 ? PackageStatus.Upgradable : PackageStatus.Installed
            });
        }

        var vm = App.Services.GetRequiredService<InstalledViewModel>();
        var allPkgsField = typeof(InstalledViewModel).GetField("_allPackages", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!;
        allPkgsField.SetValue(vm, diverseApps);

        var populateDevs = typeof(InstalledViewModel).GetMethod("PopulateDevelopersList", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!;
        populateDevs.Invoke(vm, null);

        Assert.Contains("All Publishers", vm.DeveloperOptions);
        Assert.Contains("Publisher A", vm.DeveloperOptions);
        Assert.Contains("Publisher B", vm.DeveloperOptions);
        Assert.Contains("Fleet Devs", vm.DeveloperOptions);

        vm.DeveloperFilter = "All Publishers";
        vm.SourceFilter = "winget";
        vm.ApplyFilter();
        Assert.Equal(50, vm.FilteredPackages.Count);


        vm.DeveloperFilter = "Publisher A";
        vm.ApplyFilter();
        Assert.Single(vm.FilteredPackages);
        Assert.Equal("App.1", vm.FilteredPackages[0].Id);

        vm.DeveloperFilter = "All Publishers";
        vm.SourceFilter = "winget";
        vm.ApplyFilter();
        Assert.All(vm.FilteredPackages, p => Assert.Contains("winget", p.Source ?? "", StringComparison.OrdinalIgnoreCase));

        vm.SortOrder = "az";
        vm.ApplyFilter();
        Assert.True(string.Compare(vm.FilteredPackages[0].Name, vm.FilteredPackages[1].Name, StringComparison.OrdinalIgnoreCase) <= 0);

        vm.SortOrder = "za";
        vm.ApplyFilter();
        Assert.True(string.Compare(vm.FilteredPackages[0].Name, vm.FilteredPackages[1].Name, StringComparison.OrdinalIgnoreCase) >= 0);
    }

    [Fact]
    public async Task AutomatedHeadless_ViewModelsHandleThrowingServiceGracefully()
    {
        var throwingService = new ThrowingWingetService();

        var installedVM = new InstalledViewModel(throwingService);
        await installedVM.LoadPackagesAsync();
        Assert.True(installedVM.IsErrorOpen);
        Assert.Contains("Failed to load installed apps", installedVM.ErrorMessage);

        var homeVM = new HomeViewModel(throwingService);
        await homeVM.LoadFeaturedContentAsync();
        Assert.True(homeVM.IsErrorOpen);
        Assert.Contains("Failed to load home content", homeVM.ErrorMessage);

        var updatesVM = new UpdatesViewModel(throwingService);
        await updatesVM.LoadUpgradesAsync();
        Assert.True(updatesVM.IsErrorOpen);
        Assert.Contains("Failed to load upgradable apps", updatesVM.ErrorMessage);
    }

    [Fact]
    public void NavigationHelper_ResolvesAllPageTypesCorrectly()
    {
        Assert.Equal(typeof(Pages.HomePage), NavigationHelper.GetPageType("home", false, true));
        Assert.Equal(typeof(Pages.InstalledPage), NavigationHelper.GetPageType("installed", false, true));
        Assert.Equal(typeof(Pages.UpdatesPage), NavigationHelper.GetPageType("updates", false, true));
        Assert.Equal(typeof(Pages.AboutPage), NavigationHelper.GetPageType("about", false, true));
        Assert.Equal(typeof(Pages.SettingsPage), NavigationHelper.GetPageType("settings", true, true));
        Assert.Equal(typeof(Pages.NoWingetPage), NavigationHelper.GetPageType("home", false, false));
        Assert.Null(NavigationHelper.GetPageType("unknown_tag", false, true));
    }
}
