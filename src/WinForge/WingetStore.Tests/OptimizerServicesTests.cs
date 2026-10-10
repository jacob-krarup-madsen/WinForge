using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.UI.Xaml.Controls;
using Microsoft.Win32;
using WingetStore.Services;
using WingetStore.Services.Optimizer;
using WingetStore.ViewModels;
using Xunit;

namespace WingetStore.Tests;

public sealed class OptimizerServicesTests : IDisposable
{
    private readonly string _tempDir;

    public OptimizerServicesTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "WinForgeOptimizerTests_" + Guid.NewGuid().ToString("N"));
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
            // Ignore cleanup errors in temp directory
        }
    }

    [Fact]
    public void TweakCatalog_ContainsAll25TweaksWithValidMetadata()
    {
        var all = TweakCatalog.All;
        Assert.Equal(25, all.Count);

        var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var tweak in all)
        {
            Assert.False(string.IsNullOrWhiteSpace(tweak.Info.Id));
            Assert.False(string.IsNullOrWhiteSpace(tweak.Info.Title));
            Assert.False(string.IsNullOrWhiteSpace(tweak.Info.DefaultState));
            Assert.False(string.IsNullOrWhiteSpace(tweak.Info.TargetState));
            Assert.False(string.IsNullOrWhiteSpace(tweak.Info.PositiveImpact));
            Assert.False(string.IsNullOrWhiteSpace(tweak.Info.TradeOff));
            Assert.InRange(tweak.Info.Phase, 2, 25);
            Assert.NotEmpty(tweak.Operations);
            Assert.True(ids.Add(tweak.Info.Id), $"Duplicate tweak ID: {tweak.Info.Id}");
        }

        // Security-reducing tweaks must be explicitly marked
        var securityIds = all.Where(t => t.Info.ReducesSecurity).Select(t => t.Info.Id).OrderBy(x => x).ToList();
        Assert.Equal(["DisableHVCI", "DisableHypervisorLaunch", "DisableLsaProtection", "DisableVBS"], securityIds);

        // AMD-specific tweak must be explicitly marked
        var amdIds = all.Where(t => t.Info.RequiresAmdCpu).Select(t => t.Info.Id).ToList();
        Assert.Equal(["TuneAmdCppcEpp"], amdIds);
    }

    [Fact]
    public async Task OptimizerConfigService_CreateDefaultAndRoundTripJson()
    {
        var service = new OptimizerConfigService();

        var defaultSafe = service.CreateDefault(includeSecurityReducing: false);
        Assert.False(defaultSafe.IncludeSecurityReducingTweaks);
        Assert.True(defaultSafe.CreateRestorePoint);
        Assert.DoesNotContain("DisableHVCI", defaultSafe.EnabledTweakIds);
        Assert.DoesNotContain("DisableVBS", defaultSafe.EnabledTweakIds);
        Assert.Contains("DisableTelemetry", defaultSafe.EnabledTweakIds);

        var defaultWithSec = service.CreateDefault(includeSecurityReducing: true);
        Assert.True(defaultWithSec.IncludeSecurityReducingTweaks);
        Assert.Contains("DisableHVCI", defaultWithSec.EnabledTweakIds);
        Assert.Contains("DisableLsaProtection", defaultWithSec.EnabledTweakIds);

        string filePath = Path.Combine(_tempDir, "profile.json");
        await service.ExportAsync(defaultWithSec, filePath);
        Assert.True(File.Exists(filePath));

        var imported = await service.ImportAsync(filePath);
        Assert.True(imported.IncludeSecurityReducingTweaks);
        Assert.True(imported.CreateRestorePoint);
        Assert.Equal(defaultWithSec.EnabledTweakIds.Count, imported.EnabledTweakIds.Count);
    }

    [Fact]
    public async Task SystemOptimizerService_ThrowsWhenNotElevated()
    {
        var elevation = new FakeElevationService { IsElevated = false };
        var registry = new FakeRegistryAccessor();
        var commands = new FakeCommandExecutor();
        var wmi = new FakeWmiAccessor();
        var memory = new FakeMemoryOptimizerService();
        var configService = new OptimizerConfigService();
        string snapshotPath = Path.Combine(_tempDir, "snapshot.json");

        var optimizer = new SystemOptimizerService(elevation, registry, commands, wmi, memory, snapshotPath);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            optimizer.ApplyOptimizationsAsync(configService.CreateDefault()));

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            optimizer.UndoOptimizationsAsync());
    }

    [Fact]
    public async Task SystemOptimizerService_AppliesAndRevertsFromSnapshot_RespectingSecurityAndCpuGates()
    {
        var elevation = new FakeElevationService { IsElevated = true };
        var registry = new FakeRegistryAccessor();
        var commands = new FakeCommandExecutor();
        var wmi = new FakeWmiAccessor
        {
            Summary = new SystemSummary("Intel Core i9", "GenuineIntel", "Windows 11 Pro", "10.0.26100", 120, 80),
        };
        var memory = new FakeMemoryOptimizerService();
        string snapshotPath = Path.Combine(_tempDir, "snapshot.json");

        // Seed an original non-default registry value for AllowTelemetry = 3 so we can verify exact snapshot restoration
        registry.SetValue(
            RegistryRoot.LocalMachine,
            @"SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\DataCollection",
            "AllowTelemetry",
            new RegistryValueData(3, RegistryValueKind.DWord));

        // Seed DesktopHeap Windows SubSystem string
        registry.SetValue(
            RegistryRoot.LocalMachine,
            @"SYSTEM\CurrentControlSet\Control\Session Manager\SubSystems",
            "Windows",
            new RegistryValueData(
                @"%SystemRoot%\system32\csrss.exe ObjectDirectory=\Windows SharedSection=1024,20480,768 Windows=On",
                RegistryValueKind.ExpandString));

        var configService = new OptimizerConfigService();
        var safeConfig = configService.CreateDefault(includeSecurityReducing: false);

        var optimizer = new SystemOptimizerService(elevation, registry, commands, wmi, memory, snapshotPath);
        var applyResult = await optimizer.ApplyOptimizationsAsync(safeConfig);

        Assert.True(applyResult.RestorePointCreated);
        Assert.Equal("WinForge Pre-Optimization", wmi.LastRestorePointDescription);
        Assert.True(applyResult.FailedCount == 0, string.Join(Environment.NewLine, applyResult.LogLines));
        Assert.True(applyResult.AppliedCount > 0);
        Assert.True(File.Exists(snapshotPath));

        // Telemetry was changed to 0
        var telemetryVal = registry.GetValue(
            RegistryRoot.LocalMachine,
            @"SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\DataCollection",
            "AllowTelemetry");
        Assert.Equal(0, telemetryVal?.Data);

        // DesktopHeap was updated to 1024
        var subsystemsVal = registry.GetValue(
            RegistryRoot.LocalMachine,
            @"SYSTEM\CurrentControlSet\Control\Session Manager\SubSystems",
            "Windows");
        Assert.Contains("SharedSection=1024,20480,1024", subsystemsVal?.Data?.ToString() ?? string.Empty);

        // HVCI (security-reducing) was NOT touched because IncludeSecurityReducingTweaks = false
        var hvciVal = registry.GetValue(
            RegistryRoot.LocalMachine,
            @"SYSTEM\CurrentControlSet\Control\DeviceGuard\Scenarios\HypervisorEnforcedCodeIntegrity",
            "Enabled");
        Assert.Null(hvciVal);

        // Run a second time to verify pre-optimization snapshot is preserved and not overwritten by optimized values
        await optimizer.ApplyOptimizationsAsync(safeConfig);

        // Now undo optimizations and verify original value (3, not default 1) is restored from snapshot!
        var undoResult = await optimizer.UndoOptimizationsAsync();
        Assert.True(undoResult.FailedCount == 0, string.Join(Environment.NewLine, undoResult.LogLines));
        Assert.True(undoResult.RevertedFromSnapshotCount > 0);
        Assert.False(File.Exists(snapshotPath));

        var restoredTelemetry = registry.GetValue(
            RegistryRoot.LocalMachine,
            @"SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\DataCollection",
            "AllowTelemetry");
        Assert.Equal(3, restoredTelemetry?.Data);

        var restoredSubsystems = registry.GetValue(
            RegistryRoot.LocalMachine,
            @"SYSTEM\CurrentControlSet\Control\Session Manager\SubSystems",
            "Windows");
        Assert.Contains("SharedSection=1024,20480,768", restoredSubsystems?.Data?.ToString() ?? string.Empty);
    }

    [Fact]
    public async Task SystemAuditorAndBenchmarkServices_ProduceCompleteReports()
    {
        var registry = new FakeRegistryAccessor();
        var commands = new FakeCommandExecutor();
        var wmi = new FakeWmiAccessor();
        var memory = new FakeMemoryOptimizerService();

        var auditor = new SystemAuditorService(registry, commands, wmi, memory);
        var report = await auditor.AuditAsync();

        Assert.Equal(25, report.TotalCount);
        string formattedAudit = auditor.FormatReport(report);
        Assert.Contains("WINFORGE DEEP SYSTEM STATE AUDIT REPORT", formattedAudit);
        Assert.Contains("Disable Telemetry", formattedAudit);

        var benchmark = new SystemBenchmarkService(wmi, memory, cpuIterations: 2_000, memoryBufferBytes: 64 * 1024);
        var benchResult = await benchmark.RunBenchmarkAsync();
        Assert.True(benchResult.CpuDurationMs > 0);
        Assert.True(benchResult.CpuOpsPerSecond > 0);
        Assert.True(benchResult.MemoryDurationMs > 0);
        Assert.Contains("WINFORGE SYSTEM PERFORMANCE BENCHMARK", benchResult.FormattedReport);
    }

    [Fact]
    public async Task OptimizerViewModel_ExecutesCommandsAndHandlesElevation()
    {
        var origOverride = App.DispatcherOverride;
        App.DispatcherOverride = action => action();
        try
        {
            var elevation = new FakeElevationService { IsElevated = true };
            var registry = new FakeRegistryAccessor();
            var commands = new FakeCommandExecutor();
            var wmi = new FakeWmiAccessor();
            var memory = new FakeMemoryOptimizerService();
            var configService = new OptimizerConfigService();
            string snapshotPath = Path.Combine(_tempDir, "vm-snapshot.json");
            var optimizer = new SystemOptimizerService(elevation, registry, commands, wmi, memory, snapshotPath);
            var auditor = new SystemAuditorService(registry, commands, wmi, memory);
            var benchmark = new SystemBenchmarkService(wmi, memory, cpuIterations: 1_000, memoryBufferBytes: 64 * 1024);
            var diskTargets = new DiskCleanTargets([Path.Combine(_tempDir, "temp")], [], [], []);
            var diskCleaner = new DiskCleanerService(new FakeProcessRunner(), elevation, diskTargets);

            var vm = new OptimizerViewModel(elevation, optimizer, diskCleaner, memory, auditor, benchmark, configService);
            vm.CheckElevation();
            Assert.True(vm.IsElevated);

            await vm.FlushMemoryAsync();
            Assert.Equal(InfoBarSeverity.Success, vm.InfoBarSeverity);

            await vm.CleanDiskAsync();
            Assert.Equal(InfoBarSeverity.Success, vm.InfoBarSeverity);

            await vm.RunAuditAsync();
            Assert.Equal(25, vm.AuditItems.Count);

            await vm.RunBenchmarkAsync();
            Assert.Equal(InfoBarSeverity.Success, vm.InfoBarSeverity);

            await vm.RunOptimizeAsync();
            Assert.Equal(InfoBarSeverity.Success, vm.InfoBarSeverity);

            await vm.UndoOptimizationsAsync();
            Assert.Equal(InfoBarSeverity.Success, vm.InfoBarSeverity);

            string configPath = Path.Combine(_tempDir, "vm-config.json");
            vm.IncludeSecurityReducingTweaks = true;
            await vm.ExportConfigAsync(configPath);
            Assert.True(File.Exists(configPath));

            vm.IncludeSecurityReducingTweaks = false;
            await vm.ImportConfigAsync(configPath);
            Assert.True(vm.IncludeSecurityReducingTweaks);
        }
        finally
        {
            App.DispatcherOverride = origOverride;
        }
    }

    private sealed class FakeElevationService : IElevationService
    {
        public bool IsElevated { get; set; } = true;
    }

    private sealed class FakeMemoryOptimizerService : IMemoryOptimizerService
    {
        public MemoryInfo GetCurrentMemoryStatus() => new(32UL * 1024 * 1024 * 1024, 20UL * 1024 * 1024 * 1024);

        public Task<MemoryOptimizationResult> FlushMemoryAsync(IProgress<string>? progress = null, CancellationToken cancellationToken = default)
        {
            var before = new MemoryInfo(32UL * 1024 * 1024 * 1024, 18UL * 1024 * 1024 * 1024);
            var after = new MemoryInfo(32UL * 1024 * 1024 * 1024, 20UL * 1024 * 1024 * 1024);
            return Task.FromResult(new MemoryOptimizationResult(before, after, 42, 3));
        }
    }

    private sealed class FakeProcessRunner : IProcessRunner
    {
        public Task<int> RunStreamAsync(string fileName, string arguments, Action<string> onLineReceived, CancellationToken cancellationToken = default)
        {
            onLineReceived("OK");
            return Task.FromResult(0);
        }
    }

    private sealed class FakeRegistryAccessor : IRegistryAccessor
    {
        private readonly Dictionary<string, Dictionary<string, RegistryValueData>> _keys = new(StringComparer.OrdinalIgnoreCase);

        public bool KeyExists(RegistryRoot root, string subKey) => _keys.ContainsKey(Compose(root, subKey));

        public RegistryValueData? GetValue(RegistryRoot root, string subKey, string name)
        {
            return _keys.TryGetValue(Compose(root, subKey), out var values) && values.TryGetValue(name, out var data)
                ? data
                : null;
        }

        public void SetValue(RegistryRoot root, string subKey, string name, RegistryValueData data)
        {
            string key = Compose(root, subKey);
            if (!_keys.TryGetValue(key, out var values))
            {
                values = new Dictionary<string, RegistryValueData>(StringComparer.OrdinalIgnoreCase);
                _keys[key] = values;
            }

            values[name] = data;
        }

        public bool DeleteValue(RegistryRoot root, string subKey, string name)
        {
            return _keys.TryGetValue(Compose(root, subKey), out var values) && values.Remove(name);
        }

        public bool DeleteKeyIfEmpty(RegistryRoot root, string subKey)
        {
            string key = Compose(root, subKey);
            if (_keys.TryGetValue(key, out var values) && values.Count == 0 && GetSubKeyNames(root, subKey).Count == 0)
            {
                return _keys.Remove(key);
            }

            return false;
        }

        public IReadOnlyList<string> GetSubKeyNames(RegistryRoot root, string subKey)
        {
            string prefix = Compose(root, subKey) + @"\";
            var children = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var key in _keys.Keys)
            {
                if (key.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                {
                    string remainder = key[prefix.Length..];
                    int slash = remainder.IndexOf('\\');
                    children.Add(slash >= 0 ? remainder[..slash] : remainder);
                }
            }

            return [.. children];
        }

        private static string Compose(RegistryRoot root, string subKey) => $"{root}:{subKey.Trim('\\')}";
    }

    private sealed class FakeCommandExecutor : ICommandExecutor
    {
        public Task<CommandResult> RunAsync(string fileName, string arguments, CancellationToken cancellationToken = default)
        {
            if (fileName.Equals("fsutil.exe", StringComparison.OrdinalIgnoreCase) && arguments.Contains("query", StringComparison.OrdinalIgnoreCase))
            {
                return Task.FromResult(new CommandResult(0, "DisableLastAccess = 0"));
            }

            if (fileName.Equals("powercfg.exe", StringComparison.OrdinalIgnoreCase) && arguments.Contains("/list", StringComparison.OrdinalIgnoreCase))
            {
                return Task.FromResult(new CommandResult(0, "Power Scheme GUID: 381b4222-f694-41f0-9685-ff5bb260df2e  (Balanced) *"));
            }

            if (fileName.Equals("powercfg.exe", StringComparison.OrdinalIgnoreCase) && arguments.Contains("/duplicatescheme", StringComparison.OrdinalIgnoreCase))
            {
                return Task.FromResult(new CommandResult(0, "Power Scheme GUID: e9a42b02-d5df-448d-aa00-03f14749eb61  (Ultimate Performance)"));
            }

            if (fileName.Equals("schtasks.exe", StringComparison.OrdinalIgnoreCase) && arguments.Contains("/Query", StringComparison.OrdinalIgnoreCase))
            {
                return Task.FromResult(new CommandResult(0, "Ready"));
            }

            return Task.FromResult(new CommandResult(0, string.Empty));
        }
    }

    private sealed class FakeWmiAccessor : IWmiAccessor
    {
        public MmAgentState MmState { get; set; } = new(true, true);

        public PagefileConfig Pagefile { get; set; } = new(true, []);

        public List<NicPowerState> Nics { get; set; } = [new("PCI\\VEN_8086", true)];

        public SystemSummary Summary { get; set; } = new("AMD Ryzen 9 7950X", "AuthenticAMD", "Microsoft Windows 11 Pro", "10.0.26100", 110, 75);

        public string? LastRestorePointDescription { get; private set; }

        public MmAgentState? GetMmAgent() => MmState;

        public void SetMmAgent(bool? memoryCompression, bool? pageCombining)
        {
            MmState = new MmAgentState(
                memoryCompression ?? MmState.MemoryCompression,
                pageCombining ?? MmState.PageCombining);
        }

        public PagefileConfig GetPagefileConfig() => Pagefile;

        public void ApplyPagefileConfig(PagefileConfig config) => Pagefile = config;

        public IReadOnlyList<NicPowerState> GetNicPowerStates() => [.. Nics];

        public void SetNicPowerEnabled(string instanceName, bool enabled)
        {
            for (int i = 0; i < Nics.Count; i++)
            {
                if (Nics[i].InstanceName == instanceName)
                {
                    Nics[i] = new NicPowerState(instanceName, enabled);
                }
            }
        }

        public SystemSummary GetSystemSummary() => Summary;

        public void CreateRestorePoint(string description) => LastRestorePointDescription = description;
    }
}
