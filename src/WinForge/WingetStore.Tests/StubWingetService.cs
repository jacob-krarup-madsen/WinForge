namespace WingetStore.Tests;

public abstract class StubWingetService : IWingetService
{
    public virtual ObservableCollection<InstallTask> ActiveTasks => throw new NotImplementedException();
    public virtual WingetPackage GetOrCreatePackage(WingetPackage incoming) => incoming;
    public virtual Task<string> RunCommandAsync(string arguments, CancellationToken cancellationToken = default) => throw new NotImplementedException();
    public virtual Task<List<WingetPackage>> SearchPackagesAsync(string query, CancellationToken cancellationToken = default) => throw new NotImplementedException();
    public virtual Task<List<WingetPackage>> GetInstalledPackagesAsync() => throw new NotImplementedException();
    public virtual Task<List<WingetPackage>> GetUpgradablePackagesAsync() => throw new NotImplementedException();
    public virtual Task<List<WingetPackage>> GetPopularPackagesAsync() => throw new NotImplementedException();
    public virtual Task<List<WingetPackage>> GetRecommendationsAsync() => throw new NotImplementedException();
    public virtual Task<List<CategoryItem>> GetCategoriesAsync() => throw new NotImplementedException();
    public virtual Task<WingetPackage?> GetPackageDetailsAsync(PackageId packageId) => throw new NotImplementedException();
    public virtual Task<WingetPackage> FetchAndDecoratePackageDetailsAsync(PackageId packageId) => throw new NotImplementedException();
    public virtual void InstallPackage(WingetPackage package) => throw new NotImplementedException();
    public virtual void UpgradePackage(WingetPackage package) => throw new NotImplementedException();
    public virtual void UninstallPackage(WingetPackage package) => throw new NotImplementedException();
    public virtual void TriggerPackageAction(WingetPackage package) => throw new NotImplementedException();
    public virtual void CancelTask(string taskId) {}
    public virtual void CancelTaskForPackage(string packageId) {}
    public virtual Task<string> ExportPackagesAsync(string filepath) => throw new NotImplementedException();
    public virtual Task<string> ImportPackagesAsync(string filepath) => throw new NotImplementedException();
}
