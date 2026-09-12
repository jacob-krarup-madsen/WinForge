<#
.SYNOPSIS
    TestHarness.psm1 - Zero-Dependency Automated Testing DSL and Assertion Engine for ViVeTool Feature Enabler.
#>

if (-not (Get-Variable -Name "TestResults" -Scope Global -ErrorAction SilentlyContinue)) {
    $global:TestResults = [System.Collections.Generic.List[PSObject]]::new()
}

$script:CurrentSuite = "Default"
$script:CurrentContext = ""

function Reset-TestResults {
    if ($global:TestResults) {
        $global:TestResults.Clear()
    } else {
        $global:TestResults = [System.Collections.Generic.List[PSObject]]::new()
    }
}

function Describe {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true, Position = 0)]
        [string]$Name,
        [Parameter(Mandatory = $true, Position = 1)]
        [scriptblock]$ScriptBlock
    )
    $prevSuite = $script:CurrentSuite
    $script:CurrentSuite = $Name
    Write-Host "`n[+] Suite: $Name" -ForegroundColor Cyan
    try {
        & $ScriptBlock
    } finally {
        $script:CurrentSuite = $prevSuite
    }
}

function Context {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true, Position = 0)]
        [string]$Name,
        [Parameter(Mandatory = $true, Position = 1)]
        [scriptblock]$ScriptBlock
    )
    $prevCtx = $script:CurrentContext
    $script:CurrentContext = $Name
    Write-Host "  [-] Context: $Name" -ForegroundColor DarkCyan
    try {
        & $ScriptBlock
    } finally {
        $script:CurrentContext = $prevCtx
    }
}

function It {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true, Position = 0)]
        [string]$Name,
        [Parameter(Mandatory = $true, Position = 1)]
        [scriptblock]$ScriptBlock,
        [switch]$Skip
    )
    
    $stopwatch = [System.Diagnostics.Stopwatch]::StartNew()
    $testRecord = [PSCustomObject]@{
        Suite     = $script:CurrentSuite
        Context   = $script:CurrentContext
        Name      = $Name
        Status    = "Pending"
        Error     = $null
        Duration  = 0
        Timestamp = (Get-Date -Format 'o')
    }

    if ($Skip) {
        $testRecord.Status = "Skipped"
        $stopwatch.Stop()
        $testRecord.Duration = $stopwatch.ElapsedMilliseconds
        $global:TestResults.Add($testRecord)
        Write-Host "    [SKIP] $Name" -ForegroundColor Yellow
        return
    }

    try {
        & $ScriptBlock
        $testRecord.Status = "Passed"
        Write-Host "    [PASS] $Name" -ForegroundColor Green
    } catch {
        $testRecord.Status = "Failed"
        $testRecord.Error = $_.Exception.Message
        Write-Host "    [FAIL] $Name" -ForegroundColor Red
        Write-Host "           Error: $($_.Exception.Message)" -ForegroundColor DarkRed
    } finally {
        $stopwatch.Stop()
        $testRecord.Duration = $stopwatch.ElapsedMilliseconds
        $global:TestResults.Add($testRecord)
    }
}

function Assert-Equal {
    param($Actual, $Expected, [string]$Message = "")
    if ($Actual -ne $Expected) {
        $msg = if ($Message) { "$Message. " } else { "" }
        throw "${msg}Expected: <$Expected> (Type: $($Expected.GetType().Name)), but got: <$Actual> (Type: $($Actual.GetType().Name))"
    }
}

function Assert-NotEqual {
    param($Actual, $Expected, [string]$Message = "")
    if ($Actual -eq $Expected) {
        $msg = if ($Message) { "$Message. " } else { "" }
        throw "${msg}Expected value NOT to equal: <$Expected>, but it matched."
    }
}

function Assert-True {
    param($Condition, [string]$Message = "Expected condition to be True")
    if (-not [bool]$Condition) {
        throw "Assertion Failed: $Message"
    }
}

function Assert-False {
    param($Condition, [string]$Message = "Expected condition to be False")
    if ([bool]$Condition) {
        throw "Assertion Failed: $Message"
    }
}

function Assert-Match {
    param([string]$Actual, [string]$Pattern, [string]$Message = "")
    if ($Actual -notmatch $Pattern) {
        $msg = if ($Message) { "$Message. " } else { "" }
        throw "${msg}Expected string to match pattern <$Pattern>, but got: <$Actual>"
    }
}

function Assert-NotMatch {
    param([string]$Actual, [string]$Pattern, [string]$Message = "")
    if ($Actual -match $Pattern) {
        $msg = if ($Message) { "$Message. " } else { "" }
        throw "${msg}Expected string NOT to match pattern <$Pattern>, but got match in: <$Actual>"
    }
}

function Assert-Throws {
    param([scriptblock]$ScriptBlock, [string]$Pattern = "", [string]$Message = "")
    $threw = $false
    $caughtMsg = ""
    try {
        & $ScriptBlock
    } catch {
        $threw = $true
        $caughtMsg = $_.Exception.Message
    }
    if (-not $threw) {
        $msg = if ($Message) { "$Message. " } else { "" }
        throw "${msg}Expected scriptblock to throw an exception, but it completed without errors."
    }
    if ($Pattern -and ($caughtMsg -notmatch $Pattern)) {
        throw "Exception was thrown, but message '$caughtMsg' did not match expected pattern '$Pattern'."
    }
}

function Assert-NotThrows {
    param([scriptblock]$ScriptBlock, [string]$Message = "")
    try {
        & $ScriptBlock
    } catch {
        $msg = if ($Message) { "$Message. " } else { "" }
        throw "${msg}Expected scriptblock NOT to throw, but caught error: $($_.Exception.Message)"
    }
}

function Assert-PathExists {
    param([string]$Path, [string]$Message = "")
    if (-not (Test-Path -LiteralPath $Path)) {
        $msg = if ($Message) { "$Message. " } else { "" }
        throw "${msg}Expected path to exist, but not found: <$Path>"
    }
}

function Assert-PathNotExist {
    param([string]$Path, [string]$Message = "")
    if (Test-Path -LiteralPath $Path) {
        $msg = if ($Message) { "$Message. " } else { "" }
        throw "${msg}Expected path NOT to exist, but found: <$Path>"
    }
}

function Assert-Count {
    param($Collection, [int]$ExpectedCount, [string]$Message = "")
    $actualCount = 0
    if ($null -ne $Collection) {
        if ($Collection -is [System.Collections.ICollection] -or $Collection -is [System.Array]) {
            $actualCount = $Collection.Count
        } else {
            $actualCount = @($Collection).Count
        }
    }
    if ($actualCount -ne $ExpectedCount) {
        $msg = if ($Message) { "$Message. " } else { "" }
        throw "${msg}Expected count <$ExpectedCount>, but got <$actualCount>."
    }
}

function Assert-Contains {
    param($Collection, $Item, [string]$Message = "")
    $arr = @($Collection)
    if ($arr -notcontains $Item) {
        $msg = if ($Message) { "$Message. " } else { "" }
        throw "${msg}Expected collection to contain item <$Item>."
    }
}

function New-TestSandbox {
    $guid = [Guid]::NewGuid().ToString("N").Substring(0, 8)
    $timestamp = (Get-Date -Format 'yyyyMMdd_HHmmss')
    $sandboxPath = Join-Path -Path $env:TEMP -ChildPath "ViVeTool_Test_${timestamp}_${guid}"
    New-Item -ItemType Directory -Path $sandboxPath -Force | Out-Null
    return $sandboxPath
}

function Remove-TestSandbox {
    param([string]$SandboxPath)
    if ($SandboxPath -and (Test-Path -LiteralPath $SandboxPath)) {
        Remove-Item -LiteralPath $SandboxPath -Recurse -Force -ErrorAction SilentlyContinue
    }
}

function Get-TestSummary {
    $passed = ($global:TestResults | Where-Object { $_.Status -eq "Passed" }).Count
    $failed = ($global:TestResults | Where-Object { $_.Status -eq "Failed" }).Count
    $skipped = ($global:TestResults | Where-Object { $_.Status -eq "Skipped" }).Count
    $total = $global:TestResults.Count
    
    return [PSCustomObject]@{
        Total    = $total
        Passed   = $passed
        Failed   = $failed
        Skipped  = $skipped
        AllPassed = ($failed -eq 0 -and $total -gt 0)
    }
}

Export-ModuleMember -Function Describe, Context, It, `
    Assert-Equal, Assert-NotEqual, Assert-True, Assert-False, `
    Assert-Match, Assert-NotMatch, Assert-Throws, Assert-NotThrows, `
    Assert-PathExists, Assert-PathNotExist, Assert-Count, Assert-Contains, `
    New-TestSandbox, Remove-TestSandbox, Get-TestSummary, Reset-TestResults
