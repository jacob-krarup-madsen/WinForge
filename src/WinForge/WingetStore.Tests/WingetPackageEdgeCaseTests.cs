namespace WingetStore.Tests;

public class WingetPackageEdgeCaseTests
{
    [Fact]
    public void Publisher_FallbackWhenInstalled_ReturnsWordFromId()
    {
        var pkg = new WingetPackage { Id = "Microsoft.VSCode", Publisher = "Installed" };
        Assert.Equal("Microsoft", pkg.Publisher);

        var pkg2 = new WingetPackage { Id = "Microsoft.VSCode", Publisher = "winget" };
        Assert.Equal("Microsoft", pkg2.Publisher);
    }

    [Fact]
    public void Publisher_FallbackWhenNullAndNoDotInId_ReturnsNameFirstWord()
    {
        var pkg = new WingetPackage { Id = "NoDot", Name = "Some App", Publisher = "" };
        Assert.Equal("Some", pkg.Publisher);
    }

    [Fact]
    public void Publisher_FallbackWhenAllEmpty_ReturnsWingetPackage()
    {
        var pkg = new WingetPackage { Name = "", Publisher = "" };
        Assert.Equal("Winget Package", pkg.Publisher);
    }

    [Fact]
    public void IsRedistributable_KeywordMatches()
    {
        Assert.True(new WingetPackage { Name = "Visual C++ Runtime" }.IsRedistributable);
        Assert.True(new WingetPackage { Name = ".NET Runtime" }.IsRedistributable);
        Assert.True(new WingetPackage { Name = "Microsoft WebView2" }.IsRedistributable);
        Assert.True(new WingetPackage { Name = "DirectX Runtime" }.IsRedistributable);
        Assert.True(new WingetPackage { Name = "Software Development Kit" }.IsRedistributable);
        Assert.True(new WingetPackage { Name = "Windows SDK", Id = "Some.SDK" }.IsRedistributable);
        Assert.True(new WingetPackage { Id = "Some.DotNet", Name = "Runtime" }.IsRedistributable);
        Assert.True(new WingetPackage { Id = "Some.VCRedist", Name = "Package" }.IsRedistributable);
    }

    [Fact]
    public void IsNotRedistributable_DoesNotMatchKeywords()
    {
        Assert.False(new WingetPackage { Name = "Visual Studio Code" }.IsRedistributable);
        Assert.False(new WingetPackage { Name = "Microsoft Office" }.IsRedistributable);
    }

    [Fact]
    public void DisplayTitle_StripsVersionPatternsFromName()
    {
        var pkg = new WingetPackage { Name = "App Name v1.2.3", Id = "App.Id" };
        Assert.Equal("App Name", pkg.DisplayTitle);

        var pkg2 = new WingetPackage { Name = "App Name 1.2.3", Id = "App.Id" };
        Assert.Equal("App Name", pkg2.DisplayTitle);
    }

    [Fact]
    public void DisplayTitle_FallsBackToNameWhenCleanedIsEmpty()
    {
        var pkg = new WingetPackage { Name = "v1.0", Id = "App.Id" };
        Assert.Equal("v1.0", pkg.DisplayTitle);
    }

    [Fact]
    public void DisplayTitle_FallsBackToIdWhenNameIsWhitespace()
    {
        var pkg = new WingetPackage { Name = "", Id = "App.Id" };
        Assert.Equal("App.Id", pkg.DisplayTitle);
    }

    [Fact]
    public void FormattedVersionAndSource_FormatsCorrectly()
    {
        Assert.EndsWith("Winget", new WingetPackage { Version = "", Source = "" }.FormattedVersionAndSource);
        Assert.Contains("1.0", new WingetPackage { Version = "1.0", Source = "winget" }.FormattedVersionAndSource);
        Assert.DoesNotContain("·", new WingetPackage { Version = "", Source = "winget" }.FormattedVersionAndSource);
    }

    [Fact]
    public void StatusDrivenProperties_ReturnExpectedValues()
    {
        var pkg = new WingetPackage();

        pkg.Status = PackageStatus.Installable;
        Assert.True(pkg.ShowInstallOrUpdateButton);
        Assert.False(pkg.ShowUninstallButton);
        Assert.True(pkg.IsInstallAction);
        Assert.False(pkg.IsUninstallAction);
        Assert.Equal("Install", pkg.PrimaryActionButtonText);

        pkg.Status = PackageStatus.Installed;
        Assert.False(pkg.ShowInstallOrUpdateButton);
        Assert.True(pkg.ShowUninstallButton);
        Assert.False(pkg.IsInstallAction);
        Assert.True(pkg.IsUninstallAction);
        Assert.Equal("Uninstall", pkg.PrimaryActionButtonText);

        pkg.Status = PackageStatus.Upgradable;
        Assert.True(pkg.ShowInstallOrUpdateButton);
        Assert.False(pkg.ShowUninstallButton);
        Assert.True(pkg.IsInstallAction);
        Assert.False(pkg.IsUninstallAction);
        Assert.Equal("Update", pkg.PrimaryActionButtonText);

        pkg.IsInstalling = true;
        Assert.Equal("Working...", pkg.PrimaryActionButtonText);
    }

    [Fact]
    public void PackageProperties_SetAndGet()
    {
        var pkg = new WingetPackage();
        pkg.Version = "2.0";
        Assert.Equal("2.0", pkg.Version);
        pkg.AvailableVersion = "3.0";
        Assert.Equal("3.0", pkg.AvailableVersion);
        pkg.Source = "winget";
        Assert.Equal("winget", pkg.Source);
        pkg.Description = "A test package";
        Assert.Equal("A test package", pkg.Description);
        pkg.Homepage = "https://example.com";
        Assert.Equal("https://example.com", pkg.Homepage);
        pkg.License = "MIT";
        Assert.Equal("MIT", pkg.License);
        pkg.InstallerType = "msi";
        Assert.Equal("msi", pkg.InstallerType);
        pkg.InstallerUrl = "https://example.com/setup.msi";
        Assert.Equal("https://example.com/setup.msi", pkg.InstallerUrl);
        pkg.PublisherUrl = "https://publisher.com";
        Assert.Equal("https://publisher.com", pkg.PublisherUrl);
        pkg.ReleaseNotes = "Bug fixes";
        Assert.Equal("Bug fixes", pkg.ReleaseNotes);
        pkg.InstallStatusText = "Downloading";
        Assert.Equal("Downloading", pkg.InstallStatusText);
        pkg.InstallProgress = 50.0;
        Assert.Equal(50.0, pkg.InstallProgress);
    }
}
