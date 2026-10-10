using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace WingetStore.Services.Optimizer;

/// <summary>
/// Represents the live audit state of a single tweak in the catalog.
/// </summary>
/// <param name="Info">Static metadata describing the tweak, default state, target state, and trade-offs.</param>
/// <param name="CurrentState">Live system value or status summary.</param>
/// <param name="IsOptimized">Whether the live state matches the optimized target.</param>
public sealed record SystemAuditItem(
    TweakInfo Info,
    string CurrentState,
    bool IsOptimized);

/// <summary>
/// Complete system audit report combining hardware/OS telemetry and per-tweak state inspection.
/// </summary>
/// <param name="Hardware">Hardware and OS summary.</param>
/// <param name="Memory">Current physical memory utilization.</param>
/// <param name="Items">Evaluated tweak audit items.</param>
public sealed record SystemAuditReport(
    SystemSummary Hardware,
    MemoryInfo Memory,
    IReadOnlyList<SystemAuditItem> Items)
{
    /// <summary>Gets the count of tweaks currently in their optimized state.</summary>
    public int OptimizedCount => Items.Count(i => i.IsOptimized);

    /// <summary>Gets the total number of audited tweaks.</summary>
    public int TotalCount => Items.Count;
}

/// <summary>
/// Service that inspects the live state of all system tweaks without modifying the machine.
/// </summary>
public interface ISystemAuditorService
{
    /// <summary>
    /// Queries the hardware summary, memory status, and live state of every tweak in the catalog.
    /// </summary>
    /// <param name="progress">Optional progress sink for real-time console output.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The <see cref="SystemAuditReport"/>.</returns>
    Task<SystemAuditReport> AuditAsync(
        IProgress<string>? progress = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Formats a <see cref="SystemAuditReport"/> into a human-readable console report.
    /// </summary>
    /// <param name="report">The audit report to format.</param>
    /// <returns>Multi-line formatted report string.</returns>
    string FormatReport(SystemAuditReport report);
}

/// <summary>
/// Native C# implementation of <see cref="ISystemAuditorService"/>.
/// </summary>
public sealed class SystemAuditorService : ISystemAuditorService
{
    private readonly IRegistryAccessor _registry;
    private readonly ICommandExecutor _commands;
    private readonly IWmiAccessor _wmi;
    private readonly IMemoryOptimizerService _memoryOptimizer;
    private readonly IReadOnlyList<Tweak> _catalog;

    /// <summary>
    /// Initializes a new instance of the <see cref="SystemAuditorService"/> class.
    /// </summary>
    /// <param name="registry">Registry accessor.</param>
    /// <param name="commands">Command executor.</param>
    /// <param name="wmi">WMI accessor.</param>
    /// <param name="memoryOptimizer">Memory optimizer service.</param>
    /// <param name="catalog">Optional custom tweak catalog for testing.</param>
    public SystemAuditorService(
        IRegistryAccessor registry,
        ICommandExecutor commands,
        IWmiAccessor wmi,
        IMemoryOptimizerService memoryOptimizer,
        IReadOnlyList<Tweak>? catalog = null)
    {
        ArgumentNullException.ThrowIfNull(registry);
        ArgumentNullException.ThrowIfNull(commands);
        ArgumentNullException.ThrowIfNull(wmi);
        ArgumentNullException.ThrowIfNull(memoryOptimizer);

        _registry = registry;
        _commands = commands;
        _wmi = wmi;
        _memoryOptimizer = memoryOptimizer;
        _catalog = catalog ?? TweakCatalog.All;
    }

    /// <inheritdoc />
    public async Task<SystemAuditReport> AuditAsync(
        IProgress<string>? progress = null,
        CancellationToken cancellationToken = default)
    {
        progress?.Report("=== WinForge Deep System State Audit ===");

        var summary = _wmi.GetSystemSummary();
        var memory = _memoryOptimizer.GetCurrentMemoryStatus();
        var context = new TweakContext(_registry, _commands, _wmi, memory.TotalBytes);

        var items = new List<SystemAuditItem>(_catalog.Count);
        foreach (var tweak in _catalog)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var state = await tweak.QueryAsync(context, cancellationToken).ConfigureAwait(false);
            var item = new SystemAuditItem(tweak.Info, state.Current, state.IsOptimized);
            items.Add(item);

            string badge = state.IsOptimized ? "[OPTIMIZED]" : "[DEFAULT/CUSTOM]";
            progress?.Report($"  {badge} {tweak.Info.Title}: {state.Current}");
        }

        var report = new SystemAuditReport(summary, memory, items);
        progress?.Report($"=== Audit Complete: {report.OptimizedCount} / {report.TotalCount} tweaks optimized ===");
        return report;
    }

    /// <inheritdoc />
    public string FormatReport(SystemAuditReport report)
    {
        ArgumentNullException.ThrowIfNull(report);

        double totalGb = report.Memory.TotalBytes / (1024.0 * 1024.0 * 1024.0);
        double usedGb = report.Memory.UsedBytes / (1024.0 * 1024.0 * 1024.0);
        double loadPct = report.Memory.TotalBytes > 0 ? (double)report.Memory.UsedBytes / report.Memory.TotalBytes * 100.0 : 0.0;

        var sb = new StringBuilder();
        sb.AppendLine("======================================================================");
        sb.AppendLine("             WINFORGE DEEP SYSTEM STATE AUDIT REPORT                  ");
        sb.AppendLine("======================================================================");
        sb.AppendLine(CultureInfo.InvariantCulture, $"  OS       : {report.Hardware.OsCaption} ({report.Hardware.OsVersion})");
        sb.AppendLine(CultureInfo.InvariantCulture, $"  CPU      : {report.Hardware.CpuName} [{report.Hardware.CpuVendor}]");
        sb.AppendLine(CultureInfo.InvariantCulture, $"  RAM      : {usedGb:F2} GB used / {totalGb:F2} GB total ({loadPct:F0}% load)");
        sb.AppendLine(CultureInfo.InvariantCulture, $"  Activity : {report.Hardware.ProcessCount} processes | {report.Hardware.RunningServiceCount} running services");
        sb.AppendLine(CultureInfo.InvariantCulture, $"  Score    : {report.OptimizedCount} / {report.TotalCount} optimizations active");
        sb.AppendLine("======================================================================");

        foreach (var item in report.Items)
        {
            sb.AppendLine();
            string status = item.IsOptimized ? "OPTIMIZED" : "UNOPTIMIZED";
            sb.AppendLine(CultureInfo.InvariantCulture, $"[Phase {item.Info.Phase:D2}] {item.Info.Title} [{status}]");
            sb.AppendLine(CultureInfo.InvariantCulture, $"  Current State   : {item.CurrentState}");
            sb.AppendLine(CultureInfo.InvariantCulture, $"  Windows Default : {item.Info.DefaultState}");
            sb.AppendLine(CultureInfo.InvariantCulture, $"  Target State    : {item.Info.TargetState}");
            sb.AppendLine(CultureInfo.InvariantCulture, $"  Positive Impact : {item.Info.PositiveImpact}");
            sb.AppendLine(CultureInfo.InvariantCulture, $"  Trade-Off       : {item.Info.TradeOff}");
        }

        return sb.ToString();
    }
}
