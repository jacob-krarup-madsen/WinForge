using System;
using System.Linq;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using WingetStore.Models;
using WingetStore.Services;
using WingetStore.ViewModels;

namespace WingetStore.Pages;

public sealed partial class UpdatesPage : Page
{
    private bool _isNavigatedAway;
    private readonly BulkSelectionHelperUI? _bulkSelect;
    public UpdatesViewModel ViewModel { get; }

    public UpdatesPage()
    {
        ViewModel = App.Services.GetRequiredService<UpdatesViewModel>();
        InitializeComponent();
        _bulkSelect = new BulkSelectionHelperUI(UpdatesList, BulkUpdateButton, SelectedItemsCountText, SelectAllCheckBox, BulkActionBar, BulkSelectToggle);
        ViewModel.PropertyChanged += ViewModel_PropertyChanged;
        Services.IconService.Instance.IconsUpdated += IconService_IconsUpdated;
        Unloaded += UpdatesPage_Unloaded;
    }


    private void UpdatesPage_Unloaded(object sender, RoutedEventArgs e)
    {
        ViewModel.PropertyChanged -= ViewModel_PropertyChanged;
        Services.IconService.Instance.IconsUpdated -= IconService_IconsUpdated;
    }

    private void ViewModel_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(UpdatesViewModel.IsLoading))
        {
            App.Dispatch(() =>
            {
                LoadingProgress.IsActive = ViewModel.IsLoading;
                if (ViewModel.IsLoading)
                {
                    CardScrollViewer.Visibility = Visibility.Collapsed;
                    UpdatesList.Visibility = Visibility.Collapsed;
                    EmptyStatePanel.Visibility = Visibility.Collapsed;
                    UpdateAllButton.IsEnabled = false;
                }
            });
        }
        else if (e.PropertyName == nameof(UpdatesViewModel.FilteredUpgrades))
        {
            App.Dispatch(() => UpdateViewForResultCount());
        }
        else if (e.PropertyName == nameof(UpdatesViewModel.IsGlobalProgressVisible)) App.Dispatch(() => GlobalProgressPanel.Visibility = ViewModel.IsGlobalProgressVisible ? Visibility.Visible : Visibility.Collapsed);
        else if (e.PropertyName == nameof(UpdatesViewModel.GlobalProgressValue)) App.Dispatch(() => GlobalProgressBar.Value = ViewModel.GlobalProgressValue);
        else if (e.PropertyName == nameof(UpdatesViewModel.GlobalProgressPercentText)) App.Dispatch(() => GlobalProgressPercentText.Text = ViewModel.GlobalProgressPercentText);
        else if (e.PropertyName == nameof(UpdatesViewModel.GlobalProgressStatusText)) App.Dispatch(() => GlobalProgressStatusText.Text = ViewModel.GlobalProgressStatusText);
    }

    private void IconService_IconsUpdated(object? sender, EventArgs e)
    {
        App.Dispatch(() =>
        {
            if (_isNavigatedAway || UpdatesList == null) return;
            foreach (var pkg in ViewModel.Upgrades) pkg.RefreshIcon();
        });
    }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        _isNavigatedAway = false;
        UpdateSortGlyphs();
        _ = ViewModel.LoadUpgradesAsync();
    }

    protected override void OnNavigatedFrom(NavigationEventArgs e)
    {
        base.OnNavigatedFrom(e);
        _isNavigatedAway = true;
    }

    private void UpdatesList_ItemClick(object sender, ItemClickEventArgs e)
    {
        if (e.ClickedItem is WingetPackage package) Frame.Navigate(typeof(DetailsPage), package.Id);
    }

    public static (bool HasItems, bool ShowCardView, bool ShowListView, bool ShowEmptyState, bool ShowFullToolbar, string SubtitleText) GetUpdatesViewState(int count)
    {
        bool hasItems = count > 0;
        bool isSmallSet = count > 0 && count <= 3;
        string subtitleText = hasItems ? (count == 1 ? "1 update available" : $"{count} updates available") : "";
        bool showCardView = hasItems && isSmallSet;
        bool showListView = hasItems && !isSmallSet;
        bool showEmptyState = !hasItems;
        bool showFullToolbar = !isSmallSet;
        return (hasItems, showCardView, showListView, showEmptyState, showFullToolbar, subtitleText);
    }

    public static bool CanUpdateAll(bool hasItems, IEnumerable<WingetPackage>? packages)
    {
        if (!hasItems || packages == null) return false;
        return packages.Any(p => p != null && !p.IsInstalling);
    }

    private void UpdateViewForResultCount()
    {
        int count = ViewModel.FilteredUpgrades.Count;
        var (hasItems, showCardView, showListView, showEmptyState, showFullToolbar, subtitleText) = GetUpdatesViewState(count);
        CountSubtitle.Text = subtitleText;

        if (showCardView) CardScrollViewer.Visibility = Visibility.Visible;
        else CardScrollViewer.Visibility = Visibility.Collapsed;

        if (showListView) UpdatesList.Visibility = Visibility.Visible;
        else UpdatesList.Visibility = Visibility.Collapsed;

        if (showEmptyState) EmptyStatePanel.Visibility = Visibility.Visible;
        else EmptyStatePanel.Visibility = Visibility.Collapsed;

        UpdateAllButton.IsEnabled = CanUpdateAll(hasItems, ViewModel.FilteredUpgrades);

        FilterSortPanel.Visibility = showFullToolbar ? Visibility.Visible : Visibility.Collapsed;
        BulkSelectToggle.Visibility = showFullToolbar ? Visibility.Visible : Visibility.Collapsed;
        UpdateAllButton.Visibility = showFullToolbar ? Visibility.Visible : Visibility.Collapsed;
    }

    private void ViewTaskLog_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { DataContext: WingetPackage package }) _ = App.ShowLogDialogForPackage(package, XamlRoot);
    }

    private void UpdateSingle_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { DataContext: WingetPackage package }) ViewModel.Upgrade(package);
    }


    private void BulkSelectToggle_Click(object sender, RoutedEventArgs e) => _bulkSelect?.Toggle();
    private void CancelBulkSelect_Click(object sender, RoutedEventArgs e) => _bulkSelect?.Cancel();
    private void SelectAllCheckBox_Checked(object sender, RoutedEventArgs e) => _bulkSelect?.SelectAll();
    private void SelectAllCheckBox_Unchecked(object sender, RoutedEventArgs e) => _bulkSelect?.DeselectAll();
    private void UpdatesList_SelectionChanged(object sender, SelectionChangedEventArgs e) => _bulkSelect?.OnSelectionChanged();

    private void HeaderName_Click(object sender, RoutedEventArgs e)
    {
        (ViewModel.SortBy, ViewModel.SortDirection) = InstalledPage.ToggleColumnSort(ViewModel.SortBy, ViewModel.SortDirection, "Name");
        UpdateSortGlyphs();
    }
    private void HeaderVersion_Click(object sender, RoutedEventArgs e)
    {
        (ViewModel.SortBy, ViewModel.SortDirection) = InstalledPage.ToggleColumnSort(ViewModel.SortBy, ViewModel.SortDirection, "Version");
        UpdateSortGlyphs();
    }
    private void HeaderPublisher_Click(object sender, RoutedEventArgs e)
    {
        (ViewModel.SortBy, ViewModel.SortDirection) = InstalledPage.ToggleColumnSort(ViewModel.SortBy, ViewModel.SortDirection, "Publisher");
        UpdateSortGlyphs();
    }

    public static (string Glyph, Visibility Visibility) GetSortGlyph(string sortDirection, string sortBy, string targetField) => InstalledPage.GetSortGlyph(sortDirection, sortBy, targetField);

    private void UpdateSortGlyphs() => SortGlyphUpdater.Apply(ViewModel.SortBy, ViewModel.SortDirection, HeaderNameGlyph, HeaderVersionGlyph, HeaderPublisherGlyph);

    public static List<WingetPackage> FilterPackagesForBulkUpdate(IEnumerable<WingetPackage>? selectedPackages) => PackageFilteringHelper.GetEligiblePackagesForAction(selectedPackages);

    private void BulkUpdateButton_Click(object sender, RoutedEventArgs e)
    {
        var upgradable = FilterPackagesForBulkUpdate(UpdatesList.SelectedItems.OfType<WingetPackage>());
        if (upgradable.Count == 0) return;
        foreach (var package in upgradable)
        {
            ViewModel.UpgradeCommand.Execute(package);
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
