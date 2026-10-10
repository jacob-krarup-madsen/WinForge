using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.UI.Xaml.Controls;
using ViVeToolApp.Models;
using ViVeToolApp.Services;
using WingetStore.Services;

namespace WingetStore.ViewModels;

/// <summary>
/// ViewModel backing <see cref="Pages.FeaturesPage"/> using <see cref="IViVeToolRunner"/>, <see cref="IFeatureFilterService"/>,
/// <see cref="IViVeToolLocator"/>, <see cref="IViVeToolDownloader"/>, <see cref="IFeatureScraper"/>, and <see cref="OfflineCatalog"/>.
/// </summary>
public partial class FeaturesViewModel : ObservableObject
{
    private readonly IFeatureFilterService _filterService;
    private readonly IViVeToolRunner _runner;
    private readonly IViVeToolLocator _locator;
    private readonly IViVeToolDownloader _downloader;
    private readonly IFeatureScraper _scraper;
    private List<FeatureItem> _allFeatures = [];

    /// <summary>
    /// Initializes a new instance of the <see cref="FeaturesViewModel"/> class.
    /// </summary>
    /// <param name="filterService">Feature search and group filtering service.</param>
    /// <param name="runner">ViVeTool batch and single feature execution runner.</param>
    /// <param name="locator">ViVeTool binary locator.</param>
    /// <param name="downloader">ViVeTool GitHub release downloader.</param>
    /// <param name="scraper">Pureinfotech live feature catalog scraper.</param>
    public FeaturesViewModel(
        IFeatureFilterService filterService,
        IViVeToolRunner runner,
        IViVeToolLocator locator,
        IViVeToolDownloader downloader,
        IFeatureScraper scraper)
    {
        ArgumentNullException.ThrowIfNull(filterService);
        ArgumentNullException.ThrowIfNull(runner);
        ArgumentNullException.ThrowIfNull(locator);
        ArgumentNullException.ThrowIfNull(downloader);
        ArgumentNullException.ThrowIfNull(scraper);

        _filterService = filterService;
        _runner = runner;
        _locator = locator;
        _downloader = downloader;
        _scraper = scraper;
    }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsIdle))]
    public partial bool IsBusy { get; set; }

    /// <summary>Gets a value indicating whether no ViVeTool operation is currently running.</summary>
    public bool IsIdle => !IsBusy;

    [ObservableProperty]
    public partial string SearchQuery { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string GroupFilter { get; set; } = "All groups";

    [ObservableProperty]
    public partial ObservableCollection<FeatureItem> FilteredFeatures { get; set; } = [];

    [ObservableProperty]
    public partial List<string> GroupOptions { get; set; } = ["All groups"];

    [ObservableProperty]
    public partial string SummaryText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string InfoBarTitle { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string InfoBarMessage { get; set; } = string.Empty;

    [ObservableProperty]
    public partial InfoBarSeverity InfoBarSeverity { get; set; } = InfoBarSeverity.Informational;

    [ObservableProperty]
    public partial bool IsInfoBarOpen { get; set; }

    /// <summary>Gets the complete unfiltered catalog currently loaded.</summary>
    public IReadOnlyList<FeatureItem> AllFeatures => _allFeatures;

    partial void OnSearchQueryChanged(string value) => ApplyFilter();

    partial void OnGroupFilterChanged(string value) => ApplyFilter();

    /// <summary>
    /// Loads the built-in 118-feature offline catalog and applies the active search/group filter.
    /// </summary>
    public void LoadCatalog()
    {
        _allFeatures = OfflineCatalog.GetFeatures();
        RebuildGroupOptions();
        ApplyFilter();
    }

    /// <summary>
    /// Filters <see cref="AllFeatures"/> according to <see cref="SearchQuery"/> and <see cref="GroupFilter"/>.
    /// </summary>
    public void ApplyFilter()
    {
        var visible = _filterService.Filter(_allFeatures, SearchQuery, GroupFilter).ToList();
        var summary = _filterService.CalculateSummary(visible, _allFeatures);
        App.Dispatch(() =>
        {
            FilteredFeatures = new ObservableCollection<FeatureItem>(visible);
            SummaryText = summary.SummaryText;
        });
    }

    [RelayCommand]
    public async Task RefreshCatalogAsync(CancellationToken cancellationToken = default)
    {
        if (IsBusy)
        {
            return;
        }

        App.Dispatch(() =>
        {
            IsBusy = true;
            IsInfoBarOpen = false;
        });

        try
        {
            var scraped = await _scraper.FetchAndParseAsync(customUrl: null, cancellationToken).ConfigureAwait(false);
            if (scraped is { Count: > 0 })
            {
                // Merge scraped features with offline catalog so all 118 baseline IDs remain available
                var merged = new List<FeatureItem>(scraped);
                var existingIds = new HashSet<long>(scraped.SelectMany(f => f.IDs));
                foreach (var offline in OfflineCatalog.GetFeatures())
                {
                    if (offline.IDs.Any(id => !existingIds.Contains(id)))
                    {
                        merged.Add(offline);
                    }
                }

                _allFeatures = merged;
                RebuildGroupOptions();
                ApplyFilter();
                int uniqueCount = _allFeatures.SelectMany(f => f.IDs).Distinct().Count();
                SetInfoBar(
                    InfoBarSeverity.Success,
                    "Catalog Refreshed Online",
                    $"Loaded {_allFeatures.Count} feature entries ({uniqueCount} unique velocity IDs).");
            }
            else
            {
                LoadCatalog();
                int uniqueCount = _allFeatures.SelectMany(f => f.IDs).Distinct().Count();
                SetInfoBar(
                    InfoBarSeverity.Informational,
                    "Offline Catalog Loaded",
                    $"Loaded {_allFeatures.Count} built-in feature entries ({uniqueCount} unique velocity IDs).");
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            LogService.LogError("RefreshCatalogAsync failed; falling back to offline catalog", ex);
            LoadCatalog();
            int uniqueCount = _allFeatures.SelectMany(f => f.IDs).Distinct().Count();
            SetInfoBar(
                InfoBarSeverity.Informational,
                "Offline Catalog Active",
                $"Online refresh unavailable ({ex.Message}). {uniqueCount} built-in velocity IDs ready.");
        }
        finally
        {
            App.Dispatch(() => IsBusy = false);
        }
    }

    /// <summary>
    /// Executes ViVeTool <c>/enable</c> or <c>/disable</c> on the specified feature items (or all checked items if none passed).
    /// </summary>
    /// <param name="enable">Whether to enable (<see langword="true"/>) or disable (<see langword="false"/>) the features.</param>
    /// <param name="selectedItems">Optional explicit UI selection.</param>
    /// <param name="whatIf">Whether to simulate execution without invoking ViVeTool.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The <see cref="ViVeBatchResult"/>, or <see langword="null"/> if no features were selected or ViVeTool was unavailable.</returns>
    public async Task<ViVeBatchResult?> ExecuteFeaturesAsync(
        bool enable,
        IReadOnlyCollection<FeatureItem>? selectedItems = null,
        bool whatIf = false,
        CancellationToken cancellationToken = default)
    {
        if (IsBusy)
        {
            return null;
        }

        var targets = (selectedItems is { Count: > 0 }
            ? selectedItems
            : _allFeatures.Where(f => f.IsSelected).ToList())
            .Where(f => f is not null)
            .ToList();

        if (targets.Count == 0)
        {
            SetInfoBar(InfoBarSeverity.Warning, "No Features Selected", "Select one or more feature velocity entries before running Enable or Disable.");
            return null;
        }

        string? vivePath = _locator.LocateViVeTool();
        if (!whatIf && (string.IsNullOrWhiteSpace(vivePath) || !File.Exists(vivePath)))
        {
            SetInfoBar(
                InfoBarSeverity.Error,
                "ViVeTool Not Found",
                "ViVeTool.exe was not found in the application directory, PATH, or AppData. Use the Download ViVeTool button to install it.");
            return null;
        }

        App.Dispatch(() =>
        {
            IsBusy = true;
            IsInfoBarOpen = false;
            foreach (var item in targets)
            {
                item.IsPending = true;
                item.LastStatus = FeatureRunStatus.Pending;
            }
        });

        try
        {
            var mode = enable ? ViVeExecutionMode.Enable : ViVeExecutionMode.Disable;
            var ids = targets.SelectMany(f => f.IDs).Where(id => id > 0).Distinct().OrderBy(x => x).ToList();

            var batchResult = await _runner.RunBatchAsync(
                vivePath ?? "vivetool.exe",
                ids,
                mode,
                whatIf,
                progress: null,
                cancellationToken).ConfigureAwait(false);

            var resultById = batchResult.Results.ToDictionary(r => r.FeatureId, r => r);
            var now = DateTime.Now;

            App.Dispatch(() =>
            {
                foreach (var item in targets)
                {
                    item.IsPending = false;
                    item.LastRunTime = now;

                    var matching = item.IDs
                        .Where(resultById.ContainsKey)
                        .Select(id => resultById[id])
                        .ToList();

                    if (matching.Count == 0)
                    {
                        item.LastStatus = FeatureRunStatus.Skipped;
                        item.LastMessage = "No valid feature ID";
                    }
                    else if (matching.Any(r => r.Status == ViVeToolStatus.Error))
                    {
                        var firstErr = matching.First(r => r.Status == ViVeToolStatus.Error);
                        item.LastStatus = FeatureRunStatus.Error;
                        item.LastExitCode = firstErr.ExitCode;
                        item.LastMessage = firstErr.ErrorMessage;
                    }
                    else if (matching.All(r => r.Status == ViVeToolStatus.UnsupportedOrNotFound))
                    {
                        var firstSkip = matching[0];
                        item.LastStatus = FeatureRunStatus.Skipped;
                        item.LastExitCode = firstSkip.ExitCode;
                        item.LastMessage = "Unsupported on this build";
                    }
                    else
                    {
                        var firstOk = matching[0];
                        item.LastStatus = FeatureRunStatus.Success;
                        item.LastExitCode = firstOk.ExitCode;
                        item.LastMessage = enable ? "Enabled" : "Disabled";
                    }
                }
            });

            string verbPast = enable ? "enabled" : "disabled";
            if (batchResult.ErrorCount == 0)
            {
                SetInfoBar(
                    InfoBarSeverity.Success,
                    "Velocity Features Updated",
                    $"Successfully {verbPast} {batchResult.SuccessCount} ID(s) ({batchResult.SkippedCount} skipped). A system restart may be required.");
            }
            else if (batchResult.SuccessCount > 0)
            {
                SetInfoBar(
                    InfoBarSeverity.Warning,
                    "Velocity Features Partially Updated",
                    $"Succeeded: {batchResult.SuccessCount}, skipped: {batchResult.SkippedCount}, errors: {batchResult.ErrorCount}. Running as Administrator may be required.");
            }
            else
            {
                string firstError = batchResult.Results.FirstOrDefault(r => !string.IsNullOrWhiteSpace(r.ErrorMessage))?.ErrorMessage
                    ?? "Ensure WinForge is running as Administrator.";
                SetInfoBar(
                    InfoBarSeverity.Error,
                    "Operation Failed",
                    $"Failed to apply {batchResult.ErrorCount} feature ID(s): {firstError}");
            }

            return batchResult;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            LogService.LogError("ExecuteFeaturesAsync failed", ex);
            SetInfoBar(InfoBarSeverity.Error, "Execution Error", ex.Message);
            return null;
        }
        finally
        {
            App.Dispatch(() =>
            {
                foreach (var item in targets)
                {
                    item.IsPending = false;
                }

                IsBusy = false;
            });
        }
    }

    [RelayCommand]
    public async Task DownloadViVeToolAsync(CancellationToken cancellationToken = default)
    {
        if (IsBusy)
        {
            return;
        }

        App.Dispatch(() =>
        {
            IsBusy = true;
            IsInfoBarOpen = false;
        });

        try
        {
            string targetDir = AppPaths.Root;
            string exePath = await _downloader.DownloadAndExtractViVeToolAsync(targetDir, progress: null, cancellationToken).ConfigureAwait(false);
            SetInfoBar(InfoBarSeverity.Success, "ViVeTool Ready", $"Installed ViVeTool to '{exePath}'.");
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            LogService.LogError("DownloadViVeToolAsync failed", ex);
            SetInfoBar(InfoBarSeverity.Error, "ViVeTool Download Failed", ex.Message);
        }
        finally
        {
            App.Dispatch(() => IsBusy = false);
        }
    }

    private void RebuildGroupOptions()
    {
        var groups = new List<string> { "All groups" };
        groups.AddRange(_filterService.GetDistinctGroups(_allFeatures));
        App.Dispatch(() => GroupOptions = groups);
    }

    private void SetInfoBar(InfoBarSeverity severity, string title, string message)
    {
        App.Dispatch(() =>
        {
            InfoBarSeverity = severity;
            InfoBarTitle = title;
            InfoBarMessage = message;
            IsInfoBarOpen = true;
        });
    }
}
