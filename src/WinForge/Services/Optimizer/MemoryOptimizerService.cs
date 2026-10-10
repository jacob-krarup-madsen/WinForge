using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace WingetStore.Services.Optimizer;

/// <summary>Snapshot of physical memory, in bytes.</summary>
/// <param name="TotalBytes">Total installed physical memory.</param>
/// <param name="AvailableBytes">Physical memory currently available.</param>
public readonly record struct MemoryInfo(ulong TotalBytes, ulong AvailableBytes)
{
    /// <summary>Gets the amount of memory currently in use.</summary>
    public ulong UsedBytes => TotalBytes >= AvailableBytes ? TotalBytes - AvailableBytes : 0;
}

/// <summary>Outcome of a working-set flush.</summary>
/// <param name="Before">Memory snapshot taken before trimming.</param>
/// <param name="After">Memory snapshot taken after trimming.</param>
/// <param name="ProcessesTrimmed">Number of processes whose working set was trimmed.</param>
/// <param name="ProcessesSkipped">Number of processes that could not be trimmed (access denied, exited, ...).</param>
public sealed record MemoryOptimizationResult(MemoryInfo Before, MemoryInfo After, int ProcessesTrimmed, int ProcessesSkipped)
{
    /// <summary>Gets the bytes of RAM that became available; never negative.</summary>
    public ulong FreedBytes => After.AvailableBytes > Before.AvailableBytes ? After.AvailableBytes - Before.AvailableBytes : 0;
}

/// <summary>Trims process working sets to release cached memory back to Windows.</summary>
public interface IMemoryOptimizerService
{
    /// <summary>Queries the current physical memory state.</summary>
    /// <returns>The current memory snapshot.</returns>
    MemoryInfo GetCurrentMemoryStatus();

    /// <summary>Trims the working set of every accessible process.</summary>
    /// <param name="progress">Optional sink for human-readable status lines.</param>
    /// <param name="cancellationToken">Token that aborts the flush between processes.</param>
    /// <returns>The flush result.</returns>
    Task<MemoryOptimizationResult> FlushMemoryAsync(IProgress<string>? progress = null, CancellationToken cancellationToken = default);
}

/// <summary>Low-level memory primitives, abstracted so the service can be tested without touching real processes.</summary>
public interface IMemoryNative
{
    /// <summary>Queries physical memory totals.</summary>
    /// <returns>The current memory snapshot.</returns>
    MemoryInfo QueryMemory();

    /// <summary>Enumerates the identifiers of currently running processes.</summary>
    /// <returns>Process identifiers.</returns>
    IReadOnlyList<int> GetProcessIds();

    /// <summary>Attempts to trim the working set of one process.</summary>
    /// <param name="processId">Target process identifier.</param>
    /// <returns><see langword="true"/> when the working set was trimmed.</returns>
    bool TryTrimWorkingSet(int processId);
}

/// <summary>Default <see cref="IMemoryOptimizerService"/> implementation.</summary>
public sealed class MemoryOptimizerService : IMemoryOptimizerService
{
    private readonly IMemoryNative _native;

    /// <summary>Initializes a new instance of the <see cref="MemoryOptimizerService"/> class.</summary>
    /// <param name="native">Native memory primitives.</param>
    public MemoryOptimizerService(IMemoryNative native)
    {
        ArgumentNullException.ThrowIfNull(native);
        _native = native;
    }

    /// <inheritdoc />
    public MemoryInfo GetCurrentMemoryStatus() => _native.QueryMemory();

    /// <inheritdoc />
    public async Task<MemoryOptimizationResult> FlushMemoryAsync(IProgress<string>? progress = null, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var before = _native.QueryMemory();
        progress?.Report($"Available RAM before: {FormatGb(before.AvailableBytes)}");

        int trimmed = 0;
        int skipped = 0;
        foreach (int pid in _native.GetProcessIds())
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (_native.TryTrimWorkingSet(pid))
            {
                trimmed++;
            }
            else
            {
                skipped++;
            }

            // Yield so long flushes never starve the caller's synchronization context.
            await Task.Yield();
        }

        GC.Collect();
        GC.WaitForPendingFinalizers();

        var after = _native.QueryMemory();
        var result = new MemoryOptimizationResult(before, after, trimmed, skipped);
        progress?.Report($"Working sets trimmed: {trimmed} (skipped {skipped})");
        progress?.Report($"Available RAM after: {FormatGb(after.AvailableBytes)}");
        progress?.Report($"Freed: {FormatGb(result.FreedBytes)}");
        return result;
    }

    private static string FormatGb(ulong bytes) =>
        string.Create(System.Globalization.CultureInfo.InvariantCulture, $"{bytes / 1024d / 1024d / 1024d:0.00} GB");
}
