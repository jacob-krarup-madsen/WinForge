namespace WingetStore.Tests;

public class NavigationHelperTests
{
    [Fact]
    public void GetPageType_NoWinget_ReturnsNoWingetPage()
    {
        var type = NavigationHelper.GetPageType("home", false, false);
        Assert.Equal(typeof(Pages.NoWingetPage), type);
    }

    [Fact]
    public void GetPageType_SettingsSelected_ReturnsSettingsPage()
    {
        var type = NavigationHelper.GetPageType(null, true, true);
        Assert.Equal(typeof(Pages.SettingsPage), type);
    }

    [Theory]
    [InlineData("home", typeof(Pages.HomePage))]
    [InlineData("search", typeof(Pages.HomePage))]

    [InlineData("installed", typeof(Pages.InstalledPage))]
    [InlineData("updates", typeof(Pages.UpdatesPage))]
    [InlineData("about", typeof(Pages.AboutPage))]
    public void GetPageType_ValidTag_ReturnsExpectedPage(string tag, Type expected)
    {
        var type = NavigationHelper.GetPageType(tag, false, true);
        Assert.Equal(expected, type);
    }

    [Fact]
    public void GetPageType_UnknownTag_ReturnsNull()
    {
        var type = NavigationHelper.GetPageType("unknown", false, true);
        Assert.Null(type);
    }

    [Fact]
    public void GetPageType_NullTagAndNotSettings_ReturnsNull()
    {
        var type = NavigationHelper.GetPageType(null, false, true);
        Assert.Null(type);
    }

    [Fact]
    public void GetPageType_NoWingetTakesPriorityOverSettings()
    {
        var type = NavigationHelper.GetPageType(null, true, false);
        Assert.Equal(typeof(Pages.NoWingetPage), type);
    }

    [Fact]
    public void GetPageType_EmptyString_ReturnsNull()
    {
        var type = NavigationHelper.GetPageType("", false, true);
        Assert.Null(type);
    }

}
