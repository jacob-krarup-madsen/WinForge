using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace WingetStore.Pages;

public class UiFeatureItem : INotifyPropertyChanged
{
    private string _statusText = "Default";

    public long Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;

    public string StatusText
    {
        get => _statusText;
        set
        {
            if (_statusText != value)
            {
                _statusText = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(StatusText)));
            }
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
}

public sealed partial class FeaturesPage : Page
{
    private readonly List<UiFeatureItem> _allFeatures = new();
    private readonly ObservableCollection<UiFeatureItem> _filteredFeatures = new();

    public FeaturesPage()
    {
        InitializeComponent();
        FeaturesListView.ItemsSource = _filteredFeatures;
        Loaded += FeaturesPage_Loaded;
    }

    private void FeaturesPage_Loaded(object sender, RoutedEventArgs e)
    {
        LoadFeatures();
    }

    private void LoadFeatures()
    {
        _allFeatures.Clear();

        // Built-in verified Windows 11 feature velocity catalog
        _allFeatures.AddRange(new[]
        {
            new UiFeatureItem { Id = 44470355, Name = "Copilot & AI Assistant Integration", Description = "Enables full taskbar and shell Copilot integrations." },
            new UiFeatureItem { Id = 48433719, Name = "Snap Layouts Suggestions", Description = "Displays smart window snap suggestions when hovering maximize." },
            new UiFeatureItem { Id = 48433706, Name = "Energy Saver Enhancements", Description = "Advanced battery life and low-power scheduler policies." },
            new UiFeatureItem { Id = 47557358, Name = "Settings Modern Home Card", Description = "Refreshed dynamic settings recommendations." },
            new UiFeatureItem { Id = 45952862, Name = "Windows Spotlight on Desktop", Description = "Dynamic Bing wallpaper and interactive desktop hotspots." },
            new UiFeatureItem { Id = 48433720, Name = "Voice Clarity AI", Description = "Low-latency background noise suppression for communication apps." },
            new UiFeatureItem { Id = 46603313, Name = "Widgets Board Modern Layout", Description = "Multi-column widget dashboard with news grouping." },
            new UiFeatureItem { Id = 47622124, Name = "File Explorer Tabs & Redesign", Description = "Refreshed address bar and tabbed navigation." }
        });

        ApplyFilter();
    }

    private void ApplyFilter()
    {
        var query = SearchBox.Text?.Trim() ?? string.Empty;
        _filteredFeatures.Clear();

        var matches = string.IsNullOrEmpty(query)
            ? _allFeatures
            : _allFeatures.Where(f =>
                f.Id.ToString().Contains(query, StringComparison.OrdinalIgnoreCase) ||
                f.Name.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                f.Description.Contains(query, StringComparison.OrdinalIgnoreCase));

        foreach (var item in matches)
        {
            _filteredFeatures.Add(item);
        }
    }

    private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        ApplyFilter();
    }

    private void FeaturesListView_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        var hasSelection = FeaturesListView.SelectedItems.Count > 0;
        EnableBtn.IsEnabled = hasSelection;
        DisableBtn.IsEnabled = hasSelection;
    }

    private async void EnableBtn_Click(object sender, RoutedEventArgs e)
    {
        await ExecuteViVeToolActionAsync(true);
    }

    private async void DisableBtn_Click(object sender, RoutedEventArgs e)
    {
        await ExecuteViVeToolActionAsync(false);
    }

    private async Task ExecuteViVeToolActionAsync(bool enable)
    {
        var selected = FeaturesListView.SelectedItems.Cast<UiFeatureItem>().ToList();
        if (selected.Count == 0) return;

        var vivePath = LocateViVeTool();
        if (string.IsNullOrEmpty(vivePath) || !File.Exists(vivePath))
        {
            StatusInfoBar.Severity = InfoBarSeverity.Error;
            StatusInfoBar.Title = "ViVeTool Not Found";
            StatusInfoBar.Message = "ViVeTool.exe could not be found in tools/cli or PATH. Please ensure ViVeTool is installed.";
            StatusInfoBar.IsOpen = true;
            return;
        }

        var action = enable ? "enabled" : "disabled";
        var count = selected.Count;
        int successCount = 0;
        int failCount = 0;

        EnableBtn.IsEnabled = false;
        DisableBtn.IsEnabled = false;
        RefreshBtn.IsEnabled = false;

        try
        {
            foreach (var f in selected)
            {
                var verb = enable ? "/enable" : "/disable";
                using var proc = Process.Start(new ProcessStartInfo
                {
                    FileName = vivePath,
                    Arguments = $"{verb} /id:{f.Id}",
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                });

                if (proc != null)
                {
                    await proc.WaitForExitAsync();
                    if (proc.ExitCode == 0)
                    {
                        f.StatusText = enable ? "Enabled" : "Disabled";
                        successCount++;
                    }
                    else
                    {
                        failCount++;
                    }
                }
                else
                {
                    failCount++;
                }
            }

            if (failCount == 0)
            {
                StatusInfoBar.Severity = InfoBarSeverity.Success;
                StatusInfoBar.Title = "Velocity Features Updated";
                StatusInfoBar.Message = $"Successfully {action} {successCount} feature(s). A system restart may be required for some features.";
            }
            else if (successCount > 0)
            {
                StatusInfoBar.Severity = InfoBarSeverity.Warning;
                StatusInfoBar.Title = "Velocity Features Partially Updated";
                StatusInfoBar.Message = $"{action} {successCount} feature(s), but {failCount} failed. Running as Administrator may be required.";
            }
            else
            {
                StatusInfoBar.Severity = InfoBarSeverity.Error;
                StatusInfoBar.Title = "Operation Failed";
                StatusInfoBar.Message = $"Failed to execute ViVeTool on {failCount} feature(s). Ensure the application is running as Administrator.";
            }
            StatusInfoBar.IsOpen = true;
        }
        catch (Exception ex)
        {
            StatusInfoBar.Severity = InfoBarSeverity.Error;
            StatusInfoBar.Title = "Execution Error";
            StatusInfoBar.Message = ex.Message;
            StatusInfoBar.IsOpen = true;
        }
        finally
        {
            var hasSelection = FeaturesListView.SelectedItems.Count > 0;
            EnableBtn.IsEnabled = hasSelection;
            DisableBtn.IsEnabled = hasSelection;
            RefreshBtn.IsEnabled = true;
        }
    }

    private static string? LocateViVeTool()
    {
        var baseDir = AppContext.BaseDirectory;

        // 1. Direct candidate next to binary
        var direct = Path.Combine(baseDir, "ViVeTool.exe");
        if (File.Exists(direct)) return direct;
        var directLower = Path.Combine(baseDir, "vivetool.exe");
        if (File.Exists(directLower)) return directLower;

        // 2. Probe parent directories up to repo root
        var dir = baseDir;
        for (int i = 0; i < 7 && !string.IsNullOrEmpty(dir); i++)
        {
            var candidate = Path.Combine(dir, "tools", "cli", "ViVeTool.exe");
            if (File.Exists(candidate)) return candidate;
            var candidateLower = Path.Combine(dir, "tools", "cli", "vivetool.exe");
            if (File.Exists(candidateLower)) return candidateLower;

            var parent = Directory.GetParent(dir);
            dir = parent?.FullName;
        }

        // 3. Fallback to ViVeToolLocator (PATH and standard paths)
        var locator = new ViVeToolApp.Services.ViVeToolLocator();
        return locator.LocateViVeTool();
    }

    private void RefreshBtn_Click(object sender, RoutedEventArgs e)
    {
        LoadFeatures();
        StatusInfoBar.Severity = InfoBarSeverity.Informational;
        StatusInfoBar.Title = "Catalog Refreshed";
        StatusInfoBar.Message = $"{_allFeatures.Count} feature definitions ready.";
        StatusInfoBar.IsOpen = true;
    }
}
