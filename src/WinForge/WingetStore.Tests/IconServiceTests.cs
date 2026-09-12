namespace WingetStore.Tests;

public class IconServiceTests
{
    [Fact]
    public void LocalPathVerification()
    {
        var service = IconService.Instance;
        var iconUrl = service.GetIconUrl("Test.Package.DoesNotExist", "Does Not Exist");
        Assert.Equal("", iconUrl);
    }

    [Fact]
    public void FailedIdsRegistry()
    {
        var service = IconService.Instance;
        var iconUrlFirst = service.GetIconUrl("Dummy.Failed.App", "Failed App");
        var iconUrlSecond = service.GetIconUrl("Dummy.Failed.App", "Failed App");
        Assert.Equal("", iconUrlFirst);
        Assert.Equal("", iconUrlSecond);
    }

    [Fact]
    public void GetScreenshots_ResolvesCorrectly()
    {
        var service = IconService.Instance;
        var screenshots = service.GetScreenshots("Mock.App.Nonexistent", "Nonexistent App");
        Assert.Empty(screenshots);
    }
}
