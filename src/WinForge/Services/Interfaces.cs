using System.Collections.ObjectModel;
using WingetStore.Models;

namespace WingetStore.Services;

public interface ISettingsService { bool AutoUpdate { get; set; } string AppTheme { get; set; } bool EnableNotifications { get; set; } }
public interface INotificationService { void ShowError(string title, string message); void ShowInfo(string title, string message); }
public interface IProcessRunner { Task<int> RunStreamAsync(string fileName, string arguments, Action<string> onLineReceived, CancellationToken cancellationToken = default); }
public interface IWingetService
{
    ObservableCollection<InstallTask> ActiveTasks { get; }
    Task<string> RunCommandAsync(string arguments, CancellationToken cancellationToken = default);
    Task<List<WingetPackage>> SearchPackagesAsync(string query, CancellationToken cancellationToken = default);
    Task<List<WingetPackage>> GetInstalledPackagesAsync();
    Task<List<WingetPackage>> GetUpgradablePackagesAsync();
    Task<List<WingetPackage>> GetPopularPackagesAsync();
    Task<List<WingetPackage>> GetRecommendationsAsync();
    Task<List<CategoryItem>> GetCategoriesAsync();
    Task<WingetPackage?> GetPackageDetailsAsync(PackageId packageId);
    Task<WingetPackage> FetchAndDecoratePackageDetailsAsync(PackageId packageId);
    void InstallPackage(WingetPackage package);
    void UpgradePackage(WingetPackage package);
    void UninstallPackage(WingetPackage package);
    void TriggerPackageAction(WingetPackage package);
    void CancelTask(string taskId);
    void CancelTaskForPackage(string packageId);
    WingetPackage GetOrCreatePackage(WingetPackage incoming);
    Task<string> ExportPackagesAsync(string filepath);
    Task<string> ImportPackagesAsync(string filepath);
}
