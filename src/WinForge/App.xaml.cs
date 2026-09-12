using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using WingetStore.Models;
using WingetStore.Services;
using WingetStore.Testing;
using WingetStore.ViewModels;

namespace WingetStore;

public partial class App : Application
{
    public static Window? MainWindow { get; private set; }
    private static IServiceProvider? _services;
    public static IServiceProvider Services { get => _services ??= ConfigureServices(); internal set => _services = value; }
    public static IWingetService Winget => Services.GetRequiredService<IWingetService>();
    public static ISettingsService Settings => Services.GetRequiredService<ISettingsService>();
    public static Microsoft.UI.Dispatching.DispatcherQueue? DispatcherQueue { get; private set; }
    public static Action<Action>? DispatcherOverride { get; set; }

    public static void Dispatch(Action action)
    {
        if (DispatcherOverride != null) DispatcherOverride(action);
        else if (DispatcherQueue != null) DispatcherQueue.TryEnqueue(() => action());
        else action();
    }

    public static Visibility VisibleIf(bool value) => value ? Visibility.Visible : Visibility.Collapsed;
    public static bool Not(bool value) => !value;
    public static Visibility CollapsedIf(bool value) => value ? Visibility.Collapsed : Visibility.Visible;

    public static ImageSource? ToImageSource(string path)
    {
        if (string.IsNullOrEmpty(path)) return null;
        try { return new Microsoft.UI.Xaml.Media.Imaging.BitmapImage(new Uri(path)); } catch (Exception ex) { LogService.LogError($"ToImageSource failed for path: {path}", ex); return null; }
    }

    public static ElementTheme ParseTheme(string theme) => theme switch { "Light" => ElementTheme.Light, "Dark" => ElementTheme.Dark, _ => ElementTheme.Default };

    public static string GetCrashLogDirectory() => AppPaths.Root;
    public static string GetCrashLogPath() => AppPaths.CrashLogFile;
    public static string GetCrashLogContent(string errorDetails) => GetCrashLogContent(errorDetails, DateTime.Now);
    public static string GetCrashLogContent(string errorDetails, DateTime timestamp) => $"[CRASH LOG - {timestamp.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture)}]\n{errorDetails}\n\n";
    public static string FormatErrorDetails(Exception? ex, string message) => $"Exception: {ex?.GetType().Name}\nMessage: {message}\n\nStack Trace:\n{ex?.StackTrace}";

    public static bool TryWriteCrashLog(string errorDetails)
    {
        try
        {
            Directory.CreateDirectory(GetCrashLogDirectory());
            File.WriteAllText(GetCrashLogPath(), GetCrashLogContent(errorDetails));
            return true;
        }
        catch (Exception exInner)
        {
            Debug.WriteLine($"Failed to write crash log: {exInner.Message}");
            return false;
        }
    }

    public static bool IsUITestMode() => Environment.GetCommandLineArgs().Contains("--run-ui-tests", StringComparer.OrdinalIgnoreCase);

    public App()
    {
        try { Windows.Globalization.ApplicationLanguages.PrimaryLanguageOverride = "en-US"; } catch { }
        Services = ConfigureServices();
        InitializeComponent();
        UnhandledException += App_UnhandledException;
        TaskScheduler.UnobservedTaskException += (s, e) => { LogService.LogError("UnobservedTaskException", e.Exception); e.SetObserved(); };
        AppDomain.CurrentDomain.UnhandledException += (s, e) => LogService.LogError($"UnhandledDomainException: {e.ExceptionObject}");
    }

    private static Microsoft.Extensions.DependencyInjection.ServiceProvider ConfigureServices()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IProcessRunner, CliProcessRunner>();
        services.AddSingleton<WingetService>();
        services.AddSingleton<IWingetService>(sp => new CachingWingetService(sp.GetRequiredService<WingetService>()));
        services.AddSingleton<ISettingsService, SettingsService>();
        services.AddSingleton<INotificationService, NotificationService>();
        services.AddSingleton(IconService.Instance);
        services.AddTransient<InstalledViewModel>();
        services.AddTransient<UpdatesViewModel>();
        services.AddTransient<SearchViewModel>();
        services.AddTransient<HomeViewModel>();
        return services.BuildServiceProvider();
    }

    [LibraryImport("user32.dll", StringMarshalling = StringMarshalling.Utf16)]
    [System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage]
    private static partial int MessageBox(IntPtr hWnd, string text, string caption, uint type);

    private void App_UnhandledException(object sender, Microsoft.UI.Xaml.UnhandledExceptionEventArgs e)
    {
        e.Handled = true;
        TryWriteCrashLog(FormatErrorDetails(e.Exception, e.Message));

        try { new ErrorWindow(e.Message, e.Exception?.ToString() ?? "No stack trace available.").Activate(); }
        catch { _ = MessageBox(IntPtr.Zero, $"An unexpected application error occurred:\n\n{e.Message}\n\nStack Trace:\n{e.Exception}", "WinForge - Application Error", 0x10); }
    }

    public static string FormatLogDialogTitle(string packageName, object operation) => $"Activity Log: {packageName} ({operation})";
    public static string FormatActivityLogStatus(string statusText, double progress) => $"Status: {statusText} | Progress: {(int)progress}%";

    public static async Task ShowLogDialogForPackage(WingetPackage package, XamlRoot xamlRoot)
    {
        if (package == null) return;
        var task = Winget.ActiveTasks.LastOrDefault(t => t.PackageId.Equals(package.Id, StringComparison.OrdinalIgnoreCase));
        if (task == null) return;

        var dialog = new ContentDialog
        {
            Title = FormatLogDialogTitle(task.PackageName, task.Operation),
            CloseButtonText = "Close",
            SecondaryButtonText = task.CanCancel ? "Cancel Operation" : "",
            XamlRoot = xamlRoot
        };
        dialog.SecondaryButtonClick += (s, ev) => Winget.CancelTask(task.Id);
        var grid = new Grid { Width = 600, Height = 400, RowDefinitions = { new RowDefinition { Height = GridLength.Auto }, new RowDefinition { Height = new GridLength(1, GridUnitType.Star) } } };

        var statusText = new TextBlock { Text = FormatActivityLogStatus(task.StatusText, task.Progress), Margin = new Thickness(0, 0, 0, 8), FontWeight = Microsoft.UI.Text.FontWeights.SemiBold };
        Grid.SetRow(statusText, 0); grid.Children.Add(statusText);

        var scroll = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Auto, Background = (Brush)Current.Resources["CardBackgroundFillColorDefaultBrush"], BorderBrush = (Brush)Current.Resources["CardStrokeColorDefaultBrush"], BorderThickness = new Thickness(1), Padding = new Thickness(12), CornerRadius = new CornerRadius(4) };
        Grid.SetRow(scroll, 1);

        var tb = new TextBlock { Text = task.LogOutput, TextWrapping = TextWrapping.NoWrap, FontFamily = new FontFamily("Consolas"), FontSize = 11 };
        scroll.Content = tb; grid.Children.Add(scroll);
        dialog.Content = grid;

        void OnTaskPropertyChanged(object? s, System.ComponentModel.PropertyChangedEventArgs ev)
        {
            if (ev.PropertyName == nameof(InstallTask.LogOutput) || ev.PropertyName == nameof(InstallTask.StatusText) || ev.PropertyName == nameof(InstallTask.Progress))
            {
                DispatcherQueue?.TryEnqueue(() => { tb.Text = task.LogOutput; statusText.Text = FormatActivityLogStatus(task.StatusText, task.Progress); scroll.ChangeView(null, scroll.ScrollableHeight, null); });
            }
        }

        task.PropertyChanged += OnTaskPropertyChanged;
        dialog.Closed += (s, ev) => task.PropertyChanged -= OnTaskPropertyChanged;
        await dialog.ShowAsync();
    }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        DispatcherQueue = Microsoft.UI.Dispatching.DispatcherQueue.GetForCurrentThread();
        _ = Services.GetRequiredService<IconService>().InitializeAsync();
        var mainWindow = new MainWindow();
        MainWindow = mainWindow;
        if (MainWindow.Content is FrameworkElement root) root.RequestedTheme = ParseTheme(Settings.AppTheme);
        MainWindow.Activate();

        if (IsUITestMode())
        {
            App.Dispatch(async () =>
            {
                try
                {
                    await Task.Delay(500);
                    if (mainWindow.Content is FrameworkElement fe && fe.FindName("NavFrame") is Frame navFrame)
                    {
                        await UITestRunner.RunNonHeadlessUITestsAsync(navFrame);
                    }
                }
                finally
                {
                    try { Application.Current.Exit(); } catch { }
                    Environment.Exit(0);
                }
            });
        }
    }
}

public class ErrorWindow : Window
{
    public ErrorWindow(string errorMessage, string stackTrace)
    {
        Title = "WinForge - Application Error";
        var grid = new Grid { Width = 550, Height = 350, Padding = new Thickness(24), RowDefinitions = { new RowDefinition { Height = GridLength.Auto }, new RowDefinition { Height = new GridLength(1, GridUnitType.Star) }, new RowDefinition { Height = GridLength.Auto } } };
        var titleBlock = new TextBlock { Text = "An unexpected error occurred", FontSize = 18, FontWeight = Microsoft.UI.Text.FontWeights.Bold, Margin = new Thickness(0, 0, 0, 12), Foreground = new SolidColorBrush(Microsoft.UI.Colors.Red) };
        Grid.SetRow(titleBlock, 0); grid.Children.Add(titleBlock);
        var errorScroll = new ScrollViewer { Content = new TextBlock { Text = $"{errorMessage}\n\nDetails:\n{stackTrace}", TextWrapping = TextWrapping.Wrap, FontFamily = new FontFamily("Consolas"), FontSize = 12 }, Margin = new Thickness(0, 0, 0, 16) };
        Grid.SetRow(errorScroll, 1); grid.Children.Add(errorScroll);
        var closeButton = new Button { Content = "Close", HorizontalAlignment = HorizontalAlignment.Right, Width = 80 };
        closeButton.Click += (s, e) => Close();
        Grid.SetRow(closeButton, 2); grid.Children.Add(closeButton);
        Content = grid;
    }
}
