namespace WingetStore.Tests;

public class SettingsServiceStaticTests
{
    [Fact]
    public void DeserializeSettings_ValidJson_ReturnsPopulatedSettings()
    {
        string json = "{\"AutoUpdate\":true,\"AppTheme\":\"Dark\",\"EnableNotifications\":false}";
        var settings = SettingsService.DeserializeSettings(json);
        Assert.True(settings.AutoUpdate);
        Assert.Equal("Dark", settings.AppTheme);
        Assert.False(settings.EnableNotifications);
    }

    [Fact]
    public void DeserializeSettings_NullOrEmptyJson_ReturnsDefaultSettings()
    {
        var settings1 = SettingsService.DeserializeSettings("");
        Assert.False(settings1.AutoUpdate);
        var settings2 = SettingsService.DeserializeSettings(null);
        Assert.False(settings2.AutoUpdate);
    }

    [Fact]
    public void DeserializeSettings_CorruptJson_ReturnsDefaultSettings()
    {
        var settings = SettingsService.DeserializeSettings("{ invalid json");
        Assert.False(settings.AutoUpdate);
    }

    [Fact]
    public void DeserializeSettings_CorruptJson_LogsErrorAndNotifiesUser()
    {
        var mockNotif = new TestNotificationService();
        var services = new ServiceCollection();
        services.AddSingleton<INotificationService>(mockNotif);
        var originalServices = App.Services;
        App.Services = services.BuildServiceProvider();

        try
        {
            var settings = SettingsService.DeserializeSettings("{ invalid json");
            Assert.False(settings.AutoUpdate);
            Assert.True(mockNotif.ShowErrorCalled);
            Assert.Equal("Settings Reset", mockNotif.LastErrorTitle);
            Assert.Contains("corruption", mockNotif.LastErrorMessage);
        }
        finally
        {
            App.Services = originalServices;
        }
    }

    private class TestNotificationService : INotificationService
    {
        public bool ShowErrorCalled { get; private set; }
        public string? LastErrorTitle { get; private set; }
        public string? LastErrorMessage { get; private set; }
        public void ShowError(string title, string message)
        {
            ShowErrorCalled = true;
            LastErrorTitle = title;
            LastErrorMessage = message;
        }
        public void ShowInfo(string title, string message) { }
    }

    [Fact]
    public void DeserializeSettings_NotificationThrows_ReturnsDefaultSettingsWithoutThrowing()
    {
        var mockNotif = new ThrowingNotificationService();
        var services = new ServiceCollection();
        services.AddSingleton<INotificationService>(mockNotif);
        var originalServices = App.Services;
        App.Services = services.BuildServiceProvider();

        try
        {
            var settings = SettingsService.DeserializeSettings("{ invalid json");
            Assert.False(settings.AutoUpdate);
        }
        finally
        {
            App.Services = originalServices;
        }
    }

    private class ThrowingNotificationService : INotificationService
    {
        public void ShowError(string title, string message) => throw new InvalidOperationException("Notification failed");
        public void ShowInfo(string title, string message) { }
    }

    [Fact]
    public void SerializeSettings_ValidInstance_ProducesJson()
    {
        var appSettings = new AppSettings { AutoUpdate = true, AppTheme = "Light", EnableNotifications = true };
        string json = SettingsService.SerializeSettings(appSettings);
        Assert.Contains("\"AutoUpdate\":true", json);
        Assert.Contains("\"AppTheme\":\"Light\"", json);
    }

    [Fact]
    public void SerializeSettings_NullInstance_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => SettingsService.SerializeSettings(null!));
    }
}
