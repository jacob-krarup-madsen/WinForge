namespace WingetStore.Tests;

public class HomePageHelperTests
{
    [Theory]
    [InlineData(1.0, 130.0, 146.0)]
    [InlineData(1.5, 154.0, 170.0)]
    [InlineData(1.74, 154.0, 170.0)]
    [InlineData(1.75, 186.0, 202.0)]
    [InlineData(1.99, 186.0, 202.0)]
    [InlineData(2.0, 218.0, 234.0)]
    [InlineData(2.24, 218.0, 234.0)]
    [InlineData(2.25, 250.0, 266.0)]
    [InlineData(3.0, 250.0, 266.0)]
    public void GetTextScaleData_ReturnsCorrectDimensions(double factor, double expectedCardHeight, double expectedItemHeight)
    {
        var (cardHeight, itemHeight) = HomePage.GetTextScaleData(factor);
        Assert.Equal(expectedCardHeight, cardHeight);
        Assert.Equal(expectedItemHeight, itemHeight);
    }

    [Fact]
    public void GetTextScaleData_ZeroFactor_UsesDefault()
    {
        var (cardHeight, itemHeight) = HomePage.GetTextScaleData(0);
        Assert.Equal(130.0, cardHeight);
        Assert.Equal(146.0, itemHeight);
    }

    [Theory]
    [InlineData("", null, "")]
    [InlineData("a", "Enter at least 2 characters to search", null)]
    [InlineData("ab", null, "ab")]
    [InlineData("hello world", null, "hello world")]
    public void GetSearchInputData_ReturnsExpected(string normalized, string? expectedHint, string? expectedQuery)
    {
        var (hint, query) = HomePage.GetSearchInputData(normalized);
        Assert.Equal(expectedHint, hint);
        Assert.Equal(expectedQuery, query);
    }

    [Fact]
    public void HomePage_HasNoDeadSearchCancellationFields()
    {
        var fieldNames = typeof(HomePage).GetFields(
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
            .Select(f => f.Name).ToList();

        Assert.DoesNotContain("_currentGenerationId", fieldNames);
        Assert.DoesNotContain("_partialResultsGenerationId", fieldNames);
        Assert.DoesNotContain("_searchCts", fieldNames);
        Assert.Contains("_currentNormalizedQuery", fieldNames);
    }

    [Fact]
    public void BuildRecommendationCards_NullPackages_ReturnsEmpty()
    {
        var cards = HomePage.BuildRecommendationCards(null!, new RecommendationLayoutState());
        Assert.Empty(cards);
    }

    [Fact]
    public void BuildRecommendationCards_EmptyPackages_ReturnsEmpty()
    {
        var cards = HomePage.BuildRecommendationCards([], new RecommendationLayoutState());
        Assert.Empty(cards);
    }

    [Fact]
    public void BuildRecommendationCards_MixedPackages_ReturnsOneCardPerPackage()
    {
        var pkg1 = new WingetPackage { Id = "Test.One", Name = "Test One" };
        var pkg2 = new WingetPackage { Id = "Test.Two", Name = "Test Two" };
        var pkg3 = new WingetPackage { Id = "Test.Three", Name = "Test Three" };
        var layout = new RecommendationLayoutState();

        var cards = HomePage.BuildRecommendationCards([pkg1, pkg2, pkg3], layout);

        Assert.Equal(3, cards.Count);
        Assert.Same(pkg1, cards[0].Package);
        Assert.Same(pkg2, cards[1].Package);
        Assert.Same(pkg3, cards[2].Package);
        Assert.All(cards, c => Assert.Same(layout, c.LayoutState));
    }

    [Fact]
    public void HomePage_HasClearSearchAndSearchStateChangedEvent()
    {
        var clearSearchMethod = typeof(HomePage).GetMethod("ClearSearch");
        Assert.NotNull(clearSearchMethod);

        var searchStateChangedEvent = typeof(HomePage).GetEvent("SearchStateChanged");
        Assert.NotNull(searchStateChangedEvent);
        Assert.Equal(typeof(EventHandler), searchStateChangedEvent.EventHandlerType);
    }
}
