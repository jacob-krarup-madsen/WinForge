using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace WingetStore.Services.Optimizer;

/// <summary>Selects which cleanup stages <see cref="IDiskCleanerService"/> runs.</summary>
/// <param name="ComponentStore">Run DISM component store cleanup with ResetBase (requires elevation).</param>
/// <param name="TempFiles">Purge system and user temporary folders.</param>
/// <param name="CrashDumps">Purge memory dumps and Windows Error Reporting archives.</param>
/// <param name="Logs">Purge Windows log files.</param>
public sealed record DiskCleanOptions(bool ComponentStore = true, bool TempFiles = true, bool CrashDumps = true, bool Logs = true);

/// <summary>Outcome of a disk cleanup run.</summary>
/// <param name="BytesFreed">Total size of deleted files.</param>
/// <param name="FilesDeleted">Number of files deleted.</param>
/// <param name="EntriesSkipped">Number of files or folders that could not be removed (in use, access denied).</param>
/// <param name="DismExitCode">DISM exit code, or <see langword="null"/> when DISM was not run.</param>
public sealed record DiskCleanResult(long BytesFreed, int FilesDeleted, int EntriesSkipped, int? DismExitCode)
{
    /// <summary>Gets a value indicating whether DISM was run and failed.</summary>
    public bool DismFailed => DismExitCode is { } code && code != 0;
}

/// <summary>Cleans temporary files, dumps, logs and the component store.</summary>
public interface IDiskCleanerService
{
    /// <summary>Runs the selected cleanup stages.</summary>
    /// <param name="options">Stages to run.</param>
    /// <param name="progress">Optional sink for human-readable status lines.</param>
    /// <param name="cancellationToken">Token that aborts the run between entries.</param>
    /// <returns>The cleanup result.</returns>
    /// <exception cref="UnauthorizedAccessException">Component store cleanup requested without elevation.</exception>
    Task<DiskCleanResult> CleanDiskAsync(DiskCleanOptions options, IProgress<string>? progress = null, CancellationToken cancellationToken = default);
}

/// <summary>Default <see cref="IDiskCleanerService"/> implementation.</summary>
public sealed class DiskCleanerService : IDiskCleanerService
{
    private const string DismArguments = "/Online /Cleanup-Image /StartComponentCleanup /ResetBase";

    private readonly IProcessRunner _processRunner;
    private readonly IElevationService _elevation;
    private readonly DiskCleanTargets _targets;

    /// <summary>Initializes a new instance of the <see cref="DiskCleanerService"/> class using default Windows locations.</summary>
    /// <param name="processRunner">Runner used for DISM.</param>
    /// <param name="elevation">Elevation probe.</param>
    public DiskCleanerService(IProcessRunner processRunner, IElevationService elevation)
        : this(processRunner, elevation, DiskCleanTargets.CreateDefault())
    {
    }

    /// <summary>Initializes a new instance of the <see cref="DiskCleanerService"/> class with explicit targets.</summary>
    /// <param name="processRunner">Runner used for DISM.</param>
    /// <param name="elevation">Elevation probe.</param>
    /// <param name="targets">Locations to clean.</param>
    public DiskCleanerService(IProcessRunner processRunner, IElevationService elevation, DiskCleanTargets targets)
    {
        ArgumentNullException.ThrowIfNull(processRunner);
        ArgumentNullException.ThrowIfNull(elevation);
        ArgumentNullException.ThrowIfNull(targets);
        _processRunner = processRunner;
        _elevation = elevation;
        _targets = targets;
    }

    /// <inheritdoc />
    public async Task<DiskCleanResult> CleanDiskAsync(DiskCleanOptions options, IProgress<string>? progress = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(options);
        cancellationToken.ThrowIfCancellationRequested();

        if (options.ComponentStore && !_elevation.IsElevated)
        {
            throw new UnauthorizedAccessException("Administrator privileges are required for DISM component store cleanup.");
        }

        var tally = new Tally();
        int? dismExit = null;

        if (options.ComponentStore)
        {
            progress?.Report("Running DISM component store cleanup (ResetBase)...");
            dismExit = await _processRunner.RunStreamAsync("dism.exe", DismArguments, line => progress?.Report(line), cancellationToken).ConfigureAwait(false);
            progress?.Report(dismExit == 0 ? "DISM completed." : $"DISM exited with code {dismExit}.");
        }

        if (options.TempFiles)
        {
            progress?.Report("Purging temporary files...");
            await PurgeAsync(_targets.TempDirectories, tally, cancellationToken).ConfigureAwait(false);
        }

        if (options.CrashDumps)
        {
            progress?.Report("Clearing crash dumps and error reports...");
            await PurgeAsync(_targets.DumpDirectories, tally, cancellationToken).ConfigureAwait(false);
            foreach (var file in _targets.DumpFiles)
            {
                cancellationToken.ThrowIfCancellationRequested();
                TryDeleteFile(file, tally);
            }
        }

        if (options.Logs)
        {
            progress?.Report("Trimming log files...");
            await PurgeAsync(_targets.LogDirectories, tally, cancellationToken).ConfigureAwait(false);
        }

        progress?.Report($"Deleted {tally.Files} files ({tally.Bytes / 1024d / 1024d:0.0} MB); skipped {tally.Skipped}.");
        return new DiskCleanResult(tally.Bytes, tally.Files, tally.Skipped, dismExit);
    }

    private static Task PurgeAsync(IEnumerable<string> directories, Tally tally, CancellationToken cancellationToken) =>
        Task.Run(
            () =>
            {
                foreach (var directory in directories)
                {
                    PurgeDirectoryContents(directory, tally, cancellationToken);
                }
            },
            cancellationToken);

    private static void PurgeDirectoryContents(string directory, Tally tally, CancellationToken cancellationToken)
    {
        if (!Directory.Exists(directory))
        {
            return;
        }

        IEnumerable<FileSystemInfo> entries;
        try
        {
            entries = new DirectoryInfo(directory).EnumerateFileSystemInfos();
        }
        catch (Exception ex) when (IsIoFailure(ex))
        {
            tally.Skipped++;
            return;
        }

        foreach (var entry in entries)
        {
            cancellationToken.ThrowIfCancellationRequested();

            // Never traverse or delete through symlinks/junctions: they may point outside the cleanup root.
            if (entry.Attributes.HasFlag(FileAttributes.ReparsePoint))
            {
                tally.Skipped++;
                continue;
            }

            if (entry is DirectoryInfo subDirectory)
            {
                PurgeDirectoryContents(subDirectory.FullName, tally, cancellationToken);
                try
                {
                    subDirectory.Delete(false);
                }
                catch (Exception ex) when (IsIoFailure(ex))
                {
                    tally.Skipped++;
                }
            }
            else
            {
                TryDeleteFile(entry.FullName, tally);
            }
        }
    }

    private static void TryDeleteFile(string path, Tally tally)
    {
        try
        {
            var info = new FileInfo(path);
            if (!info.Exists)
            {
                return;
            }

            long length = info.Length;
            if (info.IsReadOnly)
            {
                info.IsReadOnly = false;
            }

            info.Delete();
            tally.Files++;
            tally.Bytes += length;
        }
        catch (Exception ex) when (IsIoFailure(ex))
        {
            tally.Skipped++;
        }
    }

    private static bool IsIoFailure(Exception ex) =>
        ex is IOException or UnauthorizedAccessException or System.Security.SecurityException;

    private sealed class Tally
    {
        public long Bytes { get; set; }

        public int Files { get; set; }

        public int Skipped { get; set; }
    }
}

/// <summary>Filesystem locations targeted by <see cref="DiskCleanerService"/>.</summary>
/// <param name="TempDirectories">Folders whose contents are deleted.</param>
/// <param name="DumpDirectories">Dump / WER folders whose contents are deleted.</param>
/// <param name="DumpFiles">Individual dump files to delete.</param>
/// <param name="LogDirectories">Log folders whose contents are deleted.</param>
public sealed record DiskCleanTargets(
    IReadOnlyList<string> TempDirectories,
    IReadOnlyList<string> DumpDirectories,
    IReadOnlyList<string> DumpFiles,
    IReadOnlyList<string> LogDirectories)
{
    /// <summary>Builds the standard Windows targets, resolved from known folders rather than hard-coded drive letters.</summary>
    /// <returns>The default target set.</returns>
    public static DiskCleanTargets CreateDefault()
    {
        string windows = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
        string programData = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
        string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);

        return new DiskCleanTargets(
            TempDirectories: [Path.Combine(windows, "Temp"), Path.Combine(localAppData, "Temp")],
            DumpDirectories:
            [
                Path.Combine(windows, "Minidump"),
                Path.Combine(programData, "Microsoft", "Windows", "WER", "ReportArchive"),
                Path.Combine(programData, "Microsoft", "Windows", "WER", "ReportQueue"),
            ],
            DumpFiles: [Path.Combine(windows, "MEMORY.DMP")],
            LogDirectories: [Path.Combine(windows, "Logs")]);
    }
}
