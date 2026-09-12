using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ViVeToolApp.Models;

namespace ViVeToolApp.Services;

/// <summary>
/// Contract for scraping and parsing feature IDs from web sources or fallback catalogs.
/// </summary>
public interface IFeatureScraper
{
    /// <summary>
    /// Asynchronously fetches and parses features from the target URL (or default Pureinfotech URL).
    /// </summary>
    /// <param name="customUrl">Optional custom URL to fetch from; if null/empty, uses default.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A list of parsed <see cref="FeatureItem"/> objects.</returns>
    Task<List<FeatureItem>> FetchAndParseAsync(string? customUrl = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Parses raw HTML markup into a list of <see cref="FeatureItem"/> objects.
    /// </summary>
    /// <param name="html">The raw HTML content to parse.</param>
    /// <returns>A list of parsed features.</returns>
    List<FeatureItem> ParseHtml(string html);

    /// <summary>
    /// Gets the built-in offline fallback catalog of features.
    /// </summary>
    /// <returns>A list of default offline <see cref="FeatureItem"/> objects.</returns>
    List<FeatureItem> GetOfflineFallback();
}
