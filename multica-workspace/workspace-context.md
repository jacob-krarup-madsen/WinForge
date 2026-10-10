# WinForge — Multica workspace context

Paste this file into the workspace **Settings → Context** in the Multica UI
(or keep it as the source of truth in git). It is provided to every agent
in the workspace on every run.

---

WinForge is a 100% native C# (.NET 9) + WinUI 3 (Windows App SDK) PowerTools
suite in a single desktop app. GitHub: https://github.com/jacob-krarup-madsen/WinForge

Three product areas:
1. **Winget Package Store** — browse/search curated + global winget repo,
   one-click install, batch upgrades/uninstalls.
2. **Feature Velocity Manager** — discover/filter/batch enable-disable all 118
   Windows 11 velocity feature flags, bundled ViVeTool CLI, Pureinfotech
   catalog sync.
3. **Native System Optimizer** — 25-phase Windows 11 performance & privacy
   engine: baseline snapshots, reversible restore, EmptyWorkingSet memory
   flush, reparse-point-safe disk & WinSxS cleanup, non-destructive 24-tweak
   audit, CPU/RAM micro-benchmarks, JSON profile import/export.

Non-negotiable stack:
- C# 13, `net9.0-windows10.0.26100.0`, `LangVersion preview`,
  `Nullable enable`, `TreatWarningsAsErrors true`,
  `AnalysisLevel latest-recommended`, `EnforceCodeStyleInBuild true`.
- WinUI 3, Windows App SDK 2.4.0, CommunityToolkit.Mvvm 8.4.2
  (`[ObservableProperty]`, `[RelayCommand]`), Mica + Fluent.
- Platforms x86/x64/ARM64, SelfContained. Root namespace `WingetStore`
  with `InternalsVisibleTo(WingetStore.Tests)`.
- Native interop `[LibraryImport]` only; `Microsoft.Win32.Registry`,
  `System.Management` (WMI), `Windows.Management.Deployment.PackageManager`.
- xUnit v3 suite in `src/WinForge/WingetStore.Tests/`.

Safety rules (apply to every run):
- No live `winget install/upgrade/uninstall`, no live ViVeTool
  enable/disable, no optimizer Apply/Undo, disk cleanup, or memory flush
  without the issue explicitly requesting a live run. Default to code +
  mocked tests (`StubWingetService`, `MockProcessRunner`, mock launchers).
- Never modify `src/WinForge/Tools/ViVeTool.exe` / `Albacore.ViVe.dll`.
  Never raw-delete WinSxS. Security-reducing tweaks are opt-in.
  Check `IElevationService.IsElevated` before privileged ops.
- All external processes via `IProcessRunner.RunStreamAsync` +
  `CancellationToken`; sanitize CLI args; parsing stays in `WingetParser` /
  `FeatureFilterService` (pure + tested).
- MVVM strict: no business logic in code-behind. Public APIs get XML docs.
- Never commit `bin/` / `obj/`.

Build / test (Windows PowerShell 5.1):
`dotnet restore WinForge.sln` →
`dotnet build WinForge.sln -c Release` →
`dotnet test WinForge.sln -c Release --logger "console;verbosity=normal"`.
Launch only on request: `dotnet run --project src/WinForge/WinForge.csproj`
(VS 2022 17.12+ + Developer Mode for F5).

Delivery contract for every agent run:
- Read the issue + linked files first; post progress as issue comments.
- Change the smallest set of files that satisfies the issue.
- Add/extend xUnit v3 tests for service/parser/filter/tweak logic.
- End with: files changed, test report, manual verification steps,
  and anything left for a human reviewer. Move the issue to `in_review`
  when done; a human moves it to `done`.
