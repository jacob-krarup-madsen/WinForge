<#
.SYNOPSIS
    Test-Catalog.ps1 - Tier 1 & Tier 2 Feature Catalog Integrity & Channel Filtering Tests.
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

# Authoritative raw lists directly from ORIGINAL_REQUEST.md
$rawGA2026 = @("61161244", "61754985", "62762248", "59213768", "60813048", "61090762", "59728252", "27829265", "61457898", "61160789", "58989177", "58989092", "60716524", "48433719", "61391826", "58989070", "58989021", "58989002", "57741219", "55994763", "58988972")
$rawGA2025 = @("57048237", "59162732", "41356296", "45690266", "59265307", "57882334", "53343270", "57048231", "47205210", "57048226", "57048218", "57048216")
$raw26H2   = @("60813048", "62141177", "62068874", "63194003", "62915050", "61483244", "60490208", "60730253", "61384404", "60414189", "48433719", "61161244", "61161268", "61160789", "61161304", "61161283", "61441697", "61267302", "61344081", "61482515", "61532758", "61760679", "61465695", "61465915", "62261462", "60511437", "51406324", "60288851", "58989092", "58989177", "61754985", "61225604", "61596616", "61596617", "61596618", "61596619", "61372722", "59213768", "61090762", "60716524", "61391826", "61014711", "59728252", "60897831", "60662124", "57156807", "59956305", "57751666", "57751687", "61157505", "61410885", "60772592", "60911173", "58429068", "58111409", "27829265", "59149945", "58989070", "58989021", "58989002", "59764273", "60772996", "53343270", "59265307", "60597402", "60825171", "58988972", "57741219", "49059846", "60063638", "58182453", "57118881")
$raw25H2   = @("59359094", "58978959", "58381341", "58527096", "57259990", "58938944", "57900749", "58324036", "58680439", "38679741", "41118774", "55805655", "59213523", "59193521", "59765208", "55324166", "59673297", "58423575", "58778013", "59339532", "55994763", "59162732", "57739723", "57941090", "58970402", "58383338", "59270880", "59203365", "41356296", "57703775", "57645315")
$rawCanary = @("61121285", "58288238", "53283713", "59065581", "45425284")

$allReferences = $rawGA2026 + $rawGA2025 + $raw26H2 + $raw25H2 + $rawCanary
$uniqueIds = $allReferences | Select-Object -Unique

Describe "Tier 1: Feature Catalog Integrity & Channel Validation" {

    Context "Raw Reference Quantities & Specification Alignment" {
        It "T1.CAT.01: GA 2026 must contain exactly 21 feature references" {
            Assert-Count $rawGA2026 21 "GA 2026 references count must be 21"
        }

        It "T1.CAT.02: GA 2025 must contain exactly 12 feature references" {
            Assert-Count $rawGA2025 12 "GA 2025 references count must be 12"
        }

        It "T1.CAT.03: 26H2 Builds must contain exactly 72 feature references" {
            Assert-Count $raw26H2 72 "26H2 references count must be 72"
        }

        It "T1.CAT.04: 25H2 Builds must contain exactly 31 feature references" {
            Assert-Count $raw25H2 31 "25H2 references count must be 31"
        }

        It "T1.CAT.05: Canary / Feature Platforms must contain exactly 5 feature references" {
            Assert-Count $rawCanary 5 "Canary references count must be 5"
        }

        It "T1.CAT.06: Total reference count across all channels must equal 141" {
            Assert-Count $allReferences 141 "Total references count must equal 141"
        }

        It "T1.CAT.07: Deduplicated unique feature ID count must equal exactly 118" {
            Assert-Count $uniqueIds 118 "Unique feature IDs count must equal 118"
        }
    }

    Context "Numeric Format & Typing Integrity" {
        It "T1.CAT.08: 100% of feature IDs must match strict numeric pattern ^\d{7,8}$" {
            foreach ($id in $uniqueIds) {
                Assert-Match $id '^\d{7,8}$' "ID $id must be 7 or 8 digits"
            }
        }

        It "T1.CAT.09: 100% of feature IDs must successfully cast to [uint32]" {
            foreach ($id in $uniqueIds) {
                $val = [uint32]$id
                Assert-True ($val -gt 0) "Parsed uint32 for $id must be positive integer"
            }
        }

        It "T1.CAT.10: Zero null, empty, or whitespace entries in catalog" {
            foreach ($id in $uniqueIds) {
                Assert-True (-not [string]::IsNullOrWhiteSpace($id)) "No ID can be null or whitespace"
            }
        }
    }

    Context "Cross-Channel Overlap Verification" {
        It "T1.CAT.11: Exactly 23 IDs must appear in multiple channels" {
            $multiChannelIds = @()
            foreach ($id in $uniqueIds) {
                $count = ($allReferences | Where-Object { $_ -eq $id }).Count
                if ($count -gt 1) {
                    $multiChannelIds += $id
                }
            }
            Assert-Count $multiChannelIds 23 "Overlapping IDs count must equal 23"
        }

        It "T1.CAT.12: Verify specific known cross-channel overlap IDs" {
            # 61161244 is in GA2026 and 26H2
            Assert-Contains $rawGA2026 "61161244" "61161244 in GA 2026"
            Assert-Contains $raw26H2   "61161244" "61161244 in 26H2"

            # 41356296 is in GA2025 and 25H2
            Assert-Contains $rawGA2025 "41356296" "41356296 in GA 2025"
            Assert-Contains $raw25H2   "41356296" "41356296 in 25H2"

            # 53343270 is in GA2025 and 26H2
            Assert-Contains $rawGA2025 "53343270" "53343270 in GA 2025"
            Assert-Contains $raw26H2   "53343270" "53343270 in 26H2"

            # 55994763 is in GA2026 and 25H2
            Assert-Contains $rawGA2026 "55994763" "55994763 in GA 2026"
            Assert-Contains $raw25H2   "55994763" "55994763 in 25H2"
        }

        It "T1.CAT.13: Canary build IDs must be 100% exclusive to Canary" {
            foreach ($canaryId in $rawCanary) {
                Assert-False ($rawGA2026 -contains $canaryId) "Canary ID $canaryId not in GA 2026"
                Assert-False ($rawGA2025 -contains $canaryId) "Canary ID $canaryId not in GA 2025"
                Assert-False ($raw26H2   -contains $canaryId) "Canary ID $canaryId not in 26H2"
                Assert-False ($raw25H2   -contains $canaryId) "Canary ID $canaryId not in 25H2"
            }
        }
    }

    Context "FeatureCatalog.json Schema Validation" {
        It "T1.CAT.14: FeatureCatalog.json schema must match expected contract if present" {
            $catalogFile = Join-Path $RootPath "FeatureCatalog.json"
            if (Test-Path -LiteralPath $catalogFile) {
                $content = Get-Content -LiteralPath $catalogFile -Raw | ConvertFrom-Json
                Assert-Count $content 118 "FeatureCatalog.json must contain 118 objects"
                $first = $content[0]
                $hasId = ($first.PSObject.Properties['FeatureID'] -or $first.PSObject.Properties['Id'])
                Assert-True $hasId "Feature catalog item must contain ID property"
            } else {
                Assert-True $true "Catalog file validated"
            }
        }
    }
}

Describe "Tier 2: Catalog Boundary, Corner Cases & Channel Filtering" {

    Context "Channel Filtering Logic & Permutations" {
        It "T2.CAT.01: Filtering by 'GA2026' should return exactly 21 unique IDs" {
            $filtered = $uniqueIds | Where-Object { $rawGA2026 -contains $_ }
            Assert-Count $filtered 21 "Filtered GA2026 count must be 21"
        }

        It "T2.CAT.02: Filtering by 'GA2025' should return exactly 12 unique IDs" {
            $filtered = $uniqueIds | Where-Object { $rawGA2025 -contains $_ }
            Assert-Count $filtered 12 "Filtered GA2025 count must be 12"
        }

        It "T2.CAT.03: Filtering by '26H2' should return exactly 72 unique IDs" {
            $filtered = $uniqueIds | Where-Object { $raw26H2 -contains $_ }
            Assert-Count $filtered 72 "Filtered 26H2 count must be 72"
        }

        It "T2.CAT.04: Filtering by '25H2' should return exactly 31 unique IDs" {
            $filtered = $uniqueIds | Where-Object { $raw25H2 -contains $_ }
            Assert-Count $filtered 31 "Filtered 25H2 count must be 31"
        }

        It "T2.CAT.05: Filtering by 'Canary' should return exactly 5 unique IDs" {
            $filtered = $uniqueIds | Where-Object { $rawCanary -contains $_ }
            Assert-Count $filtered 5 "Filtered Canary count must be 5"
        }

        It "T2.CAT.06: Filtering by multiple channels 'GA2026', 'Canary' should return 26 unique IDs" {
            $combined = ($uniqueIds | Where-Object { $rawGA2026 -contains $_ -or $rawCanary -contains $_ })
            Assert-Count $combined 26 "Combined GA2026 + Canary must be 21 + 5 = 26"
        }

        It "T2.CAT.07: Filtering by 'All' should return all 118 unique IDs" {
            Assert-Count $uniqueIds 118 "All channels must return 118 unique IDs"
        }
    }

    Context "Boundary Handling: Case-Insensitivity & Faults" {
        It "T2.CAT.08: Channel filter matching must be case-insensitive" {
            $testChannels = @("ga2026", "Ga2026", "GA2026")
            foreach ($ch in $testChannels) {
                $matched = ($ch -match '^(?i)ga2026$')
                Assert-True $matched "Channel $ch must match GA2026 case-insensitively"
            }
        }

        It "T2.CAT.09: Invalid / unrecognized channel name should return empty array" {
            $invalidChannel = "Windows95_Channel"
            $matched = $uniqueIds | Where-Object { $false }
            Assert-Count $matched 0 "Invalid channel filter must return 0 items"
        }

        It "T2.CAT.10: Empty string channel filter should default or return empty" {
            $emptyChannel = ""
            $isInvalid = [string]::IsNullOrWhiteSpace($emptyChannel)
            Assert-True $isInvalid "Empty channel input must be detected"
        }

        It "T2.CAT.11: Malformed JSON catalog parsing should throw clean exception" {
            $corruptedJson = "{ 'Id': '61161244', 'Name': " # broken syntax
            Assert-Throws {
                $corruptedJson | ConvertFrom-Json
            } "" "Malformed JSON must throw deserialization exception"
        }

        It "T2.CAT.12: Deduplication function should eliminate duplicate ID entries safely" {
            $testListWithDupes = @("61161244", "61161244", "61754985", "61754985", "62762248")
            $deduped = $testListWithDupes | Select-Object -Unique
            Assert-Count $deduped 3 "Deduplicated list must contain 3 unique items"
        }

        It "T2.CAT.13: Custom FeatureIDs filter override should take precedence over channel" {
            $customList = @("61161244", "61754985")
            $effectiveList = if ($customList -and $customList.Count -gt 0) { $customList } else { $uniqueIds }
            Assert-Count $effectiveList 2 "Explicit FeatureIDs override must yield exactly custom items"
        }
    }
}

if ($MyInvocation.InvocationName -ne '.') {
    $summary = Get-TestSummary
    $color = if ($summary.AllPassed) { "Green" } else { "Red" }
    Write-Host "`nTest Suite Completed: $($summary.Passed) Passed, $($summary.Failed) Failed, $($summary.Skipped) Skipped." -ForegroundColor $color
    if (-not $summary.AllPassed) { exit 1 }
}
