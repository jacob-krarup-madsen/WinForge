namespace WingetStore.Tests;

public class ThrowingWingetService : StubWingetService
{
    public override Task<string> RunCommandAsync(string arguments, CancellationToken cancellationToken = default) => throw new Exception("CLI connection lost");
    public override Task<List<WingetPackage>> SearchPackagesAsync(string query, CancellationToken cancellationToken = default) => throw new Exception("CLI search failed");
    public override Task<List<WingetPackage>> GetInstalledPackagesAsync() => throw new Exception("CLI list failed");
    public override Task<List<WingetPackage>> GetUpgradablePackagesAsync() => throw new Exception("CLI upgrades failed");
    public override Task<List<WingetPackage>> GetPopularPackagesAsync() => throw new Exception("Popular JSON corrupted");
    public override Task<List<WingetPackage>> GetRecommendationsAsync() => throw new Exception("Recommendation engine failed");
    public override Task<List<CategoryItem>> GetCategoriesAsync() => throw new Exception("Categories missing");
    public override Task<WingetPackage?> GetPackageDetailsAsync(PackageId packageId) => throw new Exception("Details unreachable");
    public override Task<WingetPackage> FetchAndDecoratePackageDetailsAsync(PackageId packageId) => throw new Exception("Details failed");
}
