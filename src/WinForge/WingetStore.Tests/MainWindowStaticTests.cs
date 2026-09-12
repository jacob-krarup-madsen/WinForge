using Microsoft.UI.Xaml.Controls;

namespace WingetStore.Tests;

public class MainWindowStaticTests
{
    [Theory]
    [InlineData(800, 500, 1.0, 800, 500)]
    [InlineData(600, 400, 1.0, 800, 500)]
    [InlineData(800, 500, 1.5, 1200, 750)]
    [InlineData(600, 400, 1.25, 1000, 625)]
    [InlineData(900, 600, 2.0, 1800, 1200)]
    [InlineData(0, 0, 1.0, 800, 500)]
    public void GetMinimumWindowSize_ReturnsCorrectDimensions(double w, double h, double scale, int ew, int eh)
    {
        var (pw, ph) = MainWindow.GetMinimumWindowSize(w, h, scale);
        Assert.Equal(ew, pw);
        Assert.Equal(eh, ph);
    }

    [Theory]
    [InlineData(1400, NavigationMode.Desktop)]
    [InlineData(900, NavigationMode.Desktop)]
    [InlineData(1200, NavigationMode.Desktop)]
    [InlineData(899, NavigationMode.Tablet)]
    [InlineData(600, NavigationMode.Tablet)]
    [InlineData(750, NavigationMode.Tablet)]
    [InlineData(599, NavigationMode.Phone)]
    [InlineData(320, NavigationMode.Phone)]
    [InlineData(0, NavigationMode.Phone)]
    public void GetNavigationMode_ReturnsExpectedMode(double width, NavigationMode expected)
    {
        Assert.Equal(expected, MainWindow.GetNavigationMode(width));
    }

    [Theory]
    [InlineData(NavigationMode.Desktop, NavigationViewPaneDisplayMode.Left, true, 64)]
    [InlineData(NavigationMode.Tablet, NavigationViewPaneDisplayMode.LeftCompact, false, 0)]
    [InlineData(NavigationMode.Phone, NavigationViewPaneDisplayMode.LeftMinimal, false, 0)]
    public void GetNavigationModeLayout_ReturnsExpectedLayout(NavigationMode mode, NavigationViewPaneDisplayMode expectedPane, bool expectedFooter, double expectedMargin)
    {
        var (pane, footer, margin) = MainWindow.GetNavigationModeLayout(mode);
        Assert.Equal(expectedPane, pane);
        Assert.Equal(expectedFooter, footer);
        Assert.Equal(expectedMargin, margin);
    }

    [Theory]
    [InlineData("Dark", "Light")]
    [InlineData("Light", "Dark")]
    public void GetNextTheme_ReturnsExpected(string current, string expected)
    {
        var actual = current == "Dark" ? ElementTheme.Dark : ElementTheme.Light;
        string result = MainWindow.GetNextTheme(current, actual);
        Assert.Equal(expected, result);
    }

    [Fact]
    public void GetNextTheme_SystemWithDarkActual_ReturnsLight()
    {
        string result = MainWindow.GetNextTheme("System", ElementTheme.Dark);
        Assert.Equal("Light", result);
    }

    [Fact]
    public void GetNextTheme_SystemWithLightActual_ReturnsDark()
    {
        string result = MainWindow.GetNextTheme("System", ElementTheme.Light);
        Assert.Equal("Dark", result);
    }
}
