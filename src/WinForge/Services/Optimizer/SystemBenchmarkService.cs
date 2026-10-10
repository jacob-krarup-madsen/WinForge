using System;
using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace WingetStore.Services.Optimizer;

/// <summary>
/// Result of a CPU, memory allocation, and system responsiveness benchmark run.
/// </summary>
/// <param name="Hardware">Hardware and OS summary.</param>
/// <param name="Memory">Physical memory utilization snapshot.</param>
/// <param name="CpuDurationMs">Elapsed time in milliseconds for the CPU arithmetic and prime sieve workload.</param>
/// <param name="CpuOpsPerSecond">Throughput in operations per second for the CPU workload.</param>
/// <param name="MemoryDurationMs">Elapsed time in milliseconds for the memory page-touch workload.</param>
/// <param name="MemoryThroughputMbPerSec">Effective memory page-touch throughput in MB/s.</param>
/// <param name="FormattedReport">Human-readable benchmark report.</param>
public sealed record BenchmarkResult(
    SystemSummary Hardware,
    MemoryInfo Memory,
    double CpuDurationMs,
    double CpuOpsPerSecond,
    double MemoryDurationMs,
    double MemoryThroughputMbPerSec,
    string FormattedReport);

/// <summary>
/// Service that measures CPU single-thread responsiveness, memory page allocation throughput, and OS process/service footprint.
/// </summary>
public interface ISystemBenchmarkService
{
    /// <summary>
    /// Runs the CPU and memory micro-benchmarks asynchronously on a thread-pool worker.
    /// </summary>
    /// <param name="progress">Optional progress sink for real-time console output.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The <see cref="BenchmarkResult"/>.</returns>
    Task<BenchmarkResult> RunBenchmarkAsync(
        IProgress<string>? progress = null,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Native C# implementation of <see cref="ISystemBenchmarkService"/> ported from <c>Benchmark-System.ps1</c>.
/// </summary>
public sealed class SystemBenchmarkService : ISystemBenchmarkService
{
    private const int DefaultCpuIterations = 250_000;
    private const int DefaultMemoryBufferBytes = 100 * 1024 * 1024; // 100 MB
    private const int PageStepBytes = 4096;

    private readonly IWmiAccessor _wmi;
    private readonly IMemoryOptimizerService _memoryOptimizer;
    private readonly int _cpuIterations;
    private readonly int _memoryBufferBytes;

    /// <summary>
    /// Initializes a new instance of the <see cref="SystemBenchmarkService"/> class.
    /// </summary>
    /// <param name="wmi">WMI accessor used to query hardware/process facts.</param>
    /// <param name="memoryOptimizer">Memory optimizer service used to query RAM telemetry.</param>
    /// <param name="cpuIterations">Number of CPU benchmark iterations (configurable for fast unit testing).</param>
    /// <param name="memoryBufferBytes">Size of memory benchmark buffer in bytes (configurable for fast unit testing).</param>
    public SystemBenchmarkService(
        IWmiAccessor wmi,
        IMemoryOptimizerService memoryOptimizer,
        int cpuIterations = DefaultCpuIterations,
        int memoryBufferBytes = DefaultMemoryBufferBytes)
    {
        ArgumentNullException.ThrowIfNull(wmi);
        ArgumentNullException.ThrowIfNull(memoryOptimizer);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(cpuIterations, 0);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(memoryBufferBytes, PageStepBytes);

        _wmi = wmi;
        _memoryOptimizer = memoryOptimizer;
        _cpuIterations = cpuIterations;
        _memoryBufferBytes = memoryBufferBytes;
    }

    /// <inheritdoc />
    public Task<BenchmarkResult> RunBenchmarkAsync(
        IProgress<string>? progress = null,
        CancellationToken cancellationToken = default)
    {
        return Task.Run(
            () =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                progress?.Report("=== WinForge System Performance & Footprint Benchmark ===");

                var hardware = _wmi.GetSystemSummary();
                var memory = _memoryOptimizer.GetCurrentMemoryStatus();

                progress?.Report($"[1/3] Running CPU arithmetic & prime workload ({_cpuIterations:N0} iterations)...");
                var cpuWatch = Stopwatch.StartNew();
                long accumulator = 0;
                for (int i = 2; i <= _cpuIterations + 1; i++)
                {
                    if ((i & 4095) == 0)
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                    }

                    double root = Math.Sqrt(i);
                    accumulator += (long)(root * 17.0) ^ (i * 31L);
                }

                cpuWatch.Stop();
                GC.KeepAlive(accumulator);

                double cpuMs = Math.Max(0.01, cpuWatch.Elapsed.TotalMilliseconds);
                double cpuOpsPerSec = _cpuIterations / (cpuMs / 1000.0);

                double bufferMb = _memoryBufferBytes / (1024.0 * 1024.0);
                progress?.Report(string.Create(CultureInfo.InvariantCulture, $"[2/3] Running RAM allocation & 4 KB page-touch test ({bufferMb:F0} MB)..."));
                var memWatch = Stopwatch.StartNew();
                byte[] buffer = GC.AllocateUninitializedArray<byte>(_memoryBufferBytes);
                for (int offset = 0; offset < buffer.Length; offset += PageStepBytes)
                {
                    buffer[offset] = (byte)((offset >> 12) & 0xFF);
                }

                memWatch.Stop();
                GC.KeepAlive(buffer);

                double memMs = Math.Max(0.01, memWatch.Elapsed.TotalMilliseconds);
                double memThroughputMbPerSec = bufferMb / (memMs / 1000.0);

                progress?.Report("[3/3] Compiling system footprint report...");
                string report = FormatReport(hardware, memory, cpuMs, cpuOpsPerSec, memMs, memThroughputMbPerSec, bufferMb);
                progress?.Report(report);

                return new BenchmarkResult(
                    Hardware: hardware,
                    Memory: memory,
                    CpuDurationMs: cpuMs,
                    CpuOpsPerSecond: cpuOpsPerSec,
                    MemoryDurationMs: memMs,
                    MemoryThroughputMbPerSec: memThroughputMbPerSec,
                    FormattedReport: report);
            },
            cancellationToken);
    }

    private static string FormatReport(
        SystemSummary hardware,
        MemoryInfo memory,
        double cpuMs,
        double cpuOpsPerSec,
        double memMs,
        double memThroughputMbPerSec,
        double bufferMb)
    {
        double totalGb = memory.TotalBytes / (1024.0 * 1024.0 * 1024.0);
        double usedGb = memory.UsedBytes / (1024.0 * 1024.0 * 1024.0);
        double loadPct = memory.TotalBytes > 0 ? (double)memory.UsedBytes / memory.TotalBytes * 100.0 : 0.0;

        var sb = new StringBuilder();
        sb.AppendLine("======================================================================");
        sb.AppendLine("               WINFORGE SYSTEM PERFORMANCE BENCHMARK                  ");
        sb.AppendLine("======================================================================");
        sb.AppendLine(CultureInfo.InvariantCulture, $"  OS                : {hardware.OsCaption} ({hardware.OsVersion})");
        sb.AppendLine(CultureInfo.InvariantCulture, $"  Processor         : {hardware.CpuName}");
        sb.AppendLine(CultureInfo.InvariantCulture, $"  Running Processes : {hardware.ProcessCount}");
        sb.AppendLine(CultureInfo.InvariantCulture, $"  Running Services  : {hardware.RunningServiceCount}");
        sb.AppendLine(CultureInfo.InvariantCulture, $"  RAM Utilization   : {usedGb:F2} GB / {totalGb:F2} GB ({loadPct:F0}% load)");
        sb.AppendLine("----------------------------------------------------------------------");
        sb.AppendLine(CultureInfo.InvariantCulture, $"  CPU Compute Time  : {cpuMs:F2} ms ({cpuOpsPerSec:N0} ops/sec)");
        sb.AppendLine(CultureInfo.InvariantCulture, $"  RAM Page Touch    : {memMs:F2} ms for {bufferMb:F0} MB ({memThroughputMbPerSec:F0} MB/s)");
        sb.AppendLine("======================================================================");
        return sb.ToString();
    }
}
