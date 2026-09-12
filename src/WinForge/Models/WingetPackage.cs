using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;

namespace WingetStore.Models;

public partial class WingetPackage : INotifyPropertyChanged
{
    [GeneratedRegex(@"(?:\s+v?(?:ersion\s+)?\d+(?:\.\d+)+(?:\+[0-9a-zA-Z]+)?|\s+\d+\.\d+(?:\.\d+)*|\s*\((?:x64|x86|x86_64|arm64|win32|win64|32-bit|64-bit)\)|\s+(?:x64|x86|x86_64|arm64|32-bit|64-bit)\b)", RegexOptions.IgnoreCase | RegexOptions.Compiled)]
    private static partial Regex VersionInTitleRegex { get; }

    private string _id = "", _name = "", _version = "", _availableVersion = "", _source = "", _publisher = "", _description = "", _homepage = "", _license = "", _installerType = "", _installerUrl = "", _publisherUrl = "", _releaseNotes = "", _installStatusText = "";
    private PackageStatus _status = PackageStatus.Installable;
    private bool _isInstalling;
    private double _installProgress;
    public string Id { get => _id; set { _id = value; OnPropertyChanged(); } }
    public string Name { get => _name; set { _name = value; OnPropertyChanged(); OnPropertyChanged(nameof(DisplayTitle)); } }
    public string DisplayTitle
    {
        get
        {
            if (string.IsNullOrWhiteSpace(Name)) return Id;
            string cleaned = VersionInTitleRegex.Replace(Name, "").Trim();
            return string.IsNullOrWhiteSpace(cleaned) ? Name : cleaned;
        }
    }
    public string Version { get => _version; set { _version = value; OnPropertyChanged(); } }
    public string AvailableVersion { get => _availableVersion; set { _availableVersion = value; OnPropertyChanged(); } }
    public string Source { get => _source; set { _source = value; OnPropertyChanged(); } }
    public string Publisher
    {
        get
        {
            if (string.IsNullOrWhiteSpace(_publisher) || string.Equals(_publisher, "Installed", StringComparison.OrdinalIgnoreCase) || string.Equals(_publisher, "winget", StringComparison.OrdinalIgnoreCase))
            {
                if (!string.IsNullOrEmpty(Id) && Id.Contains('.'))
                {
                    var parts = Id.Split('.', StringSplitOptions.RemoveEmptyEntries);
                    if (parts.Length > 0 && !string.IsNullOrWhiteSpace(parts[0])) return parts[0];
                }
                if (!string.IsNullOrWhiteSpace(Name))
                {
                    var nameParts = Name.Split([' ', '-'], StringSplitOptions.RemoveEmptyEntries);
                    if (nameParts.Length > 0 && !string.IsNullOrWhiteSpace(nameParts[0])) return nameParts[0];
                }
                return !string.IsNullOrEmpty(Id) ? Id : "Winget Package";
            }
            return _publisher;
        }
        set { _publisher = value ?? ""; OnPropertyChanged(); }
    }
    public string Description { get => _description; set { _description = value; OnPropertyChanged(); } }
    public string Homepage { get => _homepage; set { _homepage = value; OnPropertyChanged(); } }
    public string License { get => _license; set { _license = value; OnPropertyChanged(); } }
    public string InstallerType { get => _installerType; set { _installerType = value; OnPropertyChanged(); } }
    public string InstallerUrl { get => _installerUrl; set { _installerUrl = value; OnPropertyChanged(); } }
    public string PublisherUrl { get => _publisherUrl; set { _publisherUrl = value; OnPropertyChanged(); } }
    public string ReleaseNotes { get => _releaseNotes; set { _releaseNotes = value; OnPropertyChanged(); } }
    public PackageStatus Status { get => _status; set { _status = value; OnPropertyChanged(); OnPropertyChanged(nameof(ShowInstallOrUpdateButton)); OnPropertyChanged(nameof(ShowUninstallButton)); OnPropertyChanged(nameof(ActionButtonLabel)); } }
    public bool ShowInstallOrUpdateButton => IsNotInstalling && Status != PackageStatus.Installed;
    public bool ShowUninstallButton => IsNotInstalling && Status == PackageStatus.Installed;

    public bool IsRedistributable
    {
        get
        {
            string name = Name ?? "";
            string id = Id ?? "";
            return name.Contains("Redistributable", StringComparison.OrdinalIgnoreCase)
                || name.Contains("Runtime", StringComparison.OrdinalIgnoreCase)
                || name.Contains("Visual C++", StringComparison.OrdinalIgnoreCase)
                || name.Contains("VCRedist", StringComparison.OrdinalIgnoreCase)
                || name.Contains(".NET", StringComparison.OrdinalIgnoreCase)
                || name.Contains("WebView2", StringComparison.OrdinalIgnoreCase)
                || name.Contains("DirectX", StringComparison.OrdinalIgnoreCase)
                || name.Contains("Software Development Kit", StringComparison.OrdinalIgnoreCase)
                || name.EndsWith(" SDK", StringComparison.OrdinalIgnoreCase)
                || id.Contains("Redistributable", StringComparison.OrdinalIgnoreCase)
                || id.Contains("Runtime", StringComparison.OrdinalIgnoreCase)
                || id.Contains("VCRedist", StringComparison.OrdinalIgnoreCase)
                || id.Contains("DotNet", StringComparison.OrdinalIgnoreCase);
        }
    }
    public bool IsInstalling { get => _isInstalling; set { _isInstalling = value; OnPropertyChanged(); OnPropertyChanged(nameof(IsNotInstalling)); OnPropertyChanged(nameof(PrimaryActionButtonText)); OnPropertyChanged(nameof(ShowInstallOrUpdateButton)); OnPropertyChanged(nameof(ShowUninstallButton)); } }
    public bool IsNotInstalling => !_isInstalling;
    public double InstallProgress { get => _installProgress; set { _installProgress = value; OnPropertyChanged(); } }
    public string InstallStatusText { get => _installStatusText; set { _installStatusText = value; OnPropertyChanged(); } }
    private string _iconUrl = "";
    public string IconUrl { get { if (string.IsNullOrEmpty(_iconUrl)) _iconUrl = WingetStore.Services.IconService.Instance.GetIconUrl(Id, Name); return _iconUrl; } set { _iconUrl = value; OnPropertyChanged(); OnPropertyChanged(nameof(HasIcon)); } }
    public bool HasIcon => !string.IsNullOrEmpty(IconUrl);
    private List<string> _screenshots = [];
    public List<string> Screenshots { get { if (_screenshots == null || _screenshots.Count == 0) _screenshots = WingetStore.Services.IconService.Instance.GetScreenshots(Id, Name); return _screenshots; } set { _screenshots = value; OnPropertyChanged(); OnPropertyChanged(nameof(HasScreenshots)); } }
    public bool HasScreenshots => Screenshots.Count > 0;
    public void RefreshIcon() { _iconUrl = ""; OnPropertyChanged(nameof(IconUrl)); OnPropertyChanged(nameof(HasIcon)); }
    public string Initial => string.IsNullOrWhiteSpace(Name) ? "?" : Name.Trim()[..1].ToUpperInvariant();
    private Microsoft.UI.Xaml.Media.Brush? _placeholderBackground;
    public Microsoft.UI.Xaml.Media.Brush PlaceholderBackground => _placeholderBackground ??= GetPlaceholderBrushForName(Name);
    private static readonly Windows.UI.Color[] PlaceholderColors =
    [
        Windows.UI.Color.FromArgb(255, 30, 144, 255), Windows.UI.Color.FromArgb(255, 46, 139, 87), Windows.UI.Color.FromArgb(255, 138, 43, 226), Windows.UI.Color.FromArgb(255, 210, 105, 30), Windows.UI.Color.FromArgb(255, 220, 20, 60), Windows.UI.Color.FromArgb(255, 0, 128, 128), Windows.UI.Color.FromArgb(255, 218, 112, 214), Windows.UI.Color.FromArgb(255, 255, 99, 71), Windows.UI.Color.FromArgb(255, 70, 130, 180), Windows.UI.Color.FromArgb(255, 186, 85, 211)
    ];

    internal static Windows.UI.Color GetPlaceholderColorForName(string name)
    {
        if (string.IsNullOrWhiteSpace(name)) return Microsoft.UI.Colors.Gray;
        int hash = 0; foreach (char c in name) hash = c + (hash << 6) + (hash << 16) - hash;
        return PlaceholderColors[(int)Math.Abs((long)hash) % PlaceholderColors.Length];
    }

    internal static Microsoft.UI.Xaml.Media.SolidColorBrush GetPlaceholderBrushForName(string name)
    {
        try
        {
            return new Microsoft.UI.Xaml.Media.SolidColorBrush(GetPlaceholderColorForName(name));
        }
        catch (System.Exception ex) { WingetStore.Services.LogService.LogError($"Placeholder brush failed for '{name}'", ex); return new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.Gray); }
    }

    public List<string> Tags { get; set; } = [];

    public bool IsUninstallAction => Status == PackageStatus.Installed;
    public string FormattedVersionAndSource
    {
        get
        {
            string sourceStr = string.IsNullOrWhiteSpace(Source) ? "Winget" : Source;
            if (string.IsNullOrWhiteSpace(Version)) return sourceStr;
            return $"{Version}  ·  {sourceStr}";
        }
    }

    public string ActionButtonLabel => Status switch { PackageStatus.Installed => "Uninstall", PackageStatus.Upgradable => "Update", _ => "Install" };
    public bool IsInstallAction => Status != PackageStatus.Installed;

    public string PrimaryActionButtonText => IsInstalling ? "Working..." : ActionButtonLabel;
    private string _recommendationReason = "";
    public string RecommendationReason { get => _recommendationReason; set { _recommendationReason = value; OnPropertyChanged(); OnPropertyChanged(nameof(HasRecommendationReason)); } }
    public bool HasRecommendationReason => !string.IsNullOrEmpty(RecommendationReason);
    public List<MetadataItem> Details { get; set; } = [];
    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
