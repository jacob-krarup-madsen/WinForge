using ViVeToolApp.Services;
using WingetStore.Services.Optimizer;

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

        services.AddSingleton<IElevationService, WindowsElevationService>();
        services.AddSingleton<IRegistryAccessor, WindowsRegistryAccessor>();
        services.AddSingleton<ICommandExecutor, ProcessCommandExecutor>();
        services.AddSingleton<IWmiAccessor, WindowsWmiAccessor>();
        services.AddSingleton<IMemoryNative, WindowsMemoryNative>();
        services.AddSingleton<IMemoryOptimizerService, MemoryOptimizerService>();
        services.AddSingleton<IDiskCleanerService, DiskCleanerService>();
        services.AddSingleton<IOptimizerConfigService, OptimizerConfigService>();
        services.AddSingleton<ISystemOptimizerService, SystemOptimizerService>();
        services.AddSingleton<ISystemAuditorService, SystemAuditorService>();
        services.AddSingleton<ISystemBenchmarkService, SystemBenchmarkService>();

        services.AddSingleton<IProcessLauncher, SystemProcessLauncher>();
        services.AddSingleton<IFeatureFilterService, FeatureFilterService>();
        services.AddSingleton<IViVeToolLocator, ViVeToolLocator>();
        services.AddSingleton<IViVeToolRunner, ViVeToolRunner>();
        services.AddSingleton<IViVeToolDownloader>(_ => new ViVeToolDownloader());
        services.AddSingleton<IFeatureScraper>(_ => new PureinfotechScraper());

        services.AddTransient<InstalledViewModel>();
        services.AddTransient<UpdatesViewModel>();
        services.AddTransient<SearchViewModel>();
        services.AddTransient<HomeViewModel>();
        services.AddTransient<OptimizerViewModel>();
        services.AddTransient<FeaturesViewModel>();
        App.Services = services.BuildServiceProvider();
    }
}
