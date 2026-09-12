using System;
using System.Diagnostics;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Navigation;
using WingetStore.Models;
using WingetStore.Services;

namespace WingetStore.Pages;

public sealed partial class DetailsPage : Page
{
    private string _packageId = "";
    private WingetPackage? _package;
    private bool _isNavigatedAway;

    public DetailsPage()
    {
        InitializeComponent();
        Unloaded += DetailsPage_Unloaded;
    }

    private void DetailsPage_Unloaded(object sender, RoutedEventArgs e)
    {
        _package?.PropertyChanged -= Package_PropertyChanged;
        App.Winget.ActiveTasks.CollectionChanged -= ActiveTasks_CollectionChanged;
    }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        _isNavigatedAway = false;
        if (e.Parameter is string packageId && !string.IsNullOrEmpty(packageId))
        {
            _packageId = packageId;
            _ = LoadDetailsAsync();
        }
        App.Winget.ActiveTasks.CollectionChanged -= ActiveTasks_CollectionChanged;
        App.Winget.ActiveTasks.CollectionChanged += ActiveTasks_CollectionChanged;
    }

    protected override void OnNavigatedFrom(NavigationEventArgs e)
    {
        base.OnNavigatedFrom(e);
        _isNavigatedAway = true;
        _package?.PropertyChanged -= Package_PropertyChanged;
        App.Winget.ActiveTasks.CollectionChanged -= ActiveTasks_CollectionChanged;
    }

    public static string FormatPublisher(string? publisher) =>
        string.IsNullOrWhiteSpace(publisher) ? "Unknown Publisher" : publisher;

    public static string FormatVersionText(string? version, string? availableVersion)
    {
        string baseVersion = string.IsNullOrEmpty(version) ? "Unknown" : version;
        string latestSuffix = string.IsNullOrEmpty(availableVersion) ? "" : $" (Latest: {availableVersion})";
        return $"Version: {baseVersion}{latestSuffix}";
    }

    public static string FormatDescription(string? description) =>
        string.IsNullOrWhiteSpace(description) ? "No description available for this package." : description;

    public static Visibility GetTextSectionVisibility(string? text) =>
        !string.IsNullOrEmpty(text) ? Visibility.Visible : Visibility.Collapsed;

    public static Visibility GetCollectionVisibility<T>(IReadOnlyCollection<T>? collection) =>
        collection != null && collection.Count > 0 ? Visibility.Visible : Visibility.Collapsed;

    public static string GetTagNavigationParameter(string tag) => $"tag:{tag}";

    public static InstallTask? FindActiveTaskForPackage(string? packageId, System.Collections.Generic.IEnumerable<InstallTask>? activeTasks)
    {
        if (string.IsNullOrEmpty(packageId) || activeTasks == null) return null;
        return System.Linq.Enumerable.FirstOrDefault(activeTasks, task =>
            task.PackageId.Equals(packageId, StringComparison.OrdinalIgnoreCase) &&
            (task.Status == InstallTaskStatus.Running || task.Status == InstallTaskStatus.Queued));
    }

    private async Task LoadDetailsAsync()
    {
        DetailsProgress.IsActive = true;
        DetailsContentPanel.Visibility = Visibility.Collapsed;

        _package?.PropertyChanged -= Package_PropertyChanged;
        _package = await App.Winget.FetchAndDecoratePackageDetailsAsync(_packageId);
        if (_isNavigatedAway) return;

        _package.PropertyChanged += Package_PropertyChanged;

        AppNameText.Text = _package.Name;
        PublisherText.Text = FormatPublisher(_package.Publisher);
        VersionText.Text = FormatVersionText(_package.Version, _package.AvailableVersion);
        DescriptionText.Text = FormatDescription(_package.Description);

        if (!string.IsNullOrEmpty(_package.IconUrl))
        {
            try
            {
                AppIconImage.Source = new Microsoft.UI.Xaml.Media.Imaging.BitmapImage(new Uri(_package.IconUrl));
                AppIconImage.Visibility = Visibility.Visible;
                AppIconPlaceholder.Visibility = Visibility.Collapsed;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Failed to set icon: {ex.Message}");
                AppIconImage.Visibility = Visibility.Collapsed;
                AppIconPlaceholder.Background = _package.PlaceholderBackground;
                AppIconPlaceholderText.Text = _package.Initial;
                AppIconPlaceholder.Visibility = Visibility.Visible;
            }
        }
        else
        {
            AppIconImage.Visibility = Visibility.Collapsed;
            AppIconPlaceholder.Background = _package.PlaceholderBackground;
            AppIconPlaceholderText.Text = _package.Initial;
            AppIconPlaceholder.Visibility = Visibility.Visible;
        }

        ReleaseNotesPanel.Visibility = GetTextSectionVisibility(_package.ReleaseNotes);
        if (!string.IsNullOrEmpty(_package.ReleaseNotes)) ReleaseNotesText.Text = _package.ReleaseNotes;

        TagsPanel.Visibility = GetCollectionVisibility(_package.Tags);
        if (_package.Tags.Count > 0) TagsList.ItemsSource = _package.Tags;

        ScreenshotsPanel.Visibility = GetCollectionVisibility(_package.Screenshots);
        if (_package.Screenshots.Count > 0) ScreenshotsList.ItemsSource = _package.Screenshots;

        PopulateMetadata();
        SyncWithRunningTasks();
        UpdateActionButtons();
        UpdateProgressVisibility();
        UpdateViewLogsButtonVisibility();

        DetailsProgress.IsActive = false;
        DetailsContentPanel.Visibility = Visibility.Visible;
    }

    private void TagButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Content: string tag }) Frame.Navigate(typeof(HomePage), GetTagNavigationParameter(tag));
    }


    private void PopulateMetadata()
    {
        if (_package != null) PackageDetailHelper.PopulateMetadata(MetadataContainer, _package.Details);
    }

    private void SyncWithRunningTasks()
    {
        if (_package == null) return;
        var matchingTask = FindActiveTaskForPackage(_package.Id, App.Winget.ActiveTasks);
        if (matchingTask != null)
        {
            _package.IsInstalling = true;
            _package.InstallProgress = matchingTask.Progress;
            _package.InstallStatusText = matchingTask.StatusText;
        }
    }

    private void Package_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (_package == null) return;
        App.DispatcherQueue?.TryEnqueue(() =>
        {
            if (e.PropertyName == nameof(WingetPackage.IsInstalling))
            {
                UpdateProgressVisibility();
                UpdateViewLogsButtonVisibility();
            }
            else if (e.PropertyName == nameof(WingetPackage.InstallProgress)) TaskProgressBar.Value = _package.InstallProgress;
            else if (e.PropertyName == nameof(WingetPackage.InstallStatusText)) ProgressStatusText.Text = _package.InstallStatusText;
            else if (e.PropertyName == nameof(WingetPackage.Status))
            {
                UpdateActionButtons();
                UpdateViewLogsButtonVisibility();
            }
        });
    }

    public static (string Label, bool IsEnabled) GetActionButtonData(WingetPackage pkg) => (pkg.ActionButtonLabel, !pkg.IsInstalling);

    private void UpdateActionButtons()
    {
        if (_package == null) return;
        var (label, enabled) = GetActionButtonData(_package);
        ActionButton.Content = label;
        ActionButton.IsEnabled = enabled;
    }

    public static (Visibility ProgressVisibility, double ProgressValue, string StatusText, bool IsActionEnabled) GetProgressData(WingetPackage pkg)
    {
        if (pkg.IsInstalling)
            return (Visibility.Visible, pkg.InstallProgress, pkg.InstallStatusText, false);
        return (Visibility.Collapsed, 0, "", true);
    }

    private void UpdateProgressVisibility()
    {
        if (_package == null) return;
        var (vis, value, statusText, enabled) = GetProgressData(_package);
        ProgressGrid.Visibility = vis;
        TaskProgressBar.Value = value;
        ProgressStatusText.Text = statusText;
        ActionButton.IsEnabled = enabled;
    }

    private void ActionButton_Click(object sender, RoutedEventArgs e)
    {
        if (_package == null) return;
        App.Winget.TriggerPackageAction(_package);
        UpdateProgressVisibility();
        UpdateActionButtons();
    }

    private void BackButton_Click(object sender, RoutedEventArgs e)
    {
        if (Frame.CanGoBack) Frame.GoBack();
    }

    private void ActiveTasks_CollectionChanged(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e) =>
        App.DispatcherQueue?.TryEnqueue(UpdateViewLogsButtonVisibility);

    public static Visibility GetViewLogsVisibility(WingetPackage? pkg, System.Collections.ObjectModel.ObservableCollection<InstallTask> activeTasks)
    {
        if (pkg == null || activeTasks == null) return Visibility.Collapsed;
        bool hasTask = activeTasks.Any(t => t.PackageId.Equals(pkg.Id, StringComparison.OrdinalIgnoreCase));
        return hasTask ? Visibility.Visible : Visibility.Collapsed;
    }

    private void UpdateViewLogsButtonVisibility()
    {
        if (_package == null) { ViewLogsButton.Visibility = Visibility.Collapsed; return; }
        ViewLogsButton.Visibility = GetViewLogsVisibility(_package, App.Winget.ActiveTasks);
    }

    private void ViewLogsButton_Click(object sender, RoutedEventArgs e)
    {
        if (_package != null) _ = App.ShowLogDialogForPackage(_package, XamlRoot);
    }

    private void Screenshot_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { DataContext: string imageUrl })
        {
            LightboxImage.Source = new Microsoft.UI.Xaml.Media.Imaging.BitmapImage(new Uri(imageUrl));
            LightboxOverlay.Visibility = Visibility.Visible;
        }
    }

    private void CloseLightbox_Click(object sender, RoutedEventArgs e) => LightboxOverlay.Visibility = Visibility.Collapsed;
    private void LightboxOverlay_Tapped(object sender, TappedRoutedEventArgs e) => LightboxOverlay.Visibility = Visibility.Collapsed;
}
