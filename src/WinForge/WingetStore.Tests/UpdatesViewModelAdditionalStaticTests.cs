namespace WingetStore.Tests;

public class UpdatesViewModelAdditionalStaticTests
{
    [Fact]
    public void HandlePackageInstalled_RemovesFromBothCollections()
    {
        var allUpgrades = new List<WingetPackage>
        {
            new() { Id = "Upg.App1" },
            new() { Id = "Upg.App2" }
        };
        var upgradesObs = new ObservableCollection<WingetPackage>
        {
            new() { Id = "Upg.App1" },
            new() { Id = "Upg.App2" }
        };

        bool removed = UpdatesViewModel.HandlePackageInstalled(allUpgrades, upgradesObs, new WingetPackage { Id = "upg.app1", Status = PackageStatus.Installed });
        Assert.True(removed);
        Assert.Single(allUpgrades);
        Assert.Single(upgradesObs);
        Assert.Equal("Upg.App2", allUpgrades[0].Id);
        Assert.Equal("Upg.App2", upgradesObs[0].Id);
    }

    [Fact]
    public void HandlePackageInstalled_NullOrNotFound_ReturnsFalse()
    {
        var allUpgrades = new List<WingetPackage> { new() { Id = "Upg.App1" } };
        var upgradesObs = new ObservableCollection<WingetPackage> { new() { Id = "Upg.App1" } };

        Assert.False(UpdatesViewModel.HandlePackageInstalled(allUpgrades, upgradesObs, null!));
        Assert.False(UpdatesViewModel.HandlePackageInstalled(allUpgrades, upgradesObs, new WingetPackage { Id = "Upg.NonExistent" }));
    }

    [Fact]
    public void GetEligiblePackagesForUpgrade_NullOrEmpty_ReturnsEmpty()
    {
        Assert.Empty(UpdatesViewModel.GetEligiblePackagesForUpgrade(null));
        Assert.Empty(UpdatesViewModel.GetEligiblePackagesForUpgrade([]));
    }

    [Fact]
    public void GetEligiblePackagesForUpgrade_FiltersOutInstallingPackages()
    {
        var packages = new List<WingetPackage>
        {
            new() { Id = "p1", IsInstalling = false },
            new() { Id = "p2", IsInstalling = true },
            new() { Id = "p3", IsInstalling = false }
        };

        var eligible = UpdatesViewModel.GetEligiblePackagesForUpgrade(packages);
        Assert.Equal(2, eligible.Count);
        Assert.Equal("p1", eligible[0].Id);
        Assert.Equal("p3", eligible[1].Id);
    }

    [Fact]
    public void FilterUpgradablePackages_FiltersBySourceCategoryAndCalculatesCounts()
    {
        var list = new List<WingetPackage>
        {
            new() { Id = "U1", Name = "Up 1", Source = "winget" },
            new() { Id = "U2", Name = "VCRedist Up 2", Source = "winget" }
        };

        var (filtered, appsCount, redistCount, totalCount) = UpdatesViewModel.FilterUpgradablePackages(
            list, "", "winget", "Redist", "Name", "Ascending");

        Assert.Single(filtered);
        Assert.Equal("U2", filtered[0].Id);
        Assert.Equal(1, appsCount);
        Assert.Equal(1, redistCount);
        Assert.Equal(2, totalCount);
    }
}
