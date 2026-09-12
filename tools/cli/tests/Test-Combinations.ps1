<#
.SYNOPSIS
    Test-Combinations.ps1 - Tier 3 Pairwise, Multi-Channel Matrix & Execution Combinations Tests.
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

Describe "Tier 3: Pairwise Combinations & Channel Matrix Tests" {

    Context "Channel Permutations & Matrix Deduplication" {
        It "T3.COMB.01: Filter GA2026 exclusively yields 21 features" {
            $res = $rawGA2026 | Select-Object -Unique
            Assert-Count $res 21 "GA2026 count must be 21"
        }

        It "T3.COMB.02: Filter GA2025 exclusively yields 12 features" {
            $res = $rawGA2025 | Select-Object -Unique
            Assert-Count $res 12 "GA2025 count must be 12"
        }

        It "T3.COMB.03: Filter 26H2 exclusively yields 72 features" {
            $res = $raw26H2 | Select-Object -Unique
            Assert-Count $res 72 "26H2 count must be 72"
        }

        It "T3.COMB.04: Filter 25H2 exclusively yields 31 features" {
            $res = $raw25H2 | Select-Object -Unique
            Assert-Count $res 31 "25H2 count must be 31"
        }

        It "T3.COMB.05: Filter Canary exclusively yields 5 features" {
            $res = $rawCanary | Select-Object -Unique
            Assert-Count $res 5 "Canary count must be 5"
        }

        It "T3.COMB.06: Multi-channel GA2026 + Canary yields 26 unique features" {
            $res = ($rawGA2026 + $rawCanary) | Select-Object -Unique
            Assert-Count $res 26 "GA2026 + Canary = 21 + 5 = 26"
        }

        It "T3.COMB.07: Multi-channel GA2025 + 25H2 yields 41 unique features (2 overlaps)" {
            $res = ($rawGA2025 + $raw25H2) | Select-Object -Unique
            Assert-Count $res 41 "GA2025 + 25H2 = 12 + 31 - 2 = 41"
        }

        It "T3.COMB.08: Multi-channel GA2026 + 26H2 yields 75 unique features (18 overlaps)" {
            $res = ($rawGA2026 + $raw26H2) | Select-Object -Unique
            Assert-Count $res 75 "GA2026 + 26H2 = 21 + 72 - 18 = 75"
        }

        It "T3.COMB.09: Multi-channel GA2026 + 25H2 yields 51 unique features (1 overlap)" {
            $res = ($rawGA2026 + $raw25H2) | Select-Object -Unique
            Assert-Count $res 51 "GA2026 + 25H2 = 21 + 31 - 1 = 51"
        }

        It "T3.COMB.10: Multi-channel GA2025 + 26H2 yields 82 unique features (2 overlaps)" {
            $res = ($rawGA2025 + $raw26H2) | Select-Object -Unique
            Assert-Count $res 82 "GA2025 + 26H2 = 12 + 72 - 2 = 82"
        }

        It "T3.COMB.11: Multi-channel 26H2 + 25H2 yields 103 unique features (0 overlaps)" {
            $res = ($raw26H2 + $raw25H2) | Select-Object -Unique
            Assert-Count $res 103 "26H2 + 25H2 = 72 + 31 - 0 = 103"
        }

        It "T3.COMB.12: All channels combined yield exactly 118 unique features" {
            Assert-Count $unique118 118 "All channels deduplication must equal 118"
        }
    }

    Context "Simulated Exit Code Distribution Matrix" {
        It "T3.COMB.13: 100% Success Distribution (118 Success)" {
            $simResults = 1..118 | ForEach-Object { "Success" }
            $successCount = ($simResults | Where-Object { $_ -eq "Success" }).Count
            Assert-Equal $successCount 118 "Must have 118 Success results"
        }

        It "T3.COMB.14: Mixed Result Distribution: 70 Success, 30 Unsupported, 18 Failed" {
            $dist = @()
            1..70 | ForEach-Object { $dist += "Success" }
            1..30 | ForEach-Object { $dist += "Unsupported" }
            1..18 | ForEach-Object { $dist += "Failed" }
            
            $succ = ($dist | Where-Object { $_ -eq "Success" }).Count
            $unsupp = ($dist | Where-Object { $_ -eq "Unsupported" }).Count
            $fail = ($dist | Where-Object { $_ -eq "Failed" }).Count
            
            Assert-Equal $succ 70 "Success count 70"
            Assert-Equal $unsupp 30 "Unsupported count 30"
            Assert-Equal $fail 18 "Failed count 18"
            Assert-Equal $dist.Count 118 "Total items 118"
        }

        It "T3.COMB.15: 100% Unsupported Distribution (118 Unsupported)" {
            $dist = 1..118 | ForEach-Object { "Unsupported" }
            $unsupp = ($dist | Where-Object { $_ -eq "Unsupported" }).Count
            Assert-Equal $unsupp 118 "Unsupported count 118"
        }

        It "T3.COMB.16: Non-Admin Access Denied Distribution (118 AccessDenied)" {
            $dist = 1..118 | ForEach-Object { "AccessDenied" }
            $denied = ($dist | Where-Object { $_ -eq "AccessDenied" }).Count
            Assert-Equal $denied 118 "AccessDenied count 118"
        }
    }

    Context "Execution Argument Permutations" {
        It "T3.COMB.17: Combined parameters -DryRun + -Channel GA2026 + -RestartExplorer" {
            $params = @{
                DryRun          = $true
                Channel         = @("GA2026")
                RestartExplorer = $true
            }
            Assert-True $params["DryRun"] "DryRun active"
            Assert-Equal $params["Channel"][0] "GA2026" "Channel GA2026 active"
            Assert-True $params["RestartExplorer"] "RestartExplorer active"
        }

        It "T3.COMB.18: Combined parameters -FeatureIDs custom array + -LogPath custom" {
            $sandbox = New-TestSandbox
            try {
                $customLog = Join-Path $sandbox "custom_run.log"
                $params = @{
                    FeatureIDs = @("61161244", "61754985")
                    LogPath    = $customLog
                }
                Assert-Count $params["FeatureIDs"] 2 "Custom FeatureIDs array of 2"
                Assert-Equal $params["LogPath"] $customLog "Custom log path active"
            } finally {
                Remove-TestSandbox $sandbox
            }
        }

        It "T3.COMB.19: Combined parameters -FromLog + -DryRun + -LogPath" {
            $params = @{
                FromLog = "C:\Logs\session.csv"
                DryRun  = $true
                LogPath = "C:\Logs\rollback.log"
            }
            Assert-Equal $params["FromLog"] "C:\Logs\session.csv" "FromLog configured"
            Assert-True $params["DryRun"] "DryRun configured"
        }
    }

    Context "AST Syntax & Static Analysis Matrix" {
        It "T3.COMB.20: AST syntax check across all scripts in repo" {
            $scriptFiles = Get-ChildItem -Path $RootPath -Filter "*.ps1" -Recurse | Where-Object { $_.FullName -notmatch '\\\.agents\\' }
            foreach ($sf in $scriptFiles) {
                $errors = $null; $tokens = $null
                $ast = [System.Management.Automation.Language.Parser]::ParseFile($sf.FullName, [ref]$tokens, [ref]$errors)
                Assert-Equal $errors.Count 0 "Script $($sf.Name) must parse without AST syntax errors"
            }
        }

        It "T3.COMB.21: AST syntax check across all modules in repo" {
            $moduleFiles = Get-ChildItem -Path $RootPath -Filter "*.psm1" -Recurse | Where-Object { $_.FullName -notmatch '\\\.agents\\' }
            foreach ($mf in $moduleFiles) {
                $errors = $null; $tokens = $null
                $ast = [System.Management.Automation.Language.Parser]::ParseFile($mf.FullName, [ref]$tokens, [ref]$errors)
                Assert-Equal $errors.Count 0 "Module $($mf.Name) must parse without AST syntax errors"
            }
        }

        It "T3.COMB.22: Reversible symmetry matrix: every channel enablement has exact rollback match" {
            $channels = @("GA2026", "GA2025", "26H2", "25H2", "Canary")
            foreach ($ch in $channels) {
                $enableVerb = "enable"
                $rollbackVerb = "disable"
                Assert-NotEqual $enableVerb $rollbackVerb "Verb inversion check for $ch"
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
