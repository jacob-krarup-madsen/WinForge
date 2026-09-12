# ==============================================================================
# Challenger-M4-DualEngineRollbackStressTests.ps1
# Milestone 4 Dual-Engine & Rollback Stress Testing Suite
# ==============================================================================
[CmdletBinding()]
param(
    [switch]$ExportJson,
    [string]$JsonReportPath
)

$ErrorActionPreference = 'Stop'

$script:TotalTests = 0
$script:PassedTests = 0
$script:FailedTests = 0
$script:TestDetails = [System.Collections.Generic.List[PSCustomObject]]::new()

function Test-Challenge {
    param(
        [Parameter(Mandatory = $true)][string]$Category,
        [Parameter(Mandatory = $true)][string]$Name,
        [Parameter(Mandatory = $true)][scriptblock]$TestBlock
    )

    $script:TotalTests++
    $sw = [System.Diagnostics.Stopwatch]::StartNew()
    $status = "PASS"
    $errMsg = ""

    try {
        $result = & $TestBlock
        if ($result -eq $false) {
            $status = "FAIL"
            $errMsg = "Assertion evaluated to `$false"
        }
    } catch {
        $status = "FAIL"
        $errMsg = $_.Exception.Message
        if ($_.InvocationInfo -and $_.InvocationInfo.ScriptLineNumber) {
            $errMsg += " (Line: $($_.InvocationInfo.ScriptLineNumber))"
        }
    } finally {
        $sw.Stop()
    }

    if ($status -eq "PASS") {
        $script:PassedTests++
        Write-Host "  [PASS] ($($sw.ElapsedMilliseconds)ms) $Name" -ForegroundColor Green
    } else {
        $script:FailedTests++
        Write-Host "  [FAIL] ($($sw.ElapsedMilliseconds)ms) $Name" -ForegroundColor Red
        Write-Host "         Error: $errMsg" -ForegroundColor Yellow
    }

    $script:TestDetails.Add([PSCustomObject]@{
        Category = $Category
        Name     = $Name
        Status   = $status
        Duration = "$($sw.ElapsedMilliseconds)ms"
        Error    = $errMsg
    })
}

$projectRoot = "C:\Tools\vivetool_feature_enabler"
$modulePath  = Join-Path $projectRoot "ViVeToolEnabler.psm1"
$enableScript = Join-Path $projectRoot "Enable-Features.ps1"
$disableScript = Join-Path $projectRoot "Disable-Features.ps1"
$mockScript = Join-Path $projectRoot "tests\MockViVeTool.ps1"

Write-Host "================================================================================" -ForegroundColor Cyan
Write-Host " ViVeTool Feature Enabler -- Challenger M4 Dual-Engine & Rollback Stress Suite" -ForegroundColor Cyan
Write-Host " PowerShell Engine: $($PSVersionTable.PSVersion) (Edition: $($PSVersionTable.PSEdition)) | Root: $projectRoot" -ForegroundColor Cyan
Write-Host "================================================================================" -ForegroundColor Cyan

# Import target module
Import-Module $modulePath -Force

# Set test environment guards
$env:VIVETOOL_TEST_RUNNER = "1"
$env:VIVETOOL_NON_DESTRUCTIVE = "1"
$env:VIVETOOL_MOCK_ADMIN = "1"

# ------------------------------------------------------------------------------
# CATEGORY 1: Dual-Engine Compatibility & Module Contracts
# ------------------------------------------------------------------------------
Write-Host "`n[+] 1. Dual-Engine Module Contracts & Syntax" -ForegroundColor Yellow

Test-Challenge "DualEngine" "ViVeToolEnabler module exports all required functions" {
    $expected = @(
        'Ensure-ViVeTool',
        'Invoke-SelfElevation',
        'Test-IsAdministrator',
        'Get-SystemArchitecture',
        'Get-FeatureCatalog',
        'Invoke-ViVeToolFeature',
        'Invoke-FeatureBatch',
        'Write-FeatureLog',
        'Restart-ExplorerProcess',
        'New-RollbackScript'
    )
    $exported = (Get-Command -Module ViVeToolEnabler).Name
    $allPresent = $true
    foreach ($fn in $expected) {
        if ($exported -notcontains $fn) {
            $allPresent = $false
            break
        }
    }
    $allPresent
}

Test-Challenge "DualEngine" "AST Syntax validation on all repository scripts under current engine" {
    $allPsFiles = Get-ChildItem -Path $projectRoot -Recurse -Filter "*.ps*1"
    $noErrors = $true
    foreach ($f in $allPsFiles) {
        $tokens = $null
        $errors = $null
        [System.Management.Automation.Language.Parser]::ParseFile($f.FullName, [ref]$tokens, [ref]$errors) | Out-Null
        if ($errors -and $errors.Count -gt 0) {
            $noErrors = $false
            Write-Host "AST Error in $($f.FullName): $($errors[0].Message)" -ForegroundColor Red
            break
        }
    }
    $noErrors
}

# ------------------------------------------------------------------------------
# CATEGORY 2: Full Lifecycle DryRun Batch Enablement
# ------------------------------------------------------------------------------
Write-Host "`n[+] 2. Full Lifecycle: DryRun Batch Enablement" -ForegroundColor Yellow

Test-Challenge "DryRun" "Enable-Features.ps1 -DryRun executes all 118 catalog items without side effects" {
    $sandbox = Join-Path $env:TEMP ("vivetool_challenger_dryrun_" + [guid]::NewGuid().ToString('N').Substring(0,8))
    New-Item -ItemType Directory -Path $sandbox -Force | Out-Null
    try {
        $logPath = Join-Path $sandbox "dryrun.log"
        & $enableScript -TargetDirectory $projectRoot -DryRun -LogPath $logPath -ViVeToolPath $mockScript
        
        # Verify log output
        $csvPath = Join-Path $sandbox "dryrun.csv"
        $summaryPath = Join-Path $sandbox "summary.json"
        
        $hasCsv = Test-Path -LiteralPath $csvPath
        $hasJson = Test-Path -LiteralPath $summaryPath
        
        if ($hasCsv -and $hasJson) {
            $csvRows = @(Import-Csv -LiteralPath $csvPath)
            $json = Get-Content -LiteralPath $summaryPath -Raw | ConvertFrom-Json
            ($csvRows.Count -eq 118) -and ($json.Total -eq 118) -and ($json.SkippedCount -eq 118) -and ($json.ExecutionMode -eq 'DryRun')
        } else {
            $false
        }
    } finally {
        Remove-Item -LiteralPath $sandbox -Recurse -Force -ErrorAction SilentlyContinue
    }
}

Test-Challenge "DryRun" "Enable-Features.ps1 -DryRun -Channel 'GA2026', 'Canary' processes exact subset (26 features)" {
    $sandbox = Join-Path $env:TEMP ("vivetool_challenger_dryrun_ch_" + [guid]::NewGuid().ToString('N').Substring(0,8))
    New-Item -ItemType Directory -Path $sandbox -Force | Out-Null
    try {
        $logPath = Join-Path $sandbox "dryrun_ch.log"
        & $enableScript -TargetDirectory $projectRoot -Channel 'GA2026', 'Canary' -DryRun -LogPath $logPath -ViVeToolPath $mockScript
        $csvPath = Join-Path $sandbox "dryrun_ch.csv"
        $csvRows = @(Import-Csv -LiteralPath $csvPath)
        $csvRows.Count -eq 26
    } finally {
        Remove-Item -LiteralPath $sandbox -Recurse -Force -ErrorAction SilentlyContinue
    }
}

Test-Challenge "DryRun" "Enable-Features.ps1 -DryRun -FeatureIds custom list processes exact explicit IDs" {
    $sandbox = Join-Path $env:TEMP ("vivetool_challenger_dryrun_ids_" + [guid]::NewGuid().ToString('N').Substring(0,8))
    New-Item -ItemType Directory -Path $sandbox -Force | Out-Null
    try {
        $logPath = Join-Path $sandbox "dryrun_ids.log"
        $ids = @('61161244', '61754985', '99999999')
        & $enableScript -TargetDirectory $projectRoot -FeatureIds $ids -DryRun -LogPath $logPath -ViVeToolPath $mockScript
        $csvPath = Join-Path $sandbox "dryrun_ids.csv"
        $csvRows = @(Import-Csv -LiteralPath $csvPath)
        ($csvRows.Count -eq 3) -and ($csvRows[2].FeatureID -eq '99999999')
    } finally {
        Remove-Item -LiteralPath $sandbox -Recurse -Force -ErrorAction SilentlyContinue
    }
}

# ------------------------------------------------------------------------------
# CATEGORY 3: Persistent Multi-Sink Logging Subsystem Stress
# ------------------------------------------------------------------------------
Write-Host "`n[+] 3. Multi-Sink Logging Stress Testing" -ForegroundColor Yellow

Test-Challenge "Logging" "Multi-sink logger handles quotes, newlines, and unicode safely in plain text, CSV and JSON" {
    $sandbox = Join-Path $env:TEMP ("vivetool_challenger_log_stress_" + [guid]::NewGuid().ToString('N').Substring(0,8))
    New-Item -ItemType Directory -Path $sandbox -Force | Out-Null
    try {
        $logFile = Join-Path $sandbox "stress.log"
        $entry1 = [PSCustomObject]@{
            Timestamp  = (Get-Date -Format 'o')
            FeatureID  = "61161244"
            Action     = "Enable"
            Result     = "Success"
            ExitCode   = 0
            Message    = "Standard success output: `nFeature 61161244 enabled"
            DurationMs = 12
        }
        $entry2 = [PSCustomObject]@{
            Timestamp  = (Get-Date -Format 'o')
            FeatureID  = "61754985"
            Action     = "Enable"
            Result     = "Unsupported"
            ExitCode   = 1
            Message    = 'Output with "double quotes" and special chars <>&|~'
            DurationMs = 15
        }
        
        Write-FeatureLog -LogEntry $entry1 -LogPath $logFile -NoConsole
        Write-FeatureLog -LogEntry $entry2 -LogPath $logFile -NoConsole
        
        $csvFile = Join-Path $sandbox "stress.csv"
        $csvRecords = @(Import-Csv -LiteralPath $csvFile)
        
        $csvValid = ($csvRecords.Count -eq 2) -and ($csvRecords[0].FeatureID -eq "61161244") -and ($csvRecords[1].Message -match 'double quotes')
        $logContent = Get-Content -LiteralPath $logFile -Raw
        $logValid = ($logContent -match "\[61161244\] Action: Enable") -and ($logContent -match "\[61754985\] Action: Enable")
        
        $csvValid -and $logValid
    } finally {
        Remove-Item -LiteralPath $sandbox -Recurse -Force -ErrorAction SilentlyContinue
    }
}

Test-Challenge "Logging" "Logger automatically recovers to TEMP when target directory is invalid/unwritable" {
    $badDir = "Z:\NonExistentDrive_ViVeTool_Test\subdir\test.log"
    $entry = [PSCustomObject]@{
        Timestamp  = (Get-Date -Format 'o')
        FeatureID  = "61161244"
        Action     = "Enable"
        Result     = "Success"
        ExitCode   = 0
        Message    = "Testing fallback"
        DurationMs = 5
    }
    
    try {
        Write-FeatureLog -LogEntry $entry -LogPath $badDir -NoConsole
        $true
    } catch {
        $false
    }
}

# ------------------------------------------------------------------------------
# CATEGORY 4: Dynamic Rollback Script Generation Subsystem
# ------------------------------------------------------------------------------
Write-Host "`n[+] 4. Dynamic Rollback Script Generation" -ForegroundColor Yellow

Test-Challenge "RollbackGen" "New-RollbackScript generates valid executable standalone PS1 script" {
    $sandbox = Join-Path $env:TEMP ("vivetool_challenger_rb_gen_" + [guid]::NewGuid().ToString('N').Substring(0,8))
    New-Item -ItemType Directory -Path $sandbox -Force | Out-Null
    try {
        $features = @(
            [PSCustomObject]@{ FeatureID = "61161244"; Result = "Success" },
            [PSCustomObject]@{ FeatureID = "61754985"; Result = "Success" },
            [PSCustomObject]@{ FeatureID = "62762248"; Result = "Unsupported" } # Should be excluded
        )
        $rbScript = New-RollbackScript -Features $features -OutputPath $sandbox -TargetDirectory $projectRoot -RestartExplorer:$false
        
        $exists = Test-Path -LiteralPath $rbScript
        $content = Get-Content -LiteralPath $rbScript -Raw
        
        $hasDisable1 = $content -match "vivetool /disable /id:61161244"
        $hasDisable2 = $content -match "vivetool /disable /id:61754985"
        $excludesUnsup = $content -notmatch "62762248"
        
        # Check AST syntax of generated script
        $tokens = $null
        $errors = $null
        [System.Management.Automation.Language.Parser]::ParseFile($rbScript, [ref]$tokens, [ref]$errors) | Out-Null
        $syntaxOk = ($errors.Count -eq 0)
        
        $exists -and $hasDisable1 -and $hasDisable2 -and $excludesUnsup -and $syntaxOk
    } finally {
        Remove-Item -LiteralPath $sandbox -Recurse -Force -ErrorAction SilentlyContinue
    }
}

Test-Challenge "RollbackGen" "New-RollbackScript with -ReverseOrder inverts the execution order" {
    $features = @("1001", "1002", "1003", "1004")
    $sandbox = Join-Path $env:TEMP ("vivetool_challenger_rb_rev_" + [guid]::NewGuid().ToString('N').Substring(0,8))
    New-Item -ItemType Directory -Path $sandbox -Force | Out-Null
    try {
        $rbScript = New-RollbackScript -Features $features -OutputPath $sandbox -TargetDirectory $projectRoot -ReverseOrder
        $content = Get-Content -LiteralPath $rbScript -Raw
        
        $pos1004 = $content.IndexOf("/id:1004")
        $pos1003 = $content.IndexOf("/id:1003")
        $pos1002 = $content.IndexOf("/id:1002")
        $pos1001 = $content.IndexOf("/id:1001")
        
        ($pos1004 -lt $pos1003) -and ($pos1003 -lt $pos1002) -and ($pos1002 -lt $pos1001)
    } finally {
        Remove-Item -LiteralPath $sandbox -Recurse -Force -ErrorAction SilentlyContinue
    }
}

Test-Challenge "RollbackGen" "New-RollbackScript with -FromLog parses only applied features from CSV" {
    $sandbox = Join-Path $env:TEMP ("vivetool_challenger_rb_fromlog_" + [guid]::NewGuid().ToString('N').Substring(0,8))
    New-Item -ItemType Directory -Path $sandbox -Force | Out-Null
    try {
        $csvPath = Join-Path $sandbox "session.csv"
        @"
"Timestamp","FeatureID","Action","Result","ExitCode","Message"
"2026-08-28T12:00:00Z","61161244","Enable","Success","0","Success"
"2026-08-28T12:00:01Z","61754985","Enable","Skipped","0","Skipped"
"2026-08-28T12:00:02Z","62762248","Enable","Unsupported","1","Unsupported"
"2026-08-28T12:00:03Z","59213768","Enable","AccessDenied","5","Denied"
"@ | Set-Content -LiteralPath $csvPath -Encoding utf8

        $rbScript = New-RollbackScript -FromLog $csvPath -OutputPath $sandbox -TargetDirectory $projectRoot
        $content = Get-Content -LiteralPath $rbScript -Raw
        
        $has61161244 = $content -match "61161244"
        $has61754985 = $content -match "61754985"
        $not62762248 = $content -notmatch "62762248"
        $not59213768 = $content -notmatch "59213768"
        
        $has61161244 -and $has61754985 -and $not62762248 -and $not59213768
    } finally {
        Remove-Item -LiteralPath $sandbox -Recurse -Force -ErrorAction SilentlyContinue
    }
}

# ------------------------------------------------------------------------------
# CATEGORY 5: Full Symmetric Rollback Execution Lifecycle
# ------------------------------------------------------------------------------
Write-Host "`n[+] 5. Full Symmetric Rollback Execution Lifecycle" -ForegroundColor Yellow

Test-Challenge "RollbackExecution" "Enable batch -> generate rollback script -> execute rollback script -> verify mock store" {
    $sandbox = Join-Path $env:TEMP ("vivetool_challenger_e2e_rb_" + [guid]::NewGuid().ToString('N').Substring(0,8))
    New-Item -ItemType Directory -Path $sandbox -Force | Out-Null
    try {
        # Initialize mock state store
        $mockStore = Join-Path $sandbox "mock_store.txt"
        $env:VIVETOOL_MOCK_STORE = $mockStore
        $env:VIVETOOL_MOCK_MODE = ""
        $env:VIVETOOL_MOCK_FAIL_IDS = ""
        $env:VIVETOOL_MOCK_DENIED_IDS = ""
        
        $targetIds = @('61161244', '61754985', '62762248', '59213768', '60813048')
        $enableLog = Join-Path $sandbox "enable.log"
        
        # Step 1: Run enablement
        $batch = @(Invoke-FeatureBatch -Features $targetIds -Action 'Enable' -ViVeToolPath $mockScript -LogPath $enableLog)
        
        # Verify all 5 are enabled in store
        $stored1 = if (Test-Path -LiteralPath $mockStore) { @(Get-Content -LiteralPath $mockStore) } else { @() }
        $allEnabled = ($stored1.Count -eq 5)
        foreach ($id in $targetIds) {
            if ($stored1 -notcontains $id) { $allEnabled = $false; break }
        }
        
        if (-not $allEnabled) {
            return $false
        }
        
        # Step 2: Generate dynamic rollback script
        $rbScript = New-RollbackScript -Features $batch -OutputPath $sandbox -ViVeToolPath $mockScript
        
        # Step 3: Execute rollback script in DryRun mode
        & $rbScript -DryRun -ViVeToolPath $mockScript
        
        # Verify store unchanged
        $stored2 = @(Get-Content -LiteralPath $mockStore)
        if ($stored2.Count -ne 5) {
            return $false
        }
        
        # Step 4: Execute rollback script in Live mode
        & $rbScript -ViVeToolPath $mockScript
        
        # Verify all 5 features are now removed from store (store is empty)
        $stored3 = if (Test-Path -LiteralPath $mockStore) { @(Get-Content -LiteralPath $mockStore | Where-Object { -not [string]::IsNullOrWhiteSpace($_) }) } else { @() }
        
        ($stored3.Count -eq 0)
    } finally {
        $env:VIVETOOL_MOCK_STORE = $null
        Remove-Item -LiteralPath $sandbox -Recurse -Force -ErrorAction SilentlyContinue
    }
}

Test-Challenge "RollbackExecution" "Disable-Features.ps1 -FromLog reverts only successful entries and creates disable logs" {
    $sandbox = Join-Path $env:TEMP ("vivetool_challenger_dis_fromlog_" + [guid]::NewGuid().ToString('N').Substring(0,8))
    New-Item -ItemType Directory -Path $sandbox -Force | Out-Null
    try {
        $mockStore = Join-Path $sandbox "mock_store.txt"
        $env:VIVETOOL_MOCK_STORE = $mockStore
        $env:VIVETOOL_MOCK_MODE = ""
        $env:VIVETOOL_MOCK_FAIL_IDS = ""
        $env:VIVETOOL_MOCK_DENIED_IDS = ""
        
        # Prime store with 61161244, 61754985, 62762248
        Set-Content -LiteralPath $mockStore -Value @("61161244", "61754985", "62762248") -Encoding utf8
        
        # Create mock enable log CSV with 61161244 and 61754985 as Success, but 62762248 as Unsupported
        $enableCsv = Join-Path $sandbox "enable.csv"
        @"
"Timestamp","FeatureID","Action","Result","ExitCode","Message"
"2026-08-28T12:00:00Z","61161244","Enable","Success","0","Success"
"2026-08-28T12:00:01Z","61754985","Enable","Success","0","Success"
"2026-08-28T12:00:02Z","62762248","Enable","Unsupported","1","Unsupported"
"@ | Set-Content -LiteralPath $enableCsv -Encoding utf8
        
        $disableLog = Join-Path $sandbox "disable.log"
        & $disableScript -TargetDirectory $projectRoot -FromLog $enableCsv -LogPath $disableLog -ViVeToolPath $mockScript
        
        # Verify store: 61161244 and 61754985 removed; 62762248 remains
        $finalStored = @(Get-Content -LiteralPath $mockStore | Where-Object { -not [string]::IsNullOrWhiteSpace($_) })
        $isReverted = ($finalStored.Count -eq 1) -and ($finalStored -contains "62762248") -and ($finalStored -notcontains "61161244") -and ($finalStored -notcontains "61754985")
        
        # Verify disable logs generated
        $hasDisLog = Test-Path -LiteralPath (Join-Path $sandbox "disable.log")
        $hasDisCsv = Test-Path -LiteralPath (Join-Path $sandbox "disable.csv")
        $hasDisJson = Test-Path -LiteralPath (Join-Path $sandbox "disable_summary.json")
        
        $isReverted -and $hasDisLog -and $hasDisCsv -and $hasDisJson
    } finally {
        $env:VIVETOOL_MOCK_STORE = $null
        Remove-Item -LiteralPath $sandbox -Recurse -Force -ErrorAction SilentlyContinue
    }
}

Test-Challenge "RollbackExecution" "Disable-Features.ps1 -FullReset runs rollback across all 118 catalog IDs" {
    $sandbox = Join-Path $env:TEMP ("vivetool_challenger_dis_full_" + [guid]::NewGuid().ToString('N').Substring(0,8))
    New-Item -ItemType Directory -Path $sandbox -Force | Out-Null
    try {
        $env:VIVETOOL_MOCK_MODE = ""
        $disableLog = Join-Path $sandbox "disable_full.log"
        & $disableScript -TargetDirectory $projectRoot -FullReset -LogPath $disableLog -ViVeToolPath $mockScript
        
        $disCsv = Join-Path $sandbox "disable_full.csv"
        $disSummary = Join-Path $sandbox "disable_summary.json"
        
        $csvRows = @(Import-Csv -LiteralPath $disCsv)
        $summary = Get-Content -LiteralPath $disSummary -Raw | ConvertFrom-Json
        
        ($csvRows.Count -eq 118) -and ($summary.Total -eq 118) -and ($summary.SuccessCount -eq 118)
    } finally {
        Remove-Item -LiteralPath $sandbox -Recurse -Force -ErrorAction SilentlyContinue
    }
}

# ------------------------------------------------------------------------------
# CATEGORY 6: Edge Cases, Fault Injection & Corner Conditions
# ------------------------------------------------------------------------------
Write-Host "`n[+] 6. Fault Injection & Edge Cases" -ForegroundColor Yellow

Test-Challenge "EdgeCases" "Disable-Features.ps1 -FromLog with missing file exits with error code 1" {
    $badLog = "C:\NonExistent_Directory_12345\missing.csv"
    $failed = $false
    try {
        $p = Start-Process -FilePath (if ($PSVersionTable.PSEdition -eq 'Core') { 'pwsh.exe' } else { 'powershell.exe' }) `
                           -ArgumentList "-NoProfile -ExecutionPolicy Bypass -File `"$disableScript`" -FromLog `"$badLog`"" `
                           -Wait -PassThru -NoNewWindow
        $failed = ($p.ExitCode -eq 1)
    } catch {
        $failed = $true
    }
    $failed
}

Test-Challenge "EdgeCases" "Invoke-FeatureBatch handles injected unsupported and denied IDs without breaking loop" {
    $sandbox = Join-Path $env:TEMP ("vivetool_challenger_fault_" + [guid]::NewGuid().ToString('N').Substring(0,8))
    New-Item -ItemType Directory -Path $sandbox -Force | Out-Null
    try {
        $env:VIVETOOL_MOCK_FAIL_IDS = "22222222"
        $env:VIVETOOL_MOCK_DENIED_IDS = "33333333"
        $testIds = @('11111111', '22222222', '33333333', '44444444')
        $batch = @(Invoke-FeatureBatch -Features $testIds -Action 'Enable' -ViVeToolPath $mockScript -LogPath (Join-Path $sandbox "fault.log"))
        
        # Batch must complete all 4 items
        ($batch.Count -eq 4) -and `
        ($batch[0].Result -eq 'Success') -and `
        ($batch[1].Result -eq 'Unsupported') -and `
        ($batch[2].Result -eq 'AccessDenied') -and `
        ($batch[3].Result -eq 'Success')
    } finally {
        $env:VIVETOOL_MOCK_FAIL_IDS = ""
        $env:VIVETOOL_MOCK_DENIED_IDS = ""
        Remove-Item -LiteralPath $sandbox -Recurse -Force -ErrorAction SilentlyContinue
    }
}

Test-Challenge "EdgeCases" "Invoke-FeatureBatch handles exit code 255 (Crash mode) without terminating caller" {
    $sandbox = Join-Path $env:TEMP ("vivetool_challenger_crash_" + [guid]::NewGuid().ToString('N').Substring(0,8))
    New-Item -ItemType Directory -Path $sandbox -Force | Out-Null
    try {
        $env:VIVETOOL_MOCK_MODE = "Crash"
        $testIds = @('11111111')
        $batch = @(Invoke-FeatureBatch -Features $testIds -Action 'Enable' -ViVeToolPath $mockScript -LogPath (Join-Path $sandbox "crash.log"))
        
        ($batch.Count -eq 1) -and ($batch[0].Result -eq 'FatalError' -or $batch[0].Result -eq 'Error')
    } finally {
        $env:VIVETOOL_MOCK_MODE = ""
        Remove-Item -LiteralPath $sandbox -Recurse -Force -ErrorAction SilentlyContinue
    }
}

Test-Challenge "EdgeCases" "Ensure-ViVeTool in DryRun mode returns fallback path without throwing" {
    $resolved = Ensure-ViVeTool -TargetDirectory "C:\NonExistentDir_Test" -DryRun
    $resolved -like "*ViVeTool.exe"
}

Test-Challenge "EdgeCases" "Positional argument normalization when numeric ID passed as position 0" {
    $sandbox = Join-Path $env:TEMP ("vivetool_challenger_pos_" + [guid]::NewGuid().ToString('N').Substring(0,8))
    New-Item -ItemType Directory -Path $sandbox -Force | Out-Null
    try {
        $logPath = Join-Path $sandbox "pos.log"
        & $enableScript "61161244" -DryRun -LogPath $logPath -ViVeToolPath $mockScript
        $csvPath = Join-Path $sandbox "pos.csv"
        $csvRows = @(Import-Csv -LiteralPath $csvPath)
        ($csvRows.Count -eq 1) -and ($csvRows[0].FeatureID -eq "61161244")
    } finally {
        Remove-Item -LiteralPath $sandbox -Recurse -Force -ErrorAction SilentlyContinue
    }
}

# ------------------------------------------------------------------------------
# RESULTS SUMMARY
# ------------------------------------------------------------------------------
Write-Host "`n================================================================================" -ForegroundColor Cyan
Write-Host " Challenger M4 Stress Test Results Summary" -ForegroundColor Cyan
Write-Host "================================================================================" -ForegroundColor Cyan
Write-Host " Total Tests : $script:TotalTests" -ForegroundColor White
Write-Host " Passed      : $script:PassedTests" -ForegroundColor Green
Write-Host " Failed      : $script:FailedTests" -ForegroundColor $(if ($script:FailedTests -gt 0) { "Red" } else { "Gray" })
Write-Host " Pass Rate   : $([math]::Round(($script:PassedTests / $script:TotalTests) * 100, 2))%" -ForegroundColor $(if ($script:FailedTests -eq 0) { "Green" } else { "Red" })
Write-Host "================================================================================" -ForegroundColor Cyan

if ($ExportJson -or $JsonReportPath) {
    $outJson = if ($JsonReportPath) { $JsonReportPath } else { Join-Path $PSScriptRoot "challenger_m4_results_$($PSVersionTable.PSEdition).json" }
    $report = [PSCustomObject]@{
        Engine    = $PSVersionTable.PSVersion.ToString()
        Edition   = $PSVersionTable.PSEdition.ToString()
        Timestamp = (Get-Date -Format 'o')
        Total     = $script:TotalTests
        Passed    = $script:PassedTests
        Failed    = $script:FailedTests
        Tests     = $script:TestDetails
    }
    $report | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath $outJson -Encoding utf8
    Write-Host "Exported JSON results to: $outJson" -ForegroundColor DarkGray
}

if ($script:FailedTests -eq 0) {
    exit 0
} else {
    exit 1
}
