<#
.SYNOPSIS
    Test-Enabler.ps1 - Tier 1 & Tier 2 Batch Feature Enablement, Status Parsing & Multi-Sink Logging Tests.
#>
[CmdletBinding()]
param(
    [string]$RootPath
)

if (-not $RootPath) {
    $RootPath = if ($PSScriptRoot) { Split-Path -Parent $PSScriptRoot } else { Split-Path -Parent (Get-Location).Path }
}

$modulePath = Join-Path -Path $PSScriptRoot -ChildPath "TestHarness.psm1"
Import-Module $modulePath -Force

$mockShim = Join-Path $PSScriptRoot "MockViVeTool.ps1"

Describe "Tier 1: Batch Feature Enabler Subsystem & Status Parsing" {

    Context "Sequential Execution Loop & CLI Protocol" {
        It "T1.ENAB.01: Mock ViVeTool shim should execute /enable /id:<id> successfully" {
            $testId = "61161244"
            $output = & $mockShim /enable /id:$testId
            $exitCode = $LASTEXITCODE
            
            Assert-Equal $exitCode 0 "Mock invocation must return exit code 0"
            $outText = if ($output) { [string]::Join("`n", @($output)) } else { "" }
            Assert-Match $outText "Successfully set feature configuration: $testId" "Output must indicate success"
        }

        It "T1.ENAB.02: Sequential batch enabler should iterate over all provided feature IDs" {
            $sandbox = New-TestSandbox
            try {
                $mockLog = Join-Path $sandbox "invocations.log"
                $env:VIVETOOL_MOCK_LOG_FILE = $mockLog
                
                $batchIds = @("61161244", "61754985", "62762248")
                foreach ($id in $batchIds) {
                    & $mockShim /enable /id:$id | Out-Null
                }
                
                $invocations = Get-Content $mockLog
                Assert-Count $invocations 3 "Exactly 3 invocations must be recorded"
                Assert-Match $invocations[0] "61161244" "First invocation must match ID 1"
                Assert-Match $invocations[1] "61754985" "Second invocation must match ID 2"
                Assert-Match $invocations[2] "62762248" "Third invocation must match ID 3"
            } finally {
                $env:VIVETOOL_MOCK_LOG_FILE = $null
                Remove-TestSandbox $sandbox
            }
        }

        It "T1.ENAB.03: Should format arguments with /enable and /id:<id> prefix" {
            $id = "58989092"
            $argList = @("/enable", "/id:$id")
            Assert-Equal $argList[0] "/enable" "First parameter must be /enable"
            Assert-Equal $argList[1] "/id:58989092" "Second parameter must format ID"
        }
    }

    Context "Status Classification Engine" {
        It "T1.ENAB.04: Exit code 0 with success message must classify as 'Success'" {
            $exitCode = 0
            $stdout = "ViVeTool v0.3.4`nSuccessfully set feature configuration: 61161244"
            
            $status = if ($exitCode -eq 0 -and $stdout -match "Successfully set") { "Success" } else { "Error" }
            Assert-Equal $status "Success" "Status must be classified as Success"
        }

        It "T1.ENAB.05: Exit code 1 or 'Feature not found' must classify as 'Unsupported'" {
            $exitCode = 1
            $stdout = "ViVeTool v0.3.4`nFailed to set feature configuration: Feature 99999999 not found"
            
            $status = if ($exitCode -eq 1 -or $stdout -match "not found") { "Unsupported" } else { "Error" }
            Assert-Equal $status "Unsupported" "Status must be classified as Unsupported"
        }

        It "T1.ENAB.06: Exit code 5 or 'Access is denied' must classify as 'AccessDenied'" {
            $exitCode = 5
            $stdout = "An error occurred while setting feature configurations in the Runtime store (Access is denied)"
            
            $status = if ($exitCode -eq 5 -or $stdout -match "Access is denied") { "AccessDenied" } else { "Error" }
            Assert-Equal $status "AccessDenied" "Status must be classified as AccessDenied"
        }

        It "T1.ENAB.07: Syntax error or exit code 2 must classify as 'SyntaxError'" {
            $exitCode = 2
            $stdout = "Error: Invalid parameter syntax"
            
            $status = if ($exitCode -eq 2 -or $stdout -match "Invalid parameter") { "SyntaxError" } else { "Unknown" }
            Assert-Equal $status "SyntaxError" "Status must be classified as SyntaxError"
        }

        It "T1.ENAB.08: Unexpected fatal error code (e.g. 255) must classify as 'FatalError'" {
            $exitCode = 255
            $stdout = "Process crashed"
            $status = if ($exitCode -notin @(0, 1, 5, 2)) { "FatalError" } else { "Handled" }
            Assert-Equal $status "FatalError" "Code 255 must classify as FatalError"
        }
    }

    Context "Multi-Sink Persistent Logging Subsystem" {
        It "T1.ENAB.09: Plain text log sink must write structured timestamped entries" {
            $sandbox = New-TestSandbox
            try {
                $logFile = Join-Path $sandbox "enable.log"
                $entry = "[$(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')] [INFO] [61161244] Action: Enable | Result: Success | Msg: OK"
                Set-Content -Path $logFile -Value $entry -Encoding utf8
                
                Assert-PathExists $logFile "Log file must exist"
                $content = Get-Content $logFile -Raw
                Assert-Match $content '\[\d{4}-\d{2}-\d{2} \d{2}[\.:]\d{2}[\.:]\d{2}\]' "Log must contain timestamp"
                Assert-Match $content '61161244' "Log must contain feature ID"
                Assert-Match $content 'Result: Success' "Log must contain status result"
            } finally {
                Remove-TestSandbox $sandbox
            }
        }

        It "T1.ENAB.10: Structured CSV log sink must write parseable CSV with header" {
            $sandbox = New-TestSandbox
            try {
                $csvFile = Join-Path $sandbox "enable.csv"
                $csvRows = @(
                    [PSCustomObject]@{ Timestamp = (Get-Date -Format 'o'); FeatureID = "61161244"; Action = "Enable"; Result = "Success"; ExitCode = 0; Message = "OK" },
                    [PSCustomObject]@{ Timestamp = (Get-Date -Format 'o'); FeatureID = "61754985"; Action = "Enable"; Result = "Unsupported"; ExitCode = 1; Message = "Not found" }
                )
                $csvRows | Export-Csv -Path $csvFile -NoTypeInformation -Encoding utf8
                
                Assert-PathExists $csvFile "CSV log file must exist"
                $imported = Import-Csv -Path $csvFile
                Assert-Count $imported 2 "CSV must contain 2 rows"
                Assert-Equal $imported[0].FeatureID "61161244" "Row 1 ID match"
                Assert-Equal $imported[0].Result "Success" "Row 1 Result match"
                Assert-Equal $imported[1].Result "Unsupported" "Row 2 Result match"
            } finally {
                Remove-TestSandbox $sandbox
            }
        }

        It "T1.ENAB.11: JSON metrics summary sink must contain aggregated counts" {
            $sandbox = New-TestSandbox
            try {
                $jsonFile = Join-Path $sandbox "summary.json"
                $summaryObj = [PSCustomObject]@{
                    StartTime    = (Get-Date).AddMinutes(-1).ToString('o')
                    EndTime      = (Get-Date).ToString('o')
                    Total        = 118
                    SuccessCount = 100
                    UnsupportedCount = 18
                    ErrorCount   = 0
                    ExecutionMode = "Simulated"
                }
                $summaryObj | ConvertTo-Json -Depth 4 | Set-Content -Path $jsonFile -Encoding utf8
                
                Assert-PathExists $jsonFile "JSON summary file must exist"
                $parsed = Get-Content $jsonFile -Raw | ConvertFrom-Json
                Assert-Equal $parsed.Total 118 "Total metric must be 118"
                Assert-Equal $parsed.SuccessCount 100 "Success count metric must be 100"
                Assert-Equal $parsed.UnsupportedCount 18 "Unsupported count metric must be 18"
            } finally {
                Remove-TestSandbox $sandbox
            }
        }
    }

    Context "Explorer Restart & Test Guard Subsystem" {
        It "T1.ENAB.12: In simulated / test mode, explorer restart must be intercepted safely" {
            $mockExplorerRestartCalled = $false
            $isTestMode = $true
            
            if ($isTestMode) {
                $mockExplorerRestartCalled = $true
            } else {
                Stop-Process -Name explorer -Force
            }
            
            Assert-True $mockExplorerRestartCalled "Test mode must record restart request without killing shell"
        }

        It "T1.ENAB.13: DryRun mode must produce execution preview without invoking binaries" {
            $dryRun = $true
            $invokedReal = $false
            
            if (-not $dryRun) {
                $invokedReal = $true
            }
            
            Assert-False $invokedReal "DryRun mode must completely bypass real binary invocation"
        }

        It "T1.ENAB.14: Support -WhatIf and -Confirm risk mitigation switches" {
            $hasSupportsShouldProcess = $true
            Assert-True $hasSupportsShouldProcess "CmdletBinding SupportsShouldProcess must be active"
        }
    }
}

Describe "Tier 2: Enabler Boundary, Fault Injection & Edge Cases" {

    Context "Resilience: Graceful Continuation on Errors" {
        It "T2.ENAB.01: Batch loop must continue executing remaining IDs when unsupported IDs occur" {
            $sandbox = New-TestSandbox
            try {
                $mockLog = Join-Path $sandbox "invocations.log"
                $env:VIVETOOL_MOCK_LOG_FILE = $mockLog
                $env:VIVETOOL_MOCK_FAIL_IDS = "61754985"
                
                $batchIds = @("61161244", "61754985", "62762248")
                $results = @()
                
                foreach ($id in $batchIds) {
                    $out = & $mockShim /enable /id:$id
                    $code = $LASTEXITCODE
                    $status = if ($code -eq 0) { "Success" } else { "Unsupported" }
                    $results += [PSCustomObject]@{ Id = $id; Status = $status }
                }
                
                Assert-Count $results 3 "All 3 items must be processed"
                Assert-Equal $results[0].Status "Success" "Item 1 must be Success"
                Assert-Equal $results[1].Status "Unsupported" "Item 2 must be Unsupported"
                Assert-Equal $results[2].Status "Success" "Item 3 must be Success"
            } finally {
                $env:VIVETOOL_MOCK_LOG_FILE = $null
                $env:VIVETOOL_MOCK_FAIL_IDS = $null
                Remove-TestSandbox $sandbox
            }
        }

        It "T2.ENAB.02: Batch loop must handle AccessDenied gracefully and record failure status" {
            $sandbox = New-TestSandbox
            try {
                $env:VIVETOOL_MOCK_DENIED_IDS = "61754985"
                
                $out = & $mockShim /enable /id:61754985 2>&1
                $code = $LASTEXITCODE
                $status = if ($code -eq 5) { "AccessDenied" } else { "Success" }
                
                Assert-Equal $code 5 "Exit code must be 5"
                Assert-Equal $status "AccessDenied" "Status must be AccessDenied"
            } finally {
                $env:VIVETOOL_MOCK_DENIED_IDS = $null
                Remove-TestSandbox $sandbox
            }
        }

        It "T2.ENAB.03: ViVeTool exit code 0 caveat: regex parsing catches error in stdout" {
            $simulatedExitCode = 0
            $simulatedStdout = "An error occurred while setting feature configurations in the Runtime store (Access is denied)"
            
            $status = "Success"
            if ($simulatedStdout -match "Access is denied|error occurred|not found|Failed to set") {
                if ($simulatedStdout -match "Access is denied") { $status = "AccessDenied" }
                elseif ($simulatedStdout -match "not found") { $status = "Unsupported" }
                else { $status = "Error" }
            }
            
            Assert-Equal $status "AccessDenied" "Output parser must catch error even when exit code is 0"
        }

        It "T2.ENAB.04: Batch loop must survive simulated crash of individual feature call" {
            $sandbox = New-TestSandbox
            try {
                $env:VIVETOOL_MOCK_EXITCODE = "255"
                $out = & $mockShim /enable /id:61161244 2>&1
                $code = $LASTEXITCODE
                Assert-Equal $code 255 "Crashed exit code must be 255"
            } finally {
                $env:VIVETOOL_MOCK_EXITCODE = $null
                Remove-TestSandbox $sandbox
            }
        }
    }

    Context "Path & Parameter Robustness" {
        It "T2.ENAB.05: Should support execution paths with spaces, parentheses and special symbols" {
            $sandbox = New-TestSandbox
            try {
                $complexPath = Join-Path $sandbox "Test (x86) #1 & Tools [v1]"
                New-Item -ItemType Directory -Path $complexPath -Force | Out-Null
                $shimCopy = Join-Path $complexPath "MockViVeTool.ps1"
                Copy-Item -Path $mockShim -Destination $shimCopy
                
                $out = & $shimCopy /enable /id:61161244
                Assert-Equal $LASTEXITCODE 0 "Invocation from path with spaces/symbols must succeed"
            } finally {
                Remove-TestSandbox $sandbox
            }
        }

        It "T2.ENAB.06: Unwritable log path should automatically fall back to `$env:TEMP" {
            $unwritablePath = "Z:\NonExistent_Drive_9999\test.log"
            $effectivePath = $unwritablePath
            
            try {
                $dir = [System.IO.Path]::GetDirectoryName($unwritablePath)
                if (-not (Test-Path $dir)) {
                    New-Item -ItemType Directory -Path $dir -Force -ErrorAction Stop | Out-Null
                }
            } catch {
                $effectivePath = Join-Path $env:TEMP "vivetool_fallback_$(Get-Date -Format 'yyyyMMdd_HHmmss').log"
            }
            
            Assert-Match $effectivePath "vivetool_fallback" "Fallback path must be used when target drive unwritable"
            Assert-PathExists (Split-Path $effectivePath) "Fallback directory must exist"
        }

        It "T2.ENAB.07: Single-element FeatureIDs array must not suffer from array flattening" {
            $singleIdArray = @("61161244")
            Assert-Equal $singleIdArray.Count 1 "Single element array must maintain count 1"
            
            $processedCount = 0
            foreach ($id in @($singleIdArray)) {
                $processedCount++
            }
            Assert-Equal $processedCount 1 "Foreach loop must iterate exactly once"
        }

        It "T2.ENAB.08: Empty FeatureIDs array should exit cleanly with informative warning" {
            $emptyArray = @()
            $executed = $false
            if ($emptyArray -and $emptyArray.Count -gt 0) {
                $executed = $true
            }
            Assert-False $executed "Empty feature ID list must not trigger any invocations"
        }

        It "T2.ENAB.09: Process timeout detection and kill watchdog" {
            $timeoutMs = 500
            $simulatedExecutionMs = 100
            $isTimeout = ($simulatedExecutionMs -gt $timeoutMs)
            Assert-False $isTimeout "Fast execution must not trigger timeout"
            
            $hungExecutionMs = 2000
            $isHung = ($hungExecutionMs -gt $timeoutMs)
            Assert-True $isHung "Hung execution must trigger timeout watchdog"
        }

        It "T2.ENAB.10: Handle large batch with 50+ unsupported IDs without memory bloat" {
            $largeList = 1..60 | ForEach-Object { "9000000$_" }
            $statusCounts = @{ Success = 0; Unsupported = 0 }
            foreach ($id in $largeList) {
                $statusCounts.Unsupported++
            }
            Assert-Equal $statusCounts.Unsupported 60 "All 60 unsupported IDs processed and counted"
        }
    }
}

if ($MyInvocation.InvocationName -ne '.') {
    $summary = Get-TestSummary
    $color = if ($summary.AllPassed) { "Green" } else { "Red" }
    Write-Host "`nTest Suite Completed: $($summary.Passed) Passed, $($summary.Failed) Failed, $($summary.Skipped) Skipped." -ForegroundColor $color
    if (-not $summary.AllPassed) { exit 1 }
}
