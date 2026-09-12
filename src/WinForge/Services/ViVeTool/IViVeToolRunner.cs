using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ViVeToolApp.Models;

namespace ViVeToolApp.Services;

/// <summary>
/// Service contract for formatting, executing, and classifying ViVeTool CLI commands.
/// </summary>
public interface IViVeToolRunner
{
    /// <summary>
    /// Formats the CLI argument string for a given mode and feature ID.
    /// </summary>
    string FormatArguments(ViVeExecutionMode mode, long featureId);

    /// <summary>
    /// Formats the CLI argument string for a given mode and feature ID (alias for FormatArguments).
    /// </summary>
    string BuildArguments(ViVeExecutionMode mode, long featureId);

    /// <summary>
    /// Classifies the execution status based on exit code and command output text.
    /// </summary>
    ViVeToolStatus ClassifyStatus(int exitCode, string combinedOutput);

    /// <summary>
    /// Classifies the result of executing ViVeTool on a single feature ID.
    /// </summary>
    ViVeToolResult ClassifyResult(long featureId, int exitCode, string? stdOut, string? stdErr);

    /// <summary>
    /// Classifies the result of executing ViVeTool on a single feature ID with specified execution mode.
    /// </summary>
    ViVeToolResult ClassifyResult(long featureId, ViVeExecutionMode mode, int exitCode, string? stdOut, string? stdErr);

    /// <summary>
    /// Executes ViVeTool for a single feature ID or performs a dry-run.
    /// </summary>
    Task<ViVeToolResult> ExecuteFeatureAsync(
        string viveToolPath,
        long featureId,
        ViVeExecutionMode mode,
        bool whatIf,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Executes ViVeTool for a collection of feature IDs in batch.
    /// </summary>
    Task<ViVeBatchResult> RunBatchAsync(
        string viveToolPath,
        IEnumerable<long> featureIds,
        ViVeExecutionMode mode,
        bool whatIf,
        IProgress<ViVeProgressReport>? progress = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Executes ViVeTool for selected items in a collection of features in batch.
    /// </summary>
    Task<ViVeBatchResult> RunBatchAsync(
        string viveToolPath,
        IEnumerable<FeatureItem> features,
        ViVeExecutionMode mode,
        bool whatIf,
        IProgress<ViVeProgressReport>? progress = null,
        CancellationToken cancellationToken = default);
}
