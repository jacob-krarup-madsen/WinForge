using System.Diagnostics;
using System.Text.Json;

namespace WingetStore.Services;

public class AppSettings { public bool AutoUpdate { get; set; } = false; public string AppTheme { get; set; } = "Default"; public bool EnableNotifications { get; set; } = true; }

public class SettingsService : ISettingsService
{
    private static string SettingsFilePath = AppPaths.SettingsFile;
    private static AppSettings _settings = new() { AutoUpdate = false };
    static SettingsService() => LoadSettings();
    bool ISettingsService.AutoUpdate { get => AutoUpdate; set => AutoUpdate = value; }
    string ISettingsService.AppTheme { get => AppTheme; set => AppTheme = value; }
    bool ISettingsService.EnableNotifications { get => EnableNotifications; set => EnableNotifications = value; }
    public static bool AutoUpdate { get => _settings.AutoUpdate; set { if (_settings.AutoUpdate != value) { _settings.AutoUpdate = value; SaveSettings(); } } }
    public static string AppTheme { get => _settings.AppTheme; set { if (_settings.AppTheme != value) { _settings.AppTheme = value; SaveSettings(); } } }
    public static bool EnableNotifications { get => _settings.EnableNotifications; set { if (_settings.EnableNotifications != value) { _settings.EnableNotifications = value; SaveSettings(); } } }
    internal static AppSettings DeserializeSettings(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return new AppSettings { AutoUpdate = false };
        try
        {
            var loaded = JsonSerializer.Deserialize<AppSettings>(json);
            return loaded ?? new AppSettings { AutoUpdate = false };
        }
        catch (Exception ex)
        {
            LogService.LogError("Failed to deserialize settings JSON, settings reset due to corruption.", ex);
            try
            {
                var notificationService = (INotificationService?)App.Services?.GetService(typeof(INotificationService)) ?? new NotificationService();
                notificationService.ShowError("Settings Reset", "Settings were reset due to corruption.");
            }
            catch (Exception notifEx)
            {
                LogService.LogError("Failed to show corruption notification", notifEx);
            }
            return new AppSettings { AutoUpdate = false };
        }
    }

    internal static string SerializeSettings(AppSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        return JsonSerializer.Serialize(settings);
    }

    private static void LoadSettings()
    {
        try
        {
            if (File.Exists(SettingsFilePath))
            {
                _settings = DeserializeSettings(File.ReadAllText(SettingsFilePath));
            }
        }
        catch (Exception ex) { Debug.WriteLine($"Failed to load settings: {ex.Message}"); }
    }
    private static void SaveSettings()
    {
        try
        {
            string? dir = Path.GetDirectoryName(SettingsFilePath);
            if (dir != null) Directory.CreateDirectory(dir);
            File.WriteAllText(SettingsFilePath, SerializeSettings(_settings));
        }
        catch (Exception ex) { Debug.WriteLine($"Failed to save settings: {ex.Message}"); }
    }
}
