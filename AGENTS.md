# AGENTS.md — WinForge

WinForge is a native C# (.NET 9) + WinUI 3 (Windows App SDK 2.4.0) PowerTools suite: Winget Package Store, Feature Velocity Manager (118 flags + ViVeTool), Native System Optimizer (25-phase, reversible).

## Stack (non-negotiable)
- `net9.0-windows10.0.26100.0`, C# 13, `LangVersion preview`, `Nullable enable`, `TreatWarningsAsErrors true`, `AnalysisLevel latest-recommended`, `EnforceCodeStyleInBuild true`
- `CommunityToolkit.Mvvm 8.4.2` (`[ObservableProperty]`, `[RelayCommand]`), Mica + Fluent, `Microsoft.WindowsAppSDK 2.4.0`
- Platforms `x86;x64;ARM64`, `SelfContained true`. Root namespace `WingetStore` (`InternalsVisibleTo(WingetStore.Tests)`)
- Native interop: `[LibraryImport]` only, `Microsoft.Win32.Registry`, `System.Management` (WMI), `Windows.Management.Deployment.PackageManager`

## Multi-agent routing (use @mention or Task)
- `@winforge-orchestrator` (primary) — plan + fan-out + final gate for cross-cutting work
- `@winui-mvvm` — `Pages/`, `Controls/`, `ViewModels/`, `MainWindow`, `NavigationHelper`, `IconService`, `GridCalculator`
- `@winget-core` — `Services/Winget*.cs`, `CliProcessRunner`, `CachingWingetService`, `Package*Helper`, `VersionComparer`, `Models/WingetPackage*`, winget ViewModels
- `@vive-features` — `Models/ViVeTool/*`, `Services/ViVeTool/*`, `Tools/ViVeTool.exe` interop (never modify binary), `FeaturesViewModel`
- `@optimizer-safety` — `Services/Optimizer/*`, `OptimizerViewModel` (safety owner: snapshots + undo required, security tweaks opt-in, reparse-point-safe, non-destructive audit)
- `@dotnet-quality` — `Directory.Build.*`, `.sln/.csproj`, `WingetStore.Tests/*` (xUnit v3), final build/test gate

## Safety rules
- No live `winget install/upgrade/uninstall`, no live ViVeTool enable/disable, no optimizer `Apply/Undo`, disk cleanup, or memory flush without explicit user request. Default to code + mocked tests.
- Never modify `Tools/ViVeTool.exe` / `Albacore.ViVe.dll`. Never raw-delete WinSxS. Check `IElevationService.IsElevated` before privileged ops.
- All external processes via `IProcessRunner.RunStreamAsync` + `CancellationToken`; sanitize CLI args; keep parsing in `WingetParser` / `FeatureFilterService` (pure + tested).
- MVVM strict: no business logic in code-behind. Public APIs get XML docs.

## Build / test (PowerShell 5.1)
```powershell
dotnet restore WinForge.sln
dotnet build WinForge.sln -c Release
dotnet test WinForge.sln -c Release --logger "console;verbosity=normal"
dotnet run --project src/WinForge/WinForge.csproj
```
Do not commit `bin/` / `obj/`. VS 2022 17.12+ + Developer Mode required for F5. See `src/WinForge/SKILL.md` conventions via skill `winforge-conventions`.
