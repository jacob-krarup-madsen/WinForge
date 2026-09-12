using System;
using System.Collections.Generic;
using System.Linq;
using ViVeToolApp.Models;

namespace ViVeToolApp.Services;

/// <summary>
/// Provides a hardcoded offline fallback catalog of essential Windows 11 feature IDs across all release tracks.
/// </summary>
public static class OfflineCatalog
{
    private static readonly (string Group, string BuildLabel, string Description, string IDsDisplay)[] CatalogEntries =
    [
        ("GA 2026", "Sep 2026", "Start menu resize and customization", "61754985"),
        ("GA 2026", "Sep 2026", "Windows Search settings", "62762248"),
        ("GA 2026", "Sep 2026", "Taskbar positioning", "59213768"),
        ("GA 2026", "Sep 2026", "Context menu settings", "60813048"),
        ("GA 2026", "Sep 2026", "Smaller Taskbar option", "61090762"),
        ("GA 2026", "Sep 2026", "Modern boot spinner", "59728252"),
        ("GA 2026", "Sep 2026", "Pointer Indicator", "27829265, 61457898"),
        ("GA 2025", "Dec 2025", "Widgets redesign", "59162732, 55994763"),
        ("GA 2025", "Dec 2025", "Taskbar autohide animation", "41356296"),
        ("26H2 Insider", "Build 26300.8697", "Search web toggle", "61267302, 61344081, 61482515, 61532758, 61760679"),
        ("26H2 Insider", "Build 26300.8289", "Modern Run dialog", "57156807"),
        ("26H2 Insider", "Build 26300.8289", "Screen tint accessibility", "60662124"),
        ("25H2 Insider", "Build 26220.7271", "Xbox Full Screen Experience", "59765208"),
        ("25H2 Insider", "Build 26220.6690", "Windows DreamScene", "57645315"),
        ("Canary / Feature Platforms", "Build 29648", "Unified memory for games", "61121285"),
    ];

    /// <summary>
    /// Returns a new list of 15 offline feature items across all tracks.
    /// Returns fresh instances on each call to prevent state leakage.
    /// </summary>
    public static List<FeatureItem> GetFeatures()
    {
        var items = new List<FeatureItem>(CatalogEntries.Length);

        foreach (var (group, buildLabel, description, idsDisplay) in CatalogEntries)
        {
            var parsedIds = idsDisplay
                .Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
                .Select(s => long.TryParse(s, out var id) ? id : 0)
                .Where(id => id >= 1_000_000 && id <= 999_999_999)
                .Distinct()
                .ToArray();

            items.Add(new FeatureItem
            {
                IsSelected = true,
                Group = group,
                BuildLabel = buildLabel,
                Description = description,
                IDsDisplay = idsDisplay,
                IDs = parsedIds
            });
        }

        return items;
    }

    /// <summary>
    /// Alias for <see cref="GetFeatures"/> for backwards compatibility.
    /// </summary>
    public static List<FeatureItem> GetFallbackFeatures() => GetFeatures();
}
