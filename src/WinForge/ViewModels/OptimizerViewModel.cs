using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.UI.Xaml.Controls;
using WingetStore.Services;
using WingetStore.Services.Optimizer;

namespace WingetStore.ViewModels;

/// <summary>
/// ViewModel backing <see cref="Pages.OptimizerPage"/> with 100% native C# optimization, cleanup, memory, audit, and benchmark services.
/// </summary>
public partial class OptimizerViewModel : ObservableObject
{
    private readonly IElevationService _elevationService;
    private readonly ISystemOptimizerService _optimizerService;
    private readonly IDiskCleanerService _diskCleanerService;
    private readonly IMemoryOptimizerService _memoryOptimizerService;
    private readonly ISystemAuditorService _auditorService;
    private readonly ISystemBenchmarkService _benchmarkService;
    private readonly IOptimizerConfigService _configService;
    private OptimizationConfig _currentConfig;

    /// <summary>
    /// Initializes a new instance of the <see cref="OptimizerViewModel"/> class.
    /// </summary>
    /// <param name="elevationService">Elevation check service.</param>
    /// <param name="optimizerService">25-phase optimization &amp; undo service.</param>
    /// <param name="diskCleanerService">Disk cleanup service.</param>
    /// <param name="memoryOptimizerService">Working-set memory optimizer service.</param>
    /// <param name="auditorService">Non-destructive system state auditor service.</param>
    /// <param name="benchmarkService">CPU and memory micro-benchmark service.</param>
    /// <param name="configService">JSON configuration import/export service.</param>
    public OptimizerViewModel(
        IElevationService elevationService,
        ISystemOptimizerService optimizerService,
        IDiskCleanerService diskCleanerService,
        IMemoryOptimizerService memoryOptimizerService,
        ISystemAuditorService auditorService,
        ISystemBenchmarkService benchmarkService,
        IOptimizerConfigService configService)
    {
        ArgumentNullException.ThrowIfNull(elevationService);
        ArgumentNullException.ThrowIfNull(optimizerService);
        ArgumentNullException.ThrowIfNull(diskCleanerService);
        ArgumentNullException.ThrowIfNull(memoryOptimizerService);
        ArgumentNullException.ThrowIfNull(auditorService);
        ArgumentNullException.ThrowIfNull(benchmarkService);
        ArgumentNullException.ThrowIfNull(configService);

        _elevationService = elevationService;
        _optimizerService = optimizerService;
        _diskCleanerService = diskCleanerService;
        _memoryOptimizerService = memoryOptimizerService;
        _auditorService = auditorService;
        _benchmarkService = benchmarkService;
        _configService = configService;
        _currentConfig = _configService.CreateDefault();
        IsElevated = _elevationService.IsElevated;
    }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsIdle))]
    public partial bool IsBusy { get; set; }

    /// <summary>Gets a value indicating whether no optimizer operation is currently running.</summary>
    public bool IsIdle => !IsBusy;

    [ObservableProperty]
    public partial bool IsElevated { get; set; }

    [ObservableProperty]
    public partial bool IncludeSecurityReducingTweaks { get; set; }

    [ObservableProperty]
    public partial bool CreateRestorePoint { get; set; } = true;

    [ObservableProperty]
    public partial bool DeepCleanWinSxS { get; set; }

    [ObservableProperty]
    public partial string ConsoleOutput { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string InfoBarTitle { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string InfoBarMessage { get; set; } = string.Empty;

    [ObservableProperty]
    public partial InfoBarSeverity InfoBarSeverity { get; set; } = InfoBarSeverity.Informational;

    [ObservableProperty]
    public partial bool IsInfoBarOpen { get; set; }

    [ObservableProperty]
    public partial ObservableCollection<SystemAuditItem> AuditItems { get; set; } = [];

    /// <summary>Gets the active optimization configuration.</summary>
    public OptimizationConfig CurrentConfig => BuildActiveConfig();

    /// <summary>
    /// Refreshes elevation status and surfaces an informational warning if running unprivileged.
    /// </summary>
    public void CheckElevation()
    {
        IsElevated = _elevationService.IsElevated;
        if (!IsElevated)
        {
            SetInfoBar(
                InfoBarSeverity.Warning,
                "Elevation Notice",
                "WinForge is running without administrative privileges. System optimizations and service modifications require running as Administrator.");
        }
    }

    [RelayCommand]
    public async Task RunOptimizeAsync(CancellationToken cancellationToken = default)
    {
        if (IsBusy)
        {
            return;
        }

        await ExecuteOperationAsync(
            "Windows System Optimization",
            async (progress, ct) =>
            {
                var config = BuildActiveConfig();
                var result = await _optimizerService.ApplyOptimizationsAsync(config, progress, ct).ConfigureAwait(false);
                var severity = result.FailedCount == 0 ? InfoBarSeverity.Success : InfoBarSeverity.Warning;
                SetInfoBar(
                    severity,
                    "Optimization Completed",
                    $"Applied {result.AppliedCount} tweak(s), skipped {result.SkippedCount}, failed {result.FailedCount}.");
            },
            cancellationToken).ConfigureAwait(false);
    }

    [RelayCommand]
    public async Task CleanDiskAsync(CancellationToken cancellationToken = default)
    {
        if (IsBusy)
        {
            return;
        }

        await ExecuteOperationAsync(
            "Disk & Cache Cleanup",
            async (progress, ct) =>
            {
                var options = new DiskCleanOptions(ComponentStore: DeepCleanWinSxS);
                var result = await _diskCleanerService.CleanDiskAsync(options, progress, ct).ConfigureAwait(false);
                double freedMb = result.BytesFreed / (1024.0 * 1024.0);
                SetInfoBar(
                    InfoBarSeverity.Success,
                    "Disk Cleanup Completed",
                    string.Create(CultureInfo.InvariantCulture, $"Deleted {result.FilesDeleted} file(s) ({result.EntriesSkipped} skipped), reclaiming {freedMb:F2} MB."));
            },
            cancellationToken).ConfigureAwait(false);
    }

    [RelayCommand]
    public async Task FlushMemoryAsync(CancellationToken cancellationToken = default)
    {
        if (IsBusy)
        {
            return;
        }

        await ExecuteOperationAsync(
            "Memory Working-Set Flush",
            async (progress, ct) =>
            {
                var result = await _memoryOptimizerService.FlushMemoryAsync(progress, ct).ConfigureAwait(false);
                double freedMb = result.FreedBytes / (1024.0 * 1024.0);
                SetInfoBar(
                    InfoBarSeverity.Success,
                    "Memory Flush Completed",
                    string.Create(CultureInfo.InvariantCulture, $"Trimmed working sets for {result.ProcessesTrimmed} process(es) ({result.ProcessesSkipped} skipped), freeing {freedMb:F1} MB."));
            },
            cancellationToken).ConfigureAwait(false);
    }

    [RelayCommand]
    public async Task RunAuditAsync(CancellationToken cancellationToken = default)
    {
        if (IsBusy)
        {
            return;
        }

        await ExecuteOperationAsync(
            "System Privacy & Health Audit",
            async (progress, ct) =>
            {
                var report = await _auditorService.AuditAsync(progress, ct).ConfigureAwait(false);
                string formatted = _auditorService.FormatReport(report);
                App.Dispatch(() =>
                {
                    AuditItems = new ObservableCollection<SystemAuditItem>(report.Items);
                    ConsoleOutput = formatted;
                });

                SetInfoBar(
                    InfoBarSeverity.Informational,
                    "System Audit Completed",
                    $"{report.OptimizedCount} of {report.TotalCount} system optimizations are currently active.");
            },
            cancellationToken).ConfigureAwait(false);
    }

    [RelayCommand]
    public async Task RunBenchmarkAsync(CancellationToken cancellationToken = default)
    {
        if (IsBusy)
        {
            return;
        }

        await ExecuteOperationAsync(
            "System Performance Benchmark",
            async (progress, ct) =>
            {
                var result = await _benchmarkService.RunBenchmarkAsync(progress, ct).ConfigureAwait(false);
                App.Dispatch(() => ConsoleOutput = result.FormattedReport);
                SetInfoBar(
                    InfoBarSeverity.Success,
                    "Benchmark Completed",
                    string.Create(CultureInfo.InvariantCulture, $"CPU: {result.CpuDurationMs:F1} ms ({result.CpuOpsPerSecond:N0} ops/s) | RAM: {result.MemoryThroughputMbPerSec:F0} MB/s."));
            },
            cancellationToken).ConfigureAwait(false);
    }

    [RelayCommand]
    public async Task UndoOptimizationsAsync(CancellationToken cancellationToken = default)
    {
        if (IsBusy)
        {
            return;
        }

        await ExecuteOperationAsync(
            "Restore Windows Settings",
            async (progress, ct) =>
            {
                var result = await _optimizerService.UndoOptimizationsAsync(progress, ct).ConfigureAwait(false);
                var severity = result.FailedCount == 0 ? InfoBarSeverity.Success : InfoBarSeverity.Warning;
                SetInfoBar(
                    severity,
                    "Restoration Completed",
                    $"Reverted {result.RevertedFromSnapshotCount} from snapshot, restored {result.RestoredDefaultCount} defaults ({result.FailedCount} failed).");
            },
            cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Exports the current optimization configuration to a JSON file.
    /// </summary>
    /// <param name="filePath">Target file path.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task representing the export operation.</returns>
    public async Task ExportConfigAsync(string filePath, CancellationToken cancellationToken = default)
    {
        try
        {
            var config = BuildActiveConfig();
            await _configService.ExportAsync(config, filePath, cancellationToken).ConfigureAwait(false);
            SetInfoBar(InfoBarSeverity.Success, "Configuration Exported", $"Saved optimization profile to '{filePath}'.");
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            SetInfoBar(InfoBarSeverity.Error, "Export Failed", ex.Message);
        }
    }

    /// <summary>
    /// Imports an optimization configuration from a JSON file.
    /// </summary>
    /// <param name="filePath">Source file path.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task representing the import operation.</returns>
    public async Task ImportConfigAsync(string filePath, CancellationToken cancellationToken = default)
    {
        try
        {
            var imported = await _configService.ImportAsync(filePath, cancellationToken).ConfigureAwait(false);
            _currentConfig = imported;
            App.Dispatch(() =>
            {
                IncludeSecurityReducingTweaks = imported.IncludeSecurityReducingTweaks;
                CreateRestorePoint = imported.CreateRestorePoint;
            });
            SetInfoBar(
                InfoBarSeverity.Success,
                "Configuration Imported",
                $"Loaded profile with {imported.EnabledTweakIds.Count} enabled tweak(s) from '{filePath}'.");
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            SetInfoBar(InfoBarSeverity.Error, "Import Failed", ex.Message);
        }
    }

    private OptimizationConfig BuildActiveConfig()
    {
        var enabled = new HashSet<string>(_currentConfig.EnabledTweakIds, StringComparer.OrdinalIgnoreCase);
        if (IncludeSecurityReducingTweaks)
        {
            foreach (var tweak in TweakCatalog.All)
            {
                if (tweak.Info.ReducesSecurity)
                {
                    enabled.Add(tweak.Info.Id);
                }
            }
        }

        return new OptimizationConfig(
            EnabledTweakIds: enabled,
            IncludeSecurityReducingTweaks: IncludeSecurityReducingTweaks,
            CreateRestorePoint: CreateRestorePoint,
            SchemaVersion: _currentConfig.SchemaVersion);
    }

    private async Task ExecuteOperationAsync(
        string title,
        Func<IProgress<string>, CancellationToken, Task> action,
        CancellationToken cancellationToken)
    {
        var buffer = new StringBuilder();
        buffer.AppendLine(CultureInfo.InvariantCulture, $"Starting {title}...");

        App.Dispatch(() =>
        {
            IsBusy = true;
            IsInfoBarOpen = false;
            ConsoleOutput = buffer.ToString();
        });

        var progress = new SimpleProgress(line =>
        {
            lock (buffer)
            {
                buffer.AppendLine(line);
                string text = buffer.ToString();
                App.Dispatch(() => ConsoleOutput = text);
            }
        });

        try
        {
            await action(progress, cancellationToken).ConfigureAwait(false);
        }
        catch (UnauthorizedAccessException ex)
        {
            lock (buffer)
            {
                buffer.AppendLine(CultureInfo.InvariantCulture, $"[ERROR] {ex.Message}");
                string text = buffer.ToString();
                App.Dispatch(() => ConsoleOutput = text);
            }

            SetInfoBar(InfoBarSeverity.Error, "Administrator Privileges Required", ex.Message);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            LogService.LogError($"{title} failed", ex);
            lock (buffer)
            {
                buffer.AppendLine(CultureInfo.InvariantCulture, $"[ERROR] {ex.Message}");
                string text = buffer.ToString();
                App.Dispatch(() => ConsoleOutput = text);
            }

            SetInfoBar(InfoBarSeverity.Error, "Execution Error", ex.Message);
        }
        finally
        {
            App.Dispatch(() => IsBusy = false);
        }
    }

    private void SetInfoBar(InfoBarSeverity severity, string title, string message)
    {
        App.Dispatch(() =>
        {
            InfoBarSeverity = severity;
            InfoBarTitle = title;
            InfoBarMessage = message;
            IsInfoBarOpen = true;
        });
    }

    private sealed class SimpleProgress(Action<string> callback) : IProgress<string>
    {
        public void Report(string value) => callback(value);
    }
}
