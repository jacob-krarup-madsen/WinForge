namespace WingetStore.Tests;

public class PackageDetailHelperTests
{
    [Theory]
    [InlineData("Name", true)]
    [InlineData("Version", true)]
    [InlineData("Description", true)]
    [InlineData("Release Notes", true)]
    [InlineData("Publisher", false)]
    [InlineData("Homepage", false)]
    [InlineData("", false)]
    [InlineData("Installer", false)]
    public void ShouldSkipMetadataItem_ReturnsExpected(string key, bool expected)
    {
        Assert.Equal(expected, PackageDetailHelper.ShouldSkipMetadataItem(key));
    }
}
