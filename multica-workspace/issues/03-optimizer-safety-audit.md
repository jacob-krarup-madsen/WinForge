# Optimizer safety audit: snapshot/undo + cleanup guarantees

Assignee: optimizer-safety | Priority: highest | Labels: area:optimizer

## Goal
Audit the 25-phase optimizer for reversibility and cleanup safety using
the optimizer-safety-review skill. Fix gaps with mocked tests.

## Context
- `Services/Optimizer/SystemOptimizerService.cs` (Apply/Undo), `TweakCatalog.cs`, `Tweaks.cs`, `OptimizerConfigService.cs` (JSON profiles).
- `DiskCleanerService.cs`, `MemoryOptimizerService.cs` + `WindowsMemoryNative.cs` + `WindowsWmiAccessor.cs`, `SystemAuditorService.cs`, `SystemBenchmarkService.cs`.
- Tests: `OptimizerServicesTests`, `DiskCleanerServiceTests`, `MemoryOptimizerServiceTests`.

## Acceptance criteria
- [ ] Every enabled-by-default tweak has snapshot capture + Undo path (fallback to Windows defaults only when no snapshot entry).
- [ ] Security-reducing tweaks confirmed opt-in; audit confirmed read-only; cleanup confirmed reparse-point-safe with servicing-stack-only WinSxS.
- [ ] New/updated mocked tests (no admin required) for any gap found.
- [ ] Verdict comment posted: SAFE or NEEDS-WORK with `file:line` findings.

## Safety
No live Apply/Undo, disk cleanup, or memory flush on the host. Code +
tests + dry-run reasoning only. Concurrency limit for this agent is 1.
