namespace WingetStore.Tests;

public class FilterableViewModelHelperTests
{
    [Fact]
    public void MatchesSourceFilter_NullSourceFilter_ReturnsFalse()
    {
        var method = typeof(FilterableViewModel).GetMethod("MatchesSourceFilter",
            System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic);
        var result = method!.Invoke(null, new object[] { "winget", null! });
        Assert.False((bool)result!);
    }

    [Fact]
    public void SortPackages_ExpandedSortOptions_SortsCorrectly()
    {
        var list = new List<WingetPackage>
            {
                new() { Id = "Z.App", Name = "Beta", Publisher = "Zebra Corp", Status = PackageStatus.Installed },
                new() { Id = "A.App", Name = "Alpha", Publisher = "Alpha Inc", Status = PackageStatus.Upgradable },
                new() { Id = "M.App", Name = "Gamma", Publisher = "Beta LLC", Status = PackageStatus.Installable }
            };

        var method = typeof(FilterableViewModel).GetMethod("SortPackages",
            System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic)!;

        var listPublisher = new List<WingetPackage>(list);
        method.Invoke(null, new object[] { listPublisher, "publisher" });
        Assert.Equal("Alpha Inc", listPublisher[0].Publisher);
        Assert.Equal("Beta LLC", listPublisher[1].Publisher);
        Assert.Equal("Zebra Corp", listPublisher[2].Publisher);

        var listId = new List<WingetPackage>(list);
        method.Invoke(null, new object[] { listId, "id" });
        Assert.Equal("A.App", listId[0].Id);
        Assert.Equal("M.App", listId[1].Id);
        Assert.Equal("Z.App", listId[2].Id);

        var listStatus = new List<WingetPackage>(list);
        method.Invoke(null, new object[] { listStatus, "status" });
        Assert.Equal(PackageStatus.Upgradable, listStatus[0].Status);
        Assert.Equal(PackageStatus.Installed, listStatus[1].Status);
        Assert.Equal(PackageStatus.Installable, listStatus[2].Status);
    }
}
