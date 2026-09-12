namespace WingetStore.Tests;

public class IconServiceCoverageTests
{
    [Fact]
    public void GetSafeIconFileName_NullOrEmpty_ReturnsUnknown()
    {
        Assert.Equal("unknown.png", IconService.GetSafeIconFileName(null!));
        Assert.Equal("unknown.png", IconService.GetSafeIconFileName(""));
        Assert.Equal("unknown.png", IconService.GetSafeIconFileName("   "));
    }

    [Fact]
    public void GetSafeIconFileName_SanitizesInvalidChars()
    {
        var result = IconService.GetSafeIconFileName(@"Test/App:Name");
        Assert.Contains(".png", result);
        Assert.DoesNotContain("/", result);
        Assert.DoesNotContain(":", result);
    }

    [Fact]
    public void GetSafeIconFileName_DoubleDots_Replaced()
    {
        var result = IconService.GetSafeIconFileName("Test..App");
        Assert.DoesNotContain("..", result.Replace(".png", ""));
    }

    [Fact]
    public void GetIconUrl_NullPackageId_ReturnsEmpty()
    {
        var service = IconService.Instance;
        Assert.Equal("", service.GetIconUrl(null!, "Name"));
        Assert.Equal("", service.GetIconUrl("", "Name"));
    }

    [Fact]
    public void GetScreenshots_NonExistentPackage_ReturnsEmpty()
    {
        var service = IconService.Instance;
        Assert.Empty(service.GetScreenshots("Does.Not.Exist", "Does Not Exist"));
    }

    [Fact]
    public void GetScreenshots_NullPackageId_ReturnsEmpty()
    {
        var service = IconService.Instance;
        Assert.Empty(service.GetScreenshots(null!, "Name"));
    }
}
