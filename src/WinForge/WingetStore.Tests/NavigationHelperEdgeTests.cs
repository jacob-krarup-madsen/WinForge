namespace WingetStore.Tests;

public class NavigationHelperEdgeTests
{
    [Fact]
    public void GetPageType_UnknownTag_ReturnsNull()
    {
        Assert.Null(NavigationHelper.GetPageType("nonexistent", false, true));
    }

    [Fact]
    public void GetPageType_EmptyTag_ReturnsNull()
    {
        Assert.Null(NavigationHelper.GetPageType("", false, true));
    }

    [Fact]
    public void GetPageType_NullTagNoSettingsNoWinget_ReturnsNoWingetPage()
    {
        var type = NavigationHelper.GetPageType(null, false, false);
        Assert.Equal(typeof(Pages.NoWingetPage), type);
    }

    [Fact]
    public void GetPageType_SettingsNoWinget_ReturnsNoWingetPage()
    {
        var type = NavigationHelper.GetPageType(null, true, false);
        Assert.Equal(typeof(Pages.NoWingetPage), type);
    }

    [Fact]
    public void GetPageType_SettingsWingetAvailable_ReturnsSettingsPage()
    {
        var type = NavigationHelper.GetPageType(null, true, true);
        Assert.Equal(typeof(Pages.SettingsPage), type);
    }

    [Theory]
    [InlineData("home")]
    [InlineData("search")]
    public void GetPageType_HomeAndSearchTags_ReturnHomePage(string tag)
    {
        var type = NavigationHelper.GetPageType(tag, false, true);
        Assert.Equal(typeof(Pages.HomePage), type);
    }

    [Theory]
    [InlineData("installed")]
    [InlineData("updates")]
    [InlineData("about")]
    public void GetPageType_ValidTags_ReturnCorrectPage(string tag)
    {
        var type = NavigationHelper.GetPageType(tag, false, true);
        Assert.NotNull(type);
    }
}
