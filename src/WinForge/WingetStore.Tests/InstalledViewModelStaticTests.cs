namespace WingetStore.Tests;

public class InstalledViewModelStaticTests
{
    [Fact]
    public void ExtractDevelopersList_NullOrEmpty_ReturnsEmptyList()
    {
        Assert.Empty(InstalledViewModel.ExtractDevelopersList(null));
        Assert.Empty(InstalledViewModel.ExtractDevelopersList([]));
    }

    [Fact]
    public void ExtractDevelopersList_ExtractsUniqueSortedPublishers()
    {
        var packages = new List<WingetPackage>
        {
            new() { Publisher = " Microsoft " },
            new() { Publisher = "Adobe" },
            new() { Publisher = "microsoft" }
        };
        var devs = InstalledViewModel.ExtractDevelopersList(packages);
        Assert.Equal(2, devs.Count);
        Assert.Equal("Adobe", devs[0]);
        Assert.Equal("Microsoft", devs[1]);
    }
}
