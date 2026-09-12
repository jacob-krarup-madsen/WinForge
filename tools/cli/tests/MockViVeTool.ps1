<#
.SYNOPSIS
    MockViVeTool.ps1 - High-Fidelity ViVeTool CLI Simulator for Non-Destructive Testing.

.DESCRIPTION
    Simulates vivetool.exe CLI behaviors, exit codes, and output patterns across all commands:
    /enable, /disable, /reset, /fullreset, /query, /?, -?, --help.
    Supports fault injection, latency emulation, invocation logging, and mock state persistence.

.PARAMETER Arguments
    Command line arguments passed to ViVeTool.
#>
[CmdletBinding()]
param(
    [Parameter(ValueFromRemainingArguments = $true)]
    [string[]]$Arguments
)

# 1. Evaluate Latency Simulation
if ($env:VIVETOOL_MOCK_LATENCY_MS) {
    $delay = [int]$env:VIVETOOL_MOCK_LATENCY_MS
    if ($delay -gt 0) {
        Start-Sleep -Milliseconds $delay
    }
}

# 2. Record Invocation Log if configured
if ($env:VIVETOOL_MOCK_LOG_FILE) {
    $logDir = [System.IO.Path]::GetDirectoryName($env:VIVETOOL_MOCK_LOG_FILE)
    if ($logDir -and -not (Test-Path $logDir)) {
        New-Item -ItemType Directory -Path $logDir -Force | Out-Null
    }
    $entry = "$(Get-Date -Format 'o') | $([string]::Join(' ', $Arguments))"
    Add-Content -Path $env:VIVETOOL_MOCK_LOG_FILE -Value $entry -Encoding utf8
}

# Helper to output messages both to console/pipeline
function Out-Msg {
    param([string]$Text)
    Write-Output $Text
}

function Out-ErrMsg {
    param([string]$Text)
    [Console]::Error.WriteLine($Text)
}

# 3. Direct Overrides from Environment Variables
if ($env:VIVETOOL_MOCK_STDERR) {
    Out-ErrMsg $env:VIVETOOL_MOCK_STDERR
}

if ($env:VIVETOOL_MOCK_STDOUT) {
    Out-Msg $env:VIVETOOL_MOCK_STDOUT
}

if ($env:VIVETOOL_MOCK_EXITCODE) {
    $forcedExit = [int]$env:VIVETOOL_MOCK_EXITCODE
    exit $forcedExit
}

# 4. Mode-Based Fast Paths
$mockMode = $env:VIVETOOL_MOCK_MODE

if ($mockMode -eq "ThrowException" -or $mockMode -eq "Crash") {
    Out-ErrMsg "FATAL: ViVeTool process crashed unexpectedly (Access Violation 0xC0000005)"
    exit 255
}

if ($mockMode -eq "AccessDenied") {
    Out-ErrMsg "Access is denied. Administrator privileges required."
    Out-Msg "An error occurred while setting feature configurations in the Runtime store (Access is denied)"
    exit 5
}

if ($mockMode -eq "SyntaxError") {
    Out-ErrMsg "Usage: vivetool <command> [options]"
    Out-Msg "ViVeTool v0.3.4 - Windows feature configuration tool"
    Out-Msg "Error: Invalid parameter syntax"
    exit 2
}

# 5. Parse Arguments
$command = ""
$id = ""
$store = "both"
$priority = ""
$name = ""

$argString = [string]::Join(" ", $Arguments)

foreach ($arg in $Arguments) {
    if ($arg -match '^/enable$' -or $arg -match '^-enable$') { $command = "enable" }
    elseif ($arg -match '^/disable$' -or $arg -match '^-disable$') { $command = "disable" }
    elseif ($arg -match '^/reset$' -or $arg -match '^-reset$') { $command = "reset" }
    elseif ($arg -match '^/fullreset$' -or $arg -match '^-fullreset$') { $command = "fullreset" }
    elseif ($arg -match '^/query$' -or $arg -match '^-query$') { $command = "query" }
    elseif ($arg -match '^[/\-]\?' -or $arg -match '^[/\-]help$') { $command = "help" }
    elseif ($arg -match '^[/\-]id:(\d+)$') { $id = $Matches[1] }
    elseif ($arg -match '^[/\-]id$' -or $arg -match '^-id$') { <# Next arg is id #> }
    elseif ($arg -match '^[/\-]store:(.*)$') { $store = $Matches[1] }
    elseif ($arg -match '^[/\-]name:(.*)$') { $name = $Matches[1] }
    elseif ($arg -match '^\d{7,8}$') { $id = $arg }
}

# 6. Check Specific Target ID Overrides
if ($id) {
    # Check Denied IDs
    if ($env:VIVETOOL_MOCK_DENIED_IDS) {
        $deniedList = $env:VIVETOOL_MOCK_DENIED_IDS -split ',' | ForEach-Object { $_.Trim() }
        if ($deniedList -contains $id) {
            Out-ErrMsg "Access is denied. Administrator privileges required."
            Out-Msg "An error occurred while setting feature configurations in the Runtime store (Access is denied)"
            exit 5
        }
    }

    # Check Fail / Unsupported IDs
    if ($env:VIVETOOL_MOCK_FAIL_IDS) {
        $failList = $env:VIVETOOL_MOCK_FAIL_IDS -split ',' | ForEach-Object { $_.Trim() }
        if ($failList -contains $id) {
            Out-Msg "ViVeTool v0.3.4 - Windows feature configuration tool"
            Out-Msg "Failed to set feature configuration: Feature $id not found"
            exit 1
        }
    }
}

if ($mockMode -eq "AllUnsupported") {
    Out-Msg "ViVeTool v0.3.4 - Windows feature configuration tool"
    Out-Msg "Failed to set feature configuration: Feature $id not found"
    exit 1
}

# 7. Command Execution Simulation
switch ($command) {
    "enable" {
        if (-not $id) {
            Out-ErrMsg "Error: Missing required feature ID parameter"
            exit 2
        }
        Out-Msg "ViVeTool v0.3.4 - Windows feature configuration tool"
        Out-Msg "Successfully set feature configuration: $id"
        
        # State tracking if store file specified
        if ($env:VIVETOOL_MOCK_STORE) {
            $storeDir = [System.IO.Path]::GetDirectoryName($env:VIVETOOL_MOCK_STORE)
            if ($storeDir -and -not (Test-Path $storeDir)) { New-Item -ItemType Directory -Path $storeDir -Force | Out-Null }
            $curr = if (Test-Path $env:VIVETOOL_MOCK_STORE) { Get-Content $env:VIVETOOL_MOCK_STORE } else { @() }
            if ($curr -notcontains $id) {
                Add-Content -Path $env:VIVETOOL_MOCK_STORE -Value $id
            }
        }
        exit 0
    }
    "disable" {
        if (-not $id) {
            Out-ErrMsg "Error: Missing required feature ID parameter"
            exit 2
        }
        Out-Msg "ViVeTool v0.3.4 - Windows feature configuration tool"
        Out-Msg "Successfully set feature configuration: $id"
        
        if ($env:VIVETOOL_MOCK_STORE -and (Test-Path $env:VIVETOOL_MOCK_STORE)) {
            $curr = Get-Content $env:VIVETOOL_MOCK_STORE | Where-Object { $_ -ne $id }
            Set-Content -Path $env:VIVETOOL_MOCK_STORE -Value $curr
        }
        exit 0
    }
    "reset" {
        if (-not $id) {
            Out-ErrMsg "Error: Missing required feature ID parameter"
            exit 2
        }
        Out-Msg "ViVeTool v0.3.4 - Windows feature configuration tool"
        Out-Msg "Successfully reset feature configuration: $id"
        exit 0
    }
    "fullreset" {
        Out-Msg "ViVeTool v0.3.4 - Windows feature configuration tool"
        Out-Msg "Successfully reset all feature configurations"
        if ($env:VIVETOOL_MOCK_STORE -and (Test-Path $env:VIVETOOL_MOCK_STORE)) {
            Remove-Item -Path $env:VIVETOOL_MOCK_STORE -Force -ErrorAction SilentlyContinue
        }
        exit 0
    }
    "query" {
        Out-Msg "ViVeTool v0.3.4 - Windows feature configuration tool"
        Out-Msg "Configured feature overrides:"
        if ($env:VIVETOOL_MOCK_STORE -and (Test-Path $env:VIVETOOL_MOCK_STORE)) {
            $items = Get-Content $env:VIVETOOL_MOCK_STORE
            foreach ($item in $items) {
                Out-Msg "  $item [Enabled]"
            }
        } else {
            Out-Msg "  61161244 [Enabled]"
            Out-Msg "  61754985 [Enabled]"
        }
        exit 0
    }
    "help" {
        Out-Msg "ViVeTool v0.3.4 - Windows feature configuration tool"
        Out-Msg "Usage: vivetool <command> [options]"
        Out-Msg "Commands:"
        Out-Msg "  /enable    Enable a feature"
        Out-Msg "  /disable   Disable a feature"
        Out-Msg "  /reset     Reset a feature"
        Out-Msg "  /fullreset Reset all features"
        Out-Msg "  /query     Query configured features"
        exit 0
    }
    default {
        if ($Arguments.Count -eq 0) {
            Out-Msg "ViVeTool v0.3.4 - Windows feature configuration tool"
            Out-Msg "Usage: vivetool <command> [options]"
            exit 0
        }
        Out-ErrMsg "Usage: vivetool <command> [options]"
        Out-Msg "ViVeTool v0.3.4 - Windows feature configuration tool"
        Out-Msg "Error: Unrecognized command or invalid parameter syntax '$argString'"
        exit 2
    }
}
