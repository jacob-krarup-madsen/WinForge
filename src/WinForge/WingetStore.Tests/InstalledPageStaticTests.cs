namespace WingetStore.Tests;

public class InstalledPageStaticTests
{
    [Theory]
    [InlineData("Name", "Descending", "Name", "Name", "Ascending")]
    [InlineData("Name", "Ascending", "Name", "Name", "Descending")]
    [InlineData("Name", "Descending", "Publisher", "Publisher", "Descending")]
    [InlineData("Version", "Ascending", "Name", "Name", "Descending")]
    public void ToggleColumnSort_ReturnsExpectedNewSort(string currentSortBy, string currentDir, string target, string expectedSortBy, string expectedDir)
    {
        var (newSortBy, newDir) = InstalledPage.ToggleColumnSort(currentSortBy, currentDir, target);
        Assert.Equal(expectedSortBy, newSortBy);
        Assert.Equal(expectedDir, newDir);
    }
}
