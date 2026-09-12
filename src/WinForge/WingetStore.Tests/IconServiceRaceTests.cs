using System.Net;
using System.Net.Http.Headers;

namespace WingetStore.Tests;

[Collection("IconServiceTests")]
public class IconServiceRaceTests
{
    private const string RacePackageId1 = "Mock.Race.App1";
    private const string DownloadUrl1 = "https://cdn.example.com/icons/Mock.Race.App1.png";
    private const string RacePackageId2 = "Mock.Race.App2";
    private const string DownloadUrl2 = "https://cdn.example.com/icons/Mock.Race.App2.png";

    private static readonly byte[] MinimalPng =
    [
        0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A,
        0x00, 0x00, 0x00, 0x0D, 0x49, 0x48, 0x44, 0x52,
        0x00, 0x00, 0x00, 0x01, 0x00, 0x00, 0x00, 0x01,
        0x08, 0x06, 0x00, 0x00, 0x00, 0x1F, 0x15, 0xC4,
        0x89, 0x00, 0x00, 0x00, 0x0D, 0x49, 0x44, 0x41,
        0x54, 0x78, 0x9C, 0x63, 0x00, 0x01, 0x00, 0x00,
        0x05, 0x00, 0x01, 0x0D, 0x0A, 0x2D, 0xB4, 0x00,
        0x00, 0x00, 0x00, 0x49, 0x45, 0x4E, 0x44, 0xAE,
        0x42, 0x60, 0x82
    ];

    private static string GetLocalIconPath(string pkgId) => Path.Combine(AppPaths.IconsCacheDir, IconService.GetSafeIconFileName(pkgId));

    private static void CleanupIconArtifacts(string pkgId)
    {
        try
        {
            if (!Directory.Exists(AppPaths.IconsCacheDir)) return;
            string localPath = GetLocalIconPath(pkgId);
            if (File.Exists(localPath)) File.Delete(localPath);
            string prefix = IconService.GetSafeIconFileName(pkgId) + ".";
            foreach (var tmp in Directory.GetFiles(AppPaths.IconsCacheDir, prefix + "*.tmp"))
            {
                try { File.Delete(tmp); } catch { }
            }
        }
        catch { }
    }

    [Fact]
    public void GetTempFilePath_KeysTempFileByUrl()
    {
        string local = Path.Combine(AppPaths.IconsCacheDir, "App.png");
        string t1 = IconService.GetTempFilePath(local, "https://example.com/icon1.png");
        string t2 = IconService.GetTempFilePath(local, "https://example.com/icon2.png");

        Assert.EndsWith(".tmp", t1);
        Assert.NotEqual(t1, t2);
        Assert.NotEqual(t1, local);
    }

    [Fact]
    public async Task DownloadAndResolve_Concurrent_SamePackage_OnlyOneDownloads()
    {
        CleanupIconArtifacts(RacePackageId1);
        string localIconPath = GetLocalIconPath(RacePackageId1);
        try
        {
            var handler = new ImageHttpHandler(MinimalPng, TimeSpan.FromMilliseconds(300));
            using var httpClient = new HttpClient(handler);
            var service = new IconService(httpClient);

            var downloadTask = service.DownloadIconAsync(RacePackageId1, DownloadUrl1);
            var resolveTask = service.ResolveIconOnlineAsync(RacePackageId1);

            await Task.WhenAll(downloadTask, resolveTask);

            Assert.Single(handler.RequestedUrls);
            Assert.Contains(DownloadUrl1, handler.RequestedUrls);
            Assert.True(File.Exists(localIconPath), "concurrent resolve path must not clobber the download's temp file");
            byte[] bytes = File.ReadAllBytes(localIconPath);
            Assert.True(IconService.IsValidImageHeader(bytes, bytes.Length));
        }
        finally
        {
            CleanupIconArtifacts(RacePackageId1);
        }
    }

    [Fact]
    public async Task ResolveThenDownload_Concurrent_SamePackage_OnlyResolveRuns()
    {
        CleanupIconArtifacts(RacePackageId2);
        string localIconPath = GetLocalIconPath(RacePackageId2);
        try
        {
            var handler = new ImageHttpHandler(MinimalPng, TimeSpan.FromMilliseconds(50));
            using var httpClient = new HttpClient(handler);
            var service = new IconService(httpClient);

            var resolveTask = service.ResolveIconOnlineAsync(RacePackageId2);
            var downloadTask = service.DownloadIconAsync(RacePackageId2, DownloadUrl2);

            await Task.WhenAll(downloadTask, resolveTask);

            Assert.DoesNotContain(DownloadUrl2, handler.RequestedUrls);
            Assert.NotEmpty(handler.RequestedUrls);
            Assert.True(File.Exists(localIconPath));
            byte[] bytes = File.ReadAllBytes(localIconPath);
            Assert.True(IconService.IsValidImageHeader(bytes, bytes.Length));
        }
        finally
        {
            CleanupIconArtifacts(RacePackageId2);
        }
    }

    private sealed class ImageHttpHandler : HttpMessageHandler
    {
        private readonly byte[] _imageBytes;
        private readonly TimeSpan _delay;

        public ImageHttpHandler(byte[] imageBytes, TimeSpan delay)
        {
            _imageBytes = imageBytes;
            _delay = delay;
        }

        public List<string> RequestedUrls { get; } = new();

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            lock (RequestedUrls) RequestedUrls.Add(request.RequestUri?.ToString() ?? "");
            await Task.Delay(_delay, cancellationToken);
            var content = new ByteArrayContent(_imageBytes);
            content.Headers.ContentType = new MediaTypeHeaderValue("image/png");
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = content };
        }
    }
}
