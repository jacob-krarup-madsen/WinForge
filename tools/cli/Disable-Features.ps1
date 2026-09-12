#Requires -Version 5.1
# Disable-Features.ps1
# Rollback companion -- disables every feature ID from FeatureCatalog.ps1.
# Usage:
#   .\Disable-Features.ps1
#   .\Disable-Features.ps1 -RestartExplorer
#   .\Disable-Features.ps1 -WhatIf

[CmdletBinding(SupportsShouldProcess)]
param(
    [switch]$RestartExplorer,
    [switch]$NoAutoElevate
)
Set-StrictMode -Version Latest
$ErrorActionPreference = "Continue"

# 0. Self-elevate if not admin
if (-not $NoAutoElevate) {
    $isAdmin = ([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
    if (-not $isAdmin) {
        Write-Warning "Not running as Administrator -- relaunching elevated..."
        $args2 = "-NoProfile -ExecutionPolicy Bypass -File `"$PSCommandPath`""
        if ($RestartExplorer) { $args2 += " -RestartExplorer" }
        Start-Process powershell.exe -ArgumentList $args2 -Verb RunAs
        exit
    }
}

# 1. Resolve paths
$ScriptDir = if ($PSScriptRoot) { $PSScriptRoot } else { Split-Path -Parent $MyInvocation.MyCommand.Definition }
$LogDir    = Join-Path $ScriptDir "Logs"
New-Item -ItemType Directory -Force -Path $LogDir | Out-Null
$Stamp   = Get-Date -Format "yyyyMMdd_HHmmss"
$LogFile = Join-Path $LogDir "disable_$Stamp.log"

function Write-Log {
    param([string]$Message, [string]$Level = "INFO")
    $entry = "[$(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')] [$Level] $Message"
    Add-Content -Path $LogFile -Value $entry
    switch ($Level) {
        "SUCCESS" { Write-Host $entry -ForegroundColor Green  }
        "SKIP"    { Write-Host $entry -ForegroundColor Cyan   }
        "WARN"    { Write-Host $entry -ForegroundColor Yellow }
        "ERROR"   { Write-Host $entry -ForegroundColor Red    }
        default   { Write-Host $entry }
    }
}

# 2. Provision ViVeTool
Write-Log "=== ViVeTool Feature Disabler (Rollback) ==="
Write-Log "Log: $LogFile"
. (Join-Path $ScriptDir "Get-ViveTool.ps1") -InstallDir $ScriptDir
if (-not $ViveToolPath -or -not (Test-Path $ViveToolPath)) {
    Write-Log "vivetool.exe not found. Aborting." "ERROR"
    exit 1
}
Write-Log "Using ViVeTool: $ViveToolPath"

# 3. Load catalog
. (Join-Path $ScriptDir "FeatureCatalog.ps1")
Write-Log "Catalog: $($AllFeatureIDs.Count) unique IDs to disable."

# 4. Disable features
$Results = [System.Collections.Generic.List[PSCustomObject]]::new()
$i = 0
foreach ($id in $AllFeatureIDs) {
    $i++
    $pct = [math]::Round(($i / $AllFeatureIDs.Count) * 100)
    Write-Progress -Activity "Disabling features (rollback)" -Status "$i / $($AllFeatureIDs.Count) -- ID: $id" -PercentComplete $pct
    if ($WhatIfPreference) {
        Write-Log "[WHATIF] Would run: $ViveToolPath /disable /id:$id" "WARN"
        $Results.Add([PSCustomObject]@{ID=$id;Status="WhatIf";Output="";Timestamp=(Get-Date)})
        continue
    }
    try {
        $output   = & $ViveToolPath /disable /id:$id 2>&1
        $exitCode = $LASTEXITCODE
        $outStr   = ($output -join " ").Trim()
        if ($exitCode -eq 0) {
            Write-Log "DISABLED ID:$id  $outStr" "SUCCESS"
            $Results.Add([PSCustomObject]@{ID=$id;Status="Disabled";Output=$outStr;Timestamp=(Get-Date)})
        } elseif ($outStr -match "not found|unknown|unsupported|no feature") {
            Write-Log "SKIP     ID:$id  (Not applicable) $outStr" "SKIP"
            $Results.Add([PSCustomObject]@{ID=$id;Status="Unsupported";Output=$outStr;Timestamp=(Get-Date)})
        } else {
            Write-Log "WARN     ID:$id  exit=$exitCode  $outStr" "WARN"
            $Results.Add([PSCustomObject]@{ID=$id;Status="Error";Output=$outStr;Timestamp=(Get-Date)})
        }
    } catch {
        Write-Log "ERROR    ID:$id  $_" "ERROR"
        $Results.Add([PSCustomObject]@{ID=$id;Status="Error";Output=$_.ToString();Timestamp=(Get-Date)})
    }
}
Write-Progress -Activity "Disabling features (rollback)" -Completed

# 5. Summary
$disabled    = @($Results | Where-Object Status -eq "Disabled").Count
$unsupported = @($Results | Where-Object Status -eq "Unsupported").Count
$errors      = @($Results | Where-Object Status -eq "Error").Count
Write-Log "=== SUMMARY === Total:$($Results.Count) Disabled:$disabled Unsupported:$unsupported Errors:$errors"
$csv = Join-Path $LogDir "disable_$Stamp.csv"
$Results | Export-Csv -Path $csv -NoTypeInformation -Encoding UTF8
Write-Log "CSV: $csv"

# 6. Restart Explorer
if ($RestartExplorer -and -not $WhatIfPreference) {
    Write-Log "Restarting explorer.exe ..."
    Stop-Process -Name explorer -Force -ErrorAction SilentlyContinue
    Start-Sleep -Seconds 2; Start-Process explorer.exe
    Write-Log "Explorer restarted." "SUCCESS"
} elseif (-not $RestartExplorer -and -not $WhatIfPreference) {
    $ans = Read-Host "Restart Windows Explorer now? [y/N]"
    if ($ans -match "^[Yy]") {
        Stop-Process -Name explorer -Force -ErrorAction SilentlyContinue
        Start-Sleep -Seconds 2; Start-Process explorer.exe
        Write-Log "Explorer restarted." "SUCCESS"
    }
}
Write-Log "Rollback complete."
