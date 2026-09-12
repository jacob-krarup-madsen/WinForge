using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WingetStore.Models;
using WingetStore.Services;

namespace WingetStore.ViewModels;

public partial class HomeViewModel(IWingetService winget) : FilterableViewModel
{
    private readonly IWingetService _winget = winget;
    private CancellationTokenSource? _searchCts;
    private List<WingetPackage> _allRecommendations = [];
    private List<WingetPackage> _allSearchResults = [];


    [ObservableProperty] public partial ObservableCollection<WingetPackage> FilteredRecommendations { get; set; } = [];
    [ObservableProperty] public partial ObservableCollection<WingetPackage> Recommendations { get; set; } = [];
    [ObservableProperty] public partial ObservableCollection<CategoryItem> Categories { get; set; } = [];

    [ObservableProperty] public partial ObservableCollection<WingetPackage> FilteredSearchResults { get; set; } = [];
    [ObservableProperty] public partial ObservableCollection<WingetPackage> SearchResults { get; set; } = [];
    [ObservableProperty] public partial string SearchQuery { get; set; } = "";
    [ObservableProperty] public partial bool HasSearchResults { get; set; }
    [ObservableProperty] public partial bool IsSearchActive { get; set; }

    [RelayCommand]
    public async Task LoadFeaturedContentAsync()
    {
        try
        {
            App.Dispatch(() => { IsLoading = true; IsErrorOpen = false; ErrorMessage = ""; });
            LogService.LogInfo("Loading home featured content...");
            var categories = await _winget.GetCategoriesAsync();
            var recommendations = await _winget.GetRecommendationsAsync();
            App.Dispatch(() => { Categories = new ObservableCollection<CategoryItem>(categories); _allRecommendations = recommendations; Recommendations = new ObservableCollection<WingetPackage>(recommendations); ApplyFilter(); });
            LogService.LogInfo($"Home featured content loaded ({recommendations.Count} recommendations, {categories.Count} categories).");
        }
        catch (Exception ex)
        {
            LogService.LogError("LoadFeaturedContentAsync failed", ex);
            App.Dispatch(() => { ErrorMessage = $"Failed to load home content: {ex.Message}"; IsErrorOpen = true; });
        }
        finally
        {
            App.Dispatch(() => IsLoading = false);
        }
    }

    [RelayCommand]
    public async Task SearchAsync(string query) => await SearchInternalAsync(query, false);

    public void CancelSearch() => _searchCts?.Cancel();

    public static (bool ShouldSearch, string CleanQuery, string DisplayQuery) ProcessSearchQuery(string? query, bool forceSearchAll)
    {
        string cleanQuery = query?.Trim() ?? "";
        bool shouldSearch = !string.IsNullOrWhiteSpace(cleanQuery) || forceSearchAll;
        string displayQuery = string.IsNullOrWhiteSpace(cleanQuery) ? "All Applications" : cleanQuery;
        return (shouldSearch, cleanQuery, displayQuery);
    }

    public async Task SearchInternalAsync(string query, bool forceSearchAll = false)
    {
        var (shouldSearch, searchKey, displayQuery) = ProcessSearchQuery(query, forceSearchAll);
        if (!shouldSearch)
        {
            App.Dispatch(() => { IsSearchActive = false; SearchQuery = ""; });
            return;
        }

        _searchCts?.Cancel(); _searchCts = new CancellationTokenSource(); var token = _searchCts.Token;
        try
        {
            App.Dispatch(() => { IsLoading = true; IsErrorOpen = false; ErrorMessage = ""; SearchQuery = displayQuery; IsSearchActive = true; });
            LogService.LogInfo($"Searching winget for query: '{searchKey}' (forceAll: {forceSearchAll})");
            var results = await _winget.SearchPackagesAsync(searchKey, token);
            if (!token.IsCancellationRequested) { App.Dispatch(() => { _allSearchResults = results; SearchResults = new ObservableCollection<WingetPackage>(results); ApplyFilter(); }); }
        }
        catch (OperationCanceledException) { LogService.LogInfo($"Search cancelled for query: '{searchKey}'"); }
        catch (Exception ex)
        {
            LogService.LogError($"SearchAsync failed for query '{searchKey}'", ex);
            App.Dispatch(() => { ErrorMessage = $"Search failed: {ex.Message}"; IsErrorOpen = true; });
        }
        finally
        {
            if (!token.IsCancellationRequested) App.Dispatch(() => IsLoading = false);
        }
    }

    public static List<WingetPackage> FilterAndSortRecommendations(IEnumerable<WingetPackage>? recommendations, string filterQuery, string sortOrder)
    {
        var filtered = (recommendations ?? []).Where(p => p != null && p.MatchesQuery(filterQuery ?? "")).ToList();
        SortPackages(filtered, sortOrder);
        return filtered;
    }

    public static List<WingetPackage> FilterAndSortSearchResults(IEnumerable<WingetPackage>? searchResults, string filterQuery, string sourceFilter, string sortOrder) => PackageFilteringHelper.FilterAndSortSearchResults(searchResults, filterQuery, sourceFilter, sortOrder);

    public override void ApplyFilter()
    {
        var filteredRecs = FilterAndSortRecommendations(_allRecommendations, FilterQuery, SortOrder);
        FilteredRecommendations = new ObservableCollection<WingetPackage>(filteredRecs);

        var filteredResults = FilterAndSortSearchResults(_allSearchResults, FilterQuery, SourceFilter, SortOrder);
        FilteredSearchResults = [.. filteredResults];
        HasSearchResults = FilteredSearchResults.Count > 0;
    }
}
