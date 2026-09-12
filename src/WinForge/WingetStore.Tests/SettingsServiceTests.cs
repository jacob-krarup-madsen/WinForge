namespace WingetStore.Tests;

public class SettingsServiceTests
{
    [Fact]
    public void AppTheme_SaveAndLoad()
    {
        var original = SettingsService.AppTheme;
        try
        {
            SettingsService.AppTheme = "Dark";
            Assert.Equal("Dark", SettingsService.AppTheme);

            SettingsService.AppTheme = "Light";
            Assert.Equal("Light", SettingsService.AppTheme);
        }
        finally
        {
            SettingsService.AppTheme = original;
        }
    }

    [Fact]
    public void AutoUpdate_SaveAndLoad()

    {
        var original = SettingsService.AutoUpdate;
        try
        {
            SettingsService.AutoUpdate = true;
            Assert.True(SettingsService.AutoUpdate);

            SettingsService.AutoUpdate = false;
            Assert.False(SettingsService.AutoUpdate);
        }
        finally
        {
            SettingsService.AutoUpdate = original;
        }
    }

    [Fact]
    public void SettingsService_InterfaceImplementation()
    {
        ISettingsService service = new SettingsService();
        var originalTheme = service.AppTheme;
        var originalUpdate = service.AutoUpdate;

        try
        {
            service.AppTheme = "Dark";
            Assert.Equal("Dark", service.AppTheme);

            service.AutoUpdate = true;
            Assert.True(service.AutoUpdate);
        }
        finally
        {
            service.AppTheme = originalTheme;
            service.AutoUpdate = originalUpdate;
        }
    }

    [Fact]
    public void SettingsService_CorruptFileLoadException()
    {
        string path = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "WingetStore",
            "settings.json"
        );

        string? originalJson = null;
        if (File.Exists(path))
        {
            originalJson = File.ReadAllText(path);
        }

        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, "{ corrupt json }");

            var method = typeof(SettingsService).GetMethod("LoadSettings", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)!;
            method.Invoke(null, null);
        }
        finally
        {
            if (originalJson != null)
            {
                File.WriteAllText(path, originalJson);
            }
            else if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }

    [Fact]
    public void SettingsService_EdgeCases_Coverage()
    {
        var field = typeof(SettingsService).GetField("SettingsFilePath", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic)!;
        var originalPath = field.GetValue(null);

        string path = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "WingetStore",
            "settings.json"
        );

        try
        {
            // 1. Test loaded == null in LoadSettings (file contains "null")
            File.WriteAllText(path, "null");
            var loadMethod = typeof(SettingsService).GetMethod("LoadSettings", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)!;
            loadMethod.Invoke(null, null);

            // 2. Test dir == null in SaveSettings (no crash)
            field.SetValue(null, "C:\\");
            var saveMethod = typeof(SettingsService).GetMethod("SaveSettings", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)!;
            saveMethod.Invoke(null, null);

            // 3. Test successful settings load (loaded != null)
            field.SetValue(null, path);
            var validSettings = new AppSettings { AutoUpdate = true, AppTheme = "Light" };
            File.WriteAllText(path, System.Text.Json.JsonSerializer.Serialize(validSettings));
            loadMethod.Invoke(null, null);
            Assert.True(SettingsService.AutoUpdate);
            Assert.Equal("Light", SettingsService.AppTheme);
        }
        finally
        {
            field.SetValue(null, originalPath);
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }
}
