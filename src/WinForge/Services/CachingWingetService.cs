using System.Collections.ObjectModel;
using WingetStore.Models;

namespace WingetStore.Services;

public class CachingWingetService(IWingetService inner) : IWingetService
{
    private readonly IWingetService _inner = inner ?? throw new ArgumentNullException(nameof(inner));
    private readonly Dictionary<string, WingetPackage> _packageCache = new(StringComparer.OrdinalIgnoreCase);
    public ObservableCollection<InstallTask> ActiveTasks => _inner.ActiveTasks;

    internal static void MergePackageProperties(WingetPackage existing, WingetPackage incoming)
    {
        ArgumentNullException.ThrowIfNull(existing);
        ArgumentNullException.ThrowIfNull(incoming);

        existing.Name = incoming.Name;
        if (!string.IsNullOrEmpty(incoming.Version)) existing.Version = incoming.Version;
        if (!string.IsNullOrEmpty(incoming.AvailableVersion)) existing.AvailableVersion = incoming.AvailableVersion;
        if (!string.IsNullOrEmpty(incoming.Source)) existing.Source = incoming.Source;
        if (!string.IsNullOrEmpty(incoming.Publisher)) existing.Publisher = incoming.Publisher;
        if (incoming.Status != PackageStatus.Installable) existing.Status = incoming.Status;
        if (!string.IsNullOrEmpty(incoming.Description)) existing.Description = incoming.Description;
        if (!string.IsNullOrEmpty(incoming.Homepage)) existing.Homepage = incoming.Homepage;
        if (!string.IsNullOrEmpty(incoming.License)) existing.License = incoming.License;
        if (!string.IsNullOrEmpty(incoming.ReleaseNotes)) existing.ReleaseNotes = incoming.ReleaseNotes;
        if (!string.IsNullOrEmpty(incoming.PublisherUrl)) existing.PublisherUrl = incoming.PublisherUrl;
        if (!string.IsNullOrEmpty(incoming.InstallerType)) existing.InstallerType = incoming.InstallerType;
        if (!string.IsNullOrEmpty(incoming.InstallerUrl)) existing.InstallerUrl = incoming.InstallerUrl;
        if (incoming.Tags != null && incoming.Tags.Count > 0) existing.Tags = incoming.Tags;
        if (incoming.Details != null && incoming.Details.Count > 0) existing.Details = incoming.Details;
        if (incoming.Screenshots != null && incoming.Screenshots.Count > 0) existing.Screenshots = incoming.Screenshots;
    }

    public WingetPackage GetOrCreatePackage(WingetPackage incoming)
    {
        ArgumentNullException.ThrowIfNull(incoming);
        if (string.IsNullOrEmpty(incoming.Id)) return incoming;
        lock (_packageCache)
        {
            if (_packageCache.TryGetValue(incoming.Id, out var existing))
            {
                MergePackageProperties(existing, incoming);
                return existing;
            }
            _packageCache[incoming.Id] = incoming; return incoming;
        }
    }
    public async Task<string> RunCommandAsync(string arguments, CancellationToken cancellationToken = default) => await _inner.RunCommandAsync(arguments, cancellationToken);
    private List<WingetPackage> CacheResults(List<WingetPackage> results) => [.. results.Select(GetOrCreatePackage)];
    public async Task<List<WingetPackage>> SearchPackagesAsync(string query, CancellationToken cancellationToken = default) => CacheResults(await _inner.SearchPackagesAsync(query, cancellationToken));
    public async Task<List<WingetPackage>> GetInstalledPackagesAsync() => CacheResults(await _inner.GetInstalledPackagesAsync());
    public async Task<List<WingetPackage>> GetUpgradablePackagesAsync() => CacheResults(await _inner.GetUpgradablePackagesAsync());
    public async Task<List<WingetPackage>> GetPopularPackagesAsync() => CacheResults(await _inner.GetPopularPackagesAsync());
    public async Task<List<WingetPackage>> GetRecommendationsAsync() => CacheResults(await _inner.GetRecommendationsAsync());
    public async Task<List<CategoryItem>> GetCategoriesAsync() => await _inner.GetCategoriesAsync();
    public async Task<WingetPackage?> GetPackageDetailsAsync(PackageId packageId) { var details = await _inner.GetPackageDetailsAsync(packageId); return details == null ? null : GetOrCreatePackage(details); }
    public async Task<WingetPackage> FetchAndDecoratePackageDetailsAsync(PackageId packageId) { var details = await _inner.FetchAndDecoratePackageDetailsAsync(packageId); return GetOrCreatePackage(details); }
    public void InstallPackage(WingetPackage package) => _inner.InstallPackage(GetOrCreatePackage(package));
    public void UpgradePackage(WingetPackage package) => _inner.UpgradePackage(GetOrCreatePackage(package));
    public void UninstallPackage(WingetPackage package) => _inner.UninstallPackage(GetOrCreatePackage(package));
    public void TriggerPackageAction(WingetPackage package) => _inner.TriggerPackageAction(GetOrCreatePackage(package));
    public void CancelTask(string taskId) => _inner.CancelTask(taskId);
    public void CancelTaskForPackage(string packageId) => _inner.CancelTaskForPackage(packageId);
    public async Task<string> ExportPackagesAsync(string filepath) => await _inner.ExportPackagesAsync(filepath);
    public async Task<string> ImportPackagesAsync(string filepath) => await _inner.ImportPackagesAsync(filepath);
}
