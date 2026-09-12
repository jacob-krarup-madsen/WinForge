namespace ViVeToolApp.Models;

/// <summary>
/// Calculation summary of current feature selection and visibility metrics.
/// </summary>
public record SelectionSummary(
    int TotalCount,
    int VisibleCount,
    int SelectedCount,
    double SelectedPercentage,
    int UniqueSelectedIdsCount,
    string SummaryText = ""
)
{
    /// <summary>
    /// Gets the count of checked/selected features (alias for SelectedCount).
    /// </summary>
    public int CheckedCount => SelectedCount;

    /// <summary>
    /// Gets the selection percentage (alias for SelectedPercentage).
    /// </summary>
    public double SelectionPercentage => SelectedPercentage;

    /// <summary>
    /// Gets the count of unique selected IDs (alias for UniqueSelectedIdsCount).
    /// </summary>
    public int UniqueSelectedIdCount => UniqueSelectedIdsCount;

    /// <summary>
    /// Gets the formatted summary text.
    /// </summary>
    public string FormattedSummary => !string.IsNullOrEmpty(SummaryText) 
        ? SummaryText 
        : $"Visible {VisibleCount} of {TotalCount}  ·  Checked: {SelectedCount}";
}
