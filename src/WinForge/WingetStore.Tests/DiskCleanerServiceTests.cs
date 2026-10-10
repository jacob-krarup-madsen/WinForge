using WingetStore.Services;
using WingetStore.Services.Optimizer;
using Xunit;

namespace WingetStore.Tests;

public sealed class DiskCleanerServiceTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "WinForgeDiskTests_" + Guid.NewGuid().ToString("N"));

    public DiskCleanerServiceTests() => Directory.CreateDirectory(_root);

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            foreach (var f in Directory.EnumerateFiles(_root, "*", SearchOption.AllDirectories))
            {
                File.SetAttributes(f, FileAttributes.Normal);
            }

            Directory.Delete(_root, true);
        }
    }

    private sealed class FakeRunner : IProcessRunner
    {
        public int ExitCode { get; set; }

        public List<(string File, string Args)> Calls { get; } = [];

        public Task<int> RunStreamAsync(string fileName, string arguments, Action<string> onLineReceived, CancellationToken cancellationToken = default)
        {
            Calls.Add((fileName, arguments));
            onLineReceived("dism output");
            return Task.FromResult(ExitCode);
        }
    }

    private sealed class FakeElevation(bool elevated) : IElevationService
    {
        public bool IsElevated { get; } = elevated;
    }

    private sealed class ListProgress : IProgress<string>
    {
        public List<string> Lines { get; } = [];

        public void Report(string value) => Lines.Add(value);
    }

    private string Dir(string name)
    {
        var path = Path.Combine(_root, name);
        Directory.CreateDirectory(path);
        return path;
    }

    private static DiskCleanTargets Targets(string temp, string dumpDir, string dumpFile, string log) =>
        new([temp], [dumpDir], [dumpFile], [log]);

    private static DiskCleanOptions FilesOnly => new(ComponentStore: false);

    [Fact]
    public async Task Clean_DeletesNestedFilesAndFolders_AndCountsBytes()
    {
        var temp = Dir("temp");
        Directory.CreateDirectory(Path.Combine(temp, "a", "b"));
        File.WriteAllBytes(Path.Combine(temp, "one.tmp"), new byte[100]);
        File.WriteAllBytes(Path.Combine(temp, "a", "b", "two.tmp"), new byte[50]);
        var svc = new DiskCleanerService(new FakeRunner(), new FakeElevation(false), Targets(temp, Dir("d"), Path.Combine(_root, "none.dmp"), Dir("l")));

        var result = await svc.CleanDiskAsync(FilesOnly, cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal(2, result.FilesDeleted);
        Assert.Equal(150, result.BytesFreed);
        Assert.Equal(0, result.EntriesSkipped);
        Assert.Empty(Directory.EnumerateFileSystemEntries(temp));
        Assert.True(Directory.Exists(temp));
    }

    [Fact]
    public async Task Clean_DeletesReadOnlyFiles()
    {
        var temp = Dir("temp");
        var file = Path.Combine(temp, "ro.tmp");
        File.WriteAllText(file, "x");
        File.SetAttributes(file, FileAttributes.ReadOnly);
        var svc = new DiskCleanerService(new FakeRunner(), new FakeElevation(false), Targets(temp, Dir("d"), Path.Combine(_root, "none.dmp"), Dir("l")));

        var result = await svc.CleanDiskAsync(FilesOnly, cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal(1, result.FilesDeleted);
        Assert.False(File.Exists(file));
    }

    [Fact]
    public async Task Clean_LockedFile_IsSkippedNotFatal()
    {
        var temp = Dir("temp");
        var locked = Path.Combine(temp, "locked.tmp");
        File.WriteAllText(locked, "x");
        File.WriteAllText(Path.Combine(temp, "free.tmp"), "y");
        var svc = new DiskCleanerService(new FakeRunner(), new FakeElevation(false), Targets(temp, Dir("d"), Path.Combine(_root, "none.dmp"), Dir("l")));

        DiskCleanResult result;
        using (new FileStream(locked, FileMode.Open, FileAccess.Read, FileShare.None))
        {
            result = await svc.CleanDiskAsync(FilesOnly, cancellationToken: TestContext.Current.CancellationToken);
        }

        Assert.Equal(1, result.FilesDeleted);
        Assert.True(result.EntriesSkipped >= 1);
        Assert.True(File.Exists(locked));
    }

    [Fact]
    public async Task Clean_MissingDirectories_AreIgnored()
    {
        var svc = new DiskCleanerService(
            new FakeRunner(),
            new FakeElevation(false),
            Targets(Path.Combine(_root, "nope1"), Path.Combine(_root, "nope2"), Path.Combine(_root, "nope.dmp"), Path.Combine(_root, "nope3")));

        var result = await svc.CleanDiskAsync(FilesOnly, cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal(0, result.FilesDeleted);
        Assert.Equal(0, result.EntriesSkipped);
    }

    [Fact]
    public async Task Clean_DoesNotFollowDirectoryJunctions()
    {
        var temp = Dir("temp");
        var outside = Dir("outside");
        var outsideFile = Path.Combine(outside, "keep.txt");
        File.WriteAllText(outsideFile, "keep");
        var link = Path.Combine(temp, "link");
        CreateJunction(link, outside);
        var svc = new DiskCleanerService(new FakeRunner(), new FakeElevation(false), Targets(temp, Dir("d"), Path.Combine(_root, "none.dmp"), Dir("l")));

        var result = await svc.CleanDiskAsync(FilesOnly, cancellationToken: TestContext.Current.CancellationToken);

        Assert.True(File.Exists(outsideFile));
        Assert.True(result.EntriesSkipped >= 1);
        Directory.Delete(link);
    }

    private static void CreateJunction(string link, string target)
    {
        using var process = System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
        {
            FileName = "cmd.exe",
            Arguments = $"/c mklink /J \"{link}\" \"{target}\"",
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
        })!;
        process.StandardOutput.ReadToEnd();
        process.WaitForExit();
        Assert.Equal(0, process.ExitCode);
    }

    [Fact]
    public async Task Clean_StageFlags_AreRespected()
    {
        var temp = Dir("temp");
        var dumps = Dir("dumps");
        var logs = Dir("logs");
        File.WriteAllText(Path.Combine(temp, "t.tmp"), "t");
        File.WriteAllText(Path.Combine(dumps, "d.dmp"), "d");
        File.WriteAllText(Path.Combine(logs, "l.log"), "l");
        var dumpFile = Path.Combine(_root, "MEMORY.DMP");
        File.WriteAllText(dumpFile, "m");
        var svc = new DiskCleanerService(new FakeRunner(), new FakeElevation(false), Targets(temp, dumps, dumpFile, logs));

        await svc.CleanDiskAsync(new DiskCleanOptions(false, TempFiles: false, CrashDumps: true, Logs: false), cancellationToken: TestContext.Current.CancellationToken);

        Assert.True(File.Exists(Path.Combine(temp, "t.tmp")));
        Assert.False(File.Exists(Path.Combine(dumps, "d.dmp")));
        Assert.False(File.Exists(dumpFile));
        Assert.True(File.Exists(Path.Combine(logs, "l.log")));
    }

    [Fact]
    public async Task Clean_ComponentStore_NotElevated_ThrowsAndRunsNothing()
    {
        var runner = new FakeRunner();
        var svc = new DiskCleanerService(runner, new FakeElevation(false), Targets(Dir("t"), Dir("d"), Path.Combine(_root, "n.dmp"), Dir("l")));

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => svc.CleanDiskAsync(new DiskCleanOptions(), cancellationToken: TestContext.Current.CancellationToken));

        Assert.Empty(runner.Calls);
    }

    [Fact]
    public async Task Clean_ComponentStore_Elevated_InvokesDismAndReportsExitCode()
    {
        var runner = new FakeRunner { ExitCode = 5 };
        var progress = new ListProgress();
        var svc = new DiskCleanerService(runner, new FakeElevation(true), Targets(Dir("t"), Dir("d"), Path.Combine(_root, "n.dmp"), Dir("l")));

        var result = await svc.CleanDiskAsync(new DiskCleanOptions(TempFiles: false, CrashDumps: false, Logs: false), progress, TestContext.Current.CancellationToken);

        var call = Assert.Single(runner.Calls);
        Assert.Equal("dism.exe", call.File);
        Assert.Contains("/StartComponentCleanup /ResetBase", call.Args, StringComparison.Ordinal);
        Assert.Equal(5, result.DismExitCode);
        Assert.True(result.DismFailed);
        Assert.Contains("dism output", progress.Lines);
    }

    [Fact]
    public async Task Clean_DismDisabled_LeavesExitCodeNull()
    {
        var svc = new DiskCleanerService(new FakeRunner(), new FakeElevation(false), Targets(Dir("t"), Dir("d"), Path.Combine(_root, "n.dmp"), Dir("l")));

        var result = await svc.CleanDiskAsync(FilesOnly, cancellationToken: TestContext.Current.CancellationToken);

        Assert.Null(result.DismExitCode);
        Assert.False(result.DismFailed);
    }

    [Fact]
    public async Task Clean_PreCancelled_Throws()
    {
        var svc = new DiskCleanerService(new FakeRunner(), new FakeElevation(true), Targets(Dir("t"), Dir("d"), Path.Combine(_root, "n.dmp"), Dir("l")));
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => svc.CleanDiskAsync(FilesOnly, cancellationToken: cts.Token));
    }

    [Fact]
    public async Task Clean_NullOptions_Throws()
    {
        var svc = new DiskCleanerService(new FakeRunner(), new FakeElevation(true), Targets(Dir("t"), Dir("d"), Path.Combine(_root, "n.dmp"), Dir("l")));

        await Assert.ThrowsAsync<ArgumentNullException>(() => svc.CleanDiskAsync(null!, cancellationToken: TestContext.Current.CancellationToken));
    }

    [Fact]
    public void Constructor_NullDependencies_Throw()
    {
        var targets = Targets(Dir("t"), Dir("d"), Path.Combine(_root, "n.dmp"), Dir("l"));
        Assert.Throws<ArgumentNullException>(() => new DiskCleanerService(null!, new FakeElevation(true), targets));
        Assert.Throws<ArgumentNullException>(() => new DiskCleanerService(new FakeRunner(), null!, targets));
        Assert.Throws<ArgumentNullException>(() => new DiskCleanerService(new FakeRunner(), new FakeElevation(true), null!));
    }

    [Fact]
    public void DefaultTargets_ResolveFromKnownFolders_NotHardcodedDrive()
    {
        var targets = DiskCleanTargets.CreateDefault();

        Assert.All(targets.TempDirectories.Concat(targets.DumpDirectories).Concat(targets.LogDirectories), p => Assert.True(Path.IsPathRooted(p)));
        Assert.Contains(targets.DumpFiles, f => f.EndsWith("MEMORY.DMP", StringComparison.OrdinalIgnoreCase));
    }
}
