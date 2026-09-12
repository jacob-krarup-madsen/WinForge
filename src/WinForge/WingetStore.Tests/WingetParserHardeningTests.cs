namespace WingetStore.Tests;

public class WingetParserHardeningTests
{
    [Theory]
    [InlineData("Hello World", -1, 5, "")]
    [InlineData("Hello World", 20, 25, "")]
    [InlineData("Hello World", 5, 2, "")]
    [InlineData("Hello World", 0, 100, "Hello World")]
    [InlineData("Hello World", 0, 5, "Hello")]
    public void GetSubstring_HandlesOutOfBoundsIndicesSafely(string line, int start, int endExclusive, string expected)
    {
        string actual = WingetParser.GetSubstring(line, start, endExclusive);
        Assert.Equal(expected, actual);
    }

    [Fact]
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Maintainability", "CA1508:Avoid dead code", Justification = "Test verification of ARP filter")]
    public void ParseDetailsList_FiltersArpEntriesCaseInsensitively()
    {
        string sampleOutput = "(1/2) App One [ARP\\App1]\r\n(2/2) App Two [arp\\App2]\r\n(3/3) App Three [Vendor.App3]\r\n";
        var result = WingetParser.ParseDetailsList(sampleOutput);
        Assert.Single(result);
        Assert.Equal("Vendor.App3", result[0].Id);
    }
}
