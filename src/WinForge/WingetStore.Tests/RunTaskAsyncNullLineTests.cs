namespace WingetStore.Tests;

public class RunTaskAsyncNullLineTests
{
    [Fact]
    public async Task RunTaskAsync_NullLine_DoesNotThrow()
    {
        await TestHelper.RunWithDispatcherAsync(async () =>
        {
            var runner = new NullLineRunner();
            var service = new WingetService(runner);
            var pkg = new WingetPackage { Id = "Mock.NullLine.Pkg", Name = "NullLine" };

            service.InstallPackage(pkg);
            await TestHelper.WaitWhileAsync(() => pkg.IsInstalling);

            Assert.False(pkg.IsInstalling);
            Assert.Equal(PackageStatus.Installed, pkg.Status);
            Assert.Equal(100, pkg.InstallProgress);
        });
    }
}
