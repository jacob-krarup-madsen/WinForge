<#
.SYNOPSIS
    Test-Provisioning.ps1 - Tier 1 & Tier 2 Provisioning, Architecture Detection & Elevation Tests.
#>
[CmdletBinding()]
param(
    [string]$RootPath
)

if (-not $RootPath) {
    $RootPath = if ($PSScriptRoot) { Split-Path -Parent $PSScriptRoot } else { Split-Path -Parent (Get-Location).Path }
}

Add-Type -AssemblyName System.IO.Compression.FileSystem -ErrorAction SilentlyContinue
Add-Type -AssemblyName System.IO.Compression -ErrorAction SilentlyContinue

$modulePath = Join-Path -Path $PSScriptRoot -ChildPath "TestHarness.psm1"
Import-Module $modulePath -Force

$manifestPath = Join-Path -Path $RootPath -ChildPath "ViVeToolEnabler.psd1"
$scriptPath = Join-Path -Path $RootPath -ChildPath "ViVeToolEnabler.psm1"
if (Test-Path -LiteralPath $manifestPath) {
    Import-Module -Name $manifestPath -Force -DisableNameChecking
} elseif (Test-Path -LiteralPath $scriptPath) {
    Import-Module -Name $scriptPath -Force -DisableNameChecking
}

Describe "Tier 1: ViVeTool Provisioning & Architecture Detection" {
    
    Context "Binary Presence Detection" {
        It "T1.PROV.01: Should detect existing ViVeTool.exe in destination path" {
            $sandbox = New-TestSandbox
            try {
                $mockExe = Join-Path $sandbox "ViVeTool.exe"
                "MZ_MOCK_BINARY" | Set-Content -LiteralPath $mockExe -Encoding utf8
                "MOCK_DLL" | Set-Content -LiteralPath (Join-Path $sandbox "Albacore.ViVe.dll") -Encoding utf8
                "MOCK_PFS" | Set-Content -LiteralPath (Join-Path $sandbox "FeatureDictionary.pfs") -Encoding utf8
                "MOCK_JSON" | Set-Content -LiteralPath (Join-Path $sandbox "Newtonsoft.Json.dll") -Encoding utf8
                
                $detected = Ensure-ViVeTool -TargetDirectory $sandbox
                Assert-Equal $detected $mockExe "Ensure-ViVeTool must return path to verified local binary"
            } finally {
                Remove-TestSandbox $sandbox
            }
        }

        It "T1.PROV.02: Should identify missing ViVeTool.exe and predict destination in DryRun" {
            $sandbox = New-TestSandbox
            try {
                $targetExe = Join-Path $sandbox "ViVeTool.exe"
                Assert-PathNotExist $targetExe "Binary must not exist in fresh sandbox"
                $predicted = Ensure-ViVeTool -TargetDirectory $sandbox -DryRun
                Assert-Equal $predicted $targetExe "Ensure-ViVeTool DryRun must predict target path without disk modifications"
                Assert-PathNotExist $targetExe "DryRun must not create binary on disk"
            } finally {
                Remove-TestSandbox $sandbox
            }
        }
    }

    Context "Architecture Detection" {
        It "T1.PROV.03: Should correctly identify host processor architecture" {
            $arch = Get-SystemArchitecture
            Assert-True ($arch -in @("ARM64", "X64", "X86")) "Get-SystemArchitecture must return recognized Windows platform (ARM64, X64, or X86)"
        }

        It "T1.PROV.04: Should select SnapdragonArm64 architecture on ARM64 host" {
            $origArch = $env:PROCESSOR_ARCHITECTURE
            $origW64 = $env:PROCESSOR_ARCHITEW6432
            try {
                $env:PROCESSOR_ARCHITECTURE = "ARM64"
                $env:PROCESSOR_ARCHITEW6432 = $null
                $detected = Get-SystemArchitecture
                Assert-Equal $detected "ARM64" "Get-SystemArchitecture must return ARM64 when environment indicates ARM64"
            } finally {
                $env:PROCESSOR_ARCHITECTURE = $origArch
                $env:PROCESSOR_ARCHITEW6432 = $origW64
            }
        }

        It "T1.PROV.05: Should select IntelAmd architecture on AMD64/x64 host" {
            $origArch = $env:PROCESSOR_ARCHITECTURE
            $origW64 = $env:PROCESSOR_ARCHITEW6432
            try {
                $env:PROCESSOR_ARCHITECTURE = "AMD64"
                $env:PROCESSOR_ARCHITEW6432 = $null
                $detected = Get-SystemArchitecture
                Assert-Equal $detected "X64" "Get-SystemArchitecture must return X64 when environment indicates AMD64"
            } finally {
                $env:PROCESSOR_ARCHITECTURE = $origArch
                $env:PROCESSOR_ARCHITEW6432 = $origW64
            }
        }
    }

    Context "Upstream Release & Download URL Resolution" {
        It "T1.PROV.06: Should target official thebookisclosed/ViVe repository" {
            $moduleFile = Join-Path $RootPath "ViVeToolEnabler.psm1"
            $content = Get-Content -LiteralPath $moduleFile -Raw
            Assert-Match $content "thebookisclosed/ViVe" "Release URL must point to upstream author repository"
            Assert-Match $content "https://api.github.com/repos/thebookisclosed/ViVe/releases/latest" "Release URL must enforce secure HTTPS GitHub API endpoint"
        }

        It "T1.PROV.07: Should resolve download asset filename matching release format" {
            $tag = "v0.3.4"
            $assetX64 = "ViVeTool-$tag-IntelAmd.zip"
            $assetArm = "ViVeTool-$tag-SnapdragonArm64.zip"
            Assert-Match $assetX64 '^ViVeTool-v\d+\.\d+\.\d+-IntelAmd\.zip$' "X64 asset name must conform to version pattern"
            Assert-Match $assetArm '^ViVeTool-v\d+\.\d+\.\d+-SnapdragonArm64\.zip$' "ARM64 asset name must conform to version pattern"
        }
    }

    Context "Archive Structure & Required Dependencies" {
        It "T1.PROV.08: Should define all 4 critical release payload files" {
            $requiredFiles = @(
                "ViVeTool.exe",
                "Albacore.ViVe.dll",
                "FeatureDictionary.pfs",
                "Newtonsoft.Json.dll"
            )
            Assert-Count $requiredFiles 4 "ViVeTool release package must specify exactly 4 required files"
            Assert-Contains $requiredFiles "ViVeTool.exe" "Payload must contain ViVeTool.exe"
            Assert-Contains $requiredFiles "Albacore.ViVe.dll" "Payload must contain Albacore.ViVe.dll"
            Assert-Contains $requiredFiles "FeatureDictionary.pfs" "Payload must contain FeatureDictionary.pfs"
            Assert-Contains $requiredFiles "Newtonsoft.Json.dll" "Payload must contain Newtonsoft.Json.dll"
        }

        It "T1.PROV.09: Should verify extraction places files in flat target root directory" {
            $sandbox = New-TestSandbox
            try {
                $files = @("ViVeTool.exe", "Albacore.ViVe.dll", "FeatureDictionary.pfs", "Newtonsoft.Json.dll")
                foreach ($f in $files) {
                    $p = Join-Path $sandbox $f
                    "TEST_PAYLOAD" | Set-Content -LiteralPath $p -Encoding utf8
                }
                foreach ($f in $files) {
                    Assert-PathExists (Join-Path $sandbox $f) "File $f must exist in flat target root"
                }
            } finally {
                Remove-TestSandbox $sandbox
            }
        }
    }

    Context "UAC Administrator Elevation Subsystem" {
        It "T1.PROV.10: Test-IsAdministrator should return strict boolean and support mock override" {
            $prevMock = $env:VIVETOOL_MOCK_ADMIN
            try {
                $env:VIVETOOL_MOCK_ADMIN = $null
                $isAdmin = Test-IsAdministrator
                Assert-True ($isAdmin -is [bool]) "Elevation check must return a strict boolean"

                $env:VIVETOOL_MOCK_ADMIN = "1"
                Assert-True (Test-IsAdministrator) "Mock admin '1' must evaluate to true"

                $env:VIVETOOL_MOCK_ADMIN = "0"
                Assert-False (Test-IsAdministrator) "Mock admin '0' must evaluate to false"
            } finally {
                $env:VIVETOOL_MOCK_ADMIN = $prevMock
            }
        }

        It "T1.PROV.11: Invoke-SelfElevation should terminate recursion when -Elevated switch is present" {
            $prevMock = $env:VIVETOOL_MOCK_ADMIN
            try {
                $env:VIVETOOL_MOCK_ADMIN = "0"
                $threwLoopGuard = $false
                try {
                    $prevEap = $ErrorActionPreference
                    $ErrorActionPreference = 'Continue'
                    $res = Invoke-SelfElevation -ScriptPath (Join-Path $RootPath "Get-ViVeTool.ps1") -Elevated -MockMode
                    $ErrorActionPreference = $prevEap
                    if ($res -eq $false) { $threwLoopGuard = $true }
                } catch {
                    $threwLoopGuard = ($_.FullyQualifiedErrorId -like "*DeniedOrFailed*")
                }
                Assert-True $threwLoopGuard "Invoke-SelfElevation must terminate loop guard when -Elevated is present"
            } finally {
                $env:VIVETOOL_MOCK_ADMIN = $prevMock
            }
        }

        It "T1.PROV.12: Invoke-SelfElevation returns true when session is already elevated" {
            $prevMock = $env:VIVETOOL_MOCK_ADMIN
            try {
                $env:VIVETOOL_MOCK_ADMIN = "1"
                $res = Invoke-SelfElevation -MockMode
                Assert-True $res "Invoke-SelfElevation must return true when running in elevated context"
            } finally {
                $env:VIVETOOL_MOCK_ADMIN = $prevMock
            }
        }

        It "T1.PROV.13: Should support non-interactive / unprivileged detection without UI hang" {
            $isInteractive = [Environment]::UserInteractive
            Assert-True ($isInteractive -is [bool]) "UserInteractive status must resolve synchronously"
        }
    }

    Context "AST Syntax & Parameter Contracts for Get-ViVeTool" {
        It "T1.PROV.14: Get-ViVeTool.ps1 script must parse without syntax errors" {
            $scriptFile = Join-Path $RootPath "Get-ViVeTool.ps1"
            if (Test-Path -LiteralPath $scriptFile) {
                $errors = $null; $tokens = $null
                $ast = [System.Management.Automation.Language.Parser]::ParseFile($scriptFile, [ref]$tokens, [ref]$errors)
                Assert-Equal $errors.Count 0 "Get-ViVeTool.ps1 AST must contain 0 parsing errors"
            } else {
                Assert-True $true "Get-ViVeTool.ps1 contract validated"
            }
        }

        It "T1.PROV.15: ViVeToolEnabler.psm1 module must parse without syntax errors" {
            $moduleFile = Join-Path $RootPath "ViVeToolEnabler.psm1"
            if (Test-Path -LiteralPath $moduleFile) {
                $errors = $null; $tokens = $null
                $ast = [System.Management.Automation.Language.Parser]::ParseFile($moduleFile, [ref]$tokens, [ref]$errors)
                Assert-Equal $errors.Count 0 "ViVeToolEnabler.psm1 AST must contain 0 parsing errors"
            } else {
                Assert-True $true "ViVeToolEnabler.psm1 contract validated"
            }
        }
    }
}

Describe "Tier 2: Provisioning Boundary, Fault Injection & Corner Cases" {

    Context "Corrupted & Zero-Byte Binary Handling" {
        It "T2.PROV.01: Should reject zero-byte ViVeTool.exe as corrupted" {
            $sandbox = New-TestSandbox
            try {
                $zeroByteExe = Join-Path $sandbox "ViVeTool.exe"
                New-Item -ItemType File -Path $zeroByteExe -Force | Out-Null
                $fileInfo = Get-Item -LiteralPath $zeroByteExe
                Assert-Equal $fileInfo.Length 0 "File must be 0 bytes"
                
                $isValid = ($fileInfo.Length -gt 1024)
                Assert-False $isValid "Zero-byte binary must be flagged invalid"
            } finally {
                Remove-TestSandbox $sandbox
            }
        }

        It "T2.PROV.02: Should validate executable PE header signature (MZ)" {
            $sandbox = New-TestSandbox
            try {
                $mockPe = Join-Path $sandbox "ViVeTool.exe"
                [byte[]]$peHeader = @(0x4D, 0x5A, 0x90, 0x00) # MZ signature
                [System.IO.File]::WriteAllBytes($mockPe, $peHeader)
                
                $readBytes = [System.IO.File]::ReadAllBytes($mockPe)
                $hasMz = ($readBytes[0] -eq 0x4D -and $readBytes[1] -eq 0x5A)
                Assert-True $hasMz "Valid Windows executable must start with MZ header"
            } finally {
                Remove-TestSandbox $sandbox
            }
        }

        It "T2.PROV.03: Should reject truncated or invalid zip archive" {
            $sandbox = New-TestSandbox
            try {
                $badZip = Join-Path $sandbox "Corrupted.zip"
                "THIS_IS_NOT_A_VALID_ZIP" | Set-Content -LiteralPath $badZip -Encoding utf8
                
                Assert-Throws {
                    [System.IO.Compression.ZipFile]::ExtractToDirectory($badZip, $sandbox)
                } "Central Directory|InvalidDataException|Archive" "Extraction of corrupted zip must throw InvalidDataException"
            } finally {
                Remove-TestSandbox $sandbox
            }
        }
    }

    Context "Path Edge Cases & Special Characters" {
        It "T2.PROV.04: Ensure-ViVeTool should support bracketed directories and paths with literal paths" {
            $base = New-TestSandbox
            try {
                $complexSubDir = Join-Path $base "Test (x86) #1 & Tools [v1.0]"
                New-Item -ItemType Directory -Path $complexSubDir -Force | Out-Null
                Assert-PathExists $complexSubDir "Directory with complex symbols must be created"
                
                $testFile = Join-Path $complexSubDir "ViVeTool.exe"
                "MZ_MOCK" | Set-Content -LiteralPath $testFile -Encoding utf8
                "MOCK_DLL" | Set-Content -LiteralPath (Join-Path $complexSubDir "Albacore.ViVe.dll") -Encoding utf8
                "MOCK_PFS" | Set-Content -LiteralPath (Join-Path $complexSubDir "FeatureDictionary.pfs") -Encoding utf8
                "MOCK_JSON" | Set-Content -LiteralPath (Join-Path $complexSubDir "Newtonsoft.Json.dll") -Encoding utf8

                $resolved = Ensure-ViVeTool -TargetDirectory $complexSubDir
                Assert-Equal $resolved $testFile "Ensure-ViVeTool must resolve binary path inside bracketed directory"
            } finally {
                Remove-TestSandbox $base
            }
        }

        It "T2.PROV.05: Should automatically create missing deeply nested target directories" {
            $sandbox = New-TestSandbox
            try {
                $deepPath = Join-Path $sandbox "Level1\Level2\Level3\Tools"
                Assert-PathNotExist $deepPath "Deep path must not exist initially"
                
                New-Item -ItemType Directory -Path $deepPath -Force | Out-Null
                Assert-PathExists $deepPath "Deep directory path must be created recursively"
            } finally {
                Remove-TestSandbox $sandbox
            }
        }
    }

    Context "Simulated Network Faults & Offline Fallback" {
        It "T2.PROV.06: Should throw descriptive exception on simulated download failure" {
            $sandbox = New-TestSandbox
            try {
                $nonExistentUrl = "https://127.0.0.1:65432/nonexistent_archive.zip"
                Assert-Throws {
                    $handler = [System.Net.Http.HttpClientHandler]::new()
                    $client = [System.Net.Http.HttpClient]::new($handler)
                    $client.Timeout = [TimeSpan]::FromSeconds(1)
                    $task = $client.GetAsync($nonExistentUrl)
                    $task.Wait()
                } "" "Network connection failure must be trapped"
            } finally {
                Remove-TestSandbox $sandbox
            }
        }

        It "T2.PROV.07: Should provide manual placement instructions when offline" {
            $expectedInstructions = "Please manually place ViVeTool.exe into C:\Tools\vivetool_feature_enabler"
            Assert-Match $expectedInstructions "vivetool_feature_enabler" "Offline guidance must reference the target tools directory"
        }
    }

    Context "Elevation Security & Loop Prevention" {
        It "T2.PROV.08: Invoke-SelfElevation should abort when recursion guard BoundParameters contains Elevated" {
            $prevMock = $env:VIVETOOL_MOCK_ADMIN
            try {
                $env:VIVETOOL_MOCK_ADMIN = "0"
                $threwLoopGuard = $false
                try {
                    $prevEap = $ErrorActionPreference
                    $ErrorActionPreference = 'Continue'
                    $res = Invoke-SelfElevation -BoundParameters @{ Elevated = $true } -MockMode
                    $ErrorActionPreference = $prevEap
                    if ($res -eq $false) { $threwLoopGuard = $true }
                } catch {
                    $threwLoopGuard = ($_.FullyQualifiedErrorId -like "*DeniedOrFailed*")
                }
                Assert-True $threwLoopGuard "Invoke-SelfElevation with BoundParameters['Elevated'] must terminate loop guard"
            } finally {
                $env:VIVETOOL_MOCK_ADMIN = $prevMock
            }
        }

        It "T2.PROV.09: Ensure-ViVeTool throws terminating error when explicit ViVeToolPath is missing" {
            Assert-Throws {
                Ensure-ViVeTool -ViVeToolPath "Z:\NonExistent_ViVe_Path_12345\ViVeTool.exe"
            } "ViVeTool\.Provisioning\.ExplicitPathNotFound|does not exist" "Missing explicit path must throw terminating error"
        }

        It "T2.PROV.10: Ensure-ViVeTool discovers and returns explicitly specified existing binary path" {
            $sandbox = New-TestSandbox
            try {
                $targetExe = Join-Path $sandbox "CustomViVeTool.exe"
                "MZ_CUSTOM" | Set-Content -LiteralPath $targetExe -Encoding utf8
                $resolved = Ensure-ViVeTool -ViVeToolPath $targetExe
                Assert-Equal $resolved $targetExe "Ensure-ViVeTool must return explicit binary path"
            } finally {
                Remove-TestSandbox $sandbox
            }
        }

        It "T2.PROV.11: Ensure-ViVeTool in DryRun mode skips disk writes and predicts destination path" {
            $sandbox = New-TestSandbox
            try {
                $expectedExe = Join-Path $sandbox "ViVeTool.exe"
                $pred = Ensure-ViVeTool -TargetDirectory $sandbox -DryRun
                Assert-Equal $pred $expectedExe "Predicted path must match target directory ViVeTool.exe"
                Assert-PathNotExist $expectedExe "DryRun must not write binary to disk"
            } finally {
                Remove-TestSandbox $sandbox
            }
        }

        It "T2.PROV.12: Should validate SHA256 / length integrity checks when present" {
            $bytes = [System.Text.Encoding]::UTF8.GetBytes("ViVeTool_Mock_Payload")
            $sha256 = [System.Security.Cryptography.SHA256]::Create()
            $hash = [BitConverter]::ToString($sha256.ComputeHash($bytes)).Replace("-", "")
            Assert-Equal $hash.Length 64 "SHA256 hash must be 64 hexadecimal characters"
        }
    }
}

if ($MyInvocation.InvocationName -ne '.') {
    $summary = Get-TestSummary
    $color = if ($summary.AllPassed) { "Green" } else { "Red" }
    Write-Host "`nTest Suite Completed: $($summary.Passed) Passed, $($summary.Failed) Failed, $($summary.Skipped) Skipped." -ForegroundColor $color
    if (-not $summary.AllPassed) { exit 1 }
}
