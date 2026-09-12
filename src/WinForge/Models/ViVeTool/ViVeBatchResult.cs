using System.Collections.Generic;

namespace ViVeToolApp.Models;

/// <summary>
/// Aggregated summary of a completed batch execution.
/// </summary>
public class ViVeBatchResult
{
    public int TotalProcessed { get; set; }
    public int SuccessCount { get; set; }
    public int SkippedCount { get; set; }
    public int SkipCount
    {
        get => SkippedCount;
        set => SkippedCount = value;
    }
    public int ErrorCount { get; set; }
    public List<ViVeToolResult> Results { get; set; } = new();

    public string FormattedSummary => $"Done — OK:{SuccessCount}  Skip:{SkippedCount}  Err:{ErrorCount}";
    public string SummaryMessage => FormattedSummary;

    public ViVeBatchResult()
    {
    }

    public ViVeBatchResult(
        int totalProcessed,
        int successCount,
        int skippedCount,
        int errorCount,
        List<ViVeToolResult> results)
    {
        TotalProcessed = totalProcessed;
        SuccessCount = successCount;
        SkippedCount = skippedCount;
        ErrorCount = errorCount;
        Results = results;
    }
}
