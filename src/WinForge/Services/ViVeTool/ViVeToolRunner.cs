using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using ViVeToolApp.Models;

namespace ViVeToolApp.Services;

/// <summary>
/// Implementation of ViVeTool CLI execution, output classification, and batch orchestration.
/// </summary>
public class ViVeToolRunner : IViVeToolRunner
{
    private readonly IProcessLauncher _processLauncher;
    private static readonly Regex UnsupportedRegex = new(
        @"not found|unknown|unsupported|no feature",
        RegexOptions.IgnoreCase | RegexOptions.Compiled,
        TimeSpan.FromSeconds(5));

    private static readonly Regex MissingDependencyRegex = new(
        @"Albacore\.ViVe|Could not load file or assembly.*Albacore",
        RegexOptions.IgnoreCase | RegexOptions.Compiled,
        TimeSpan.FromSeconds(5));

    // Exit code 0xE0434352 = CLR unhandled exception (-532462766)
    private const int ClrUnhandledExceptionExitCode = -532462766;

    public ViVeToolRunner(IProcessLauncher? processLauncher = null)
    {
        _processLauncher = processLauncher ?? new SystemProcessLauncher();
    }

    public string FormatArguments(ViVeExecutionMode mode, long featureId)
    {
        var verb = mode == ViVeExecutionMode.Enable ? "/enable" : "/disable";
        return $"{verb} /id:{featureId}";
    }

    public string BuildArguments(ViVeExecutionMode mode, long featureId)
    {
        return FormatArguments(mode, featureId);
    }

    public ViVeToolStatus ClassifyStatus(int exitCode, string combinedOutput)
    {
        if (exitCode == 0)
        {
            return ViVeToolStatus.Success;
        }

        // Missing dependency must never be mis-classified as Unsupported/Skip
        if (!string.IsNullOrEmpty(combinedOutput) && MissingDependencyRegex.IsMatch(combinedOutput))
        {
            return ViVeToolStatus.Error;
        }

        if (!string.IsNullOrEmpty(combinedOutput) && UnsupportedRegex.IsMatch(combinedOutput))
        {
            return ViVeToolStatus.UnsupportedOrNotFound;
        }

        return ViVeToolStatus.Error;
    }

    private static bool IsMissingDependency(string combinedOutput, int exitCode)
    {
        if (!string.IsNullOrEmpty(combinedOutput) && MissingDependencyRegex.IsMatch(combinedOutput))
        {
            return true;
        }

        // CLR unhandled FileNotFound often surfaces with this exit code + no output
        if (exitCode == ClrUnhandledExceptionExitCode && !string.IsNullOrEmpty(combinedOutput) && combinedOutput.Contains("FileNotFoundException", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return false;
    }

    private static bool IsViVeToolInstallationIncomplete(string viveToolPath)
    {
        try
        {
            var dir = Path.GetDirectoryName(viveToolPath);
            if (string.IsNullOrEmpty(dir))
            {
                return false;
            }

            // ViVeTool v0.3.x .NET build requires Albacore.ViVe.dll alongside vivetool.exe
            var companion = Path.Combine(dir, "Albacore.ViVe.dll");
            if (File.Exists(viveToolPath) && !File.Exists(companion))
            {
                // Only flag if the exe exists but companion is missing — avoids false positives for C++ builds
                // B-06: Use AppContext.BaseDirectory instead of hardcoded C:\Tools path
                var sourceCompanion = Path.Combine(AppContext.BaseDirectory, "Albacore.ViVe.dll");
                if (File.Exists(sourceCompanion))
                {
                    return true;
                }
            }
        }
        catch
        {
            // Ignore IO errors — fall back to normal execution
        }

        return false;
    }

    public ViVeToolResult ClassifyResult(long featureId, int exitCode, string? stdOut, string? stdErr)
    {
        return ClassifyResult(featureId, ViVeExecutionMode.Enable, exitCode, stdOut, stdErr);
    }

    public ViVeToolResult ClassifyResult(long featureId, ViVeExecutionMode mode, int exitCode, string? stdOut, string? stdErr)
    {
        var safeOut = stdOut?.Trim() ?? string.Empty;
        var safeErr = stdErr?.Trim() ?? string.Empty;
        var combined = (safeOut + " " + safeErr).Trim();
        var status = ClassifyStatus(exitCode, combined);
        var output = !string.IsNullOrWhiteSpace(safeOut) ? safeOut : safeErr;
        var errorMsg = exitCode != 0 ? (string.IsNullOrWhiteSpace(safeErr) ? output : safeErr) : string.Empty;

        // Enrich missing-dependency errors with actionable guidance
        if (status == ViVeToolStatus.Error && IsMissingDependency(combined, exitCode))
        {
            var hint = "ViVeTool installation incomplete: Albacore.ViVe.dll missing next to vivetool.exe. Reinstall via the Download button or re-extract the ViVeTool zip.";
            errorMsg = string.IsNullOrWhiteSpace(errorMsg) ? hint : $"{errorMsg} | {hint}";
            // Force error message to appear even if stdout carried the exception
            if (string.IsNullOrWhiteSpace(safeErr) && !string.IsNullOrWhiteSpace(safeOut) && combined.Contains("Albacore", StringComparison.OrdinalIgnoreCase))
            {
                errorMsg = $"{safeOut} | {hint}";
                output = string.Empty;
            }
        }
        else if (exitCode == ClrUnhandledExceptionExitCode && string.IsNullOrWhiteSpace(errorMsg))
        {
            errorMsg = $"ViVeTool crashed with CLR unhandled exception (exit {exitCode}). Check that Albacore.ViVe.dll is present next to vivetool.exe and that you run as Administrator. Raw: {combined}";
        }

        return new ViVeToolResult(featureId, mode, status, exitCode, output, errorMsg);
    }

    public async Task<ViVeToolResult> ExecuteFeatureAsync(
        string viveToolPath,
        long featureId,
        ViVeExecutionMode mode,
        bool whatIf,
        CancellationToken cancellationToken = default)
    {
        var args = FormatArguments(mode, featureId);

        if (whatIf)
        {
            return new ViVeToolResult(
                featureId,
                mode,
                ViVeToolStatus.Success,
                exitCode: 0,
                output: $"[WHATIF] {args}",
                errorMessage: string.Empty);
        }

        if (string.IsNullOrWhiteSpace(viveToolPath))
        {
            return new ViVeToolResult(
                featureId,
                mode,
                ViVeToolStatus.Error,
                exitCode: -1,
                output: string.Empty,
                errorMessage: "ViVeTool executable path is not specified.");
        }

        try
        {
            var (exitCode, stdout, stderr) = await _processLauncher.RunProcessAsync(
                viveToolPath,
                args,
                cancellationToken).ConfigureAwait(false);

            return ClassifyResult(featureId, mode, exitCode, stdout, stderr);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            return new ViVeToolResult(
                featureId,
                mode,
                ViVeToolStatus.Error,
                exitCode: -1,
                output: string.Empty,
                errorMessage: ex.Message);
        }
    }

    public async Task<ViVeBatchResult> RunBatchAsync(
        string viveToolPath,
        IEnumerable<long> featureIds,
        ViVeExecutionMode mode,
        bool whatIf,
        IProgress<ViVeProgressReport>? progress = null,
        CancellationToken cancellationToken = default)
    {
        if (featureIds == null)
        {
            return new ViVeBatchResult(0, 0, 0, 0, new List<ViVeToolResult>());
        }

        // Fail-fast: detect missing companion before spamming 118 processes
        if (!whatIf && IsViVeToolInstallationIncomplete(viveToolPath))
        {
            var companion = Path.Combine(Path.GetDirectoryName(viveToolPath) ?? string.Empty, "Albacore.ViVe.dll");
            var msg = $"ViVeTool installation incomplete: Albacore.ViVe.dll missing next to vivetool.exe at '{viveToolPath}'. Expected companion at '{companion}'. Reinstall via the Download button.";
            var earlyResult = new ViVeToolResult(0, mode, ViVeToolStatus.Error, ClrUnhandledExceptionExitCode, string.Empty, msg);
            progress?.Report(new ViVeProgressReport(0, 1, 0, 0, earlyResult, $"[ERROR] {msg}"));
            var allIds = featureIds.Distinct().OrderBy(x => x).ToList();
            var filledResults = allIds.Select(id => new ViVeToolResult(id, mode, ViVeToolStatus.Error, ClrUnhandledExceptionExitCode, string.Empty, msg)).ToList();
            return new ViVeBatchResult(allIds.Count, 0, 0, allIds.Count, filledResults);
        }

        var idList = featureIds.Distinct().OrderBy(x => x).ToList();
        var results = new List<ViVeToolResult>(idList.Count);
        int successCount = 0;
        int skippedCount = 0;
        int errorCount = 0;

        for (int i = 0; i < idList.Count; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var id = idList[i];
            var result = await ExecuteFeatureAsync(viveToolPath, id, mode, whatIf, cancellationToken).ConfigureAwait(false);
            results.Add(result);

            // If execution reveals missing dependency, abort remaining to avoid spamming 118 processes
            if (result.Status == ViVeToolStatus.Error && IsMissingDependency(result.ErrorMessage + " " + result.Output, result.ExitCode))
            {
                var abortMsg = result.ErrorMessage;

                // Count current result
                switch (result.Status)
                {
                    case ViVeToolStatus.Success: successCount++; break;
                    case ViVeToolStatus.UnsupportedOrNotFound: skippedCount++; break;
                    default: errorCount++; break;
                }

                var pctCur = (double)(i + 1) / idList.Count * 100.0;
                progress?.Report(new ViVeProgressReport(i + 1, idList.Count, id, pctCur, result, $"[ERROR] ID:{id}  exit={result.ExitCode}  {result.ErrorMessage} — batch aborted (missing dependency)"));

                // Populate remaining IDs as same error without launching processes
                for (int j = i + 1; j < idList.Count; j++)
                {
                    var remainingId = idList[j];
                    var aborted = new ViVeToolResult(remainingId, mode, ViVeToolStatus.Error, result.ExitCode, string.Empty, abortMsg + " (batch aborted — install incomplete)");
                    results.Add(aborted);
                }

                errorCount += (idList.Count - i - 1);

                for (int k = i + 1; k < idList.Count; k++)
                {
                    var pct2 = (double)(k + 1) / idList.Count * 100.0;
                    progress?.Report(new ViVeProgressReport(k + 1, idList.Count, idList[k], pct2, results[k], $"[ERROR] ID:{idList[k]}  aborted — {abortMsg}"));
                }

                return new ViVeBatchResult(idList.Count, successCount, skippedCount, errorCount, results);
            }

            switch (result.Status)
            {
                case ViVeToolStatus.Success:
                    successCount++;
                    break;
                case ViVeToolStatus.UnsupportedOrNotFound:
                    skippedCount++;
                    break;
                case ViVeToolStatus.Error:
                case ViVeToolStatus.Warning:
                default:
                    errorCount++;
                    break;
            }

            var currentIndex = i + 1;
            var pct = idList.Count > 0 ? (double)currentIndex / idList.Count * 100.0 : 0.0;
            var msg = result.Status switch
            {
                ViVeToolStatus.Success => whatIf ? result.Output : $"[SUCCESS] ID:{id}  {result.Output}",
                ViVeToolStatus.UnsupportedOrNotFound => $"[SKIP]    ID:{id}  Unsupported on this build",
                _ => $"[WARN]    ID:{id}  exit={result.ExitCode}  {result.ErrorMessage}"
            };

            progress?.Report(new ViVeProgressReport(currentIndex, idList.Count, id, pct, result, msg));
        }

        return new ViVeBatchResult(idList.Count, successCount, skippedCount, errorCount, results);
    }

    public Task<ViVeBatchResult> RunBatchAsync(
        string viveToolPath,
        IEnumerable<FeatureItem> features,
        ViVeExecutionMode mode,
        bool whatIf,
        IProgress<ViVeProgressReport>? progress = null,
        CancellationToken cancellationToken = default)
    {
        if (features == null)
        {
            return RunBatchAsync(viveToolPath, Enumerable.Empty<long>(), mode, whatIf, progress, cancellationToken);
        }

        var selectedIds = features
            .Where(f => f != null && f.IsSelected)
            .SelectMany(f => f.IDs ?? Array.Empty<long>())
            .Where(id => id > 0)
            .Distinct()
            .OrderBy(x => x);

        return RunBatchAsync(viveToolPath, selectedIds, mode, whatIf, progress, cancellationToken);
    }
}
