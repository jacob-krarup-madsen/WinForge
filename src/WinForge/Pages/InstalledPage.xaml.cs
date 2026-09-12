using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Navigation;
using WingetStore.Models;
using WingetStore.Services;
using WingetStore.ViewModels;

namespace WingetStore.Pages;

public sealed partial class InstalledPage : Page
{
    private bool _isNavigatedAway;
    private readonly BulkSelectionHelperUI? _bulkSelect;
    public InstalledViewModel ViewModel { get; }

    public InstalledPage()
    {
        ViewModel = App.Services.GetRequiredService<InstalledViewModel>();
        InitializeComponent();
        _bulkSelect = new BulkSelectionHelperUI(InstalledAppsList, BulkUninstallButton, SelectedItemsCountText, SelectAllCheckBox, BulkActionBar, BulkSelectToggle);
        ViewModel.PropertyChanged += ViewModel_PropertyChanged;
        Services.IconService.Instance.IconsUpdated += IconService_IconsUpdated;
        Unloaded += InstalledPage_Unloaded;
    }


    private void InstalledPage_Unloaded(object sender, RoutedEventArgs e)
    {
        ViewModel.PropertyChanged -= ViewModel_PropertyChanged;
        Services.IconService.Instance.IconsUpdated -= IconService_IconsUpdated;
    }

    public static (Visibility LoadingProgressVis, Visibility AppsListVis, Visibility EmptyStateVis) GetInstalledViewState(bool isLoading, int itemCount)
    {
        if (isLoading)
        {
            return (Visibility.Visible, Visibility.Collapsed, Visibility.Collapsed);
        }

        bool hasItems = itemCount > 0;
        return (
            Visibility.Collapsed,
            hasItems ? Visibility.Visible : Visibility.Collapsed,
            hasItems ? Visibility.Collapsed : Visibility.Visible
        );
    }

    private void ViewModel_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(InstalledViewModel.IsLoading) || e.PropertyName == nameof(InstalledViewModel.FilteredPackages))
        {
            App.Dispatch(() =>
            {
                if (_isNavigatedAway) return;
                var list = ViewModel.FilteredPackages;
                int count = list?.Count ?? 0;
                var (loadingVis, listVis, emptyVis) = GetInstalledViewState(ViewModel.IsLoading, count);
                if (LoadingProgress != null)
                {
                    LoadingProgress.IsActive = ViewModel.IsLoading;
                    if (ViewModel.IsLoading) LoadingProgress.Visibility = loadingVis;
                }
                if (InstalledAppsList != null)
                {
                    if (e.PropertyName == nameof(InstalledViewModel.FilteredPackages)) InstalledAppsList.ItemsSource = list;
                    InstalledAppsList.Visibility = listVis;
                }
                if (EmptyStatePanel != null) EmptyStatePanel.Visibility = emptyVis;
            });
        }
        else if (e.PropertyName == nameof(InstalledViewModel.LastRefreshTimeText))
        {
            App.Dispatch(() =>
            {
                if (_isNavigatedAway || LastRefreshText == null) return;
                LastRefreshText.Text = ViewModel.LastRefreshTimeText;
            });
        }
    }

    private void IconService_IconsUpdated(object? sender, EventArgs e)
    {
        App.Dispatch(() =>
        {
            if (_isNavigatedAway || InstalledAppsList == null || ViewModel.FilteredPackages == null) return;
            foreach (var pkg in ViewModel.FilteredPackages) { pkg?.RefreshIcon(); }
        });
    }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        _isNavigatedAway = false;
        UpdateSortGlyphs();
        _ = ViewModel.LoadPackagesAsync();
    }

    protected override void OnNavigatedFrom(NavigationEventArgs e)
    {
        base.OnNavigatedFrom(e);
        _isNavigatedAway = true;
    }

    private void InstalledAppsList_ItemClick(object sender, ItemClickEventArgs e)
    {
        if (e.ClickedItem is WingetPackage package) Frame.Navigate(typeof(DetailsPage), package.Id);
    }

    public static Visibility GetUpdateVisibility(PackageStatus status) => status == PackageStatus.Upgradable ? Visibility.Visible : Visibility.Collapsed;

    public static (InfoBarSeverity Severity, string Title, string Message) GetImportStatusMessage(bool isSuccess, Exception? exception)
    {
        if (isSuccess)
        {
            return (InfoBarSeverity.Success, "Import Completed", "Packages list imported and processed successfully.");
        }
        return (InfoBarSeverity.Error, "Import Failed", $"An error occurred during import: {exception?.Message}");
    }

    public static (InfoBarSeverity Severity, string Title, string Message) GetExportStatusMessage(bool isSuccess, string? filePath, Exception? exception)
    {
        if (isSuccess)
        {
            return (InfoBarSeverity.Success, "Export Complete", $"Successfully exported your installed packages list to: {filePath}");
        }
        return (InfoBarSeverity.Error, "Export Failed", $"An error occurred during export: {exception?.Message}");
    }

    private async void ImportButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var picker = new Windows.Storage.Pickers.FileOpenPicker();
            var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(App.MainWindow);
            WinRT.Interop.InitializeWithWindow.Initialize(picker, hwnd);
            picker.SuggestedStartLocation = Windows.Storage.Pickers.PickerLocationId.DocumentsLibrary;
            picker.FileTypeFilter.Add(".json");

            var file = await picker.PickSingleFileAsync();
            if (file != null)
            {
                LoadingProgress?.IsActive = true;
                InstalledAppsList?.Visibility = Visibility.Collapsed;
                EmptyStatePanel?.Visibility = Visibility.Collapsed;

                if (StatusInfoBar is not null)
                {
                    StatusInfoBar.Severity = InfoBarSeverity.Informational;
                    StatusInfoBar.Title = "Importing Packages";
                    StatusInfoBar.Message = "Windows Package Manager is importing the package list in the background. This may take several minutes.";
                    StatusInfoBar.IsOpen = true;
                }

                await App.Winget.ImportPackagesAsync(file.Path);
                _ = ViewModel.LoadPackagesAsync();

                if (StatusInfoBar is not null)
                {
                    var (severity, title, message) = GetImportStatusMessage(true, null);
                    StatusInfoBar.Severity = severity;
                    StatusInfoBar.Title = title;
                    StatusInfoBar.Message = message;
                }
            }
        }
        catch (Exception ex)
        {
            LogService.LogError("Import failed", ex);
            if (StatusInfoBar is not null)
            {
                var (severity, title, message) = GetImportStatusMessage(false, ex);
                StatusInfoBar.Severity = severity;
                StatusInfoBar.Title = title;
                StatusInfoBar.Message = message;
                StatusInfoBar.IsOpen = true;
            }
            LoadingProgress?.IsActive = false;
            ViewModel.ApplyFilter();
        }
    }

    private async void ExportButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var picker = new Windows.Storage.Pickers.FileSavePicker();
            var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(App.MainWindow);
            WinRT.Interop.InitializeWithWindow.Initialize(picker, hwnd);
            picker.SuggestedStartLocation = Windows.Storage.Pickers.PickerLocationId.DocumentsLibrary;
            picker.FileTypeChoices.Add("Winget Import/Export JSON", [".json"]);
            picker.SuggestedFileName = "winget-packages";

            var file = await picker.PickSaveFileAsync();
            if (file != null)
            {
                LoadingProgress?.IsActive = true;
                InstalledAppsList?.Visibility = Visibility.Collapsed;
                EmptyStatePanel?.Visibility = Visibility.Collapsed;

                await App.Winget.ExportPackagesAsync(file.Path);

                LoadingProgress?.IsActive = false;
                ViewModel.ApplyFilter();

                if (StatusInfoBar is not null)
                {
                    var (severity, title, message) = GetExportStatusMessage(true, file.Path, null);
                    StatusInfoBar.Severity = severity;
                    StatusInfoBar.Title = title;
                    StatusInfoBar.Message = message;
                    StatusInfoBar.IsOpen = true;
                }
            }
        }
        catch (Exception ex)
        {
            LogService.LogError("Export failed", ex);
            if (StatusInfoBar is not null)
            {
                var (severity, title, message) = GetExportStatusMessage(false, null, ex);
                StatusInfoBar.Severity = severity;
                StatusInfoBar.Title = title;
                StatusInfoBar.Message = message;
                StatusInfoBar.IsOpen = true;
            }
            LoadingProgress?.IsActive = false;
            ViewModel.ApplyFilter();
        }
    }

    private void ViewTaskLog_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { DataContext: WingetPackage package }) _ = App.ShowLogDialogForPackage(package, XamlRoot);
    }

    private void UninstallSingle_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { DataContext: WingetPackage package }) ViewModel.Uninstall(package);
    }

    private void UpdateSingle_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { DataContext: WingetPackage package }) ViewModel.Upgrade(package);
    }


    private void BulkSelectToggle_Click(object sender, RoutedEventArgs e) => _bulkSelect?.Toggle();
    private void CancelBulkSelect_Click(object sender, RoutedEventArgs e) => _bulkSelect?.Cancel();
    private void SelectAllCheckBox_Checked(object sender, RoutedEventArgs e) => _bulkSelect?.SelectAll();
    private void SelectAllCheckBox_Unchecked(object sender, RoutedEventArgs e) => _bulkSelect?.DeselectAll();
    private void InstalledAppsList_SelectionChanged(object sender, SelectionChangedEventArgs e) => _bulkSelect?.OnSelectionChanged();

    public static (string NewSortBy, string NewSortDirection) ToggleColumnSort(string currentSortBy, string currentSortDirection, string targetField)
    {
        if (currentSortBy == targetField)
        {
            string nextDirection = currentSortDirection == "Descending" ? "Ascending" : "Descending";
            return (currentSortBy, nextDirection);
        }
        return (targetField, "Descending");
    }

    private void HeaderName_Click(object sender, RoutedEventArgs e)
    {
        (ViewModel.SortBy, ViewModel.SortDirection) = ToggleColumnSort(ViewModel.SortBy, ViewModel.SortDirection, "Name");
        UpdateSortGlyphs();
    }
    private void HeaderVersion_Click(object sender, RoutedEventArgs e)
    {
        (ViewModel.SortBy, ViewModel.SortDirection) = ToggleColumnSort(ViewModel.SortBy, ViewModel.SortDirection, "Version");
        UpdateSortGlyphs();
    }
    private void HeaderPublisher_Click(object sender, RoutedEventArgs e)
    {
        (ViewModel.SortBy, ViewModel.SortDirection) = ToggleColumnSort(ViewModel.SortBy, ViewModel.SortDirection, "Publisher");
        UpdateSortGlyphs();
    }

    public static (string Glyph, Visibility Visibility) GetSortGlyph(string sortDirection, string sortBy, string targetField)
    {
        string glyph = sortDirection == "Descending" ? "\uE74B" : "\uE74A";
        bool isActive = sortBy == targetField;
        return (glyph, isActive ? Visibility.Visible : Visibility.Collapsed);
    }

    private void UpdateSortGlyphs() => SortGlyphUpdater.Apply(ViewModel.SortBy, ViewModel.SortDirection, HeaderNameGlyph, HeaderVersionGlyph, HeaderPublisherGlyph);

    public static List<WingetPackage> GetEligibleBulkUninstallPackages(IEnumerable<WingetPackage?>? selectedPackages) => PackageFilteringHelper.GetEligiblePackagesForAction(selectedPackages);

    private void BulkUninstallButton_Click(object sender, RoutedEventArgs e)
    {
        if (InstalledAppsList == null) return;
        var selected = GetEligibleBulkUninstallPackages(InstalledAppsList.SelectedItems.OfType<WingetPackage>());
        if (selected.Count == 0) return;
        foreach (var package in selected)
        {
            ViewModel.UninstallCommand.Execute(package);
        }
        _bulkSelect?.Deactivate();
    }

    private void CategoryBtn_Click(object sender, RoutedEventArgs e)
    {
        ViewModel.ApplyFilter();
    }

    private void AllCategoryBtn_Click(object sender, RoutedEventArgs e)
    {
        ViewModel.CategoryFilter = "All";
    }
}
