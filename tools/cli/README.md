# ViVeTool Feature Enabler Suite

Automatically enables all documented hidden/experimental Windows 11 feature IDs
from the Pureinfotech ViVeTool codes list across GA 2025/2026, 26H2, 25H2, and
Canary/Feature Platforms builds.

## Files

| File | Purpose |
|------|---------|
| `FeatureCatalog.ps1` | Master list of all feature IDs (dot-source to get `$AllFeatureIDs`) |
| `Get-ViveTool.ps1` | Downloads/extracts ViVeTool from GitHub if not present |
| `Enable-Features.ps1` | Main script — batch-enables all IDs with logging |
| `Disable-Features.ps1` | Rollback script — batch-disables all IDs |
| `Test-Suite.ps1` | Non-destructive self-test runner |

## Quick Start

Open **PowerShell as Administrator** and run:

```powershell
Set-ExecutionPolicy Bypass -Scope Process -Force
cd tools\cli

# 1. Dry run first (no changes made)
.\Enable-Features.ps1 -WhatIf

# 2. Run for real (auto-elevates if needed)
.\Enable-Features.ps1

# 3. Optionally restart Explorer automatically
.\Enable-Features.ps1 -RestartExplorer
```

## Rollback

```powershell
.\Disable-Features.ps1
```

## Self-Test

```powershell
.\Test-Suite.ps1 -Verbose
```

## Logs

Every run creates timestamped `.log` and `.csv` files in the `Logs\` subdirectory:
- `enable_YYYYMMDD_HHmmss.log` / `.csv` — enable run results
- `disable_YYYYMMDD_HHmmss.log` / `.csv` — rollback run results
- `test_YYYYMMDD_HHmmss.csv` — self-test report

## Notes

- Feature IDs that are unsupported on your current build are gracefully skipped
  and recorded as `Unsupported` in the log — no crashes, no aborted runs.
- ViVeTool is downloaded from the official GitHub release (`thebookisclosed/ViVe`)
  if not already present.
- Always reboot or restart Explorer after enabling features for shell changes to
  take effect.
- Use `-WhatIf` to preview all operations without making any changes.

## Source

Feature IDs sourced from: https://pureinfotech.com/vivetool-codes-enable-features-windows-11/
