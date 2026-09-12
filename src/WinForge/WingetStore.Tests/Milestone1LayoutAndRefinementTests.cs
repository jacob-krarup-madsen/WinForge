namespace WingetStore.Tests;

public class Milestone1LayoutAndRefinementTests
{
    [Theory]
    [InlineData(600, Controls.ResponsiveBand.Narrow)]
    [InlineData(699, Controls.ResponsiveBand.Narrow)]
    [InlineData(700, Controls.ResponsiveBand.Medium)]
    [InlineData(1199, Controls.ResponsiveBand.Medium)]
    [InlineData(1200, Controls.ResponsiveBand.Wide)]
    [InlineData(1920, Controls.ResponsiveBand.Wide)]
    public void ResponsiveBand_CalculatesCorrectBands(double width, Controls.ResponsiveBand expectedBand)
    {
        var band = Controls.ResponsivePageContainer.GetBand(width);
        Assert.Equal(expectedBand, band);
    }

    [Theory]
    [InlineData(Controls.ResponsiveBand.Narrow, 16, 16, 16, 24)]
    [InlineData(Controls.ResponsiveBand.Medium, 24, 20, 24, 28)]
    [InlineData(Controls.ResponsiveBand.Wide, 32, 24, 32, 32)]
    public void ResponsiveBand_ReturnsCorrectPaddings(Controls.ResponsiveBand band, double left, double top, double right, double bottom)
    {
        var padding = Controls.ResponsivePageContainer.GetPadding(band);
        Assert.Equal(left, padding.Left);
        Assert.Equal(top, padding.Top);
        Assert.Equal(right, padding.Right);
        Assert.Equal(bottom, padding.Bottom);
    }

    [Fact]
    public void ResponsiveBand_WidthZero_ReturnsNarrow()
    {
        Assert.Equal(Controls.ResponsiveBand.Narrow, Controls.ResponsivePageContainer.GetBand(0));
    }

    [Fact]
    public void ResponsiveBand_NegativeWidth_ReturnsNarrow()
    {
        Assert.Equal(Controls.ResponsiveBand.Narrow, Controls.ResponsivePageContainer.GetBand(-1));
    }
}
