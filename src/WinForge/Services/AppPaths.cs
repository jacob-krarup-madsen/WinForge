namespace WingetStore.Services;

public static class AppPaths
{
    private static string LocalAppData => Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
    public static string Root => Path.Combine(LocalAppData, "WingetStore");
    public static string LogsDir => Path.Combine(Root, "logs");
    public static string AppLogFile => Path.Combine(LogsDir, "app.log");
    public static string SettingsFile => Path.Combine(Root, "settings.json");
    public static string IconsCacheDir => Path.Combine(Root, "icons");
    public static string ScreenshotDbFile => Path.Combine(Root, "screenshot-database-v2.json");
    public static string CrashLogFile => Path.Combine(Root, "crash.log");
}
