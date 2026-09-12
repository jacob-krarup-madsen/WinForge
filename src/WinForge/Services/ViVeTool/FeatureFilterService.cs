using System;
using System.Collections.Generic;
using System.Linq;
using ViVeToolApp.Models;

namespace ViVeToolApp.Services;

/// <summary>
/// Implementation of search and track filtering, selection updates, and summary calculations.
/// </summary>
public class FeatureFilterService : IFeatureFilterService
{
    public IEnumerable<FeatureItem> Filter(IEnumerable<FeatureItem> allFeatures, string? searchQuery, string? groupFilter)
    {
        if (allFeatures == null)
        {
            return Enumerable.Empty<FeatureItem>();
        }

        var search = searchQuery?.Trim() ?? string.Empty;
        var group = !string.IsNullOrWhiteSpace(groupFilter) ? groupFilter.Trim() : "All groups";

        bool isAllGroups(string g) => string.Equals(g, "All groups", StringComparison.OrdinalIgnoreCase) ||
                                      string.Equals(g, "All Tracks", StringComparison.OrdinalIgnoreCase);

        return allFeatures.Where(item =>
        {
            var matchGroup = isAllGroups(group) ||
                              string.Equals(item.Group, group, StringComparison.OrdinalIgnoreCase);

            var matchSearch = string.IsNullOrEmpty(search) ||
                              item.Description.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                              item.IDsDisplay.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                              item.BuildLabel.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                              item.Group.Contains(search, StringComparison.OrdinalIgnoreCase);

            return matchGroup && matchSearch;
        });
    }

    public SelectionSummary CalculateSummary(IEnumerable<FeatureItem> visibleFeatures, IEnumerable<FeatureItem> allFeatures)
    {
        var all = allFeatures?.ToList() ?? new List<FeatureItem>();
        var vis = visibleFeatures?.ToList() ?? new List<FeatureItem>();

        var total = all.Count;
        var visibleCount = vis.Count;
        var selectedCount = all.Count(i => i.IsSelected);
        var selectedPercentage = total > 0 ? (double)selectedCount / total * 100.0 : 0.0;
        var uniqueSelectedIds = all
            .Where(i => i.IsSelected)
            .SelectMany(i => i.IDs)
            .Where(id => id > 0)
            .Distinct()
            .Count();

        var summaryText = $"Visible {visibleCount} of {total}  ·  Checked: {selectedCount}";

        return new SelectionSummary(total, visibleCount, selectedCount, selectedPercentage, uniqueSelectedIds, summaryText);
    }

    public List<long> GetDistinctSelectedFeatureIds(IEnumerable<FeatureItem> features)
    {
        if (features == null)
        {
            return new List<long>();
        }

        return features
            .Where(i => i.IsSelected)
            .SelectMany(i => i.IDs)
            .Where(id => id > 0)
            .Distinct()
            .OrderBy(x => x)
            .ToList();
    }

    public List<string> GetDistinctGroups(IEnumerable<FeatureItem> features)
    {
        if (features == null)
        {
            return new List<string>();
        }

        return features
            .Select(i => i.Group)
            .Where(g => !string.IsNullOrWhiteSpace(g))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(x => x)
            .ToList();
    }

    public void SetSelection(IEnumerable<FeatureItem> features, bool isSelected)
    {
        if (features == null) return;
        foreach (var item in features)
        {
            item.IsSelected = isSelected;
        }
    }

    public void SetGroupSelection(IEnumerable<FeatureItem> allFeatures, string group, bool isSelected)
    {
        if (allFeatures == null) return;
        var cleanGroup = string.IsNullOrWhiteSpace(group) ? "All groups" : group.Trim();
        bool isAllGroups(string g) => string.Equals(g, "All groups", StringComparison.OrdinalIgnoreCase) ||
                                      string.Equals(g, "All Tracks", StringComparison.OrdinalIgnoreCase);

        foreach (var item in allFeatures)
        {
            if (isAllGroups(cleanGroup) ||
                 string.Equals(item.Group, cleanGroup, StringComparison.OrdinalIgnoreCase))
            {
                item.IsSelected = isSelected;
            }
        }
    }
}
