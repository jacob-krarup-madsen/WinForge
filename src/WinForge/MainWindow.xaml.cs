using System;
using System.Threading.Tasks;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Windows.Graphics;
using WingetStore.Services;

namespace WingetStore;

public enum NavigationMode { Desktop, Tablet, Phone }

public sealed partial class MainWindow : Window
{
    private double _lastRasterizationScale = double.NaN;
    private bool _isResizing;
    private NavigationMode _currentNavigationMode;

    public MainWindow()
    {
        InitializeComponent();
        ExtendsContentIntoTitleBar = true;
        SetTitleBar(AppTitleBar);
        if (AppWindowTitleBar.IsCustomizationSupported())
        {
            var titleBar = AppWindow.TitleBar;
            titleBar.ButtonBackgroundColor = Microsoft.UI.Colors.Transparent;
            titleBar.ButtonInactiveBackgroundColor = Microsoft.UI.Colors.Transparent;
            titleBar.PreferredHeightOption = TitleBarHeightOption.Tall;
        }
        AppWindow.SetIcon("Assets/AppIcon.ico");
        var displayArea = DisplayArea.GetFromWindowId(AppWindow.Id, DisplayAreaFallback.Primary);
        if (displayArea != null)
        {
            int w = 1100, h = 750;
            AppWindow.MoveAndResize(new RectInt32(displayArea.WorkArea.X + (displayArea.WorkArea.Width - w) / 2, displayArea.WorkArea.Y + (displayArea.WorkArea.Height - h) / 2, w, h));
        }

        SizeChanged += MainWindow_SizeChanged;
        NavView.Loaded += (s, e) =>
        {
            if (NavView.SettingsItem is NavigationViewItem settingsItem)
            {
                settingsItem.Content = "Settings";
                settingsItem.Margin = new Thickness(0, 0, 0, 64);
            }
            ApplyNavigationMode(NavView.ActualWidth);
        };
        NavFrame.Navigated += NavFrame_Navigated;
        _ = RefreshUpdatesCountAsync();
        ApplyTheme(SettingsService.AppTheme);
    }

    public static (int PhysW, int PhysH) GetMinimumWindowSize(double width, double height, double scale)
    {
        int targetW = Math.Max((int)width, 800);
        int targetH = Math.Max((int)height, 500);
        return ((int)Math.Ceiling(targetW * scale), (int)Math.Ceiling(targetH * scale));
    }

    public static NavigationMode GetNavigationMode(double width) => width switch
    {
        >= 900 => NavigationMode.Desktop,
        >= 600 => NavigationMode.Tablet,
        _ => NavigationMode.Phone
    };

    public static (NavigationViewPaneDisplayMode PaneDisplayMode, bool IsPaneFooterVisible, double SettingsBottomMargin) GetNavigationModeLayout(NavigationMode mode) => mode switch
    {
        NavigationMode.Desktop => (NavigationViewPaneDisplayMode.Left, true, 64),
        NavigationMode.Tablet => (NavigationViewPaneDisplayMode.LeftCompact, false, 0),
        _ => (NavigationViewPaneDisplayMode.LeftMinimal, false, 0)
    };

    private void MainWindow_SizeChanged(object sender, WindowSizeChangedEventArgs args)
    {
        ApplyNavigationMode(args.Size.Width);
        if (_isResizing || RootGrid?.XamlRoot == null) return;
        double scale = RootGrid.XamlRoot.RasterizationScale;
        if (args.Size.Width < 800 || args.Size.Height < 500)
        {
            var (physW, physH) = GetMinimumWindowSize(args.Size.Width, args.Size.Height, scale);
            SizeInt32 currentSize = AppWindow.Size;
            if (currentSize.Width < physW || currentSize.Height < physH)
            {
                _isResizing = true;
                try
                {
                    AppWindow.Resize(new SizeInt32(Math.Max(currentSize.Width, physW), Math.Max(currentSize.Height, physH)));
                }
                finally
                {
                    _isResizing = false;
                }
            }
        }
    }

    private void ApplyNavigationMode(double width)
    {
        NavigationMode mode = GetNavigationMode(width);
        if (_currentNavigationMode == mode) return;
        _currentNavigationMode = mode;
        if (NavView == null) return;

        var (paneDisplayMode, isFooterVisible, settingsMargin) = GetNavigationModeLayout(mode);
        NavView.PaneDisplayMode = paneDisplayMode;
        NavView.IsPaneToggleButtonVisible = mode != NavigationMode.Desktop;
        NavView.PaneFooter = isFooterVisible ? CreatePaneFooter() : null;
        if (NavView.SettingsItem is NavigationViewItem settingsItem)
            settingsItem.Margin = new Thickness(0, 0, 0, settingsMargin);
    }

    private static Grid CreatePaneFooter() => new Grid
    {
        Height = 44,
        Padding = new Thickness(12, 0, 12, 0),
        Children =
        {
            new TextBlock
            {
                Text = "WinForge v1.0.0",
                VerticalAlignment = VerticalAlignment.Center,
                Foreground = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["TextFillColorDisabledBrush"],
                FontSize = 11
            }
        }
    };

    private void RootGrid_Loaded(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement root)
        {
            root.ActualThemeChanged -= Root_ActualThemeChanged;
            root.ActualThemeChanged += Root_ActualThemeChanged;
            if (root.XamlRoot != null)
            {
                root.XamlRoot.Changed -= XamlRoot_Changed;
                root.XamlRoot.Changed += XamlRoot_Changed;
                UpdateMinWindowSize(root.XamlRoot);
            }
            UpdateThemeToggleIcon();
        }
    }

    private void RootGrid_Unloaded(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement root)
        {
            root.ActualThemeChanged -= Root_ActualThemeChanged;
            if (root.XamlRoot != null)
            {
                root.XamlRoot.Changed -= XamlRoot_Changed;
            }
        }
    }

    private void Root_ActualThemeChanged(FrameworkElement sender, object args)
    {
        UpdateThemeToggleIcon();
    }

    private void XamlRoot_Changed(XamlRoot sender, XamlRootChangedEventArgs args)
    {
        UpdateMinWindowSize(sender);
    }

    private void UpdateMinWindowSize(XamlRoot root)
    {
        double scale = root.RasterizationScale;
        if (Math.Abs(scale - _lastRasterizationScale) < 0.01) return;
        _lastRasterizationScale = scale;
    }

    public static bool IsTopLevelPage(Type pageType) => pageType == typeof(Pages.HomePage) || pageType == typeof(Pages.InstalledPage) || pageType == typeof(Pages.UpdatesPage) || pageType == typeof(Pages.SettingsPage) || pageType == typeof(Pages.AboutPage) || pageType == typeof(Pages.NoWingetPage);

    public static Visibility IsBackButtonVisible(bool isTopLevelPage, bool canGoBack) =>
        (!isTopLevelPage && canGoBack) ? Visibility.Visible : Visibility.Collapsed;

    private void NavFrame_Navigated(object sender, Microsoft.UI.Xaml.Navigation.NavigationEventArgs e)
    {
        bool isTopLevelPage = IsTopLevelPage(e.SourcePageType);
        TitleBarBackButton.Visibility = IsBackButtonVisible(isTopLevelPage, NavFrame.CanGoBack);
    }

    public void ApplyTheme(string theme)
    {
        ElementTheme elementTheme = App.ParseTheme(theme);
        if (Content is FrameworkElement root) root.RequestedTheme = elementTheme;
        UpdateThemeToggleIcon();
    }

    public static string GetNextTheme(string currentTheme, ElementTheme actualTheme) => currentTheme switch
    {
        "Dark" => "Light",
        "Light" => "Dark",
        _ => actualTheme == ElementTheme.Dark ? "Light" : "Dark"
    };

    private void ThemeToggleButton_Click(object sender, RoutedEventArgs e)
    {
        string currentTheme = SettingsService.AppTheme;
        ElementTheme actual = (Content is FrameworkElement fe) ? fe.ActualTheme : ElementTheme.Light;
        string newTheme = GetNextTheme(currentTheme, actual);
        SettingsService.AppTheme = newTheme;
        ApplyTheme(newTheme);
    }

    private void UpdateThemeToggleIcon()
    {
        if (ThemeToggleIcon == null || ThemeToggleButton == null) return;
        ElementTheme requested = Content is FrameworkElement r ? r.RequestedTheme : ElementTheme.Default;
        ElementTheme actual = Content is FrameworkElement fe ? fe.ActualTheme : ElementTheme.Light;
        ElementTheme currentActual = ResolveCurrentTheme(requested, actual);

        var (glyph, label) = GetThemeToggleData(currentActual);
        ThemeToggleIcon.Glyph = glyph;
        ToolTipService.SetToolTip(ThemeToggleButton, label);
        AutomationProperties.SetName(ThemeToggleButton, label);
    }

    public async Task RefreshUpdatesCountAsync()
    {
        try
        {
            if (WingetService.IsWingetAvailable())
            {
                var upgrades = await App.Winget.GetUpgradablePackagesAsync();
                UpdateUpdatesBadge(upgrades.Count);
            }
        }
        catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"Failed to refresh updates count: {ex.Message}"); }
    }

    public static (bool IsVisible, string BadgeText, string AutomationName) GetBadgeData(int count)
    {
        count = Math.Max(0, count);
        bool hasUpdates = count > 0;
        string badgeText = Math.Min(count, 99).ToString();
        string automationName = count switch
        {
            0 => "Updates, none available",
            1 => "Updates, 1 available",
            _ => $"Updates, {count} available"
        };
        return (hasUpdates, badgeText, automationName);
    }

    public static (string Glyph, string Label) GetThemeToggleData(ElementTheme actualTheme)
    {
        string glyph = actualTheme == ElementTheme.Dark ? "\uE706" : "\uE708";
        string label = actualTheme == ElementTheme.Dark ? "Switch to light theme" : "Switch to dark theme";
        return (glyph, label);
    }

    public static ElementTheme ResolveCurrentTheme(ElementTheme? requestedTheme, ElementTheme actualTheme)
    {
        if (requestedTheme.HasValue && requestedTheme.Value != ElementTheme.Default)
            return requestedTheme.Value;
        return actualTheme == ElementTheme.Dark ? ElementTheme.Dark : ElementTheme.Light;
    }

    public void UpdateUpdatesBadge(int count) => App.DispatcherQueue?.TryEnqueue(() =>
    {
        if (UpdatesBadgeContainer == null || UpdatesNavItem == null) return;

        var (isVisible, badgeText, automationName) = GetBadgeData(count);
        UpdatesBadgeContainer.Visibility = isVisible ? Visibility.Visible : Visibility.Collapsed;

        if (isVisible)
        {
            UpdatesBadgeText.Text = badgeText;
        }

        AutomationProperties.SetName(UpdatesNavItem, automationName);
        ToolTipService.SetToolTip(UpdatesNavItem, automationName);
    });

    private void TitleBarBackButton_Click(object sender, RoutedEventArgs e) => NavFrame.GoBack();

    private void NavView_SelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
    {
        var pageType = NavigationHelper.GetPageType(args.SelectedItem is NavigationViewItem item ? item.Tag as string : null, args.IsSettingsSelected, WingetService.IsWingetAvailable());
        if (pageType != null && NavFrame.CurrentSourcePageType != pageType)
        {
            NavFrame.Navigate(pageType);
            NavFrame.BackStack.Clear();
            NavView.IsBackButtonVisible = Microsoft.UI.Xaml.Controls.NavigationViewBackButtonVisible.Collapsed;
        }
    }
}
