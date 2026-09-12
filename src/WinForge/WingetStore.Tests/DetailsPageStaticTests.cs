namespace WingetStore.Tests;

public class DetailsPageStaticTests
{
    [Fact]
    public void GetActionButtonData_NormalPackage_ReturnsLabelAndEnabled()
    {
        var pkg = new WingetPackage { Status = PackageStatus.Installable, IsInstalling = false };
        var (label, enabled) = DetailsPage.GetActionButtonData(pkg);
        Assert.Equal("Install", label);
        Assert.True(enabled);
    }

    [Fact]
    public void GetActionButtonData_InstallingPackage_ReturnsDisabled()
    {
        var pkg = new WingetPackage { Status = PackageStatus.Installable, IsInstalling = true };
        var (_, enabled) = DetailsPage.GetActionButtonData(pkg);
        Assert.False(enabled);
    }

    [Fact]
    public void GetProgressData_InstallingPackage_ReturnsVisibleProgress()
    {
        var pkg = new WingetPackage { IsInstalling = true, InstallProgress = 75.0, InstallStatusText = "Downloading..." };
        var (vis, val, text, enabled) = DetailsPage.GetProgressData(pkg);
        Assert.Equal(Visibility.Visible, vis);
        Assert.Equal(75.0, val);
        Assert.Equal("Downloading...", text);
        Assert.False(enabled);
    }

    [Fact]
    public void GetProgressData_NotInstalling_ReturnsCollapsed()
    {
        var pkg = new WingetPackage { IsInstalling = false };
        var (vis, val, text, enabled) = DetailsPage.GetProgressData(pkg);
        Assert.Equal(Visibility.Collapsed, vis);
        Assert.Equal(0, val);
        Assert.Equal("", text);
        Assert.True(enabled);
    }

    [Fact]
    public void GetViewLogsVisibility_NullOrNoTask_ReturnsCollapsed()
    {
        Assert.Equal(Visibility.Collapsed, DetailsPage.GetViewLogsVisibility(null, []));
        var pkg = new WingetPackage { Id = "test.app" };
        var tasks = new ObservableCollection<InstallTask>();
        Assert.Equal(Visibility.Collapsed, DetailsPage.GetViewLogsVisibility(pkg, tasks));
    }

    [Fact]
    public void GetViewLogsVisibility_HasMatchingTask_ReturnsVisible()
    {
        var pkg = new WingetPackage { Id = "test.app" };
        var tasks = new ObservableCollection<InstallTask>
        {
            new() { PackageId = "test.app" }
        };
        Assert.Equal(Visibility.Visible, DetailsPage.GetViewLogsVisibility(pkg, tasks));
    }
}
