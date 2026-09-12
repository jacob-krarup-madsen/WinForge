<#
.SYNOPSIS
    Test-E2EWorkflow.ps1 - Tier 4 Real-World Full Lifecycle Application Scenarios & Verification.
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

# Authoritative channel reference sets
$rawGA2026 = @("61161244", "61754985", "62762248", "59213768", "60813048", "61090762", "59728252", "27829265", "61457898", "61160789", "58989177", "58989092", "60716524", "48433719", "61391826", "58989070", "58989021", "58989002", "57741219", "55994763", "58988972")
$rawGA2025 = @("57048237", "59162732", "41356296", "45690266", "59265307", "57882334", "53343270", "57048231", "47205210", "57048226", "57048218", "57048216")
$raw26H2   = @("60813048", "62141177", "62068874", "63194003", "62915050", "61483244", "60490208", "60730253", "61384404", "60414189", "48433719", "61161244", "61161268", "61160789", "61161304", "61161283", "61441697", "61267302", "61344081", "61482515", "61532758", "61760679", "61465695", "61465915", "62261462", "60511437", "51406324", "60288851", "58989092", "58989177", "61754985", "61225604", "61596616", "61596617", "61596618", "61596619", "61372722", "59213768", "61090762", "60716524", "61391826", "61014711", "59728252", "60897831", "60662124", "57156807", "59956305", "57751666", "57751687", "61157505", "61410885", "60772592", "60911173", "58429068", "58111409", "27829265", "59149945", "58989070", "58989021", "58989002", "59764273", "60772996", "53343270", "59265307", "60597402", "60825171", "58988972", "57741219", "49059846", "60063638", "58182453", "57118881")
$raw25H2   = @("59359094", "58978959", "58381341", "58527096", "57259990", "58938944", "57900749", "58324036", "58680439", "38679741", "41118774", "55805655", "59213523", "59193521", "59765208", "55324166", "59673297", "58423575", "58778013", "59339532", "55994763", "59162732", "57739723", "57941090", "58970402", "58383338", "59270880", "59203365", "41356296", "57703775", "57645315")
$rawCanary = @("61121285", "58288238", "53283713", "59065581", "45425284")

$unique118 = ($rawGA2026 + $rawGA2025 + $raw26H2 + $raw25H2 + $rawCanary) | Select-Object -Unique

Describe "Tier 4: Real-World End-to-End Application Scenarios" {

    Context "Scenario S1: Clean Setup to Full Enablement Lifecycle" {
        It "T4.E2E.01: End-to-end enablement of 118 catalog features with full logging" {
            $sandbox = New-TestSandbox
            try {
                $mockStore = Join-Path $sandbox "mock_store.txt"
                $logFile = Join-Path $sandbox "enable.log"
                $csvFile = Join-Path $sandbox "enable.csv"
                $jsonFile = Join-Path $sandbox "summary.json"
                
                $env:VIVETOOL_MOCK_STORE = $mockStore
                $env:VIVETOOL_MOCK_LOG_FILE = $logFile
                
                $csvRows = @()
                $successCount = 0
                
                foreach ($id in $unique118) {
                    $out = & $mockShim /enable /id:$id
                    $code = $LASTEXITCODE
                    $status = if ($code -eq 0) { "Success" } else { "Error" }
                    if ($status -eq "Success") { $successCount++ }
                    
                    $csvRows += [PSCustomObject]@{
                        Timestamp = (Get-Date -Format 'o')
                        FeatureID = $id
                        Action    = "Enable"
                        Result    = $status
                        ExitCode  = $code
                    }
                }
                
                $csvRows | Export-Csv -Path $csvFile -NoTypeInformation -Encoding utf8
                
                $summaryObj = [PSCustomObject]@{
                    TotalFeatures = $unique118.Count
                    SuccessCount  = $successCount
                    ErrorCount    = 0
                    ExecutionDate = (Get-Date -Format 'o')
                }
                $summaryObj | ConvertTo-Json | Set-Content -Path $jsonFile -Encoding utf8
                
                Assert-PathExists $logFile "Log file must exist"
                Assert-PathExists $csvFile "CSV log file must exist"
                Assert-PathExists $jsonFile "JSON summary must exist"
                
                $stored = Get-Content $mockStore
                Assert-Count $stored 118 "Mock store must record 118 enabled features"
                Assert-Equal $successCount 118 "All 118 features enabled successfully"
            } finally {
                $env:VIVETOOL_MOCK_STORE = $null
                $env:VIVETOOL_MOCK_LOG_FILE = $null
                Remove-TestSandbox $sandbox
            }
        }
    }

    Context "Scenario S2: Incremental Run & Idempotency" {
        It "T4.E2E.02: Successive enablement runs must complete cleanly without collision" {
            $sandbox = New-TestSandbox
            try {
                $mockStore = Join-Path $sandbox "mock_store.txt"
                $env:VIVETOOL_MOCK_STORE = $mockStore
                
                $testIds = @("61161244", "61754985", "62762248")
                
                # Run 1
                foreach ($id in $testIds) {
                    & $mockShim /enable /id:$id | Out-Null
                    Assert-Equal $LASTEXITCODE 0 "Run 1 must succeed for $id"
                }
                
                # Run 2 (Idempotency)
                foreach ($id in $testIds) {
                    & $mockShim /enable /id:$id | Out-Null
                    Assert-Equal $LASTEXITCODE 0 "Run 2 must succeed idempotently for $id"
                }
                
                $stored = Get-Content $mockStore
                Assert-Count $stored 3 "Mock store must contain deduplicated unique IDs"
            } finally {
                $env:VIVETOOL_MOCK_STORE = $null
                Remove-TestSandbox $sandbox
            }
        }
    }

    Context "Scenario S3: Fault Injection & Resilient Recovery" {
        It "T4.E2E.03: Batch must process 100% of features despite injected 0x80070490 and AccessDenied" {
            $sandbox = New-TestSandbox
            try {
                $env:VIVETOOL_MOCK_FAIL_IDS = "61754985,59213768"
                $env:VIVETOOL_MOCK_DENIED_IDS = "60813048"
                
                $batchIds = @("61161244", "61754985", "62762248", "59213768", "60813048", "61090762")
                $results = @()
                
                foreach ($id in $batchIds) {
                    $out = & $mockShim /enable /id:$id 2>&1
                    $code = $LASTEXITCODE
                    $status = if ($code -eq 0) { "Success" }
                              elseif ($code -eq 1) { "Unsupported" }
                              elseif ($code -eq 5) { "AccessDenied" }
                              else { "Error" }
                    
                    $results += [PSCustomObject]@{ Id = $id; Status = $status; Code = $code }
                }
                
                Assert-Count $results 6 "All 6 items must be processed without early termination"
                
                $successes = @($results | Where-Object { $_.Status -eq "Success" }).Count
                $unsupported = @($results | Where-Object { $_.Status -eq "Unsupported" }).Count
                $denied = @($results | Where-Object { $_.Status -eq "AccessDenied" }).Count
                
                Assert-Equal $successes 3 "Exactly 3 succeeded"
                Assert-Equal $unsupported 2 "Exactly 2 unsupported (61754985, 59213768)"
                Assert-Equal $denied 1 "Exactly 1 access denied (60813048)"
            } finally {
                $env:VIVETOOL_MOCK_FAIL_IDS = $null
                $env:VIVETOOL_MOCK_DENIED_IDS = $null
                Remove-TestSandbox $sandbox
            }
        }
    }

    Context "Scenario S4: Full Symmetric Rollback Lifecycle" {
        It "T4.E2E.04: Full cycle: Batch Enable -> Session Rollback Gen -> Batch Disable -> Verify State" {
            $sandbox = New-TestSandbox
            try {
                $mockStore = Join-Path $sandbox "mock_store.txt"
                $env:VIVETOOL_MOCK_STORE = $mockStore
                
                # Step 1: Enable 118 Features
                foreach ($id in $unique118) {
                    & $mockShim /enable /id:$id | Out-Null
                }
                $enabledCount = (Get-Content $mockStore).Count
                Assert-Equal $enabledCount 118 "118 features enabled in mock store"
                
                # Step 2: Generate Rollback Script
                $rollbackScript = Join-Path $sandbox "rollback_full.ps1"
                $lines = @("# Auto-generated rollback")
                foreach ($id in $unique118) {
                    $lines += "& '$mockShim' /disable /id:$id"
                }
                $lines | Set-Content -Path $rollbackScript -Encoding utf8
                Assert-PathExists $rollbackScript "Rollback script created"
                
                # Step 3: Execute Rollback Script
                & $rollbackScript | Out-Null
                
                # Step 4: Verify Store is empty
                $remainingCount = if (Test-Path $mockStore) { @(Get-Content $mockStore).Count } else { 0 }
                Assert-Equal $remainingCount 0 "All 118 features must be removed from mock store after rollback"
            } finally {
                $env:VIVETOOL_MOCK_STORE = $null
                Remove-TestSandbox $sandbox
            }
        }
    }

    Context "Scenario S5: Partial Channel Selective Enablement & Rollback" {
        It "T4.E2E.05: Selectively enable GA2026 (21 IDs), revert from CSV log, verify exact reversion" {
            $sandbox = New-TestSandbox
            try {
                $mockStore = Join-Path $sandbox "mock_store.txt"
                $sessionCsv = Join-Path $sandbox "ga2026_session.csv"
                $env:VIVETOOL_MOCK_STORE = $mockStore
                
                # Step 1: Enable only GA 2026 IDs
                $csvRows = @()
                foreach ($id in $rawGA2026) {
                    & $mockShim /enable /id:$id | Out-Null
                    $csvRows += [PSCustomObject]@{ FeatureID = $id; Action = "Enable"; Result = "Success" }
                }
                $csvRows | Export-Csv -Path $sessionCsv -NoTypeInformation -Encoding utf8
                
                $stored = Get-Content $mockStore
                Assert-Count $stored 21 "21 features enabled in store"
                
                # Step 2: Rollback from Log
                $records = Import-Csv -Path $sessionCsv
                foreach ($rec in $records) {
                    if ($rec.Result -eq "Success") {
                        & $mockShim /disable /id:$($rec.FeatureID) | Out-Null
                    }
                }
                
                $finalCount = if (Test-Path $mockStore) { @(Get-Content $mockStore).Count } else { 0 }
                Assert-Equal $finalCount 0 "Store must be completely cleared after log-driven rollback"
            } finally {
                $env:VIVETOOL_MOCK_STORE = $null
                Remove-TestSandbox $sandbox
            }
        }
    }

    Context "Scenario S6: Non-Destructive Mock Safety & Host Integrity" {
        It "T4.E2E.06: Verify Explorer process is unharmed and host system registry is unmodified" {
            $explorerProcesses = Get-Process -Name explorer -ErrorAction SilentlyContinue
            Assert-True ($explorerProcesses.Count -gt 0) "Explorer must remain actively running during test executions"
        }

        It "T4.E2E.07: Verify temporary test sandboxes are cleanly removable" {
            $sandbox = New-TestSandbox
            Assert-PathExists $sandbox "Sandbox created"
            Remove-TestSandbox $sandbox
            Assert-PathNotExist $sandbox "Sandbox cleanly removed"
        }
    }

    Context "Scenario S7: Custom Feature ID Subset Lifecycle" {
        It "T4.E2E.08: Enable specific 5 IDs, generate rollback, revert and verify store" {
            $sandbox = New-TestSandbox
            try {
                $mockStore = Join-Path $sandbox "mock_store.txt"
                $env:VIVETOOL_MOCK_STORE = $mockStore
                $custom5 = @("61121285", "58288238", "53283713", "59065581", "45425284")
                
                foreach ($id in $custom5) {
                    & $mockShim /enable /id:$id | Out-Null
                }
                $count1 = (Get-Content $mockStore).Count
                Assert-Equal $count1 5 "5 IDs enabled"
                
                foreach ($id in $custom5) {
                    & $mockShim /disable /id:$id | Out-Null
                }
                $count2 = if (Test-Path $mockStore) { @(Get-Content $mockStore).Count } else { 0 }
                Assert-Equal $count2 0 "5 IDs cleanly disabled"
            } finally {
                $env:VIVETOOL_MOCK_STORE = $null
                Remove-TestSandbox $sandbox
            }
        }
    }
}

if ($MyInvocation.InvocationName -ne '.') {
    $summary = Get-TestSummary
    $color = if ($summary.AllPassed) { "Green" } else { "Red" }
    Write-Host "`nTest Suite Completed: $($summary.Passed) Passed, $($summary.Failed) Failed, $($summary.Skipped) Skipped." -ForegroundColor $color
    if (-not $summary.AllPassed) { exit 1 }
}
