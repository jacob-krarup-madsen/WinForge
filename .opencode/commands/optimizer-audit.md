---
description: Review optimizer change for reversibility and safety (no live Apply)
agent: optimizer-safety
---

Safety review for optimizer work — dry-run only, no live Apply/Undo/cleanup/flush:

1. Identify tweaks/phases in $ARGUMENTS (files or feature description).
2. Check: snapshot capture before apply, Undo path with fallback to defaults, security opt-in flag, elevation check, reparse-point-safe cleanup, non-destructive audit, [LibraryImport] usage.
3. Report: SAFE / NEEDS-WORK with file:line findings and required tests (`OptimizerServicesTests`, `DiskCleanerServiceTests`, `MemoryOptimizerServiceTests`).
