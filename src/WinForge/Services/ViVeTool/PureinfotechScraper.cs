using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using ViVeToolApp.Models;

namespace ViVeToolApp.Services;

/// <summary>
/// Scrapes and parses Windows 11 feature IDs from Pureinfotech articles.
/// </summary>
public class PureinfotechScraper : IFeatureScraper
{
    public const string DefaultUrl = "https://pureinfotech.com/vivetool-codes-enable-features-windows-11/";

    private readonly HttpClient _httpClient;

    public PureinfotechScraper(HttpClient? httpClient = null)
    {
        _httpClient = httpClient ?? new HttpClient();
        if (_httpClient.DefaultRequestHeaders.UserAgent.Count == 0)
        {
            _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 ViVeToolApp/3.0");
        }
        if (_httpClient.Timeout == TimeSpan.FromSeconds(100))
        {
            _httpClient.Timeout = TimeSpan.FromSeconds(25);
        }
    }

    private static readonly TimeSpan RegexTimeout = TimeSpan.FromSeconds(5);

    /// <inheritdoc />
    public async Task<List<FeatureItem>> FetchAndParseAsync(string? customUrl = null, CancellationToken cancellationToken = default)
    {
        var url = string.IsNullOrWhiteSpace(customUrl) ? DefaultUrl : customUrl.Trim();
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            throw new ArgumentException($"Invalid or unsupported HTTP/HTTPS URL: '{url}'", nameof(customUrl));
        }

        using var response = await _httpClient.GetAsync(uri, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        var html = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        return ParseHtml(html);
    }

    /// <inheritdoc />
    public List<FeatureItem> GetOfflineFallback()
    {
        return OfflineCatalog.GetFeatures();
    }

    /// <inheritdoc />
    public List<FeatureItem> ParseHtml(string html)
    {
        var results = new List<FeatureItem>();
        if (string.IsNullOrWhiteSpace(html))
        {
            return results;
        }

        // 1. Strip script and style blocks
        var cleanedHtml = Regex.Replace(html, @"<script[\s\S]*?</script>", "", RegexOptions.IgnoreCase, RegexTimeout);
        cleanedHtml = Regex.Replace(cleanedHtml, @"<style[\s\S]*?</style>", "", RegexOptions.IgnoreCase, RegexTimeout);

        // 2. Slice article content if markers exist
        const string startTag = "class=\"entry-content\"";
        const string endMarker = "<!-- CONTENT END";
        var si = cleanedHtml.IndexOf(startTag, StringComparison.OrdinalIgnoreCase);
        if (si >= 0)
        {
            var ei = cleanedHtml.IndexOf(endMarker, si, StringComparison.OrdinalIgnoreCase);
            cleanedHtml = ei > si ? cleanedHtml[si..ei] : cleanedHtml[si..];
        }

        var currentGroup = "General";
        var currentBuild = string.Empty;

        // 3. Match sections, build labels, and feature items in document order
        var tokenPattern = new Regex(
            @"(?<h><h[2-4][^>]*>(?<htext>[\s\S]*?)(?:</h[2-4]>|(?=<h[2-4]|<li|<strong|<p\b|$)))|(?<b><strong[^>]*>(?<btext>[\s\S]*?)(?:</strong>|(?=<h[2-4]|<li|<strong|<p\b|$)))|(?<li><li[^>]*>(?<licontent>[\s\S]*?)(?:</li>|(?=<li|<h[2-4]|<strong|<p\b|$)))",
            RegexOptions.IgnoreCase,
            RegexTimeout);

        var matches = tokenPattern.Matches(cleanedHtml);
        foreach (Match match in matches)
        {
            if (match.Groups["h"].Success)
            {
                var headerText = StripHtml(match.Groups["htext"].Value);
                if (!string.IsNullOrWhiteSpace(headerText))
                {
                    currentGroup = MapGroup(headerText);
                    currentBuild = string.Empty;
                }
            }
            else if (match.Groups["b"].Success)
            {
                var bold = StripHtml(match.Groups["btext"].Value);
                if (!string.IsNullOrWhiteSpace(bold)
                    && !Regex.IsMatch(bold, @"KB", RegexOptions.IgnoreCase, RegexTimeout)
                    && !Regex.IsMatch(bold, @"Update", RegexOptions.IgnoreCase, RegexTimeout))
                {
                    var isBuild = Regex.IsMatch(bold, @"^\s*Build\s+\d{5,}(\.\d+)?\s*:?\s*$", RegexOptions.IgnoreCase, RegexTimeout);
                    var isMonthly = Regex.IsMatch(bold, @"^(January|February|March|April|May|June|July|August|September|October|November|December)\s+20\d\d\s*:?\s*$", RegexOptions.IgnoreCase, RegexTimeout);
                    // Fallback with leading whitespace tolerance for monthly (StripHtml trims but be safe)
                    var isMonthlyAlt = Regex.IsMatch(bold, @"^\s*(January|February|March|April|May|June|July|August|September|October|November|December)\s+20\d\d\s*:?\s*$", RegexOptions.IgnoreCase, RegexTimeout);
                    if (isBuild || isMonthly || isMonthlyAlt)
                    {
                        currentBuild = bold.TrimEnd(':').Trim();
                    }
                }
            }
            else if (match.Groups["li"].Success)
            {
                var liContent = match.Groups["licontent"].Value;

                // Extract all code blocks inside this <li>
                var codeMatches = Regex.Matches(liContent, @"<code[^>]*>(?<codeRaw>[\s\S]*?)(?:</code>|$)", RegexOptions.IgnoreCase, RegexTimeout);
                if (codeMatches.Count == 0)
                {
                    continue;
                }

                var codeCombined = string.Join(" ", codeMatches.Select(m => m.Groups["codeRaw"].Value));
                var ids = ParseIds(codeCombined);
                if (ids.Length == 0)
                {
                    continue;
                }

                // Description is the <li> content with <code> tags removed, stripped of HTML
                var descRaw = Regex.Replace(liContent, @"<code[^>]*>[\s\S]*?(?:</code>|$)", "", RegexOptions.IgnoreCase, RegexTimeout);
                var desc = CleanDescription(descRaw);

                results.Add(new FeatureItem
                {
                    IsSelected = true,
                    Group = currentGroup,
                    BuildLabel = currentBuild,
                    Description = desc,
                    IDsDisplay = string.Join(", ", ids),
                    IDs = ids
                });
            }
        }

        return results;
    }

    /// <summary>
    /// Strips HTML tags, decodes HTML entities, and normalizes whitespace.
    /// </summary>
    public static string StripHtml(string input)
    {
        if (string.IsNullOrEmpty(input)) return string.Empty;
        var s = Regex.Replace(input, "<[^>]+>", " ", RegexOptions.None, RegexTimeout);
        s = WebUtility.HtmlDecode(s);
        s = Regex.Replace(s, @"\s+", " ", RegexOptions.None, RegexTimeout);
        return s.Trim();
    }

    /// <summary>
    /// Cleans and formats feature descriptions, falling back to "(No description)" if empty.
    /// </summary>
    public static string CleanDescription(string raw)
    {
        var cleaned = StripHtml(raw).Trim(' ', ':', '-', '.', '\t', '\r', '\n');
        return string.IsNullOrWhiteSpace(cleaned) ? "(No description)" : cleaned;
    }

    /// <summary>
    /// Extracts, validates (1,000,000 to 999,999,999), and deduplicates feature IDs from raw code text.
    /// </summary>
    public static long[] ParseIds(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return Array.Empty<long>();

        var cleaned = Regex.Replace(raw, @"[^\d,]", " ", RegexOptions.None, RegexTimeout);
        return Regex.Matches(cleaned, @"\b\d{7,9}\b", RegexOptions.None, RegexTimeout)
            .Select(m => long.TryParse(m.Value, out var v) ? v : 0)
            .Where(v => v >= 1_000_000 && v <= 999_999_999)
            .Distinct()
            .ToArray();
    }

    /// <summary>
    /// Maps section heading strings to standardized track groups.
    /// </summary>
    public static string MapGroup(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return "General";

        if (Regex.IsMatch(raw, @"Canary", RegexOptions.IgnoreCase, RegexTimeout)) return "Canary / Feature Platforms";
        if (Regex.IsMatch(raw, @"Feature\s*Platforms", RegexOptions.IgnoreCase, RegexTimeout)) return "Canary / Feature Platforms";
        if (Regex.IsMatch(raw, @"26H1", RegexOptions.IgnoreCase, RegexTimeout)) return "Canary / Feature Platforms";
        if (Regex.IsMatch(raw, @"26H2", RegexOptions.IgnoreCase, RegexTimeout)) return "26H2 Insider";
        if (Regex.IsMatch(raw, @"25H2", RegexOptions.IgnoreCase, RegexTimeout)) return "25H2 Insider";
        // Reject hero-like headings containing KB/Build to avoid collapsing to GA (e.g., "September 2026 Update KB5120998")
        if (Regex.IsMatch(raw, @"KB", RegexOptions.IgnoreCase, RegexTimeout) || Regex.IsMatch(raw, @"\bBuild\b", RegexOptions.IgnoreCase, RegexTimeout))
        {
            return raw.Trim();
        }
        if (Regex.IsMatch(raw, @"2026", RegexOptions.IgnoreCase, RegexTimeout)) return "GA 2026";
        if (Regex.IsMatch(raw, @"2025", RegexOptions.IgnoreCase, RegexTimeout)) return "GA 2025";

        return raw.Trim();
    }
}
