using WingetStore.Models;

namespace WingetStore.Services;

public static class PackageFilteringHelper
{
    public static bool MatchesQuery(this WingetPackage pkg, string query) { if (pkg == null) return false; if (string.IsNullOrWhiteSpace(query)) return true; query = query.Trim(); if (query.StartsWith("tag:", StringComparison.OrdinalIgnoreCase)) { string targetTag = query["tag:".Length..].Trim(); if (pkg.Tags != null && pkg.Tags.Exists(t => t.Equals(targetTag, StringComparison.OrdinalIgnoreCase))) return true; } return (pkg.Name ?? "").Contains(query, StringComparison.OrdinalIgnoreCase) || (pkg.Id ?? "").Contains(query, StringComparison.OrdinalIgnoreCase) || (pkg.Publisher ?? "").Contains(query, StringComparison.OrdinalIgnoreCase) || (pkg.Description ?? "").Contains(query, StringComparison.OrdinalIgnoreCase); }
    public static List<WingetPackage> FilterAndSortPackages(List<WingetPackage> source, string query, string sourceFilter = "all", string sortOrder = "default") { var filtered = source.FindAll(p => p.MatchesQuery(query) && MatchesSourceFilter(p.Source, sourceFilter)); SortPackages(filtered, sortOrder); return filtered; }
    public static bool MatchesSourceFilter(string? packageSource, string sourceFilter) => sourceFilter switch { SourceFilters.All => true, SourceFilters.Winget => (packageSource ?? "").Contains("winget", StringComparison.OrdinalIgnoreCase), _ => false };
    public static List<WingetPackage> GetEligiblePackagesForAction(IEnumerable<WingetPackage?>? packages)
    {
        if (packages == null) return [];
        return packages.Where(p => p != null && !p.IsInstalling).Cast<WingetPackage>().ToList();
    }
    public static bool MatchesCategoryFilter(bool isRedistributable, string? categoryFilter)
    {
        if (string.Equals(categoryFilter, "Apps", StringComparison.OrdinalIgnoreCase)) return !isRedistributable;
        if (string.Equals(categoryFilter, "Redist", StringComparison.OrdinalIgnoreCase)) return isRedistributable;
        return true;
    }
    public static (List<WingetPackage> Filtered, int AppsCount, int RedistCount, int TotalCount) FilterAndCountPackages(
        IEnumerable<WingetPackage>? packages,
        string? filterQuery,
        string? sourceFilter,
        string? categoryFilter,
        string? sortBy,
        string? sortDirection,
        Func<WingetPackage, bool>? extraFilter = null)
    {
        var baseList = (packages ?? [])
            .Where(p => p != null && p.MatchesQuery(filterQuery ?? "")
                && MatchesSourceFilter(p.Source, sourceFilter ?? SourceFilters.All)
                && (extraFilter == null || extraFilter(p)))
            .ToList();

        int appsCount = baseList.Count(p => !p.IsRedistributable);
        int redistCount = baseList.Count(p => p.IsRedistributable);
        int totalCount = baseList.Count;

        var filtered = baseList.FindAll(p => MatchesCategoryFilter(p.IsRedistributable, categoryFilter));
        SortPackages(filtered, sortBy ?? "Name", sortDirection ?? "Ascending");

        return (filtered, appsCount, redistCount, totalCount);
    }
    public static List<WingetPackage> FilterAndSortSearchResults(IEnumerable<WingetPackage>? searchResults, string filterQuery, string sourceFilter, string sortOrder)
    {
        var filtered = (searchResults ?? [])
            .Where(p => p != null && p.MatchesQuery(filterQuery ?? "") && MatchesSourceFilter(p.Source, sourceFilter))
            .ToList();

        if (sortOrder == SortOrders.Default)
        {
            filtered = [.. filtered.OrderBy(p => (p.Source ?? "").Equals(SourceFilters.Winget, StringComparison.OrdinalIgnoreCase) ? 0 : 1)];
        }
        else
        {
            SortPackages(filtered, sortOrder);
        }

        return filtered;
    }
    public static void SortPackages(List<WingetPackage> packages, string sortBy, string sortDirection = "Descending")
    {
        if (sortBy == SortOrders.Az) { packages.Sort((a, b) => string.Compare(a.Name ?? "", b.Name ?? "", StringComparison.OrdinalIgnoreCase)); return; }
        if (sortBy == SortOrders.Za) { packages.Sort((a, b) => string.Compare(b.Name ?? "", a.Name ?? "", StringComparison.OrdinalIgnoreCase)); return; }
        if (sortBy == SortOrders.Publisher) { packages.Sort((a, b) => string.Compare(a.Publisher ?? "", b.Publisher ?? "", StringComparison.OrdinalIgnoreCase)); return; }
        if (sortBy == SortOrders.Id) { packages.Sort((a, b) => string.Compare(a.Id ?? "", b.Id ?? "", StringComparison.OrdinalIgnoreCase)); return; }
        if (sortBy == SortOrders.Status)
        {
            static int GetStatusWeight(PackageStatus status) => status switch { PackageStatus.Upgradable => 0, PackageStatus.Installed => 1, _ => 2 };
            packages.Sort((a, b) => GetStatusWeight(a.Status).CompareTo(GetStatusWeight(b.Status)));
            return;
        }

        bool isDescending = string.Equals(sortDirection, "Descending", StringComparison.OrdinalIgnoreCase);
        int descMultiplier = isDescending ? -1 : 1;

        switch (sortBy?.ToLowerInvariant())
        {
            case "name":
                packages.Sort((a, b) => descMultiplier * string.Compare(a.Name ?? "", b.Name ?? "", StringComparison.OrdinalIgnoreCase));
                break;

            case "version":
                packages.Sort((a, b) =>
                {
                    int cmp = VersionComparer.Instance.Compare(a.Version ?? "", b.Version ?? "");
                    if (cmp == 0) cmp = a.Status.CompareTo(b.Status);
                    return descMultiplier * cmp;
                });
                break;

            case "publisher":
                packages.Sort((a, b) => descMultiplier * string.Compare(a.Publisher ?? "", b.Publisher ?? "", StringComparison.OrdinalIgnoreCase));
                break;

            case "id":
                packages.Sort((a, b) => descMultiplier * string.Compare(a.Id ?? "", b.Id ?? "", StringComparison.OrdinalIgnoreCase));
                break;

            default:
                packages.Sort((a, b) => descMultiplier * string.Compare(a.Name ?? "", b.Name ?? "", StringComparison.OrdinalIgnoreCase));
                break;
        }
    }
}
