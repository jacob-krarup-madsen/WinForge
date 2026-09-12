namespace WingetStore.Tests;

public class RunTaskAsyncProgressStatusTests
{
    [Fact]
    public async Task Install_StatusOnlyLines_UpdatesStatusText()
    {
        await TestHelper.RunWithDispatcherAsync(async () =>
        {
            var runner = new StatusOnlyLinesRunner();
            var service = new WingetService(runner);
            var pkg = new WingetPackage { Id = "Mock.StatusOnly", Name = "StatusOnly" };

            service.InstallPackage(pkg);
            await TestHelper.WaitWhileAsync(() => pkg.IsInstalling);

            Assert.False(pkg.IsInstalling);
            Assert.Equal(PackageStatus.Installed, pkg.Status);
        });
    }
}
