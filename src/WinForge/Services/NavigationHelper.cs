using WingetStore.Models;

namespace WingetStore.Services;

public static class NavigationHelper
{
    public static bool CanGoBack(Microsoft.UI.Xaml.Controls.Frame frame) => frame.CanGoBack;
    public static Type? GetPageType(string? tag, bool isSettingsSelected, bool isWingetAvailable) { if (!isWingetAvailable) return typeof(Pages.NoWingetPage); if (isSettingsSelected) return typeof(Pages.SettingsPage); if (string.IsNullOrEmpty(tag)) return null; return tag switch { NavTags.Home or NavTags.Search => typeof(Pages.HomePage), NavTags.Installed => typeof(Pages.InstalledPage), NavTags.Updates => typeof(Pages.UpdatesPage), "features" => typeof(Pages.FeaturesPage), "optimizer" => typeof(Pages.OptimizerPage), NavTags.About => typeof(Pages.AboutPage), _ => null }; }
}
