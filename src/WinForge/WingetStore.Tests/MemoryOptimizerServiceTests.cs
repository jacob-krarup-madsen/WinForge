using WingetStore.Services;
using WingetStore.Services.Optimizer;
using Xunit;

namespace WingetStore.Tests;

public sealed class MemoryOptimizerServiceTests
{
    private sealed class FakeNative : IMemoryNative
    {
        private readonly Queue<MemoryInfo> _snapshots;
        private readonly HashSet<int> _trimmable;

        public FakeNative(IEnumerable<MemoryInfo> snapshots, IReadOnlyList<int> pids, IEnumerable<int> trimmable)
        {
            _snapshots = new Queue<MemoryInfo>(snapshots);
            Pids = pids;
            _trimmable = [.. trimmable];
        }

        public IReadOnlyList<int> Pids { get; }

        public List<int> TrimAttempts { get; } = [];

        public MemoryInfo QueryMemory() => _snapshots.Count > 1 ? _snapshots.Dequeue() : _snapshots.Peek();

        public IReadOnlyList<int> GetProcessIds() => Pids;

        public bool TryTrimWorkingSet(int processId)
        {
            TrimAttempts.Add(processId);
            return _trimmable.Contains(processId);
        }
    }

    private sealed class ListProgress : IProgress<string>
    {
        public List<string> Lines { get; } = [];

        public void Report(string value) => Lines.Add(value);
    }

    [Fact]
    public void Constructor_NullNative_Throws() =>
        Assert.Throws<ArgumentNullException>(() => new MemoryOptimizerService(null!));

    [Fact]
    public async Task Flush_CountsTrimmedAndSkippedAndComputesFreedBytes()
    {
        var native = new FakeNative(
            [new MemoryInfo(16_000, 4_000), new MemoryInfo(16_000, 6_500)],
            [10, 20, 30],
            [10, 30]);
        var service = new MemoryOptimizerService(native);

        var result = await service.FlushMemoryAsync(cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal(2, result.ProcessesTrimmed);
        Assert.Equal(1, result.ProcessesSkipped);
        Assert.Equal(2_500UL, result.FreedBytes);
        Assert.Equal([10, 20, 30], native.TrimAttempts);
    }

    [Fact]
    public async Task Flush_AvailableMemoryShrinks_FreedBytesNeverNegative()
    {
        var native = new FakeNative([new MemoryInfo(16_000, 6_000), new MemoryInfo(16_000, 5_000)], [10], [10]);

        var result = await new MemoryOptimizerService(native).FlushMemoryAsync(cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal(0UL, result.FreedBytes);
    }

    [Fact]
    public async Task Flush_NoProcesses_ReturnsZeroCounts()
    {
        var native = new FakeNative([new MemoryInfo(8_000, 4_000)], [], []);

        var result = await new MemoryOptimizerService(native).FlushMemoryAsync(cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal(0, result.ProcessesTrimmed);
        Assert.Equal(0, result.ProcessesSkipped);
    }

    [Fact]
    public async Task Flush_ReportsProgressLines()
    {
        var native = new FakeNative([new MemoryInfo(8_000, 4_000)], [10], [10]);
        var progress = new ListProgress();

        await new MemoryOptimizerService(native).FlushMemoryAsync(progress, TestContext.Current.CancellationToken);

        Assert.Contains(progress.Lines, l => l.StartsWith("Working sets trimmed: 1", StringComparison.Ordinal));
        Assert.Contains(progress.Lines, l => l.StartsWith("Freed:", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Flush_PreCancelled_ThrowsWithoutTrimming()
    {
        var native = new FakeNative([new MemoryInfo(8_000, 4_000)], [10], [10]);
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => new MemoryOptimizerService(native).FlushMemoryAsync(cancellationToken: cts.Token));

        Assert.Empty(native.TrimAttempts);
    }

    [Fact]
    public async Task Flush_CancelledMidway_StopsEarly()
    {
        var native = new FakeNative([new MemoryInfo(8_000, 4_000)], [1, 2, 3, 4, 5], [1, 2, 3, 4, 5]);
        using var cts = new CancellationTokenSource();
        var progressNative = new CancelAfterFirstTrim(native, cts);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => new MemoryOptimizerService(progressNative).FlushMemoryAsync(cancellationToken: cts.Token));

        Assert.Single(native.TrimAttempts);
    }

    [Fact]
    public void MemoryInfo_UsedBytes_ClampsWhenAvailableExceedsTotal() =>
        Assert.Equal(0UL, new MemoryInfo(100, 200).UsedBytes);

    [Fact]
    public void GetCurrentMemoryStatus_DelegatesToNative()
    {
        var native = new FakeNative([new MemoryInfo(8_000, 3_000)], [], []);

        var info = new MemoryOptimizerService(native).GetCurrentMemoryStatus();

        Assert.Equal(5_000UL, info.UsedBytes);
    }

    [Fact]
    public void WindowsMemoryNative_QueryMemory_ReportsPlausibleValues()
    {
        var info = new WindowsMemoryNative().QueryMemory();

        Assert.True(info.TotalBytes > 0);
        Assert.True(info.AvailableBytes <= info.TotalBytes);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(4)]
    [InlineData(-1)]
    public void WindowsMemoryNative_ReservedPids_AreNotTrimmed(int pid) =>
        Assert.False(new WindowsMemoryNative().TryTrimWorkingSet(pid));

    private sealed class CancelAfterFirstTrim : IMemoryNative
    {
        private readonly FakeNative _inner;
        private readonly CancellationTokenSource _cts;

        public CancelAfterFirstTrim(FakeNative inner, CancellationTokenSource cts)
        {
            _inner = inner;
            _cts = cts;
        }

        public MemoryInfo QueryMemory() => _inner.QueryMemory();

        public IReadOnlyList<int> GetProcessIds() => _inner.GetProcessIds();

        public bool TryTrimWorkingSet(int processId)
        {
            bool ok = _inner.TryTrimWorkingSet(processId);
            _cts.Cancel();
            return ok;
        }
    }
}
