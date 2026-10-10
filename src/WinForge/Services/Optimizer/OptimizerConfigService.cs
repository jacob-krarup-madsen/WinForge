using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace WingetStore.Services.Optimizer;

/// <summary>
/// Represents a user-customizable and serializable Windows optimization configuration.
/// </summary>
/// <param name="EnabledTweakIds">Set of tweak identifiers enabled for execution.</param>
/// <param name="IncludeSecurityReducingTweaks">Whether security-reducing tweaks (HVCI, VBS, Hypervisor, LSA PPL) are opted into.</param>
/// <param name="CreateRestorePoint">Whether to create a System Restore point before applying optimizations.</param>
/// <param name="SchemaVersion">Configuration schema version.</param>
public sealed record OptimizationConfig(
    IReadOnlySet<string> EnabledTweakIds,
    bool IncludeSecurityReducingTweaks = false,
    bool CreateRestorePoint = true,
    int SchemaVersion = 1);

/// <summary>
/// Service for creating, importing, and exporting <see cref="OptimizationConfig"/> profiles.
/// </summary>
public interface IOptimizerConfigService
{
    /// <summary>Creates the default optimization configuration based on <see cref="TweakCatalog.All"/>.</summary>
    /// <param name="includeSecurityReducing">Whether security-reducing tweaks should be enabled by default.</param>
    /// <returns>The default <see cref="OptimizationConfig"/>.</returns>
    OptimizationConfig CreateDefault(bool includeSecurityReducing = false);

    /// <summary>Serializes a configuration to indented JSON text.</summary>
    /// <param name="config">Configuration to serialize.</param>
    /// <returns>JSON string representation.</returns>
    string Serialize(OptimizationConfig config);

    /// <summary>Deserializes a configuration from JSON text.</summary>
    /// <param name="json">JSON string representation.</param>
    /// <returns>The parsed <see cref="OptimizationConfig"/>.</returns>
    OptimizationConfig Deserialize(string json);

    /// <summary>Exports a configuration to a file on disk.</summary>
    /// <param name="config">Configuration to export.</param>
    /// <param name="filePath">Target file path.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task representing the asynchronous write operation.</returns>
    Task ExportAsync(OptimizationConfig config, string filePath, CancellationToken cancellationToken = default);

    /// <summary>Imports a configuration from a JSON file on disk.</summary>
    /// <param name="filePath">Source file path.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The imported <see cref="OptimizationConfig"/>.</returns>
    Task<OptimizationConfig> ImportAsync(string filePath, CancellationToken cancellationToken = default);
}

/// <summary>
/// Default JSON-backed implementation of <see cref="IOptimizerConfigService"/>.
/// </summary>
public sealed class OptimizerConfigService : IOptimizerConfigService
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
    };

    private readonly IReadOnlyList<Tweak> _catalog;

    /// <summary>
    /// Initializes a new instance of the <see cref="OptimizerConfigService"/> class.
    /// </summary>
    /// <param name="catalog">Optional tweak catalog override for testing.</param>
    public OptimizerConfigService(IReadOnlyList<Tweak>? catalog = null)
    {
        _catalog = catalog ?? TweakCatalog.All;
    }

    /// <inheritdoc />
    public OptimizationConfig CreateDefault(bool includeSecurityReducing = false)
    {
        var enabled = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var tweak in _catalog)
        {
            if (!tweak.Info.ReducesSecurity || includeSecurityReducing)
            {
                enabled.Add(tweak.Info.Id);
            }
        }

        return new OptimizationConfig(
            EnabledTweakIds: enabled,
            IncludeSecurityReducingTweaks: includeSecurityReducing,
            CreateRestorePoint: true,
            SchemaVersion: 1);
    }

    /// <inheritdoc />
    public string Serialize(OptimizationConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);

        var dto = new ConfigDto
        {
            SchemaVersion = config.SchemaVersion,
            IncludeSecurityReducingTweaks = config.IncludeSecurityReducingTweaks,
            CreateRestorePoint = config.CreateRestorePoint,
            Tweaks = _catalog.ToDictionary(
                t => t.Info.Id,
                t => config.EnabledTweakIds.Contains(t.Info.Id),
                StringComparer.OrdinalIgnoreCase),
        };

        foreach (string id in config.EnabledTweakIds)
        {
            if (!dto.Tweaks.ContainsKey(id))
            {
                dto.Tweaks[id] = true;
            }
        }

        return JsonSerializer.Serialize(dto, SerializerOptions);
    }

    /// <inheritdoc />
    public OptimizationConfig Deserialize(string json)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(json);

        var dto = JsonSerializer.Deserialize<ConfigDto>(json, SerializerOptions)
            ?? throw new InvalidDataException("Configuration JSON deserialized to null.");

        var enabled = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (dto.Tweaks is not null)
        {
            foreach (var (id, isEnabled) in dto.Tweaks)
            {
                if (isEnabled && !string.IsNullOrWhiteSpace(id))
                {
                    enabled.Add(id);
                }
            }
        }

        return new OptimizationConfig(
            EnabledTweakIds: enabled,
            IncludeSecurityReducingTweaks: dto.IncludeSecurityReducingTweaks,
            CreateRestorePoint: dto.CreateRestorePoint,
            SchemaVersion: dto.SchemaVersion <= 0 ? 1 : dto.SchemaVersion);
    }

    /// <inheritdoc />
    public async Task ExportAsync(OptimizationConfig config, string filePath, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(config);
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);

        string? directory = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        string json = Serialize(config);
        await File.WriteAllTextAsync(filePath, json, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task<OptimizationConfig> ImportAsync(string filePath, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException("Optimization configuration file was not found.", filePath);
        }

        string json = await File.ReadAllTextAsync(filePath, cancellationToken).ConfigureAwait(false);
        return Deserialize(json);
    }

    private sealed class ConfigDto
    {
        public int SchemaVersion { get; set; } = 1;

        public bool IncludeSecurityReducingTweaks { get; set; }

        public bool CreateRestorePoint { get; set; } = true;

        public Dictionary<string, bool>? Tweaks { get; set; }
    }
}
