using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using WingetStore.Models;
using WingetStore.Services;

namespace WingetStore.ViewModels;

public partial class UpdatesViewModel : FilterableViewModel
{
    private readonly IWingetService _winget;
    private List<WingetPackage> _allUpgrades = [];
    [ObservableProperty] public partial ObservableCollection<WingetPackage> FilteredUpgrades { get; set; } = [];
    [ObservableProperty] public partial ObservableCollection<WingetPackage> Upgrades { get; set; } = [];
    [ObservableProperty] public partial bool IsGlobalProgressVisible { get; set; }
    [ObservableProperty] public partial double GlobalProgressValue { get; set; }
    [ObservableProperty] public partial string GlobalProgressPercentText { get; set; } = "0%";
    [ObservableProperty] public partial string GlobalProgressStatusText { get; set; } = "";
    [ObservableProperty] public partial string LastRefreshTimeText { get; set; } = "";
    public UpdatesViewModel(IWingetService winget)
    {
        _winget = winget;
        WeakReferenceMessenger.Default.Register<PackageStatusChangedMessage>(this, (r, m) =>
        {
            var package = m.Value; if (package == null || string.IsNullOrEmpty(package.Id)) return;
            App.Dispatch(() =>
            {
                UpdateGlobalProgress();
                if (package.Status == PackageStatus.Installed)
                {
                    bool removed = HandlePackageInstalled(_allUpgrades, Upgrades, package);
                    if (removed)
                    {
                        ApplyFilter();
                        if (App.MainWindow is MainWindow mainWindow) mainWindow.UpdateUpdatesBadge(Upgrades.Count);
                    }
                }
            });
        });
    }
    [RelayCommand]
    public async Task LoadUpgradesAsync()
    {
        try
        {
            App.Dispatch(() => { IsLoading = true; IsErrorOpen = false; ErrorMessage = ""; });
            LogService.LogInfo("Loading upgradable packages...");
            var packages = await _winget.GetUpgradablePackagesAsync();
            App.Dispatch(() =>
            {
                Upgrades = new ObservableCollection<WingetPackage>(packages);
                _allUpgrades = packages;
                if (App.MainWindow is MainWindow mainWindow) mainWindow.UpdateUpdatesBadge(Upgrades.Count);
                ApplyFilter();
                UpdateGlobalProgress();
                LastRefreshTimeText = $"Last checked: {DateTime.Now.ToString("h:mm tt", CultureInfo.InvariantCulture)}";
            });
        }
        catch (Exception ex)
        {
            LogService.LogError("LoadUpgradesAsync failed", ex);
            App.Dispatch(() => { ErrorMessage = $"Failed to load upgradable apps: {ex.Message}"; IsErrorOpen = true; });
        }
        finally
        {
            App.Dispatch(() => IsLoading = false);
        }
    }

    public static bool HandlePackageInstalled(List<WingetPackage> allUpgrades, ObservableCollection<WingetPackage> upgradesCollection, WingetPackage installedPackage)
    {
        if (installedPackage == null || string.IsNullOrEmpty(installedPackage.Id)) return false;
        bool removedFromUpgrades = false;
        if (upgradesCollection != null)
        {
            var itemToRemove = upgradesCollection.FirstOrDefault(p => p != null && p.Id.Equals(installedPackage.Id, StringComparison.OrdinalIgnoreCase));
            if (itemToRemove != null)
            {
                upgradesCollection.Remove(itemToRemove);
                removedFromUpgrades = true;
            }
        }
        int removedCount = allUpgrades?.RemoveAll(p => p != null && p.Id.Equals(installedPackage.Id, StringComparison.OrdinalIgnoreCase)) ?? 0;
        return removedFromUpgrades || removedCount > 0;
    }

    public static List<WingetPackage> GetEligiblePackagesForUpgrade(IEnumerable<WingetPackage>? packages) => PackageFilteringHelper.GetEligiblePackagesForAction(packages);

    public static (List<WingetPackage> FilteredUpgrades, int AppsCount, int RedistCount, int TotalCount) FilterUpgradablePackages(
        IEnumerable<WingetPackage>? packages,
        string? filterQuery,
        string? sourceFilter,
        string? categoryFilter,
        string? sortBy,
        string? sortDirection)
    {
        return PackageFilteringHelper.FilterAndCountPackages(
            packages, filterQuery, sourceFilter, categoryFilter, sortBy, sortDirection);
    }

    public override void ApplyFilter()
    {
        var (filtered, appsCount, redistCount, totalCount) = FilterUpgradablePackages(
            _allUpgrades, FilterQuery, SourceFilter, CategoryFilter, SortBy, SortDirection);
        AppsCount = appsCount;
        RedistCount = redistCount;
        TotalCount = totalCount;
        FilteredUpgrades = new ObservableCollection<WingetPackage>(filtered);
    }
    [RelayCommand] public void Upgrade(WingetPackage package) { if (package == null) return; LogService.LogInfo($"Upgrading single package: {package.Id}"); _winget.UpgradePackage(package); UpdateGlobalProgress(); }
    [RelayCommand] public void UpgradeAll() { LogService.LogInfo("Upgrading all available packages..."); var itemsToUpgrade = GetEligiblePackagesForUpgrade(Upgrades); foreach (var package in itemsToUpgrade) { _winget.UpgradePackage(package); } UpdateGlobalProgress(); }
    public static (bool IsVisible, double ProgressValue, string PercentText, string StatusText) CalculateGlobalProgress(IEnumerable<WingetPackage>? packages)
    {
        if (packages == null) return (false, 0, "0%", "");
        var activeUpgrades = packages.Where(p => p != null && p.IsInstalling).ToList();
        if (activeUpgrades.Count == 0) return (false, 0, "0%", "");
        double averageProgress = activeUpgrades.Sum(pkg => pkg.InstallProgress) / activeUpgrades.Count;
        string statusText = activeUpgrades.Count == 1 ? $"Updating {activeUpgrades[0].Name}..." : $"Updating {activeUpgrades.Count} apps...";
        return (true, averageProgress, $"{(int)averageProgress}%", statusText);
    }

    public void UpdateGlobalProgress()
    {
        var (isVisible, progressValue, percentText, statusText) = CalculateGlobalProgress(Upgrades);
        IsGlobalProgressVisible = isVisible;
        if (isVisible)
        {
            GlobalProgressValue = progressValue;
            GlobalProgressPercentText = percentText;
            GlobalProgressStatusText = statusText;
        }
    }
}

