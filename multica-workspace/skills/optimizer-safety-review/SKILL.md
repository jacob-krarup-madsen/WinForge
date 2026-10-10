---
name: optimizer-safety-review
description: Use when reviewing or changing WinForge optimizer tweaks — reversibility, snapshot/undo, elevation, reparse-point-safe cleanup and non-destructive audit checklist.
---

# Optimizer safety review

Use this skill for ANY change under `src/WinForge/Services/Optimizer/`,
`ViewModels/OptimizerViewModel.cs`, or `Pages/OptimizerPage.xaml*`.
Attach to `@optimizer-safety` and `@winforge-lead`.

## Review checklist — all must hold

1. **Snapshot before apply.** `ApplyOptimizationsAsync` captures the
   pre-optimization baseline for every touched setting before changing it.
2. **Undo path exists.** `UndoOptimizationsAsync` reverts from the snapshot;
   falls back to Windows defaults ONLY when no snapshot entry exists.
   Both runs return `OptimizationResult` / `UndoResult` with log lines.
3. **Security opt-in.** Security-reducing tweaks are gated behind
   `IncludeSecurityReducingTweaks` / config flag. Defaults stay safe.
4. **Elevation.** Privileged ops check `IElevationService.IsElevated` and
   fail with a clear message when not elevated.
5. **Cleanup safety.** Disk cleanup is reparse-point-safe; WinSxS is handled
   via the supported servicing stack only — never raw deletes.
6. **Audit is read-only.** `SystemAuditorService` mutates nothing.
7. **Interop hygiene.** `[LibraryImport]` only; Registry/WMI/commands behind
   `IRegistryAccessor` / `IWmiAccessor` / `ICommandExecutor` so tests run
   without admin (`OptimizerServicesTests`, `DiskCleanerServiceTests`,
   `MemoryOptimizerServiceTests`).

## Verdict format

Post as an issue comment:

- Verdict: SAFE or NEEDS-WORK
- For each finding: `file:line` + what breaks + required fix + required test
- Confirm: snapshot behavior, elevation need, undo verification,
  and that no live Apply/Undo/cleanup/flush was executed
  (live runs need explicit issue approval).
