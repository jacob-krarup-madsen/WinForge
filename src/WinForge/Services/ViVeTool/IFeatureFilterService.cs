using System.Collections.Generic;
using ViVeToolApp.Models;

namespace ViVeToolApp.Services;

/// <summary>
/// Service contract for feature filtering, track groupings, and selection metrics.
/// </summary>
public interface IFeatureFilterService
{
    /// <summary>
    /// Filters features by search query (matching description, IDs, build, or group) and track group.
    /// </summary>
    IEnumerable<FeatureItem> Filter(IEnumerable<FeatureItem> allFeatures, string? searchQuery, string? groupFilter);

    /// <summary>
    /// Calculates aggregate selection and visibility metrics.
    /// </summary>
    SelectionSummary CalculateSummary(IEnumerable<FeatureItem> visibleFeatures, IEnumerable<FeatureItem> allFeatures);

    /// <summary>
    /// Extracts distinct, sorted numeric IDs for all checked features.
    /// </summary>
    List<long> GetDistinctSelectedFeatureIds(IEnumerable<FeatureItem> features);

    /// <summary>
    /// Extracts distinct, sorted track group names.
    /// </summary>
    List<string> GetDistinctGroups(IEnumerable<FeatureItem> features);

    /// <summary>
    /// Sets the selection state for all items in the given collection.
    /// </summary>
    void SetSelection(IEnumerable<FeatureItem> features, bool isSelected);

    /// <summary>
    /// Sets the selection state for all items belonging to a specified track group.
    /// </summary>
    void SetGroupSelection(IEnumerable<FeatureItem> allFeatures, string group, bool isSelected);
}
