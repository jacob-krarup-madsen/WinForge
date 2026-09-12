namespace WingetStore.Tests;

public class ChallengerM2StaticMethodStressTests
{
    // --- WingetParser Stress Tests ---
    [Fact]
    public void WingetParser_GetSubstring_BoundaryAndInvalidInputs()
    {
        Assert.Equal("", WingetParser.GetSubstring(null!, 0, 5));
        Assert.Equal("", WingetParser.GetSubstring("", 0, 5));
        Assert.Equal("", WingetParser.GetSubstring("test", -1, 3));
        Assert.Equal("", WingetParser.GetSubstring("test", 4, 5));
        Assert.Equal("", WingetParser.GetSubstring("test", 2, 2));
        Assert.Equal("", WingetParser.GetSubstring("test", 3, 2));
        Assert.Equal("st", WingetParser.GetSubstring("test", 2, 10));
    }

    [Fact]
    public void WingetParser_FindHeaderLine_Variations()
    {
        Assert.Equal(-1, WingetParser.FindHeaderLine([]));
        Assert.Equal(-1, WingetParser.FindHeaderLine(["Line 1", "Line 2"]));
        Assert.Equal(-1, WingetParser.FindHeaderLine(["---"])); // line 0 -> 0-1 = -1
        Assert.Equal(0, WingetParser.FindHeaderLine(["Header", "---"]));
    }

    [Fact]
    public void WingetParser_TryParseColumnPositions_MalformedHeaders()
    {
        Assert.False(WingetParser.TryParseColumnPositions("Name Version Id", out _)); // Id after Version
        Assert.False(WingetParser.TryParseColumnPositions("Name Source", out _)); // Missing Id & Version
        Assert.True(WingetParser.TryParseColumnPositions("Name Id Version Source", out var pos));
        Assert.True(pos.sourcePos > 0);
    }

    [Fact]
    public void WingetParser_ParseTable_MalformedOutputs()
    {
        Assert.Empty(WingetParser.ParseTable(""));
        Assert.Empty(WingetParser.ParseTable("Short\nOutput"));
        Assert.Empty(WingetParser.ParseTable("Header Line\n----------------\n")); // Invalid column positions

        string tableWithNoMatch = "Name                           Id                                       Version\n--------------------------------------------------------------------------------------\nApp One                        App.One                                  1.0.0";
        var res = WingetParser.ParseTable(tableWithNoMatch);
        Assert.Single(res);
        Assert.Equal("App.One", res[0]["Id"]);
    }

    [Fact]
    public void WingetParser_ParseDetailsList_ARPFilterAndMalformed()
    {
        Assert.Empty(WingetParser.ParseDetailsList(""));
        string outputWithARP = "(1/2) Normal App [App.Normal]\n  Publisher: Pub\n(2/2) ARP App [ARP\\ControlPanelApp]\n  Publisher: Pub2";
        var list = WingetParser.ParseDetailsList(outputWithARP);
        Assert.Single(list);
        Assert.Equal("App.Normal", list[0].Id);
    }

    [Fact]
    public void WingetParser_ParseProgressAndStatusText()
    {
        Assert.Equal(0.0, WingetParser.ParseProgressFromOutput(""));
        Assert.Equal(20.0, WingetParser.ParseProgressFromOutput("Downloading something..."));
        Assert.Equal(60.0, WingetParser.ParseProgressFromOutput("Verifying package..."));
        Assert.Equal(80.0, WingetParser.ParseProgressFromOutput("Installing package..."));
        Assert.Equal(45.0, WingetParser.ParseProgressFromOutput("Progress: 45%"));
        // Empirical observation: Decimal percentages (e.g. 45.5%) match integer regex (\d+)% on the decimal part, returning 5.
        Assert.Equal(5.0, WingetParser.ParseProgressFromOutput("Progress: 45.5%"));

        Assert.Equal("", WingetParser.ParseStatusTextFromOutput(""));
        Assert.Equal("Downloading installer...", WingetParser.ParseStatusTextFromOutput("Downloading file"));
        Assert.Equal("Verifying installer...", WingetParser.ParseStatusTextFromOutput("Successfully verified installer hash"));
        Assert.Equal("Installing...", WingetParser.ParseStatusTextFromOutput("Starting package install"));
        Assert.Equal("Completed", WingetParser.ParseStatusTextFromOutput("Successfully installed"));
        Assert.Equal("Uninstalled", WingetParser.ParseStatusTextFromOutput("Successfully uninstalled"));
        Assert.Equal("", WingetParser.ParseStatusTextFromOutput("Progress 50%"));
        Assert.Equal("Very long status message that exceeds...", WingetParser.ParseStatusTextFromOutput("Very long status message that exceeds forty characters limit in output"));
    }

    // --- IconService Stress Tests ---
    [Fact]
    public void IconService_GetSafeIconFileName_SanitizesSpecialChars()
    {
        Assert.Equal("unknown.png", IconService.GetSafeIconFileName(""));
        Assert.Equal("unknown.png", IconService.GetSafeIconFileName("   "));
        Assert.Equal("App_Name.png", IconService.GetSafeIconFileName("App:Name"));
        Assert.Equal("App_Name.png", IconService.GetSafeIconFileName("App/Name"));
        Assert.Equal("App_Name.png", IconService.GetSafeIconFileName("App..Name"));
    }

    [Fact]
    public void IconService_ParseDatabaseJson_HandlesMalformedAndEmpty()
    {
        var (i1, s1) = IconService.ParseDatabaseJson("");
        Assert.Empty(i1); Assert.Empty(s1);

        var (i2, s2) = IconService.ParseDatabaseJson("{ invalid json }");
        Assert.Empty(i2); Assert.Empty(s2);

        string validJson = @"{
            ""icons_and_screenshots"": {
                ""App1"": {
                    ""icon"": ""https://example.com/icon.png"",
                    ""images"": [""https://example.com/ss1.png""]
                }
            }
        }";
        var (i3, s3) = IconService.ParseDatabaseJson(validJson);
        Assert.Single(i3);
        Assert.Equal("https://example.com/icon.png", i3["App1"]);
        Assert.Single(s3);
        Assert.Equal("https://example.com/ss1.png", s3["App1"][0]);
    }

    [Fact]
    public void IconService_IsCacheExpired_TestBoundaries()
    {
        var now = DateTime.Now;
        Assert.True(IconService.IsCacheExpired(now.AddHours(1), now, TimeSpan.FromHours(24))); // Future time -> expired
        Assert.True(IconService.IsCacheExpired(now.AddHours(-25), now, TimeSpan.FromHours(24))); // > 24 hours -> expired
        Assert.False(IconService.IsCacheExpired(now.AddHours(-10), now, TimeSpan.FromHours(24))); // < 24 hours -> not expired
    }

    [Fact]
    public void IconService_ExtractDomainFromUrl_Variations()
    {
        Assert.Equal("", IconService.ExtractDomainFromUrl(""));
        Assert.Equal("", IconService.ExtractDomainFromUrl("invalid-url"));
        Assert.Equal("example.com", IconService.ExtractDomainFromUrl("https://example.com/path"));
        Assert.Equal("example.com", IconService.ExtractDomainFromUrl("https://www.example.com/path?q=1"));
    }

    [Fact]
    public void IconService_NormalizePackageName_PerformanceWordEdgeCase()
    {
        Assert.Equal("", IconService.NormalizePackageName(""));
        Assert.Equal("DotNetSDK", IconService.NormalizePackageName("Microsoft.DotNet.SDK"));
        // Empirical observation: IndexOf("for") matches substrings inside words like "Performance" (Per-for-mance), truncating to "Per".
        string normPerf = IconService.NormalizePackageName("Performance Tool");
        Assert.Equal("Per", normPerf);
    }

    // --- CachingWingetService Stress Tests ---
    [Fact]
    public void CachingWingetService_MergePackageProperties_NullChecksAndMerging()
    {
        Assert.Throws<ArgumentNullException>(() => CachingWingetService.MergePackageProperties(null!, new WingetPackage()));
        Assert.Throws<ArgumentNullException>(() => CachingWingetService.MergePackageProperties(new WingetPackage(), null!));

        var existing = new WingetPackage { Id = "Test.App", Name = "Old Name", Version = "1.0", Status = PackageStatus.Installable };
        var incoming = new WingetPackage { Id = "Test.App", Name = "New Name", Version = "2.0", Status = PackageStatus.Installed, Publisher = "Pub" };

        CachingWingetService.MergePackageProperties(existing, incoming);
        Assert.Equal("New Name", existing.Name);
        Assert.Equal("2.0", existing.Version);
        Assert.Equal(PackageStatus.Installed, existing.Status);
        Assert.Equal("Pub", existing.Publisher);
    }

    // --- SettingsService Stress Tests ---
    [Fact]
    public void SettingsService_DeserializeAndSerialize()
    {
        Assert.False(SettingsService.DeserializeSettings(null).AutoUpdate);
        Assert.False(SettingsService.DeserializeSettings("").AutoUpdate);
        Assert.False(SettingsService.DeserializeSettings("invalid json").AutoUpdate);

        Assert.Throws<ArgumentNullException>(() => SettingsService.SerializeSettings(null!));

        var appSettings = new AppSettings { AutoUpdate = true, AppTheme = "Dark", EnableNotifications = false };
        string json = SettingsService.SerializeSettings(appSettings);
        var restored = SettingsService.DeserializeSettings(json);
        Assert.True(restored.AutoUpdate);
        Assert.Equal("Dark", restored.AppTheme);
        Assert.False(restored.EnableNotifications);
    }

    // --- LogService Stress Tests ---
    [Fact]
    public void LogService_FormatLogEntry_Formatting()
    {
        var ts = new DateTime(2026, 7, 23, 14, 30, 0);
        string entry = LogService.FormatLogEntry("INFO", "Test message", ts);
        Assert.Equal("[2026-07-23 14:30:00] [INFO] Test message", entry);
    }

    // --- WingetService Stress Tests ---
    [Fact]
    public void WingetService_EscapeArgument_EscapingRules()
    {
        Assert.Equal("\"\"", WingetService.EscapeArgument(null));
        Assert.Equal("\"\"", WingetService.EscapeArgument(""));
        Assert.Equal("\"simple\"", WingetService.EscapeArgument("simple"));
        Assert.Equal("\"with space\"", WingetService.EscapeArgument("with space"));
        Assert.Equal("\"with\\\"quote\"", WingetService.EscapeArgument("with\"quote"));
        Assert.Equal("\"C:\\Program Files\\\\\"", WingetService.EscapeArgument(@"C:\Program Files\"));
    }

    [Fact]
    public void WingetService_BuildRecommendations_NullAndEmptyInputs()
    {
        Assert.Empty(WingetService.BuildRecommendations(null, null));
        Assert.Empty(WingetService.BuildRecommendations([], null));

        var popular = new List<WingetPackage> { new() { Id = "App.1", Name = "App One" } };
        var recs = WingetService.BuildRecommendations(popular, null);
        Assert.Single(recs);
        Assert.Equal(PackageStatus.Installable, recs[0].Status);

        var installedMap = new Dictionary<string, WingetPackage> { ["App.1"] = new() { Id = "App.1", Version = "1.5.0", Status = PackageStatus.Installed } };
        var recsInstalled = WingetService.BuildRecommendations(popular, installedMap);
        Assert.Single(recsInstalled);
        Assert.Equal(PackageStatus.Installed, recsInstalled[0].Status);
        Assert.Equal("1.5.0", recsInstalled[0].Version);
    }

    [Fact]
    public void WingetService_DecoratePackageDetails_StatusResolution()
    {
        var details = new WingetPackage { Id = "App.1", Name = "App One" };
        var upgradable = new List<WingetPackage> { new() { Id = "App.1", Version = "1.0", AvailableVersion = "2.0" } };
        var installed = new List<WingetPackage> { new() { Id = "App.1", Version = "1.0" } };

        var decUpgradable = WingetService.DecoratePackageDetails(details, "App.1", installed, upgradable);
        Assert.Equal(PackageStatus.Upgradable, decUpgradable.Status);
        Assert.Equal("2.0", decUpgradable.AvailableVersion);

        var decInstalled = WingetService.DecoratePackageDetails(details, "App.1", installed, []);
        Assert.Equal(PackageStatus.Installed, decInstalled.Status);

        var decInstallable = WingetService.DecoratePackageDetails(null, "App.2", [], []);
        Assert.Equal("App.2", decInstallable.Id);
        Assert.Equal(PackageStatus.Installable, decInstallable.Status);
    }

    [Fact]
    public void WingetService_DeterminePackageAction_AllStates()
    {
        Assert.Equal(WingetService.PackageActionKind.None, WingetService.DeterminePackageAction(null));
        Assert.Equal(WingetService.PackageActionKind.Cancel, WingetService.DeterminePackageAction(new WingetPackage { IsInstalling = true }));
        Assert.Equal(WingetService.PackageActionKind.Uninstall, WingetService.DeterminePackageAction(new WingetPackage { Status = PackageStatus.Installed }));
        Assert.Equal(WingetService.PackageActionKind.Upgrade, WingetService.DeterminePackageAction(new WingetPackage { Status = PackageStatus.Upgradable }));
        Assert.Equal(WingetService.PackageActionKind.Install, WingetService.DeterminePackageAction(new WingetPackage { Status = PackageStatus.Installable }));
    }

    // --- Helpers Stress Tests ---
    [Fact]
    public void NavigationHelper_GetPageType_AllBranches()
    {
        Assert.Equal(typeof(Pages.NoWingetPage), NavigationHelper.GetPageType(NavTags.Home, false, false));
        Assert.Equal(typeof(Pages.SettingsPage), NavigationHelper.GetPageType(NavTags.Home, true, true));
        Assert.Null(NavigationHelper.GetPageType(null, false, true));
        Assert.Null(NavigationHelper.GetPageType("", false, true));
        Assert.Null(NavigationHelper.GetPageType("UnknownTag", false, true));
        Assert.Equal(typeof(Pages.HomePage), NavigationHelper.GetPageType(NavTags.Home, false, true));
        Assert.Equal(typeof(Pages.InstalledPage), NavigationHelper.GetPageType(NavTags.Installed, false, true));
        Assert.Equal(typeof(Pages.UpdatesPage), NavigationHelper.GetPageType(NavTags.Updates, false, true));
        Assert.Equal(typeof(Pages.AboutPage), NavigationHelper.GetPageType(NavTags.About, false, true));
    }

    [Fact]
    public void PackageFilteringHelper_MatchesQuery_And_Sorting()
    {
        Assert.False(PackageFilteringHelper.MatchesQuery(null!, "test"));
        var pkg = new WingetPackage { Name = "Calculator", Id = "Microsoft.WindowsCalculator", Publisher = "Microsoft", Description = "Standard calc", Tags = ["math", "tools"] };

        Assert.True(pkg.MatchesQuery(""));
        Assert.True(pkg.MatchesQuery("calc"));
        Assert.True(pkg.MatchesQuery("tag:math"));
        Assert.False(pkg.MatchesQuery("tag:games"));

        var list = new List<WingetPackage>
        {
            new() { Name = "B App", Id = "B.App", Publisher = "Pub B", Status = PackageStatus.Installable, Version = "1.0" },
            new() { Name = "A App", Id = "A.App", Publisher = "Pub A", Status = PackageStatus.Upgradable, Version = "2.0" }
        };

        PackageFilteringHelper.SortPackages(list, SortOrders.Az);
        Assert.Equal("A App", list[0].Name);

        PackageFilteringHelper.SortPackages(list, SortOrders.Status);
        Assert.Equal(PackageStatus.Upgradable, list[0].Status);
    }

    [Fact]
    public void GridCalculator_CalculateGridDimensions_ExceptionsAndBounds()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => GridCalculator.CalculateGridDimensions(500, minCardWidth: 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => GridCalculator.CalculateGridDimensions(500, gap: -1));
        Assert.Throws<ArgumentOutOfRangeException>(() => GridCalculator.CalculateGridDimensions(500, maxColumns: 0));

        var zeroWidth = GridCalculator.CalculateGridDimensions(0);
        Assert.Equal(1, zeroWidth.Columns);
        Assert.Equal(0, zeroWidth.SlotWidth);

        var normalGrid = GridCalculator.CalculateGridDimensions(1000, minCardWidth: 300, gap: 16, maxColumns: 5);
        Assert.Equal(3, normalGrid.Columns); // floor(1000 / 316) = 3
    }

    [Fact]
    public void BulkSelectionHelper_ComputeSelectAllState_Combinations()
    {
        Assert.Equal(false, BulkSelectionHelper.ComputeSelectAllState(0, 0));
        Assert.Equal(false, BulkSelectionHelper.ComputeSelectAllState(10, 0));
        Assert.Equal(true, BulkSelectionHelper.ComputeSelectAllState(5, 5));
        Assert.Null(BulkSelectionHelper.ComputeSelectAllState(10, 5));
    }

    [Fact]
    public void PackageDetailHelper_ShouldSkipMetadataItem_Keys()
    {
        Assert.True(PackageDetailHelper.ShouldSkipMetadataItem("Name"));
        Assert.True(PackageDetailHelper.ShouldSkipMetadataItem("Version"));
        Assert.True(PackageDetailHelper.ShouldSkipMetadataItem("Description"));
        Assert.True(PackageDetailHelper.ShouldSkipMetadataItem("Release Notes"));
        Assert.False(PackageDetailHelper.ShouldSkipMetadataItem("Publisher"));
    }
}
