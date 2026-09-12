using CommunityToolkit.Mvvm.ComponentModel;
using WingetStore.Models;
using WingetStore.Services;

namespace WingetStore.ViewModels;

public abstract partial class FilterableViewModel : ObservableObject
{
    [ObservableProperty] public partial bool IsLoading { get; set; }
    [ObservableProperty] public partial string ErrorMessage { get; set; } = "";
    [ObservableProperty] public partial bool IsErrorOpen { get; set; }
    [ObservableProperty] public partial string FilterQuery { get; set; } = "";
    [ObservableProperty] public partial string SortOrder { get; set; } = SortOrders.Default;
    [ObservableProperty] public partial string SortBy { get; set; } = "Name";
    [ObservableProperty] public partial string SortDirection { get; set; } = "Ascending";
    [ObservableProperty] public partial string CategoryFilter { get; set; } = "Apps";
    [ObservableProperty] public partial string SourceFilter { get; set; } = SourceFilters.All;
    [ObservableProperty] public partial int AppsCount { get; set; }
    [ObservableProperty] public partial int RedistCount { get; set; }
    [ObservableProperty] public partial int TotalCount { get; set; }

    public static string FormatAppsCountText(int count) => $"Applications ({count})";
    public static string FormatRedistCountText(int count) => $"Redistributables ({count})";
    public static string FormatAllCountText(int count) => $"All ({count})";

    public string AppsCountText => FormatAppsCountText(AppsCount);
    public string RedistCountText => FormatRedistCountText(RedistCount);
    public string AllCountText => FormatAllCountText(TotalCount);

    public static bool IsCategorySelected(string? categoryFilter, string targetCategory)
        => string.Equals(categoryFilter, targetCategory, StringComparison.OrdinalIgnoreCase);

    public static string ResolveCategorySelection(string? currentCategoryFilter, string targetCategory, bool isSelected)
        => isSelected ? targetCategory : (currentCategoryFilter ?? "");

    public static bool MatchesCategoryFilter(bool isRedistributable, string? categoryFilter) => PackageFilteringHelper.MatchesCategoryFilter(isRedistributable, categoryFilter);

    public bool IsCategoryApps
    {
        get => IsCategorySelected(CategoryFilter, "Apps");
        set { if (value && CategoryFilter != "Apps") CategoryFilter = ResolveCategorySelection(CategoryFilter, "Apps", value); }
    }
    public bool IsCategoryRedist
    {
        get => IsCategorySelected(CategoryFilter, "Redist");
        set { if (value && CategoryFilter != "Redist") CategoryFilter = ResolveCategorySelection(CategoryFilter, "Redist", value); }
    }
    public bool IsCategoryAll
    {
        get => IsCategorySelected(CategoryFilter, "All");
        set { if (value && CategoryFilter != "All") CategoryFilter = ResolveCategorySelection(CategoryFilter, "All", value); }
    }

    partial void OnCategoryFilterChanged(string value)
    {
        OnPropertyChanged(nameof(IsCategoryApps));
        OnPropertyChanged(nameof(IsCategoryRedist));
        OnPropertyChanged(nameof(IsCategoryAll));
        ApplyFilter();
    }
    partial void OnSourceFilterChanged(string value) => ApplyFilter();
    partial void OnAppsCountChanged(int value) => OnPropertyChanged(nameof(AppsCountText));
    partial void OnRedistCountChanged(int value) => OnPropertyChanged(nameof(RedistCountText));
    partial void OnTotalCountChanged(int value) => OnPropertyChanged(nameof(AllCountText));

    partial void OnFilterQueryChanged(string value) => ApplyFilter();

    public static (string SortBy, string SortDirection) MapSortOrder(string? sortOrder, string currentSortBy = "Name", string currentSortDirection = "Ascending")
    {
        if (sortOrder == SortOrders.Az) return ("Name", "Ascending");
        if (sortOrder == SortOrders.Za) return ("Name", "Descending");
        if (sortOrder == SortOrders.Publisher) return ("Publisher", "Ascending");
        if (sortOrder == SortOrders.Id) return ("Id", "Ascending");
        if (sortOrder == SortOrders.Status) return ("Version", "Descending");
        return (currentSortBy, currentSortDirection);
    }

    partial void OnSortOrderChanged(string value)
    {
        (SortBy, SortDirection) = MapSortOrder(value, SortBy, SortDirection);
        ApplyFilter();
    }
    partial void OnSortByChanged(string value) => ApplyFilter();
    partial void OnSortDirectionChanged(string value) => ApplyFilter();
    public abstract void ApplyFilter();
    protected static bool MatchesSourceFilter(string? packageSource, string sourceFilter) => PackageFilteringHelper.MatchesSourceFilter(packageSource, sourceFilter);

    protected static void SortPackages(List<WingetPackage> packages, string sortOrder) => PackageFilteringHelper.SortPackages(packages, sortOrder);
}
