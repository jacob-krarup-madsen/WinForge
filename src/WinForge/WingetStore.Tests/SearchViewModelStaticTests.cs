namespace WingetStore.Tests;

public class SearchViewModelStaticTests
{
    [Fact]
    public void FilterAndSortSearchResults_NullInput_ReturnsEmptyList()
    {
        Assert.Empty(SearchViewModel.FilterAndSortSearchResults(null, "", "all", "default"));
    }

    [Fact]
    public void FilterAndSortSearchResults_FiltersQueryAndSource()
    {
        var packages = new List<WingetPackage>
        {
            new() { Id = "App.Git", Name = "Git", Source = "winget" },
            new() { Id = "App.Git2", Name = "Git GUI", Source = "msstore" },
            new() { Id = "App.VSCode", Name = "VS Code", Source = "winget" }
        };

        var results = SearchViewModel.FilterAndSortSearchResults(packages, "Git", "winget", "az");
        Assert.Single(results);
        Assert.Equal("App.Git", results[0].Id);
    }

    [Fact]
    public void FilterAndSortSearchResults_DefaultSort_PutsWingetFirst()
    {
        var packages = new List<WingetPackage>
        {
            new() { Id = "App1", Source = "msstore" },
            new() { Id = "App2", Source = "winget" }
        };

        var results = SearchViewModel.FilterAndSortSearchResults(packages, "", "all", "default");
        Assert.Equal(2, results.Count);
        Assert.Equal("winget", results[0].Source);
        Assert.Equal("msstore", results[1].Source);
    }

    [Fact]
    public void FilterAndSortSearchResults_CustomSort_SortsByNameDescending()
    {
        var packages = new List<WingetPackage>
        {
            new() { Id = "AppA", Name = "Alpha", Source = "winget" },
            new() { Id = "AppZ", Name = "Zeta", Source = "winget" }
        };

        var results = SearchViewModel.FilterAndSortSearchResults(packages, "", "all", "za");
        Assert.Equal(2, results.Count);
        Assert.Equal("Zeta", results[0].Name);
        Assert.Equal("Alpha", results[1].Name);
    }
}
