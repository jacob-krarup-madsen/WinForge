namespace WingetStore.Tests;

public class HomeViewModelStaticTests
{
    [Theory]
    [InlineData("  git  ", false, true, "git", "git")]
    [InlineData("vscode", false, true, "vscode", "vscode")]
    public void ProcessSearchQuery_ValidQuery_ReturnsShouldSearchTrue(string input, bool forceAll, bool expectedShould, string expectedClean, string expectedDisplay)
    {
        var (shouldSearch, cleanQuery, displayQuery) = HomeViewModel.ProcessSearchQuery(input, forceAll);
        Assert.Equal(expectedShould, shouldSearch);
        Assert.Equal(expectedClean, cleanQuery);
        Assert.Equal(expectedDisplay, displayQuery);
    }

    [Theory]
    [InlineData("", false, false, "", "All Applications")]
    [InlineData(null, false, false, "", "All Applications")]
    [InlineData("   ", false, false, "", "All Applications")]
    public void ProcessSearchQuery_EmptyQueryNoForce_ReturnsShouldSearchFalse(string? input, bool forceAll, bool expectedShould, string expectedClean, string expectedDisplay)
    {
        var (shouldSearch, cleanQuery, displayQuery) = HomeViewModel.ProcessSearchQuery(input, forceAll);
        Assert.Equal(expectedShould, shouldSearch);
        Assert.Equal(expectedClean, cleanQuery);
        Assert.Equal(expectedDisplay, displayQuery);
    }

    [Theory]
    [InlineData("", true, true, "", "All Applications")]
    [InlineData(null, true, true, "", "All Applications")]
    public void ProcessSearchQuery_EmptyQueryForced_ReturnsShouldSearchTrueAndFallbackDisplay(string? input, bool forceAll, bool expectedShould, string expectedClean, string expectedDisplay)
    {
        var (shouldSearch, cleanQuery, displayQuery) = HomeViewModel.ProcessSearchQuery(input, forceAll);
        Assert.Equal(expectedShould, shouldSearch);
        Assert.Equal(expectedClean, cleanQuery);
        Assert.Equal(expectedDisplay, displayQuery);
    }

    [Fact]
    public void FilterAndSortRecommendations_NullOrEmptyInput_ReturnsEmptyList()
    {
        Assert.Empty(HomeViewModel.FilterAndSortRecommendations(null, "", "az"));
        Assert.Empty(HomeViewModel.FilterAndSortRecommendations([], "git", "az"));
    }

    [Fact]
    public void FilterAndSortRecommendations_FiltersByQueryAndSortsByName()
    {
        var packages = new List<WingetPackage>
        {
            new() { Id = "Git.Git", Name = "Git for Windows" },
            new() { Id = "Microsoft.VSCode", Name = "Visual Studio Code" },
            new() { Id = "Git.GitHubDesktop", Name = "GitHub Desktop" }
        };

        var result = HomeViewModel.FilterAndSortRecommendations(packages, "Git", "az");
        Assert.Equal(2, result.Count);
        Assert.Equal("Git for Windows", result[0].Name);
        Assert.Equal("GitHub Desktop", result[1].Name);
    }

    [Fact]
    public void FilterAndSortSearchResults_NullInput_ReturnsEmptyList()
    {
        Assert.Empty(HomeViewModel.FilterAndSortSearchResults(null, "", "all", "default"));
    }

    [Fact]
    public void FilterAndSortSearchResults_DefaultSort_PrioritizesWingetSource()
    {
        var packages = new List<WingetPackage>
        {
            new() { Id = "App1", Name = "App One", Source = "msstore" },
            new() { Id = "App2", Name = "App Two", Source = "winget" }
        };

        var result = HomeViewModel.FilterAndSortSearchResults(packages, "", "all", "default");
        Assert.Equal(2, result.Count);
        Assert.Equal("winget", result[0].Source);
        Assert.Equal("msstore", result[1].Source);
    }

    [Fact]
    public void FilterAndSortSearchResults_SourceFilter_FiltersByWingetSource()
    {
        var packages = new List<WingetPackage>
        {
            new() { Id = "App1", Name = "App One", Source = "msstore" },
            new() { Id = "App2", Name = "App Two", Source = "winget" }
        };

        var result = HomeViewModel.FilterAndSortSearchResults(packages, "", "winget", "az");
        Assert.Single(result);
        Assert.Equal("App2", result[0].Id);
    }
}
