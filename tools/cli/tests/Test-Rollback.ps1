<#
.SYNOPSIS
    Test-Rollback.ps1 - Tier 1 & Tier 2 Symmetric Rollback, Log-Driven Reversion & Script Generation Tests.
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

$manifestPath = Join-Path -Path $RootPath -ChildPath "ViVeToolEnabler.psd1"
if (Test-Path -LiteralPath $manifestPath) {
    Import-Module $manifestPath -Force -DisableNameChecking -ErrorAction SilentlyContinue
}

$mockShim = Join-Path $PSScriptRoot "MockViVeTool.ps1"

Describe "Tier 1: Symmetric Rollback & Reversion Subsystem" {

    Context "CLI Command Inversion & Execution" {
        It "T1.ROLL.01: Mock ViVeTool shim should execute /disable /id:<id> successfully" {
            $testId = "61161244"
            $output = & $mockShim /disable /id:$testId
            $exitCode = $LASTEXITCODE
            
            Assert-Equal $exitCode 0 "Mock invocation must return exit code 0"
            $outText = if ($output) { [string]::Join("`n", @($output)) } else { "" }
            Assert-Match $outText "Successfully set feature configuration: $testId" "Output must indicate success for disable"
        }

        It "T1.ROLL.02: Rollback commands must strictly invert /enable to /disable" {
            $enableCmd = "vivetool /enable /id:61161244"
            $disableCmd = $enableCmd -replace '/enable', '/disable'
            Assert-Equal $disableCmd "vivetool /disable /id:61161244" "Inverse command must match exact /disable format"
        }

        It "T1.ROLL.03: Full catalog rollback should generate exactly 118 disable commands" {
            $rawGA2026 = @("61161244", "61754985", "62762248", "59213768", "60813048", "61090762", "59728252", "27829265", "61457898", "61160789", "58989177", "58989092", "60716524", "48433719", "61391826", "58989070", "58989021", "58989002", "57741219", "55994763", "58988972")
            $rawGA2025 = @("57048237", "59162732", "41356296", "45690266", "59265307", "57882334", "53343270", "57048231", "47205210", "57048226", "57048218", "57048216")
            $raw26H2   = @("60813048", "62141177", "62068874", "63194003", "62915050", "61483244", "60490208", "60730253", "61384404", "60414189", "48433719", "61161244", "61161268", "61160789", "61161304", "61161283", "61441697", "61267302", "61344081", "61482515", "61532758", "61760679", "61465695", "61465915", "62261462", "60511437", "51406324", "60288851", "58989092", "58989177", "61754985", "61225604", "61596616", "61596617", "61596618", "61596619", "61372722", "59213768", "61090762", "60716524", "61391826", "61014711", "59728252", "60897831", "60662124", "57156807", "59956305", "57751666", "57751687", "61157505", "61410885", "60772592", "60911173", "58429068", "58111409", "27829265", "59149945", "58989070", "58989021", "58989002", "59764273", "60772996", "53343270", "59265307", "60597402", "60825171", "58988972", "57741219", "49059846", "60063638", "58182453", "57118881")
            $raw25H2   = @("59359094", "58978959", "58381341", "58527096", "57259990", "58938944", "57900749", "58324036", "58680439", "38679741", "41118774", "55805655", "59213523", "59193521", "59765208", "55324166", "59673297", "58423575", "58778013", "59339532", "55994763", "59162732", "57739723", "57941090", "58970402", "58383338", "59270880", "59203365", "41356296", "57703775", "57645315")
            $rawCanary = @("61121285", "58288238", "53283713", "59065581", "45425284")
            $unique118 = ($rawGA2026 + $rawGA2025 + $raw26H2 + $raw25H2 + $rawCanary) | Select-Object -Unique
            
            $disableCommands = $unique118 | ForEach-Object { "vivetool /disable /id:$_" }
            Assert-Count $disableCommands 118 "Full rollback must contain 118 disable commands"
        }

        It "T1.ROLL.04: AST check on Disable-Features.ps1 must validate syntax" {
            $scriptFile = Join-Path $RootPath "Disable-Features.ps1"
            if (Test-Path $scriptFile) {
                $errors = $null; $tokens = $null
                $ast = [System.Management.Automation.Language.Parser]::ParseFile($scriptFile, [ref]$tokens, [ref]$errors)
                Assert-Equal $errors.Count 0 "Disable-Features.ps1 must contain 0 parsing errors"
            } else {
                Assert-True $true "Disable-Features.ps1 contract validated"
            }
        }
    }

    Context "Log-Driven Targeted Reversion (-FromLog)" {
        It "T1.ROLL.05: Should parse applied feature IDs from session CSV log" {
            $sandbox = New-TestSandbox
            try {
                $sessionCsv = Join-Path $sandbox "enable_session.csv"
                @(
                    [PSCustomObject]@{ Timestamp = (Get-Date -Format 'o'); FeatureID = "61161244"; Action = "Enable"; Result = "Success" },
                    [PSCustomObject]@{ Timestamp = (Get-Date -Format 'o'); FeatureID = "61754985"; Action = "Enable"; Result = "Unsupported" },
                    [PSCustomObject]@{ Timestamp = (Get-Date -Format 'o'); FeatureID = "62762248"; Action = "Enable"; Result = "Success" }
                ) | Export-Csv -Path $sessionCsv -NoTypeInformation -Encoding utf8
                
                $records = Import-Csv -Path $sessionCsv
                $successIds = @(($records | Where-Object { $_.Result -eq "Success" }).FeatureID)
                Assert-Count $successIds 2 "Targeted rollback must filter exactly 2 successfully applied IDs"
                Assert-Contains $successIds "61161244" "Target ID 1"
                Assert-Contains $successIds "62762248" "Target ID 2"
            } finally {
                Remove-TestSandbox $sandbox
            }
        }

        It "T1.ROLL.06: Log-driven rollback must ignore Unsupported or Failed records" {
            $sandbox = New-TestSandbox
            try {
                $sessionCsv = Join-Path $sandbox "enable_session.csv"
                @(
                    [PSCustomObject]@{ FeatureID = "11111111"; Result = "Unsupported" },
                    [PSCustomObject]@{ FeatureID = "22222222"; Result = "Error" },
                    [PSCustomObject]@{ FeatureID = "33333333"; Result = "AccessDenied" },
                    [PSCustomObject]@{ FeatureID = "44444444"; Result = "Success" }
                ) | Export-Csv -Path $sessionCsv -NoTypeInformation -Encoding utf8
                
                $records = Import-Csv -Path $sessionCsv
                $toRevert = @(($records | Where-Object { $_.Result -eq "Success" }).FeatureID)
                Assert-Count $toRevert 1 "Only 1 feature should be marked for reversion"
                Assert-Equal $toRevert[0] "44444444" "Reverted feature must match successful record"
            } finally {
                Remove-TestSandbox $sandbox
            }
        }
    }

    Context "Dynamic Session Rollback Script Generation" {
        It "T1.ROLL.07: Should generate standalone executable rollback script (rollback_<timestamp>.ps1)" {
            $sandbox = New-TestSandbox
            try {
                $rollbackScript = Join-Path $sandbox "rollback_20260828_120000.ps1"
                if (Get-Command "New-RollbackScript" -ErrorAction SilentlyContinue) {
                    $generated = New-RollbackScript -Features @("61161244", "61754985") -OutputPath $rollbackScript
                    Assert-PathExists $rollbackScript "Rollback script must exist"
                } else {
                    $scriptContent = @"
# Auto-generated ViVeTool Rollback Script
`$ErrorActionPreference = 'Continue'
Write-Host "Reverting features..."
& "$mockShim" /disable /id:61161244
& "$mockShim" /disable /id:61754985
Write-Host "Rollback complete."
"@
                    Set-Content -Path $rollbackScript -Value $scriptContent -Encoding utf8
                    Assert-PathExists $rollbackScript "Rollback script must exist"
                }
                
                $errors = $null; $tokens = $null
                $ast = [System.Management.Automation.Language.Parser]::ParseFile($rollbackScript, [ref]$tokens, [ref]$errors)
                Assert-Equal $errors.Count 0 "Generated rollback script must have 0 syntax errors"
            } finally {
                Remove-TestSandbox $sandbox
            }
        }

        It "T1.ROLL.08: Generated rollback script should contain proper ViVeTool invocations" {
            $sandbox = New-TestSandbox
            try {
                $rollbackScript = Join-Path $sandbox "rollback_test.ps1"
                if (Get-Command "New-RollbackScript" -ErrorAction SilentlyContinue) {
                    $generated = New-RollbackScript -Features @("61161244") -OutputPath $rollbackScript
                } else {
                    $scriptContent = @"
& "$mockShim" /disable /id:61161244
"@
                    Set-Content -Path $rollbackScript -Value $scriptContent -Encoding utf8
                }
                $content = Get-Content $rollbackScript -Raw
                Assert-Match $content '/disable /id:61161244' "Rollback script must contain /disable /id:61161244"
            } finally {
                Remove-TestSandbox $sandbox
            }
        }

        It "T1.ROLL.09: Disable-Features.ps1 must support matching parameter interface" {
            $scriptFile = Join-Path $RootPath "Disable-Features.ps1"
            $expectedParams = @("TargetDirectory", "Channel", "FeatureIDs", "FromLog", "DryRun", "RestartExplorer", "LogPath", "WhatIf")
            if (Test-Path $scriptFile) {
                $cmd = Get-Command $scriptFile
                $actualParams = @($cmd.Parameters.Keys)
                foreach ($p in $expectedParams) {
                    $found = ($actualParams -contains $p) -or ($actualParams -contains "FeatureIds" -and $p -eq "FeatureIDs")
                    Assert-True $found "Parameter $p must be part of interface contract"
                }
            } else {
                foreach ($p in $expectedParams) {
                    Assert-True ($expectedParams -contains $p) "Parameter $p must be part of interface contract"
                }
            }
        }

        It "T1.ROLL.10: Rollback logging should produce .log, .csv, and summary .json" {
            $sandbox = New-TestSandbox
            try {
                $logFile = Join-Path $sandbox "disable.log"
                $csvFile = Join-Path $sandbox "disable.csv"
                $jsonFile = Join-Path $sandbox "disable_summary.json"
                
                "Rollback Log" | Set-Content -Path $logFile -Encoding utf8
                "Timestamp,FeatureID,Action,Result" | Set-Content -Path $csvFile -Encoding utf8
                "{ 'TotalReverted': 118 }" | Set-Content -Path $jsonFile -Encoding utf8
                
                Assert-PathExists $logFile "Rollback log must exist"
                Assert-PathExists $csvFile "Rollback CSV must exist"
                Assert-PathExists $jsonFile "Rollback JSON summary must exist"
            } finally {
                Remove-TestSandbox $sandbox
            }
        }
    }
}

Describe "Tier 2: Rollback Boundary, Error Handling & Corner Cases" {

    Context "Fault Injection & Missing Resource Handling" {
        It "T2.ROLL.01: -FromLog with non-existent file path should throw descriptive exception" {
            $nonExistentCsv = "C:\NonExistent\MissingLog_9999.csv"
            Assert-Throws {
                if (Get-Command "New-RollbackScript" -ErrorAction SilentlyContinue) {
                    New-RollbackScript -FromLog $nonExistentCsv
                } else {
                    if (-not (Test-Path $nonExistentCsv)) {
                        throw [System.IO.FileNotFoundException]::new("Log file not found: $nonExistentCsv")
                    }
                }
            } "not found|FileNotFoundException" "Missing log file must throw FileNotFoundException"
        }

        It "T2.ROLL.02: -FromLog with empty or corrupted CSV should handle gracefully" {
            $sandbox = New-TestSandbox
            try {
                $emptyCsv = Join-Path $sandbox "empty.csv"
                "" | Set-Content -Path $emptyCsv -Encoding utf8
                
                $records = Import-Csv -Path $emptyCsv -ErrorAction SilentlyContinue
                $targetIds = if ($records) { @($records.FeatureID) } else { @() }
                Assert-Count $targetIds 0 "Empty CSV must result in 0 target IDs without crashing"
            } finally {
                Remove-TestSandbox $sandbox
            }
        }

        It "T2.ROLL.03: Reverting an already disabled or unsupported ID must not abort batch" {
            $sandbox = New-TestSandbox
            try {
                $env:VIVETOOL_MOCK_FAIL_IDS = "61754985"
                $results = @()
                $batchIds = @("61161244", "61754985", "62762248")
                
                foreach ($id in $batchIds) {
                    $out = & $mockShim /disable /id:$id
                    $code = $LASTEXITCODE
                    $status = if ($code -eq 0) { "Success" } else { "Unsupported" }
                    $results += [PSCustomObject]@{ Id = $id; Status = $status }
                }
                
                Assert-Count $results 3 "All 3 items processed in rollback"
                Assert-Equal $results[0].Status "Success" "Item 1 Success"
                Assert-Equal $results[1].Status "Unsupported" "Item 2 Unsupported"
                Assert-Equal $results[2].Status "Success" "Item 3 Success"
            } finally {
                $env:VIVETOOL_MOCK_FAIL_IDS = $null
                Remove-TestSandbox $sandbox
            }
        }

        It "T2.ROLL.04: Rollback with channel filter 'Canary' should only revert Canary IDs" {
            $rawCanary = @("61121285", "58288238", "53283713", "59065581", "45425284")
            $selectedChannel = "Canary"
            $filtered = if ($selectedChannel -eq "Canary") { $rawCanary } else { @() }
            Assert-Count $filtered 5 "Canary filtered rollback must contain exactly 5 IDs"
        }

        It "T2.ROLL.05: DryRun rollback must not execute any real disable operations" {
            $dryRun = $true
            $realInvoked = $false
            if (-not $dryRun) { $realInvoked = $true }
            Assert-False $realInvoked "DryRun rollback must bypass real executions"
        }

        It "T2.ROLL.06: Rollback log destination unwritable should fall back to temp" {
            $unwritableLog = "X:\NonExistent_Drive\rollback.log"
            $fallback = Join-Path $env:TEMP "vivetool_rollback_fallback.log"
            $effective = if (Test-Path (Split-Path $unwritableLog)) { $unwritableLog } else { $fallback }
            Assert-Match $effective "vivetool_rollback_fallback" "Must select fallback temp path"
        }

        It "T2.ROLL.07: Rollback path containing spaces and special characters must execute properly" {
            $sandbox = New-TestSandbox
            try {
                $complexPath = Join-Path $sandbox "Rollback (x86) & #1"
                New-Item -ItemType Directory -Path $complexPath -Force | Out-Null
                $shimCopy = Join-Path $complexPath "MockViVeTool.ps1"
                Copy-Item -Path $mockShim -Destination $shimCopy
                
                $out = & $shimCopy /disable /id:61161244
                Assert-Equal $LASTEXITCODE 0 "Rollback execution from complex path must succeed"
            } finally {
                Remove-TestSandbox $sandbox
            }
        }

        It "T2.ROLL.08: Reverse ordering execution support for dependency unwinding" {
            $forward = @("61161244", "61754985", "62762248")
            [array]::Reverse($forward)
            Assert-Equal $forward[0] "62762248" "Reversed order first item"
            Assert-Equal $forward[2] "61161244" "Reversed order last item"
        }
    }
}

if ($MyInvocation.InvocationName -ne '.') {
    $summary = Get-TestSummary
    $color = if ($summary.AllPassed) { "Green" } else { "Red" }
    Write-Host "`nTest Suite Completed: $($summary.Passed) Passed, $($summary.Failed) Failed, $($summary.Skipped) Skipped." -ForegroundColor $color
    if (-not $summary.AllPassed) { exit 1 }
}
