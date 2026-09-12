namespace WingetStore.Models;

public enum TaskOperation { Install, Upgrade, Uninstall }
public enum InstallTaskStatus { Queued, Running, Completed, Failed, Cancelled }
public enum PackageStatus { Installable, Installed, Upgradable }

public static class SortOrders
{
    public const string Default = "default";
    public const string Az = "az";
    public const string Za = "za";
    public const string Publisher = "publisher";
    public const string Id = "id";
    public const string Status = "status";
}

public static class SourceFilters
{
    public const string All = "all";
    public const string Winget = "winget";
}

public static class NavTags
{
    public const string Home = "home";
    public const string Search = "search";
    public const string Installed = "installed";
    public const string Updates = "updates";
    public const string About = "about";
}

public static class FilterDefaults
{
    public const string AllDevelopers = "All Publishers";
}
