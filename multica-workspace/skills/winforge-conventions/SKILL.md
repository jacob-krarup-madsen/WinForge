---
name: winforge-conventions
description: Use when writing C#, XAML, or tests for WinForge — covers MVVM, nullable, analyzers, PInvoke, winget/ViVe/optimizer patterns and build commands.
---

# WinForge conventions

Load this skill for any WinForge code change. Attach to every agent in the
WinForge Multica workspace.

## Stack
- C# 13, `net9.0-windows10.0.26100.0`, `LangVersion preview`, `Nullable enable`, `TreatWarningsAsErrors true`, `AnalysisLevel latest-recommended`, `EnforceCodeStyleInBuild true`.
- WinUI 3 + Windows App SDK 2.4.0 + CommunityToolkit.Mvvm 8.4.2. Platforms x86;x64;ARM64, SelfContained.

## MVVM (UI)
- ViewModels extend `ObservableObject` with `[ObservableProperty]` / `[RelayCommand]` source-gen. No hand INPC.
- Code-behind = navigation/lifecycle only. Bind via x:Bind. Layout via `ResponsivePageContainer` + `GridCalculator`. Preserve Mica/Fluent.
- Errors via `INotificationService`, logs via `LogService`, async with `CancellationToken` + `IProgress<string>`.

## Services
- Winget: all CLI via `IProcessRunner.RunStreamAsync`. Parse only in `WingetParser`. Cache via `CachingWingetService`. Sanitize args. Test with `MockProcessRunner`, `StubWingetService`, `NullLineRunner`, `StatusOnlyLinesRunner`.
- ViVeTool: launch via `IProcessLauncher` abstraction. Filter in `FeatureFilterService`. Never commit `Tools/ViVeTool.exe` changes. No live enable/disable without explicit approval + elevation check.
- Optimizer: every tweak needs snapshot capture + `UndoOptimizationsAsync` path. Security-reducing tweaks opt-in. Audit is read-only. Disk-clean reparse-point-safe, WinSxS via servicing stack only. P/Invoke `[LibraryImport]` only. Registry/WMI behind `IRegistryAccessor` / `IWmiAccessor` / `ICommandExecutor`. Check `IElevationService.IsElevated`.

## Quality
- Public APIs need XML docs. No `#pragma warning disable`, no `!` without test justification.
- Tests: xUnit v3 in `WingetStore.Tests/`, `InternalsVisibleTo` available. Mirror `*StaticTests` + mock patterns.
- Commands (PowerShell 5.1): `dotnet restore WinForge.sln`, `dotnet build WinForge.sln -c Release`, `dotnet test WinForge.sln -c Release --logger "console;verbosity=normal"`. Never commit `bin/` `obj/`.

## Multica routing
- UI → `@winui-mvvm`, winget → `@winget-core`, flags → `@vive-features`, tweaks/safety → `@optimizer-safety`, build/tests → `@dotnet-quality`, triage/coordination → squad leader `@winforge-lead` (WinForge Delivery squad).
