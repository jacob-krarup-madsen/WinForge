namespace WingetStore.Tests;

public class TaskCancellationTests
{
    [Fact]
    public void InstallTask_CanCancelProperty_TracksStatusCorrectly()
    {
        var task = new InstallTask { Status = InstallTaskStatus.Running };
        Assert.True(task.CanCancel);

        task.Status = InstallTaskStatus.Completed;
        Assert.False(task.CanCancel);
    }

    [Fact]
    public async Task WingetService_CancelTaskForPackage_CancelsRunningProcess()
    {
        var mockRunner = new SlowProcessRunner();
        var service = new WingetService(mockRunner);
        var pkg = new WingetPackage { Id = "Slow.App", Status = PackageStatus.Installable };

        service.InstallPackage(pkg);
        await Task.Delay(100);

        Assert.True(pkg.IsInstalling);
        service.CancelTaskForPackage("Slow.App");

        await TestHelper.WaitWhileAsync(() => pkg.IsInstalling, 2000);
        Assert.False(pkg.IsInstalling);
        Assert.Equal("Canceled", pkg.InstallStatusText);
    }

    private class SlowProcessRunner : IProcessRunner
    {
        public async Task<int> RunStreamAsync(string fileName, string arguments, Action<string> onLineReceived, CancellationToken cancellationToken = default)
        {
            onLineReceived("Starting...");
            await Task.Delay(5000, cancellationToken);
            return 0;
        }
    }
}
