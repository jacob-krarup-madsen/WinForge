using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using WingetStore.Models;
using WingetStore.Services;

namespace WingetStore.ViewModels;

public partial class InstalledViewModel : FilterableViewModel
{
    private readonly IWingetService _winget;
    private List<WingetPackage> _allPackages = [];
    [ObservableProperty] public partial ObservableCollection<WingetPackage> FilteredPackages { get; set; } = [];
    [ObservableProperty][NotifyPropertyChangedFor(nameof(DeveloperOptions))] public partial List<string> DevelopersList { get; set; } = [];
    [ObservableProperty] public partial string LastRefreshTimeText { get; set; } = "";
    public List<string> DeveloperOptions { get { var list = new List<string> { FilterDefaults.AllDevelopers }; if (DevelopersList != null) list.AddRange(DevelopersList); return list; } }
    [ObservableProperty] public partial string DeveloperFilter { get; set; } = FilterDefaults.AllDevelopers;
    public InstalledViewModel(IWingetService winget)
    {
        _winget = winget;
        WeakReferenceMessenger.Default.Register<PackageStatusChangedMessage>(this, (r, m) =>
        {
            var package = m.Value; if (package == null || string.IsNullOrEmpty(package.Id)) return;
            App.Dispatch(() =>
            {
                bool updated = HandlePackageStatusChange(_allPackages, package);
                if (updated)
                {
                    ApplyFilter();
                    if (package.Status == PackageStatus.Installed && App.MainWindow is MainWindow mainWindow)
                    {
                        mainWindow.UpdateUpdatesBadge(CountUpgradablePackages(_allPackages));
                    }
                }
            });
        });
    }
    partial void OnDeveloperFilterChanged(string value) => ApplyFilter();
    [RelayCommand]
    public async Task LoadPackagesAsync()
    {
        try
        {
            App.Dispatch(() => { IsLoading = true; IsErrorOpen = false; ErrorMessage = ""; SourceFilter = SourceFilters.All; DeveloperFilter = FilterDefaults.AllDevelopers; });
            LogService.LogInfo("Loading installed packages...");
            var packages = await _winget.GetInstalledPackagesAsync();
            App.Dispatch(() => { _allPackages = packages ?? []; PopulateDevelopersList(); ApplyFilter(); LastRefreshTimeText = $"Last refreshed: {DateTime.Now.ToString("h:mm tt", CultureInfo.InvariantCulture)}"; });
        }
        catch (Exception ex)
        {
            LogService.LogError("LoadPackagesAsync failed", ex);
            App.Dispatch(() => { ErrorMessage = $"Failed to load installed apps: {ex.Message}"; IsErrorOpen = true; });
        }
        finally
        {
            App.Dispatch(() => IsLoading = false);
        }
    }
    public static List<string> ExtractDevelopersList(IEnumerable<WingetPackage>? packages)
    {
        if (packages == null) return [];
        var publishers = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var p in packages)
        {
            if (p != null && !string.IsNullOrWhiteSpace(p.Publisher))
                publishers.Add(p.Publisher.Trim());
        }
        return [.. publishers.OrderBy(p => p, StringComparer.OrdinalIgnoreCase)];
    }

    public static string NormalizeDeveloperFilter(string? currentFilter, IEnumerable<string>? availableOptions)
    {
        if (string.IsNullOrWhiteSpace(currentFilter)) return FilterDefaults.AllDevelopers;
        if (availableOptions != null && availableOptions.Contains(currentFilter, StringComparer.OrdinalIgnoreCase)) return currentFilter;
        return FilterDefaults.AllDevelopers;
    }

    public static bool MatchesDeveloperFilter(string? packagePublisher, string? developerFilter)
    {
        string devFilter = (string.IsNullOrWhiteSpace(developerFilter) || developerFilter.Equals(FilterDefaults.AllDevelopers, StringComparison.OrdinalIgnoreCase)) ? SourceFilters.All : developerFilter;
        if (devFilter == SourceFilters.All) return true;
        if (string.IsNullOrEmpty(packagePublisher)) return false;
        return packagePublisher.Equals(devFilter, StringComparison.OrdinalIgnoreCase);
    }

    public static bool HandlePackageStatusChange(List<WingetPackage> packages, WingetPackage statusPackage)
    {
        if (packages == null || statusPackage == null || string.IsNullOrEmpty(statusPackage.Id)) return false;

        if (statusPackage.Status == PackageStatus.Installable)
        {
            int removedCount = packages.RemoveAll(p => p != null && p.Id.Equals(statusPackage.Id, StringComparison.OrdinalIgnoreCase));
            return removedCount > 0;
        }
        else if (statusPackage.Status == PackageStatus.Installed)
        {
            var target = packages.FirstOrDefault(p => p != null && p.Id.Equals(statusPackage.Id, StringComparison.OrdinalIgnoreCase));
            if (target != null)
            {
                target.Status = PackageStatus.Installed;
                if (!string.IsNullOrEmpty(statusPackage.AvailableVersion))
                {
                    target.Version = statusPackage.AvailableVersion;
                    target.AvailableVersion = "";
                }
                return true;
            }
        }
        return false;
    }

    public static int CountUpgradablePackages(IEnumerable<WingetPackage>? packages)
    {
        if (packages == null) return 0;
        return packages.Count(p => p != null && p.Status == PackageStatus.Upgradable);
    }

    public static (List<WingetPackage> FilteredPackages, int AppsCount, int RedistCount, int TotalCount) FilterInstalledPackages(
        IEnumerable<WingetPackage>? packages,
        string? filterQuery,
        string? developerFilter,
        string? sourceFilter,
        string? categoryFilter,
        string? sortBy,
        string? sortDirection)
    {
        return PackageFilteringHelper.FilterAndCountPackages(
            packages, filterQuery, sourceFilter, categoryFilter, sortBy, sortDirection,
            p => MatchesDeveloperFilter(p.Publisher, developerFilter));
    }

    private void PopulateDevelopersList()
    {
        DevelopersList = ExtractDevelopersList(_allPackages);
        DeveloperFilter = NormalizeDeveloperFilter(DeveloperFilter, DeveloperOptions);
    }

    public override void ApplyFilter()
    {
        var (filtered, appsCount, redistCount, totalCount) = FilterInstalledPackages(
            _allPackages, FilterQuery, DeveloperFilter, SourceFilter, CategoryFilter, SortBy, SortDirection);
        AppsCount = appsCount;
        RedistCount = redistCount;
        TotalCount = totalCount;
        FilteredPackages = [.. filtered];
    }

    [RelayCommand] public void Uninstall(WingetPackage package) { if (package == null) return; LogService.LogInfo($"Uninstalling package: {package.Id}"); _winget.UninstallPackage(package); }
    [RelayCommand] public void Upgrade(WingetPackage package) { if (package == null) return; LogService.LogInfo($"Upgrading package: {package.Id}"); _winget.UpgradePackage(package); }
}

