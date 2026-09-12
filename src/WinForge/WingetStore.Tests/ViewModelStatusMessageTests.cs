namespace WingetStore.Tests;

public class ViewModelStatusMessageTests
{
    [Fact]
    public async Task InstalledViewModel_RemovesUninstalledPackage_ByIdMatch()
    {
        await TestHelper.RunWithDispatcherAsync(async () =>
        {
            var mockService = new StubWingetServiceWithPackages();
            var vm = new InstalledViewModel(mockService);
            await vm.LoadPackagesAsync();
            Assert.Equal(2, vm.FilteredPackages.Count);

            // Send status change message with a DISTINCT C# object reference but SAME Id
            var uninstalledPkg = new WingetPackage { Id = "App.One", Status = PackageStatus.Installable };
            WeakReferenceMessenger.Default.Send(new PackageStatusChangedMessage(uninstalledPkg));

            Assert.Single(vm.FilteredPackages);
            Assert.Equal("App.Two", vm.FilteredPackages[0].Id);
        });
    }

    [Fact]
    public async Task UpdatesViewModel_RemovesUpgradedPackage_ByIdMatch()
    {
        await TestHelper.RunWithDispatcherAsync(async () =>
        {
            var mockService = new StubWingetServiceWithUpgrades();
            var vm = new UpdatesViewModel(mockService);
            await vm.LoadUpgradesAsync();
            Assert.Equal(2, vm.FilteredUpgrades.Count);

            // Send status change message with a DISTINCT C# object reference but SAME Id
            var upgradedPkg = new WingetPackage { Id = "Upg.App1", Status = PackageStatus.Installed };
            WeakReferenceMessenger.Default.Send(new PackageStatusChangedMessage(upgradedPkg));

            Assert.Single(vm.FilteredUpgrades);
            Assert.Equal("Upg.App2", vm.FilteredUpgrades[0].Id);
        });
    }

    private class StubWingetServiceWithPackages : StubWingetService
    {
        public override Task<List<WingetPackage>> GetInstalledPackagesAsync() =>
            Task.FromResult(new List<WingetPackage>
            {
                new() { Id = "App.One", Name = "App One", Status = PackageStatus.Installed },
                new() { Id = "App.Two", Name = "App Two", Status = PackageStatus.Installed }
            });
    }

    private class StubWingetServiceWithUpgrades : StubWingetService
    {
        public override Task<List<WingetPackage>> GetUpgradablePackagesAsync() =>
            Task.FromResult(new List<WingetPackage>
            {
                new() { Id = "Upg.App1", Name = "Upgrade One", Status = PackageStatus.Upgradable },
                new() { Id = "Upg.App2", Name = "Upgrade Two", Status = PackageStatus.Upgradable }
            });
    }
}
