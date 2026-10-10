# ViVe feature filtering + batch flow dry-run

Assignee: vive-features | Priority: high | Labels: area:vive

## Goal
Verify and harden the FeaturesPage filter pipeline and the batch
enable/disable flow (dry-run only) with progress + logging + undo path.

## Context
- Models: `src/WinForge/Models/ViVeTool/*` (FeatureItem, ViVeToolResult, ViVeBatchResult, ViVeProgressReport, SelectionSummary).
- Services: `src/WinForge/Services/ViVeTool/*` (FeatureFilterService, IFeatureScraper, IProcessLauncher).
- UI logic: `ViewModels/FeaturesViewModel.cs` (coordinate XAML via winui-mvvm if needed).
- Tests: `WingetStore.Tests/ViVeToolServicesTests.cs`.
- Binaries `Tools/ViVeTool.exe` / `Albacore.ViVe.dll` are NEVER modified.

## Acceptance criteria
- [ ] Release-track filter + search + SelectionSummary covered by pure unit tests.
- [ ] Batch flow streams progress, logs results, exposes undo; all launches go through `IProcessLauncher` mocks in tests.
- [ ] Catalog sync failure degrades to offline cache, no UI block, honors CancellationToken.
- [ ] Build clean; ViVe test suites green; dry-run output pasted in a comment.

## Safety
Dry-run / query / parse-only. No live enable/disable without explicit
approval + elevation check via `IElevationService`.
