using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Text;
using CommunityToolkit.Mvvm.Messaging;
using WingetStore.Models;

namespace WingetStore.Services;

public class WingetService(IProcessRunner processRunner) : IWingetService
{
    private static string WingetPath = ResolveWingetPath();
    private readonly IProcessRunner _processRunner = processRunner ?? throw new ArgumentNullException(nameof(processRunner));
    private readonly Dictionary<string, CancellationTokenSource> _taskCtsMap = new();
    [DebuggerNonUserCode] public WingetPackage GetOrCreatePackage(WingetPackage incoming) => incoming;
    public ObservableCollection<InstallTask> ActiveTasks { get; } = [];
    [DebuggerNonUserCode]
    private static string ResolveWingetPath()
    {
        string knownPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), @"Microsoft\WindowsApps\winget.exe");
        if (File.Exists(knownPath)) return knownPath;
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "where.exe",
                Arguments = "winget.exe",
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = Process.Start(psi);
            if (process == null) return knownPath;

            string output = process.StandardOutput.ReadToEnd().Trim();
            process.WaitForExit();
            if (process.ExitCode == 0 && !string.IsNullOrEmpty(output))
            {
                string[] paths = output.Split([Environment.NewLine], StringSplitOptions.RemoveEmptyEntries);
                if (paths.Length > 0 && File.Exists(paths[0])) return paths[0];
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"ResolveWingetPath failed: {ex.Message}");
        }
        return knownPath;
    }

    public static string EscapeArgument(string? arg)
    {
        if (string.IsNullOrEmpty(arg)) return "\"\"";
        var sb = new StringBuilder(arg.Length + 4);
        sb.Append('"');
        int backslashCount = 0;
        for (int i = 0; i < arg.Length; i++)
        {
            char c = arg[i];
            if (c == '\\')
            {
                backslashCount++;
            }
            else if (c == '"')
            {
                sb.Append('\\', backslashCount * 2 + 1);
                sb.Append('"');
                backslashCount = 0;
            }
            else
            {
                if (backslashCount > 0)
                {
                    sb.Append('\\', backslashCount);
                    backslashCount = 0;
                }
                sb.Append(c);
            }
        }
        if (backslashCount > 0)
        {
            sb.Append('\\', backslashCount * 2);
        }
        sb.Append('"');
        return sb.ToString();
    }

    public static string BuildSearchArguments(string query) => $"search {EscapeArgument(query)} --source winget --accept-source-agreements";
    public static string BuildListArguments() => "list --source winget --details --accept-source-agreements";
    public static string BuildUpgradeListArguments() => "upgrade --source winget --accept-source-agreements";
    public static string BuildShowArguments(string packageId) => $"show {EscapeArgument(packageId)} --accept-source-agreements";
    public static string BuildInstallArguments(string packageId) => $"install {EscapeArgument(packageId)} --silent --accept-package-agreements --accept-source-agreements";
    public static string BuildUpgradeArguments(string packageId) => $"upgrade {EscapeArgument(packageId)} --silent --accept-package-agreements --accept-source-agreements";
    public static string BuildUninstallArguments(string packageId) => $"uninstall {EscapeArgument(packageId)} --silent";
    public static string BuildExportArguments(string filepath) => $"export -o {EscapeArgument(filepath)} --source winget --accept-source-agreements";
    public static string BuildImportArguments(string filepath) => $"import -i {EscapeArgument(filepath)} --accept-package-agreements --accept-source-agreements";

    public static bool IsWingetAvailable() => File.Exists(WingetPath);
    public async Task<string> RunCommandAsync(string arguments, CancellationToken cancellationToken = default)
    {
        if (!IsWingetAvailable())
        {
            throw new FileNotFoundException("Winget.exe was not found. Please install Windows Package Manager.");
        }

        var output = new StringBuilder();
        await _processRunner.RunStreamAsync(
            WingetPath,
            arguments,
            line =>
            {
                if (line != null)
                {
                    output.AppendLine(line);
                }
            },
            cancellationToken);

        return output.ToString();
    }

    internal static WingetPackage MapFromRow(
        Dictionary<string, string> row,
        bool includeAvailable = false,
        PackageStatus defaultStatus = PackageStatus.Installable)
    {
        string src = row.GetValueOrDefault("Source", "");
        if (string.IsNullOrWhiteSpace(src)) src = "winget";

        string name = row.GetValueOrDefault("Name", "");
        string id = row.GetValueOrDefault("Id", "");
        string version = row.GetValueOrDefault("Version", "");
        string available = includeAvailable ? row.GetValueOrDefault("Available", "") : "";

        return new WingetPackage
        {
            Name = name,
            Id = id,
            Version = version,
            AvailableVersion = available,
            Source = src,
            Status = defaultStatus
        };
    }

    public async Task<List<WingetPackage>> SearchPackagesAsync(string query, CancellationToken cancellationToken = default)
    {
        try
        {
            string output = await RunCommandAsync(BuildSearchArguments(query), cancellationToken);
            var parsedRows = WingetParser.ParseTable(output);
            var packages = parsedRows.Select(row => MapFromRow(row)).Take(150);
            return [.. packages];
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            Debug.WriteLine($"SearchPackagesAsync failed: {ex.Message}");
            return [];
        }
    }

    public async Task<List<WingetPackage>> GetInstalledPackagesAsync()
    {
        try
        {
            string output = await RunCommandAsync(BuildListArguments());
            var detailsList = WingetParser.ParseDetailsList(output);
            if (detailsList != null && detailsList.Count > 0)
            {
                return detailsList;
            }

            var tableList = WingetParser.ParseTable(output);
            var packages = tableList.Select(row => MapFromRow(row, defaultStatus: PackageStatus.Installed));
            return [.. packages];
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"GetInstalledPackagesAsync failed: {ex.Message}");
            return [];
        }
    }

    public async Task<List<WingetPackage>> GetUpgradablePackagesAsync()
    {
        try
        {
            string output = await RunCommandAsync(BuildUpgradeListArguments());
            var tableList = WingetParser.ParseTable(output);
            var packages = tableList.Select(row => MapFromRow(row, includeAvailable: true, defaultStatus: PackageStatus.Upgradable));
            return [.. packages];
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"GetUpgradablePackagesAsync failed: {ex.Message}");
            return [];
        }
    }

    private static string GetAssetPath(string fileName) => Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", fileName);

    private static async Task<List<T>> LoadAssetListAsync<T>(string fileName)
    {
        try
        {
            string path = GetAssetPath(fileName);
            if (File.Exists(path))
            {
                string json = await File.ReadAllTextAsync(path);
                var list = System.Text.Json.JsonSerializer.Deserialize<List<T>>(json);
                if (list != null)
                {
                    return list;
                }
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"LoadAssetListAsync({fileName}) failed: {ex.Message}");
        }

        return [];
    }
    public async Task<List<WingetPackage>> GetPopularPackagesAsync() => await LoadAssetListAsync<WingetPackage>("popular_packages.json");

    internal static List<WingetPackage> BuildRecommendations(
        IEnumerable<WingetPackage>? popularPackages,
        IDictionary<string, WingetPackage>? installedMap,
        int maxCount = 10)
    {
        if (popularPackages == null) return [];
        installedMap ??= new Dictionary<string, WingetPackage>(StringComparer.OrdinalIgnoreCase);

        var result = new List<WingetPackage>();
        foreach (var p in popularPackages.Take(maxCount))
        {
            if (p == null) continue;
            string id = p.Id ?? "";
            var pkg = new WingetPackage
            {
                Id = id,
                Name = p.Name ?? "",
                Publisher = p.Publisher ?? "",
                Version = p.Version ?? "",
                Source = p.Source ?? "",
                Description = p.Description ?? ""
            };
            if (!string.IsNullOrEmpty(id) && installedMap.TryGetValue(id, out var inst))
            {
                pkg.Status = PackageStatus.Installed;
                if (!string.IsNullOrEmpty(inst.Version)) pkg.Version = inst.Version;
            }
            else
            {
                pkg.Status = PackageStatus.Installable;
            }
            result.Add(pkg);
        }
        return result;
    }

    public async Task<List<WingetPackage>> GetRecommendationsAsync()
    {
        List<WingetPackage> popular = [];
        try
        {
            popular = await GetPopularPackagesAsync() ?? [];
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"GetPopularPackagesAsync failed: {ex.Message}");
        }

        if (popular == null || popular.Count == 0) return [];

        Dictionary<string, WingetPackage> installedMap = new(StringComparer.OrdinalIgnoreCase);
        try
        {
            var installed = await GetInstalledPackagesAsync();
            if (installed != null)
            {
                foreach (var inst in installed)
                {
                    if (inst != null && !string.IsNullOrEmpty(inst.Id)) installedMap[inst.Id] = inst;
                }
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"GetInstalledPackagesAsync failed during recommendations: {ex.Message}");
        }

        return BuildRecommendations(popular, installedMap, 10);
    }
    private async Task FetchDetailsInBackground(WingetPackage pkg)
    {
        try
        {
            string output = await RunCommandAsync(BuildShowArguments(pkg.Id));
            var parsed = WingetParser.ParsePackageDetails(output, pkg.Id);
            var tags = WingetParser.ParseTagsFromShowOutput(output);

            App.Dispatch(() =>
            {
                pkg.Description = parsed.Description;
                pkg.Homepage = parsed.Homepage;
                pkg.Publisher = parsed.Publisher;
                pkg.License = parsed.License;
                pkg.Tags = tags;
            });
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"FetchDetailsInBackground failed for {pkg.Id}: {ex.Message}");
        }
    }

    public async Task<List<CategoryItem>> GetCategoriesAsync() => await LoadAssetListAsync<CategoryItem>("categories.json");

    public async Task<WingetPackage?> GetPackageDetailsAsync(PackageId packageId)
    {
        try
        {
            string output = await RunCommandAsync(BuildShowArguments(packageId));
            var parsed = WingetParser.ParsePackageDetails(output, packageId);
            var tags = WingetParser.ParseTagsFromShowOutput(output);
            string name = string.IsNullOrEmpty(parsed.Name) ? packageId : parsed.Name;

            return new WingetPackage
            {
                Id = packageId,
                Name = name,
                Publisher = parsed.Publisher,
                Description = parsed.Description,
                Homepage = parsed.Homepage,
                License = parsed.License,
                Version = parsed.Version,
                InstallerType = parsed.InstallerType,
                Tags = tags
            };
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"GetPackageDetailsAsync failed: {ex.Message}");
            return null;
        }
    }

    internal static WingetPackage DecoratePackageDetails(
        WingetPackage? details,
        string packageId,
        IEnumerable<WingetPackage>? installedPackages,
        IEnumerable<WingetPackage>? upgradablePackages)
    {
        var pkg = details ?? new WingetPackage { Id = packageId, Name = packageId };
        var installed = installedPackages ?? Enumerable.Empty<WingetPackage>();
        var upgradable = upgradablePackages ?? Enumerable.Empty<WingetPackage>();

        var upg = upgradable.FirstOrDefault(p => p != null && string.Equals(p.Id, packageId, StringComparison.OrdinalIgnoreCase));
        if (upg != null)
        {
            pkg.Status = PackageStatus.Upgradable;
            if (!string.IsNullOrEmpty(upg.Version)) pkg.Version = upg.Version;
            if (!string.IsNullOrEmpty(upg.AvailableVersion)) pkg.AvailableVersion = upg.AvailableVersion;
        }
        else
        {
            var inst = installed.FirstOrDefault(p => p != null && string.Equals(p.Id, packageId, StringComparison.OrdinalIgnoreCase));
            if (inst != null)
            {
                pkg.Status = PackageStatus.Installed;
                if (!string.IsNullOrEmpty(inst.Version)) pkg.Version = inst.Version;
            }
            else
            {
                pkg.Status = PackageStatus.Installable;
            }
        }
        return pkg;
    }

    public async Task<WingetPackage> FetchAndDecoratePackageDetailsAsync(PackageId packageId)
    {
        var detailsTask = GetPackageDetailsAsync(packageId);
        var installedTask = GetInstalledPackagesAsync();
        var upgradableTask = GetUpgradablePackagesAsync();

        await Task.WhenAll(detailsTask, installedTask, upgradableTask);

        var details = detailsTask.Result;
        var installed = installedTask.Result;
        var upgradable = upgradableTask.Result;

        return DecoratePackageDetails(details, packageId, installed, upgradable);
    }

    public enum PackageActionKind { None, Cancel, Uninstall, Upgrade, Install }

    internal static PackageActionKind DeterminePackageAction(WingetPackage? package)
    {
        if (package == null) return PackageActionKind.None;
        if (package.IsInstalling) return PackageActionKind.Cancel;
        if (package.Status == PackageStatus.Installed) return PackageActionKind.Uninstall;
        if (package.Status == PackageStatus.Upgradable) return PackageActionKind.Upgrade;
        return PackageActionKind.Install;
    }

    public void TriggerPackageAction(WingetPackage package)
    {
        var action = DeterminePackageAction(package);
        switch (action)
        {
            case PackageActionKind.Cancel:
                CancelTaskForPackage(package.Id);
                break;
            case PackageActionKind.Uninstall:
                UninstallPackage(package);
                break;
            case PackageActionKind.Upgrade:
                UpgradePackage(package);
                break;
            case PackageActionKind.Install:
                InstallPackage(package);
                break;
        }
    }
    public void InstallPackage(WingetPackage package) => _ = RunTaskAsync(package, TaskOperation.Install, BuildInstallArguments(package.Id));
    public void UpgradePackage(WingetPackage package) => _ = RunTaskAsync(package, TaskOperation.Upgrade, BuildUpgradeArguments(package.Id));
    public void UninstallPackage(WingetPackage package) => _ = RunTaskAsync(package, TaskOperation.Uninstall, BuildUninstallArguments(package.Id));

    public void CancelTask(string taskId)
    {
        if (string.IsNullOrEmpty(taskId)) return;
        lock (_taskCtsMap)
        {
            if (_taskCtsMap.TryGetValue(taskId, out var cts))
            {
                try { cts.Cancel(); } catch (Exception ex) { Debug.WriteLine($"Failed to cancel task {taskId}: {ex.Message}"); }
            }
        }
    }

    public void CancelTaskForPackage(string packageId)
    {
        if (string.IsNullOrEmpty(packageId)) return;
        var task = ActiveTasks.LastOrDefault(t =>
            t.PackageId.Equals(packageId, StringComparison.OrdinalIgnoreCase) &&
            (t.Status == InstallTaskStatus.Running || t.Status == InstallTaskStatus.Queued));
        if (task != null) CancelTask(task.Id);
    }

    private async Task RunTaskAsync(WingetPackage package, TaskOperation op, string arguments)
    {
        var cts = new CancellationTokenSource();
        var task = new InstallTask
        {
            PackageId = package.Id,
            PackageName = package.Name,
            Operation = op,
            Status = InstallTaskStatus.Running,
            StatusText = "Initializing...",
            CancellationTokenSource = cts
        };

        lock (_taskCtsMap)
        {
            _taskCtsMap[task.Id] = cts;
        }

        App.Dispatch(() =>
        {
            ActiveTasks.Add(task);
            package.IsInstalling = true;
            package.InstallStatusText = "Initializing...";
            package.InstallProgress = 5;
        });

        try
        {
            var outputLog = new StringBuilder();
            int exitCode = await _processRunner.RunStreamAsync(
                WingetPath,
                arguments,
                line =>
                {
                    if (line == null) return;

                    outputLog.AppendLine(line);
                    double progress = WingetParser.ParseProgressFromOutput(line);
                    string statusText = WingetParser.ParseStatusTextFromOutput(line);

                    App.Dispatch(() =>
                    {
                        task.LogOutput = outputLog.ToString();
                        if (progress > 0)
                        {
                            task.Progress = progress;
                            package.InstallProgress = progress;
                        }
                        if (!string.IsNullOrEmpty(statusText))
                        {
                            task.StatusText = statusText;
                            package.InstallStatusText = statusText;
                        }
                    });
                },
                cts.Token);

            App.Dispatch(() =>
            {
                package.IsInstalling = false;
                if (exitCode == 0)
                {
                    task.Status = InstallTaskStatus.Completed;
                    task.StatusText = op == TaskOperation.Uninstall ? "Uninstalled" : "Installed";
                    task.Progress = 100;
                    package.InstallProgress = 100;
                    package.InstallStatusText = task.StatusText;
                    package.Status = op == TaskOperation.Uninstall ? PackageStatus.Installable : PackageStatus.Installed;
                }
                else
                {
                    task.Status = InstallTaskStatus.Failed;
                    task.StatusText = $"Failed (Exit code: {exitCode})";
                    package.InstallStatusText = task.StatusText;
                }

                WeakReferenceMessenger.Default.Send(new PackageStatusChangedMessage(package));
            });
        }
        catch (OperationCanceledException)
        {
            App.Dispatch(() =>
            {
                package.IsInstalling = false;
                task.Status = InstallTaskStatus.Cancelled;
                task.StatusText = "Canceled";
                package.InstallStatusText = "Canceled";
                WeakReferenceMessenger.Default.Send(new PackageStatusChangedMessage(package));
            });
        }
        catch (Exception ex)
        {
            App.Dispatch(() =>
            {
                package.IsInstalling = false;
                task.Status = InstallTaskStatus.Failed;
                task.StatusText = $"Error: {ex.Message}";
                package.InstallStatusText = task.StatusText;
                WeakReferenceMessenger.Default.Send(new PackageStatusChangedMessage(package));
            });
        }
        finally
        {
            lock (_taskCtsMap)
            {
                _taskCtsMap.Remove(task.Id);
            }
            cts.Dispose();
        }
    }

    public async Task<string> ExportPackagesAsync(string filepath) => await RunCommandAsync(BuildExportArguments(filepath));

    public async Task<string> ImportPackagesAsync(string filepath) => await RunCommandAsync(BuildImportArguments(filepath));
}

