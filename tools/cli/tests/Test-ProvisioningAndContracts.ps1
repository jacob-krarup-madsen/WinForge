# ==============================================================================
# tests\Test-ProvisioningAndContracts.ps1 — Non-Destructive Unit Test Fixtures
# ==============================================================================

[CmdletBinding()]
param(
    [Parameter()]
    [switch]$VerboseOutput
)

$ErrorActionPreference = 'Stop'

# ------------------------------------------------------------------------------
# Test Framework Helpers (Zero external dependency)
# ------------------------------------------------------------------------------
$script:PassCount = 0
$script:FailCount = 0
$script:TestResults = [System.Collections.Generic.List[PSCustomObject]]::new()

function Assert-Test {
    param(
        [Parameter(Mandatory = $true)][string]$TestName,
        [Parameter(Mandatory = $true)][scriptblock]$Assertion
    )

    try {
        $result = & $Assertion
        if ($result -eq $true -or $null -eq $result) {
            $script:PassCount++
            Write-Host "  [PASS] $TestName" -ForegroundColor Green
            $script:TestResults.Add([PSCustomObject]@{ Test = $TestName; Status = 'PASS'; Error = $null })
        } else {
            $script:FailCount++
            Write-Host "  [FAIL] $TestName (Assertion returned false)" -ForegroundColor Red
            $script:TestResults.Add([PSCustomObject]@{ Test = $TestName; Status = 'FAIL'; Error = 'Assertion returned false' })
        }
    } catch {
        $script:FailCount++
        Write-Host "  [FAIL] $TestName ($($_.Exception.Message))" -ForegroundColor Red
        $script:TestResults.Add([PSCustomObject]@{ Test = $TestName; Status = 'FAIL'; Error = $_.Exception.Message })
    }
}

Write-Host "================================================================================" -ForegroundColor Cyan
Write-Host " Running Milestone 1 Module & Packaging Test Suite" -ForegroundColor Cyan
Write-Host "================================================================================" -ForegroundColor Cyan

$projectRoot = Split-Path -Path $PSScriptRoot -Parent
if (-not (Test-Path -Path (Join-Path -Path $projectRoot -ChildPath "ViVeToolEnabler.psd1"))) {
    $projectRoot = $PSScriptRoot # In case executed from project root
}

$manifestPath = Join-Path -Path $projectRoot -ChildPath "ViVeToolEnabler.psd1"
$modulePath   = Join-Path -Path $projectRoot -ChildPath "ViVeToolEnabler.psm1"
$scriptPath   = Join-Path -Path $projectRoot -ChildPath "Get-ViVeTool.ps1"

# ------------------------------------------------------------------------------
# FIXTURE 1: AST Parsing & Syntax Validation
# ------------------------------------------------------------------------------
Write-Host "`n[+] Fixture 1: Static AST & Syntax Validation" -ForegroundColor Yellow

Assert-Test "Module Manifest ViVeToolEnabler.psd1 exists" {
    Test-Path -Path $manifestPath -PathType Leaf
}

Assert-Test "Module Manifest passes Test-ModuleManifest validation" {
    $manifestData = Test-ModuleManifest -Path $manifestPath -ErrorAction Stop
    $manifestData.Name -eq 'ViVeToolEnabler' -and $manifestData.Version -eq [version]'1.0.0'
}

Assert-Test "Root Module ViVeToolEnabler.psm1 is syntactically valid" {
    $errors = $null
    $tokens = $null
    $ast = [System.Management.Automation.Language.Parser]::ParseFile($modulePath, [ref]$tokens, [ref]$errors)
    $errors.Count -eq 0
}

Assert-Test "Standalone Script Get-ViVeTool.ps1 is syntactically valid" {
    $errors = $null
    $tokens = $null
    $ast = [System.Management.Automation.Language.Parser]::ParseFile($scriptPath, [ref]$tokens, [ref]$errors)
    $errors.Count -eq 0
}

# ------------------------------------------------------------------------------
# FIXTURE 2: Module Loading & Member Export Contracts
# ------------------------------------------------------------------------------
Write-Host "`n[+] Fixture 2: Module Loading & Member Export Contracts" -ForegroundColor Yellow

Assert-Test "Module imports successfully into session" {
    Import-Module -Name $manifestPath -Force -ErrorAction Stop
    $mod = Get-Module -Name "ViVeToolEnabler"
    $null -ne $mod
}

Assert-Test "Exported functions match Milestone 1 contract" {
    $exported = (Get-Command -Module "ViVeToolEnabler").Name
    ($exported -contains 'Ensure-ViVeTool') -and
    ($exported -contains 'Invoke-SelfElevation') -and
    ($exported -contains 'Test-IsAdministrator') -and
    ($exported -contains 'Get-SystemArchitecture')
}

Assert-Test "Private functions are NOT leaked or exported" {
    $exported = (Get-Command -Module "ViVeToolEnabler").Name
    ($exported -notcontains 'New-ViVeToolError') -and
    ($exported -notcontains 'Set-TlsSecurityProtocols') -and
    ($exported -notcontains 'Format-ArgumentForPowerShell')
}

# ------------------------------------------------------------------------------
# FIXTURE 3: Architecture Detection Contract Tests
# ------------------------------------------------------------------------------
Write-Host "`n[+] Fixture 3: Architecture Detection Contract Tests" -ForegroundColor Yellow

Assert-Test "Get-SystemArchitecture returns valid enum string ('ARM64', 'X64', or 'X86')" {
    $arch = Get-SystemArchitecture
    $arch -in @('ARM64', 'X64', 'X86')
}

# ------------------------------------------------------------------------------
# FIXTURE 4: Provisioning Subsystem & Ensure-ViVeTool Unit Tests
# ------------------------------------------------------------------------------
Write-Host "`n[+] Fixture 4: Ensure-ViVeTool Non-Destructive Unit Tests" -ForegroundColor Yellow

$sandboxDir = Join-Path -Path ([System.IO.Path]::GetTempPath()) -ChildPath "ViVeTool_Test_$(Get-Random)"
New-Item -ItemType Directory -Path $sandboxDir -Force | Out-Null

try {
    Assert-Test "Ensure-ViVeTool in DryRun mode does not write files" {
        $simPath = Ensure-ViVeTool -TargetDirectory $sandboxDir -DryRun
        (-not (Test-Path -Path (Join-Path -Path $sandboxDir -ChildPath "ViVeTool.exe"))) -and ($simPath -like "*ViVeTool.exe")
    }

    Assert-Test "Ensure-ViVeTool discovers pre-existing local binary with companion files" {
        $fakeExe = Join-Path -Path $sandboxDir -ChildPath "ViVeTool.exe"
        $fakeDll = Join-Path -Path $sandboxDir -ChildPath "Albacore.ViVe.dll"
        $fakePfs = Join-Path -Path $sandboxDir -ChildPath "FeatureDictionary.pfs"
        $fakeJson = Join-Path -Path $sandboxDir -ChildPath "Newtonsoft.Json.dll"

        Set-Content -Path $fakeExe -Value "MOCK_BINARY" -Force
        Set-Content -Path $fakeDll -Value "MOCK_DLL" -Force
        Set-Content -Path $fakePfs -Value "MOCK_PFS" -Force
        Set-Content -Path $fakeJson -Value "MOCK_JSON" -Force

        $resultPath = Ensure-ViVeTool -TargetDirectory $sandboxDir
        $resultPath -eq $fakeExe
    }

    Assert-Test "Ensure-ViVeTool honors explicit binary path" {
        $explicitExe = Join-Path -Path $sandboxDir -ChildPath "custom_vivetool.exe"
        Set-Content -Path $explicitExe -Value "MOCK_CUSTOM_PAYLOAD" -Force
        $resultPath = Ensure-ViVeTool -ViVeToolPath $explicitExe
        $resultPath -eq $explicitExe
    }

    Assert-Test "Ensure-ViVeTool throws terminating error for non-existent explicit path" {
        $threw = $false
        try {
            Ensure-ViVeTool -ViVeToolPath "Z:\NonExistent_Path_12345\vivetool.exe"
        } catch {
            $threw = ($_.FullyQualifiedErrorId -like "*ExplicitPathNotFound*")
        }
        $threw
    }

    Assert-Test "Ensure-ViVeTool honors environment mock shim (`$env:VIVETOOL_MOCK_MODE)" {
        $orig = $env:VIVETOOL_MOCK_MODE
        try {
            $env:VIVETOOL_MOCK_MODE = "1"
            $mockResult = Ensure-ViVeTool -TargetDirectory $sandboxDir
            $mockResult -like "*MockViVeTool.cmd"
        } finally {
            $env:VIVETOOL_MOCK_MODE = $orig
        }
    }
} finally {
    Remove-Item -Path $sandboxDir -Recurse -Force -ErrorAction SilentlyContinue
}

# ------------------------------------------------------------------------------
# FIXTURE 5: Auto-Elevation & Loop Guard Contract Tests
# ------------------------------------------------------------------------------
Write-Host "`n[+] Fixture 5: Invoke-SelfElevation & Loop Guard Contract Tests" -ForegroundColor Yellow

Assert-Test "Test-IsAdministrator executes safely and returns boolean" {
    $res = Test-IsAdministrator
    $res -is [bool]
}

Assert-Test "Test-IsAdministrator honors VIVETOOL_MOCK_ADMIN environment variable" {
    $orig = $env:VIVETOOL_MOCK_ADMIN
    try {
        $env:VIVETOOL_MOCK_ADMIN = "1"
        $isAdminTrue = Test-IsAdministrator
        $env:VIVETOOL_MOCK_ADMIN = "0"
        $isAdminFalse = Test-IsAdministrator
        $isAdminTrue -eq $true -and $isAdminFalse -eq $false
    } finally {
        $env:VIVETOOL_MOCK_ADMIN = $orig
    }
}

Assert-Test "Invoke-SelfElevation returns true when already elevated (or mocked admin)" {
    $orig = $env:VIVETOOL_MOCK_ADMIN
    try {
        $env:VIVETOOL_MOCK_ADMIN = "1"
        $res = Invoke-SelfElevation -ScriptPath $scriptPath
        $res -eq $true
    } finally {
        $env:VIVETOOL_MOCK_ADMIN = $orig
    }
}

Assert-Test "Invoke-SelfElevation returns false gracefully in MockMode when unprivileged" {
    $orig = $env:VIVETOOL_MOCK_ADMIN
    try {
        $env:VIVETOOL_MOCK_ADMIN = "0"
        $res = Invoke-SelfElevation -ScriptPath $scriptPath -MockMode
        $res -eq $false
    } finally {
        $env:VIVETOOL_MOCK_ADMIN = $orig
    }
}

# ------------------------------------------------------------------------------
# Test Suite Summary
# ------------------------------------------------------------------------------
Write-Host "`n================================================================================" -ForegroundColor Cyan
Write-Host " Test Summary: $script:PassCount Passed, $script:FailCount Failed (Total: $($script:PassCount + $script:FailCount))" -ForegroundColor Cyan
Write-Host "================================================================================" -ForegroundColor Cyan

if ($script:FailCount -gt 0) {
    exit 1
} else {
    exit 0
}