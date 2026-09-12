namespace WingetStore.Tests;

public class LogAndNotificationTests
{
    [Fact]
    public void LogService_LogsCorrectly()
    {
        LogService.LogInfo("Test info log message");
        LogService.LogError("Test error log message");
        LogService.LogError("Test error log message with exception", new Exception("Simulated exception"));

        var logDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "WingetStore", "logs");
        var logFile = Path.Combine(logDir, "app.log");
        Assert.True(File.Exists(logFile));
        var content = File.ReadAllText(logFile);
        Assert.Contains("Test info log message", content);
        Assert.Contains("Test error log message", content);
        Assert.Contains("Simulated exception", content);
    }

    [Fact]
    public void NotificationService_ShowErrorAndInfo_NullWindow_Coverage()
    {
        var oldMainWindow = App.MainWindow;
        var prop = typeof(App).GetProperty("MainWindow", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static)!;
        prop.SetValue(null, null);
        App.DispatcherOverride = action => action();
        try
        {
            var notification = new NotificationService();
            notification.ShowError("Error Title", "Error Message");
            notification.ShowInfo("Info Title", "Info Message");
        }
        finally
        {
            prop.SetValue(null, oldMainWindow);
            App.DispatcherOverride = null;
        }
    }
}
