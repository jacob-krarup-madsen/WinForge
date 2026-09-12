namespace WingetStore.Tests;

public class Milestone3PageStaticMethodTests
{
    // --- HomePage Static Tests ---
    [Theory]
    [InlineData(null, "")]
    [InlineData("", "")]
    [InlineData("   ", "")]
    [InlineData("git", "git")]
    [InlineData("  vscode  ", "vscode")]
    [InlineData("category:Developer Tools", "Developer Tools")]
    [InlineData("category:  Tools  ", "Tools")]
    [InlineData("category:", "")]
    [InlineData(12345, "")]
    public void HomePage_ExtractSearchQuery_ReturnsExpectedQuery(object? parameter, string expected)
    {
        Assert.Equal(expected, HomePage.ExtractSearchQuery(parameter));
    }

    [Theory]
    [InlineData(false, 0, false, "", Visibility.Collapsed, Visibility.Visible, Visibility.Collapsed, Visibility.Collapsed, "")]
    [InlineData(true, 5, false, "python", Visibility.Visible, Visibility.Collapsed, Visibility.Visible, Visibility.Collapsed, "Search Results for \"python\"")]
    [InlineData(true, 0, true, "vs", Visibility.Visible, Visibility.Collapsed, Visibility.Collapsed, Visibility.Collapsed, "Search Results for \"vs\"")]
    [InlineData(true, 0, false, "unknown", Visibility.Visible, Visibility.Collapsed, Visibility.Collapsed, Visibility.Visible, "Search Results for \"unknown\"")]
    public void HomePage_DetermineSearchViewState_ReturnsExpectedVisibilitiesAndTitle(
        bool isSearchActive, int itemCount, bool isLoading, string searchQuery,
        Visibility expSearchVis, Visibility expDiscVis, Visibility expListVis, Visibility expEmptyVis, string expTitle)
    {
        var (searchVis, discVis, listVis, emptyVis, title) = HomePage.DetermineSearchViewState(isSearchActive, itemCount, isLoading, searchQuery);
        Assert.Equal(expSearchVis, searchVis);
        Assert.Equal(expDiscVis, discVis);
        Assert.Equal(expListVis, listVis);
        Assert.Equal(expEmptyVis, emptyVis);
        Assert.Equal(expTitle, title);
    }

    [Fact]
    public void HomePage_ShouldUpdateGridLayout_Recreated_ReturnsTrue()
    {
        Assert.True(HomePage.ShouldUpdateGridLayout(true, 3, 3, 300, 300, 150, 150, 130, 130, 16, 16));
    }

    [Fact]
    public void HomePage_ShouldUpdateGridLayout_Identical_ReturnsFalse()
    {
        Assert.False(HomePage.ShouldUpdateGridLayout(false, 3, 3, 300.2, 300.0, 150.1, 150.0, 130.1, 130.0, 16.1, 16.0));
    }

    [Fact]
    public void HomePage_ShouldUpdateGridLayout_DeltasExceedThreshold_ReturnsTrue()
    {
        Assert.True(HomePage.ShouldUpdateGridLayout(false, 4, 3, 300, 300, 150, 150, 130, 130, 16, 16));
        Assert.True(HomePage.ShouldUpdateGridLayout(false, 3, 3, 301, 300, 150, 150, 130, 130, 16, 16));
        Assert.True(HomePage.ShouldUpdateGridLayout(false, 3, 3, 300, 300, 151, 150, 130, 130, 16, 16));
        Assert.True(HomePage.ShouldUpdateGridLayout(false, 3, 3, 300, 300, 150, 150, 131, 130, 16, 16));
        Assert.True(HomePage.ShouldUpdateGridLayout(false, 3, 3, 300, 300, 150, 150, 130, 130, 17, 16));
    }

    [Fact]
    public void HomePage_FormatSearchResultsTitle_FormatsCorrectly()
    {
        Assert.Equal("Search Results for \"git\"", HomePage.FormatSearchResultsTitle("git"));
    }

    [Fact]
    public void HomePage_NormalizeQuery_TrimsWhitespace()
    {
        Assert.Equal("", HomePage.NormalizeQuery(null));
        Assert.Equal("", HomePage.NormalizeQuery("   "));
        Assert.Equal("git", HomePage.NormalizeQuery("  git  "));
    }

    // --- InstalledPage Static Tests ---
    [Fact]
    public void InstalledPage_GetUpdateVisibility_ReturnsExpected()
    {
        Assert.Equal(Visibility.Visible, InstalledPage.GetUpdateVisibility(PackageStatus.Upgradable));
        Assert.Equal(Visibility.Collapsed, InstalledPage.GetUpdateVisibility(PackageStatus.Installed));
        Assert.Equal(Visibility.Collapsed, InstalledPage.GetUpdateVisibility(PackageStatus.Installable));
    }

    [Theory]
    [InlineData("Descending", "Name", "Name", "\uE74B", Visibility.Visible)]
    [InlineData("Ascending", "Name", "Name", "\uE74A", Visibility.Visible)]
    [InlineData("Descending", "Publisher", "Name", "\uE74B", Visibility.Collapsed)]
    public void InstalledPage_GetSortGlyph_ReturnsExpectedGlyphAndVisibility(string sortDir, string sortBy, string targetField, string expGlyph, Visibility expVis)
    {
        var (glyph, vis) = InstalledPage.GetSortGlyph(sortDir, sortBy, targetField);
        Assert.Equal(expGlyph, glyph);
        Assert.Equal(expVis, vis);
    }

    [Fact]
    public void InstalledPage_GetInstalledViewState_Loading_ReturnsLoadingVisible()
    {
        var (progressVis, listVis, emptyVis) = InstalledPage.GetInstalledViewState(true, 5);
        Assert.Equal(Visibility.Visible, progressVis);
        Assert.Equal(Visibility.Collapsed, listVis);
        Assert.Equal(Visibility.Collapsed, emptyVis);
    }

    [Fact]
    public void InstalledPage_GetInstalledViewState_HasItems_ReturnsListVisible()
    {
        var (progressVis, listVis, emptyVis) = InstalledPage.GetInstalledViewState(false, 5);
        Assert.Equal(Visibility.Collapsed, progressVis);
        Assert.Equal(Visibility.Visible, listVis);
        Assert.Equal(Visibility.Collapsed, emptyVis);
    }

    [Fact]
    public void InstalledPage_GetInstalledViewState_ZeroItems_ReturnsEmptyVisible()
    {
        var (progressVis, listVis, emptyVis) = InstalledPage.GetInstalledViewState(false, 0);
        Assert.Equal(Visibility.Collapsed, progressVis);
        Assert.Equal(Visibility.Collapsed, listVis);
        Assert.Equal(Visibility.Visible, emptyVis);
    }

    [Fact]
    public void InstalledPage_GetEligibleBulkUninstallPackages_FiltersInstallingAndNulls()
    {
        Assert.Empty(InstalledPage.GetEligibleBulkUninstallPackages(null));
        Assert.Empty(InstalledPage.GetEligibleBulkUninstallPackages([]));

        var list = new List<WingetPackage?>
        {
            null,
            new WingetPackage { Id = "p1", IsInstalling = true },
            new WingetPackage { Id = "p2", IsInstalling = false }
        };

        var eligible = InstalledPage.GetEligibleBulkUninstallPackages(list);
        Assert.Single(eligible);
        Assert.Equal("p2", eligible[0].Id);
    }

    [Fact]
    public void InstalledPage_GetImportStatusMessage_ReturnsCorrectMessages()
    {
        var (successSev, successTitle, successMsg) = InstalledPage.GetImportStatusMessage(true, null);
        Assert.Equal(Microsoft.UI.Xaml.Controls.InfoBarSeverity.Success, successSev);
        Assert.Equal("Import Completed", successTitle);
        Assert.Contains("imported and processed successfully", successMsg);

        var (failSev, failTitle, failMsg) = InstalledPage.GetImportStatusMessage(false, new Exception("File corrupted"));
        Assert.Equal(Microsoft.UI.Xaml.Controls.InfoBarSeverity.Error, failSev);
        Assert.Equal("Import Failed", failTitle);
        Assert.Contains("File corrupted", failMsg);
    }

    [Fact]
    public void InstalledPage_GetExportStatusMessage_ReturnsCorrectMessages()
    {
        var (successSev, successTitle, successMsg) = InstalledPage.GetExportStatusMessage(true, "C:\\export.json", null);
        Assert.Equal(Microsoft.UI.Xaml.Controls.InfoBarSeverity.Success, successSev);
        Assert.Equal("Export Complete", successTitle);
        Assert.Contains("C:\\export.json", successMsg);

        var (failSev, failTitle, failMsg) = InstalledPage.GetExportStatusMessage(false, null, new Exception("Access Denied"));
        Assert.Equal(Microsoft.UI.Xaml.Controls.InfoBarSeverity.Error, failSev);
        Assert.Equal("Export Failed", failTitle);
        Assert.Contains("Access Denied", failMsg);
    }

    // --- UpdatesPage Static Tests ---
    [Fact]
    public void UpdatesPage_CanUpdateAll_EvaluatesConditionsCorrectly()
    {
        Assert.False(UpdatesPage.CanUpdateAll(false, [new WingetPackage { IsInstalling = false }]));
        Assert.False(UpdatesPage.CanUpdateAll(true, null));
        Assert.False(UpdatesPage.CanUpdateAll(true, []));
        Assert.False(UpdatesPage.CanUpdateAll(true, [new WingetPackage { IsInstalling = true }]));
        Assert.True(UpdatesPage.CanUpdateAll(true, [new WingetPackage { IsInstalling = true }, new WingetPackage { IsInstalling = false }]));
        Assert.True(UpdatesPage.CanUpdateAll(true, [null!, new WingetPackage { IsInstalling = false }]));
    }

    [Fact]
    public void UpdatesPage_FilterPackagesForBulkUpdate_FiltersNullsAndInstalling()
    {
        Assert.Empty(UpdatesPage.FilterPackagesForBulkUpdate(null));
        Assert.Empty(UpdatesPage.FilterPackagesForBulkUpdate([]));

        var selected = new List<WingetPackage?>
        {
            null,
            new WingetPackage { Id = "u1", IsInstalling = true },
            new WingetPackage { Id = "u2", IsInstalling = false }
        };

        var filtered = UpdatesPage.FilterPackagesForBulkUpdate(selected!);
        Assert.Single(filtered);
        Assert.Equal("u2", filtered[0].Id);
    }

    // --- DetailsPage Static Tests ---
    [Fact]
    public void DetailsPage_FormatPublisher_ReturnsPublisherOrFallback()
    {
        Assert.Equal("Unknown Publisher", DetailsPage.FormatPublisher(null));
        Assert.Equal("Unknown Publisher", DetailsPage.FormatPublisher(""));
        Assert.Equal("Unknown Publisher", DetailsPage.FormatPublisher("   "));
        Assert.Equal("Microsoft Corporation", DetailsPage.FormatPublisher("Microsoft Corporation"));
    }

    [Fact]
    public void DetailsPage_FormatVersionText_CombinesVersionsCorrectly()
    {
        Assert.Equal("Version: 1.0.0", DetailsPage.FormatVersionText("1.0.0", null));
        Assert.Equal("Version: 1.0.0", DetailsPage.FormatVersionText("1.0.0", ""));
        Assert.Equal("Version: 1.0.0 (Latest: 1.2.0)", DetailsPage.FormatVersionText("1.0.0", "1.2.0"));
        Assert.Equal("Version: Unknown (Latest: 2.0.0)", DetailsPage.FormatVersionText(null, "2.0.0"));
    }

    [Fact]
    public void DetailsPage_FormatDescription_ReturnsDescriptionOrFallback()
    {
        Assert.Equal("No description available for this package.", DetailsPage.FormatDescription(null));
        Assert.Equal("No description available for this package.", DetailsPage.FormatDescription(""));
        Assert.Equal("No description available for this package.", DetailsPage.FormatDescription("   "));
        Assert.Equal("Git version control system.", DetailsPage.FormatDescription("Git version control system."));
    }

    [Fact]
    public void DetailsPage_FindActiveTaskForPackage_FindsRunningOrQueuedTaskCaseInsensitively()
    {
        Assert.Null(DetailsPage.FindActiveTaskForPackage(null, []));
        Assert.Null(DetailsPage.FindActiveTaskForPackage("app.id", null));

        var tasks = new List<InstallTask>
        {
            new InstallTask { Id = "1", PackageId = "app.git", PackageName = "Git", Operation = TaskOperation.Install, Status = InstallTaskStatus.Completed },
            new InstallTask { Id = "2", PackageId = "app.vscode", PackageName = "VS Code", Operation = TaskOperation.Install, Status = InstallTaskStatus.Running },
            new InstallTask { Id = "3", PackageId = "app.node", PackageName = "Node", Operation = TaskOperation.Install, Status = InstallTaskStatus.Queued }
        };

        Assert.Null(DetailsPage.FindActiveTaskForPackage("app.git", tasks));
        var task2 = DetailsPage.FindActiveTaskForPackage("APP.VSCODE", tasks);
        Assert.NotNull(task2);
        Assert.Equal("2", task2.Id);

        var task3 = DetailsPage.FindActiveTaskForPackage("app.node", tasks);
        Assert.NotNull(task3);
        Assert.Equal("3", task3.Id);
    }

    [Fact]
    public void DetailsPage_GetTextSectionVisibility_ReturnsVisibleOnlyWhenHasContent()
    {
        Assert.Equal(Visibility.Collapsed, DetailsPage.GetTextSectionVisibility(null));
        Assert.Equal(Visibility.Collapsed, DetailsPage.GetTextSectionVisibility(""));
        Assert.Equal(Visibility.Visible, DetailsPage.GetTextSectionVisibility("Release notes text"));
    }

    [Fact]
    public void DetailsPage_GetCollectionVisibility_ReturnsVisibleOnlyWhenHasItems()
    {
        Assert.Equal(Visibility.Collapsed, DetailsPage.GetCollectionVisibility<string>(null));
        Assert.Equal(Visibility.Collapsed, DetailsPage.GetCollectionVisibility<string>([]));
        Assert.Equal(Visibility.Visible, DetailsPage.GetCollectionVisibility<string>(["tag1"]));
    }

    [Fact]
    public void DetailsPage_GetTagNavigationParameter_ConstructsPrefix()
    {
        Assert.Equal("tag:developer", DetailsPage.GetTagNavigationParameter("developer"));
    }

    // --- App / MainWindow / NoWingetPage / SettingsPage Static Tests ---
    [Fact]
    public void App_FormatLogDialogTitle_FormatsCorrectly()
    {
        Assert.Equal("Activity Log: Git (Install)", App.FormatLogDialogTitle("Git", "Install"));
    }

    [Fact]
    public void App_FormatActivityLogStatus_FormatsPercentageCorrectly()
    {
        Assert.Equal("Status: Downloading... | Progress: 45%", App.FormatActivityLogStatus("Downloading...", 45.7));
        Assert.Equal("Status: Done | Progress: 100%", App.FormatActivityLogStatus("Done", 100));
    }

    [Theory]
    [InlineData(false, true, Visibility.Visible)]
    [InlineData(true, true, Visibility.Collapsed)]
    [InlineData(false, false, Visibility.Collapsed)]
    [InlineData(true, false, Visibility.Collapsed)]
    public void MainWindow_IsBackButtonVisible_ReturnsExpectedVisibility(bool isTopLevel, bool canGoBack, Visibility expected)
    {
        Assert.Equal(expected, MainWindow.IsBackButtonVisible(isTopLevel, canGoBack));
    }

    [Fact]
    public void NoWingetPage_GetTempInstallerPath_CombinesDirectoryAndFileName()
    {
        string path = NoWingetPage.GetTempInstallerPath("C:\\temp");
        Assert.Equal(Path.Combine("C:\\temp", "Microsoft.DesktopAppInstaller.msixbundle"), path);
    }

    [Fact]
    public void NoWingetPage_GetPowershellInstallArguments_ConstructsFormattedCommand()
    {
        string args = NoWingetPage.GetPowershellInstallArguments("C:\\temp\\installer.msixbundle");
        Assert.Equal("-NoProfile -ExecutionPolicy Bypass -Command \"Add-AppxPackage -Path 'C:\\temp\\installer.msixbundle'\"", args);
    }

    [Fact]
    public void SettingsPage_GetStatusBrushResourceKey_ReturnsExpectedResourceKey()
    {
        Assert.Equal("SystemFillColorSuccessBrush", SettingsPage.GetStatusBrushResourceKey(true));
        Assert.Equal("SystemFillColorCriticalBrush", SettingsPage.GetStatusBrushResourceKey(false));
    }
}
