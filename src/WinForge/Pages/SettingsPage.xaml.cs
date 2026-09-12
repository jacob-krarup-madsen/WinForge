using System;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using WingetStore.Services;

namespace WingetStore.Pages;

public sealed partial class SettingsPage : Page
{
    private bool _isInitializing = true;
    private DateTime _lastCheckedTime;

    public SettingsPage()
    {
        InitializeComponent();
        Loaded += SettingsPage_Loaded;
    }

    private void SettingsPage_Loaded(object sender, RoutedEventArgs e)
    {
        _isInitializing = true;
        AutoUpdateToggle.IsOn = SettingsService.AutoUpdate;
        NotificationsToggle.IsOn = SettingsService.EnableNotifications;
        _isInitializing = false;

        _lastCheckedTime = DateTime.Now;
        UpdateDiagnostics();
    }

    private void AutoUpdateToggle_Toggled(object sender, RoutedEventArgs e)
    {
        if (_isInitializing) return;
        SettingsService.AutoUpdate = AutoUpdateToggle.IsOn;
    }

    private void NotificationsToggle_Toggled(object sender, RoutedEventArgs e)
    {
        if (_isInitializing) return;
        SettingsService.EnableNotifications = NotificationsToggle.IsOn;
    }

    private void TestStatusButton_Click(object sender, RoutedEventArgs e)
    {
        _lastCheckedTime = DateTime.Now;
        UpdateDiagnostics();
    }

    public static (string StatusText, bool IsAvailable, string Glyph, string FormattedLastChecked) GetDiagnosticsData(bool isWingetAvailable, DateTime lastCheckedTime)
    {
        string statusText = isWingetAvailable ? "Connected to Windows Package Manager" : "Winget not found on this system";
        string glyph = isWingetAvailable ? "\uE73E" : "\uEA39";
        string formatted = lastCheckedTime.Date == DateTime.Today ? $"Checked today at {lastCheckedTime:t}" : $"Checked {lastCheckedTime:d} at {lastCheckedTime:t}";
        return (statusText, isWingetAvailable, glyph, formatted);
    }

    public static string GetStatusBrushResourceKey(bool isWingetAvailable) =>
        isWingetAvailable ? "SystemFillColorSuccessBrush" : "SystemFillColorCriticalBrush";

    private void UpdateDiagnostics()
    {
        bool available = WingetService.IsWingetAvailable();
        var (statusText, _, glyph, formatted) = GetDiagnosticsData(available, _lastCheckedTime);
        DiagnosticsStatusText.Text = statusText;

        string resourceKey = GetStatusBrushResourceKey(available);
        var brush = Application.Current.Resources[resourceKey] as Brush ?? (available ? new SolidColorBrush(Windows.UI.Color.FromArgb(255, 16, 124, 65)) : new SolidColorBrush(Windows.UI.Color.FromArgb(255, 232, 17, 35)));
        DiagnosticsStatusText.Foreground = brush;

        if (DiagnosticsStatusIcon != null)
        {
            DiagnosticsStatusIcon.Glyph = glyph;
            DiagnosticsStatusIcon.Foreground = brush;
            DiagnosticsStatusIcon.Visibility = Visibility.Visible;
        }

        LastCheckedText.Text = formatted;
        LastCheckedText.Visibility = Visibility.Visible;
    }
}
