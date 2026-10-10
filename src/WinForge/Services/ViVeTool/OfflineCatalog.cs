using System;
using System.Collections.Generic;
using System.Linq;
using ViVeToolApp.Models;

namespace ViVeToolApp.Services;

/// <summary>
/// Provides the complete offline catalog of all 118 Windows 11 feature velocity IDs across all release tracks.
/// </summary>
public static class OfflineCatalog
{
    private static readonly (string Group, string BuildLabel, string Description, string IDsDisplay)[] CatalogEntries =
    [
        // Curated multi-ID and named GA 2026 features
        ("GA 2026", "Sep 2026", "Start menu resize and customization", "61754985"),
        ("GA 2026", "Sep 2026", "Windows Search settings", "62762248"),
        ("GA 2026", "Sep 2026", "Taskbar positioning", "59213768"),
        ("GA 2026", "Sep 2026", "Context menu settings", "60813048"),
        ("GA 2026", "Sep 2026", "Smaller Taskbar option", "61090762"),
        ("GA 2026", "Sep 2026", "Modern boot spinner", "59728252"),
        ("GA 2026", "Sep 2026", "Pointer Indicator", "27829265, 61457898"),
        ("GA 2026", "GA 2026 / 26H2", "Windows 11 feature velocity override 61161244", "61161244"),
        ("GA 2026", "GA 2026 / 26H2", "Windows 11 feature velocity override 61160789", "61160789"),
        ("GA 2026", "GA 2026 / 26H2", "Windows 11 feature velocity override 58989177", "58989177"),
        ("GA 2026", "GA 2026 / 26H2", "Windows 11 feature velocity override 58989092", "58989092"),
        ("GA 2026", "GA 2026 / 26H2", "Windows 11 feature velocity override 60716524", "60716524"),
        ("GA 2026", "GA 2026 / 26H2", "Windows 11 feature velocity override 48433719", "48433719"),
        ("GA 2026", "GA 2026 / 26H2", "Windows 11 feature velocity override 61391826", "61391826"),
        ("GA 2026", "GA 2026 / 26H2", "Windows 11 feature velocity override 58989070", "58989070"),
        ("GA 2026", "GA 2026 / 26H2", "Windows 11 feature velocity override 58989021", "58989021"),
        ("GA 2026", "GA 2026 / 26H2", "Windows 11 feature velocity override 58989002", "58989002"),
        ("GA 2026", "GA 2026 / 26H2", "Windows 11 feature velocity override 57741219", "57741219"),
        ("GA 2026", "GA 2026 / 26H2", "Windows 11 feature velocity override 58988972", "58988972"),

        // GA 2025 features
        ("GA 2025", "Dec 2025", "Widgets redesign", "59162732, 55994763"),
        ("GA 2025", "Dec 2025", "Taskbar autohide animation", "41356296"),
        ("GA 2025", "GA 2025", "Windows 11 feature velocity override 57048237", "57048237"),
        ("GA 2025", "GA 2025", "Windows 11 feature velocity override 45690266", "45690266"),
        ("GA 2025", "GA 2025 / 26H2", "Windows 11 feature velocity override 59265307", "59265307"),
        ("GA 2025", "GA 2025", "Windows 11 feature velocity override 57882334", "57882334"),
        ("GA 2025", "GA 2025 / 26H2", "Windows 11 feature velocity override 53343270", "53343270"),
        ("GA 2025", "GA 2025", "Windows 11 feature velocity override 57048231", "57048231"),
        ("GA 2025", "GA 2025", "Windows 11 feature velocity override 47205210", "47205210"),
        ("GA 2025", "GA 2025", "Windows 11 feature velocity override 57048226", "57048226"),
        ("GA 2025", "GA 2025", "Windows 11 feature velocity override 57048218", "57048218"),
        ("GA 2025", "GA 2025", "Windows 11 feature velocity override 57048216", "57048216"),

        // 26H2 Insider features
        ("26H2 Insider", "Build 26300.8697", "Search web toggle", "61267302, 61344081, 61482515, 61532758, 61760679"),
        ("26H2 Insider", "Build 26300.8289", "Modern Run dialog", "57156807"),
        ("26H2 Insider", "Build 26300.8289", "Screen tint accessibility", "60662124"),
        ("26H2 Insider", "26H2", "Windows 11 feature velocity override 62141177", "62141177"),
        ("26H2 Insider", "26H2", "Windows 11 feature velocity override 62068874", "62068874"),
        ("26H2 Insider", "26H2", "Windows 11 feature velocity override 63194003", "63194003"),
        ("26H2 Insider", "26H2", "Windows 11 feature velocity override 62915050", "62915050"),
        ("26H2 Insider", "26H2", "Windows 11 feature velocity override 61483244", "61483244"),
        ("26H2 Insider", "26H2", "Windows 11 feature velocity override 60490208", "60490208"),
        ("26H2 Insider", "26H2", "Windows 11 feature velocity override 60730253", "60730253"),
        ("26H2 Insider", "26H2", "Windows 11 feature velocity override 61384404", "61384404"),
        ("26H2 Insider", "26H2", "Windows 11 feature velocity override 60414189", "60414189"),
        ("26H2 Insider", "26H2", "Windows 11 feature velocity override 61161268", "61161268"),
        ("26H2 Insider", "26H2", "Windows 11 feature velocity override 61161304", "61161304"),
        ("26H2 Insider", "26H2", "Windows 11 feature velocity override 61161283", "61161283"),
        ("26H2 Insider", "26H2", "Windows 11 feature velocity override 61441697", "61441697"),
        ("26H2 Insider", "26H2", "Windows 11 feature velocity override 61465695", "61465695"),
        ("26H2 Insider", "26H2", "Windows 11 feature velocity override 61465915", "61465915"),
        ("26H2 Insider", "26H2", "Windows 11 feature velocity override 62261462", "62261462"),
        ("26H2 Insider", "26H2", "Windows 11 feature velocity override 60511437", "60511437"),
        ("26H2 Insider", "26H2", "Windows 11 feature velocity override 51406324", "51406324"),
        ("26H2 Insider", "26H2", "Windows 11 feature velocity override 60288851", "60288851"),
        ("26H2 Insider", "26H2", "Windows 11 feature velocity override 61225604", "61225604"),
        ("26H2 Insider", "26H2", "Windows 11 feature velocity override 61596616", "61596616"),
        ("26H2 Insider", "26H2", "Windows 11 feature velocity override 61596617", "61596617"),
        ("26H2 Insider", "26H2", "Windows 11 feature velocity override 61596618", "61596618"),
        ("26H2 Insider", "26H2", "Windows 11 feature velocity override 61596619", "61596619"),
        ("26H2 Insider", "26H2", "Windows 11 feature velocity override 61372722", "61372722"),
        ("26H2 Insider", "26H2", "Windows 11 feature velocity override 61014711", "61014711"),
        ("26H2 Insider", "26H2", "Windows 11 feature velocity override 60897831", "60897831"),
        ("26H2 Insider", "26H2", "Windows 11 feature velocity override 59956305", "59956305"),
        ("26H2 Insider", "26H2", "Windows 11 feature velocity override 57751666", "57751666"),
        ("26H2 Insider", "26H2", "Windows 11 feature velocity override 57751687", "57751687"),
        ("26H2 Insider", "26H2", "Windows 11 feature velocity override 61157505", "61157505"),
        ("26H2 Insider", "26H2", "Windows 11 feature velocity override 61410885", "61410885"),
        ("26H2 Insider", "26H2", "Windows 11 feature velocity override 60772592", "60772592"),
        ("26H2 Insider", "26H2", "Windows 11 feature velocity override 60911173", "60911173"),
        ("26H2 Insider", "26H2", "Windows 11 feature velocity override 58429068", "58429068"),
        ("26H2 Insider", "26H2", "Windows 11 feature velocity override 58111409", "58111409"),
        ("26H2 Insider", "26H2", "Windows 11 feature velocity override 59149945", "59149945"),
        ("26H2 Insider", "26H2", "Windows 11 feature velocity override 59764273", "59764273"),
        ("26H2 Insider", "26H2", "Windows 11 feature velocity override 60772996", "60772996"),
        ("26H2 Insider", "26H2", "Windows 11 feature velocity override 60597402", "60597402"),
        ("26H2 Insider", "26H2", "Windows 11 feature velocity override 60825171", "60825171"),
        ("26H2 Insider", "26H2", "Windows 11 feature velocity override 49059846", "49059846"),
        ("26H2 Insider", "26H2", "Windows 11 feature velocity override 60063638", "60063638"),
        ("26H2 Insider", "26H2", "Windows 11 feature velocity override 58182453", "58182453"),
        ("26H2 Insider", "26H2", "Windows 11 feature velocity override 57118881", "57118881"),

        // 25H2 Insider features
        ("25H2 Insider", "Build 26220.7271", "Xbox Full Screen Experience", "59765208"),
        ("25H2 Insider", "Build 26220.6690", "Windows DreamScene", "57645315"),
        ("25H2 Insider", "25H2", "Windows 11 feature velocity override 59359094", "59359094"),
        ("25H2 Insider", "25H2", "Windows 11 feature velocity override 58978959", "58978959"),
        ("25H2 Insider", "25H2", "Windows 11 feature velocity override 58381341", "58381341"),
        ("25H2 Insider", "25H2", "Windows 11 feature velocity override 58527096", "58527096"),
        ("25H2 Insider", "25H2", "Windows 11 feature velocity override 57259990", "57259990"),
        ("25H2 Insider", "25H2", "Windows 11 feature velocity override 58938944", "58938944"),
        ("25H2 Insider", "25H2", "Windows 11 feature velocity override 57900749", "57900749"),
        ("25H2 Insider", "25H2", "Windows 11 feature velocity override 58324036", "58324036"),
        ("25H2 Insider", "25H2", "Windows 11 feature velocity override 58680439", "58680439"),
        ("25H2 Insider", "25H2", "Windows 11 feature velocity override 38679741", "38679741"),
        ("25H2 Insider", "25H2", "Windows 11 feature velocity override 41118774", "41118774"),
        ("25H2 Insider", "25H2", "Windows 11 feature velocity override 55805655", "55805655"),
        ("25H2 Insider", "25H2", "Windows 11 feature velocity override 59213523", "59213523"),
        ("25H2 Insider", "25H2", "Windows 11 feature velocity override 59193521", "59193521"),
        ("25H2 Insider", "25H2", "Windows 11 feature velocity override 55324166", "55324166"),
        ("25H2 Insider", "25H2", "Windows 11 feature velocity override 59673297", "59673297"),
        ("25H2 Insider", "25H2", "Windows 11 feature velocity override 58423575", "58423575"),
        ("25H2 Insider", "25H2", "Windows 11 feature velocity override 58778013", "58778013"),
        ("25H2 Insider", "25H2", "Windows 11 feature velocity override 59339532", "59339532"),
        ("25H2 Insider", "25H2", "Windows 11 feature velocity override 57739723", "57739723"),
        ("25H2 Insider", "25H2", "Windows 11 feature velocity override 57941090", "57941090"),
        ("25H2 Insider", "25H2", "Windows 11 feature velocity override 58970402", "58970402"),
        ("25H2 Insider", "25H2", "Windows 11 feature velocity override 58383338", "58383338"),
        ("25H2 Insider", "25H2", "Windows 11 feature velocity override 59270880", "59270880"),
        ("25H2 Insider", "25H2", "Windows 11 feature velocity override 59203365", "59203365"),
        ("25H2 Insider", "25H2", "Windows 11 feature velocity override 57703775", "57703775"),

        // Canary / Feature Platforms
        ("Canary / Feature Platforms", "Build 29648", "Unified memory for games", "61121285"),
        ("Canary / Feature Platforms", "Canary", "Windows 11 feature velocity override 58288238", "58288238"),
        ("Canary / Feature Platforms", "Canary", "Windows 11 feature velocity override 53283713", "53283713"),
        ("Canary / Feature Platforms", "Canary", "Windows 11 feature velocity override 59065581", "59065581"),
        ("Canary / Feature Platforms", "Canary", "Windows 11 feature velocity override 45425284", "45425284"),
    ];

    /// <summary>
    /// Returns a new list of offline feature items covering all 118 Windows 11 velocity IDs across all release tracks.
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
                IDs = parsedIds,
            });
        }

        return items;
    }

    /// <summary>
    /// Alias for <see cref="GetFeatures"/> for backwards compatibility.
    /// </summary>
    public static List<FeatureItem> GetFallbackFeatures() => GetFeatures();
}
