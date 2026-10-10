using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Win32;

namespace WingetStore.Services.Optimizer;

/// <summary>Everything a tweak operation may touch, injected so operations are testable.</summary>
/// <param name="Registry">Registry access.</param>
/// <param name="Commands">External command execution.</param>
/// <param name="Wmi">WMI access.</param>
/// <param name="TotalPhysicalMemoryBytes">Installed RAM, used by size-sensitive tweaks.</param>
public sealed record TweakContext(IRegistryAccessor Registry, ICommandExecutor Commands, IWmiAccessor Wmi, ulong TotalPhysicalMemoryBytes);

/// <summary>Live state of one operation.</summary>
/// <param name="Current">Human-readable current value.</param>
/// <param name="IsOptimized">Whether the current value already equals the optimized target.</param>
/// <param name="Applicable">False when the setting does not exist on this machine (service not installed, no matching GPU key, ...).</param>
public sealed record OperationState(string Current, bool IsOptimized, bool Applicable = true);

/// <summary>Outcome of applying an operation or tweak.</summary>
/// <param name="Applied">Whether anything was changed.</param>
/// <param name="Message">Optional explanation, mainly for skips.</param>
public sealed record TweakApplyResult(bool Applied, string? Message = null)
{
    /// <summary>A change was made.</summary>
    public static TweakApplyResult Done { get; } = new(true);

    /// <summary>Nothing was changed, with a reason.</summary>
    /// <param name="reason">Why the operation was skipped.</param>
    /// <returns>A skipped result.</returns>
    public static TweakApplyResult Skipped(string reason) => new(false, reason);
}

/// <summary>An atomic, reversible system setting change.</summary>
public interface ITweakOperation
{
    /// <summary>Reads the live state.</summary>
    /// <param name="context">Environment.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The state.</returns>
    Task<OperationState> QueryAsync(TweakContext context, CancellationToken cancellationToken);

    /// <summary>Records the original state so it can be restored exactly.</summary>
    /// <param name="context">Environment.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A JSON snapshot owned by the caller; <see cref="ApplyAsync"/> may add fields to it.</returns>
    Task<JsonObject> CaptureAsync(TweakContext context, CancellationToken cancellationToken);

    /// <summary>Applies the optimized target.</summary>
    /// <param name="context">Environment.</param>
    /// <param name="snapshot">Snapshot from <see cref="CaptureAsync"/>; may be extended with state needed for reverting.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The outcome.</returns>
    Task<TweakApplyResult> ApplyAsync(TweakContext context, JsonObject snapshot, CancellationToken cancellationToken);

    /// <summary>Restores the state recorded in <paramref name="snapshot"/>.</summary>
    /// <param name="context">Environment.</param>
    /// <param name="snapshot">Snapshot from <see cref="CaptureAsync"/>.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task that completes when the state is restored.</returns>
    Task RevertAsync(TweakContext context, JsonObject snapshot, CancellationToken cancellationToken);

    /// <summary>Writes the Windows default (used when no snapshot exists).</summary>
    /// <param name="context">Environment.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task that completes when the default is written.</returns>
    Task RestoreDefaultAsync(TweakContext context, CancellationToken cancellationToken);
}

/// <summary>Grouping used by the UI and audit report.</summary>
public enum TweakCategory
{
    /// <summary>Telemetry, tracking and web search.</summary>
    Privacy,

    /// <summary>Background services.</summary>
    Services,

    /// <summary>Security features traded for speed.</summary>
    Security,

    /// <summary>CPU, scheduler and timers.</summary>
    Cpu,

    /// <summary>Graphics.</summary>
    Gpu,

    /// <summary>Memory and paging.</summary>
    Memory,

    /// <summary>Storage and file system.</summary>
    Storage,

    /// <summary>Network stack and adapters.</summary>
    Network,

    /// <summary>Audio and USB latency.</summary>
    Peripherals,

    /// <summary>Power plan and power policy.</summary>
    Power,

    /// <summary>Shell and context menus.</summary>
    Desktop,
}

/// <summary>Static description of a tweak.</summary>
/// <param name="Id">Stable identifier used in configs and journals.</param>
/// <param name="Title">Short title.</param>
/// <param name="Category">Display category.</param>
/// <param name="Phase">Phase number from the original 25-phase script (for progress and ordering).</param>
/// <param name="DefaultState">Windows default, as text.</param>
/// <param name="TargetState">Optimized state, as text.</param>
/// <param name="PositiveImpact">Benefit.</param>
/// <param name="TradeOff">Cost or risk.</param>
/// <param name="ReducesSecurity">Weakens a security protection; only applied with explicit opt-in.</param>
/// <param name="RequiresAmdCpu">Only meaningful on AMD processors.</param>
public sealed record TweakInfo(
    string Id,
    string Title,
    TweakCategory Category,
    int Phase,
    string DefaultState,
    string TargetState,
    string PositiveImpact,
    string TradeOff,
    bool ReducesSecurity = false,
    bool RequiresAmdCpu = false);

/// <summary>Aggregated live state of a tweak.</summary>
/// <param name="Current">Human-readable current state.</param>
/// <param name="IsOptimized">Whether every applicable operation is already optimized.</param>
public sealed record TweakState(string Current, bool IsOptimized);

/// <summary>A named, ordered group of operations.</summary>
public sealed class Tweak
{
    /// <summary>Initializes a new instance of the <see cref="Tweak"/> class.</summary>
    /// <param name="info">Metadata.</param>
    /// <param name="operations">Operations, applied in order and reverted in reverse order.</param>
    public Tweak(TweakInfo info, params ITweakOperation[] operations)
    {
        ArgumentNullException.ThrowIfNull(info);
        ArgumentNullException.ThrowIfNull(operations);
        if (operations.Length == 0)
        {
            throw new ArgumentException("A tweak needs at least one operation.", nameof(operations));
        }

        Info = info;
        Operations = operations;
    }

    /// <summary>Gets the metadata.</summary>
    public TweakInfo Info { get; }

    /// <summary>Gets the operations.</summary>
    public IReadOnlyList<ITweakOperation> Operations { get; }

    /// <summary>Reads and aggregates the live state of all operations.</summary>
    /// <param name="context">Environment.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The aggregated state.</returns>
    public async Task<TweakState> QueryAsync(TweakContext context, CancellationToken cancellationToken)
    {
        var states = new List<OperationState>(Operations.Count);
        foreach (var operation in Operations)
        {
            states.Add(await operation.QueryAsync(context, cancellationToken).ConfigureAwait(false));
        }

        var applicable = states.Where(s => s.Applicable).ToList();
        if (applicable.Count == 0)
        {
            return new TweakState(states.Count == 1 ? states[0].Current : "Not applicable on this system", true);
        }

        bool allOptimized = applicable.All(s => s.IsOptimized);
        if (applicable.Count == 1)
        {
            return new TweakState(applicable[0].Current, allOptimized);
        }

        int done = applicable.Count(s => s.IsOptimized);
        return new TweakState(allOptimized ? "Optimized" : $"{done} of {applicable.Count} settings optimized", allOptimized);
    }

    /// <summary>Captures the original state of every operation.</summary>
    /// <param name="context">Environment.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A snapshot of the form <c>{"ops":[...]}</c>.</returns>
    public async Task<JsonObject> CaptureAsync(TweakContext context, CancellationToken cancellationToken)
    {
        var ops = new JsonArray();
        foreach (var operation in Operations)
        {
            ops.Add(await operation.CaptureAsync(context, cancellationToken).ConfigureAwait(false));
        }

        return new JsonObject { ["ops"] = ops };
    }

    /// <summary>Applies every operation; failures do not stop later operations but are reported together.</summary>
    /// <param name="context">Environment.</param>
    /// <param name="snapshot">Snapshot from <see cref="CaptureAsync"/>.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The combined outcome.</returns>
    /// <exception cref="AggregateException">One or more operations failed.</exception>
    public async Task<TweakApplyResult> ApplyAsync(TweakContext context, JsonObject snapshot, CancellationToken cancellationToken)
    {
        var ops = OperationSnapshots(snapshot);
        var errors = new List<Exception>();
        var messages = new List<string>();
        bool applied = false;
        for (int i = 0; i < Operations.Count; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                var result = await Operations[i].ApplyAsync(context, ops[i], cancellationToken).ConfigureAwait(false);
                applied |= result.Applied;
                if (result.Message is not null)
                {
                    messages.Add(result.Message);
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                errors.Add(ex);
            }
        }

        if (errors.Count > 0)
        {
            throw new AggregateException($"{Info.Title}: {errors.Count} of {Operations.Count} operations failed.", errors);
        }

        return new TweakApplyResult(applied, messages.Count == 0 ? null : string.Join(" ", messages));
    }

    /// <summary>Restores the captured state in reverse operation order.</summary>
    /// <param name="context">Environment.</param>
    /// <param name="snapshot">Snapshot from <see cref="CaptureAsync"/>.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task that completes when restoration finished.</returns>
    /// <exception cref="AggregateException">One or more operations failed to restore.</exception>
    public async Task RevertAsync(TweakContext context, JsonObject snapshot, CancellationToken cancellationToken)
    {
        var ops = OperationSnapshots(snapshot);
        var errors = new List<Exception>();
        for (int i = Operations.Count - 1; i >= 0; i--)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                await Operations[i].RevertAsync(context, ops[i], cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                errors.Add(ex);
            }
        }

        if (errors.Count > 0)
        {
            throw new AggregateException($"{Info.Title}: {errors.Count} of {Operations.Count} operations failed to restore.", errors);
        }
    }

    /// <summary>Writes Windows defaults in reverse operation order.</summary>
    /// <param name="context">Environment.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task that completes when defaults are written.</returns>
    /// <exception cref="AggregateException">One or more operations failed.</exception>
    public async Task RestoreDefaultAsync(TweakContext context, CancellationToken cancellationToken)
    {
        var errors = new List<Exception>();
        for (int i = Operations.Count - 1; i >= 0; i--)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                await Operations[i].RestoreDefaultAsync(context, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                errors.Add(ex);
            }
        }

        if (errors.Count > 0)
        {
            throw new AggregateException($"{Info.Title}: {errors.Count} of {Operations.Count} operations failed to reset.", errors);
        }
    }

    private JsonObject[] OperationSnapshots(JsonObject snapshot)
    {
        if (snapshot["ops"] is not JsonArray array || array.Count != Operations.Count)
        {
            throw new InvalidOperationException($"Snapshot for '{Info.Id}' does not match the tweak definition.");
        }

        return [.. array.Select(n => n as JsonObject ?? throw new InvalidOperationException($"Snapshot for '{Info.Id}' is corrupt."))];
    }
}

internal static class RegistrySnapshot
{
    public static JsonNode? Write(RegistryValueData? value)
    {
        if (value is null)
        {
            return null;
        }

        var node = new JsonObject { ["kind"] = value.Kind.ToString() };
        switch (value.Data)
        {
            case string[] lines:
                node["lines"] = new JsonArray([.. lines.Select(l => (JsonNode?)JsonValue.Create(l))]);
                break;
            case byte[] bytes:
                node["text"] = Convert.ToBase64String(bytes);
                break;
            default:
                node["text"] = Convert.ToString(value.Data, CultureInfo.InvariantCulture);
                break;
        }

        return node;
    }

    public static RegistryValueData? Read(JsonNode? node)
    {
        if (node is not JsonObject obj || !Enum.TryParse<RegistryValueKind>((string?)obj["kind"], out var kind))
        {
            return null;
        }

        string text = (string?)obj["text"] ?? string.Empty;
        return kind switch
        {
            RegistryValueKind.DWord => new RegistryValueData(int.Parse(text, CultureInfo.InvariantCulture), kind),
            RegistryValueKind.QWord => new RegistryValueData(long.Parse(text, CultureInfo.InvariantCulture), kind),
            RegistryValueKind.MultiString => new RegistryValueData(
                (obj["lines"] as JsonArray)?.Select(n => (string?)n ?? string.Empty).ToArray() ?? [],
                kind),
            RegistryValueKind.Binary or RegistryValueKind.Unknown or RegistryValueKind.None => new RegistryValueData(Convert.FromBase64String(text), RegistryValueKind.Binary),
            _ => new RegistryValueData(text, kind),
        };
    }
}

/// <summary>Sets one registry value.</summary>
public sealed class RegistryValueOperation : ITweakOperation
{
    private readonly RegistryRoot _root;
    private readonly string _subKey;
    private readonly string _name;
    private readonly RegistryValueData _target;
    private readonly RegistryValueData? _default;

    /// <summary>Initializes a new instance of the <see cref="RegistryValueOperation"/> class.</summary>
    /// <param name="root">Registry root.</param>
    /// <param name="subKey">Key path.</param>
    /// <param name="name">Value name (empty for the default value).</param>
    /// <param name="target">Optimized value.</param>
    /// <param name="windowsDefault">Windows default; <see langword="null"/> means the value does not exist by default.</param>
    public RegistryValueOperation(RegistryRoot root, string subKey, string name, RegistryValueData target, RegistryValueData? windowsDefault)
    {
        _root = root;
        _subKey = subKey;
        _name = name;
        _target = target;
        _default = windowsDefault;
    }

    /// <inheritdoc />
    public Task<OperationState> QueryAsync(TweakContext context, CancellationToken cancellationToken)
    {
        var current = context.Registry.GetValue(_root, _subKey, _name);
        return Task.FromResult(new OperationState(current?.ToString() ?? "(not set)", _target.ContentEquals(current)));
    }

    /// <inheritdoc />
    public Task<JsonObject> CaptureAsync(TweakContext context, CancellationToken cancellationToken) =>
        Task.FromResult(new JsonObject
        {
            ["keyExisted"] = context.Registry.KeyExists(_root, _subKey),
            ["value"] = RegistrySnapshot.Write(context.Registry.GetValue(_root, _subKey, _name)),
        });

    /// <inheritdoc />
    public Task<TweakApplyResult> ApplyAsync(TweakContext context, JsonObject snapshot, CancellationToken cancellationToken)
    {
        context.Registry.SetValue(_root, _subKey, _name, _target);
        return Task.FromResult(TweakApplyResult.Done);
    }

    /// <inheritdoc />
    public Task RevertAsync(TweakContext context, JsonObject snapshot, CancellationToken cancellationToken)
    {
        var original = RegistrySnapshot.Read(snapshot["value"]);
        if (original is not null)
        {
            context.Registry.SetValue(_root, _subKey, _name, original);
        }
        else
        {
            context.Registry.DeleteValue(_root, _subKey, _name);
            if (snapshot["keyExisted"]?.GetValue<bool>() == false)
            {
                context.Registry.DeleteKeyIfEmpty(_root, _subKey);
            }
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task RestoreDefaultAsync(TweakContext context, CancellationToken cancellationToken)
    {
        if (_default is null)
        {
            context.Registry.DeleteValue(_root, _subKey, _name);
        }
        else
        {
            context.Registry.SetValue(_root, _subKey, _name, _default);
        }

        return Task.CompletedTask;
    }
}

/// <summary>Sets one value on every key returned by a provider (GPU class keys, network interfaces, USB devices, ...).</summary>
public sealed class RegistryEachKeyOperation : ITweakOperation
{
    private readonly RegistryRoot _root;
    private readonly Func<IRegistryAccessor, IReadOnlyList<string>> _keys;
    private readonly string _name;
    private readonly RegistryValueData _target;
    private readonly RegistryValueData? _default;
    private readonly bool _onlyWhereValueExists;

    /// <summary>Initializes a new instance of the <see cref="RegistryEachKeyOperation"/> class.</summary>
    /// <param name="root">Registry root.</param>
    /// <param name="keys">Provider of full key paths to touch.</param>
    /// <param name="name">Value name.</param>
    /// <param name="target">Optimized value.</param>
    /// <param name="windowsDefault">Windows default; <see langword="null"/> deletes the value.</param>
    /// <param name="onlyWhereValueExists">When true, keys that lack the value are ignored.</param>
    public RegistryEachKeyOperation(RegistryRoot root, Func<IRegistryAccessor, IReadOnlyList<string>> keys, string name, RegistryValueData target, RegistryValueData? windowsDefault, bool onlyWhereValueExists)
    {
        _root = root;
        _keys = keys;
        _name = name;
        _target = target;
        _default = windowsDefault;
        _onlyWhereValueExists = onlyWhereValueExists;
    }

    /// <inheritdoc />
    public Task<OperationState> QueryAsync(TweakContext context, CancellationToken cancellationToken)
    {
        var keys = Targets(context);
        if (keys.Count == 0)
        {
            return Task.FromResult(new OperationState("Not present", true, Applicable: false));
        }

        int done = keys.Count(k => _target.ContentEquals(context.Registry.GetValue(_root, k, _name)));
        return Task.FromResult(new OperationState($"{done} of {keys.Count} optimized", done == keys.Count));
    }

    /// <inheritdoc />
    public Task<JsonObject> CaptureAsync(TweakContext context, CancellationToken cancellationToken)
    {
        var values = new JsonObject();
        foreach (var key in Targets(context))
        {
            values[key] = RegistrySnapshot.Write(context.Registry.GetValue(_root, key, _name));
        }

        return Task.FromResult(new JsonObject { ["values"] = values });
    }

    /// <inheritdoc />
    public Task<TweakApplyResult> ApplyAsync(TweakContext context, JsonObject snapshot, CancellationToken cancellationToken)
    {
        var keys = Targets(context);
        foreach (var key in keys)
        {
            cancellationToken.ThrowIfCancellationRequested();
            context.Registry.SetValue(_root, key, _name, _target);
        }

        return Task.FromResult(keys.Count == 0 ? TweakApplyResult.Skipped("No matching registry keys found.") : TweakApplyResult.Done);
    }

    /// <inheritdoc />
    public Task RevertAsync(TweakContext context, JsonObject snapshot, CancellationToken cancellationToken)
    {
        if (snapshot["values"] is not JsonObject values)
        {
            return Task.CompletedTask;
        }

        foreach (var (key, node) in values)
        {
            var original = RegistrySnapshot.Read(node);
            if (original is null)
            {
                context.Registry.DeleteValue(_root, key, _name);
            }
            else
            {
                context.Registry.SetValue(_root, key, _name, original);
            }
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task RestoreDefaultAsync(TweakContext context, CancellationToken cancellationToken)
    {
        foreach (var key in Targets(context))
        {
            if (_default is null)
            {
                context.Registry.DeleteValue(_root, key, _name);
            }
            else
            {
                context.Registry.SetValue(_root, key, _name, _default);
            }
        }

        return Task.CompletedTask;
    }

    private IReadOnlyList<string> Targets(TweakContext context)
    {
        var keys = _keys(context.Registry);
        return _onlyWhereValueExists
            ? [.. keys.Where(k => context.Registry.GetValue(_root, k, _name) is not null)]
            : keys;
    }
}

/// <summary>Rewrites the <c>SharedSection=a,b,c</c> triple inside the Windows subsystem string.</summary>
public sealed class DesktopHeapOperation : ITweakOperation
{
    private const string SubKey = @"SYSTEM\CurrentControlSet\Control\Session Manager\SubSystems";
    private const string ValueName = "Windows";
    private const string TargetTriple = "1024,20480,1024";
    private const string DefaultTriple = "1024,20480,768";
    private static readonly Regex SharedSection = new(@"SharedSection=(\d+,\d+,\d+)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1));

    /// <inheritdoc />
    public Task<OperationState> QueryAsync(TweakContext context, CancellationToken cancellationToken)
    {
        string? triple = Triple(context.Registry.GetValue(RegistryRoot.LocalMachine, SubKey, ValueName));
        return Task.FromResult(triple is null
            ? new OperationState("SharedSection not found", true, Applicable: false)
            : new OperationState(triple, triple == TargetTriple));
    }

    /// <inheritdoc />
    public Task<JsonObject> CaptureAsync(TweakContext context, CancellationToken cancellationToken) =>
        Task.FromResult(new JsonObject { ["value"] = RegistrySnapshot.Write(context.Registry.GetValue(RegistryRoot.LocalMachine, SubKey, ValueName)) });

    /// <inheritdoc />
    public Task<TweakApplyResult> ApplyAsync(TweakContext context, JsonObject snapshot, CancellationToken cancellationToken) =>
        Task.FromResult(Rewrite(context, TargetTriple));

    /// <inheritdoc />
    public Task RevertAsync(TweakContext context, JsonObject snapshot, CancellationToken cancellationToken)
    {
        var original = RegistrySnapshot.Read(snapshot["value"]);
        if (original is not null)
        {
            context.Registry.SetValue(RegistryRoot.LocalMachine, SubKey, ValueName, original);
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task RestoreDefaultAsync(TweakContext context, CancellationToken cancellationToken)
    {
        Rewrite(context, DefaultTriple);
        return Task.CompletedTask;
    }

    private static string? Triple(RegistryValueData? value) =>
        value?.Data is string text && SharedSection.Match(text) is { Success: true } match ? match.Groups[1].Value : null;

    private static TweakApplyResult Rewrite(TweakContext context, string triple)
    {
        var current = context.Registry.GetValue(RegistryRoot.LocalMachine, SubKey, ValueName);
        if (current?.Data is not string text || !SharedSection.IsMatch(text))
        {
            return TweakApplyResult.Skipped("Desktop heap SharedSection not found.");
        }

        string updated = SharedSection.Replace(text, $"SharedSection={triple}", 1);
        context.Registry.SetValue(RegistryRoot.LocalMachine, SubKey, ValueName, new RegistryValueData(updated, current.Kind));
        return TweakApplyResult.Done;
    }
}

/// <summary>Disables a Windows service and stops it.</summary>
public sealed class ServiceOperation : ITweakOperation
{
    private const int StartDisabled = 4;
    private const int ServiceRunning = 4;
    private static readonly Regex StateRegex = new(@"STATE\s*:\s*(\d+)", RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1));
    private readonly string _name;

    /// <summary>Initializes a new instance of the <see cref="ServiceOperation"/> class.</summary>
    /// <param name="serviceName">Service short name.</param>
    public ServiceOperation(string serviceName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(serviceName);
        _name = serviceName;
    }

    private string KeyPath => $@"SYSTEM\CurrentControlSet\Services\{_name}";

    /// <inheritdoc />
    public Task<OperationState> QueryAsync(TweakContext context, CancellationToken cancellationToken)
    {
        if (!context.Registry.KeyExists(RegistryRoot.LocalMachine, KeyPath))
        {
            return Task.FromResult(new OperationState($"{_name}: not installed", true, Applicable: false));
        }

        int start = StartType(context);
        return Task.FromResult(new OperationState($"{_name}: {Describe(start)}", start == StartDisabled));
    }

    /// <inheritdoc />
    public async Task<JsonObject> CaptureAsync(TweakContext context, CancellationToken cancellationToken)
    {
        bool exists = context.Registry.KeyExists(RegistryRoot.LocalMachine, KeyPath);
        return new JsonObject
        {
            ["exists"] = exists,
            ["start"] = exists ? StartType(context) : 0,
            ["delayed"] = exists && context.Registry.GetValue(RegistryRoot.LocalMachine, KeyPath, "DelayedAutostart")?.Data is int d && d == 1,
            ["running"] = exists && await IsRunningAsync(context, cancellationToken).ConfigureAwait(false),
        };
    }

    /// <inheritdoc />
    public async Task<TweakApplyResult> ApplyAsync(TweakContext context, JsonObject snapshot, CancellationToken cancellationToken)
    {
        if (!context.Registry.KeyExists(RegistryRoot.LocalMachine, KeyPath))
        {
            return TweakApplyResult.Skipped($"Service {_name} is not installed.");
        }

        await RunScAsync(context, $"config {_name} start= disabled", cancellationToken).ConfigureAwait(false);
        await context.Commands.RunAsync("sc.exe", $"stop {_name}", cancellationToken).ConfigureAwait(false);
        return TweakApplyResult.Done;
    }

    /// <inheritdoc />
    public async Task RevertAsync(TweakContext context, JsonObject snapshot, CancellationToken cancellationToken)
    {
        if (snapshot["exists"]?.GetValue<bool>() != true)
        {
            return;
        }

        int start = snapshot["start"]?.GetValue<int>() ?? 3;
        bool delayed = snapshot["delayed"]?.GetValue<bool>() == true;
        string? mode = start switch
        {
            2 => delayed ? "delayed-auto" : "auto",
            3 => "demand",
            StartDisabled => "disabled",
            _ => null,
        };

        if (mode is not null)
        {
            await RunScAsync(context, $"config {_name} start= {mode}", cancellationToken).ConfigureAwait(false);
        }

        if (snapshot["running"]?.GetValue<bool>() == true && !await IsRunningAsync(context, cancellationToken).ConfigureAwait(false))
        {
            await context.Commands.RunAsync("sc.exe", $"start {_name}", cancellationToken).ConfigureAwait(false);
        }
    }

    /// <inheritdoc />
    public async Task RestoreDefaultAsync(TweakContext context, CancellationToken cancellationToken)
    {
        if (!context.Registry.KeyExists(RegistryRoot.LocalMachine, KeyPath))
        {
            return;
        }

        await RunScAsync(context, $"config {_name} start= auto", cancellationToken).ConfigureAwait(false);
        await context.Commands.RunAsync("sc.exe", $"start {_name}", cancellationToken).ConfigureAwait(false);
    }

    private static string Describe(int start) => start switch
    {
        0 => "Boot",
        1 => "System",
        2 => "Automatic",
        3 => "Manual",
        StartDisabled => "Disabled",
        _ => $"Unknown ({start})",
    };

    private int StartType(TweakContext context) =>
        context.Registry.GetValue(RegistryRoot.LocalMachine, KeyPath, "Start")?.Data is int start ? start : 3;

    private async Task<bool> IsRunningAsync(TweakContext context, CancellationToken cancellationToken)
    {
        var result = await context.Commands.RunAsync("sc.exe", $"query {_name}", cancellationToken).ConfigureAwait(false);
        var match = StateRegex.Match(result.Output);
        return match.Success && int.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture) == ServiceRunning;
    }

    private static async Task RunScAsync(TweakContext context, string arguments, CancellationToken cancellationToken)
    {
        var result = await context.Commands.RunAsync("sc.exe", arguments, cancellationToken).ConfigureAwait(false);
        if (!result.Succeeded)
        {
            throw new InvalidOperationException($"sc.exe {arguments} failed (exit {result.ExitCode}): {result.Output.Trim()}");
        }
    }
}

/// <summary>Disables a scheduled task.</summary>
public sealed class ScheduledTaskOperation : ITweakOperation
{
    private static readonly Regex SettingsBlock = new(@"<Settings>.*?</Settings>", RegexOptions.Singleline | RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1));
    private static readonly Regex EnabledElement = new(@"<Enabled>\s*(true|false)\s*</Enabled>", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1));
    private readonly string _taskPath;

    /// <summary>Initializes a new instance of the <see cref="ScheduledTaskOperation"/> class.</summary>
    /// <param name="taskPath">Full task path, e.g. <c>\Microsoft\Windows\Autochk\Proxy</c>.</param>
    public ScheduledTaskOperation(string taskPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(taskPath);
        _taskPath = taskPath;
    }

    private string Leaf => _taskPath[(_taskPath.LastIndexOf('\\') + 1)..];

    /// <inheritdoc />
    public async Task<OperationState> QueryAsync(TweakContext context, CancellationToken cancellationToken)
    {
        bool? enabled = await IsEnabledAsync(context, cancellationToken).ConfigureAwait(false);
        return enabled is null
            ? new OperationState($"{Leaf}: not found", true, Applicable: false)
            : new OperationState($"{Leaf}: {(enabled == true ? "Enabled" : "Disabled")}", enabled == false);
    }

    /// <inheritdoc />
    public async Task<JsonObject> CaptureAsync(TweakContext context, CancellationToken cancellationToken)
    {
        bool? enabled = await IsEnabledAsync(context, cancellationToken).ConfigureAwait(false);
        return new JsonObject { ["exists"] = enabled is not null, ["enabled"] = enabled ?? true };
    }

    /// <inheritdoc />
    public async Task<TweakApplyResult> ApplyAsync(TweakContext context, JsonObject snapshot, CancellationToken cancellationToken)
    {
        if (snapshot["exists"]?.GetValue<bool>() != true)
        {
            return TweakApplyResult.Skipped($"Scheduled task {Leaf} not found.");
        }

        await ChangeAsync(context, "/DISABLE", cancellationToken).ConfigureAwait(false);
        return TweakApplyResult.Done;
    }

    /// <inheritdoc />
    public async Task RevertAsync(TweakContext context, JsonObject snapshot, CancellationToken cancellationToken)
    {
        if (snapshot["exists"]?.GetValue<bool>() != true)
        {
            return;
        }

        await ChangeAsync(context, snapshot["enabled"]?.GetValue<bool>() == false ? "/DISABLE" : "/ENABLE", cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task RestoreDefaultAsync(TweakContext context, CancellationToken cancellationToken)
    {
        if (await IsEnabledAsync(context, cancellationToken).ConfigureAwait(false) is not null)
        {
            await ChangeAsync(context, "/ENABLE", cancellationToken).ConfigureAwait(false);
        }
    }

    private async Task<bool?> IsEnabledAsync(TweakContext context, CancellationToken cancellationToken)
    {
        var result = await context.Commands.RunAsync("schtasks.exe", $"/Query /TN \"{_taskPath}\" /XML", cancellationToken).ConfigureAwait(false);
        if (!result.Succeeded)
        {
            return null;
        }

        var settings = SettingsBlock.Match(result.Output);
        var enabled = settings.Success ? EnabledElement.Match(settings.Value) : Match.Empty;
        return !enabled.Success || !string.Equals(enabled.Groups[1].Value, "false", StringComparison.OrdinalIgnoreCase);
    }

    private async Task ChangeAsync(TweakContext context, string switchName, CancellationToken cancellationToken)
    {
        var result = await context.Commands.RunAsync("schtasks.exe", $"/Change /TN \"{_taskPath}\" {switchName}", cancellationToken).ConfigureAwait(false);
        if (!result.Succeeded)
        {
            throw new InvalidOperationException($"schtasks {switchName} for '{_taskPath}' failed (exit {result.ExitCode}): {result.Output.Trim()}");
        }
    }
}

/// <summary>Sets or deletes a boot configuration (BCD) element for the current boot entry.</summary>
public sealed class BcdOperation : ITweakOperation
{
    private readonly string _element;
    private readonly string _target;
    private readonly string? _default;

    /// <summary>Initializes a new instance of the <see cref="BcdOperation"/> class.</summary>
    /// <param name="element">BCD element name, e.g. <c>tscsyncpolicy</c>.</param>
    /// <param name="target">Optimized value.</param>
    /// <param name="windowsDefault">Windows default value; <see langword="null"/> removes the element.</param>
    public BcdOperation(string element, string target, string? windowsDefault)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(element);
        ArgumentException.ThrowIfNullOrWhiteSpace(target);
        _element = element;
        _target = target;
        _default = windowsDefault;
    }

    /// <inheritdoc />
    public async Task<OperationState> QueryAsync(TweakContext context, CancellationToken cancellationToken)
    {
        var (available, value) = await ReadAsync(context, cancellationToken).ConfigureAwait(false);
        if (!available)
        {
            return new OperationState($"{_element}: unavailable", true, Applicable: false);
        }

        return new OperationState($"{_element}: {value ?? "(default)"}", string.Equals(value, _target, StringComparison.OrdinalIgnoreCase));
    }

    /// <inheritdoc />
    public async Task<JsonObject> CaptureAsync(TweakContext context, CancellationToken cancellationToken)
    {
        var (available, value) = await ReadAsync(context, cancellationToken).ConfigureAwait(false);
        return new JsonObject { ["available"] = available, ["present"] = value is not null, ["value"] = value };
    }

    /// <inheritdoc />
    public async Task<TweakApplyResult> ApplyAsync(TweakContext context, JsonObject snapshot, CancellationToken cancellationToken)
    {
        if (snapshot["available"]?.GetValue<bool>() != true)
        {
            return TweakApplyResult.Skipped($"Boot configuration is unavailable; skipped '{_element}'.");
        }

        await SetAsync(context, _target, cancellationToken).ConfigureAwait(false);
        return TweakApplyResult.Done;
    }

    /// <inheritdoc />
    public async Task RevertAsync(TweakContext context, JsonObject snapshot, CancellationToken cancellationToken)
    {
        if (snapshot["available"]?.GetValue<bool>() != true)
        {
            return;
        }

        string? original = snapshot["present"]?.GetValue<bool>() == true ? (string?)snapshot["value"] : null;
        if (original is null)
        {
            await context.Commands.RunAsync("bcdedit.exe", $"/deletevalue {_element}", cancellationToken).ConfigureAwait(false);
        }
        else
        {
            await SetAsync(context, original, cancellationToken).ConfigureAwait(false);
        }
    }

    /// <inheritdoc />
    public async Task RestoreDefaultAsync(TweakContext context, CancellationToken cancellationToken)
    {
        if (_default is null)
        {
            await context.Commands.RunAsync("bcdedit.exe", $"/deletevalue {_element}", cancellationToken).ConfigureAwait(false);
        }
        else
        {
            await SetAsync(context, _default, cancellationToken).ConfigureAwait(false);
        }
    }

    private async Task<(bool Available, string? Value)> ReadAsync(TweakContext context, CancellationToken cancellationToken)
    {
        var result = await context.Commands.RunAsync("bcdedit.exe", "/enum {current}", cancellationToken).ConfigureAwait(false);
        if (!result.Succeeded)
        {
            return (false, null);
        }

        var match = Regex.Match(
            result.Output,
            $@"^\s*{Regex.Escape(_element)}\s+(\S.*?)\s*$",
            RegexOptions.IgnoreCase | RegexOptions.Multiline | RegexOptions.CultureInvariant,
            TimeSpan.FromSeconds(1));
        return (true, match.Success ? match.Groups[1].Value : null);
    }

    private async Task SetAsync(TweakContext context, string value, CancellationToken cancellationToken)
    {
        var result = await context.Commands.RunAsync("bcdedit.exe", $"/set {_element} {value}", cancellationToken).ConfigureAwait(false);
        if (!result.Succeeded)
        {
            throw new InvalidOperationException($"bcdedit /set {_element} {value} failed (exit {result.ExitCode}): {result.Output.Trim()}");
        }
    }
}

/// <summary>Sets an AC power-plan value on the active scheme.</summary>
public sealed class PowerSettingOperation : ITweakOperation
{
    private static readonly Regex HexIndex = new(@"0x[0-9a-fA-F]{8}", RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1));
    private readonly string _subGroup;
    private readonly string _setting;
    private readonly uint _target;
    private readonly uint _default;

    /// <summary>Initializes a new instance of the <see cref="PowerSettingOperation"/> class.</summary>
    /// <param name="subGroup">Sub group GUID or alias.</param>
    /// <param name="setting">Setting GUID.</param>
    /// <param name="target">Optimized AC value.</param>
    /// <param name="windowsDefault">Windows default AC value.</param>
    public PowerSettingOperation(string subGroup, string setting, uint target, uint windowsDefault)
    {
        _subGroup = subGroup;
        _setting = setting;
        _target = target;
        _default = windowsDefault;
    }

    /// <inheritdoc />
    public async Task<OperationState> QueryAsync(TweakContext context, CancellationToken cancellationToken)
    {
        uint? ac = await ReadAcAsync(context, cancellationToken).ConfigureAwait(false);
        return ac is null
            ? new OperationState("Power setting unavailable", true, Applicable: false)
            : new OperationState($"AC index {ac}", ac == _target);
    }

    /// <inheritdoc />
    public async Task<JsonObject> CaptureAsync(TweakContext context, CancellationToken cancellationToken)
    {
        uint? ac = await ReadAcAsync(context, cancellationToken).ConfigureAwait(false);
        return new JsonObject { ["ac"] = ac is null ? null : (long)ac.Value };
    }

    /// <inheritdoc />
    public async Task<TweakApplyResult> ApplyAsync(TweakContext context, JsonObject snapshot, CancellationToken cancellationToken)
    {
        await SetAsync(context, _target, cancellationToken).ConfigureAwait(false);
        return TweakApplyResult.Done;
    }

    /// <inheritdoc />
    public async Task RevertAsync(TweakContext context, JsonObject snapshot, CancellationToken cancellationToken)
    {
        if (snapshot["ac"] is JsonNode node)
        {
            await SetAsync(context, (uint)node.GetValue<long>(), cancellationToken).ConfigureAwait(false);
        }
    }

    /// <inheritdoc />
    public Task RestoreDefaultAsync(TweakContext context, CancellationToken cancellationToken) =>
        SetAsync(context, _default, cancellationToken);

    private async Task<uint?> ReadAcAsync(TweakContext context, CancellationToken cancellationToken)
    {
        var result = await context.Commands.RunAsync("powercfg.exe", $"/query SCHEME_CURRENT {_subGroup} {_setting}", cancellationToken).ConfigureAwait(false);
        if (!result.Succeeded)
        {
            return null;
        }

        // The last two hex values in the report are the current AC and DC indexes, in that order.
        var matches = HexIndex.Matches(result.Output);
        return matches.Count < 2
            ? null
            : Convert.ToUInt32(matches[^2].Value, 16);
    }

    private async Task SetAsync(TweakContext context, uint value, CancellationToken cancellationToken)
    {
        var set = await context.Commands.RunAsync("powercfg.exe", $"/setacvalueindex SCHEME_CURRENT {_subGroup} {_setting} {value}", cancellationToken).ConfigureAwait(false);
        if (!set.Succeeded)
        {
            throw new InvalidOperationException($"powercfg /setacvalueindex failed (exit {set.ExitCode}): {set.Output.Trim()}");
        }

        await context.Commands.RunAsync("powercfg.exe", "/setactive SCHEME_CURRENT", cancellationToken).ConfigureAwait(false);
    }
}

/// <summary>Switches to the Ultimate Performance power plan (falling back to High Performance).</summary>
public sealed class PowerPlanOperation : ITweakOperation
{
    private const string UltimateGuid = "e9a42b02-d5df-448d-aa00-03f14749eb61";
    private const string HighPerformanceGuid = "8c5e7fda-e8bf-4a96-9a85-a6e23a8c635c";
    private const string BalancedGuid = "381b4222-f694-41f0-9685-ff5bb260df2e";
    private static readonly Regex GuidRegex = new(@"[0-9a-fA-F]{8}(?:-[0-9a-fA-F]{4}){3}-[0-9a-fA-F]{12}", RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1));

    /// <inheritdoc />
    public async Task<OperationState> QueryAsync(TweakContext context, CancellationToken cancellationToken)
    {
        var (guid, text) = await ActiveAsync(context, cancellationToken).ConfigureAwait(false);
        if (guid is null)
        {
            return new OperationState("Power plan unavailable", true, Applicable: false);
        }

        bool optimized = text.Contains("Ultimate", StringComparison.OrdinalIgnoreCase)
            || text.Contains("High performance", StringComparison.OrdinalIgnoreCase)
            || guid.Equals(UltimateGuid, StringComparison.OrdinalIgnoreCase)
            || guid.Equals(HighPerformanceGuid, StringComparison.OrdinalIgnoreCase);
        return new OperationState(text, optimized);
    }

    /// <inheritdoc />
    public async Task<JsonObject> CaptureAsync(TweakContext context, CancellationToken cancellationToken)
    {
        var (guid, _) = await ActiveAsync(context, cancellationToken).ConfigureAwait(false);
        return new JsonObject { ["previous"] = guid };
    }

    /// <inheritdoc />
    public async Task<TweakApplyResult> ApplyAsync(TweakContext context, JsonObject snapshot, CancellationToken cancellationToken)
    {
        var duplicate = await context.Commands.RunAsync("powercfg.exe", $"/duplicatescheme {UltimateGuid}", cancellationToken).ConfigureAwait(false);
        var created = duplicate.Succeeded ? GuidRegex.Match(duplicate.Output) : Match.Empty;
        if (created.Success)
        {
            snapshot["created"] = created.Value;
            var activate = await context.Commands.RunAsync("powercfg.exe", $"/setactive {created.Value}", cancellationToken).ConfigureAwait(false);
            if (activate.Succeeded)
            {
                return TweakApplyResult.Done;
            }
        }

        var fallback = await context.Commands.RunAsync("powercfg.exe", $"/setactive {HighPerformanceGuid}", cancellationToken).ConfigureAwait(false);
        if (!fallback.Succeeded)
        {
            throw new InvalidOperationException($"Unable to activate a high-performance power plan (exit {fallback.ExitCode}): {fallback.Output.Trim()}");
        }

        return new TweakApplyResult(true, "Ultimate Performance is unavailable; activated High Performance instead.");
    }

    /// <inheritdoc />
    public async Task RevertAsync(TweakContext context, JsonObject snapshot, CancellationToken cancellationToken)
    {
        if ((string?)snapshot["previous"] is { Length: > 0 } previous)
        {
            await context.Commands.RunAsync("powercfg.exe", $"/setactive {previous}", cancellationToken).ConfigureAwait(false);
        }

        if ((string?)snapshot["created"] is { Length: > 0 } created)
        {
            await context.Commands.RunAsync("powercfg.exe", $"/delete {created}", cancellationToken).ConfigureAwait(false);
        }
    }

    /// <inheritdoc />
    public async Task RestoreDefaultAsync(TweakContext context, CancellationToken cancellationToken) =>
        await context.Commands.RunAsync("powercfg.exe", $"/setactive {BalancedGuid}", cancellationToken).ConfigureAwait(false);

    private static async Task<(string? Guid, string Text)> ActiveAsync(TweakContext context, CancellationToken cancellationToken)
    {
        var result = await context.Commands.RunAsync("powercfg.exe", "/getactivescheme", cancellationToken).ConfigureAwait(false);
        var match = result.Succeeded ? GuidRegex.Match(result.Output) : Match.Empty;
        return match.Success ? (match.Value, result.Output.Trim()) : (null, string.Empty);
    }
}

/// <summary>Reads and sets an <c>fsutil behavior</c> value.</summary>
public sealed class FsutilBehaviorOperation : ITweakOperation
{
    private static readonly Regex ValueRegex = new(@"=\s*(\d+)", RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1));
    private readonly string _behavior;
    private readonly string _target;
    private readonly string _default;

    /// <summary>Initializes a new instance of the <see cref="FsutilBehaviorOperation"/> class.</summary>
    /// <param name="behavior">Behavior name, e.g. <c>disablelastaccess</c>.</param>
    /// <param name="target">Optimized value.</param>
    /// <param name="windowsDefault">Windows default value.</param>
    public FsutilBehaviorOperation(string behavior, string target, string windowsDefault)
    {
        _behavior = behavior;
        _target = target;
        _default = windowsDefault;
    }

    /// <inheritdoc />
    public async Task<OperationState> QueryAsync(TweakContext context, CancellationToken cancellationToken)
    {
        string? value = await ReadAsync(context, cancellationToken).ConfigureAwait(false);
        return value is null
            ? new OperationState($"{_behavior}: unavailable", true, Applicable: false)
            : new OperationState($"{_behavior} = {value}", value == _target);
    }

    /// <inheritdoc />
    public async Task<JsonObject> CaptureAsync(TweakContext context, CancellationToken cancellationToken) =>
        new() { ["value"] = await ReadAsync(context, cancellationToken).ConfigureAwait(false) };

    /// <inheritdoc />
    public async Task<TweakApplyResult> ApplyAsync(TweakContext context, JsonObject snapshot, CancellationToken cancellationToken)
    {
        await SetAsync(context, _target, cancellationToken).ConfigureAwait(false);
        return TweakApplyResult.Done;
    }

    /// <inheritdoc />
    public async Task RevertAsync(TweakContext context, JsonObject snapshot, CancellationToken cancellationToken)
    {
        if ((string?)snapshot["value"] is { Length: > 0 } original)
        {
            await SetAsync(context, original, cancellationToken).ConfigureAwait(false);
        }
    }

    /// <inheritdoc />
    public Task RestoreDefaultAsync(TweakContext context, CancellationToken cancellationToken) =>
        SetAsync(context, _default, cancellationToken);

    private async Task<string?> ReadAsync(TweakContext context, CancellationToken cancellationToken)
    {
        var result = await context.Commands.RunAsync("fsutil.exe", $"behavior query {_behavior}", cancellationToken).ConfigureAwait(false);
        var match = result.Succeeded ? ValueRegex.Match(result.Output) : Match.Empty;
        return match.Success ? match.Groups[1].Value : null;
    }

    private async Task SetAsync(TweakContext context, string value, CancellationToken cancellationToken)
    {
        var result = await context.Commands.RunAsync("fsutil.exe", $"behavior set {_behavior} {value}", cancellationToken).ConfigureAwait(false);
        if (!result.Succeeded)
        {
            throw new InvalidOperationException($"fsutil behavior set {_behavior} {value} failed (exit {result.ExitCode}): {result.Output.Trim()}");
        }
    }
}

/// <summary>Runs a fixed command whose effect cannot be read back (idempotent global settings).</summary>
public sealed class FixedCommandOperation : ITweakOperation
{
    private readonly string _file;
    private readonly string _applyArguments;
    private readonly string _defaultArguments;

    /// <summary>Initializes a new instance of the <see cref="FixedCommandOperation"/> class.</summary>
    /// <param name="fileName">Executable.</param>
    /// <param name="applyArguments">Arguments that apply the optimized setting.</param>
    /// <param name="defaultArguments">Arguments that restore the Windows default.</param>
    public FixedCommandOperation(string fileName, string applyArguments, string defaultArguments)
    {
        _file = fileName;
        _applyArguments = applyArguments;
        _defaultArguments = defaultArguments;
    }

    /// <inheritdoc />
    public Task<OperationState> QueryAsync(TweakContext context, CancellationToken cancellationToken) =>
        Task.FromResult(new OperationState("State cannot be read back", true, Applicable: false));

    /// <inheritdoc />
    public Task<JsonObject> CaptureAsync(TweakContext context, CancellationToken cancellationToken) =>
        Task.FromResult(new JsonObject());

    /// <inheritdoc />
    public async Task<TweakApplyResult> ApplyAsync(TweakContext context, JsonObject snapshot, CancellationToken cancellationToken)
    {
        var result = await context.Commands.RunAsync(_file, _applyArguments, cancellationToken).ConfigureAwait(false);
        if (!result.Succeeded)
        {
            throw new InvalidOperationException($"{_file} {_applyArguments} failed (exit {result.ExitCode}): {result.Output.Trim()}");
        }

        return TweakApplyResult.Done;
    }

    /// <inheritdoc />
    public Task RevertAsync(TweakContext context, JsonObject snapshot, CancellationToken cancellationToken) =>
        context.Commands.RunAsync(_file, _defaultArguments, cancellationToken);

    /// <inheritdoc />
    public Task RestoreDefaultAsync(TweakContext context, CancellationToken cancellationToken) =>
        context.Commands.RunAsync(_file, _defaultArguments, cancellationToken);
}

/// <summary>Disables Windows memory compression or page combining through WMI.</summary>
public sealed class MmAgentOperation : ITweakOperation
{
    private readonly bool _memoryCompression;

    /// <summary>Initializes a new instance of the <see cref="MmAgentOperation"/> class.</summary>
    /// <param name="memoryCompression"><see langword="true"/> to control memory compression, <see langword="false"/> for page combining.</param>
    public MmAgentOperation(bool memoryCompression) => _memoryCompression = memoryCompression;

    private string Name => _memoryCompression ? "Memory compression" : "Page combining";

    /// <inheritdoc />
    public Task<OperationState> QueryAsync(TweakContext context, CancellationToken cancellationToken)
    {
        var state = context.Wmi.GetMmAgent();
        return Task.FromResult(state is null
            ? new OperationState($"{Name}: unavailable (requires elevation)", true, Applicable: false)
            : new OperationState($"{Name}: {(Read(state) ? "Enabled" : "Disabled")}", !Read(state)));
    }

    /// <inheritdoc />
    public Task<JsonObject> CaptureAsync(TweakContext context, CancellationToken cancellationToken)
    {
        var state = context.Wmi.GetMmAgent();
        return Task.FromResult(new JsonObject { ["available"] = state is not null, ["enabled"] = state is null || Read(state) });
    }

    /// <inheritdoc />
    public Task<TweakApplyResult> ApplyAsync(TweakContext context, JsonObject snapshot, CancellationToken cancellationToken)
    {
        if (snapshot["available"]?.GetValue<bool>() != true)
        {
            return Task.FromResult(TweakApplyResult.Skipped($"{Name} state is unavailable."));
        }

        Set(context, false);
        return Task.FromResult(TweakApplyResult.Done);
    }

    /// <inheritdoc />
    public Task RevertAsync(TweakContext context, JsonObject snapshot, CancellationToken cancellationToken)
    {
        if (snapshot["available"]?.GetValue<bool>() == true)
        {
            Set(context, snapshot["enabled"]?.GetValue<bool>() != false);
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task RestoreDefaultAsync(TweakContext context, CancellationToken cancellationToken)
    {
        Set(context, true);
        return Task.CompletedTask;
    }

    private bool Read(MmAgentState state) => _memoryCompression ? state.MemoryCompression : state.PageCombining;

    private void Set(TweakContext context, bool enabled) =>
        context.Wmi.SetMmAgent(_memoryCompression ? enabled : null, _memoryCompression ? null : enabled);
}

/// <summary>Replaces the managed page file with a fixed-size page file on machines with enough RAM.</summary>
public sealed class StaticPagefileOperation : ITweakOperation
{
    private const ulong MinimumRamBytes = 16UL * 1024 * 1024 * 1024;
    private readonly uint _sizeMb;

    /// <summary>Initializes a new instance of the <see cref="StaticPagefileOperation"/> class.</summary>
    /// <param name="sizeMb">Fixed page file size in MB.</param>
    public StaticPagefileOperation(uint sizeMb) => _sizeMb = sizeMb;

    /// <inheritdoc />
    public Task<OperationState> QueryAsync(TweakContext context, CancellationToken cancellationToken)
    {
        var config = context.Wmi.GetPagefileConfig();
        bool isStatic = !config.AutomaticManaged && config.Entries.Any(e => e.InitialSizeMb == _sizeMb && e.MaximumSizeMb == _sizeMb);
        string text = config.AutomaticManaged ? "System managed" : isStatic ? $"Static {_sizeMb} MB fixed" : "Custom size";
        return Task.FromResult(new OperationState(text, isStatic));
    }

    /// <inheritdoc />
    public Task<JsonObject> CaptureAsync(TweakContext context, CancellationToken cancellationToken)
    {
        var config = context.Wmi.GetPagefileConfig();
        var entries = new JsonArray([.. config.Entries.Select(e => (JsonNode?)new JsonObject
        {
            ["name"] = e.Name,
            ["initial"] = (long)e.InitialSizeMb,
            ["maximum"] = (long)e.MaximumSizeMb,
        })]);
        return Task.FromResult(new JsonObject { ["automatic"] = config.AutomaticManaged, ["entries"] = entries });
    }

    /// <inheritdoc />
    public Task<TweakApplyResult> ApplyAsync(TweakContext context, JsonObject snapshot, CancellationToken cancellationToken)
    {
        if (context.TotalPhysicalMemoryBytes < MinimumRamBytes)
        {
            return Task.FromResult(TweakApplyResult.Skipped("Static page file skipped: less than 16 GB of RAM installed."));
        }

        string systemDrive = System.IO.Path.GetPathRoot(Environment.SystemDirectory) ?? @"C:\";
        var existing = context.Wmi.GetPagefileConfig().Entries
            .FirstOrDefault(e => e.Name.StartsWith(systemDrive, StringComparison.OrdinalIgnoreCase));
        string name = existing?.Name ?? System.IO.Path.Combine(systemDrive, "pagefile.sys");
        context.Wmi.ApplyPagefileConfig(new PagefileConfig(false, [new PageFileEntry(name, _sizeMb, _sizeMb)]));
        return Task.FromResult(TweakApplyResult.Done);
    }

    /// <inheritdoc />
    public Task RevertAsync(TweakContext context, JsonObject snapshot, CancellationToken cancellationToken)
    {
        bool automatic = snapshot["automatic"]?.GetValue<bool>() != false;
        var entries = (snapshot["entries"] as JsonArray)?
            .OfType<JsonObject>()
            .Select(o => new PageFileEntry(
                (string?)o["name"] ?? string.Empty,
                (uint)(o["initial"]?.GetValue<long>() ?? 0),
                (uint)(o["maximum"]?.GetValue<long>() ?? 0)))
            .ToList() ?? [];
        context.Wmi.ApplyPagefileConfig(new PagefileConfig(automatic, entries));
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task RestoreDefaultAsync(TweakContext context, CancellationToken cancellationToken)
    {
        context.Wmi.ApplyPagefileConfig(new PagefileConfig(true, []));
        return Task.CompletedTask;
    }
}

/// <summary>Stops Windows from powering network adapters down.</summary>
public sealed class NicPowerSavingOperation : ITweakOperation
{
    /// <inheritdoc />
    public Task<OperationState> QueryAsync(TweakContext context, CancellationToken cancellationToken)
    {
        var states = context.Wmi.GetNicPowerStates();
        if (states.Count == 0)
        {
            return Task.FromResult(new OperationState("No adapters expose power management", true, Applicable: false));
        }

        int off = states.Count(s => !s.Enabled);
        return Task.FromResult(new OperationState($"{off} of {states.Count} adapters have power saving off", off == states.Count));
    }

    /// <inheritdoc />
    public Task<JsonObject> CaptureAsync(TweakContext context, CancellationToken cancellationToken)
    {
        var adapters = new JsonObject();
        foreach (var state in context.Wmi.GetNicPowerStates())
        {
            adapters[state.InstanceName] = state.Enabled;
        }

        return Task.FromResult(new JsonObject { ["adapters"] = adapters });
    }

    /// <inheritdoc />
    public Task<TweakApplyResult> ApplyAsync(TweakContext context, JsonObject snapshot, CancellationToken cancellationToken)
    {
        var states = context.Wmi.GetNicPowerStates();
        foreach (var state in states)
        {
            context.Wmi.SetNicPowerEnabled(state.InstanceName, false);
        }

        return Task.FromResult(states.Count == 0 ? TweakApplyResult.Skipped("No network adapters expose power management.") : TweakApplyResult.Done);
    }

    /// <inheritdoc />
    public Task RevertAsync(TweakContext context, JsonObject snapshot, CancellationToken cancellationToken)
    {
        if (snapshot["adapters"] is JsonObject adapters)
        {
            foreach (var (instance, enabled) in adapters)
            {
                context.Wmi.SetNicPowerEnabled(instance, enabled?.GetValue<bool>() != false);
            }
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task RestoreDefaultAsync(TweakContext context, CancellationToken cancellationToken)
    {
        foreach (var state in context.Wmi.GetNicPowerStates())
        {
            context.Wmi.SetNicPowerEnabled(state.InstanceName, true);
        }

        return Task.CompletedTask;
    }
}
