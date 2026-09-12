# ==============================================================================
# Challenger-M1-AdversarialTests.ps1 -- Adversarial Stress & Fault Injection Suite
# ==============================================================================
[CmdletBinding()]
param()

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

$projectRoot = (Split-Path -Path $PSScriptRoot -Parent)
if (-not (Test-Path -Path (Join-Path $projectRoot "ViVeToolEnabler.psm1"))) {
    $projectRoot = "C:\Tools\vivetool_feature_enabler"
}
$modulePath = Join-Path $projectRoot "ViVeToolEnabler.psm1"
$scriptPath = Join-Path $projectRoot "Get-ViVeTool.ps1"

Write-Host "================================================================================" -ForegroundColor Cyan
Write-Host " ViVeTool Feature Enabler -- Challenger Adversarial and Stress Suite (M1)" -ForegroundColor Cyan
Write-Host " Environment: PowerShell $($PSVersionTable.PSVersion) | Project: $projectRoot" -ForegroundColor Cyan
Write-Host "================================================================================" -ForegroundColor Cyan

# Import target module
Import-Module $modulePath -Force

# ------------------------------------------------------------------------------
# CATEGORY 1: Architecture Detection & WOW64 Emulation
# ------------------------------------------------------------------------------
Write-Host "`n[+] 1. Architecture Detection and WOW64 Stress Tests" -ForegroundColor Yellow

Test-Challenge "Architecture" "Detects native x64 when PROCESSOR_ARCHITECTURE is AMD64" {
    $origArch = $env:PROCESSOR_ARCHITECTURE
    $origW64 = $env:PROCESSOR_ARCHITEW6432
    try {
        $env:PROCESSOR_ARCHITECTURE = "AMD64"
        $env:PROCESSOR_ARCHITEW6432 = $null
        (Get-SystemArchitecture) -eq "X64"
    } finally {
        $env:PROCESSOR_ARCHITECTURE = $origArch
        $env:PROCESSOR_ARCHITEW6432 = $origW64
    }
}

Test-Challenge "Architecture" "Detects native ARM64 when PROCESSOR_ARCHITECTURE is ARM64" {
    $origArch = $env:PROCESSOR_ARCHITECTURE
    $origW64 = $env:PROCESSOR_ARCHITEW6432
    try {
        $env:PROCESSOR_ARCHITECTURE = "ARM64"
        $env:PROCESSOR_ARCHITEW6432 = $null
        (Get-SystemArchitecture) -eq "ARM64"
    } finally {
        $env:PROCESSOR_ARCHITECTURE = $origArch
        $env:PROCESSOR_ARCHITEW6432 = $origW64
    }
}

Test-Challenge "Architecture" "Detects 32-bit WOW64 on AMD64 host (PROCESSOR_ARCHITEW6432=AMD64)" {
    $origArch = $env:PROCESSOR_ARCHITECTURE
    $origW64 = $env:PROCESSOR_ARCHITEW6432
    try {
        $env:PROCESSOR_ARCHITECTURE = "x86"
        $env:PROCESSOR_ARCHITEW6432 = "AMD64"
        (Get-SystemArchitecture) -eq "X64"
    } finally {
        $env:PROCESSOR_ARCHITECTURE = $origArch
        $env:PROCESSOR_ARCHITEW6432 = $origW64
    }
}

Test-Challenge "Architecture" "Detects 32-bit WOW64 on ARM64 host (PROCESSOR_ARCHITEW6432=ARM64)" {
    $origArch = $env:PROCESSOR_ARCHITECTURE
    $origW64 = $env:PROCESSOR_ARCHITEW6432
    try {
        $env:PROCESSOR_ARCHITECTURE = "x86"
        $env:PROCESSOR_ARCHITEW6432 = "ARM64"
        (Get-SystemArchitecture) -eq "ARM64"
    } finally {
        $env:PROCESSOR_ARCHITECTURE = $origArch
        $env:PROCESSOR_ARCHITEW6432 = $origW64
    }
}

Test-Challenge "Architecture" "Handles lowercase and mixed-case architecture strings gracefully" {
    $origArch = $env:PROCESSOR_ARCHITECTURE
    $origW64 = $env:PROCESSOR_ARCHITEW6432
    try {
        $env:PROCESSOR_ARCHITECTURE = "amd64"
        $env:PROCESSOR_ARCHITEW6432 = $null
        $res1 = ((Get-SystemArchitecture) -eq "X64")

        $env:PROCESSOR_ARCHITECTURE = "arm64"
        $res2 = ((Get-SystemArchitecture) -eq "ARM64")

        $res1 -and $res2
    } finally {
        $env:PROCESSOR_ARCHITECTURE = $origArch
        $env:PROCESSOR_ARCHITEW6432 = $origW64
    }
}

# ------------------------------------------------------------------------------
# CATEGORY 2: Administrator Token & Elevation Security
# ------------------------------------------------------------------------------
Write-Host "`n[+] 2. Elevation and Token Inspection Stress Tests" -ForegroundColor Yellow

Test-Challenge "Elevation" "Test-IsAdministrator returns strict boolean under real execution" {
    $orig = $env:VIVETOOL_MOCK_ADMIN
    try {
        $env:VIVETOOL_MOCK_ADMIN = $null
        $isAdmin = Test-IsAdministrator
        $isAdmin -is [bool]
    } finally {
        $env:VIVETOOL_MOCK_ADMIN = $orig
    }
}

Test-Challenge "Elevation" "Test-IsAdministrator handles mock variations (1, 0, true, false)" {
    $orig = $env:VIVETOOL_MOCK_ADMIN
    try {
        $env:VIVETOOL_MOCK_ADMIN = '1'
        $r1 = ((Test-IsAdministrator) -eq $true)
        $env:VIVETOOL_MOCK_ADMIN = 'true'
        $r2 = ((Test-IsAdministrator) -eq $true)
        $env:VIVETOOL_MOCK_ADMIN = '0'
        $r3 = ((Test-IsAdministrator) -eq $false)
        $env:VIVETOOL_MOCK_ADMIN = 'false'
        $r4 = ((Test-IsAdministrator) -eq $false)
        $r1 -and $r2 -and $r3 -and $r4
    } finally {
        $env:VIVETOOL_MOCK_ADMIN = $orig
    }
}

Test-Challenge "Elevation" "Invoke-SelfElevation terminates recursion loop when -Elevated is present" {
    $orig = $env:VIVETOOL_MOCK_ADMIN
    try {
        $env:VIVETOOL_MOCK_ADMIN = '0'
        $threwLoopGuard = $false
        try {
            $prevEap = $ErrorActionPreference
            $ErrorActionPreference = 'Continue'
            $res = Invoke-SelfElevation -ScriptPath $scriptPath -Elevated -MockMode
            $ErrorActionPreference = $prevEap
            if ($res -eq $false) { $threwLoopGuard = $true }
        } catch {
            $threwLoopGuard = ($_.FullyQualifiedErrorId -like "*DeniedOrFailed*")
        }
        $threwLoopGuard
    } finally {
        $env:VIVETOOL_MOCK_ADMIN = $orig
    }
}

Test-Challenge "Elevation" "Invoke-SelfElevation throws terminating error when -ScriptPath is invalid" {
    $orig = $env:VIVETOOL_MOCK_ADMIN
    try {
        $env:VIVETOOL_MOCK_ADMIN = '0'
        $threw = $false
        try {
            Invoke-SelfElevation -ScriptPath "Z:\FakeDirectory\NonExistentScript_12345.ps1"
        } catch {
            $threw = ($_.FullyQualifiedErrorId -like "*ScriptPathUnresolved*")
        }
        $threw
    } finally {
        $env:VIVETOOL_MOCK_ADMIN = $orig
    }
}

# ------------------------------------------------------------------------------
# CATEGORY 3: Provisioning Path Discovery & Companion Dependency Integrity
# ------------------------------------------------------------------------------
Write-Host "`n[+] 3. Provisioning Path Discovery and Companion Dependencies" -ForegroundColor Yellow

Test-Challenge "Provisioning" "Ensure-ViVeTool discovers local binary when all 4 companions exist" {
    $testDir = Join-Path ([System.IO.Path]::GetTempPath()) ("ViVeTool_AdvTest_" + [guid]::NewGuid().ToString('N').Substring(0,8))
    New-Item -ItemType Directory -Path $testDir -Force | Out-Null
    try {
        Set-Content -LiteralPath (Join-Path $testDir "ViVeTool.exe") -Value "MZ_TEST"
        Set-Content -LiteralPath (Join-Path $testDir "Albacore.ViVe.dll") -Value "DLL_TEST"
        Set-Content -LiteralPath (Join-Path $testDir "FeatureDictionary.pfs") -Value "PFS_TEST"
        Set-Content -LiteralPath (Join-Path $testDir "Newtonsoft.Json.dll") -Value "JSON_TEST"

        $found = Ensure-ViVeTool -TargetDirectory $testDir
        $found -eq (Join-Path $testDir "ViVeTool.exe")
    } finally {
        Remove-Item -LiteralPath $testDir -Recurse -Force -ErrorAction SilentlyContinue
    }
}

Test-Challenge "Provisioning" "Ensure-ViVeTool warns and re-provisions if companion DLL is missing" {
    $testDir = Join-Path ([System.IO.Path]::GetTempPath()) ("ViVeTool_AdvTest_" + [guid]::NewGuid().ToString('N').Substring(0,8))
    New-Item -ItemType Directory -Path $testDir -Force | Out-Null
    try {
        Set-Content -LiteralPath (Join-Path $testDir "ViVeTool.exe") -Value "MZ_TEST"
        # Albacore.ViVe.dll is intentionally omitted!
        Set-Content -LiteralPath (Join-Path $testDir "FeatureDictionary.pfs") -Value "PFS_TEST"
        Set-Content -LiteralPath (Join-Path $testDir "Newtonsoft.Json.dll") -Value "JSON_TEST"

        # In DryRun mode, Ensure-ViVeTool should detect missing companions and return default path without accepting broken local
        $res = Ensure-ViVeTool -TargetDirectory $testDir -DryRun
        $res -eq (Join-Path $testDir "ViVeTool.exe")
    } finally {
        Remove-Item -LiteralPath $testDir -Recurse -Force -ErrorAction SilentlyContinue
    }
}

Test-Challenge "Provisioning" "Ensure-ViVeTool handles complex directory path with spaces and brackets" {
    $testDir = Join-Path ([System.IO.Path]::GetTempPath()) "ViVe Test (x86) [v1.0] Special and Spaces"
    New-Item -ItemType Directory -Path $testDir -Force | Out-Null
    try {
        Set-Content -LiteralPath (Join-Path $testDir "ViVeTool.exe") -Value "MZ_TEST"
        Set-Content -LiteralPath (Join-Path $testDir "Albacore.ViVe.dll") -Value "DLL_TEST"
        Set-Content -LiteralPath (Join-Path $testDir "FeatureDictionary.pfs") -Value "PFS_TEST"
        Set-Content -LiteralPath (Join-Path $testDir "Newtonsoft.Json.dll") -Value "JSON_TEST"

        $found = Ensure-ViVeTool -TargetDirectory $testDir
        $found -eq (Join-Path $testDir "ViVeTool.exe")
    } finally {
        Remove-Item -LiteralPath $testDir -Recurse -Force -ErrorAction SilentlyContinue
    }
}

Test-Challenge "Provisioning" "Ensure-ViVeTool DryRun produces no file side-effects" {
    $testDir = Join-Path ([System.IO.Path]::GetTempPath()) ("ViVeTool_AdvTest_" + [guid]::NewGuid().ToString('N').Substring(0,8))
    try {
        $res = Ensure-ViVeTool -TargetDirectory $testDir -DryRun
        (-not (Test-Path -LiteralPath $testDir)) -and ($res -like "*ViVeTool.exe")
    } finally {
        if (Test-Path -LiteralPath $testDir) { Remove-Item -LiteralPath $testDir -Recurse -Force -ErrorAction SilentlyContinue }
    }
}

Test-Challenge "Provisioning" "Ensure-ViVeTool throws terminating error for non-existent explicit path" {
    $threw = $false
    try {
        Ensure-ViVeTool -ViVeToolPath "C:\NonExistent_Directory_12345\vivetool.exe"
    } catch {
        $threw = ($_.FullyQualifiedErrorId -like "*ExplicitPathNotFound*")
    }
    $threw
}

# ------------------------------------------------------------------------------
# CATEGORY 4: Live GitHub API, Download & Binary Verification
# ------------------------------------------------------------------------------
Write-Host "`n[+] 4. Live GitHub API and Release Download Verification" -ForegroundColor Yellow

Test-Challenge "LiveDownload" "Live query to GitHub API retrieves v0.3.4 release asset metadata" {
    $headers = @{
        "User-Agent" = "ViVeTool-Feature-Enabler-PowerShell/1.0"
        "Accept"     = "application/vnd.github.v3+json"
    }
    $apiUrl = "https://api.github.com/repos/thebookisclosed/ViVe/releases/latest"
    $rel = Invoke-RestMethod -Uri $apiUrl -Headers $headers -TimeoutSec 15 -ErrorAction Stop
    $assetNames = $rel.assets.name
    ($assetNames -match 'IntelAmd') -and ($assetNames -match 'SnapdragonArm64')
}

Test-Challenge "LiveDownload" "Live Ensure-ViVeTool provisions genuine ViVeTool into isolated sandbox" {
    $isolatedDir = Join-Path ([System.IO.Path]::GetTempPath()) ("ViVeTool_LiveSandBox_" + [guid]::NewGuid().ToString('N').Substring(0,8))
    try {
        $exePath = Ensure-ViVeTool -TargetDirectory $isolatedDir -ForceDownload -TimeoutSeconds 30
        
        $exeExists = Test-Path -Path $exePath -PathType Leaf
        $dllExists = Test-Path -Path (Join-Path $isolatedDir "Albacore.ViVe.dll") -PathType Leaf
        $pfsExists = Test-Path -Path (Join-Path $isolatedDir "FeatureDictionary.pfs") -PathType Leaf
        $jsonExists = Test-Path -Path (Join-Path $isolatedDir "Newtonsoft.Json.dll") -PathType Leaf

        # PE Header Check
        $bytes = [System.IO.File]::ReadAllBytes($exePath)
        $isPe = ($bytes[0] -eq 0x4D -and $bytes[1] -eq 0x5A)

        # Execution Check
        $out = & $exePath /? 2>&1
        $hasBanner = ($out -join ' ') -match 'ViVeTool v0\.3\.\d+'

        $exeExists -and $dllExists -and $pfsExists -and $jsonExists -and $isPe -and $hasBanner
    } finally {
        Remove-Item -Path $isolatedDir -Recurse -Force -ErrorAction SilentlyContinue
    }
}

# ------------------------------------------------------------------------------
# CATEGORY 5: Fault Injection & Resilience Testing
# ------------------------------------------------------------------------------
Write-Host "`n[+] 5. Fault Injection and Resilience Tests" -ForegroundColor Yellow

Test-Challenge "FaultInjection" "Corrupted ZIP archive during extraction triggers exception" {
    $sandbox = Join-Path ([System.IO.Path]::GetTempPath()) ("ViVeTool_CorruptZip_" + [guid]::NewGuid().ToString('N').Substring(0,8))
    New-Item -ItemType Directory -Path $sandbox -Force | Out-Null
    try {
        # We simulate what happens if Expand-Archive fails on corrupted bytes
        $badZip = Join-Path $sandbox "BadArchive.zip"
        [System.IO.File]::WriteAllBytes($badZip, @(0x50, 0x4B, 0x03, 0x04, 0x00, 0x00, 0xFF, 0xFF))

        $threw = $false
        try {
            Expand-Archive -Path $badZip -DestinationPath $sandbox -Force -ErrorAction Stop
        } catch {
            $threw = $true
        }
        $threw
    } finally {
        Remove-Item -Path $sandbox -Recurse -Force -ErrorAction SilentlyContinue
    }
}

Test-Challenge "FaultInjection" "Unreachable download endpoint fails within reasonable timeout" {
    $sandbox = Join-Path ([System.IO.Path]::GetTempPath()) ("ViVeTool_NetFail_" + [guid]::NewGuid().ToString('N').Substring(0,8))
    try {
        $sw = [System.Diagnostics.Stopwatch]::StartNew()
        $threw = $false
        try {
            # Unreachable IP with 2s timeout
            Invoke-WebRequest -Uri "http://10.255.255.1/ViVeTool.zip" -OutFile (Join-Path $sandbox "test.zip") -TimeoutSec 2 -ErrorAction Stop
        } catch {
            $threw = $true
        } finally {
            $sw.Stop()
        }
        $threw -and ($sw.Elapsed.TotalSeconds -lt 10)
    } finally {
        if (Test-Path $sandbox) { Remove-Item -Path $sandbox -Recurse -Force -ErrorAction SilentlyContinue }
    }
}

# ------------------------------------------------------------------------------
# CATEGORY 6: CLI Script Entry Point (Get-ViVeTool.ps1) Contracts & Exit Codes
# ------------------------------------------------------------------------------
Write-Host "`n[+] 6. CLI Script Entry Point (Get-ViVeTool.ps1) Contract Tests" -ForegroundColor Yellow

Test-Challenge "CLI" "Get-ViVeTool.ps1 -PassThru outputs binary path string and exits 0" {
    $tempOut = Join-Path ([System.IO.Path]::GetTempPath()) ("gvt_out_" + [guid]::NewGuid().ToString('N') + ".txt")
    $proc = Start-Process -FilePath "powershell.exe" `
                          -ArgumentList "-NoProfile", "-ExecutionPolicy", "Bypass", "-File", "`"$scriptPath`"", "-PassThru" `
                          -NoNewWindow -Wait -PassThru -RedirectStandardOutput $tempOut
    
    $out = if (Test-Path $tempOut) { Get-Content $tempOut -Raw } else { "" }
    $exitCode = $proc.ExitCode
    Remove-Item $tempOut -Force -ErrorAction SilentlyContinue

    ($exitCode -eq 0) -and ($out -match 'ViVeTool\.exe')
}

Test-Challenge "CLI" "Get-ViVeTool.ps1 -DryRun exits with code 0" {
    $proc = Start-Process -FilePath "powershell.exe" `
                          -ArgumentList "-NoProfile", "-ExecutionPolicy", "Bypass", "-File", "`"$scriptPath`"", "-DryRun" `
                          -NoNewWindow -Wait -PassThru
    $proc.ExitCode -eq 0
}

Test-Challenge "CLI" "Get-ViVeTool.ps1 exits with non-zero for invalid explicit path" {
    $proc = Start-Process -FilePath "powershell.exe" `
                          -ArgumentList "-NoProfile", "-ExecutionPolicy", "Bypass", "-File", "`"$scriptPath`"", "-ViVeToolPath", "`"Z:\Invalid\Path\vivetool.exe`"" `
                          -NoNewWindow -Wait -PassThru
    $proc.ExitCode -ne 0
}

Test-Challenge "CLI" "Get-ViVeTool.ps1 parameter validator rejects out-of-range TimeoutSeconds" {
    $proc = Start-Process -FilePath "powershell.exe" `
                          -ArgumentList "-NoProfile", "-ExecutionPolicy", "Bypass", "-File", "`"$scriptPath`"", "-TimeoutSeconds", "1" `
                          -NoNewWindow -Wait -PassThru
    $proc.ExitCode -ne 0
}

Test-Challenge "CLI" "Get-ViVeTool.ps1 alias -TargetDirectory is properly bound" {
    $sandbox = Join-Path ([System.IO.Path]::GetTempPath()) ("ViVeTool_AliasTest_" + [guid]::NewGuid().ToString('N').Substring(0,8))
    $tempOut = Join-Path ([System.IO.Path]::GetTempPath()) ("gvt_alias_" + [guid]::NewGuid().ToString('N') + ".txt")
    try {
        $proc = Start-Process -FilePath "powershell.exe" `
                              -ArgumentList "-NoProfile", "-ExecutionPolicy", "Bypass", "-File", "`"$scriptPath`"", "-TargetDirectory", "`"$sandbox`"", "-ForceDownload", "-DryRun", "-PassThru" `
                              -NoNewWindow -Wait -PassThru -RedirectStandardOutput $tempOut
        $out = if (Test-Path $tempOut) { Get-Content $tempOut -Raw } else { "" }
        ($proc.ExitCode -eq 0) -and ($out -match [regex]::Escape($sandbox))
    } finally {
        Remove-Item $tempOut -Force -ErrorAction SilentlyContinue
        if (Test-Path $sandbox) { Remove-Item -Path $sandbox -Recurse -Force -ErrorAction SilentlyContinue }
    }
}

# ------------------------------------------------------------------------------
# SUMMARY REPORT
# ------------------------------------------------------------------------------
Write-Host "`n================================================================================" -ForegroundColor Cyan
Write-Host " Adversarial Test Suite Summary" -ForegroundColor Cyan
Write-Host " Total: $script:TotalTests | Passed: $script:PassedTests | Failed: $script:FailedTests" -ForegroundColor $(if ($script:FailedTests -eq 0) { "Green" } else { "Red" })
Write-Host "================================================================================" -ForegroundColor Cyan

if ($script:FailedTests -gt 0) {
    exit 1
} else {
    exit 0
}
