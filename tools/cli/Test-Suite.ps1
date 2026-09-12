#Requires -Version 5.1
# Test-Suite.ps1 -- Non-destructive self-test. Run standalone only.
Set-StrictMode -Version Latest
$ErrorActionPreference = "Continue"
$ScriptDir = if ($PSScriptRoot) { $PSScriptRoot } else { Split-Path -Parent $MyInvocation.MyCommand.Definition }

$passed = 0; $failed = 0; $results = @()

function Test-Assert {
    param([string]$Name,[bool]$Condition,[string]$Detail="")
    if ($Condition) { Write-Host "  [PASS] $Name" -ForegroundColor Green; $script:passed++ }
    else            { Write-Host "  [FAIL] $Name  $Detail" -ForegroundColor Red; $script:failed++ }
    $script:results += [PSCustomObject]@{Test=$Name;Result=if($Condition){"PASS"}else{"FAIL"};Detail=$Detail}
}

Write-Host "`n=== ViVeTool Suite -- Self-Test Runner ===" -ForegroundColor Cyan
Write-Host "Working directory: $ScriptDir`n"

# --- 1. File Integrity ---
Write-Host "--- [1] File Integrity ---" -ForegroundColor Cyan
foreach ($f in @("FeatureCatalog.ps1","Get-ViveTool.ps1","Enable-Features.ps1","Disable-Features.ps1")) {
    $p = Join-Path $ScriptDir $f
    Test-Assert "File exists: $f" (Test-Path $p) "Expected: $p"
}

# --- 2. Syntax Check (excludes self to avoid re-entry) ---
Write-Host "`n--- [2] PowerShell Syntax Check ---" -ForegroundColor Cyan
$checkFiles = @("FeatureCatalog.ps1","Get-ViveTool.ps1","Enable-Features.ps1","Disable-Features.ps1")
foreach ($fname in $checkFiles) {
    $fpath = Join-Path $ScriptDir $fname
    if (Test-Path $fpath) {
        $errs = $null
        $null = [System.Management.Automation.Language.Parser]::ParseFile($fpath,[ref]$null,[ref]$errs)
        Test-Assert "Syntax OK: $fname" ($errs.Count -eq 0) "Errors: $($errs -join '; ')"
    }
}

# --- 3. Feature Catalog ---
Write-Host "`n--- [3] Feature Catalog ---" -ForegroundColor Cyan
try {
    . (Join-Path $ScriptDir "FeatureCatalog.ps1")
    Test-Assert "FeatureCatalog loads" $true
    Test-Assert "Has named entries"    ($FeatureCatalog.Count -gt 0)  "Count: $($FeatureCatalog.Count)"
    Test-Assert "AllFeatureIDs populated" ($AllFeatureIDs.Count -gt 0) "Count: $($AllFeatureIDs.Count)"
    Test-Assert "100+ unique IDs"  ($AllFeatureIDs.Count -ge 100) "Found: $($AllFeatureIDs.Count)"
    $bad = @($AllFeatureIDs | Where-Object { $_ -lt 10000000 -or $_ -gt 99999999 })
    Test-Assert "All IDs are 8-digit" ($bad.Count -eq 0) "Invalid: $($bad -join ', ')"
    $minID = ($AllFeatureIDs | Measure-Object -Minimum).Minimum
    $maxID = ($AllFeatureIDs | Measure-Object -Maximum).Maximum
    Write-Host "  [INFO] Named entries : $($FeatureCatalog.Count)" -ForegroundColor DarkCyan
    Write-Host "  [INFO] Unique IDs    : $($AllFeatureIDs.Count)"  -ForegroundColor DarkCyan
    Write-Host "  [INFO] ID range      : $minID to $maxID"         -ForegroundColor DarkCyan
} catch {
    Test-Assert "FeatureCatalog loads without error" $false "Exception: $_"
}

# --- 4. ViVeTool Availability ---
Write-Host "`n--- [4] ViVeTool Availability ---" -ForegroundColor Cyan
$vitePath = Join-Path $ScriptDir "vivetool.exe"
$onPath   = Get-Command vivetool.exe -ErrorAction SilentlyContinue
$available = (Test-Path $vitePath) -or ($null -ne $onPath)
Test-Assert "vivetool.exe found (local or PATH)" $available "Run Enable-Features.ps1 to auto-download"
if ($available) {
    $rp = if (Test-Path $vitePath) { $vitePath } else { $onPath.Source }
    try {
        $out = & $rp /help 2>&1
        Test-Assert "ViVeTool responds to /help" ($LASTEXITCODE -in @(0,1))
        Test-Assert "ViVeTool output has 'enable'" (($out -join "") -match "enable")
    } catch {
        Test-Assert "ViVeTool invocable" $false "Exception: $_"
    }
}

# --- 5. Logs Directory ---
Write-Host "`n--- [5] Logs Directory ---" -ForegroundColor Cyan
$logDir = Join-Path $ScriptDir "Logs"
if (-not (Test-Path $logDir)) { New-Item -ItemType Directory -Path $logDir | Out-Null }
Test-Assert "Logs directory exists or created" (Test-Path $logDir)

# --- Summary ---
Write-Host "`n=== TEST RESULTS ===" -ForegroundColor Cyan
Write-Host "  PASSED: $passed" -ForegroundColor Green
Write-Host "  FAILED: $failed" -ForegroundColor $(if ($failed -gt 0) { "Red" } else { "Green" })
$rpt = Join-Path $ScriptDir "Logs\test_$(Get-Date -Format 'yyyyMMdd_HHmmss').csv"
$results | Export-Csv -Path $rpt -NoTypeInformation -Encoding UTF8
Write-Host "  Report : $rpt" -ForegroundColor DarkCyan
if ($failed -gt 0) { exit 1 } else { exit 0 }
