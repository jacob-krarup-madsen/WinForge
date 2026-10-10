using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Security;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Win32;

namespace WingetStore.Services.Optimizer;

/// <summary>Registry roots used by optimizer tweaks.</summary>
public enum RegistryRoot
{
    /// <summary>HKEY_LOCAL_MACHINE.</summary>
    LocalMachine,

    /// <summary>HKEY_CURRENT_USER.</summary>
    CurrentUser,

    /// <summary>HKEY_CLASSES_ROOT.</summary>
    ClassesRoot,
}

/// <summary>A typed registry value.</summary>
/// <param name="Data">The value data (<see cref="int"/>, <see cref="long"/>, <see cref="string"/>, <see cref="string"/>[] or <see cref="byte"/>[]).</param>
/// <param name="Kind">The registry value kind.</param>
public sealed record RegistryValueData(object Data, RegistryValueKind Kind)
{
    /// <summary>Compares two values by kind and content (arrays compared element-wise).</summary>
    /// <param name="other">Value to compare with.</param>
    /// <returns><see langword="true"/> when kind and content are equal.</returns>
    public bool ContentEquals(RegistryValueData? other)
    {
        if (other is null || other.Kind != Kind)
        {
            return false;
        }

        return (Data, other.Data) switch
        {
            (string[] a, string[] b) => a.SequenceEqual(b, StringComparer.Ordinal),
            (byte[] a, byte[] b) => a.AsSpan().SequenceEqual(b),
            (string a, string b) => string.Equals(a, b, StringComparison.Ordinal),
            (var a, var b) => Equals(a, b),
        };
    }

    /// <summary>Formats the value for display.</summary>
    /// <returns>Human-readable text.</returns>
    public override string ToString() => Data switch
    {
        string[] lines => string.Join("; ", lines),
        byte[] bytes => Convert.ToHexString(bytes),
        _ => Convert.ToString(Data, System.Globalization.CultureInfo.InvariantCulture) ?? string.Empty,
    };
}

/// <summary>Abstraction over the Windows registry so tweaks are testable without touching the machine.</summary>
public interface IRegistryAccessor
{
    /// <summary>Reads a value; returns <see langword="null"/> when the key or value does not exist.</summary>
    /// <param name="root">Registry root.</param>
    /// <param name="subKey">Key path below the root.</param>
    /// <param name="name">Value name; empty for the default value.</param>
    /// <returns>The value, or <see langword="null"/>.</returns>
    RegistryValueData? GetValue(RegistryRoot root, string subKey, string name);

    /// <summary>Writes a value, creating the key if needed.</summary>
    /// <param name="root">Registry root.</param>
    /// <param name="subKey">Key path below the root.</param>
    /// <param name="name">Value name; empty for the default value.</param>
    /// <param name="value">Value to write.</param>
    void SetValue(RegistryRoot root, string subKey, string name, RegistryValueData value);

    /// <summary>Deletes a value.</summary>
    /// <param name="root">Registry root.</param>
    /// <param name="subKey">Key path below the root.</param>
    /// <param name="name">Value name.</param>
    /// <returns><see langword="true"/> when a value was deleted.</returns>
    bool DeleteValue(RegistryRoot root, string subKey, string name);

    /// <summary>Deletes a key only when it has no values and no sub keys.</summary>
    /// <param name="root">Registry root.</param>
    /// <param name="subKey">Key path below the root.</param>
    /// <returns><see langword="true"/> when the key was deleted.</returns>
    bool DeleteKeyIfEmpty(RegistryRoot root, string subKey);

    /// <summary>Checks whether a key exists.</summary>
    /// <param name="root">Registry root.</param>
    /// <param name="subKey">Key path below the root.</param>
    /// <returns><see langword="true"/> when the key exists.</returns>
    bool KeyExists(RegistryRoot root, string subKey);

    /// <summary>Lists the names of direct sub keys; inaccessible keys yield an empty list.</summary>
    /// <param name="root">Registry root.</param>
    /// <param name="subKey">Key path below the root.</param>
    /// <returns>Sub key names.</returns>
    IReadOnlyList<string> GetSubKeyNames(RegistryRoot root, string subKey);
}

/// <summary>Registry implementation using the 64-bit view of the real registry.</summary>
public sealed class WindowsRegistryAccessor : IRegistryAccessor
{
    /// <inheritdoc />
    public RegistryValueData? GetValue(RegistryRoot root, string subKey, string name)
    {
        try
        {
            using var baseKey = Open(root);
            using var key = baseKey.OpenSubKey(subKey, writable: false);
            object? data = key?.GetValue(name, null, RegistryValueOptions.DoNotExpandEnvironmentNames);
            return data is null || key is null ? null : new RegistryValueData(data, key.GetValueKind(name));
        }
        catch (Exception ex) when (ex is SecurityException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    /// <inheritdoc />
    public void SetValue(RegistryRoot root, string subKey, string name, RegistryValueData value)
    {
        ArgumentNullException.ThrowIfNull(value);
        using var baseKey = Open(root);
        using var key = baseKey.CreateSubKey(subKey, writable: true)
            ?? throw new InvalidOperationException($"Unable to create or open registry key '{subKey}'.");
        key.SetValue(name, value.Data, value.Kind);
    }

    /// <inheritdoc />
    public bool DeleteValue(RegistryRoot root, string subKey, string name)
    {
        using var baseKey = Open(root);
        using var key = baseKey.OpenSubKey(subKey, writable: true);
        if (key is null || key.GetValue(name, null, RegistryValueOptions.DoNotExpandEnvironmentNames) is null)
        {
            return false;
        }

        key.DeleteValue(name, throwOnMissingValue: false);
        return true;
    }

    /// <inheritdoc />
    public bool DeleteKeyIfEmpty(RegistryRoot root, string subKey)
    {
        int separator = subKey.LastIndexOf('\\');
        if (separator < 0)
        {
            return false;
        }

        using var baseKey = Open(root);
        using (var key = baseKey.OpenSubKey(subKey, writable: false))
        {
            if (key is null || key.ValueCount > 0 || key.SubKeyCount > 0)
            {
                return false;
            }
        }

        using var parent = baseKey.OpenSubKey(subKey[..separator], writable: true);
        if (parent is null)
        {
            return false;
        }

        parent.DeleteSubKey(subKey[(separator + 1)..], throwOnMissingSubKey: false);
        return true;
    }

    /// <inheritdoc />
    public bool KeyExists(RegistryRoot root, string subKey)
    {
        try
        {
            using var baseKey = Open(root);
            using var key = baseKey.OpenSubKey(subKey, writable: false);
            return key is not null;
        }
        catch (Exception ex) when (ex is SecurityException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    /// <inheritdoc />
    public IReadOnlyList<string> GetSubKeyNames(RegistryRoot root, string subKey)
    {
        try
        {
            using var baseKey = Open(root);
            using var key = baseKey.OpenSubKey(subKey, writable: false);
            return key?.GetSubKeyNames() ?? [];
        }
        catch (Exception ex) when (ex is SecurityException or UnauthorizedAccessException)
        {
            return [];
        }
    }

    private static RegistryKey Open(RegistryRoot root) => RegistryKey.OpenBaseKey(
        root switch
        {
            RegistryRoot.LocalMachine => RegistryHive.LocalMachine,
            RegistryRoot.CurrentUser => RegistryHive.CurrentUser,
            RegistryRoot.ClassesRoot => RegistryHive.ClassesRoot,
            _ => throw new ArgumentOutOfRangeException(nameof(root), root, null),
        },
        RegistryView.Registry64);
}

/// <summary>Result of an external command.</summary>
/// <param name="ExitCode">Process exit code.</param>
/// <param name="Output">Combined standard output and standard error.</param>
public sealed record CommandResult(int ExitCode, string Output)
{
    /// <summary>Gets a value indicating whether the command exited with code zero.</summary>
    public bool Succeeded => ExitCode == 0;
}

/// <summary>Runs external system tools (<c>powercfg</c>, <c>bcdedit</c>, <c>sc</c>, ...) and captures their output.</summary>
public interface ICommandExecutor
{
    /// <summary>Runs a command to completion.</summary>
    /// <param name="fileName">Executable name or path.</param>
    /// <param name="arguments">Command-line arguments.</param>
    /// <param name="cancellationToken">Token that kills the process when cancelled.</param>
    /// <returns>Exit code and combined output.</returns>
    Task<CommandResult> RunAsync(string fileName, string arguments, CancellationToken cancellationToken = default);
}

/// <summary>Process-based <see cref="ICommandExecutor"/>.</summary>
public sealed class ProcessCommandExecutor : ICommandExecutor
{
    private static readonly TimeSpan DefaultTimeout = TimeSpan.FromMinutes(2);

    /// <inheritdoc />
    public async Task<CommandResult> RunAsync(string fileName, string arguments, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fileName);
        var startInfo = new ProcessStartInfo
        {
            FileName = fileName,
            Arguments = arguments,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
        };

        using var process = new Process { StartInfo = startInfo };
        var output = new StringBuilder();
        var gate = new object();
        void Append(object? _, DataReceivedEventArgs e)
        {
            if (e.Data is null)
            {
                return;
            }

            lock (gate)
            {
                output.AppendLine(e.Data);
            }
        }

        process.OutputDataReceived += Append;
        process.ErrorDataReceived += Append;
        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(DefaultTimeout);
        try
        {
            await process.WaitForExitAsync(timeout.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            try
            {
                process.Kill(entireProcessTree: true);
            }
            catch (Exception ex) when (ex is InvalidOperationException or System.ComponentModel.Win32Exception)
            {
                // Process already exited.
            }

            cancellationToken.ThrowIfCancellationRequested();
            throw new TimeoutException($"'{fileName}' did not finish within {DefaultTimeout.TotalSeconds:0} seconds.");
        }

        // Ensure asynchronous output handlers have drained.
        process.WaitForExit();
        lock (gate)
        {
            return new CommandResult(process.ExitCode, output.ToString());
        }
    }
}
