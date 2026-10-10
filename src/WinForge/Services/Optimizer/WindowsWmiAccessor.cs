using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Management;

namespace WingetStore.Services.Optimizer;

/// <summary>Memory manager agent switches relevant to the optimizer.</summary>
/// <param name="MemoryCompression">Whether memory compression is enabled.</param>
/// <param name="PageCombining">Whether page combining is enabled.</param>
public sealed record MmAgentState(bool MemoryCompression, bool PageCombining);

/// <summary>A configured page file.</summary>
/// <param name="Name">Full page file path, e.g. <c>C:\pagefile.sys</c>.</param>
/// <param name="InitialSizeMb">Initial size in MB.</param>
/// <param name="MaximumSizeMb">Maximum size in MB.</param>
public sealed record PageFileEntry(string Name, uint InitialSizeMb, uint MaximumSizeMb);

/// <summary>Page file configuration.</summary>
/// <param name="AutomaticManaged">Whether Windows manages page files automatically.</param>
/// <param name="Entries">Explicit page file settings (empty when automatically managed).</param>
public sealed record PagefileConfig(bool AutomaticManaged, IReadOnlyList<PageFileEntry> Entries);

/// <summary>Power-management permission of one network adapter.</summary>
/// <param name="InstanceName">WMI instance name.</param>
/// <param name="Enabled">Whether Windows may power the device down to save energy.</param>
public sealed record NicPowerState(string InstanceName, bool Enabled);

/// <summary>Static hardware and OS facts shown in the audit header.</summary>
/// <param name="CpuName">Processor marketing name.</param>
/// <param name="CpuVendor">Processor vendor identifier (for example <c>AuthenticAMD</c>).</param>
/// <param name="OsCaption">Operating system caption.</param>
/// <param name="OsVersion">Operating system version string.</param>
/// <param name="ProcessCount">Number of running processes.</param>
/// <param name="RunningServiceCount">Number of running services.</param>
public sealed record SystemSummary(string CpuName, string CpuVendor, string OsCaption, string OsVersion, int ProcessCount, int RunningServiceCount)
{
    /// <summary>Gets a value indicating whether the processor is made by AMD.</summary>
    public bool IsAmdCpu => CpuVendor.Contains("AMD", StringComparison.OrdinalIgnoreCase);
}

/// <summary>WMI-backed system facilities that have no registry or command-line equivalent.</summary>
public interface IWmiAccessor
{
    /// <summary>Reads the memory manager agent state; <see langword="null"/> when unavailable (for example not elevated).</summary>
    /// <returns>The state, or <see langword="null"/>.</returns>
    MmAgentState? GetMmAgent();

    /// <summary>Enables or disables memory manager features; <see langword="null"/> arguments are left unchanged.</summary>
    /// <param name="memoryCompression">Target memory compression state.</param>
    /// <param name="pageCombining">Target page combining state.</param>
    void SetMmAgent(bool? memoryCompression, bool? pageCombining);

    /// <summary>Reads the page file configuration.</summary>
    /// <returns>The current configuration.</returns>
    PagefileConfig GetPagefileConfig();

    /// <summary>Applies a page file configuration.</summary>
    /// <param name="config">Configuration to apply.</param>
    void ApplyPagefileConfig(PagefileConfig config);

    /// <summary>Lists the power-management state of enabled network adapters.</summary>
    /// <returns>One entry per adapter exposing power management.</returns>
    IReadOnlyList<NicPowerState> GetNicPowerStates();

    /// <summary>Sets the power-management permission of one adapter.</summary>
    /// <param name="instanceName">WMI instance name from <see cref="GetNicPowerStates"/>.</param>
    /// <param name="enabled">Whether Windows may power the device down.</param>
    void SetNicPowerEnabled(string instanceName, bool enabled);

    /// <summary>Gathers hardware and OS facts.</summary>
    /// <returns>The summary.</returns>
    SystemSummary GetSystemSummary();

    /// <summary>Creates a system restore point.</summary>
    /// <param name="description">Restore point description.</param>
    void CreateRestorePoint(string description);
}

/// <summary>Real WMI implementation based on <c>System.Management</c>.</summary>
public sealed class WindowsWmiAccessor : IWmiAccessor
{
    private const string MmAgentNamespace = @"root\Microsoft\Windows\PS_MMAgent";
    private const uint RestorePointTypeModifySettings = 12;
    private const uint EventTypeBeginSystemChange = 100;

    /// <inheritdoc />
    public MmAgentState? GetMmAgent()
    {
        try
        {
            using var mmAgent = new ManagementClass(new ManagementScope(MmAgentNamespace), new ManagementPath("PS_MMAgent"), null);
            using var output = mmAgent.InvokeMethod("Get", null, null);
            if (output?["cmdletOutput"] is not ManagementBaseObject components)
            {
                return null;
            }

            return new MmAgentState(
                Convert.ToBoolean(components["MemoryCompression"], CultureInfo.InvariantCulture),
                Convert.ToBoolean(components["PageCombining"], CultureInfo.InvariantCulture));
        }
        catch (Exception ex) when (ex is ManagementException or UnauthorizedAccessException or InvalidCastException)
        {
            return null;
        }
    }

    /// <inheritdoc />
    public void SetMmAgent(bool? memoryCompression, bool? pageCombining)
    {
        using var mmAgent = new ManagementClass(new ManagementScope(MmAgentNamespace), new ManagementPath("PS_MMAgent"), null);
        InvokeToggle(mmAgent, memoryCompression, "MemoryCompression");
        InvokeToggle(mmAgent, pageCombining, "PageCombining");
    }

    /// <inheritdoc />
    public PagefileConfig GetPagefileConfig()
    {
        bool automatic = false;
        using (var system = new ManagementObjectSearcher("SELECT AutomaticManagedPagefile FROM Win32_ComputerSystem"))
        {
            foreach (ManagementBaseObject item in system.Get())
            {
                automatic = Convert.ToBoolean(item["AutomaticManagedPagefile"], CultureInfo.InvariantCulture);
                item.Dispose();
            }
        }

        var entries = new List<PageFileEntry>();
        using (var settings = new ManagementObjectSearcher("SELECT Name, InitialSize, MaximumSize FROM Win32_PageFileSetting"))
        {
            foreach (ManagementBaseObject item in settings.Get())
            {
                entries.Add(new PageFileEntry(
                    Convert.ToString(item["Name"], CultureInfo.InvariantCulture) ?? string.Empty,
                    Convert.ToUInt32(item["InitialSize"], CultureInfo.InvariantCulture),
                    Convert.ToUInt32(item["MaximumSize"], CultureInfo.InvariantCulture)));
                item.Dispose();
            }
        }

        return new PagefileConfig(automatic, entries);
    }

    /// <inheritdoc />
    public void ApplyPagefileConfig(PagefileConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);
        var options = new ConnectionOptions { EnablePrivileges = true };
        var scope = new ManagementScope(@"\\.\root\cimv2", options);
        scope.Connect();

        using (var system = new ManagementObjectSearcher(scope, new ObjectQuery("SELECT * FROM Win32_ComputerSystem")))
        {
            foreach (ManagementObject item in system.Get())
            {
                item["AutomaticManagedPagefile"] = config.AutomaticManaged;
                item.Put();
                item.Dispose();
            }
        }

        if (config.AutomaticManaged)
        {
            return;
        }

        foreach (var entry in config.Entries)
        {
            string escaped = entry.Name.Replace("\\", "\\\\", StringComparison.Ordinal);
            using var searcher = new ManagementObjectSearcher(scope, new ObjectQuery($"SELECT * FROM Win32_PageFileSetting WHERE Name = '{escaped}'"));
            ManagementObject? existing = searcher.Get().Cast<ManagementObject>().FirstOrDefault();
            if (existing is null)
            {
                using var settingClass = new ManagementClass(scope, new ManagementPath("Win32_PageFileSetting"), null);
                existing = settingClass.CreateInstance();
                existing["Name"] = entry.Name;
            }

            existing["InitialSize"] = entry.InitialSizeMb;
            existing["MaximumSize"] = entry.MaximumSizeMb;
            existing.Put();
            existing.Dispose();
        }
    }

    /// <inheritdoc />
    public IReadOnlyList<NicPowerState> GetNicPowerStates()
    {
        var pnpIds = new List<string>();
        using (var adapters = new ManagementObjectSearcher("SELECT PNPDeviceID FROM Win32_NetworkAdapter WHERE NetEnabled = True"))
        {
            foreach (ManagementBaseObject item in adapters.Get())
            {
                if (item["PNPDeviceID"] is string id && id.Length > 0)
                {
                    pnpIds.Add(id);
                }

                item.Dispose();
            }
        }

        var result = new List<NicPowerState>();
        using var power = new ManagementObjectSearcher(@"root\wmi", "SELECT InstanceName, Enable FROM MSPower_DeviceEnable");
        foreach (ManagementBaseObject item in power.Get())
        {
            string instance = Convert.ToString(item["InstanceName"], CultureInfo.InvariantCulture) ?? string.Empty;
            if (pnpIds.Any(id => instance.Contains(id, StringComparison.OrdinalIgnoreCase)))
            {
                result.Add(new NicPowerState(instance, Convert.ToBoolean(item["Enable"], CultureInfo.InvariantCulture)));
            }

            item.Dispose();
        }

        return result;
    }

    /// <inheritdoc />
    public void SetNicPowerEnabled(string instanceName, bool enabled)
    {
        ArgumentException.ThrowIfNullOrEmpty(instanceName);
        string escaped = instanceName.Replace("\\", "\\\\", StringComparison.Ordinal);
        using var power = new ManagementObjectSearcher(@"root\wmi", $"SELECT * FROM MSPower_DeviceEnable WHERE InstanceName = '{escaped}'");
        foreach (ManagementObject item in power.Get())
        {
            item["Enable"] = enabled;
            item.Put();
            item.Dispose();
        }
    }

    /// <inheritdoc />
    public SystemSummary GetSystemSummary()
    {
        string cpuName = "Unknown CPU";
        string vendor = string.Empty;
        using (var cpu = new ManagementObjectSearcher("SELECT Name, Manufacturer FROM Win32_Processor"))
        {
            foreach (ManagementBaseObject item in cpu.Get())
            {
                cpuName = (Convert.ToString(item["Name"], CultureInfo.InvariantCulture) ?? cpuName).Trim();
                vendor = Convert.ToString(item["Manufacturer"], CultureInfo.InvariantCulture) ?? string.Empty;
                item.Dispose();
                break;
            }
        }

        string caption = "Windows";
        string version = string.Empty;
        using (var os = new ManagementObjectSearcher("SELECT Caption, Version FROM Win32_OperatingSystem"))
        {
            foreach (ManagementBaseObject item in os.Get())
            {
                caption = (Convert.ToString(item["Caption"], CultureInfo.InvariantCulture) ?? caption).Trim();
                version = Convert.ToString(item["Version"], CultureInfo.InvariantCulture) ?? string.Empty;
                item.Dispose();
                break;
            }
        }

        int services = 0;
        using (var running = new ManagementObjectSearcher("SELECT Name FROM Win32_Service WHERE State = 'Running'"))
        {
            services = running.Get().Count;
        }

        var processes = Process.GetProcesses();
        int processCount = processes.Length;
        foreach (var process in processes)
        {
            process.Dispose();
        }

        return new SystemSummary(cpuName, vendor, caption, version, processCount, services);
    }

    /// <inheritdoc />
    public void CreateRestorePoint(string description)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(description);
        using var restore = new ManagementClass(new ManagementScope(@"\\.\root\default"), new ManagementPath("SystemRestore"), null);

        // Mirrors Enable-ComputerRestore: make sure protection is on for the system drive (best effort).
        try
        {
            string systemDrive = Path.GetPathRoot(Environment.GetFolderPath(Environment.SpecialFolder.Windows)) ?? @"C:\";
            restore.InvokeMethod("Enable", [systemDrive, 0]);
        }
        catch (ManagementException)
        {
            // Already enabled or not supported; creation below reports the real failure.
        }

        object? code = restore.InvokeMethod("CreateRestorePoint", [description, RestorePointTypeModifySettings, EventTypeBeginSystemChange]);
        uint returnValue = Convert.ToUInt32(code, CultureInfo.InvariantCulture);
        if (returnValue != 0)
        {
            throw new InvalidOperationException($"CreateRestorePoint failed with WMI return value {returnValue}.");
        }
    }

    private static void InvokeToggle(ManagementClass mmAgent, bool? desired, string component)
    {
        if (desired is not { } value)
        {
            return;
        }

        using var parameters = mmAgent.GetMethodParameters(value ? "Enable" : "Disable");
        parameters[component] = true;
        using var result = mmAgent.InvokeMethod(value ? "Enable" : "Disable", parameters, null);
        uint code = result is null ? 0 : Convert.ToUInt32(result["ReturnValue"] ?? 0u, CultureInfo.InvariantCulture);
        if (code != 0)
        {
            throw new InvalidOperationException($"PS_MMAgent.{(value ? "Enable" : "Disable")}({component}) failed with return value {code}.");
        }
    }
}
