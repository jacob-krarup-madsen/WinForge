namespace WingetStore.Tests;

public class MainWindowHelperTests
{
    [Theory]
    [InlineData(0, false, "0", "Updates, none available")]
    [InlineData(1, true, "1", "Updates, 1 available")]
    [InlineData(5, true, "5", "Updates, 5 available")]
    [InlineData(99, true, "99", "Updates, 99 available")]
    [InlineData(100, true, "99", "Updates, 100 available")]
    public void GetBadgeData_ReturnsCorrectValues(int count, bool expectedVisible, string expectedText, string expectedAutomation)
    {
        var (isVisible, badgeText, automation) = MainWindow.GetBadgeData(count);
        Assert.Equal(expectedVisible, isVisible);
        Assert.Equal(expectedText, badgeText);
        Assert.Equal(expectedAutomation, automation);
    }

    [Fact]
    public void GetBadgeData_NegativeCount_TreatedAsNoUpdates()
    {
        var (isVisible, badgeText, automation) = MainWindow.GetBadgeData(-1);
        Assert.False(isVisible);
        Assert.Equal("0", badgeText);
        Assert.Equal("Updates, none available", automation);
    }

    [Theory]
    [InlineData(ElementTheme.Dark, "\uE706", "Switch to light theme")]
    [InlineData(ElementTheme.Light, "\uE708", "Switch to dark theme")]
    public void GetThemeToggleData_ReturnsCorrectGlyph(ElementTheme theme, string expectedGlyph, string expectedLabel)
    {
        var (glyph, label) = MainWindow.GetThemeToggleData(theme);
        Assert.Equal(expectedGlyph, glyph);
        Assert.Equal(expectedLabel, label);
    }

    [Theory]
    [MemberData(nameof(PageTypeData))]
    public void IsTopLevelPage_ReturnsCorrectResult(Type? pageType, bool expected)
    {
        if (pageType == null)
            Assert.False(MainWindow.IsTopLevelPage(null!));
        else
            Assert.Equal(expected, MainWindow.IsTopLevelPage(pageType));
    }

    public static IEnumerable<object[]> PageTypeData()
    {
        yield return [typeof(HomePage), true];
        yield return [typeof(InstalledPage), true];
        yield return [typeof(UpdatesPage), true];
        yield return [typeof(SettingsPage), true];
        yield return [typeof(AboutPage), true];
        yield return [typeof(NoWingetPage), true];
        yield return [typeof(FeaturesPage), true];
        yield return [typeof(OptimizerPage), true];
        yield return [typeof(DetailsPage), false];
    }

    [Theory]
    [InlineData(ElementTheme.Dark, ElementTheme.Dark, ElementTheme.Dark)]
    [InlineData(ElementTheme.Light, ElementTheme.Dark, ElementTheme.Light)]
    [InlineData(ElementTheme.Default, ElementTheme.Dark, ElementTheme.Dark)]
    [InlineData(ElementTheme.Default, ElementTheme.Light, ElementTheme.Light)]
    [InlineData(null, ElementTheme.Dark, ElementTheme.Dark)]
    [InlineData(null, ElementTheme.Light, ElementTheme.Light)]
    public void ResolveCurrentTheme_ReturnsExpected(ElementTheme? requested, ElementTheme actual, ElementTheme expected)
    {
        Assert.Equal(expected, MainWindow.ResolveCurrentTheme(requested, actual));
    }
}
