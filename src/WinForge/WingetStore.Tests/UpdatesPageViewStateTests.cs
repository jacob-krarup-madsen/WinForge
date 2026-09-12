namespace WingetStore.Tests;

public class UpdatesPageViewStateTests
{
    [Fact]
    public void GetUpdatesViewState_ZeroCount_ShowsEmpty()
    {
        var (hasItems, showCardView, showListView, showEmptyState, showFullToolbar, subtitle) = UpdatesPage.GetUpdatesViewState(0);
        Assert.False(hasItems);
        Assert.False(showCardView);
        Assert.False(showListView);
        Assert.True(showEmptyState);
        Assert.True(showFullToolbar);
        Assert.Equal("", subtitle);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public void GetUpdatesViewState_SmallSet_ShowsCardView(int count)
    {
        var (hasItems, showCardView, showListView, showEmptyState, showFullToolbar, subtitle) = UpdatesPage.GetUpdatesViewState(count);
        Assert.True(hasItems);
        Assert.True(showCardView);
        Assert.False(showListView);
        Assert.False(showEmptyState);
        Assert.False(showFullToolbar);
        string expected = count == 1 ? "1 update available" : $"{count} updates available";
        Assert.Equal(expected, subtitle);
    }

    [Theory]
    [InlineData(4)]
    [InlineData(10)]
    [InlineData(100)]
    public void GetUpdatesViewState_LargeSet_ShowsListView(int count)
    {
        var (hasItems, showCardView, showListView, showEmptyState, showFullToolbar, subtitle) = UpdatesPage.GetUpdatesViewState(count);
        Assert.True(hasItems);
        Assert.False(showCardView);
        Assert.True(showListView);
        Assert.False(showEmptyState);
        Assert.True(showFullToolbar);
        Assert.Equal($"{count} updates available", subtitle);
    }
}
