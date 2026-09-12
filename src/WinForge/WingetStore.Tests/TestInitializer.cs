namespace WingetStore.Tests;

public static class TestInitializer
{
    [ModuleInitializer]
    public static void Initialize()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IProcessRunner, MockProcessRunner>();
        services.AddSingleton<WingetService>();
        services.AddSingleton<IWingetService>(sp => new CachingWingetService(sp.GetRequiredService<WingetService>()));
        services.AddSingleton<ISettingsService, SettingsService>();
        services.AddSingleton<INotificationService, NotificationService>();
        services.AddSingleton<IconService>(IconService.Instance);
        services.AddTransient<InstalledViewModel>();
        services.AddTransient<UpdatesViewModel>();
        services.AddTransient<SearchViewModel>();
        services.AddTransient<HomeViewModel>();
        App.Services = services.BuildServiceProvider();
    }
}
