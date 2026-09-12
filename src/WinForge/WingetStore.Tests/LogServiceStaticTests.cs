namespace WingetStore.Tests;

public class LogServiceStaticTests
{
    [Fact]
    public void FormatLogEntry_ValidInputs_FormatsCorrectly()
    {
        var timestamp = new DateTime(2026, 7, 23, 18, 0, 0);
        string formatted = LogService.FormatLogEntry("INFO", "Application started", timestamp);
        Assert.Equal("[2026-07-23 18:00:00] [INFO] Application started", formatted);
    }

    [Fact]
    public void FormatLogEntry_SpecialCharacters_PreservesMessage()
    {
        var timestamp = new DateTime(2026, 7, 23, 12, 30, 45);
        string formatted = LogService.FormatLogEntry("ERROR", "Failed to connect: 500 & path='C:\\test'", timestamp);
        Assert.Equal("[2026-07-23 12:30:45] [ERROR] Failed to connect: 500 & path='C:\\test'", formatted);
    }
}
