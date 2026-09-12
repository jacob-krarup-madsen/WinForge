namespace WingetStore.Tests;

public class DetailsPageHelperTests
{
    [Fact]
    public void GetActionButtonData_Installed_ReturnsUninstallEnabled()
    {
        var pkg = new WingetPackage { Id = "Test", Name = "Test", Status = PackageStatus.Installed };
        var (label, enabled) = DetailsPage.GetActionButtonData(pkg);
        Assert.Equal("Uninstall", label);
        Assert.True(enabled);
    }

    [Fact]
    public void GetActionButtonData_Installing_Disabled()
    {
        var pkg = new WingetPackage { Id = "Test", Name = "Test", Status = PackageStatus.Installed, IsInstalling = true };
        var (label, enabled) = DetailsPage.GetActionButtonData(pkg);
        Assert.Equal("Uninstall", label);
        Assert.False(enabled);
    }

    [Fact]
    public void GetProgressData_NotInstalling_Collapsed()
    {
        var pkg = new WingetPackage { Id = "Test", Name = "Test" };
        var (vis, value, statusText, enabled) = DetailsPage.GetProgressData(pkg);
        Assert.Equal(Visibility.Collapsed, vis);
        Assert.Equal(0, value);
        Assert.Equal("", statusText);
        Assert.True(enabled);
    }

    [Fact]
    public void GetProgressData_IsInstalling_ShowsProgress()
    {
        var pkg = new WingetPackage { Id = "Test", Name = "Test", IsInstalling = true, InstallProgress = 50, InstallStatusText = "Installing..." };
        var (vis, value, statusText, enabled) = DetailsPage.GetProgressData(pkg);
        Assert.Equal(Visibility.Visible, vis);
        Assert.Equal(50, value);
        Assert.Equal("Installing...", statusText);
        Assert.False(enabled);
    }

    [Fact]
    public void GetViewLogsVisibility_NullPackage_Collapsed()
    {
        Assert.Equal(Visibility.Collapsed, DetailsPage.GetViewLogsVisibility(null, new ObservableCollection<InstallTask>()));
    }

    [Fact]
    public void GetViewLogsVisibility_NullTasks_Collapsed()
    {
        Assert.Equal(Visibility.Collapsed, DetailsPage.GetViewLogsVisibility(new WingetPackage { Id = "Test", Name = "Test" }, null!));
    }

    [Fact]
    public void GetViewLogsVisibility_HasMatchingTask_Visible()
    {
        var pkg = new WingetPackage { Id = "Test.Pkg", Name = "Test" };
        var tasks = new ObservableCollection<InstallTask>
        {
            new() { PackageId = "Other.Pkg", PackageName = "Other" },
            new() { PackageId = "Test.Pkg", PackageName = "Test" }
        };
        Assert.Equal(Visibility.Visible, DetailsPage.GetViewLogsVisibility(pkg, tasks));
    }

    [Fact]
    public void GetViewLogsVisibility_NoMatchingTask_Collapsed()
    {
        var pkg = new WingetPackage { Id = "Test.Pkg", Name = "Test" };
        var tasks = new ObservableCollection<InstallTask>
        {
            new() { PackageId = "Other.Pkg", PackageName = "Other" }
        };
        Assert.Equal(Visibility.Collapsed, DetailsPage.GetViewLogsVisibility(pkg, tasks));
    }
}
