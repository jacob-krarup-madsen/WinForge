using System;
using System.Threading;
using System.Threading.Tasks;

namespace ViVeToolApp.Services;

/// <summary>
/// Service contract for downloading and extracting the latest ViVeTool release from GitHub.
/// </summary>
public interface IViVeToolDownloader
{
    /// <summary>
    /// Downloads the latest ViVeTool archive from GitHub, extracts it to the target directory, and returns the path to vivetool.exe.
    /// </summary>
    Task<string> DownloadAndExtractViVeToolAsync(
        string targetDirectory,
        IProgress<int>? progress = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Parses GitHub release JSON to find the browser download URL of the ViVeTool zip archive.
    /// </summary>
    string? ExtractZipUrlFromReleaseJson(string json);
}
