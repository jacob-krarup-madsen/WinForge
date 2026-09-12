namespace WingetStore.Tests;

public class RunCommandAsyncNullLineTests
{
    [Fact]
    public async Task RunCommandAsync_NullLine_DoesNotThrow()
    {
        var runner = new NullLineRunner();
        var service = new WingetService(runner);

        var result = await service.RunCommandAsync("list", TestContext.Current.CancellationToken);

        Assert.Equal("", result);
    }
}
