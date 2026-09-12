namespace WingetStore.Tests;

public class NotificationsSettingsTests
{
    [Fact]
    public void EnableNotifications_TogglesSettingCorrectly()
    {
        var original = SettingsService.EnableNotifications;
        try
        {
            SettingsService.EnableNotifications = false;
            Assert.False(SettingsService.EnableNotifications);
            SettingsService.EnableNotifications = true;
            Assert.True(SettingsService.EnableNotifications);
        }
        finally
        {
            SettingsService.EnableNotifications = original;
        }
    }
}
