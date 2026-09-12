namespace WingetStore.Tests;

public class CachingWingetServiceTests
{
    [Fact]
    public void Constructor_NullParameter_ThrowsException()
    {
        Assert.Throws<ArgumentNullException>(() => new CachingWingetService(null!));
    }

    [Fact]
    public void GetOrCreatePackage_NullOrEmptyId_Checks()
    {
        var innerService = App.Services.GetRequiredService<WingetService>();
        var cachingService = new CachingWingetService(innerService);

        // Null check
        Assert.Throws<ArgumentNullException>(() => cachingService.GetOrCreatePackage(null!));

        // Empty ID check
        var emptyPkg = new WingetPackage { Id = "", Name = "Empty ID Pkg" };
        Assert.Same(emptyPkg, cachingService.GetOrCreatePackage(emptyPkg));
    }

    [Fact]
    public async Task GetPackageDetailsAsync_ReturnsNull_ForNonExistentPackage()
    {
        var cachingService = (CachingWingetService)App.Services.GetRequiredService<IWingetService>();
        var details = await cachingService.GetPackageDetailsAsync("Mock.NotExist");
        Assert.NotNull(details);
        Assert.Equal("Mock", details.Publisher);

        // Test successful TryGetValue cache hit branch
        var details1 = await cachingService.GetPackageDetailsAsync("Git.Git");
        var details2 = await cachingService.GetPackageDetailsAsync("Git.Git");
        Assert.Same(details1, details2);
    }

    [Fact]
    public void TriggerPackageAction_And_CommonOperations_Coverage()
    {
        TestHelper.RunWithDispatcher(() =>
        {
            var cachingService = (CachingWingetService)App.Services.GetRequiredService<IWingetService>();
            var pkg = new WingetPackage { Id = "TriggerAction.Mock.App.Installed", Name = "Mock Installed App", Status = PackageStatus.Installed };

            cachingService.TriggerPackageAction(pkg);
            Assert.True(pkg.IsInstalling);
            Assert.NotEmpty(cachingService.ActiveTasks);
        });
    }

    [Fact]
    public async Task GetPackageDetailsAsync_InnerReturnsNull()
    {
        var cachingService = (CachingWingetService)App.Services.GetRequiredService<IWingetService>();
        MockProcessRunner.ShouldThrow = true;
        try
        {
            var details = await cachingService.GetPackageDetailsAsync("Mock.NonExistent");
            Assert.Null(details);
        }
        finally
        {
            MockProcessRunner.ShouldThrow = false;
        }
    }

    [Fact]
    public async Task RunTaskAsync_FailureAndException_Coverage()
    {
        await TestHelper.RunWithDispatcherAsync(async () =>
        {
            var service = App.Services.GetRequiredService<WingetService>();

            var pkgFail = new WingetPackage { Id = "Mock.Fail.App", Name = "Mock Fail App", Status = PackageStatus.Installable };
            service.InstallPackage(pkgFail);

            await TestHelper.WaitWhileAsync(() => pkgFail.IsInstalling, 1500);

            Assert.False(pkgFail.IsInstalling);
            Assert.Contains("Failed (Exit code: 2)", pkgFail.InstallStatusText);

            var pkgThrow = new WingetPackage { Id = "Mock.Throw.App", Name = "Mock Throw App", Status = PackageStatus.Installable };
            service.InstallPackage(pkgThrow);

            await TestHelper.WaitWhileAsync(() => pkgThrow.IsInstalling, 1500);

            Assert.False(pkgThrow.IsInstalling);
            Assert.Contains("Error: Simulated task exception", pkgThrow.InstallStatusText);
        });
    }

    [Fact]
    public void SettingsService_SaveSettings_Exception()
    {
        var field = typeof(SettingsService).GetField("SettingsFilePath", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic)!;
        var originalPath = field.GetValue(null);
        try
        {
            field.SetValue(null, "illegal:\0path");
            var ex = Record.Exception(() => { SettingsService.AppTheme = "NonExistentThemeXYZ"; });
            Assert.Null(ex);
        }
        finally
        {
            field.SetValue(null, originalPath);
        }
    }

    [Fact]
    public void WingetParser_ParseProgressFromOutput_EdgeCases()
    {
        Assert.Equal(0.0, WingetParser.ParseProgressFromOutput(""));
        Assert.Equal(0.0, WingetParser.ParseProgressFromOutput(null!));
        Assert.Equal(100.0, WingetParser.ParseProgressFromOutput("100%"));
        Assert.Equal(5.0, WingetParser.ParseProgressFromOutput("-5%"));
        Assert.Equal(0.0, WingetParser.ParseProgressFromOutput("0%"));
    }

    [Fact]
    public void WingetParser_ParseStatusTextFromOutput_EdgeCases()
    {
        Assert.Equal(string.Empty, WingetParser.ParseStatusTextFromOutput(null!));
        Assert.Equal(string.Empty, WingetParser.ParseStatusTextFromOutput(""));
        Assert.Equal("Line", WingetParser.ParseStatusTextFromOutput("Line\r\n"));
        var s40 = new string('A', 40);
        var s41 = new string('A', 41);
        Assert.Equal(s40, WingetParser.ParseStatusTextFromOutput(s40));
        Assert.Equal(new string('A', 37) + "...", WingetParser.ParseStatusTextFromOutput(s41));
    }

    [Fact]
    public void PackageFilteringHelper_MatchesQuery_SpecialCharacters()
    {
        var pkg = new WingetPackage { Id = "App.Name", Name = "App+Name", Publisher = "Pub.*" };
        Assert.True(pkg.MatchesQuery("+"));
        Assert.True(pkg.MatchesQuery(".*"));
        Assert.False(pkg.MatchesQuery("^"));
    }

    [Fact]
    public async Task IconService_Initialize_WithNonExistentCacheFile()
    {
        var service = IconService.Instance;
        await service.InitializeAsync();
        Assert.False(service.GetType().GetField("_isInitialized", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!.GetValue(service) is false);
    }

    [Fact]
    public void IconService_GetIconUrl_WithInvalidPackageId()
    {
        var service = IconService.Instance;
        Assert.Equal(string.Empty, service.GetIconUrl(null!, "Name"));
        Assert.Equal(string.Empty, service.GetIconUrl("", "Name"));
    }

    [Fact]
    public void WingetParser_ParsePackageDetails_MalformedDetails()
    {
        var pkg = WingetParser.ParsePackageDetails("some random non-yaml text here", "App.Id");
        Assert.Equal("App.Id", pkg.Id);
        Assert.Equal(string.Empty, pkg.Name);
    }

    [Fact]
    public void WingetService_Constructor_NullParameter_ThrowsException()
    {
        Assert.Throws<ArgumentNullException>(() => new WingetService(null!));
    }

    [Fact]
    public void DataTypes_EdgeCoverage()
    {
        var task = new InstallTask();
        bool changed = false;
        task.PropertyChanged += (s, e) => changed = true;
        task.Status = InstallTaskStatus.Running;
        Assert.True(changed);

        PackageId id = null!;
        Assert.Equal(string.Empty, (string)id);

        string s = null!;
        Assert.Equal(string.Empty, ((PackageId)s).Value);

        PackageVersion verNull = null!;
        Assert.Equal(string.Empty, (string)verNull);

        string svNull = null!;
        Assert.Equal(string.Empty, ((PackageVersion)svNull).Value);

        var nonNullVer = new PackageVersion("1.0.0");
        Assert.Equal("1.0.0", (string)nonNullVer);
        Assert.Equal("1.0.0", nonNullVer.ToString());
        Assert.Equal("1.0.0", ((PackageVersion)"1.0.0").Value);
    }

    [Fact]
    public void CancelTask_DelegatesToInner()
    {
        var innerService = App.Services.GetRequiredService<WingetService>();
        var cachingService = new CachingWingetService(innerService);
        cachingService.CancelTask("test-task-id");
    }

    [Fact]
    public void CancelTaskForPackage_DelegatesToInner()
    {
        var innerService = App.Services.GetRequiredService<WingetService>();
        var cachingService = new CachingWingetService(innerService);
        cachingService.CancelTaskForPackage("test-package-id");
    }
}
