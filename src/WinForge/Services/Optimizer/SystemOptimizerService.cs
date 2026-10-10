using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;

namespace WingetStore.Services.Optimizer;

/// <summary>
/// Summary of an optimization run across the tweak catalog.
/// </summary>
/// <param name="AppliedCount">Number of tweaks applied.</param>
/// <param name="SkippedCount">Number of tweaks skipped due to configuration, CPU vendor, or security opt-in.</param>
/// <param name="FailedCount">Number of tweaks that encountered errors.</param>
/// <param name="RestorePointCreated">Whether a System Restore point was created prior to applying tweaks.</param>
/// <param name="LogLines">Chronological log messages emitted during execution.</param>
public sealed record OptimizationResult(
    int AppliedCount,
    int SkippedCount,
    int FailedCount,
    bool RestorePointCreated,
    IReadOnlyList<string> LogLines);

/// <summary>
/// Summary of an optimization undo/restore run.
/// </summary>
/// <param name="RevertedFromSnapshotCount">Number of tweaks reverted to their captured pre-optimization snapshot.</param>
/// <param name="RestoredDefaultCount">Number of tweaks restored to standard Windows defaults (when no snapshot entry existed).</param>
/// <param name="FailedCount">Number of tweaks that encountered errors during restoration.</param>
/// <param name="LogLines">Chronological log messages emitted during execution.</param>
public sealed record UndoResult(
    int RevertedFromSnapshotCount,
    int RestoredDefaultCount,
    int FailedCount,
    IReadOnlyList<string> LogLines);

/// <summary>
/// Service that orchestrates the 25-phase Windows 11 optimization suite and reversible snapshot restoration.
/// </summary>
public interface ISystemOptimizerService
{
    /// <summary>
    /// Captures pre-optimization snapshots and applies all enabled tweaks in <paramref name="config"/>.
    /// </summary>
    /// <param name="config">Optimization configuration specifying enabled tweaks and security opt-in.</param>
    /// <param name="progress">Optional progress sink for real-time console output.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The <see cref="OptimizationResult"/>.</returns>
    Task<OptimizationResult> ApplyOptimizationsAsync(
        OptimizationConfig config,
        IProgress<string>? progress = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Reverts all tweaks using the saved snapshot file if present, falling back to Windows defaults per tweak.
    /// </summary>
    /// <param name="progress">Optional progress sink for real-time console output.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The <see cref="UndoResult"/>.</returns>
    Task<UndoResult> UndoOptimizationsAsync(
        IProgress<string>? progress = null,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Native C# implementation of <see cref="ISystemOptimizerService"/>.
/// </summary>
public sealed class SystemOptimizerService : ISystemOptimizerService
{
    private static readonly JsonSerializerOptions SnapshotJsonOptions = new() { WriteIndented = true };

    private readonly IElevationService _elevationService;
    private readonly IRegistryAccessor _registry;
    private readonly ICommandExecutor _commands;
    private readonly IWmiAccessor _wmi;
    private readonly IMemoryOptimizerService _memoryOptimizer;
    private readonly string _snapshotFilePath;
    private readonly IReadOnlyList<Tweak> _catalog;

    /// <summary>
    /// Initializes a new instance of the <see cref="SystemOptimizerService"/> class.
    /// </summary>
    /// <param name="elevationService">Elevation check service.</param>
    /// <param name="registry">Registry accessor.</param>
    /// <param name="commands">Command executor.</param>
    /// <param name="wmi">WMI accessor.</param>
    /// <param name="memoryOptimizer">Memory optimizer service used to query installed RAM.</param>
    /// <param name="snapshotFilePath">Optional custom snapshot file path for testing.</param>
    /// <param name="catalog">Optional custom tweak catalog for testing.</param>
    public SystemOptimizerService(
        IElevationService elevationService,
        IRegistryAccessor registry,
        ICommandExecutor commands,
        IWmiAccessor wmi,
        IMemoryOptimizerService memoryOptimizer,
        string? snapshotFilePath = null,
        IReadOnlyList<Tweak>? catalog = null)
    {
        ArgumentNullException.ThrowIfNull(elevationService);
        ArgumentNullException.ThrowIfNull(registry);
        ArgumentNullException.ThrowIfNull(commands);
        ArgumentNullException.ThrowIfNull(wmi);
        ArgumentNullException.ThrowIfNull(memoryOptimizer);

        _elevationService = elevationService;
        _registry = registry;
        _commands = commands;
        _wmi = wmi;
        _memoryOptimizer = memoryOptimizer;
        _snapshotFilePath = string.IsNullOrWhiteSpace(snapshotFilePath) ? AppPaths.OptimizerSnapshotFile : snapshotFilePath;
        _catalog = catalog ?? TweakCatalog.All;
    }

    /// <inheritdoc />
    public async Task<OptimizationResult> ApplyOptimizationsAsync(
        OptimizationConfig config,
        IProgress<string>? progress = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(config);

        if (!_elevationService.IsElevated)
        {
            throw new UnauthorizedAccessException("Administrator privileges are required to apply system optimizations.");
        }

        var logs = new List<string>();
        void Emit(string message)
        {
            logs.Add(message);
            progress?.Report(message);
        }

        Emit("=== WinForge Native Windows 11 Optimization Engine ===");

        bool restorePointCreated = false;
        if (config.CreateRestorePoint)
        {
            Emit("[Phase 1] Creating System Restore Point ('WinForge Pre-Optimization')...");
            try
            {
                _wmi.CreateRestorePoint("WinForge Pre-Optimization");
                restorePointCreated = true;
                Emit("  [OK] System Restore point created.");
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                Emit($"  [WARN] Could not create System Restore point: {ex.Message}");
            }
        }
        else
        {
            Emit("[Phase 1] Skipping System Restore Point (disabled in configuration).");
        }

        var summary = _wmi.GetSystemSummary();
        var memory = _memoryOptimizer.GetCurrentMemoryStatus();
        var context = new TweakContext(_registry, _commands, _wmi, memory.TotalBytes);

        JsonObject rootSnapshot = await LoadSnapshotRootAsync(cancellationToken).ConfigureAwait(false);
        if (rootSnapshot["tweaks"] is not JsonObject tweaksNode)
        {
            tweaksNode = new JsonObject();
            rootSnapshot["tweaks"] = tweaksNode;
        }

        int appliedCount = 0;
        int skippedCount = 0;
        int failedCount = 0;
        bool snapshotDirty = false;

        try
        {
            foreach (var tweak in _catalog)
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (!config.EnabledTweakIds.Contains(tweak.Info.Id))
                {
                    skippedCount++;
                    Emit($"[Phase {tweak.Info.Phase}] [SKIP] {tweak.Info.Title} (disabled in config).");
                    continue;
                }

                if (tweak.Info.ReducesSecurity && !config.IncludeSecurityReducingTweaks)
                {
                    skippedCount++;
                    Emit($"[Phase {tweak.Info.Phase}] [SKIP] {tweak.Info.Title} (security-reducing tweak requires explicit opt-in).");
                    continue;
                }

                if (tweak.Info.RequiresAmdCpu && !summary.IsAmdCpu)
                {
                    skippedCount++;
                    Emit($"[Phase {tweak.Info.Phase}] [SKIP] {tweak.Info.Title} (requires AMD processor; detected '{summary.CpuVendor}').");
                    continue;
                }

                Emit($"[Phase {tweak.Info.Phase}] Applying: {tweak.Info.Title}...");
                try
                {
                    if (tweaksNode[tweak.Info.Id] is not JsonObject tweakSnapshot)
                    {
                        tweakSnapshot = await tweak.CaptureAsync(context, cancellationToken).ConfigureAwait(false);
                        tweaksNode[tweak.Info.Id] = tweakSnapshot;
                        snapshotDirty = true;
                    }

                    var result = await tweak.ApplyAsync(context, tweakSnapshot, cancellationToken).ConfigureAwait(false);
                    snapshotDirty = true;

                    if (result.Applied)
                    {
                        appliedCount++;
                        string detail = string.IsNullOrWhiteSpace(result.Message) ? string.Empty : $" ({result.Message})";
                        Emit($"  [OK] {tweak.Info.Title}{detail}");
                    }
                    else
                    {
                        skippedCount++;
                        string reason = string.IsNullOrWhiteSpace(result.Message) ? "no applicable targets" : result.Message;
                        Emit($"  [SKIP] {tweak.Info.Title}: {reason}");
                    }
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    failedCount++;
                    Emit($"  [ERROR] {tweak.Info.Title}: {ex.Message}");
                }
            }
        }
        finally
        {
            if (snapshotDirty)
            {
                rootSnapshot["updatedAtUtc"] = DateTimeOffset.UtcNow.ToString("O");
                await SaveSnapshotRootAsync(rootSnapshot, CancellationToken.None).ConfigureAwait(false);
            }
        }

        Emit($"=== Optimization Complete: {appliedCount} applied, {skippedCount} skipped, {failedCount} failed ===");
        return new OptimizationResult(appliedCount, skippedCount, failedCount, restorePointCreated, logs);
    }

    /// <inheritdoc />
    public async Task<UndoResult> UndoOptimizationsAsync(
        IProgress<string>? progress = null,
        CancellationToken cancellationToken = default)
    {
        if (!_elevationService.IsElevated)
        {
            throw new UnauthorizedAccessException("Administrator privileges are required to undo system optimizations.");
        }

        var logs = new List<string>();
        void Emit(string message)
        {
            logs.Add(message);
            progress?.Report(message);
        }

        Emit("=== WinForge System Optimization Restoration ===");

        var memory = _memoryOptimizer.GetCurrentMemoryStatus();
        var context = new TweakContext(_registry, _commands, _wmi, memory.TotalBytes);

        JsonObject rootSnapshot = await LoadSnapshotRootAsync(cancellationToken).ConfigureAwait(false);
        var tweaksNode = rootSnapshot["tweaks"] as JsonObject;

        int revertedFromSnapshot = 0;
        int restoredDefault = 0;
        int failedCount = 0;

        for (int i = _catalog.Count - 1; i >= 0; i--)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var tweak = _catalog[i];

            try
            {
                if (tweaksNode?[tweak.Info.Id] is JsonObject tweakSnapshot)
                {
                    Emit($"[Phase {tweak.Info.Phase}] Reverting from snapshot: {tweak.Info.Title}...");
                    await tweak.RevertAsync(context, tweakSnapshot, cancellationToken).ConfigureAwait(false);
                    revertedFromSnapshot++;
                    Emit($"  [OK] Reverted {tweak.Info.Title} to captured baseline.");
                }
                else
                {
                    Emit($"[Phase {tweak.Info.Phase}] Restoring Windows default: {tweak.Info.Title}...");
                    await tweak.RestoreDefaultAsync(context, cancellationToken).ConfigureAwait(false);
                    restoredDefault++;
                    Emit($"  [OK] Restored {tweak.Info.Title} to Windows default.");
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                failedCount++;
                Emit($"  [ERROR] Failed to restore {tweak.Info.Title}: {ex.Message}");
            }
        }

        if (failedCount == 0 && File.Exists(_snapshotFilePath))
        {
            try
            {
                File.Delete(_snapshotFilePath);
            }
            catch (IOException)
            {
                // Best-effort cleanup of snapshot file.
            }
        }

        Emit($"=== Restoration Complete: {revertedFromSnapshot} reverted from snapshot, {restoredDefault} restored to default, {failedCount} failed ===");
        return new UndoResult(revertedFromSnapshot, restoredDefault, failedCount, logs);
    }

    private async Task<JsonObject> LoadSnapshotRootAsync(CancellationToken cancellationToken)
    {
        if (!File.Exists(_snapshotFilePath))
        {
            return new JsonObject { ["tweaks"] = new JsonObject() };
        }

        try
        {
            string json = await File.ReadAllTextAsync(_snapshotFilePath, cancellationToken).ConfigureAwait(false);
            if (JsonNode.Parse(json) is JsonObject obj)
            {
                return obj;
            }
        }
        catch (Exception ex) when (ex is IOException or JsonException)
        {
            // Corrupted or unreadable snapshot file; start fresh.
        }

        return new JsonObject { ["tweaks"] = new JsonObject() };
    }

    private async Task SaveSnapshotRootAsync(JsonObject rootSnapshot, CancellationToken cancellationToken)
    {
        string? directory = Path.GetDirectoryName(_snapshotFilePath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        string json = rootSnapshot.ToJsonString(SnapshotJsonOptions);
        await File.WriteAllTextAsync(_snapshotFilePath, json, cancellationToken).ConfigureAwait(false);
    }
}
