namespace WingetStore.Tests;

public class FilterableViewModelStaticTests
{
    [Theory]
    [InlineData(0, "Applications (0)")]
    [InlineData(42, "Applications (42)")]
    public void FormatAppsCountText_ReturnsExpected(int count, string expected)
    {
        Assert.Equal(expected, FilterableViewModel.FormatAppsCountText(count));
    }

    [Theory]
    [InlineData(0, "Redistributables (0)")]
    [InlineData(15, "Redistributables (15)")]
    public void FormatRedistCountText_ReturnsExpected(int count, string expected)
    {
        Assert.Equal(expected, FilterableViewModel.FormatRedistCountText(count));
    }

    [Theory]
    [InlineData(0, "All (0)")]
    [InlineData(100, "All (100)")]
    public void FormatAllCountText_ReturnsExpected(int count, string expected)
    {
        Assert.Equal(expected, FilterableViewModel.FormatAllCountText(count));
    }

    [Theory]
    [InlineData("Apps", "Apps", true)]
    [InlineData("apps", "Apps", true)]
    [InlineData("Redist", "Apps", false)]
    [InlineData(null, "Apps", false)]
    public void IsCategorySelected_ReturnsExpectedBool(string? category, string target, bool expected)
    {
        Assert.Equal(expected, FilterableViewModel.IsCategorySelected(category, target));
    }

    [Fact]
    public void ResolveCategorySelection_ReturnsExpectedCategory()
    {
        Assert.Equal("Apps", FilterableViewModel.ResolveCategorySelection("Redist", "Apps", true));
        Assert.Equal("Redist", FilterableViewModel.ResolveCategorySelection("Redist", "Apps", false));
        Assert.Equal("", FilterableViewModel.ResolveCategorySelection(null, "Apps", false));
    }

    [Theory]
    [InlineData(false, "Apps", true)]
    [InlineData(true, "Apps", false)]
    [InlineData(false, "Redist", false)]
    [InlineData(true, "Redist", true)]
    [InlineData(true, "All", true)]
    [InlineData(false, "All", true)]
    [InlineData(true, null, true)]
    public void MatchesCategoryFilter_ReturnsExpected(bool isRedistributable, string? categoryFilter, bool expected)
    {
        Assert.Equal(expected, FilterableViewModel.MatchesCategoryFilter(isRedistributable, categoryFilter));
    }

    [Theory]
    [InlineData("az", "Name", "Ascending")]
    [InlineData("za", "Name", "Descending")]
    [InlineData("publisher", "Publisher", "Ascending")]
    [InlineData("id", "Id", "Ascending")]
    [InlineData("status", "Version", "Descending")]
    public void MapSortOrder_ValidPresets_ReturnsCorrectTuple(string order, string expectedBy, string expectedDir)
    {
        var (by, dir) = FilterableViewModel.MapSortOrder(order);
        Assert.Equal(expectedBy, by);
        Assert.Equal(expectedDir, dir);
    }

    [Fact]
    public void MapSortOrder_UnknownOrNullOrder_PreservesCurrentValues()
    {
        var (by, dir) = FilterableViewModel.MapSortOrder("unknown", "CustomBy", "CustomDir");
        Assert.Equal("CustomBy", by);
        Assert.Equal("CustomDir", dir);

        var (byNull, dirNull) = FilterableViewModel.MapSortOrder(null, "CustomBy", "CustomDir");
        Assert.Equal("CustomBy", byNull);
        Assert.Equal("CustomDir", dirNull);
    }
}
