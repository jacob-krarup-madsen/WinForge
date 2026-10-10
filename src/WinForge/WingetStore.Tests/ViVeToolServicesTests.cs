using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.UI.Xaml.Controls;
using ViVeToolApp.Models;
using ViVeToolApp.Services;
using WingetStore.ViewModels;
using Xunit;

namespace WingetStore.Tests;

public sealed class ViVeToolServicesTests : IDisposable
{
    private readonly string _tempDir;

    public ViVeToolServicesTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "WinForgeViVeTests_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_tempDir))
            {
                Directory.Delete(_tempDir, recursive: true);
            }
        }
        catch
        {
            // Ignore cleanup errors
        }
    }

    [Fact]
    public void OfflineCatalog_ContainsAll118UniqueFeatureIdsAcrossAllFiveTracks()
    {
        var firstCall = OfflineCatalog.GetFeatures();
        var secondCall = OfflineCatalog.GetFallbackFeatures();

        Assert.NotSame(firstCall, secondCall);
        Assert.NotEmpty(firstCall);

        var distinctIds = firstCall
            .SelectMany(f => f.IDs)
            .Distinct()
            .ToList();

        Assert.Equal(118, distinctIds.Count);
        Assert.All(distinctIds, id => Assert.InRange(id, 10_000_000, 99_999_999));

        var groups = firstCall.Select(f => f.Group).Distinct().OrderBy(x => x).ToList();
        Assert.Equal(
            ["25H2 Insider", "26H2 Insider", "Canary / Feature Platforms", "GA 2025", "GA 2026"],
            groups);
    }

    [Fact]
    public void FeatureFilterService_FiltersBySearchAndGroupAndCalculatesSummary()
    {
        var filterService = new FeatureFilterService();
        var all = OfflineCatalog.GetFeatures();

        var ga2026 = filterService.Filter(all, searchQuery: null, groupFilter: "GA 2026").ToList();
        Assert.NotEmpty(ga2026);
        Assert.All(ga2026, f => Assert.Equal("GA 2026", f.Group));

        var dreamScene = filterService.Filter(all, searchQuery: "DreamScene", groupFilter: "All groups").ToList();
        Assert.Single(dreamScene);
        Assert.Contains(57645315L, dreamScene[0].IDs);

        var summary = filterService.CalculateSummary(dreamScene, all);
        Assert.Equal(all.Count, summary.TotalCount);
        Assert.Equal(1, summary.VisibleCount);
        Assert.Equal(118, summary.UniqueSelectedIdsCount);

        filterService.SetGroupSelection(all, "GA 2026", isSelected: false);
        Assert.All(all.Where(f => f.Group == "GA 2026"), f => Assert.False(f.IsSelected));

        filterService.SetSelection(all, isSelected: true);
        Assert.Equal(118, filterService.GetDistinctSelectedFeatureIds(all).Count);
    }

    [Fact]
    public void ViVeToolLocator_FindsExecutableInCustomPathAndToolsSubfolder()
    {
        var locator = new ViVeToolLocator();
        string toolsDir = Path.Combine(_tempDir, "Tools");
        Directory.CreateDirectory(toolsDir);
        string fakeExe = Path.Combine(toolsDir, "vivetool.exe");
        File.WriteAllText(fakeExe, "MZ");

        string? foundInSubDir = locator.LocateViVeTool(customBaseDirectory: _tempDir, pathEnvironment: string.Empty);
        Assert.Equal(fakeExe, foundInSubDir);

        string? foundDirect = locator.LocateViVeTool(customPath: fakeExe);
        Assert.Equal(fakeExe, foundDirect);
    }

    [Fact]
    public async Task ViVeToolRunner_ExecutesSingleAndBatchAndClassifiesStatuses()
    {
        var calls = new List<string>();
        var launcher = new FakeProcessLauncher((exe, args) =>
        {
            calls.Add(args);
            if (args.Contains("99999999", StringComparison.Ordinal))
            {
                return (1, "No feature found with ID 99999999", string.Empty);
            }

            return (0, "Successfully set feature configuration", string.Empty);
        });

        var runner = new ViVeToolRunner(launcher);
        Assert.Equal("/enable /id:61754985", runner.FormatArguments(ViVeExecutionMode.Enable, 61754985));
        Assert.Equal("/disable /id:61754985", runner.FormatArguments(ViVeExecutionMode.Disable, 61754985));

        string fakeExe = Path.Combine(_tempDir, "vivetool.exe");
        File.WriteAllText(fakeExe, "MZ");
        File.WriteAllText(Path.Combine(_tempDir, "Albacore.ViVe.dll"), "MZ");

        var whatIfRes = await runner.ExecuteFeatureAsync(fakeExe, 61754985, ViVeExecutionMode.Enable, whatIf: true);
        Assert.Equal(ViVeToolStatus.Success, whatIfRes.Status);
        Assert.Empty(calls);

        var batchRes = await runner.RunBatchAsync(
            fakeExe,
            [61754985L, 99999999L],
            ViVeExecutionMode.Enable,
            whatIf: false);

        Assert.Equal(2, batchRes.TotalProcessed);
        Assert.Equal(1, batchRes.SuccessCount);
        Assert.Equal(1, batchRes.SkippedCount);
        Assert.Equal(0, batchRes.ErrorCount);
    }

    [Fact]
    public void PureinfotechScraper_ParsesHtmlListAndViVeToolDownloaderExtractsZipUrl()
    {
        var scraper = new PureinfotechScraper();
        const string sampleHtml = """
            <div class="entry-content">
            <h2>Windows 11 26H2 features</h2>
            <p><strong>Build 26300.1000:</strong></p>
            <ul>
                <li>Start Menu Redesign: <code>61754985</code></li>
            </ul>
            <!-- CONTENT END -->
            </div>
            """;

        var parsed = scraper.ParseHtml(sampleHtml);
        Assert.NotEmpty(parsed);
        Assert.Contains(parsed, f => f.IDs.Contains(61754985L));

        var downloader = new ViVeToolDownloader();
        const string releaseJson = """
            {
              "assets": [
                {
                  "name": "ViVeTool-v0.3.4-IntelAmd.zip",
                  "browser_download_url": "https://github.com/thebookisclosed/ViVe/releases/download/v0.3.4/ViVeTool-v0.3.4-IntelAmd.zip"
                }
              ]
            }
            """;

        string? url = downloader.ExtractZipUrlFromReleaseJson(releaseJson);
        Assert.Equal("https://github.com/thebookisclosed/ViVe/releases/download/v0.3.4/ViVeTool-v0.3.4-IntelAmd.zip", url);
    }

    [Fact]
    public async Task FeaturesViewModel_LoadsCatalogFiltersAndExecutesBatch()
    {
        var origOverride = App.DispatcherOverride;
        App.DispatcherOverride = action => action();
        try
        {
            string fakeExe = Path.Combine(_tempDir, "vivetool.exe");
            File.WriteAllText(fakeExe, "MZ");
            File.WriteAllText(Path.Combine(_tempDir, "Albacore.ViVe.dll"), "MZ");

            var filterService = new FeatureFilterService();
            var runner = new ViVeToolRunner(new FakeProcessLauncher((_, _) => (0, "Success", string.Empty)));
            var locator = new FakeLocator(fakeExe);
            var downloader = new FakeDownloader(fakeExe);
            var scraper = new FakeScraper();

            var vm = new FeaturesViewModel(filterService, runner, locator, downloader, scraper);
            vm.LoadCatalog();

            Assert.NotEmpty(vm.FilteredFeatures);
            Assert.Contains("GA 2026", vm.GroupOptions);

            vm.SearchQuery = "DreamScene";
            Assert.Single(vm.FilteredFeatures);

            var batch = await vm.ExecuteFeaturesAsync(enable: true, vm.FilteredFeatures.ToList());
            Assert.NotNull(batch);
            Assert.Equal(1, batch.SuccessCount);
            Assert.Equal(FeatureRunStatus.Success, vm.FilteredFeatures[0].LastStatus);
            Assert.Equal(InfoBarSeverity.Success, vm.InfoBarSeverity);

            await vm.RefreshCatalogAsync();
            Assert.True(vm.IsInfoBarOpen);

            await vm.DownloadViVeToolAsync();
            Assert.Equal(InfoBarSeverity.Success, vm.InfoBarSeverity);
        }
        finally
        {
            App.DispatcherOverride = origOverride;
        }
    }

    private sealed class FakeProcessLauncher(Func<string, string, (int ExitCode, string Output, string Error)> handler) : IProcessLauncher
    {
        public Task<(int ExitCode, string Output, string Error)> RunProcessAsync(string fileName, string arguments, CancellationToken cancellationToken = default)
            => Task.FromResult(handler(fileName, arguments));
    }

    private sealed class FakeLocator(string path) : IViVeToolLocator
    {
        public string? LocateViVeTool(string? customBaseDirectory = null, string? customPath = null, string? pathEnvironment = null) => path;
    }

    private sealed class FakeDownloader(string path) : IViVeToolDownloader
    {
        public Task<string> DownloadAndExtractViVeToolAsync(string targetDirectory, IProgress<int>? progress = null, CancellationToken cancellationToken = default)
            => Task.FromResult(path);

        public string? ExtractZipUrlFromReleaseJson(string json) => null;
    }

    private sealed class FakeScraper : IFeatureScraper
    {
        public Task<List<FeatureItem>> FetchAndParseAsync(string? customUrl = null, CancellationToken cancellationToken = default)
            => Task.FromResult(OfflineCatalog.GetFeatures());

        public List<FeatureItem> ParseHtml(string html) => [];

        public List<FeatureItem> GetOfflineFallback() => OfflineCatalog.GetFeatures();
    }
}
