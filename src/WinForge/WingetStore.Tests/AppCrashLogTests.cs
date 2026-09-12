namespace WingetStore.Tests;

public class AppCrashLogTests
{
    [Fact]
    public void GetCrashLogDirectory_ReturnsWingetStoreUnderLocalAppData()
    {
        string dir = App.GetCrashLogDirectory();
        Assert.Equal(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "WingetStore"), dir);
    }

    [Fact]
    public void GetCrashLogPath_ReturnsCrashLogFileUnderDirectory()
    {
        string path = App.GetCrashLogPath();
        Assert.Equal(Path.Combine(App.GetCrashLogDirectory(), "crash.log"), path);
    }

    [Fact]
    public void GetCrashLogContent_IncludesTimestampAndErrorDetails()
    {
        string content = App.GetCrashLogContent("test error");
        Assert.Contains("test error", content);
        Assert.Contains("[CRASH LOG - ", content);
    }

    [Fact]
    public void GetCrashLogContent_WithTimestamp_FormatsInvariantCulture()
    {
        var timestamp = new DateTime(2026, 7, 23, 18, 0, 0);
        string content = App.GetCrashLogContent("test error", timestamp);
        Assert.Equal("[CRASH LOG - 2026-07-23 18:00:00]\ntest error\n\n", content);
    }

    [Fact]
    public void GetCrashLogContent_WithTimestamp_IncludesErrorDetails()
    {
        var timestamp = new DateTime(2026, 7, 23, 12, 30, 45);
        string content = App.GetCrashLogContent("boom: C:\\path & 'quote'", timestamp);
        Assert.StartsWith("[CRASH LOG - 2026-07-23 12:30:45]", content);
        Assert.Contains("boom: C:\\path & 'quote'", content);
    }

    [Fact]
    public void TryWriteCrashLog_Success_WritesFileAndReturnsTrue()
    {
        string crashLogPath = App.GetCrashLogPath();
        bool result = App.TryWriteCrashLog("synthetic crash details");
        Assert.True(result);
        Assert.True(File.Exists(crashLogPath));
        Assert.Contains("synthetic crash details", File.ReadAllText(crashLogPath));
    }

    [Fact]
    public void TryWriteCrashLog_Failure_ReturnsFalseWhenPathIsDirectory()
    {
        string crashLogPath = App.GetCrashLogPath();
        string? backup = null;
        bool existedAsFile = File.Exists(crashLogPath);
        if (existedAsFile) backup = File.ReadAllText(crashLogPath);
        try
        {
            Directory.CreateDirectory(App.GetCrashLogDirectory());
            if (existedAsFile) File.Delete(crashLogPath);
            Directory.CreateDirectory(crashLogPath);
            Assert.False(App.TryWriteCrashLog("test error details"));
        }
        finally
        {
            if (Directory.Exists(crashLogPath)) Directory.Delete(crashLogPath, true);
            if (backup != null) File.WriteAllText(crashLogPath, backup);
        }
    }

    [Fact]
    public void FormatErrorDetails_WithException_IncludesTypeAndMessage()
    {
        var ex = new InvalidOperationException("test msg");
        string result = App.FormatErrorDetails(ex, "user message");
        Assert.Contains("InvalidOperationException", result);
        Assert.Contains("user message", result);
        Assert.Contains("Stack Trace", result);
    }

    [Fact]
    public void FormatErrorDetails_WithNullException_ShowsNullType()
    {
        string result = App.FormatErrorDetails(null, "msg");
        Assert.Contains("Exception:", result);
        Assert.DoesNotContain("NullReferenceException", result);
    }
}
