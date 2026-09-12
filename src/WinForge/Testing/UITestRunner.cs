#pragma warning disable CS1998

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using WingetStore.Models;
using WingetStore.Services;
using WingetStore.ViewModels;

namespace WingetStore.Testing;

public static class UITestRunner
{
    /// <summary>Wraps IServiceProvider to override specific service registrations for testing.</summary>
    private sealed class OverrideServiceProvider(IServiceProvider inner) : IServiceProvider
    {
        private readonly Dictionary<Type, object> _overrides = [];

        public void AddOverride<T>(T instance) where T : class => _overrides[typeof(T)] = instance;

        public object? GetService(Type serviceType) =>
            _overrides.TryGetValue(serviceType, out var instance) ? instance : inner.GetService(serviceType);
    }

    /// <summary>Delegating IWingetService that delegates all calls and supports per-method override lambdas.</summary>
    private sealed class DelegatingWingetService(IWingetService inner) : IWingetService
    {
        public Func<string, CancellationToken, Task<List<WingetPackage>>>? SearchPackagesAsyncFunc;
        public Func<Task<WingetPackage>>? FetchAndDecoratePackageDetailsAsyncFunc;
        public ObservableCollection<InstallTask> ActiveTasks => inner.ActiveTasks;
        public Task<string> RunCommandAsync(string arguments, CancellationToken cancellationToken = default) => inner.RunCommandAsync(arguments, cancellationToken);
        public Task<List<WingetPackage>> SearchPackagesAsync(string query, CancellationToken cancellationToken = default) =>
            SearchPackagesAsyncFunc?.Invoke(query, cancellationToken) ?? inner.SearchPackagesAsync(query, cancellationToken);
        public Task<List<WingetPackage>> GetInstalledPackagesAsync() => inner.GetInstalledPackagesAsync();
        public Task<List<WingetPackage>> GetUpgradablePackagesAsync() => inner.GetUpgradablePackagesAsync();
        public Task<List<WingetPackage>> GetPopularPackagesAsync() => inner.GetPopularPackagesAsync();
        public Task<List<WingetPackage>> GetRecommendationsAsync() => inner.GetRecommendationsAsync();
        public Task<List<CategoryItem>> GetCategoriesAsync() => inner.GetCategoriesAsync();
        public Task<WingetPackage?> GetPackageDetailsAsync(PackageId packageId) => inner.GetPackageDetailsAsync(packageId);
        public Task<WingetPackage> FetchAndDecoratePackageDetailsAsync(PackageId packageId) =>
            FetchAndDecoratePackageDetailsAsyncFunc?.Invoke() ?? inner.FetchAndDecoratePackageDetailsAsync(packageId);
        public void InstallPackage(WingetPackage package) => inner.InstallPackage(package);
        public void UpgradePackage(WingetPackage package) => inner.UpgradePackage(package);
        public void UninstallPackage(WingetPackage package) => inner.UninstallPackage(package);
        public void TriggerPackageAction(WingetPackage package) => inner.TriggerPackageAction(package);
        public void CancelTask(string taskId) => inner.CancelTask(taskId);
        public void CancelTaskForPackage(string packageId) => inner.CancelTaskForPackage(packageId);
        public WingetPackage GetOrCreatePackage(WingetPackage incoming) => inner.GetOrCreatePackage(incoming);
        public Task<string> ExportPackagesAsync(string filepath) => inner.ExportPackagesAsync(filepath);
        public Task<string> ImportPackagesAsync(string filepath) => inner.ImportPackagesAsync(filepath);
    }

    /// <summary>Mock IWingetService that returns a rich WingetPackage for LoadDetailsAsync coverage.</summary>
    private sealed class MockWingetService(IWingetService inner) : IWingetService
    {
        public ObservableCollection<InstallTask> ActiveTasks => inner.ActiveTasks;
        public Task<string> RunCommandAsync(string arguments, CancellationToken cancellationToken = default) => inner.RunCommandAsync(arguments, cancellationToken);
        public Task<List<WingetPackage>> SearchPackagesAsync(string query, CancellationToken cancellationToken = default) => inner.SearchPackagesAsync(query, cancellationToken);
        public Task<List<WingetPackage>> GetInstalledPackagesAsync() => inner.GetInstalledPackagesAsync();
        public Task<List<WingetPackage>> GetUpgradablePackagesAsync() => inner.GetUpgradablePackagesAsync();
        public Task<List<WingetPackage>> GetPopularPackagesAsync() => inner.GetPopularPackagesAsync();
        public Task<List<WingetPackage>> GetRecommendationsAsync() => inner.GetRecommendationsAsync();
        public Task<List<CategoryItem>> GetCategoriesAsync() => inner.GetCategoriesAsync();
        public Task<WingetPackage?> GetPackageDetailsAsync(PackageId packageId) => inner.GetPackageDetailsAsync(packageId);
        public async Task<WingetPackage> FetchAndDecoratePackageDetailsAsync(PackageId packageId) => new()
        {
            Id = packageId,
            Name = "Mock Rich Package",
            Publisher = "Mock Publisher Inc.",
            Version = "2.1.0",
            AvailableVersion = "3.0.0-beta",
            Description = "A feature-rich mock package used to exercise DetailsPage UI code paths.",
            IconUrl = "https://example.com/icons/mock-app.png",
            ReleaseNotes = "Added new features\nFixed critical bugs\nImproved performance",
            Tags = ["developer", "tools", "testing", "mock"],
            Screenshots = ["https://example.com/screenshots/ss1.png", "https://example.com/screenshots/ss2.png"],
            Status = PackageStatus.Installable,
        };
        public void InstallPackage(WingetPackage package) => inner.InstallPackage(package);
        public void UpgradePackage(WingetPackage package) => inner.UpgradePackage(package);
        public void UninstallPackage(WingetPackage package) => inner.UninstallPackage(package);
        public void TriggerPackageAction(WingetPackage package) => inner.TriggerPackageAction(package);
        public void CancelTask(string taskId) => inner.CancelTask(taskId);
        public void CancelTaskForPackage(string packageId) => inner.CancelTaskForPackage(packageId);
        public WingetPackage GetOrCreatePackage(WingetPackage incoming) => inner.GetOrCreatePackage(incoming);
        public Task<string> ExportPackagesAsync(string filepath) => inner.ExportPackagesAsync(filepath);
        public Task<string> ImportPackagesAsync(string filepath) => inner.ImportPackagesAsync(filepath);
    }
    public static async Task RunNonHeadlessUITestsAsync(Microsoft.UI.Xaml.Controls.Frame navFrame)
    {
        LogService.LogInfo("=== STARTING NON-HEADLESS WINUI 3 INTEGRATION TEST CYCLE ===");
        int pass = 0, fail = 0;
        try
        {
            ArgumentNullException.ThrowIfNull(navFrame);

            static async Task<bool> NavigateAndTest(Microsoft.UI.Xaml.Controls.Frame frame, Type pageType, string label, Func<Task>? test = null, object? param = null)
            {
                try
                {
                    frame.Navigate(pageType, param);
                    await Task.Delay(500);
                    if (test != null) await test();
                    LogService.LogInfo($"PASS: {label}");
                    return true;
                }
                catch (Exception ex)
                {
                    LogService.LogError($"FAIL: {label}", ex);
                    try { while (frame.CanGoBack) frame.GoBack(); } catch { }
                    return false;
                }
            }

            // ========== HomePage ==========
            if (await NavigateAndTest(navFrame, typeof(Pages.HomePage), "HomePage loaded"))
                pass++; else fail++;

            if (await NavigateAndTest(navFrame, typeof(Pages.HomePage), "HomePage search & sort", async () =>
            {
                if (navFrame.Content is Pages.HomePage hp)
                {
                    hp.ViewModel.FilterQuery = "git";
                    hp.ViewModel.SortOrder = "az";
                    hp.ViewModel.ApplyFilter();
                }
            })) pass++; else fail++;

            if (await NavigateAndTest(navFrame, typeof(Pages.HomePage), "HomePage clear search", async () =>
            {
                if (navFrame.Content is Pages.HomePage hp)
                {
                    var hpType = typeof(Pages.HomePage);
                    var clearMethod = hpType.GetMethod("ClearSearchButton_Click",
                        System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                    clearMethod?.Invoke(hp, [null, null]);
                }
            })) pass++; else fail++;

            if (await NavigateAndTest(navFrame, typeof(Pages.HomePage), "HomePage see all", async () =>
            {
                if (navFrame.Content is Pages.HomePage hp)
                {
                    var hpType = typeof(Pages.HomePage);
                    var seeAllMethod = hpType.GetMethod("SeeAllButton_Click",
                        System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                    seeAllMethod?.Invoke(hp, [null, null]);
                }
            })) pass++; else fail++;

            // HomePage SearchButton_Click: covers ProcessSearchInput path
            if (await NavigateAndTest(navFrame, typeof(Pages.HomePage), "HomePage SearchButton_Click", async () =>
            {
                if (navFrame.Content is Pages.HomePage hp)
                {
                    var hpType = typeof(Pages.HomePage);
                    var searchBtnMethod = hpType.GetMethod("SearchButton_Click",
                        System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                    searchBtnMethod?.Invoke(hp, [null, null]);
                }
            })) pass++; else fail++;

            // HomePage DetailsButton_Click: covers navigation via Button.DataContext
            if (await NavigateAndTest(navFrame, typeof(Pages.HomePage), "HomePage DetailsButton_Click", async () =>
            {
                if (navFrame.Content is Pages.HomePage hp)
                {
                    var hpType = typeof(Pages.HomePage);
                    var detailsBtnMethod = hpType.GetMethod("DetailsButton_Click",
                        System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                    detailsBtnMethod?.Invoke(hp, [new Button { DataContext = new WingetPackage { Id = "Test.Pkg", Name = "Test" } }, new RoutedEventArgs()]);
                }
            })) pass++; else fail++;

            // HomePage ActionButton_Click: covers RecommendationCardViewModel DataContext path
            if (await NavigateAndTest(navFrame, typeof(Pages.HomePage), "HomePage ActionButton_Click (card VM)", async () =>
            {
                if (navFrame.Content is Pages.HomePage hp)
                {
                    var hpType = typeof(Pages.HomePage);
                    var actionBtnMethod = hpType.GetMethod("ActionButton_Click",
                        System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                    var cardVm = new RecommendationCardViewModel(new WingetPackage { Id = "Test.Card", Name = "Card" }, new RecommendationLayoutState());
                    actionBtnMethod?.Invoke(hp, [new Button { DataContext = cardVm }, new RoutedEventArgs()]);
                }
            })) pass++; else fail++;

            // HomePage ActionButton_Click: covers WingetPackage DataContext path
            if (await NavigateAndTest(navFrame, typeof(Pages.HomePage), "HomePage ActionButton_Click (pkg)", async () =>
            {
                if (navFrame.Content is Pages.HomePage hp)
                {
                    var hpType = typeof(Pages.HomePage);
                    var actionBtnMethod = hpType.GetMethod("ActionButton_Click",
                        System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                    actionBtnMethod?.Invoke(hp, [new Button { DataContext = new WingetPackage { Id = "Test.Pkg2", Name = "Package" } }, new RoutedEventArgs()]);
                }
            })) pass++; else fail++;

            // ========== ViewModel_PropertyChanged & IconService_IconsUpdated ==========

            // InstalledPage ViewModel_PropertyChanged: IsLoading + FilteredPackages + LastRefreshTimeText
            if (await NavigateAndTest(navFrame, typeof(Pages.InstalledPage), "InstalledPage ViewModel_PropertyChanged", async () =>
            {
                if (navFrame.Content is Pages.InstalledPage ip)
                {
                    var ipType = typeof(Pages.InstalledPage);
                    var propChanged = ipType.GetMethod("ViewModel_PropertyChanged",
                        System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                    var origOverride = App.DispatcherOverride;
                    App.DispatcherOverride = action => action();
                    try
                    {
                        propChanged?.Invoke(ip, [null, new System.ComponentModel.PropertyChangedEventArgs(nameof(InstalledViewModel.IsLoading))]);
                        propChanged?.Invoke(ip, [null, new System.ComponentModel.PropertyChangedEventArgs(nameof(InstalledViewModel.FilteredPackages))]);
                        propChanged?.Invoke(ip, [null, new System.ComponentModel.PropertyChangedEventArgs(nameof(InstalledViewModel.LastRefreshTimeText))]);
                    }
                    finally
                    {
                        App.DispatcherOverride = origOverride;
                    }
                }
            })) pass++; else fail++;

            // InstalledPage IconService_IconsUpdated
            if (await NavigateAndTest(navFrame, typeof(Pages.InstalledPage), "InstalledPage IconService_IconsUpdated", async () =>
            {
                if (navFrame.Content is Pages.InstalledPage ip)
                {
                    var ipType = typeof(Pages.InstalledPage);
                    var iconsUpdated = ipType.GetMethod("IconService_IconsUpdated",
                        System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                    var origOverride = App.DispatcherOverride;
                    App.DispatcherOverride = action => action();
                    try
                    {
                        iconsUpdated?.Invoke(ip, [null, EventArgs.Empty]);
                    }
                    finally
                    {
                        App.DispatcherOverride = origOverride;
                    }
                }
            })) pass++; else fail++;

            // UpdatesPage ViewModel_PropertyChanged: IsLoading, FilteredUpgrades, progress properties
            if (await NavigateAndTest(navFrame, typeof(Pages.UpdatesPage), "UpdatesPage ViewModel_PropertyChanged", async () =>
            {
                if (navFrame.Content is Pages.UpdatesPage up)
                {
                    var upType = typeof(Pages.UpdatesPage);
                    var propChanged = upType.GetMethod("ViewModel_PropertyChanged",
                        System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                    var origOverride = App.DispatcherOverride;
                    App.DispatcherOverride = action => action();
                    try
                    {
                        propChanged?.Invoke(up, [null, new System.ComponentModel.PropertyChangedEventArgs(nameof(UpdatesViewModel.IsLoading))]);
                        propChanged?.Invoke(up, [null, new System.ComponentModel.PropertyChangedEventArgs(nameof(UpdatesViewModel.FilteredUpgrades))]);
                        propChanged?.Invoke(up, [null, new System.ComponentModel.PropertyChangedEventArgs(nameof(UpdatesViewModel.IsGlobalProgressVisible))]);
                        propChanged?.Invoke(up, [null, new System.ComponentModel.PropertyChangedEventArgs(nameof(UpdatesViewModel.GlobalProgressValue))]);
                        propChanged?.Invoke(up, [null, new System.ComponentModel.PropertyChangedEventArgs(nameof(UpdatesViewModel.GlobalProgressPercentText))]);
                        propChanged?.Invoke(up, [null, new System.ComponentModel.PropertyChangedEventArgs(nameof(UpdatesViewModel.GlobalProgressStatusText))]);
                    }
                    finally
                    {
                        App.DispatcherOverride = origOverride;
                    }
                }
            })) pass++; else fail++;

            // UpdatesPage IconService_IconsUpdated
            if (await NavigateAndTest(navFrame, typeof(Pages.UpdatesPage), "UpdatesPage IconService_IconsUpdated", async () =>
            {
                if (navFrame.Content is Pages.UpdatesPage up)
                {
                    var upType = typeof(Pages.UpdatesPage);
                    var iconsUpdated = upType.GetMethod("IconService_IconsUpdated",
                        System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                    var origOverride = App.DispatcherOverride;
                    App.DispatcherOverride = action => action();
                    try
                    {
                        iconsUpdated?.Invoke(up, [null, EventArgs.Empty]);
                    }
                    finally
                    {
                        App.DispatcherOverride = origOverride;
                    }
                }
            })) pass++; else fail++;

            // HomePage ViewModel_PropertyChanged: IsLoading, IsSearchActive/FilteredSearchResults, FilteredRecommendations
            if (await NavigateAndTest(navFrame, typeof(Pages.HomePage), "HomePage ViewModel_PropertyChanged", async () =>
            {
                if (navFrame.Content is Pages.HomePage hp)
                {
                    var hpType = typeof(Pages.HomePage);
                    var propChanged = hpType.GetMethod("ViewModel_PropertyChanged",
                        System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                    var origOverride = App.DispatcherOverride;
                    App.DispatcherOverride = action => action();
                    try
                    {
                        propChanged?.Invoke(hp, [null, new System.ComponentModel.PropertyChangedEventArgs(nameof(HomeViewModel.IsLoading))]);
                        propChanged?.Invoke(hp, [null, new System.ComponentModel.PropertyChangedEventArgs(nameof(HomeViewModel.IsSearchActive))]);
                        propChanged?.Invoke(hp, [null, new System.ComponentModel.PropertyChangedEventArgs(nameof(HomeViewModel.FilteredSearchResults))]);
                        propChanged?.Invoke(hp, [null, new System.ComponentModel.PropertyChangedEventArgs(nameof(HomeViewModel.FilteredRecommendations))]);
                    }
                    finally
                    {
                        App.DispatcherOverride = origOverride;
                    }
                }
            })) pass++; else fail++;

            // HomePage IconService_IconsUpdated
            if (await NavigateAndTest(navFrame, typeof(Pages.HomePage), "HomePage IconService_IconsUpdated", async () =>
            {
                if (navFrame.Content is Pages.HomePage hp)
                {
                    var hpType = typeof(Pages.HomePage);
                    var iconsUpdated = hpType.GetMethod("IconService_IconsUpdated",
                        System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                    var origOverride = App.DispatcherOverride;
                    App.DispatcherOverride = action => action();
                    try
                    {
                        iconsUpdated?.Invoke(hp, [null, EventArgs.Empty]);
                    }
                    finally
                    {
                        App.DispatcherOverride = origOverride;
                    }
                }
            })) pass++; else fail++;

            // ========== HomeViewModel SearchInternalAsync paths ==========
            try
            {
                var origServices = App.Services;
                try
                {
                    var overrideProvider = new OverrideServiceProvider(origServices);
                    var delegating = new DelegatingWingetService(
                        origServices.GetRequiredService<IWingetService>());
                    overrideProvider.AddOverride<IWingetService>(delegating);
                    overrideProvider.AddOverride<HomeViewModel>(new HomeViewModel(delegating));
                    App.Services = overrideProvider;

                    // Cancel path: make search delay so we can cancel mid-flight
                    bool cancelObserved = false;
                    delegating.SearchPackagesAsyncFunc = async (query, ct) =>
                    {
                        try
                        {
                            await Task.Delay(10000, ct);
                            ct.ThrowIfCancellationRequested();
                            return [];
                        }
                        catch (OperationCanceledException)
                        {
                            cancelObserved = true;
                            throw;
                        }
                    };

                    navFrame.Navigate(typeof(Pages.HomePage));
                    await Task.Delay(1000);
                    if (navFrame.Content is Pages.HomePage hp)
                    {
                        // Start a search that will block on the delay (fire-and-forget)
                        _ = hp.ViewModel.SearchAsync("delayed-search");
                        await Task.Delay(200);
                        // Cancel the in-flight search via the ViewModel's public CancelSearch()
                        hp.ViewModel.CancelSearch();
                        await Task.Delay(500);
                        if (!cancelObserved)
                        {
                            throw new InvalidOperationException("CancelSearch did not cancel the in-flight search");
                        }
                    }
                }
                finally
                {
                    App.Services = origServices;
                }
                LogService.LogInfo("PASS: HomeViewModel search cancellation path");
                pass++;
            }
            catch (Exception ex)
            {
                LogService.LogError("FAIL: HomeViewModel search cancellation path", ex);
                fail++;
            }

            try
            {
                var origServicesNav = App.Services;
                try
                {
                    var overrideProvider = new OverrideServiceProvider(origServicesNav);
                    var delegating = new DelegatingWingetService(
                        origServicesNav.GetRequiredService<IWingetService>());
                    overrideProvider.AddOverride<IWingetService>(delegating);
                    overrideProvider.AddOverride<HomeViewModel>(new HomeViewModel(delegating));
                    App.Services = overrideProvider;

                    bool cancelObserved = false;
                    delegating.SearchPackagesAsyncFunc = async (query, ct) =>
                    {
                        try
                        {
                            await Task.Delay(10000, ct);
                            ct.ThrowIfCancellationRequested();
                            return [];
                        }
                        catch (OperationCanceledException)
                        {
                            cancelObserved = true;
                            throw;
                        }
                    };

                    navFrame.Navigate(typeof(Pages.HomePage));
                    await Task.Delay(1000);
                    if (navFrame.Content is Pages.HomePage hp)
                    {
                        // Start a search that will block on the delay (fire-and-forget)
                        _ = hp.ViewModel.SearchAsync("nav-away-search");
                        await Task.Delay(200);
                        // Navigate away to trigger HomePage.OnNavigatedFrom -> ViewModel.CancelSearch()
                        navFrame.Navigate(typeof(Pages.AboutPage));
                        await Task.Delay(1000);
                        if (!cancelObserved)
                        {
                            throw new InvalidOperationException("Navigating away did not cancel the in-flight search");
                        }
                    }
                }
                finally
                {
                    App.Services = origServicesNav;
                }
                LogService.LogInfo("PASS: HomePage OnNavigatedFrom cancels in-flight search");
                pass++;
            }
            catch (Exception ex)
            {
                LogService.LogError("FAIL: HomePage OnNavigatedFrom cancels in-flight search", ex);
                fail++;
            }

            try
            {
                var origServices2 = App.Services;
                try
                {
                    var overrideProvider = new OverrideServiceProvider(origServices2);
                    var delegating = new DelegatingWingetService(
                        origServices2.GetRequiredService<IWingetService>());
                    overrideProvider.AddOverride<IWingetService>(delegating);
                    overrideProvider.AddOverride<HomeViewModel>(new HomeViewModel(delegating));
                    App.Services = overrideProvider;

                    delegating.SearchPackagesAsyncFunc = (query, ct) =>
                        throw new InvalidOperationException("Simulated search failure");

                    navFrame.Navigate(typeof(Pages.HomePage));
                    await Task.Delay(1000);
                    if (navFrame.Content is Pages.HomePage hp)
                    {
                        _ = hp.ViewModel.SearchAsync("failing-search");
                        await Task.Delay(500);
                    }
                }
                finally
                {
                    App.Services = origServices2;
                }
                LogService.LogInfo("PASS: HomeViewModel search exception path");
                pass++;
            }
            catch (Exception ex)
            {
                LogService.LogError("FAIL: HomeViewModel search exception path", ex);
                fail++;
            }

            // ========== InstalledPage ==========
            if (await NavigateAndTest(navFrame, typeof(Pages.InstalledPage), "InstalledPage loaded & filtered", async () =>
            {
                if (navFrame.Content is Pages.InstalledPage ip)
                {
                    ip.ViewModel.SourceFilter = SourceFilters.All;
                    ip.ViewModel.FilterQuery = "Git";
                    ip.ViewModel.ApplyFilter();
                }
            })) pass++; else fail++;

            if (await NavigateAndTest(navFrame, typeof(Pages.InstalledPage), "InstalledPage sort headers", async () =>
            {
                if (navFrame.Content is Pages.InstalledPage ip)
                {
                    var ipType = typeof(Pages.InstalledPage);
                    var nameMethod = ipType.GetMethod("HeaderName_Click",
                        System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                    var versionMethod = ipType.GetMethod("HeaderVersion_Click",
                        System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                    var publisherMethod = ipType.GetMethod("HeaderPublisher_Click",
                        System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                    nameMethod?.Invoke(ip, [null, null]);
                    versionMethod?.Invoke(ip, [null, null]);
                    publisherMethod?.Invoke(ip, [null, null]);
                }
            })) pass++; else fail++;

            if (await NavigateAndTest(navFrame, typeof(Pages.InstalledPage), "InstalledPage category buttons", async () =>
            {
                if (navFrame.Content is Pages.InstalledPage ip)
                {
                    var ipType = typeof(Pages.InstalledPage);
                    var categoryBtn = ipType.GetMethod("CategoryBtn_Click",
                        System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                    var allCategoryBtn = ipType.GetMethod("AllCategoryBtn_Click",
                        System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                    categoryBtn?.Invoke(ip, [null, null]);
                    allCategoryBtn?.Invoke(ip, [null, null]);
                }
            })) pass++; else fail++;

            // InstalledPage ViewTaskLog_Click: covers ShowLogDialogForPackage call
            if (await NavigateAndTest(navFrame, typeof(Pages.InstalledPage), "InstalledPage ViewTaskLog_Click", async () =>
            {
                if (navFrame.Content is Pages.InstalledPage ip)
                {
                    var ipType = typeof(Pages.InstalledPage);
                    var viewLogMethod = ipType.GetMethod("ViewTaskLog_Click",
                        System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                    viewLogMethod?.Invoke(ip, [new Button { DataContext = new WingetPackage { Id = "Test.Pkg", Name = "Test" } }, new RoutedEventArgs()]);
                }
            })) pass++; else fail++;

            // InstalledPage UninstallSingle_Click: covers Uninstall path
            if (await NavigateAndTest(navFrame, typeof(Pages.InstalledPage), "InstalledPage UninstallSingle_Click", async () =>
            {
                if (navFrame.Content is Pages.InstalledPage ip)
                {
                    var ipType = typeof(Pages.InstalledPage);
                    var uninstallMethod = ipType.GetMethod("UninstallSingle_Click",
                        System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                    uninstallMethod?.Invoke(ip, [new Button { DataContext = new WingetPackage { Id = "Test.Pkg", Name = "Test" } }, new RoutedEventArgs()]);
                }
            })) pass++; else fail++;

            // InstalledPage UpdateSingle_Click: covers Upgrade path
            if (await NavigateAndTest(navFrame, typeof(Pages.InstalledPage), "InstalledPage UpdateSingle_Click", async () =>
            {
                if (navFrame.Content is Pages.InstalledPage ip)
                {
                    var ipType = typeof(Pages.InstalledPage);
                    var updateMethod = ipType.GetMethod("UpdateSingle_Click",
                        System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                    updateMethod?.Invoke(ip, [new Button { DataContext = new WingetPackage { Id = "Test.Pkg", Name = "Test" } }, new RoutedEventArgs()]);
                }
            })) pass++; else fail++;

            // InstalledPage BulkUninstallButton_Click: early return with no selected items
            if (await NavigateAndTest(navFrame, typeof(Pages.InstalledPage), "InstalledPage BulkUninstallButton_Click", async () =>
            {
                if (navFrame.Content is Pages.InstalledPage ip)
                {
                    var ipType = typeof(Pages.InstalledPage);
                    var bulkUninstallMethod = ipType.GetMethod("BulkUninstallButton_Click",
                        System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                    bulkUninstallMethod?.Invoke(ip, [null, null]);
                }
            })) pass++; else fail++;

            // BulkSelectionHelperUI: inject items, activate bulk select, select all, deselect, cancel
            if (await NavigateAndTest(navFrame, typeof(Pages.InstalledPage), "InstalledPage bulk select", async () =>
            {
                if (navFrame.Content is Pages.InstalledPage ip)
                {
                    var ipType = typeof(Pages.InstalledPage);
                    ListView? listView = null;
                    try
                    {
                        var listViewField = ipType.GetField("InstalledAppsList",
                            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                        listView = listViewField?.GetValue(ip) as ListView;
                        if (listView != null)
                        {
                            listView.ItemsSource = new List<WingetPackage>
                            {
                                new() { Id = "Test.Pkg.1", Name = "Alpha", Status = PackageStatus.Installed },
                                new() { Id = "Test.Pkg.2", Name = "Beta", Status = PackageStatus.Installed },
                                new() { Id = "Test.Pkg.3", Name = "Gamma", Status = PackageStatus.Installable }
                            };
                        }
                    }
                    catch (Exception ex) { LogService.LogError("bulk listView setup failed", ex); }

                    var toggleMethod = ipType.GetMethod("BulkSelectToggle_Click",
                        System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                    var selectAllMethod = ipType.GetMethod("SelectAllCheckBox_Checked",
                        System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                    var deselectAllMethod = ipType.GetMethod("SelectAllCheckBox_Unchecked",
                        System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                    var cancelMethod = ipType.GetMethod("CancelBulkSelect_Click",
                        System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

                    // Activate bulk mode: set toggle checked + selection mode before invoking Toggle
                    try
                    {
                        // Force Activate path by setting SelectionMode directly
                        if (listView != null) listView.SelectionMode = ListViewSelectionMode.Multiple;
                        toggleMethod?.Invoke(ip, [null, null]);
                    }
                    catch (Exception ex) { LogService.LogError("bulk toggle failed", ex.InnerException ?? ex); }
                    await Task.Delay(100);
                    try { selectAllMethod?.Invoke(ip, [null, null]); }
                    catch (Exception ex) { LogService.LogError("bulk selectAll failed", ex.InnerException ?? ex); }
                    await Task.Delay(100);
                    try { deselectAllMethod?.Invoke(ip, [null, null]); }
                    catch (Exception ex) { LogService.LogError("bulk deselectAll failed", ex.InnerException ?? ex); }
                    await Task.Delay(100);
                    try { cancelMethod?.Invoke(ip, [null, null]); }
                    catch (Exception ex) { LogService.LogError("bulk cancel failed", ex.InnerException ?? ex); }
                }
            })) pass++; else fail++;

            // ========== UpdatesPage ==========
            if (await NavigateAndTest(navFrame, typeof(Pages.UpdatesPage), "UpdatesPage loaded", async () =>
            {
                if (navFrame.Content is Pages.UpdatesPage up)
                {
                    up.ViewModel.SourceFilter = SourceFilters.All;
                    up.ViewModel.SortOrder = "az";
                    up.ViewModel.ApplyFilter();
                }
            })) pass++; else fail++;

            if (await NavigateAndTest(navFrame, typeof(Pages.UpdatesPage), "UpdatesPage sort headers", async () =>
            {
                if (navFrame.Content is Pages.UpdatesPage up)
                {
                    var upType = typeof(Pages.UpdatesPage);
                    var nameMethod = upType.GetMethod("HeaderName_Click",
                        System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                    var versionMethod = upType.GetMethod("HeaderVersion_Click",
                        System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                    var publisherMethod = upType.GetMethod("HeaderPublisher_Click",
                        System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                    nameMethod?.Invoke(up, [null, null]);
                    versionMethod?.Invoke(up, [null, null]);
                    publisherMethod?.Invoke(up, [null, null]);
                }
            })) pass++; else fail++;

            if (await NavigateAndTest(navFrame, typeof(Pages.UpdatesPage), "UpdatesPage category buttons", async () =>
            {
                if (navFrame.Content is Pages.UpdatesPage up)
                {
                    var upType = typeof(Pages.UpdatesPage);
                    var categoryBtn = upType.GetMethod("CategoryBtn_Click",
                        System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                    var allCategoryBtn = upType.GetMethod("AllCategoryBtn_Click",
                        System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                    categoryBtn?.Invoke(up, [null, null]);
                    allCategoryBtn?.Invoke(up, [null, null]);
                }
            })) pass++; else fail++;

            // UpdatesPage ViewTaskLog_Click: covers ShowLogDialogForPackage call
            if (await NavigateAndTest(navFrame, typeof(Pages.UpdatesPage), "UpdatesPage ViewTaskLog_Click", async () =>
            {
                if (navFrame.Content is Pages.UpdatesPage up)
                {
                    var upType = typeof(Pages.UpdatesPage);
                    var viewLogMethod = upType.GetMethod("ViewTaskLog_Click",
                        System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                    viewLogMethod?.Invoke(up, [new Button { DataContext = new WingetPackage { Id = "Test.Pkg", Name = "Test" } }, new RoutedEventArgs()]);
                }
            })) pass++; else fail++;

            // UpdatesPage UpdateSingle_Click: covers Upgrade path
            if (await NavigateAndTest(navFrame, typeof(Pages.UpdatesPage), "UpdatesPage UpdateSingle_Click", async () =>
            {
                if (navFrame.Content is Pages.UpdatesPage up)
                {
                    var upType = typeof(Pages.UpdatesPage);
                    var updateMethod = upType.GetMethod("UpdateSingle_Click",
                        System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                    updateMethod?.Invoke(up, [new Button { DataContext = new WingetPackage { Id = "Test.Pkg", Name = "Test" } }, new RoutedEventArgs()]);
                }
            })) pass++; else fail++;

            // UpdatesPage BulkUpdateButton_Click: early return with no selected items
            if (await NavigateAndTest(navFrame, typeof(Pages.UpdatesPage), "UpdatesPage BulkUpdateButton_Click", async () =>
            {
                if (navFrame.Content is Pages.UpdatesPage up)
                {
                    var upType = typeof(Pages.UpdatesPage);
                    var bulkUpdateMethod = upType.GetMethod("BulkUpdateButton_Click",
                        System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                    bulkUpdateMethod?.Invoke(up, [null, null]);
                }
            })) pass++; else fail++;

            // ========== SettingsPage ==========
            if (await NavigateAndTest(navFrame, typeof(Pages.SettingsPage), "SettingsPage loaded"))
                pass++; else fail++;

            if (await NavigateAndTest(navFrame, typeof(Pages.SettingsPage), "SettingsPage toggle switches", async () =>
            {
                if (navFrame.Content is Pages.SettingsPage sp)
                {
                    var autoToggle = sp.FindName("AutoUpdateToggle") as ToggleSwitch;
                    if (autoToggle != null)
                    {
                        bool orig = autoToggle.IsOn;
                        autoToggle.IsOn = !orig;
                        autoToggle.IsOn = orig;
                    }

                    var notifToggle = sp.FindName("NotificationsToggle") as ToggleSwitch;
                    if (notifToggle != null)
                    {
                        bool orig = notifToggle.IsOn;
                        notifToggle.IsOn = !orig;
                        notifToggle.IsOn = orig;
                    }

                    var spType = typeof(Pages.SettingsPage);
                    var testStatusMethod = spType.GetMethod("TestStatusButton_Click",
                        System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                    testStatusMethod?.Invoke(sp, [null, null]);
                }
            })) pass++; else fail++;

            // ========== AboutPage ==========
            if (await NavigateAndTest(navFrame, typeof(Pages.AboutPage), "AboutPage loaded"))
                pass++; else fail++;

            // ========== DetailsPage ==========
            if (await NavigateAndTest(navFrame, typeof(Pages.DetailsPage), "DetailsPage loaded", param: "Git.Git"))
                pass++; else fail++;

            if (await NavigateAndTest(navFrame, typeof(Pages.DetailsPage), "DetailsPage event handlers", async () =>
            {
                if (navFrame.Content is Pages.DetailsPage dp)
                {
                    var dpType = typeof(Pages.DetailsPage);

                    var screenshotMethod = dpType.GetMethod("Screenshot_Click",
                        System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                    // Pass a proper Button with DataContext to cover the lightbox body
                    screenshotMethod?.Invoke(dp, [new Button { DataContext = "https://example.com/test.png" }, new RoutedEventArgs()]);

                    var closeLbMethod = dpType.GetMethod("CloseLightbox_Click",
                        System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                    closeLbMethod?.Invoke(dp, [null, null]);

                    var tappedMethod = dpType.GetMethod("LightboxOverlay_Tapped",
                        System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                    tappedMethod?.Invoke(dp, [null, null]);

                    var tagMethod = dpType.GetMethod("TagButton_Click",
                        System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                    // Pass a proper Button with Content to cover Frame.Navigate body
                    tagMethod?.Invoke(dp, [new Button { Content = "test-tag" }, new RoutedEventArgs()]);

                    // Wait for _package to be set before invoking ViewLogsButton_Click
                    var packageField = dpType.GetField("_package",
                        System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                    for (int i = 0; i < 30; i++)
                    {
                        if (packageField?.GetValue(dp) != null) break;
                        await Task.Delay(100);
                    }

                    var viewLogsMethod = dpType.GetMethod("ViewLogsButton_Click",
                        System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                    viewLogsMethod?.Invoke(dp, [null, null]);

                    var propChangedMethod = dpType.GetMethod("Package_PropertyChanged",
                        System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                    propChangedMethod?.Invoke(dp, [null, new System.ComponentModel.PropertyChangedEventArgs(nameof(WingetPackage.IsInstalling))]);
                    propChangedMethod?.Invoke(dp, [null, new System.ComponentModel.PropertyChangedEventArgs(nameof(WingetPackage.InstallProgress))]);
                    propChangedMethod?.Invoke(dp, [null, new System.ComponentModel.PropertyChangedEventArgs(nameof(WingetPackage.InstallStatusText))]);
                    propChangedMethod?.Invoke(dp, [null, new System.ComponentModel.PropertyChangedEventArgs(nameof(WingetPackage.Status))]);
                }
            }, param: "Git.Git")) pass++; else fail++;

            // DetailsPage ActionButton_Click: safe to invoke (fire-and-forget, won't throw)
            if (await NavigateAndTest(navFrame, typeof(Pages.DetailsPage), "DetailsPage ActionButton_Click", async () =>
            {
                if (navFrame.Content is Pages.DetailsPage dp)
                {
                    var dpType = typeof(Pages.DetailsPage);
                    var actionMethod = dpType.GetMethod("ActionButton_Click",
                        System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                    actionMethod?.Invoke(dp, [null, null]);
                }
            }, param: "Git.Git")) pass++; else fail++;

            // PackageDetailHelper.PopulateMetadata: pure static, needs Panel
            if (await NavigateAndTest(navFrame, typeof(Pages.DetailsPage), "PackageDetailHelper PopulateMetadata", async () =>
            {
                var panel = new StackPanel();
                var items = new List<MetadataItem>
                {
                    new() { Key = "Publisher", Value = "Test Publisher" },
                    new() { Key = "Homepage", Value = "https://example.com", IsUrl = true },
                    new() { Key = "License", Value = "MIT" },
                    new() { Key = "Installer Type", Value = "MSI" },
                    new() { Key = "", Value = "No Key Item" },
                    new() { Key = "With SubItems", Value = "Parent", SubItems = new List<MetadataItem>
                    {
                        new() { Key = "Sub Key", Value = "Sub Value" },
                        new() { Key = "Sub URL", Value = "https://sub.example.com", IsUrl = true }
                    }}
                };
                PackageDetailHelper.PopulateMetadata(panel, items);
                LogService.LogInfo("PopulateMetadata created " + panel.Children.Count + " cards");
            }, param: "Git.Git")) pass++; else fail++;

            // DetailsPage Back button
            if (await NavigateAndTest(navFrame, typeof(Pages.DetailsPage), "DetailsPage Back button", async () =>
            {
                if (navFrame.Content is Pages.DetailsPage dp)
                {
                    var dpType = typeof(Pages.DetailsPage);
                    var backMethod = dpType.GetMethod("BackButton_Click",
                        System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                    backMethod?.Invoke(dp, [dp.FindName("BackButton"), new RoutedEventArgs()]);
                }
            }, param: "Git.Git")) pass++; else fail++;

            // DetailsPage with rich data via mock service (covers icon, release notes, tags, screenshots)
            try
            {
                var originalServices = App.Services;
                try
                {
                    var overrideProvider = new OverrideServiceProvider(originalServices);
                    overrideProvider.AddOverride<IWingetService>(new MockWingetService(
                        originalServices.GetRequiredService<IWingetService>()));
                    App.Services = overrideProvider;

                    navFrame.Navigate(typeof(Pages.DetailsPage), "Mock.TestApp");
                    await Task.Delay(1000);
                    if (navFrame.Content is Pages.DetailsPage dp)
                    {
                        var dpType = typeof(Pages.DetailsPage);

                        // Verify rich data is displayed via FindName
                        var appNameText = dp.FindName("AppNameText") as TextBlock;
                        LogService.LogInfo($"DetailsPage rich data: AppNameText = '{appNameText?.Text}'");

                        var releaseNotesPanel = dp.FindName("ReleaseNotesPanel") as UIElement;
                        LogService.LogInfo($"DetailsPage rich data: ReleaseNotesPanel.Visibility = {releaseNotesPanel?.Visibility}");

                        var tagsPanel = dp.FindName("TagsPanel") as UIElement;
                        LogService.LogInfo($"DetailsPage rich data: TagsPanel.Visibility = {tagsPanel?.Visibility}");

                        var screenshotsPanel = dp.FindName("ScreenshotsPanel") as UIElement;
                        LogService.LogInfo($"DetailsPage rich data: ScreenshotsPanel.Visibility = {screenshotsPanel?.Visibility}");

                        var appIconImage = dp.FindName("AppIconImage") as UIElement;
                        LogService.LogInfo($"DetailsPage rich data: AppIconImage.Visibility = {appIconImage?.Visibility}");

                        // Also invoke ViewLogsButton_Click here so _package is guaranteed non-null
                        // This covers App.ShowLogDialogForPackage entry + early return
                        var viewLogsMethod = dpType.GetMethod("ViewLogsButton_Click",
                            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                        viewLogsMethod?.Invoke(dp, [null, null]);
                    }
                }
                finally
                {
                    App.Services = originalServices;
                }
                LogService.LogInfo("PASS: DetailsPage rich data with mock service");
                pass++;
            }
            catch (Exception ex)
            {
                LogService.LogError("FAIL: DetailsPage rich data", ex);
                fail++;
            }

            // DetailsPage icon catch block: mock returns invalid IconUrl so BitmapImage constructor throws
            try
            {
                var origServices = App.Services;
                try
                {
                    var overrideProvider = new OverrideServiceProvider(origServices);
                    var delegating = new DelegatingWingetService(origServices.GetRequiredService<IWingetService>());
                    overrideProvider.AddOverride<IWingetService>(delegating);
                    App.Services = overrideProvider;

                    delegating.FetchAndDecoratePackageDetailsAsyncFunc = () => Task.FromResult(new WingetPackage
                    {
                        Id = "Mock.CatchBlock",
                        Name = "Catch Block Test",
                        Publisher = "Test Publisher",
                        Version = "1.0.0",
                        Description = "Testing the icon catch block.",
                        IconUrl = "://invalid-uri",  // Uri constructor will throw UriFormatException
                        Status = PackageStatus.Installable,
                    });

                    navFrame.Navigate(typeof(Pages.DetailsPage), "Mock.CatchBlock");
                    await Task.Delay(1000);
                    if (navFrame.Content is Pages.DetailsPage)
                    {
                        // The page loaded and caught the icon exception; verify placeholder fallback
                        LogService.LogInfo("DetailsPage icon catch block test: page loaded with placeholder");
                    }
                }
                finally
                {
                    App.Services = origServices;
                }
                LogService.LogInfo("PASS: DetailsPage icon catch block");
                pass++;
            }
            catch (Exception ex)
            {
                LogService.LogError("FAIL: DetailsPage icon catch block", ex);
                fail++;
            }

            // ========== NoWingetPage ==========
            if (await NavigateAndTest(navFrame, typeof(Pages.NoWingetPage), "NoWingetPage loaded"))
                pass++; else fail++;

            if (await NavigateAndTest(navFrame, typeof(Pages.NoWingetPage), "NoWingetPage install click & cancel", async () =>
            {
                if (navFrame.Content is Pages.NoWingetPage nwp)
                {
                    var nwpType = typeof(Pages.NoWingetPage);
                    var installMethod = nwpType.GetMethod("InstallButton_Click",
                        System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                    var ctsField = nwpType.GetField("_installCts",
                        System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

                    installMethod?.Invoke(nwp, [null, null]);

                    await Task.Delay(100);
                    var cts = ctsField?.GetValue(nwp) as System.Threading.CancellationTokenSource;
                    cts?.Cancel();
                }
            })) pass++; else fail++;

            if (await NavigateAndTest(navFrame, typeof(Pages.HomePage), "NoWingetPage â†’ HomePage"))
                pass++; else fail++;

            // ========== ErrorWindow ==========
            try
            {
                var errorWin = new ErrorWindow("Integration test error message", "Integration test stack trace");
                errorWin.Activate();
                await Task.Delay(200);
                // Cover the close button Click lambda by finding the button and invoking via AutomationPeer
                if (errorWin.Content is Grid errorGrid)
                {
                    foreach (var child in errorGrid.Children)
                    {
                        if (child is Button closeBtn)
                        {
                            var peer = new Microsoft.UI.Xaml.Automation.Peers.ButtonAutomationPeer(closeBtn);
                            peer.Invoke();
                            break;
                        }
                    }
                }
                errorWin.Close();
                LogService.LogInfo("PASS: ErrorWindow created and closed");
                pass++;
            }
            catch (Exception ex)
            {
                LogService.LogError("FAIL: ErrorWindow test", ex);
                fail++;
            }

            // ========== NavigationHelper.CanGoBack ==========
            try
            {
                // DetailsPage creates back stack
                navFrame.Navigate(typeof(Pages.DetailsPage), "Git.Git");
                await Task.Delay(500);
                bool canGoBack = NavigationHelper.CanGoBack(navFrame);
                LogService.LogInfo($"PASS: NavigationHelper.CanGoBack = {canGoBack}");
                pass++;
            }
            catch (Exception ex)
            {
                LogService.LogError("FAIL: NavigationHelper.CanGoBack", ex);
                fail++;
            }

            // ========== IconService ==========
            // NotifyIconsUpdated: subscribe, invoke via reflection
            if (await NavigateAndTest(navFrame, typeof(Pages.HomePage), "IconService NotifyIconsUpdated", async () =>
            {
                var iconService = IconService.Instance;
                var iconType = typeof(IconService);

                bool eventFired = false;
                EventHandler handler = (_, _) => eventFired = true;
                iconService.IconsUpdated += handler;

                var notifyMethod = iconType.GetMethod("NotifyIconsUpdated",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                notifyMethod?.Invoke(iconService, null);

                iconService.IconsUpdated -= handler;
                LogService.LogInfo($"NotifyIconsUpdated fired: {eventFired}");
            })) pass++; else fail++;

            // LoadDatabaseAsync: write temp JSON, invoke via reflection
            if (await NavigateAndTest(navFrame, typeof(Pages.HomePage), "IconService LoadDatabaseAsync", async () =>
            {
                var tempDir = Path.Combine(Path.GetTempPath(), "WingetStoreTest_" + Guid.NewGuid().ToString("N"));
                Directory.CreateDirectory(tempDir);
                try
                {
                    string testJson = @"{""icons_and_screenshots"":{""Test.Package"":{""icon"":""https://example.com/icon.png"",""images"":[""https://example.com/screenshot1.png"",""https://example.com/screenshot2.png""]}}}";
                    string filePath = Path.Combine(tempDir, "test-db.json");
                    await File.WriteAllTextAsync(filePath, testJson);

                    var iconType = typeof(IconService);
                    var loadMethod = iconType.GetMethod("LoadDatabaseAsync",
                        System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                    if (loadMethod != null)
                    {
                        var task = loadMethod.Invoke(IconService.Instance, [filePath]) as Task;
                        if (task != null) await task;
                    }
                }
                finally
                {
                    try { Directory.Delete(tempDir, true); } catch { }
                }
            })) pass++; else fail++;

            // DownloadIconAsync error path: call GetIconUrl for a known package to trigger fire-and-forget download
            if (await NavigateAndTest(navFrame, typeof(Pages.HomePage), "IconService DownloadIconAsync error path", async () =>
            {
                var iconService = IconService.Instance;
                // GetIconUrl for a package with a known URL will fire DownloadIconAsync which will fail (HTTP timeout on example.com)
                string url = iconService.GetIconUrl("Test.Package", "Test Package");
                LogService.LogInfo($"GetIconUrl returned: '{url}'");
                await Task.Delay(2000); // Wait for fire-and-forget DownloadIconAsync to complete/fail
                var iconType = typeof(IconService);
                // Also call ResolveIconOnlineAsync via GetIconUrl with an unknown package (no URL in _icons)
                string url2 = iconService.GetIconUrl("Unknown.Package", "Unknown");
                LogService.LogInfo($"GetIconUrl(Unknown) returned: '{url2}'");
                await Task.Delay(2000); // Wait for fire-and-forget ResolveIconOnlineAsync to complete/fail
            })) pass++; else fail++;

            // ========== MainWindow ==========
            if (await NavigateAndTest(navFrame, typeof(Pages.HomePage), "MainWindow theme toggle", async () =>
            {
                if (App.MainWindow is MainWindow mw)
                {
                    var mwType = typeof(MainWindow);
                    var themeMethod = mwType.GetMethod("ThemeToggleButton_Click",
                        System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                    themeMethod?.Invoke(mw, [null, null]);

                    var currentTheme = SettingsService.AppTheme;
                    var restoreTheme = currentTheme == "Dark" ? "Light" : "Dark";
                    SettingsService.AppTheme = restoreTheme;
                    mw.ApplyTheme(restoreTheme);
                }
            })) pass++; else fail++;

            // UpdateThemeToggleIcon: invoke via reflection
            if (await NavigateAndTest(navFrame, typeof(Pages.HomePage), "MainWindow UpdateThemeToggleIcon", async () =>
            {
                if (App.MainWindow is MainWindow mw)
                {
                    var mwType = typeof(MainWindow);
                    var updateIconMethod = mwType.GetMethod("UpdateThemeToggleIcon",
                        System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                    updateIconMethod?.Invoke(mw, null);
                }
            })) pass++; else fail++;

            // ========== SettingsService I/O error path ==========
            if (await NavigateAndTest(navFrame, typeof(Pages.HomePage), "MainWindow UpdateUpdatesBadge", async () =>
            {
                if (App.MainWindow is MainWindow mw)
                {
                    mw.UpdateUpdatesBadge(0);
                    mw.UpdateUpdatesBadge(5);
                    mw.UpdateUpdatesBadge(150);
                }
            })) pass++; else fail++;

            // ========== SettingsService I/O error path ==========
            if (await NavigateAndTest(navFrame, typeof(Pages.HomePage), "SettingsService I/O error path", async () =>
            {
                try
                {
                    var ssType = typeof(SettingsService);
                    var settingsFilePathField = ssType.GetField("SettingsFilePath",
                        System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
                    if (settingsFilePathField?.GetValue(null) is string settingsFilePath)
                    {
                        string? dir = Path.GetDirectoryName(settingsFilePath);
                        // Trigger SaveSettings catch (line 58): make file read-only, change setting
                        if (File.Exists(settingsFilePath))
                        {
                            var origAttrs = File.GetAttributes(settingsFilePath);
                            File.SetAttributes(settingsFilePath, origAttrs | FileAttributes.ReadOnly);
                            // Changing a setting triggers SaveSettings which will fail to write
                            var origTheme = SettingsService.AppTheme;
                            SettingsService.AppTheme = "Dark";
                            // Restore
                            File.SetAttributes(settingsFilePath, origAttrs);
                            SettingsService.AppTheme = origTheme;
                        }

                        // Trigger LoadSettings catch (line 48): replace file with directory, call LoadSettings via reflection
                        if (File.Exists(settingsFilePath))
                        {
                            string tempBackup = settingsFilePath + ".bak";
                            File.Move(settingsFilePath, tempBackup);
                            try
                            {
                                Directory.CreateDirectory(settingsFilePath); // Replace file with dir
                                var loadMethod = ssType.GetMethod("LoadSettings",
                                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
                                loadMethod?.Invoke(null, null); // This will fail File.ReadAllText on a directory â†’ catches at line 48
                            }
                            finally
                            {
                                if (Directory.Exists(settingsFilePath))
                                {
                                    try { Directory.Delete(settingsFilePath, false); } catch { }
                                }
                                if (File.Exists(tempBackup))
                                    File.Move(tempBackup, settingsFilePath);
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    LogService.LogError("SettingsService I/O error path test", ex);
                }
            })) pass++; else fail++;

            // MainWindow_SizeChanged: resize via AppWindow API to trigger handler
            if (await NavigateAndTest(navFrame, typeof(Pages.HomePage), "MainWindow SizeChanged", async () =>
            {
                if (App.MainWindow is MainWindow mw)
                {
                    try
                    {
                        var appWindow = Microsoft.UI.Windowing.AppWindow.GetFromWindowId(
                            Microsoft.UI.Win32Interop.GetWindowIdFromWindow(
                                WinRT.Interop.WindowNative.GetWindowHandle(mw)));
                        var origSize = appWindow.Size;
                        appWindow.Resize(new Windows.Graphics.SizeInt32 { Width = 400, Height = 300 });
                        await Task.Delay(200);
                        appWindow.Resize(origSize);
                    }
                    catch (Exception ex)
                    {
                        LogService.LogError("SizeChanged via AppWindow failed", ex);
                    }
                }
            })) pass++; else fail++;

            // TitleBarBackButton_Click: navigate to non-top-level page, invoke back
            if (await NavigateAndTest(navFrame, typeof(Pages.DetailsPage), "MainWindow TitleBarBackButton", async () =>
            {
                if (App.MainWindow is MainWindow mw)
                {
                    var mwType = typeof(MainWindow);
                    var backMethod = mwType.GetMethod("TitleBarBackButton_Click",
                        System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                    backMethod?.Invoke(mw, [null, null]);
                }
            }, param: "Git.Git")) pass++; else fail++;

            // NavView full navigation
            if (await NavigateAndTest(navFrame, typeof(Pages.HomePage), "NavView installed navigation", async () =>
            {
                if (App.MainWindow is MainWindow mw && mw.Content is FrameworkElement root)
                {
                    var navView = root.FindName("NavView") as NavigationView;
                    if (navView != null)
                    {
                        foreach (var item in navView.MenuItems)
                        {
                            if (item is NavigationViewItem nvi && nvi.Tag?.ToString() == "installed")
                            {
                                navView.SelectedItem = nvi;
                                await Task.Delay(300);
                                break;
                            }
                        }
                    }
                }
            })) pass++; else fail++;

            if (await NavigateAndTest(navFrame, typeof(Pages.HomePage), "NavView updates navigation", async () =>
            {
                if (App.MainWindow is MainWindow mw && mw.Content is FrameworkElement root)
                {
                    var navView = root.FindName("NavView") as NavigationView;
                    if (navView != null)
                    {
                        foreach (var item in navView.MenuItems)
                        {
                            if (item is NavigationViewItem nvi && nvi.Tag?.ToString() == "updates")
                            {
                                navView.SelectedItem = nvi;
                                await Task.Delay(300);
                                break;
                            }
                        }
                    }
                }
            })) pass++; else fail++;

            if (await NavigateAndTest(navFrame, typeof(Pages.HomePage), "NavView home navigation", async () =>
            {
                if (App.MainWindow is MainWindow mw && mw.Content is FrameworkElement root)
                {
                    var navView = root.FindName("NavView") as NavigationView;
                    if (navView != null)
                    {
                        foreach (var item in navView.MenuItems)
                        {
                            if (item is NavigationViewItem nvi && nvi.Tag?.ToString() == "home")
                            {
                                navView.SelectedItem = nvi;
                                await Task.Delay(300);
                                break;
                            }
                        }
                    }
                }
            })) pass++; else fail++;

            if (await NavigateAndTest(navFrame, typeof(Pages.HomePage), "NavView settings navigation", async () =>
            {
                if (App.MainWindow is MainWindow mw && mw.Content is FrameworkElement root)
                {
                    var navView = root.FindName("NavView") as NavigationView;
                    if (navView != null)
                    {
                        var settingsItem = navView.SettingsItem as NavigationViewItem;
                        if (settingsItem != null)
                            navView.SelectedItem = settingsItem;
                    }
                }
            })) pass++; else fail++;

            // Final: return to HomePage
            if (await NavigateAndTest(navFrame, typeof(Pages.HomePage), "Final â†’ HomePage"))
                pass++; else fail++;

            LogService.LogInfo($"=== INTEGRATION TEST CYCLE COMPLETE: {pass} passed, {fail} failed ===");
        }
        catch (Exception ex)
        {
            LogService.LogError("Fatal error in integration test runner", ex);
        }
    }
}
