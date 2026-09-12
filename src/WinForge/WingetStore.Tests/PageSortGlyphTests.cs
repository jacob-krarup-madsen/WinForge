namespace WingetStore.Tests;

public class PageSortGlyphTests
{
    [Theory]
    [InlineData("Descending", "Name", "Name", "\uE74B", Visibility.Visible)]
    [InlineData("Ascending", "Name", "Name", "\uE74A", Visibility.Visible)]
    [InlineData("Descending", "Name", "Version", "\uE74B", Visibility.Collapsed)]
    [InlineData("Descending", "Version", "Name", "\uE74B", Visibility.Collapsed)]
    [InlineData("Ascending", "Version", "Version", "\uE74A", Visibility.Visible)]
    public void InstalledPage_GetSortGlyph_ReturnsCorrectValues(string direction, string sortBy, string target, string expectedGlyph, Visibility expectedVis)
    {
        var (glyph, vis) = InstalledPage.GetSortGlyph(direction, sortBy, target);
        Assert.Equal(expectedGlyph, glyph);
        Assert.Equal(expectedVis, vis);
    }

    [Theory]
    [InlineData("Descending", "Name", "Name", "\uE74B", Visibility.Visible)]
    [InlineData("Ascending", "Name", "Name", "\uE74A", Visibility.Visible)]
    [InlineData("Descending", "Name", "Publisher", "\uE74B", Visibility.Collapsed)]
    [InlineData("Ascending", "Publisher", "Publisher", "\uE74A", Visibility.Visible)]
    public void UpdatesPage_GetSortGlyph_ReturnsCorrectValues(string direction, string sortBy, string target, string expectedGlyph, Visibility expectedVis)
    {
        var (glyph, vis) = UpdatesPage.GetSortGlyph(direction, sortBy, target);
        Assert.Equal(expectedGlyph, glyph);
        Assert.Equal(expectedVis, vis);
    }
}
