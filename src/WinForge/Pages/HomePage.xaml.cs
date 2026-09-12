using System;
using System.Collections.ObjectModel;
using System.Linq;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Navigation;
using Windows.UI.ViewManagement;
using WingetStore.Models;
using WingetStore.Services;
using WingetStore.ViewModels;

namespace WingetStore.Pages;

public sealed partial class HomePage : Page
{
    private bool _isPageActive;
    private UISettings? _uiSettings;

    private int _lastColumnCount;
    private double _lastSlotWidth = double.NaN;
    private double _lastItemHeight = double.NaN;
    private double _lastCardHeight = double.NaN;
    private double _lastEffectiveGap = double.NaN;
    private ItemsWrapGrid? _lastWrapGrid;
    private GridDimensions _lastGridDimensions;

    private string _currentNormalizedQuery = "";

    public HomeViewModel ViewModel { get; }
    public RecommendationLayoutState LayoutState { get; } = new();
    public event EventHandler? SearchStateChanged;

    public double CurrentCardHeight { get; private set; } = 130.0;
    public double CurrentItemHeight { get; private set; } = 146.0;

    public HomePage()
    {
        ViewModel = App.Services.GetRequiredService<HomeViewModel>();
        InitializeComponent();

        ViewModel.PropertyChanged += ViewModel_PropertyChanged;
        IconService.Instance.IconsUpdated += IconService_IconsUpdated;

        Loaded += Page_Loaded;
        Unloaded += Page_Unloaded;

        RecommendationsGrid.Loaded += RecommendationsGrid_Loaded;
        RecommendationsGrid.SizeChanged += RecommendationsGrid_SizeChanged;
        CategoriesGrid.SizeChanged += CategoriesGrid_SizeChanged;
    }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        _isPageActive = true;
        _ = ViewModel.LoadFeaturedContentAsync();

        string searchString = ExtractSearchQuery(e.Parameter);
        if (!string.IsNullOrEmpty(searchString))
        {
            HomeSearchBox.Text = searchString;
            ProcessSearchInput(searchString);
        }

        ApplyRecommendationGridLayout();
    }

    protected override void OnNavigatedFrom(NavigationEventArgs e)
    {
        _isPageActive = false;
        ViewModel.CancelSearch();
        base.OnNavigatedFrom(e);
    }

    private void Page_Loaded(object sender, RoutedEventArgs e)
    {
        _uiSettings ??= new UISettings();
        _uiSettings.TextScaleFactorChanged -= OnTextScaleFactorChanged;
        _uiSettings.TextScaleFactorChanged += OnTextScaleFactorChanged;

        ApplyTextScale(_uiSettings.TextScaleFactor);
        ApplyRecommendationGridLayout();
    }

    private void Page_Unloaded(object sender, RoutedEventArgs e)
    {
        ViewModel.PropertyChanged -= ViewModel_PropertyChanged;
        IconService.Instance.IconsUpdated -= IconService_IconsUpdated;

        if (_uiSettings != null)
        {
            _uiSettings.TextScaleFactorChanged -= OnTextScaleFactorChanged;
        }
    }

    private void RecommendationsGrid_Loaded(object sender, RoutedEventArgs e) => ApplyRecommendationGridLayout();
    private void RecommendationsGrid_SizeChanged(object sender, SizeChangedEventArgs e) => ApplyRecommendationGridLayout();
    private void CategoriesGrid_SizeChanged(object sender, SizeChangedEventArgs e) => ApplyCategoryGridLayout();

    private void OnTextScaleFactorChanged(UISettings sender, object args)
    {
        double factor = sender.TextScaleFactor;
        DispatcherQueue.TryEnqueue(() =>
        {
            if (!_isPageActive) return;
            ApplyTextScale(factor);
            ApplyRecommendationGridLayout();
        });
    }

    public static (double CardHeight, double ItemHeight) GetTextScaleData(double factor) => factor switch
    {
        < 1.5 => (130.0, 146.0),
        < 1.75 => (154.0, 170.0),
        < 2.0 => (186.0, 202.0),
        < 2.25 => (218.0, 234.0),
        _ => (250.0, 266.0)
    };

    private void ApplyTextScale(double factor)
    {
        (CurrentCardHeight, CurrentItemHeight) = GetTextScaleData(factor);
    }

    public static bool ShouldUpdateGridLayout(bool gridRecreated, int newColumns, int lastColumns, double newSlotWidth, double lastSlotWidth, double newItemHeight, double lastItemHeight, double newCardHeight, double lastCardHeight, double newGap, double lastGap)
    {
        if (gridRecreated) return true;
        bool widthChanged = newColumns != lastColumns || Math.Abs(newSlotWidth - lastSlotWidth) >= 0.5;
        bool heightChanged = Math.Abs(newItemHeight - lastItemHeight) >= 0.5;
        bool cardHeightChanged = Math.Abs(newCardHeight - lastCardHeight) >= 0.5;
        bool gapChanged = Math.Abs(newGap - lastGap) >= 0.5;
        return widthChanged || heightChanged || cardHeightChanged || gapChanged;
    }

    private void ApplyRecommendationGridLayout()
    {
        if (RecommendationsGrid.ItemsPanelRoot is not ItemsWrapGrid wrapGrid)
            return;

        bool gridRecreated = _lastWrapGrid != wrapGrid;
        _lastWrapGrid = wrapGrid;

        double usableWidth = Math.Max(0, RecommendationsGrid.ActualWidth);
        GridDimensions dimensions = GridCalculator.CalculateGridDimensions(usableWidth);

        if (!ShouldUpdateGridLayout(gridRecreated, dimensions.Columns, _lastColumnCount, dimensions.SlotWidth, _lastSlotWidth, CurrentItemHeight, _lastItemHeight, CurrentCardHeight, _lastCardHeight, dimensions.EffectiveGap, _lastEffectiveGap))
            return;

        _lastColumnCount = dimensions.Columns;
        _lastSlotWidth = dimensions.SlotWidth;
        _lastItemHeight = CurrentItemHeight;
        _lastCardHeight = CurrentCardHeight;
        _lastEffectiveGap = dimensions.EffectiveGap;
        _lastGridDimensions = dimensions;

        wrapGrid.ItemWidth = dimensions.SlotWidth;
        wrapGrid.ItemHeight = CurrentItemHeight;

        LayoutState.CardMargin = new Thickness(0, 0, dimensions.EffectiveGap, 16);
        LayoutState.CardHeight = CurrentCardHeight;

        ApplyCategoryGridLayout();
    }

    private void ApplyCategoryGridLayout()
    {
        if (CategoriesGrid.ItemsPanelRoot is not ItemsWrapGrid catWrapGrid) return;
        var dims = _lastGridDimensions;
        if (dims.Columns <= 0 || dims.SlotWidth <= 0) return;
        catWrapGrid.ItemWidth = dims.SlotWidth;
        catWrapGrid.ItemHeight = 88.0;
    }

    public static string FormatSearchResultsTitle(string query) => $"Search Results for \"{query}\"";

    public static (Visibility SearchResultsVis, Visibility DiscoverContentVis, Visibility SearchResultsListVis, Visibility EmptyStateVis, string TitleText) DetermineSearchViewState(bool isSearchActive, int itemCount, bool isLoading, string searchQuery)
    {
        if (!isSearchActive)
        {
            return (Visibility.Collapsed, Visibility.Visible, Visibility.Collapsed, Visibility.Collapsed, string.Empty);
        }

        bool hasItems = itemCount > 0;
        Visibility listVis = hasItems ? Visibility.Visible : Visibility.Collapsed;
        Visibility emptyVis = (!hasItems && !isLoading) ? Visibility.Visible : Visibility.Collapsed;
        string title = FormatSearchResultsTitle(searchQuery);

        return (Visibility.Visible, Visibility.Collapsed, listVis, emptyVis, title);
    }

    internal static List<RecommendationCardViewModel> BuildRecommendationCards(IEnumerable<WingetPackage> packages, RecommendationLayoutState layoutState)
    {
        return (packages ?? []).Select(pkg => new RecommendationCardViewModel(pkg, layoutState)).ToList();
    }

    private void ViewModel_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(HomeViewModel.IsLoading))
        {
            App.Dispatch(() =>
            {
                SearchProgress.IsActive = ViewModel.IsLoading;
                SearchProgress.Visibility = ViewModel.IsLoading ? Visibility.Visible : Visibility.Collapsed;
            });
        }
        else if (e.PropertyName == nameof(HomeViewModel.IsSearchActive) || e.PropertyName == nameof(HomeViewModel.FilteredSearchResults))
        {
            App.Dispatch(() =>
            {
                if (!_isPageActive) return;
                var (searchResultsVis, discoverVis, listVis, emptyVis, titleText) = DetermineSearchViewState(
                    ViewModel.IsSearchActive,
                    ViewModel.FilteredSearchResults.Count,
                    ViewModel.IsLoading,
                    ViewModel.SearchQuery
                );
                SearchResultsPanel.Visibility = searchResultsVis;
                DiscoverContentPanel.Visibility = discoverVis;

                if (ViewModel.IsSearchActive)
                {
                    SearchResultsList.Visibility = listVis;
                    EmptyStatePanel.Visibility = emptyVis;
                    SearchResultsTitle.Text = titleText;
                }
                SearchStateChanged?.Invoke(this, EventArgs.Empty);
            });
        }
        else if (e.PropertyName == nameof(HomeViewModel.FilteredRecommendations))
        {
            App.Dispatch(() =>
            {
                var cards = BuildRecommendationCards(ViewModel.FilteredRecommendations, LayoutState);
                RecommendationsGrid.ItemsSource = cards;
                RecommendationsPanel.Visibility = cards.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
                ApplyRecommendationGridLayout();
            });
        }
    }

    private void IconService_IconsUpdated(object? sender, EventArgs e)
    {
        App.Dispatch(() =>
        {
            try
            {
                if (!_isPageActive) return;
                if (ViewModel.Recommendations != null)
                {
                    foreach (var pkg in ViewModel.Recommendations) pkg.RefreshIcon();
                }
                if (ViewModel.SearchResults != null)
                {
                    foreach (var pkg in ViewModel.SearchResults) pkg.RefreshIcon();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Icon update exception: {ex.Message}");
            }
        });
    }

    public static string ExtractSearchQuery(object? parameter)
    {
        if (parameter is string query && !string.IsNullOrWhiteSpace(query))
        {
            return query.StartsWith("category:", StringComparison.OrdinalIgnoreCase)
                ? query["category:".Length..].Trim()
                : query.Trim();
        }
        return string.Empty;
    }

    public static string NormalizeQuery(string? value) => string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();

    public static (string? HintText, string? SearchQuery) GetSearchInputData(string normalized)
    {
        if (normalized.Length == 0)
            return (null, "");
        if (normalized.Length == 1)
            return ("Enter at least 2 characters to search", null);
        return (null, normalized);
    }

    private void ProcessSearchInput(string input)
    {
        string normalized = NormalizeQuery(input);
        if (string.Equals(normalized, _currentNormalizedQuery, StringComparison.OrdinalIgnoreCase))
            return;

        _currentNormalizedQuery = normalized;
        ViewModel.CancelSearch();

        var (hintText, searchQuery) = GetSearchInputData(normalized);
        if (hintText != null)
        {
            SearchHintText.Text = hintText;
            SearchHintText.Visibility = Visibility.Visible;
            SearchResultsPanel.Visibility = Visibility.Collapsed;
            DiscoverContentPanel.Visibility = Visibility.Visible;
            return;
        }
        SearchHintText.Visibility = Visibility.Collapsed;
        _ = ViewModel.SearchAsync(searchQuery ?? "");
    }

    private void HomeSearchBox_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key == Windows.System.VirtualKey.Enter)
        {
            ProcessSearchInput(HomeSearchBox.Text);
        }
    }

    private void SearchButton_Click(object sender, RoutedEventArgs e)
    {
        ProcessSearchInput(HomeSearchBox.Text);
    }

    public void ClearSearch()
    {
        HomeSearchBox.Text = "";
        ProcessSearchInput("");
    }

    private void ClearSearchButton_Click(object sender, RoutedEventArgs e)
    {
        ClearSearch();
    }

    private void SeeAllButton_Click(object sender, RoutedEventArgs e)
    {
        HomeSearchBox.Text = "";
        _currentNormalizedQuery = "";
        _ = ViewModel.SearchInternalAsync("", forceSearchAll: true);
    }

    private void PopularAppsGrid_ItemClick(object sender, ItemClickEventArgs e)
    {
        if (e.ClickedItem is RecommendationCardViewModel cardVm)
        {
            Frame.Navigate(typeof(DetailsPage), cardVm.Package.Id);
        }
        else if (e.ClickedItem is WingetPackage package)
        {
            Frame.Navigate(typeof(DetailsPage), package.Id);
        }
    }

    private void SearchResultsList_ItemClick(object sender, ItemClickEventArgs e)
    {
        if (e.ClickedItem is WingetPackage package) Frame.Navigate(typeof(DetailsPage), package.Id);
    }

    private void DetailsButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { DataContext: WingetPackage package }) Frame.Navigate(typeof(DetailsPage), package.Id);
    }

    private void ActionButton_Click(object sender, RoutedEventArgs e)
    {
        WingetPackage? package = null;
        if (sender is Button { DataContext: RecommendationCardViewModel cardVm }) package = cardVm.Package;
        else if (sender is Button { DataContext: WingetPackage pkg }) package = pkg;

        if (package != null) App.Winget.TriggerPackageAction(package);
    }

    private void CategoriesGrid_ItemClick(object sender, ItemClickEventArgs e)
    {
        if (e.ClickedItem is CategoryItem category)
        {
            HomeSearchBox.Text = category.Name;
            ProcessSearchInput(category.Name);
        }
    }
}
