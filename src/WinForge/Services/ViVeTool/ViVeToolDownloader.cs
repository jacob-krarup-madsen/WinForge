using System;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace ViVeToolApp.Services;

/// <summary>
/// Downloads and extracts the latest ViVeTool release from GitHub.
/// </summary>
public class ViVeToolDownloader : IViVeToolDownloader
{
    private const string GitHubApiUrl = "https://api.github.com/repos/thebookisclosed/ViVe/releases/latest";
    private readonly HttpClient _httpClient;

    private static readonly TimeSpan RegexTimeout = TimeSpan.FromSeconds(5);

    public ViVeToolDownloader(HttpClient? httpClient = null)
    {
        _httpClient = httpClient ?? new HttpClient();
        if (_httpClient.DefaultRequestHeaders.UserAgent.Count == 0)
        {
            _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 ViVeToolApp/3.0");
        }
        if (_httpClient.Timeout == TimeSpan.FromSeconds(100))
        {
            _httpClient.Timeout = TimeSpan.FromSeconds(30);
        }
    }

    public string? ExtractZipUrlFromReleaseJson(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return null;
        }

        var match = Regex.Match(json, @"https://[^\s""<>]*(?:vivetool|ViVeTool)[^\s""<>]*\.zip", RegexOptions.IgnoreCase, RegexTimeout);
        return match.Success ? match.Value : null;
    }

    public async Task<string> DownloadAndExtractViVeToolAsync(
        string targetDirectory,
        IProgress<int>? progress = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(targetDirectory))
        {
            throw new ArgumentException("Target directory cannot be null or empty.", nameof(targetDirectory));
        }

        cancellationToken.ThrowIfCancellationRequested();
        progress?.Report(5);

        using var releaseResponse = await _httpClient.GetAsync(GitHubApiUrl, cancellationToken).ConfigureAwait(false);
        releaseResponse.EnsureSuccessStatusCode();

        var releaseJson = await releaseResponse.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        var zipUrl = ExtractZipUrlFromReleaseJson(releaseJson)
            ?? throw new InvalidOperationException("No ViVeTool zip asset found in latest GitHub release.");

        cancellationToken.ThrowIfCancellationRequested();
        progress?.Report(20);

        var tempZip = Path.Combine(Path.GetTempPath(), $"ViVeTool_{Guid.NewGuid():N}.zip");
        try
        {
            using (var zipResponse = await _httpClient.GetAsync(zipUrl, cancellationToken).ConfigureAwait(false))
            {
                zipResponse.EnsureSuccessStatusCode();
                var zipBytes = await zipResponse.Content.ReadAsByteArrayAsync(cancellationToken).ConfigureAwait(false);

                cancellationToken.ThrowIfCancellationRequested();
                progress?.Report(60);

                await File.WriteAllBytesAsync(tempZip, zipBytes, cancellationToken).ConfigureAwait(false);
            }

            cancellationToken.ThrowIfCancellationRequested();
            progress?.Report(80);

            Directory.CreateDirectory(targetDirectory);
            ZipFile.ExtractToDirectory(tempZip, targetDirectory, overwriteFiles: true);
            progress?.Report(100);

            var extractedExe = Path.Combine(targetDirectory, "vivetool.exe");
            if (!File.Exists(extractedExe))
            {
                throw new FileNotFoundException("vivetool.exe was not found inside the downloaded archive.", extractedExe);
            }

            return extractedExe;
        }
        finally
        {
            if (File.Exists(tempZip))
            {
                try
                {
                    File.Delete(tempZip);
                }
                catch
                {
                    // Best effort temp cleanup
                }
            }
        }
    }
}
