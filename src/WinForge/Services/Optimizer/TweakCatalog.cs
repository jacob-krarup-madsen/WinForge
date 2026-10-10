using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Win32;

namespace WingetStore.Services.Optimizer;

/// <summary>
/// Authoritative catalog of all 25 Windows 11 optimization phases ported from the legacy PowerShell suite.
/// </summary>
public static class TweakCatalog
{
    private const string DisplayClassKey = @"SYSTEM\CurrentControlSet\Control\Class\{4d36e968-e325-11ce-bfc1-08002be10318}";
    private const string TcpipInterfacesKey = @"SYSTEM\CurrentControlSet\Services\Tcpip\Parameters\Interfaces";
    private const string NetBtInterfacesKey = @"SYSTEM\CurrentControlSet\Services\NetBT\Parameters\Interfaces";
    private const string UsbEnumKey = @"SYSTEM\CurrentControlSet\Enum\USB";

    /// <summary>Gets the complete ordered list of tweaks.</summary>
    public static IReadOnlyList<Tweak> All { get; } = CreateAll();

    private static IReadOnlyList<Tweak> CreateAll() =>
    [
        new Tweak(
            new TweakInfo(
                Id: "DisableTelemetry",
                Title: "Disable Telemetry, Activity Feed & Delivery Optimization P2P",
                Category: TweakCategory.Privacy,
                Phase: 2,
                DefaultState: "Enabled (AllowTelemetry = 1, P2P On)",
                TargetState: "Disabled (AllowTelemetry = 0, P2P Off)",
                PositiveImpact: "Stops diagnostic background telemetry uploads, activity tracking, and P2P update seeding.",
                TradeOff: "Windows Diagnostic Feedback and cross-device timeline history are disabled."),
            DWord(RegistryRoot.LocalMachine, @"SOFTWARE\Policies\Microsoft\Windows\DataCollection", "AllowTelemetry", 0, null),
            DWord(RegistryRoot.LocalMachine, @"SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\DataCollection", "AllowTelemetry", 0, 1),
            DWord(RegistryRoot.LocalMachine, @"SOFTWARE\Policies\Microsoft\Windows\System", "EnableActivityFeed", 0, null),
            DWord(RegistryRoot.LocalMachine, @"SOFTWARE\Policies\Microsoft\Windows\System", "PublishUserActivities", 0, null),
            DWord(RegistryRoot.LocalMachine, @"SOFTWARE\Policies\Microsoft\Windows\System", "UploadUserActivities", 0, null),
            DWord(RegistryRoot.LocalMachine, @"SOFTWARE\Microsoft\Windows\CurrentVersion\DeliveryOptimization\Config", "DODownloadMode", 0, 1),
            new ScheduledTaskOperation(@"\Microsoft\Windows\Application Experience\Microsoft Compatibility Appraiser"),
            new ScheduledTaskOperation(@"\Microsoft\Windows\Application Experience\ProgramDataUpdater"),
            new ScheduledTaskOperation(@"\Microsoft\Windows\Autochk\Proxy"),
            new ScheduledTaskOperation(@"\Microsoft\Windows\Customer Experience Improvement Program\Consolidator"),
            new ScheduledTaskOperation(@"\Microsoft\Windows\Customer Experience Improvement Program\UsbCeip"),
            new ScheduledTaskOperation(@"\Microsoft\Windows\DiskDiagnostic\Microsoft-Windows-DiskDiagnosticDataCollector")),

        new Tweak(
            new TweakInfo(
                Id: "DisableServices",
                Title: "Streamline Non-Essential Background Services",
                Category: TweakCategory.Services,
                Phase: 3,
                DefaultState: "Running (Automatic)",
                TargetState: "Disabled",
                PositiveImpact: "Reclaims background CPU/RAM from telemetry, print spooler, search indexer, and Xbox background services.",
                TradeOff: "Local printing, indexed search, and Xbox Live background integration stay off until re-enabled."),
            new ServiceOperation("DiagTrack"),
            new ServiceOperation("dmwappushservice"),
            new ServiceOperation("Spooler"),
            new ServiceOperation("WSearch"),
            new ServiceOperation("XblAuthManager"),
            new ServiceOperation("XblGameSave"),
            new ServiceOperation("XboxGipSvc"),
            new ServiceOperation("XboxNetApiSvc")),

        new Tweak(
            new TweakInfo(
                Id: "DisableHVCI",
                Title: "Disable Memory Integrity (HVCI)",
                Category: TweakCategory.Security,
                Phase: 4,
                DefaultState: "1 (Enabled)",
                TargetState: "0 (Disabled)",
                PositiveImpact: "Eliminates 5-10% CPU virtualization overhead in games and removes frame-pacing micro-stutter.",
                TradeOff: "Disables kernel code-integrity hypervisor guard against unsigned or vulnerable kernel drivers.",
                ReducesSecurity: true),
            DWord(RegistryRoot.LocalMachine, @"SYSTEM\CurrentControlSet\Control\DeviceGuard\Scenarios\HypervisorEnforcedCodeIntegrity", "Enabled", 0, 1)),

        new Tweak(
            new TweakInfo(
                Id: "DisableBingSearch",
                Title: "Disable Start Menu Bing Web Search & Highlights",
                Category: TweakCategory.Desktop,
                Phase: 5,
                DefaultState: "1 (Enabled)",
                TargetState: "0 (Disabled)",
                PositiveImpact: "Start menu search results are 100% instant local files without web query latency.",
                TradeOff: "Start menu will not display Bing web search suggestions."),
            DWord(RegistryRoot.CurrentUser, @"SOFTWARE\Policies\Microsoft\Windows\Explorer", "DisableSearchBoxSuggestions", 1, null),
            DWord(RegistryRoot.CurrentUser, @"SOFTWARE\Microsoft\Windows\CurrentVersion\Search", "BingSearchEnabled", 0, null),
            DWord(RegistryRoot.CurrentUser, @"Software\Microsoft\Windows\CurrentVersion\SearchSettings", "IsDeviceSearchWithHighlightsEnabled", 0, 1)),

        new Tweak(
            new TweakInfo(
                Id: "AddTakeOwnership",
                Title: "Add 'Take Ownership' Right-Click Context Menu",
                Category: TweakCategory.Desktop,
                Phase: 6,
                DefaultState: "Not present",
                TargetState: "Registered on files and folders",
                PositiveImpact: "Adds a one-click elevated ownership and ACL grant action to Explorer context menus.",
                TradeOff: "Adds an extra item to the classic Explorer context menu."),
            Str(RegistryRoot.ClassesRoot, @"*\shell\runas", string.Empty, "Take Ownership", null),
            Str(RegistryRoot.ClassesRoot, @"*\shell\runas", "NoWorkingDirectory", string.Empty, null),
            Str(RegistryRoot.ClassesRoot, @"*\shell\runas\command", string.Empty, "cmd.exe /c takeown /f \"%1\" && icacls \"%1\" /grant administrators:F", null),
            Str(RegistryRoot.ClassesRoot, @"Directory\shell\runas", string.Empty, "Take Ownership", null),
            Str(RegistryRoot.ClassesRoot, @"Directory\shell\runas", "NoWorkingDirectory", string.Empty, null),
            Str(RegistryRoot.ClassesRoot, @"Directory\shell\runas\command", string.Empty, "cmd.exe /c takeown /f \"%1\" /r /d y && icacls \"%1\" /grant administrators:F /t", null)),

        new Tweak(
            new TweakInfo(
                Id: "Win32PrioritySeparation",
                Title: "CPU Quantum Win32PrioritySeparation (0x26)",
                Category: TweakCategory.Cpu,
                Phase: 7,
                DefaultState: "2 (Default Variable Quantum)",
                TargetState: "38 (0x26 Short Fixed 3:1 Foreground Boost)",
                PositiveImpact: "Gives the active foreground window 3x CPU quantum priority, reducing input and frame-time jitter.",
                TradeOff: "Background batch jobs receive slightly smaller time slices while a foreground app is active."),
            DWord(RegistryRoot.LocalMachine, @"SYSTEM\CurrentControlSet\Control\PriorityControl", "Win32PrioritySeparation", 38, 2)),

        new Tweak(
            new TweakInfo(
                Id: "DisableGameDVR",
                Title: "Disable GameDVR Background Video Capture",
                Category: TweakCategory.Gpu,
                Phase: 8,
                DefaultState: "1 (Enabled)",
                TargetState: "0 (Disabled)",
                PositiveImpact: "Eliminates background GPU encode ring-buffer overhead and DWM capture hooks.",
                TradeOff: "Xbox Game Bar background clip recording is disabled."),
            DWord(RegistryRoot.CurrentUser, @"System\GameConfigStore", "GameDVR_Enabled", 0, 1),
            DWord(RegistryRoot.LocalMachine, @"SOFTWARE\Policies\Microsoft\Windows\GameDVR", "AllowGameDVR", 0, null)),

        new Tweak(
            new TweakInfo(
                Id: "DisableUsbSelectiveSuspend",
                Title: "Disable USB Selective Suspend Power Policy",
                Category: TweakCategory.Peripherals,
                Phase: 9,
                DefaultState: "AC index 1 (Enabled)",
                TargetState: "AC index 0 (Disabled)",
                PositiveImpact: "Keeps USB mice, keyboards, and audio DACs on continuous full power with zero wake latency.",
                TradeOff: "Slightly higher USB controller idle power draw."),
            new PowerSettingOperation("2a737441-1930-4402-8d77-b2bebba308a3", "48678926-e24f-4730-b564-8f2887c00810", 0, 1)),

        new Tweak(
            new TweakInfo(
                Id: "TuneMMCSSAudio",
                Title: "Elevate MMCSS Pro Audio & Audio Thread Priority",
                Category: TweakCategory.Peripherals,
                Phase: 10,
                DefaultState: "Medium / Normal",
                TargetState: "High / Priority 2",
                PositiveImpact: "Prioritizes real-time audio threads to prevent buffer underruns, crackles, and dropouts.",
                TradeOff: "Minor CPU scheduling priority shift toward multimedia audio threads."),
            Str(RegistryRoot.LocalMachine, @"SOFTWARE\Microsoft\Windows NT\CurrentVersion\Multimedia\SystemProfile\Tasks\Pro Audio", "Scheduling Category", "High", "Medium"),
            Str(RegistryRoot.LocalMachine, @"SOFTWARE\Microsoft\Windows NT\CurrentVersion\Multimedia\SystemProfile\Tasks\Pro Audio", "SFIO Priority", "High", "Normal"),
            DWord(RegistryRoot.LocalMachine, @"SOFTWARE\Microsoft\Windows NT\CurrentVersion\Multimedia\SystemProfile\Tasks\Pro Audio", "Priority", 2, 2),
            Str(RegistryRoot.LocalMachine, @"SOFTWARE\Microsoft\Windows NT\CurrentVersion\Multimedia\SystemProfile\Tasks\Audio", "Scheduling Category", "High", "Medium"),
            Str(RegistryRoot.LocalMachine, @"SOFTWARE\Microsoft\Windows NT\CurrentVersion\Multimedia\SystemProfile\Tasks\Audio", "SFIO Priority", "High", "Normal"),
            DWord(RegistryRoot.LocalMachine, @"SOFTWARE\Microsoft\Windows NT\CurrentVersion\Multimedia\SystemProfile\Tasks\Audio", "Priority", 2, 6)),

        new Tweak(
            new TweakInfo(
                Id: "DisableNicPowerSaving",
                Title: "Disable Network Adapter Power Saving",
                Category: TweakCategory.Network,
                Phase: 11,
                DefaultState: "Power saving enabled",
                TargetState: "Power saving disabled",
                PositiveImpact: "Prevents NIC low-power state transitions that cause packet bursts and ping spikes.",
                TradeOff: "Slightly higher network adapter idle power consumption."),
            new NicPowerSavingOperation()),

        new Tweak(
            new TweakInfo(
                Id: "RemoveQosBandwidthLimit",
                Title: "Remove QoS 20% Reserved Bandwidth Limit",
                Category: TweakCategory.Network,
                Phase: 12,
                DefaultState: "20 (20% Reserved)",
                TargetState: "0 (0% Reserved)",
                PositiveImpact: "Unlocks 100% of link bandwidth for foreground applications and downloads.",
                TradeOff: "Removes packet-scheduler bandwidth reservation."),
            DWord(RegistryRoot.LocalMachine, @"SOFTWARE\Policies\Microsoft\Windows\Psched", "NonBestEffortLimit", 0, null)),

        new Tweak(
            new TweakInfo(
                Id: "DisableUsbAudioSleep",
                Title: "Disable USB Device Enhanced Power Management",
                Category: TweakCategory.Peripherals,
                Phase: 13,
                DefaultState: "1 (Enabled)",
                TargetState: "0 (Disabled)",
                PositiveImpact: "Prevents USB audio interfaces and peripherals from entering selective device sleep.",
                TradeOff: "USB peripherals remain powered while the system is awake."),
            new RegistryEachKeyOperation(
                RegistryRoot.LocalMachine,
                EnumerateUsbDeviceParameterKeys,
                "EnhancedPowerManagementEnabled",
                new RegistryValueData(0, RegistryValueKind.DWord),
                new RegistryValueData(1, RegistryValueKind.DWord),
                onlyWhereValueExists: true)),

        new Tweak(
            new TweakInfo(
                Id: "DisableVBS",
                Title: "Disable Virtualization-Based Security (VBS)",
                Category: TweakCategory.Security,
                Phase: 14,
                DefaultState: "1 (Enabled)",
                TargetState: "0 (Disabled)",
                PositiveImpact: "Frees hardware virtualization registers and eliminates nested page-table translation overhead.",
                TradeOff: "Disables Credential Guard and VBS memory enclaves.",
                ReducesSecurity: true),
            DWord(RegistryRoot.LocalMachine, @"SYSTEM\CurrentControlSet\Control\DeviceGuard", "EnableVirtualizationBasedSecurity", 0, 1)),

        new Tweak(
            new TweakInfo(
                Id: "EnhancedTscSyncPolicy",
                Title: "Enable Enhanced TSC Timer Sync & Platform Tick",
                Category: TweakCategory.Cpu,
                Phase: 15,
                DefaultState: "Default",
                TargetState: "tscsyncpolicy Enhanced, useplatformtick Yes",
                PositiveImpact: "Enforces invariant Time Stamp Counter synchronization across all CPU cores.",
                TradeOff: "Requires a reboot to take effect."),
            new BcdOperation("tscsyncpolicy", "Enhanced", null),
            new BcdOperation("useplatformtick", "yes", null)),

        new Tweak(
            new TweakInfo(
                Id: "DisableAmdUlps",
                Title: "Disable AMD GPU Ultra-Low Power State (ULPS)",
                Category: TweakCategory.Gpu,
                Phase: 16,
                DefaultState: "1 (Enabled)",
                TargetState: "0 (Disabled)",
                PositiveImpact: "Prevents GPU deep-sleep stalls, alt-tab delays, and DisplayPort wake drops on Radeon GPUs.",
                TradeOff: "Slightly higher idle GPU power on AMD graphics cards."),
            new RegistryEachKeyOperation(
                RegistryRoot.LocalMachine,
                reg => SubKeysUnder(reg, DisplayClassKey),
                "EnableUlps",
                new RegistryValueData(0, RegistryValueKind.DWord),
                new RegistryValueData(1, RegistryValueKind.DWord),
                onlyWhereValueExists: true)),

        new Tweak(
            new TweakInfo(
                Id: "ActiveNvmeTrim",
                Title: "Enforce Active NVMe TRIM",
                Category: TweakCategory.Storage,
                Phase: 17,
                DefaultState: "disabledeletenotify = 0",
                TargetState: "disabledeletenotify = 0",
                PositiveImpact: "Ensures continuous TRIM delete notifications so NVMe SSD write speeds stay consistent.",
                TradeOff: "None."),
            new FsutilBehaviorOperation("disabledeletenotify", "0", "0")),

        new Tweak(
            new TweakInfo(
                Id: "ExpandDesktopHeap",
                Title: "Expand Win32 Desktop Heap SharedSection",
                Category: TweakCategory.Memory,
                Phase: 18,
                DefaultState: "1024,20480,768",
                TargetState: "1024,20480,1024",
                PositiveImpact: "Prevents Win32 desktop heap exhaustion during heavy multi-process builds.",
                TradeOff: "Allocates 256 KB more non-interactive desktop heap per session."),
            new DesktopHeapOperation()),

        new Tweak(
            new TweakInfo(
                Id: "EnableHAGS",
                Title: "Enable Hardware-Accelerated GPU Scheduling (HAGS)",
                Category: TweakCategory.Gpu,
                Phase: 19,
                DefaultState: "1 (Disabled)",
                TargetState: "2 (Enabled)",
                PositiveImpact: "Offloads GPU VRAM scheduling to the dedicated GPU processor, reducing render queue latency.",
                TradeOff: "Requires a reboot to take effect."),
            DWord(RegistryRoot.LocalMachine, @"SYSTEM\CurrentControlSet\Control\GraphicsDrivers", "HwSchMode", 2, 1)),

        new Tweak(
            new TweakInfo(
                Id: "TuneAmdCppcEpp",
                Title: "Tune AMD Ryzen CPPC Energy Performance Preference",
                Category: TweakCategory.Cpu,
                Phase: 20,
                DefaultState: "AC index 50 (Balanced)",
                TargetState: "AC index 0 (Max Performance)",
                PositiveImpact: "Keeps AMD Ryzen cores at maximum boost frequency readiness with zero wake latency.",
                TradeOff: "Slightly higher idle CPU package power draw (1-3 W).",
                RequiresAmdCpu: true),
            new PowerSettingOperation("SUB_PROCESSOR", "54533751-838f-4805-9259-9737f198e481", 0, 50)),

        new Tweak(
            new TweakInfo(
                Id: "DisableHypervisorLaunch",
                Title: "Disable Hypervisor Launch Type (Bare-Metal Mode)",
                Category: TweakCategory.Security,
                Phase: 20,
                DefaultState: "Auto",
                TargetState: "Off",
                PositiveImpact: "Runs Windows directly on bare metal without Hyper-V root partition overhead.",
                TradeOff: "WSL2, Hyper-V VMs, and Windows Sandbox cannot run while the hypervisor is off.",
                ReducesSecurity: true),
            new BcdOperation("hypervisorlaunchtype", "off", "auto")),

        new Tweak(
            new TweakInfo(
                Id: "DisablePageCombining",
                Title: "Disable Memory Page Combining",
                Category: TweakCategory.Memory,
                Phase: 21,
                DefaultState: "Enabled",
                TargetState: "Disabled",
                PositiveImpact: "Eliminates background CPU scanning for duplicate memory pages.",
                TradeOff: "Identical physical pages across processes are not deduplicated."),
            new MmAgentOperation(memoryCompression: false)),

        new Tweak(
            new TweakInfo(
                Id: "DisableLsaProtection",
                Title: "Disable LSA Protected Process Light (RunAsPPL)",
                Category: TweakCategory.Security,
                Phase: 22,
                DefaultState: "1 (Enabled)",
                TargetState: "0 (Disabled)",
                PositiveImpact: "Removes Protected Process Light verification overhead on Local Security Authority calls.",
                TradeOff: "Weakens LSASS credential memory protection against administrative process dumping.",
                ReducesSecurity: true),
            DWord(RegistryRoot.LocalMachine, @"SYSTEM\CurrentControlSet\Control\Lsa", "RunAsPPL", 0, 1)),

        new Tweak(
            new TweakInfo(
                Id: "DisableNetBios",
                Title: "Disable NetBIOS over TCP/IP",
                Category: TweakCategory.Network,
                Phase: 23,
                DefaultState: "0 (Default / DHCP)",
                TargetState: "2 (Disabled)",
                PositiveImpact: "Eliminates legacy NetBIOS name-resolution broadcast traffic on active network interfaces.",
                TradeOff: "Legacy NetBIOS-only LAN discovery is disabled."),
            new RegistryEachKeyOperation(
                RegistryRoot.LocalMachine,
                reg => SubKeysUnder(reg, NetBtInterfacesKey),
                "NetbiosOptions",
                new RegistryValueData(2, RegistryValueKind.DWord),
                new RegistryValueData(0, RegistryValueKind.DWord),
                onlyWhereValueExists: false)),

        new Tweak(
            new TweakInfo(
                Id: "DisablePowerThrottlingAndCoreParking",
                Title: "Disable Power Throttling, Unpark Cores & Activate Ultimate Power Plan",
                Category: TweakCategory.Power,
                Phase: 24,
                DefaultState: "Balanced plan, core parking 5%, SystemResponsiveness 20",
                TargetState: "Ultimate/High plan, 100% cores unparked, SystemResponsiveness 0",
                PositiveImpact: "Prevents CPU core parking, disables background power throttling, and removes multimedia network throttling.",
                TradeOff: "Higher idle power consumption."),
            DWord(RegistryRoot.LocalMachine, @"SYSTEM\CurrentControlSet\Control\Power", "PowerThrottlingOff", 1, null),
            new PowerPlanOperation(),
            new PowerSettingOperation("SUB_PROCESSOR", "0cc5b647-c1df-4637-891a-dec42631f105", 100, 5),
            new BcdOperation("disabledynamictick", "yes", null),
            DWord(RegistryRoot.LocalMachine, @"SOFTWARE\Microsoft\Windows NT\CurrentVersion\Multimedia\SystemProfile", "SystemResponsiveness", 0, 20),
            DWord(RegistryRoot.LocalMachine, @"SOFTWARE\Microsoft\Windows NT\CurrentVersion\Multimedia\SystemProfile", "NetworkThrottlingIndex", unchecked((int)0xFFFFFFFF), 10)),

        new Tweak(
            new TweakInfo(
                Id: "TuneMemoryStorageAndTcp",
                Title: "Pin Kernel in RAM, Disable Memory Compression, Tune NTFS & TCP Nagle",
                Category: TweakCategory.Memory,
                Phase: 25,
                DefaultState: "Compression on, dynamic pagefile, Nagle enabled, MenuShowDelay 400",
                TargetState: "Kernel pinned, compression off, 4096 MB pagefile, TCP_NODELAY, MenuShowDelay 0",
                PositiveImpact: "Keeps kernel executive in RAM, removes compression CPU tax, disables 8.3/last-access NTFS writes, and disables Nagle ACK delay.",
                TradeOff: "Uses ~500 MB more physical RAM for resident kernel pages."),
            DWord(RegistryRoot.LocalMachine, @"SYSTEM\CurrentControlSet\Control\Session Manager\Memory Management", "DisablePagingExecutive", 1, 0),
            DWord(RegistryRoot.LocalMachine, @"SYSTEM\CurrentControlSet\Control\Session Manager\Memory Management", "LargeSystemCache", 0, 0),
            new MmAgentOperation(memoryCompression: true),
            new StaticPagefileOperation(4096),
            new FsutilBehaviorOperation("disable8dot3", "1", "0"),
            new FsutilBehaviorOperation("disablelastaccess", "1", "0"),
            new FixedCommandOperation("netsh.exe", "int tcp set global autotuninglevel=normal", "int tcp set global autotuninglevel=normal"),
            new RegistryEachKeyOperation(
                RegistryRoot.LocalMachine,
                reg => SubKeysUnder(reg, TcpipInterfacesKey),
                "TcpAckFrequency",
                new RegistryValueData(1, RegistryValueKind.DWord),
                null,
                onlyWhereValueExists: false),
            new RegistryEachKeyOperation(
                RegistryRoot.LocalMachine,
                reg => SubKeysUnder(reg, TcpipInterfacesKey),
                "TCPNoDelay",
                new RegistryValueData(1, RegistryValueKind.DWord),
                null,
                onlyWhereValueExists: false),
            Str(RegistryRoot.CurrentUser, @"Control Panel\Desktop", "MenuShowDelay", "0", "400")),
    ];

    private static RegistryValueOperation DWord(RegistryRoot root, string subKey, string name, int target, int? windowsDefault) =>
        new(
            root,
            subKey,
            name,
            new RegistryValueData(target, RegistryValueKind.DWord),
            windowsDefault is { } d ? new RegistryValueData(d, RegistryValueKind.DWord) : null);

    private static RegistryValueOperation Str(RegistryRoot root, string subKey, string name, string target, string? windowsDefault) =>
        new(
            root,
            subKey,
            name,
            new RegistryValueData(target, RegistryValueKind.String),
            windowsDefault is { } d ? new RegistryValueData(d, RegistryValueKind.String) : null);

    private static List<string> SubKeysUnder(IRegistryAccessor registry, string parent) =>
        [.. registry.GetSubKeyNames(RegistryRoot.LocalMachine, parent).Select(child => $@"{parent}\{child}")];

    private static List<string> EnumerateUsbDeviceParameterKeys(IRegistryAccessor registry)
    {
        var results = new List<string>();
        foreach (var device in registry.GetSubKeyNames(RegistryRoot.LocalMachine, UsbEnumKey))
        {
            string devicePath = $@"{UsbEnumKey}\{device}";
            foreach (var instance in registry.GetSubKeyNames(RegistryRoot.LocalMachine, devicePath))
            {
                string paramKey = $@"{devicePath}\{instance}\Device Parameters";
                if (registry.KeyExists(RegistryRoot.LocalMachine, paramKey))
                {
                    results.Add(paramKey);
                }
            }
        }

        return results;
    }
}
