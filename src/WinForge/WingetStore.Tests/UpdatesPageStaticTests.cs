namespace WingetStore.Tests;

public class UpdatesPageStaticTests
{
    [Fact]
    public void GetUpdatesViewState_ZeroCount_ReturnsEmptyState()
    {
        var (hasItems, showCard, showList, showEmpty, showToolbar, subtitle) = UpdatesPage.GetUpdatesViewState(0);
        Assert.False(hasItems);
        Assert.False(showCard);
        Assert.False(showList);
        Assert.True(showEmpty);
        Assert.True(showToolbar);
        Assert.Equal("", subtitle);
    }

    [Fact]
    public void GetUpdatesViewState_SmallCount_ReturnsCardView()
    {
        var (hasItems, showCard, showList, showEmpty, showToolbar, subtitle) = UpdatesPage.GetUpdatesViewState(2);
        Assert.True(hasItems);
        Assert.True(showCard);
        Assert.False(showList);
        Assert.False(showEmpty);
        Assert.False(showToolbar);
        Assert.Equal("2 updates available", subtitle);
    }

    [Fact]
    public void GetUpdatesViewState_LargeCount_ReturnsListView()
    {
        var (hasItems, showCard, showList, showEmpty, showToolbar, subtitle) = UpdatesPage.GetUpdatesViewState(5);
        Assert.True(hasItems);
        Assert.False(showCard);
        Assert.True(showList);
        Assert.False(showEmpty);
        Assert.True(showToolbar);
        Assert.Equal("5 updates available", subtitle);
    }

    [Theory]
    [InlineData("Descending", "Name", "Name", "\uE74B", Visibility.Visible)]
    [InlineData("Ascending", "Name", "Name", "\uE74A", Visibility.Visible)]
    [InlineData("Descending", "Publisher", "Name", "\uE74B", Visibility.Collapsed)]
    public void GetSortGlyph_ReturnsCorrectGlyphAndVisibility(string dir, string sortBy, string target, string expectedGlyph, Visibility expectedVis)
    {
        var (glyph, vis) = UpdatesPage.GetSortGlyph(dir, sortBy, target);
        Assert.Equal(expectedGlyph, glyph);
        Assert.Equal(expectedVis, vis);
    }
}
