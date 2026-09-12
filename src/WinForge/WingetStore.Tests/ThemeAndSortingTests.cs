namespace WingetStore.Tests;

public class ThemeAndSortingTests
{
    [Theory]
    [InlineData("Light", Microsoft.UI.Xaml.ElementTheme.Light)]
    [InlineData("Dark", Microsoft.UI.Xaml.ElementTheme.Dark)]
    [InlineData("Default", Microsoft.UI.Xaml.ElementTheme.Default)]
    [InlineData("Unknown", Microsoft.UI.Xaml.ElementTheme.Default)]
    public void ParseTheme_ReturnsExpectedTheme(string themeString, Microsoft.UI.Xaml.ElementTheme expectedTheme)
    {
        var actual = App.ParseTheme(themeString);
        Assert.Equal(expectedTheme, actual);
    }

    [Fact]
    public void SortPackages_SortsByPropertyAndDirection()
    {
        var packages = new List<WingetPackage>
        {
            new() { Name = "Alpha", Id = "A.Id", Publisher = "Publisher A", Version = "1.0" },
            new() { Name = "Zebra", Id = "B.Id", Publisher = "Publisher B", Version = "2.0" }
        };

        // High to Low (Descending) by default
        PackageFilteringHelper.SortPackages(packages, "Name", "Descending");
        Assert.Equal("Zebra", packages[0].Name);

        // Low to High (Ascending)
        PackageFilteringHelper.SortPackages(packages, "Name", "Ascending");
        Assert.Equal("Alpha", packages[0].Name);

        // High to Low by Publisher
        PackageFilteringHelper.SortPackages(packages, "Publisher", "Descending");
        Assert.Equal("Publisher B", packages[0].Publisher);

        // High to Low by Id
        PackageFilteringHelper.SortPackages(packages, "Id", "Descending");
        Assert.Equal("B.Id", packages[0].Id);
    }

    [Fact]
    public void SortPackages_SortsByVersion()
    {
        var packages = new List<WingetPackage>
        {
            new() { Name = "App", Version = "1.0.0" },
            new() { Name = "App", Version = "2.0.0" }
        };
        PackageFilteringHelper.SortPackages(packages, "Version", "Descending");
        Assert.Equal("2.0.0", packages[0].Version);
        PackageFilteringHelper.SortPackages(packages, "Version", "Ascending");
        Assert.Equal("1.0.0", packages[0].Version);
    }

    [Fact]
    public void SortPackages_FallbackToDefaultSortByName()
    {
        var packages = new List<WingetPackage>
        {
            new() { Name = "Zed" },
            new() { Name = "Alpha" }
        };
        PackageFilteringHelper.SortPackages(packages, "UnknownField", "Ascending");
        Assert.Equal("Alpha", packages[0].Name);
    }

    [Theory]
    [InlineData("Google.Chrome", "Installed", "Google")]
    [InlineData("Microsoft.PowerToys", "Installed", "Microsoft")]
    [InlineData("Discord.Discord", "Discord Inc.", "Discord Inc.")]
    [InlineData("SingleWordId", "", "SingleWordId")]
    public void Publisher_DerivesFromId_WhenEmptyOrInstalled(string id, string explicitPublisher, string expectedPublisher)
    {
        var package = new WingetPackage { Id = id, Publisher = explicitPublisher };
        Assert.Equal(expectedPublisher, package.Publisher);
    }
    [Theory]
    [InlineData("Antigravity 2.3.1", "Antigravity")]
    [InlineData("Ente Auth version 4.4.24+1048", "Ente Auth")]
    [InlineData("Everything 1.4.1.1032 (x64)", "Everything")]
    [InlineData("LightBulb 2.6.3 (x86)", "LightBulb")]
    [InlineData("Normal App Name x64", "Normal App Name")]
    public void DisplayTitle_StripsVersionNumbersAndArchitecture(string originalName, string expectedCleanTitle)
    {
        var package = new WingetPackage { Name = originalName };
        Assert.Equal(expectedCleanTitle, package.DisplayTitle);
    }

    [Theory]
    [InlineData("Microsoft Visual C++ 2015-2022 Redistributable (x64)", true)]
    [InlineData("Microsoft .NET Desktop Runtime 8.0.1 (x64)", true)]
    [InlineData("Microsoft Edge WebView2 Runtime", true)]
    [InlineData("Google Chrome", false)]
    [InlineData("Discord", false)]
    public void IsRedistributable_DetectsRuntimesAndRedists(string name, bool expectedIsRedist)
    {
        var package = new WingetPackage { Name = name };
        Assert.Equal(expectedIsRedist, package.IsRedistributable);
    }

    [Theory]
    [InlineData(0, 1, 0, 0)]
    [InlineData(300, 1, 300, 0)]
    [InlineData(631, 1, 631, 0)]
    [InlineData(632, 2, 316, 16)]
    [InlineData(947, 2, 473.5, 16)]
    [InlineData(948, 3, 316, 16)]
    [InlineData(1263, 3, 421, 16)]
    [InlineData(1264, 4, 316, 16)]
    [InlineData(1579, 4, 394.75, 16)]
    [InlineData(1580, 5, 316, 16)]
    public void GridCalculator_OptionB_Boundaries(double usableWidth, int expectedCols, double expectedSlotWidth, double expectedGap)
    {
        var dims = GridCalculator.CalculateGridDimensions(usableWidth);
        Assert.Equal(expectedCols, dims.Columns);
        Assert.Equal(expectedSlotWidth, dims.SlotWidth, 2);
        Assert.Equal(expectedGap, dims.EffectiveGap);
        Assert.Equal(Math.Max(0, dims.SlotWidth - dims.EffectiveGap), dims.CardWidth, 2);
    }

    [Fact]
    public void GridCalculator_ValidatesArguments()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => GridCalculator.CalculateGridDimensions(500, minCardWidth: 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => GridCalculator.CalculateGridDimensions(500, gap: -1));
        Assert.Throws<ArgumentOutOfRangeException>(() => GridCalculator.CalculateGridDimensions(500, maxColumns: 0));
    }

    [Theory]
    [InlineData("1.0.0", "1.0.0", 0)]
    [InlineData("1.0.1", "1.0.0", 1)]
    [InlineData("v1.2.3", "1.2.3", 0)]
    [InlineData("v2.0.0", "1.9.9", 1)]
    [InlineData("1.0.0-alpha", "1.0.0", -1)]
    public void VersionComparer_OptionB_Comparisons(string v1, string v2, int expectedSign)
    {
        int result = VersionComparer.Instance.Compare(v1, v2);
        if (expectedSign == 0) Assert.Equal(0, result);
        else if (expectedSign > 0) Assert.True(result > 0);
        else Assert.True(result < 0);
    }
}
