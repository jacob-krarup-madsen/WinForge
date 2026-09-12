namespace WingetStore.Tests;

public class PackageFilteringHelperEdgeTests
{
    [Fact]
    public void MatchesQuery_NullPackage_ReturnsFalse()
    {
        Assert.False(PackageFilteringHelper.MatchesQuery(null!, "test"));
    }

    [Fact]
    public void MatchesQuery_EmptyQuery_ReturnsTrue()
    {
        var pkg = new WingetPackage { Id = "Test", Name = "Test" };
        Assert.True(pkg.MatchesQuery(""));
        Assert.True(pkg.MatchesQuery(null!));
        Assert.True(pkg.MatchesQuery("   "));
    }

    [Fact]
    public void MatchesQuery_TagMatch_ReturnsTrue()
    {
        var pkg = new WingetPackage { Id = "Test", Name = "Test", Tags = new List<string> { "utility" } };
        Assert.True(pkg.MatchesQuery("tag:utility"));
    }

    [Fact]
    public void MatchesQuery_TagNoMatch_ReturnsFalse()
    {
        var pkg = new WingetPackage { Id = "Test", Name = "Test", Tags = new List<string> { "utility" } };
        Assert.False(pkg.MatchesQuery("tag:unknown"));
    }

    [Fact]
    public void MatchesQuery_NullProperties_NoException()
    {
        var pkg = new WingetPackage { Id = null!, Name = null!, Publisher = null!, Description = null! };
        Assert.False(pkg.MatchesQuery("test"));
    }

    [Theory]
    [InlineData("all", "winget", true)]
    [InlineData("all", null, true)]
    [InlineData("all", "", true)]
    [InlineData("winget", "winget", true)]
    [InlineData("winget", "WINGET", true)]
    [InlineData("winget", "other", false)]
    [InlineData("winget", null, false)]
    public void MatchesSourceFilter_ReturnsCorrectResult(string filter, string? source, bool expected)
    {
        Assert.Equal(expected, PackageFilteringHelper.MatchesSourceFilter(source, filter));
    }

    [Fact]
    public void FilterAndSortPackages_FiltersByQueryAndSourceAndSorts()
    {
        var packages = new List<WingetPackage>
        {
            new() { Name = "Brave", Id = "Brave.Brave", Source = "winget" },
            new() { Name = "Zoom", Id = "Zoom.Zoom", Source = "msstore" },
            new() { Name = "DBeaver", Id = "DBeaver.DBeaver", Source = "winget" }
        };
        var result = PackageFilteringHelper.FilterAndSortPackages(packages, "brave", "all");
        Assert.Single(result);
        Assert.Equal("Brave", result[0].Name);
    }

    [Fact]
    public void FilterAndSortPackages_EmptyQuery_ReturnsAllSorted()
    {
        var packages = new List<WingetPackage>
        {
            new() { Name = "Zebra", Id = "Z.Z" },
            new() { Name = "Alpha", Id = "A.A" }
        };
        var result = PackageFilteringHelper.FilterAndSortPackages(packages, "", "all", "name");
        Assert.Equal(2, result.Count);
        Assert.Equal("Zebra", result[0].Name);
    }
}
