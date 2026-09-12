using System.Diagnostics;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace WingetStore.Services;

public class IconService
{
    private static readonly string CacheDir = AppPaths.Root;
    private static readonly string CacheFile = AppPaths.ScreenshotDbFile;
    private static readonly string IconsDir = AppPaths.IconsCacheDir;
    private const string DbUrl = "https://raw.githubusercontent.com/Devolutions/UniGetUI/main/WebBasedData/screenshot-database-v2.json";
    private Dictionary<string, string> _icons = new(StringComparer.OrdinalIgnoreCase);
    private Dictionary<string, List<string>> _screenshots = new(StringComparer.OrdinalIgnoreCase);
    private readonly HttpClient _httpClient;
    private readonly HashSet<string> _activeIds = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _failedIds = new(StringComparer.OrdinalIgnoreCase);
    private bool _isInitialized;
    public static IconService Instance { get; } = new();
    public event EventHandler? IconsUpdated;

    internal IconService(HttpClient? httpClient = null)
    {
        _httpClient = httpClient ?? CreateDefaultHttpClient();
    }

    private static HttpClient CreateDefaultHttpClient()
    {
        var client = new HttpClient();
        client.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36");
        return client;
    }

    public static string GetSafeIconFileName(string packageId, string extension = ".png")
    {
        if (string.IsNullOrWhiteSpace(packageId)) return "unknown" + extension;
        char[] invalidChars = Path.GetInvalidFileNameChars();
        char[] sanitized = packageId.Select(c => invalidChars.Contains(c) ? '_' : c).ToArray();
        string name = new string(sanitized).Replace("..", "_");
        return $"{name}{extension}";
    }

    public async Task InitializeAsync()
    {
        if (_isInitialized) return;
        try { Directory.CreateDirectory(CacheDir); Directory.CreateDirectory(IconsDir); } catch (Exception ex) { Debug.WriteLine($"Failed to create icon cache dirs: {ex.Message}"); }
        try { if (File.Exists(CacheFile)) { await LoadDatabaseAsync(CacheFile); } } catch (Exception ex) { Debug.WriteLine($"Failed to load icon cache: {ex.Message}"); }
        _isInitialized = true;
        _ = Task.Run(async () =>
        {
            try
            {
                if (!File.Exists(CacheFile) || IsCacheExpired(File.GetLastWriteTime(CacheFile), DateTime.Now, TimeSpan.FromHours(24))) { var data = await _httpClient.GetStringAsync(DbUrl); await File.WriteAllTextAsync(CacheFile, data); await LoadDatabaseAsync(CacheFile); NotifyIconsUpdated(); }
            }
            catch (Exception ex) { Debug.WriteLine($"Failed to download icon database: {ex.Message}"); }
        });
    }

    private void NotifyIconsUpdated()
    {
        App.Dispatch(() => IconsUpdated?.Invoke(this, EventArgs.Empty));
    }

    internal static (Dictionary<string, string> icons, Dictionary<string, List<string>> screenshots) ParseDatabaseJson(string json)
    {
        var newIcons = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var newScreenshots = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
        if (string.IsNullOrWhiteSpace(json)) return (newIcons, newScreenshots);
        try
        {
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.TryGetProperty("icons_and_screenshots", out var iconsNode))
            {
                foreach (var prop in iconsNode.EnumerateObject())
                {
                    if (prop.Value.TryGetProperty("icon", out var iconProp) && iconProp.ValueKind == JsonValueKind.String)
                    {
                        string iconUrl = iconProp.GetString() ?? "";
                        if (!string.IsNullOrEmpty(iconUrl)) newIcons[prop.Name] = iconUrl;
                    }
                    if (prop.Value.TryGetProperty("images", out var imagesProp) && imagesProp.ValueKind == JsonValueKind.Array)
                    {
                        var list = new List<string>();
                        foreach (var item in imagesProp.EnumerateArray())
                        {
                            if (item.ValueKind == JsonValueKind.String)
                            {
                                string imgUrl = item.GetString() ?? "";
                                if (!string.IsNullOrEmpty(imgUrl)) list.Add(imgUrl);
                            }
                        }
                        if (list.Count > 0) newScreenshots[prop.Name] = list;
                    }
                }
            }
        }
        catch
        {
            // Ignore malformed JSON
        }
        return (newIcons, newScreenshots);
    }

    internal static bool IsCacheExpired(DateTime lastWriteTime, DateTime currentTime, TimeSpan maxAge)
    {
        if (lastWriteTime > currentTime) return true;
        return (currentTime - lastWriteTime) > maxAge;
    }

    internal static string ExtractHomepageFromShowOutput(string showOutput)
    {
        if (string.IsNullOrWhiteSpace(showOutput)) return "";
        foreach (var line in showOutput.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries))
        {
            string trimmed = line.Trim();
            if (trimmed.StartsWith("Homepage:", StringComparison.OrdinalIgnoreCase))
            {
                return trimmed["Homepage:".Length..].Trim();
            }
        }
        return "";
    }

    internal static string ExtractDomainFromUrl(string url)
    {
        if (string.IsNullOrWhiteSpace(url)) return "";
        if (Uri.TryCreate(url, UriKind.Absolute, out var uri))
        {
            string domain = uri.Host;
            if (domain.StartsWith("www.", StringComparison.OrdinalIgnoreCase))
                domain = domain[4..];
            return domain;
        }
        return "";
    }

    internal static string GetHunterLogoUrl(string domain) => string.IsNullOrEmpty(domain) ? "" : $"https://logos.hunter.io/{domain}";
    internal static string GetGoogleFaviconUrl(string domain, int size = 128) => string.IsNullOrEmpty(domain) ? "" : $"https://www.google.com/s2/favicons?domain={domain}&sz={size}";

    internal static IEnumerable<string> GetIconUrlCandidates(string domain)
    {
        if (string.IsNullOrEmpty(domain)) yield break;
        yield return GetHunterLogoUrl(domain);
        yield return GetGoogleFaviconUrl(domain);
    }

    private async Task LoadDatabaseAsync(string filePath)
    {
        using var stream = File.OpenRead(filePath);
        using var reader = new StreamReader(stream);
        string json = await reader.ReadToEndAsync();
        var (newIcons, newScreenshots) = ParseDatabaseJson(json);
        lock (_icons) _icons = newIcons;
        lock (_screenshots) _screenshots = newScreenshots;
    }

    public string GetIconUrl(string packageId, string packageName)
    {
        if (string.IsNullOrEmpty(packageId)) return "";
        lock (_failedIds) { if (_failedIds.Contains(packageId)) return ""; }
        string? localFilePath = FindLocalIconFilePath(IconsDir, packageId);
        if (localFilePath != null) return new Uri(localFilePath).AbsoluteUri;
        string? remoteUrl = ResolveRemoteUrl(packageId, packageName);
        if (!string.IsNullOrEmpty(remoteUrl)) _ = DownloadIconAsync(packageId, remoteUrl);
        else _ = ResolveIconOnlineAsync(packageId);
        return "";
    }
    public List<string> GetScreenshots(string packageId, string packageName)
    {
        string pid = packageId ?? "";
        string pname = packageName ?? "";
        if (string.IsNullOrEmpty(pid) && string.IsNullOrEmpty(pname)) return [];
        lock (_screenshots) { if (!string.IsNullOrEmpty(pid) && _screenshots.TryGetValue(pid, out var cached)) return cached; if (!string.IsNullOrEmpty(pname) && _screenshots.TryGetValue(pname, out var cachedName)) return cachedName; }
        return [];
    }
    private string? ResolveRemoteUrl(string packageId, string packageName)
    {
        lock (_icons) { if (_icons.TryGetValue(packageId, out var url)) return url; if (_icons.TryGetValue(packageName, out var urlName)) return urlName; }
        return null;
    }

    public const long MaxIconSizeBytes = 5 * 1024 * 1024; // 5 MB

    private static readonly HashSet<string> AllowedContentTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "image/png",
        "image/jpeg",
        "image/jpg",
        "image/pjpeg",
        "image/gif",
        "image/webp",
        "image/x-icon",
        "image/vnd.microsoft.icon",
        "image/ico",
        "image/icon",
        "image/bmp",
        "image/svg+xml",
        "application/octet-stream"
    };

    internal static bool IsAllowedContentType(string? contentType)
    {
        if (string.IsNullOrWhiteSpace(contentType)) return false;
        string mediaType = contentType.Split(';')[0].Trim();
        return AllowedContentTypes.Contains(mediaType) || mediaType.StartsWith("image/", StringComparison.OrdinalIgnoreCase);
    }

    internal static string GetFileExtensionForContentType(string? mediaType)
    {
        if (string.IsNullOrWhiteSpace(mediaType)) return ".png";
        string mediaTypeLower = mediaType.Split(';')[0].Trim().ToLowerInvariant();
        return mediaTypeLower switch
        {
            "image/png" => ".png",
            "image/jpeg" or "image/jpg" or "image/pjpeg" => ".jpg",
            "image/gif" => ".gif",
            "image/webp" => ".webp",
            "image/x-icon" or "image/vnd.microsoft.icon" or "image/ico" or "image/icon" => ".ico",
            "image/bmp" => ".bmp",
            "image/svg+xml" => ".svg",
            _ => ".png"
        };
    }

    private static readonly string[] KnownIconExtensions = [".png", ".jpg", ".jpeg", ".webp", ".svg", ".gif", ".ico", ".bmp"];

    internal static string? FindLocalIconFilePath(string iconsDir, string packageId)
    {
        if (string.IsNullOrWhiteSpace(packageId)) return null;
        string basePath = Path.Combine(iconsDir, GetSafeIconFileName(packageId, ""));
        foreach (string extension in KnownIconExtensions)
        {
            string candidate = basePath + extension;
            if (File.Exists(candidate)) return candidate;
        }
        return null;
    }

    internal static bool IsValidImageHeader(byte[] headerBytes, int length)
    {
        if (headerBytes == null || length < 2) return false;

        // PNG: 89 50 4E 47
        if (length >= 4 && headerBytes[0] == 0x89 && headerBytes[1] == 0x50 && headerBytes[2] == 0x4E && headerBytes[3] == 0x47)
            return true;

        // JPEG: FF D8 FF
        if (length >= 3 && headerBytes[0] == 0xFF && headerBytes[1] == 0xD8 && headerBytes[2] == 0xFF)
            return true;

        // GIF: 47 49 46 38 (GIF8)
        if (length >= 4 && headerBytes[0] == 0x47 && headerBytes[1] == 0x49 && headerBytes[2] == 0x46 && headerBytes[3] == 0x38)
            return true;

        // BMP: 42 4D (BM)
        if (headerBytes[0] == 0x42 && headerBytes[1] == 0x4D)
            return true;

        // ICO: 00 00 01 00 or CUR: 00 00 02 00
        if (length >= 4 && headerBytes[0] == 0x00 && headerBytes[1] == 0x00 && (headerBytes[2] == 0x01 || headerBytes[2] == 0x02) && headerBytes[3] == 0x00)
            return true;

        // WEBP: RIFF...WEBP
        if (length >= 12 && headerBytes[0] == 0x52 && headerBytes[1] == 0x49 && headerBytes[2] == 0x46 && headerBytes[3] == 0x46 &&
            headerBytes[8] == 0x57 && headerBytes[9] == 0x45 && headerBytes[10] == 0x42 && headerBytes[11] == 0x50)
            return true;

        // SVG: text containing <svg
        try
        {
            string content = System.Text.Encoding.UTF8.GetString(headerBytes, 0, Math.Min(length, 1024));
            if (content.Contains("<svg", StringComparison.OrdinalIgnoreCase))
                return true;
        }
        catch
        {
            // Ignore encoding exceptions
        }

        return false;
    }

    private readonly SemaphoreSlim _downloadSemaphore = new(3, 3);
    internal static string GetTempFilePath(string localFilePath, string url)
    {
        byte[] hash = SHA256.HashData(Encoding.UTF8.GetBytes(url));
        return $"{localFilePath}.{Convert.ToHexString(hash)[..8]}.tmp";
    }

    private async Task<bool> DownloadToFileAsync(string url, string localFilePath)
    {
        string tempFilePath = GetTempFilePath(localFilePath, url);
        try
        {
            using var response = await _httpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead);
            if (!response.IsSuccessStatusCode) return false;

            // 1. Validate Content-Type
            string? contentType = response.Content.Headers.ContentType?.MediaType;
            if (!IsAllowedContentType(contentType)) return false;

            // Use the extension matching the actual content type for the final file.
            string finalFilePath = Path.ChangeExtension(localFilePath, GetFileExtensionForContentType(contentType));

            // 2. Validate Content-Length header if specified
            if (response.Content.Headers.ContentLength.HasValue && response.Content.Headers.ContentLength.Value > MaxIconSizeBytes)
                return false;

            using var stream = await response.Content.ReadAsStreamAsync();
            byte[] buffer = new byte[8192];
            int firstBytesRead = 0;
            long totalBytesRead = 0;
            byte[] headerBuffer = new byte[1024];

            using (var fileStream = File.Create(tempFilePath))
            {
                int bytesRead;
                while ((bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length)) > 0)
                {
                    totalBytesRead += bytesRead;
                    if (totalBytesRead > MaxIconSizeBytes)
                    {
                        fileStream.Dispose();
                        try { if (File.Exists(tempFilePath)) File.Delete(tempFilePath); } catch { }
                        return false;
                    }

                    if (firstBytesRead < headerBuffer.Length)
                    {
                        int toCopy = Math.Min(bytesRead, headerBuffer.Length - firstBytesRead);
                        Array.Copy(buffer, 0, headerBuffer, firstBytesRead, toCopy);
                        firstBytesRead += toCopy;
                    }

                    await fileStream.WriteAsync(buffer, 0, bytesRead);
                }
            }

            // 3. Verify magic bytes / image header
            if (!IsValidImageHeader(headerBuffer, firstBytesRead))
            {
                try { if (File.Exists(tempFilePath)) File.Delete(tempFilePath); } catch { }
                return false;
            }

            // 4. Atomic move to final destination
            string? dir = Path.GetDirectoryName(finalFilePath);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
            File.Move(tempFilePath, finalFilePath, overwrite: true);
            return true;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"DownloadToFileAsync failed for {url}: {ex.Message}");
            try { if (File.Exists(tempFilePath)) File.Delete(tempFilePath); } catch { }
            return false;
        }
    }

    internal async Task DownloadIconAsync(string packageId, string remoteUrl)
    {
        lock (_activeIds) { if (!_activeIds.Add(packageId)) return; }
        await _downloadSemaphore.WaitAsync();
        try
        {
            string localFilePath = Path.Combine(IconsDir, GetSafeIconFileName(packageId));
            if (await DownloadToFileAsync(remoteUrl, localFilePath)) NotifyIconsUpdated();
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"DownloadIconAsync failed for {packageId}: {ex.Message}");
            lock (_failedIds) _failedIds.Add(packageId);
        }
        finally
        {
            lock (_activeIds) _activeIds.Remove(packageId);
            _downloadSemaphore.Release();
        }
    }

    private readonly SemaphoreSlim _resolveSemaphore = new(2, 2);
    internal async Task ResolveIconOnlineAsync(string packageId)
    {
        if (packageId.StartsWith("Dummy.", StringComparison.OrdinalIgnoreCase)) return;
        lock (_activeIds) { if (!_activeIds.Add(packageId)) return; }
        await _resolveSemaphore.WaitAsync();
        try
        {
            string safePackageId = WingetService.EscapeArgument(packageId);
            string showOutput = await App.Winget.RunCommandAsync($"show {safePackageId} --accept-source-agreements");
            string homepage = ExtractHomepageFromShowOutput(showOutput);
            string domain = ExtractDomainFromUrl(homepage);
            string localFilePath = Path.Combine(IconsDir, GetSafeIconFileName(packageId));
            foreach (string candidateUrl in GetIconUrlCandidates(domain))
            {
                if (candidateUrl == GetHunterLogoUrl(domain))
                {
                    using var checkResponse = await _httpClient.SendAsync(new HttpRequestMessage(HttpMethod.Head, candidateUrl));
                    if (!checkResponse.IsSuccessStatusCode) continue;
                }
                if (await DownloadToFileAsync(candidateUrl, localFilePath)) NotifyIconsUpdated();
                break;
            }
        }
        catch (Exception ex) { Debug.WriteLine($"ResolveIconOnlineAsync failed for {packageId}: {ex.Message}"); }
        finally
        {
            lock (_activeIds) _activeIds.Remove(packageId);
            _resolveSemaphore.Release();
        }
    }

    internal static string NormalizePackageName(string packageName)
    {
        if (string.IsNullOrEmpty(packageName)) return "";
        string normalized = packageName.Replace("Microsoft.", "", StringComparison.OrdinalIgnoreCase).Replace(".", "").Replace(" ", "").Replace("-", "").Replace("(", "").Replace(")", "").Trim();
        int idx = normalized.IndexOf("for", StringComparison.OrdinalIgnoreCase); if (idx > 0) normalized = normalized[..idx].Trim();
        return normalized.Length > 2 ? normalized : packageName;
    }
}
