namespace ViVeToolApp.Models;

/// <summary>
/// Real-time progress report emitted during batch feature execution.
/// </summary>
public class ViVeProgressReport
{
    public int CurrentIndex { get; set; }
    public int TotalCount { get; set; }
    public long CurrentFeatureId { get; set; }
    public double Percentage { get; set; }
    public ViVeToolResult? LastResult { get; set; }
    public string FormattedMessage { get; set; } = string.Empty;

    public string LogMessage
    {
        get => !string.IsNullOrEmpty(FormattedMessage) ? FormattedMessage : string.Empty;
        set => FormattedMessage = value;
    }

    public ViVeProgressReport()
    {
    }

    public ViVeProgressReport(
        int currentIndex,
        int totalCount,
        long currentFeatureId,
        double percentage,
        ViVeToolResult? lastResult,
        string formattedMessage = "")
    {
        CurrentIndex = currentIndex;
        TotalCount = totalCount;
        CurrentFeatureId = currentFeatureId;
        Percentage = percentage;
        LastResult = lastResult;
        FormattedMessage = formattedMessage;
    }
}
