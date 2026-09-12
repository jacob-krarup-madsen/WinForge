using Microsoft.UI.Xaml.Controls;

namespace WingetStore.Pages;

public static class SortGlyphUpdater
{
    public static void Apply(string sortBy, string sortDirection, FontIcon? nameGlyph, FontIcon? versionGlyph, FontIcon? publisherGlyph)
    {
        if (nameGlyph != null)
        {
            var (glyph, vis) = InstalledPage.GetSortGlyph(sortDirection, sortBy, "Name");
            nameGlyph.Glyph = glyph;
            nameGlyph.Visibility = vis;
        }

        if (versionGlyph != null)
        {
            var (glyph, vis) = InstalledPage.GetSortGlyph(sortDirection, sortBy, "Version");
            versionGlyph.Glyph = glyph;
            versionGlyph.Visibility = vis;
        }

        if (publisherGlyph != null)
        {
            var (glyph, vis) = InstalledPage.GetSortGlyph(sortDirection, sortBy, "Publisher");
            publisherGlyph.Glyph = glyph;
            publisherGlyph.Visibility = vis;
        }
    }
}
