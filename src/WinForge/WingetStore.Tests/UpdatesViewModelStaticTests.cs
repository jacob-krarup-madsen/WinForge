namespace WingetStore.Tests;

public class UpdatesViewModelStaticTests
{
    [Fact]
    public void CalculateGlobalProgress_NullOrEmpty_ReturnsNotVisible()
    {
        var (isVis, val, text, status) = UpdatesViewModel.CalculateGlobalProgress(null);
        Assert.False(isVis);
        Assert.Equal(0, val);
        Assert.Equal("0%", text);
        Assert.Equal("", status);

        var (isVis2, _, _, _) = UpdatesViewModel.CalculateGlobalProgress(new List<WingetPackage>());
        Assert.False(isVis2);
    }

    [Fact]
    public void CalculateGlobalProgress_NoActiveUpgrades_ReturnsNotVisible()
    {
        var packages = new List<WingetPackage>
        {
            new() { Id = "pkg1", IsInstalling = false },
            new() { Id = "pkg2", IsInstalling = false }
        };
        var (isVis, _, _, _) = UpdatesViewModel.CalculateGlobalProgress(packages);
        Assert.False(isVis);
    }

    [Fact]
    public void CalculateGlobalProgress_SingleActiveUpgrade_ReturnsCorrectStatus()
    {
        var packages = new List<WingetPackage>
        {
            new() { Id = "pkg1", Name = "App One", IsInstalling = true, InstallProgress = 45.0 }
        };
        var (isVis, val, text, status) = UpdatesViewModel.CalculateGlobalProgress(packages);
        Assert.True(isVis);
        Assert.Equal(45.0, val);
        Assert.Equal("45%", text);
        Assert.Equal("Updating App One...", status);
    }

    [Fact]
    public void CalculateGlobalProgress_MultipleActiveUpgrades_CalculatesAverage()
    {
        var packages = new List<WingetPackage>
        {
            new() { Id = "pkg1", Name = "App One", IsInstalling = true, InstallProgress = 20.0 },
            new() { Id = "pkg2", Name = "App Two", IsInstalling = true, InstallProgress = 60.0 },
            new() { Id = "pkg3", Name = "App Three", IsInstalling = false, InstallProgress = 0.0 }
        };
        var (isVis, val, text, status) = UpdatesViewModel.CalculateGlobalProgress(packages);
        Assert.True(isVis);
        Assert.Equal(40.0, val);
        Assert.Equal("40%", text);
        Assert.Equal("Updating 2 apps...", status);
    }
}
