using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;

namespace ViVeToolApp.Models;

/// <summary>
/// Per-feature execution status for visibility tracking.
/// </summary>
public enum FeatureRunStatus
{
    NotRun = 0,
    Pending = 1,
    Success = 2,
    Skipped = 3,
    Error = 4
}

/// <summary>
/// Represents a Windows Feature toggle entry with associated ViVeTool IDs and metadata.
/// Implements <see cref="INotifyPropertyChanged"/> for UI data binding.
/// </summary>
public class FeatureItem : INotifyPropertyChanged
{
    private bool _isSelected = true;
    private FeatureRunStatus _lastStatus = FeatureRunStatus.NotRun;
    private string _lastMessage = string.Empty;
    private int? _lastExitCode;
    private DateTime? _lastRunTime;
    private bool _isPending;
    private string _group = string.Empty;
    private string _buildLabel = string.Empty;
    private string _description = string.Empty;
    private string _idsDisplay = string.Empty;

    private static readonly Brush SuccessBrush = new SolidColorBrush(Microsoft.UI.Colors.Green);
    private static readonly Brush ErrorBrush = new SolidColorBrush(Microsoft.UI.Colors.Red);
    private static readonly Brush SkippedBrush = new SolidColorBrush(Microsoft.UI.Colors.Orange);
    private static readonly Brush PendingBrush = new SolidColorBrush(Microsoft.UI.Colors.Goldenrod);
    private static readonly Brush DefaultBrush = new SolidColorBrush(Microsoft.UI.Colors.Gray);

    /// <summary>
    /// Gets or sets a value indicating whether this feature is selected for batch operations.
    /// </summary>
    public bool IsSelected
    {
        get => _isSelected;
        set
        {
            if (_isSelected != value)
            {
                _isSelected = value;
                OnPropertyChanged();
            }
        }
    }

    /// <summary>
    /// Gets or sets the track or release group (e.g. "GA 2026", "26H2 Insider").
    /// </summary>
    public string Group
    {
        get => _group;
        set
        {
            var newVal = value ?? string.Empty;
            if (_group != newVal)
            {
                _group = newVal;
                OnPropertyChanged();
            }
        }
    }

    /// <summary>
    /// Gets or sets the build label or date (e.g. "Build 26300.8697", "Sep 2026").
    /// </summary>
    public string BuildLabel
    {
        get => _buildLabel;
        set
        {
            var newVal = value ?? string.Empty;
            if (_buildLabel != newVal)
            {
                _buildLabel = newVal;
                OnPropertyChanged();
            }
        }
    }

    /// <summary>
    /// Gets or sets the descriptive summary of the feature.
    /// </summary>
    public string Description
    {
        get => _description;
        set
        {
            var newVal = value ?? string.Empty;
            if (_description != newVal)
            {
                _description = newVal;
                OnPropertyChanged();
            }
        }
    }

    /// <summary>
    /// Gets or sets the display-formatted string of feature IDs (e.g. "61267302, 61344081").
    /// </summary>
    public string IDsDisplay
    {
        get => _idsDisplay;
        set
        {
            var newVal = value ?? string.Empty;
            if (_idsDisplay != newVal)
            {
                _idsDisplay = newVal;
                OnPropertyChanged();
            }
        }
    }

    /// <summary>
    /// Gets or sets the parsed numeric feature IDs.
    /// </summary>
    public long[] IDs { get; set; } = Array.Empty<long>();

    /// <summary>
    /// Last execution status for this feature row.
    /// </summary>
    public FeatureRunStatus LastStatus
    {
        get => _lastStatus;
        set
        {
            if (_lastStatus != value)
            {
                _lastStatus = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(StatusGlyph));
                OnPropertyChanged(nameof(StatusLabel));
                OnPropertyChanged(nameof(StatusToolTip));
                OnPropertyChanged(nameof(StatusAccessibilityName));
                OnPropertyChanged(nameof(StatusBrush));
                OnPropertyChanged(nameof(StatusBrushKey));
                OnPropertyChanged(nameof(LastRunTimeText));
            }
        }
    }

    /// <summary>
    /// Last execution message / output.
    /// </summary>
    public string LastMessage
    {
        get => _lastMessage;
        set
        {
            if (_lastMessage != value)
            {
                _lastMessage = value ?? string.Empty;
                OnPropertyChanged();
                OnPropertyChanged(nameof(StatusToolTip));
                OnPropertyChanged(nameof(StatusAccessibilityName));
            }
        }
    }

    /// <summary>
    /// Last exit code if available.
    /// </summary>
    public int? LastExitCode
    {
        get => _lastExitCode;
        set
        {
            if (_lastExitCode != value)
            {
                _lastExitCode = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(StatusToolTip));
            }
        }
    }

    /// <summary>
    /// Timestamp of last run for this feature.
    /// </summary>
    public DateTime? LastRunTime
    {
        get => _lastRunTime;
        set
        {
            if (_lastRunTime != value)
            {
                _lastRunTime = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(LastRunTimeText));
                OnPropertyChanged(nameof(StatusToolTip));
            }
        }
    }

    /// <summary>
    /// Indicates whether this feature is currently pending execution.
    /// </summary>
    public bool IsPending
    {
        get => _isPending;
        set
        {
            if (_isPending != value)
            {
                _isPending = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(StatusGlyph));
                OnPropertyChanged(nameof(StatusLabel));
                OnPropertyChanged(nameof(StatusToolTip));
                OnPropertyChanged(nameof(StatusAccessibilityName));
            }
        }
    }

    /// <summary>
    /// Glyph for status icon (Segoe MDL2).
    /// </summary>
    public string StatusGlyph => LastStatus switch
    {
        FeatureRunStatus.NotRun => "—", // dash / not run
        FeatureRunStatus.Pending => "\uE72C", // Sync / pending
        FeatureRunStatus.Success => "\uE73E", // CheckMark
        FeatureRunStatus.Skipped => "\uE7BA", // Warning / skipped
        FeatureRunStatus.Error => "\uE783", // Error
        _ => "—"
    };

    /// <summary>
    /// Human-readable status label.
    /// </summary>
    public string StatusLabel => LastStatus switch
    {
        FeatureRunStatus.NotRun => "—",
        FeatureRunStatus.Pending => "Pending",
        FeatureRunStatus.Success => "Success",
        FeatureRunStatus.Skipped => "Skipped",
        FeatureRunStatus.Error => "Error",
        _ => "—"
    };

    /// <summary>
    /// Tooltip text including status, message, exit code and time.
    /// </summary>
    public string StatusToolTip
    {
        get
        {
            if (LastStatus == FeatureRunStatus.NotRun)
            {
                return "Not run yet";
            }

            var basePart = StatusLabel;
            if (!string.IsNullOrWhiteSpace(LastMessage))
            {
                basePart += $" — {LastMessage}";
            }
            if (LastExitCode.HasValue)
            {
                basePart += $" (exit {LastExitCode})";
            }
            if (LastRunTime.HasValue)
            {
                basePart += $" at {LastRunTime:HH:mm:ss}";
            }
            return basePart;
        }
    }

    /// <summary>
    /// Accessibility name for screen readers.
    /// </summary>
    public string StatusAccessibilityName
    {
        get
        {
            var desc = string.IsNullOrWhiteSpace(Description) ? "Feature" : Description;
            if (LastStatus == FeatureRunStatus.NotRun)
            {
                return $"{desc} status {StatusLabel}";
            }
            var msg = string.IsNullOrWhiteSpace(LastMessage) ? StatusLabel : $"{StatusLabel} {LastMessage}";
            return $"{desc} status {msg}";
        }
    }

    /// <summary>
    /// Formatted last run time text.
    /// </summary>
    public string LastRunTimeText => LastRunTime?.ToString("HH:mm:ss") ?? "—";

    /// <summary>
    /// Resource key for the status brush.
    /// </summary>
    public string StatusBrushKey => LastStatus switch
    {
        FeatureRunStatus.Success => "SystemFillColorSuccessBrush",
        FeatureRunStatus.Error => "SystemFillColorCriticalBrush",
        FeatureRunStatus.Skipped => "SystemFillColorCautionBrush",
        FeatureRunStatus.Pending => "SystemFillColorAttentionBrush",
        _ => "TextFillColorSecondaryBrush"
    };

    /// <summary>
    /// Brush for status foreground, resolved from application resources.
    /// </summary>
    public Brush StatusBrush
    {
        get
        {
            var key = StatusBrushKey;
            try
            {
                if (Application.Current != null && Application.Current.Resources.TryGetValue(key, out var res) && res is Brush b)
                {
                    return b;
                }
            }
            catch
            {
                // Fallback below
            }
            // Fallback brushes without relying on resources (avoid null) - cached statics to avoid allocation bomb
            return LastStatus switch
            {
                FeatureRunStatus.Success => SuccessBrush,
                FeatureRunStatus.Error => ErrorBrush,
                FeatureRunStatus.Skipped => SkippedBrush,
                FeatureRunStatus.Pending => PendingBrush,
                _ => DefaultBrush
            };
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
