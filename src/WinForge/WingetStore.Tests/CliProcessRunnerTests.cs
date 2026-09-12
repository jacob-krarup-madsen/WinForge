namespace WingetStore.Tests;

public class CliProcessRunnerTests
{
    [Fact]
    public async Task RunStreamAsync_CapturesStdout()
    {
        var runner = new CliProcessRunner();
        var lines = new List<string>();
        int exitCode = await runner.RunStreamAsync("cmd.exe", "/c echo hello-world", s => lines.Add(s), TestContext.Current.CancellationToken);
        Assert.Equal(0, exitCode);
        Assert.Contains(lines, l => l.Contains("hello-world"));
    }

    [Fact]
    public async Task RunStreamAsync_ReturnsExitCode()
    {
        var runner = new CliProcessRunner();
        int exitCode = await runner.RunStreamAsync("cmd.exe", "/c exit 42", _ => { }, TestContext.Current.CancellationToken);
        Assert.Equal(42, exitCode);
    }

    [Fact]
    public async Task RunStreamAsync_CancellationKillsProcess()
    {
        var runner = new CliProcessRunner();
        using var cts = new CancellationTokenSource();
        var lines = new List<string>();
        var task = runner.RunStreamAsync("cmd.exe", "/c ping -n 10 127.0.0.1", s => lines.Add(s), cts.Token);
        cts.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => task);
    }

    [Fact]
    public async Task RunStreamAsync_WingetVersion()
    {
        var runner = new CliProcessRunner();
        var lines = new List<string>();
        int exitCode = await runner.RunStreamAsync("winget.exe", "--version", s => lines.Add(s), TestContext.Current.CancellationToken);
        Assert.Equal(0, exitCode);
        Assert.NotEmpty(lines);
    }
}

