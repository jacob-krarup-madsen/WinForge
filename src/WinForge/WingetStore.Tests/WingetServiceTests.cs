namespace WingetStore.Tests;

public class WingetServiceTests
{
    private static string AssetPath(string name) => Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", name);

    private static async Task<string?> BackupAssetAsync(string name)
    {
        var path = AssetPath(name);
        if (!File.Exists(path)) return null;
        var content = await File.ReadAllTextAsync(path);
        File.Delete(path);
        return content;
    }

    private static async Task RestoreAssetAsync(string name, string? backup)
    {
        if (backup == null) return;
        await File.WriteAllTextAsync(AssetPath(name), backup);
    }

    private static async Task WriteAssetAsync(string name, string content)
    {
        await File.WriteAllTextAsync(AssetPath(name), content);
    }

    [Fact]
    public void GetOrCreatePackage_CoreInstanceMerging()
    {
        var service = App.Winget;
        var pkg1 = new WingetPackage { Id = "Core.App.ID", Name = "Core App Version 1", Version = "1.0.0" };
        var pkg2 = new WingetPackage
        {
            Id = "Core.App.ID",
            Name = "Core App Version 2",
            Version = "2.0.0",
            AvailableVersion = "2.1.0",
            Source = "winget",
            Publisher = "Publisher",
            Status = PackageStatus.Installed,
            Description = "Desc",
            Homepage = "Home",
            License = "MIT",
            ReleaseNotes = "Notes",
            PublisherUrl = "PubUrl",
            InstallerType = "Nullsoft",
            InstallerUrl = "InstUrl",
            Tags = new List<string> { "tag1" },
            Details = new List<MetadataItem> { new MetadataItem { Key = "DetailKey" } },
            Screenshots = new List<string> { "https://screenshot.png" }
        };

        var cached1 = service.GetOrCreatePackage(pkg1);
        var cached2 = service.GetOrCreatePackage(pkg2);

        Assert.Same(cached1, cached2);
        Assert.Equal("Core App Version 2", cached1.Name);
        Assert.Equal("2.0.0", cached1.Version);
        Assert.Equal("2.1.0", cached1.AvailableVersion);
        Assert.Equal("winget", cached1.Source);
        Assert.Equal("Publisher", cached1.Publisher);
        Assert.Equal(PackageStatus.Installed, cached1.Status);
        Assert.Equal("Desc", cached1.Description);
        Assert.Equal("Home", cached1.Homepage);
        Assert.Equal("MIT", cached1.License);
        Assert.Equal("Notes", cached1.ReleaseNotes);
        Assert.Equal("PubUrl", cached1.PublisherUrl);
        Assert.Equal("Nullsoft", cached1.InstallerType);
        Assert.Equal("InstUrl", cached1.InstallerUrl);
        Assert.Single(cached1.Tags);
        Assert.Single(cached1.Details);
        Assert.Single(cached1.Screenshots);
    }

    [Fact]
    public void GetOrCreatePackage_ScreenshotsMerging()
    {
        var service = App.Winget;
        var pkg1 = new WingetPackage { Id = "Core.App.Screenshot", Name = "Core App" };
        var pkg2 = new WingetPackage
        {
            Id = "Core.App.Screenshot",
            Name = "Core App",
            Screenshots = new List<string> { "https://example.com/screenshot1.png" }
        };

        var cached1 = service.GetOrCreatePackage(pkg1);
        var cached2 = service.GetOrCreatePackage(pkg2);

        Assert.Same(cached1, cached2);
        Assert.True(cached1.HasScreenshots);
        Assert.Single(cached1.Screenshots);
        Assert.Equal("https://example.com/screenshot1.png", cached1.Screenshots[0]);
    }

    [Fact]
    public void GetOrCreatePackage_EmptyMerging()
    {
        var service = App.Winget;
        var pkg1 = new WingetPackage
        {
            Id = "Core.App.EmptyID",
            Name = "Core App Version 1",
            Version = "1.0.0",
            AvailableVersion = "2.1.0",
            Source = "winget",
            Publisher = "Publisher",
            Status = PackageStatus.Installed,
            Description = "Desc",
            Homepage = "Home",
            License = "MIT",
            ReleaseNotes = "Notes",
            PublisherUrl = "PubUrl",
            InstallerType = "Nullsoft",
            InstallerUrl = "InstUrl",
            Tags = new List<string> { "tag1" },
            Details = new List<MetadataItem> { new MetadataItem { Key = "DetailKey" } },
            Screenshots = new List<string> { "https://screenshot.png" }
        };
        var pkg2 = new WingetPackage
        {
            Id = "Core.App.EmptyID",
            Name = "Core App Version 1",
            Version = "",
            AvailableVersion = null!,
            Source = "",
            Publisher = null!,
            Status = PackageStatus.Installable,
            Description = "",
            Homepage = null!,
            License = "",
            ReleaseNotes = null!,
            PublisherUrl = "",
            InstallerType = null!,
            InstallerUrl = "",
            Tags = null!,
            Details = new List<MetadataItem>(),
            Screenshots = null!
        };

        var pkg3 = new WingetPackage
        {
            Id = "Core.App.EmptyID",
            Name = "Core App Version 1",
            Status = PackageStatus.Installable,
            Tags = new List<string>(),
            Details = null!,
            Screenshots = new List<string>()
        };

        var cached1 = service.GetOrCreatePackage(pkg1);
        var cached2 = service.GetOrCreatePackage(pkg2);
        var cached3 = service.GetOrCreatePackage(pkg3);

        Assert.Same(cached1, cached2);
        Assert.Same(cached1, cached3);
        Assert.Equal("1.0.0", cached1.Version);
        Assert.Equal("2.1.0", cached1.AvailableVersion);
        Assert.Equal("winget", cached1.Source);
        Assert.Equal("Core", cached1.Publisher);
        Assert.Equal(PackageStatus.Installed, cached1.Status);
        Assert.Equal("Desc", cached1.Description);
        Assert.Equal("Home", cached1.Homepage);
        Assert.Equal("MIT", cached1.License);
        Assert.Equal("Notes", cached1.ReleaseNotes);
        Assert.Equal("PubUrl", cached1.PublisherUrl);
        Assert.Equal("Nullsoft", cached1.InstallerType);
        Assert.Equal("InstUrl", cached1.InstallerUrl);
        Assert.Single(cached1.Tags);
        Assert.Single(cached1.Details);
        Assert.Single(cached1.Screenshots);
    }

    [Fact]
    public void TableParsingHelper()
    {
        string tableOutput =
            "Name           Id             Version    Available\r\n" +
            "-----------------------------------------------------------\r\n" +
            "Git            Git.Git        2.40.0     2.41.0\r\n" +
            "Notepad++      Notepad.Npp    8.5.1      8.5.2\r\n";

        var parsed = WingetParser.ParseTable(tableOutput);
        Assert.Equal(2, parsed.Count);
        Assert.Equal("Git.Git", parsed[0]["Id"]);
        Assert.Equal("2.41.0", parsed[0]["Available"]);
    }

    [Fact]
    public void ListDetailsOutputParsing()
    {
        string detailsOutput =
            "(1/2) Git [Git.Git]\r\n" +
            "  Publisher: Git\r\n" +
            "  Version: 2.40.0\r\n" +
            "  Origin Source: winget\r\n" +
            "(2/2) VS Code [Microsoft.VisualStudioCode]\r\n" +
            "  Publisher: Microsoft\r\n" +
            "  Version: 1.80.0\r\n" +
            "  Origin Source: winget\r\n";

        var parsed = WingetParser.ParseDetailsList(detailsOutput);
        Assert.Equal(2, parsed.Count);
        Assert.Equal("Git.Git", parsed[0].Id);
        Assert.Equal("Microsoft", parsed[1].Publisher);
        Assert.Equal("1.80.0", parsed[1].Version);
    }

    [Fact]
    public async Task InstalledAndUpgradable_Fetch()
    {
        var service = App.Winget;
        var installed = await service.GetInstalledPackagesAsync();
        Assert.Equal(4, installed.Count);
        Assert.Contains(installed, p => p.Id == "Git.Git");
        var upgradable = await service.GetUpgradablePackagesAsync();
        Assert.Equal(3, upgradable.Count);
        Assert.Contains(upgradable, p => p.Id == "Git.Git");
    }

    [Fact]
    public async Task SearchPackages_Operations()
    {
        var service = App.Winget;
        var searchResults = await service.SearchPackagesAsync("git", TestContext.Current.CancellationToken);
        Assert.Equal(3, searchResults.Count);
        Assert.All(searchResults, p => Assert.Equal("winget", p.Source));
    }

    [Fact]
    public async Task GetPackageDetails_Extraction()
    {
        var service = App.Winget;
        var pkgDetails = await service.GetPackageDetailsAsync("Git.Git");
        Assert.NotNull(pkgDetails);
        Assert.Equal("Git.Git", pkgDetails.Id);

        var decorated = await service.FetchAndDecoratePackageDetailsAsync("Git.Git");
        Assert.NotNull(decorated);
        Assert.Equal("Git.Git", decorated.Id);
    }

    public static IEnumerable<object[]> PackageOperationData()
    {
        yield return new object[] { (Action<IWingetService, WingetPackage>)((s, p) => s.InstallPackage(p)), "Mock.Install.App", PackageStatus.Installable };
        yield return new object[] { (Action<IWingetService, WingetPackage>)((s, p) => s.UpgradePackage(p)), "Mock.Upgrade.App", PackageStatus.Upgradable };
        yield return new object[] { (Action<IWingetService, WingetPackage>)((s, p) => s.UninstallPackage(p)), "Mock.Uninstall.App", PackageStatus.Installed };
    }

    [Theory]
    [MemberData(nameof(PackageOperationData))]
    public async Task PackageOperation_MockOperations(Action<IWingetService, WingetPackage> operation, string id, PackageStatus status)
    {
        await TestHelper.RunWithDispatcherAsync(async () =>
        {
            var service = App.Winget;
            var pkg = new WingetPackage { Id = id, Name = "Mock App", Status = status };
            operation(service, pkg);

            Assert.True(pkg.IsInstalling);
            Assert.Equal("Initializing...", pkg.InstallStatusText);

            await Task.Delay(100);
            Assert.True(pkg.InstallProgress > 0);

            pkg.IsInstalling = false;
        });
    }

    [Fact]
    public async Task BasicServiceFunctionality_ReturnsExpectedData()
    {
        var service = App.Winget;
        Assert.Equal(4, (await service.GetInstalledPackagesAsync()).Count);
        Assert.Equal(3, (await service.GetUpgradablePackagesAsync()).Count);
        var categories = await service.GetCategoriesAsync();
        Assert.NotEmpty(categories);
    }


    [Fact]
    public async Task ExportAndImport_CommandGeneration()
    {
        var service = App.Winget;
        string tempFile = Path.Combine(Path.GetTempPath(), "winget_export_test.json");
        try
        {
            var result = await service.ExportPackagesAsync(tempFile);
            Assert.Equal("", result);

            var importResult = await service.ImportPackagesAsync(tempFile);
            Assert.Equal("", importResult);
        }
        finally
        {
            if (File.Exists(tempFile)) File.Delete(tempFile);
        }
    }

    [Fact]
    public async Task PopularAndRecommendations_FetchJson()
    {
        var service = App.Winget;
        var popular = await service.GetPopularPackagesAsync();
        Assert.NotEmpty(popular);

        var recommendations = await service.GetRecommendationsAsync();
        Assert.NotEmpty(recommendations);
    }

    [Fact]
    public void WingetService_TriggerPackageAction_Null_DoesNotThrow()

    {
        var service = App.Services.GetRequiredService<WingetService>();
        var ex = Record.Exception(() => service.TriggerPackageAction(null!));
        Assert.Null(ex);
    }


    [Fact]
    public async Task SearchPackagesAsync_Cancellation_ThrowsOperationCanceledException()
    {
        var service = App.Services.GetRequiredService<WingetService>();
        var cts = new CancellationTokenSource();
        cts.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => service.SearchPackagesAsync("anything", cts.Token));
    }

    [Fact]
    public async Task WingetService_IsWingetAvailable_FileNotFound()
    {
        var service = App.Services.GetRequiredService<WingetService>();
        var field = typeof(WingetService).GetField("WingetPath", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic)!;
        var original = field.GetValue(null);
        try
        {
            field.SetValue(null, "C:\\nonexistent\\winget.exe");
            await Assert.ThrowsAsync<FileNotFoundException>(() => service.RunCommandAsync("anything", TestContext.Current.CancellationToken));
        }
        finally
        {
            field.SetValue(null, original);
        }
    }

    [Fact]
    public async Task FetchAndDecoratePackageDetailsAsync_NullPackage_Coverage()
    {
        var service = App.Services.GetRequiredService<WingetService>();
        MockProcessRunner.ShouldThrow = true;
        try
        {
            var pkg = await service.FetchAndDecoratePackageDetailsAsync("Mock.NonExistent.App");
            Assert.NotNull(pkg);
            Assert.Equal("Mock.NonExistent.App", pkg.Id);
            Assert.Equal("Mock.NonExistent.App", pkg.Name);
            Assert.Equal(PackageStatus.Installable, pkg.Status);
        }
        finally
        {
            MockProcessRunner.ShouldThrow = false;
        }
    }

    [Fact]
    public async Task WingetService_NoFiles_Coverage()
    {
        var service = App.Services.GetRequiredService<WingetService>();
        var bakP = await BackupAssetAsync("popular_packages.json");
        var bakC = await BackupAssetAsync("categories.json");

        try
        {
            Assert.Empty(await service.GetPopularPackagesAsync());
            Assert.Empty(await service.GetCategoriesAsync());
        }
        finally
        {
            await RestoreAssetAsync("popular_packages.json", bakP);
            await RestoreAssetAsync("categories.json", bakC);
        }
    }

    [Fact]
    public async Task WingetService_DeepEdgeCases_Coverage()
    {
        var service = App.Services.GetRequiredService<WingetService>();

        // 1. FetchAndDecoratePackageDetailsAsync installed not upgradable
        Assert.Equal(PackageStatus.Installed, (await service.FetchAndDecoratePackageDetailsAsync("Mock.App.Installed")).Status);

        // 2. FetchDetailsInBackground catch block
        var fetchMethod = typeof(WingetService).GetMethod("FetchDetailsInBackground", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!;
        MockProcessRunner.ShouldThrow = true;
        try
        {
            await (Task)fetchMethod.Invoke(service, new object[] { new WingetPackage { Id = "fail-app" } })!;
        }
        finally
        {
            MockProcessRunner.ShouldThrow = false;
        }

        // 3. TriggerPackageAction branches (Upgradable and Installable)
        service.TriggerPackageAction(new WingetPackage { Id = "upg", Status = PackageStatus.Upgradable });
        service.TriggerPackageAction(new WingetPackage { Id = "inst", Status = PackageStatus.Installable });

        // 4. null/empty list coverage for popular, categories, recommendations
        var bakP = await BackupAssetAsync("popular_packages.json");
        var bakC = await BackupAssetAsync("categories.json");
        try
        {
            await WriteAssetAsync("popular_packages.json", "null");
            await WriteAssetAsync("categories.json", "null");
            Assert.Empty(await service.GetPopularPackagesAsync());
            Assert.Empty(await service.GetCategoriesAsync());

            await WriteAssetAsync("popular_packages.json", "[null]");
            Assert.Empty(await service.GetRecommendationsAsync());
        }
        finally
        {
            await RestoreAssetAsync("popular_packages.json", bakP);
            await RestoreAssetAsync("categories.json", bakC);
        }
    }
    [Fact]
    public async Task Uninstall_SuccessPath_Coverage()
    {
        await TestHelper.RunWithDispatcherAsync(async () =>
        {
            var service = App.Services.GetRequiredService<WingetService>();
            var pkg = new WingetPackage { Id = "Mock.Uninstall.Success", Name = "Mock Uninstall", Status = PackageStatus.Installed };
            service.UninstallPackage(pkg);

            await TestHelper.WaitWhileAsync(() => pkg.IsInstalling);

            Assert.False(pkg.IsInstalling);
            Assert.Equal(PackageStatus.Installable, pkg.Status);
            Assert.Equal(100, pkg.InstallProgress);
        });
    }

    [Fact]
    public async Task GetPackageDetailsAsync_NullNameFallback()
    {
        var service = App.Services.GetRequiredService<WingetService>();
        var result = await service.GetPackageDetailsAsync("Mock.NotExist");
        Assert.NotNull(result);
        Assert.Equal("Mock.NotExist", result.Name);
    }

    [Fact]
    public async Task FetchDetailsInBackground_SuccessPath()
    {
        await TestHelper.RunWithDispatcherAsync(async () =>
        {
            var service = App.Services.GetRequiredService<WingetService>();
            var pkg = new WingetPackage { Id = "Git.Git", Name = "Git" };

            var fetchMethod = typeof(WingetService).GetMethod("FetchDetailsInBackground",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!;

            await (Task)fetchMethod.Invoke(service, new object[] { pkg })!;

            Assert.Equal("Software Corp", pkg.Publisher);
            Assert.Contains("version control", pkg.Description);
        });
    }

    [Fact]
    public async Task WingetService_AllMethods_ExceptionPaths_Coverage()
    {
        var service = App.Services.GetRequiredService<WingetService>();
        var bakP = await BackupAssetAsync("popular_packages.json");
        var bakC = await BackupAssetAsync("categories.json");

        await WriteAssetAsync("popular_packages.json", "{invalid json}");
        await WriteAssetAsync("categories.json", "{invalid json}");

        MockProcessRunner.ShouldThrow = true;
        try
        {
            Assert.Empty(await service.SearchPackagesAsync("anything", TestContext.Current.CancellationToken));
            Assert.Empty(await service.GetInstalledPackagesAsync());
            Assert.Empty(await service.GetUpgradablePackagesAsync());
            Assert.Empty(await service.GetPopularPackagesAsync());
            Assert.Empty(await service.GetRecommendationsAsync());
            Assert.Empty(await service.GetCategoriesAsync());
            Assert.Null(await service.GetPackageDetailsAsync("any"));
            await Assert.ThrowsAsync<InvalidOperationException>(() => service.ExportPackagesAsync("anyfile"));


            var decPkg = await service.FetchAndDecoratePackageDetailsAsync("any");
            Assert.NotNull(decPkg);
            Assert.Equal("any", decPkg.Id);
            Assert.Equal("any", decPkg.Name);
            Assert.Equal(PackageStatus.Installable, decPkg.Status);
        }
        finally
        {
            MockProcessRunner.ShouldThrow = false;
            await RestoreAssetAsync("popular_packages.json", bakP);
            await RestoreAssetAsync("categories.json", bakC);
        }
    }
}
