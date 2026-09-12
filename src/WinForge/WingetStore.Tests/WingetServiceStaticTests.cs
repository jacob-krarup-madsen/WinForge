namespace WingetStore.Tests;

public class WingetServiceStaticTests
{
    [Fact]
    public void MapFromRow_StandardRow_MapsPropertiesCorrectly()
    {
        var row = new Dictionary<string, string>
        {
            { "Name", "Git" },
            { "Id", "Git.Git" },
            { "Version", "2.40.0" },
            { "Source", "winget" }
        };

        var pkg = WingetService.MapFromRow(row);
        Assert.Equal("Git", pkg.Name);
        Assert.Equal("Git.Git", pkg.Id);
        Assert.Equal("2.40.0", pkg.Version);
        Assert.Equal("winget", pkg.Source);
        Assert.Equal(PackageStatus.Installable, pkg.Status);
    }

    [Fact]
    public void MapFromRow_EmptySource_DefaultsToWinget()
    {
        var row = new Dictionary<string, string>
        {
            { "Name", "App" },
            { "Id", "App.Id" },
            { "Version", "1.0" }
        };

        var pkg = WingetService.MapFromRow(row);
        Assert.Equal("winget", pkg.Source);
    }

    [Fact]
    public void MapFromRow_IncludeAvailableTrue_MapsAvailableVersion()
    {
        var row = new Dictionary<string, string>
        {
            { "Name", "App" },
            { "Id", "App.Id" },
            { "Version", "1.0" },
            { "Available", "2.0" }
        };

        var pkg = WingetService.MapFromRow(row, includeAvailable: true, defaultStatus: PackageStatus.Upgradable);
        Assert.Equal("1.0", pkg.Version);
        Assert.Equal("2.0", pkg.AvailableVersion);
        Assert.Equal(PackageStatus.Upgradable, pkg.Status);
    }

    [Fact]
    public void BuildRecommendations_MatchingInstalledPackage_SetsInstalledStatusAndVersion()
    {
        var popular = new List<WingetPackage>
        {
            new() { Id = "Git.Git", Name = "Git", Version = "1.0" },
            new() { Id = "NodeJS.NodeJS", Name = "Node.js", Version = "18.0" }
        };
        var installedMap = new Dictionary<string, WingetPackage>(StringComparer.OrdinalIgnoreCase)
        {
            { "Git.Git", new WingetPackage { Id = "Git.Git", Version = "2.40.0" } }
        };

        var recs = WingetService.BuildRecommendations(popular, installedMap, 10);
        Assert.Equal(2, recs.Count);
        Assert.Equal(PackageStatus.Installed, recs[0].Status);
        Assert.Equal("2.40.0", recs[0].Version);
        Assert.Equal(PackageStatus.Installable, recs[1].Status);
    }

    [Fact]
    public void BuildRecommendations_CaseInsensitiveIdMatch_UpdatesStatusCorrectly()
    {
        var popular = new List<WingetPackage>
        {
            new() { Id = "Git.Git", Name = "Git" }
        };
        var installedMap = new Dictionary<string, WingetPackage>(StringComparer.OrdinalIgnoreCase)
        {
            { "git.git", new WingetPackage { Id = "Git.Git", Version = "2.40.0" } }
        };

        var recs = WingetService.BuildRecommendations(popular, installedMap, 10);
        Assert.Single(recs);
        Assert.Equal(PackageStatus.Installed, recs[0].Status);
    }

    [Fact]
    public void BuildRecommendations_RespectsMaxCountLimit()
    {
        var popular = Enumerable.Range(1, 15).Select(i => new WingetPackage { Id = $"App.{i}", Name = $"App {i}" }).ToList();
        var recs = WingetService.BuildRecommendations(popular, null, 5);
        Assert.Equal(5, recs.Count);
    }

    [Fact]
    public void BuildRecommendations_NullOrEmptyInputs_ReturnsEmptyList()
    {
        Assert.Empty(WingetService.BuildRecommendations(null, null));
        Assert.Empty(WingetService.BuildRecommendations([], null));
    }

    [Fact]
    public void DecoratePackageDetails_NullDetails_CreatesFallbackPackage()
    {
        var pkg = WingetService.DecoratePackageDetails(null, "App.Id", [], []);
        Assert.Equal("App.Id", pkg.Id);
        Assert.Equal("App.Id", pkg.Name);
        Assert.Equal(PackageStatus.Installable, pkg.Status);
    }

    [Fact]
    public void DecoratePackageDetails_UpgradableMatch_SetsUpgradableStatusAndVersions()
    {
        var details = new WingetPackage { Id = "App.Id", Name = "App" };
        var upgradable = new List<WingetPackage>
        {
            new() { Id = "App.Id", Version = "1.0", AvailableVersion = "2.0" }
        };

        var pkg = WingetService.DecoratePackageDetails(details, "App.Id", [], upgradable);
        Assert.Equal(PackageStatus.Upgradable, pkg.Status);
        Assert.Equal("1.0", pkg.Version);
        Assert.Equal("2.0", pkg.AvailableVersion);
    }

    [Fact]
    public void DecoratePackageDetails_InstalledMatch_SetsInstalledStatusAndVersion()
    {
        var details = new WingetPackage { Id = "App.Id", Name = "App" };
        var installed = new List<WingetPackage>
        {
            new() { Id = "App.Id", Version = "1.5" }
        };

        var pkg = WingetService.DecoratePackageDetails(details, "App.Id", installed, []);
        Assert.Equal(PackageStatus.Installed, pkg.Status);
        Assert.Equal("1.5", pkg.Version);
    }

    [Fact]
    public void DecoratePackageDetails_UpgradablePrecedesInstalled()
    {
        var details = new WingetPackage { Id = "App.Id" };
        var installed = new List<WingetPackage> { new() { Id = "App.Id", Version = "1.0" } };
        var upgradable = new List<WingetPackage> { new() { Id = "App.Id", Version = "1.0", AvailableVersion = "2.0" } };

        var pkg = WingetService.DecoratePackageDetails(details, "App.Id", installed, upgradable);
        Assert.Equal(PackageStatus.Upgradable, pkg.Status);
    }

    [Fact]
    public void DeterminePackageAction_NullPackage_ReturnsNone()
    {
        Assert.Equal(WingetService.PackageActionKind.None, WingetService.DeterminePackageAction(null));
    }

    [Fact]
    public void DeterminePackageAction_IsInstalling_ReturnsCancel()
    {
        var pkg = new WingetPackage { Id = "App.Id", IsInstalling = true };
        Assert.Equal(WingetService.PackageActionKind.Cancel, WingetService.DeterminePackageAction(pkg));
    }

    [Fact]
    public void DeterminePackageAction_Installed_ReturnsUninstall()
    {
        var pkg = new WingetPackage { Id = "App.Id", Status = PackageStatus.Installed };
        Assert.Equal(WingetService.PackageActionKind.Uninstall, WingetService.DeterminePackageAction(pkg));
    }

    [Fact]
    public void DeterminePackageAction_Upgradable_ReturnsUpgrade()
    {
        var pkg = new WingetPackage { Id = "App.Id", Status = PackageStatus.Upgradable };
        Assert.Equal(WingetService.PackageActionKind.Upgrade, WingetService.DeterminePackageAction(pkg));
    }

    [Fact]
    public void DeterminePackageAction_Installable_ReturnsInstall()
    {
        var pkg = new WingetPackage { Id = "App.Id", Status = PackageStatus.Installable };
        Assert.Equal(WingetService.PackageActionKind.Install, WingetService.DeterminePackageAction(pkg));
    }

    [Fact]
    public void BuildSearchArguments_EscapesQuery()
    {
        Assert.Equal("search \"git\" --source winget --accept-source-agreements", WingetService.BuildSearchArguments("git"));
    }

    [Fact]
    public void BuildShowArguments_EscapesPackageId()
    {
        Assert.Equal("show \"Git.Git\" --accept-source-agreements", WingetService.BuildShowArguments("Git.Git"));
    }

    [Fact]
    public void BuildInstallArguments_EscapesPackageId()
    {
        Assert.Equal("install \"Git.Git\" --silent --accept-package-agreements --accept-source-agreements", WingetService.BuildInstallArguments("Git.Git"));
    }

    [Fact]
    public void BuildUpgradeArguments_EscapesPackageId()
    {
        Assert.Equal("upgrade \"Git.Git\" --silent --accept-package-agreements --accept-source-agreements", WingetService.BuildUpgradeArguments("Git.Git"));
    }

    [Fact]
    public void BuildUninstallArguments_EscapesPackageId()
    {
        Assert.Equal("uninstall \"Git.Git\" --silent", WingetService.BuildUninstallArguments("Git.Git"));
    }

    [Fact]
    public void BuildExportArguments_EscapesFilePath()
    {
        Assert.Equal("export -o \"C:\\temp\\apps.json\" --source winget --accept-source-agreements", WingetService.BuildExportArguments(@"C:\temp\apps.json"));
    }

    [Fact]
    public void BuildImportArguments_EscapesFilePath()
    {
        Assert.Equal("import -i \"C:\\temp\\apps.json\" --accept-package-agreements --accept-source-agreements", WingetService.BuildImportArguments(@"C:\temp\apps.json"));
    }
}
