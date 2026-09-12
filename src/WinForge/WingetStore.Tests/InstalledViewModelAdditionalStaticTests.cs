namespace WingetStore.Tests;

public class InstalledViewModelAdditionalStaticTests
{
    [Theory]
    [InlineData(null, "All Publishers")]
    [InlineData("", "All Publishers")]
    [InlineData("   ", "All Publishers")]
    public void NormalizeDeveloperFilter_NullOrEmpty_ReturnsAllDevelopers(string? current, string expected)
    {
        var options = new List<string> { "All Publishers", "Microsoft", "Adobe" };
        Assert.Equal(expected, InstalledViewModel.NormalizeDeveloperFilter(current, options));
    }

    [Fact]
    public void NormalizeDeveloperFilter_InvalidOption_ReturnsAllDevelopers()
    {
        var options = new List<string> { "All Publishers", "Microsoft" };
        Assert.Equal("All Publishers", InstalledViewModel.NormalizeDeveloperFilter("UnknownDev", options));
    }

    [Fact]
    public void NormalizeDeveloperFilter_ValidOption_ReturnsCurrentFilter()
    {
        var options = new List<string> { "All Publishers", "Microsoft", "Adobe" };
        Assert.Equal("Microsoft", InstalledViewModel.NormalizeDeveloperFilter("Microsoft", options));
        Assert.Equal("microsoft", InstalledViewModel.NormalizeDeveloperFilter("microsoft", options));
    }

    [Theory]
    [InlineData("Microsoft", "All Publishers", true)]
    [InlineData("Microsoft", null, true)]
    [InlineData("Microsoft", "", true)]
    [InlineData("Microsoft", "microsoft", true)]
    [InlineData(null, "Microsoft", false)]
    [InlineData("", "Microsoft", false)]
    [InlineData("Microsoft", "Adobe", false)]
    public void MatchesDeveloperFilter_ReturnsExpectedBool(string? pub, string? devFilter, bool expected)
    {
        Assert.Equal(expected, InstalledViewModel.MatchesDeveloperFilter(pub, devFilter));
    }

    [Fact]
    public void HandlePackageStatusChange_InstallableStatus_RemovesPackageFromList()
    {
        var list = new List<WingetPackage>
        {
            new() { Id = "App.Git" },
            new() { Id = "App.VSCode" }
        };

        bool result = InstalledViewModel.HandlePackageStatusChange(list, new WingetPackage { Id = "app.git", Status = PackageStatus.Installable });
        Assert.True(result);
        Assert.Single(list);
        Assert.Equal("App.VSCode", list[0].Id);
    }

    [Fact]
    public void HandlePackageStatusChange_InstalledStatus_UpdatesTargetVersionAndStatus()
    {
        var list = new List<WingetPackage>
        {
            new() { Id = "App.Git", Status = PackageStatus.Upgradable, Version = "1.0", AvailableVersion = "2.0" }
        };

        bool result = InstalledViewModel.HandlePackageStatusChange(list, new WingetPackage { Id = "App.Git", Status = PackageStatus.Installed, AvailableVersion = "2.0" });
        Assert.True(result);
        Assert.Equal(PackageStatus.Installed, list[0].Status);
        Assert.Equal("2.0", list[0].Version);
        Assert.Equal("", list[0].AvailableVersion);
    }

    [Fact]
    public void HandlePackageStatusChange_PackageNotFoundOrNull_ReturnsFalse()
    {
        var list = new List<WingetPackage> { new() { Id = "App.Git" } };
        Assert.False(InstalledViewModel.HandlePackageStatusChange(list, new WingetPackage { Id = "App.Other", Status = PackageStatus.Installable }));
        Assert.False(InstalledViewModel.HandlePackageStatusChange(list, null!));
        Assert.False(InstalledViewModel.HandlePackageStatusChange(null!, new WingetPackage { Id = "App.Git" }));
    }

    [Fact]
    public void CountUpgradablePackages_NullOrEmpty_ReturnsZero()
    {
        Assert.Equal(0, InstalledViewModel.CountUpgradablePackages(null));
        Assert.Equal(0, InstalledViewModel.CountUpgradablePackages([]));
    }

    [Fact]
    public void CountUpgradablePackages_ValidList_CountsUpgradableOnly()
    {
        var list = new List<WingetPackage>
        {
            new() { Status = PackageStatus.Upgradable },
            new() { Status = PackageStatus.Installed },
            new() { Status = PackageStatus.Upgradable }
        };
        Assert.Equal(2, InstalledViewModel.CountUpgradablePackages(list));
    }

    [Fact]
    public void FilterInstalledPackages_FiltersAndCountsCorrectly()
    {
        var list = new List<WingetPackage>
        {
            new() { Id = "App1", Name = "App One", Publisher = "MS", Source = "winget" },
            new() { Id = "App2", Name = "VCRedist", Publisher = "MS", Source = "winget" },
            new() { Id = "App3", Name = "App Three", Publisher = "Adobe", Source = "winget" }
        };

        var (filtered, appsCount, redistCount, totalCount) = InstalledViewModel.FilterInstalledPackages(
            list, "", "MS", "all", "Apps", "Name", "Ascending");

        Assert.Single(filtered);
        Assert.Equal("App1", filtered[0].Id);
        Assert.Equal(1, appsCount);
        Assert.Equal(1, redistCount);
        Assert.Equal(2, totalCount);
    }
}
