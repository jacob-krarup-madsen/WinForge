# WindowsOptimizer.psm1 - Windows 11 Speed & Latency Optimization PowerShell Module
$ScriptDir = $PSScriptRoot
$CoreDir   = Join-Path $ScriptDir "Core"

function Invoke-WindowsOptimization {
    <#
    .SYNOPSIS
    Runs the Master 25-Phase Windows 11 Speed & Latency Optimization Suite.
    #>
    [CmdletBinding()]
    param()
    
    $OptScript = Join-Path $CoreDir "Optimize-Windows.ps1"
    if (Test-Path $OptScript) {
        Start-Process powershell.exe -ArgumentList "-ExecutionPolicy Bypass -NoExit -File `"$OptScript`"" -Verb RunAs
    } else {
        Write-Error "Optimize-Windows.ps1 not found in Core directory."
    }
}

function Invoke-SystemAudit {
    <#
    .SYNOPSIS
    Runs the 3-State Master Matrix System Audit tool.
    #>
    [CmdletBinding()]
    param()
    
    $AuditScript = Join-Path $CoreDir "Audit-System.ps1"
    if (Test-Path $AuditScript) {
        & powershell.exe -ExecutionPolicy Bypass -File $AuditScript
    } else {
        Write-Error "Audit-System.ps1 not found in Core directory."
    }
}

function Invoke-DiskCleanup {
    <#
    .SYNOPSIS
    Runs DISM Component Store resetbase and storage cleanup.
    #>
    [CmdletBinding()]
    param()
    
    $CleanScript = Join-Path $CoreDir "Clean-Disk.ps1"
    if (Test-Path $CleanScript) {
        Start-Process powershell.exe -ArgumentList "-ExecutionPolicy Bypass -NoExit -File `"$CleanScript`"" -Verb RunAs
    } else {
        Write-Error "Clean-Disk.ps1 not found in Core directory."
    }
}

function Invoke-SystemBenchmark {
    <#
    .SYNOPSIS
    Runs the automated System Benchmark suite.
    #>
    [CmdletBinding()]
    param()
    
    $BenchScript = Join-Path $CoreDir "Benchmark-System.ps1"
    if (Test-Path $BenchScript) {
        & powershell.exe -ExecutionPolicy Bypass -File $BenchScript
    } else {
        Write-Error "Benchmark-System.ps1 not found in Core directory."
    }
}

function Invoke-MemoryFlush {
    <#
    .SYNOPSIS
    Flushes RAM working sets and standby memory lists.
    #>
    [CmdletBinding()]
    param()
    
    $FlushScript = Join-Path $CoreDir "Flush-Memory.ps1"
    if (Test-Path $FlushScript) {
        & powershell.exe -ExecutionPolicy Bypass -File $FlushScript
    } else {
        Write-Error "Flush-Memory.ps1 not found in Core directory."
    }
}

function Open-WinUI3ControlPanel {
    <#
    .SYNOPSIS
    Launches the Native Compiled C# WinUI 3 Application.
    #>
    [CmdletBinding()]
    param()
    
    $ExePath = Join-Path $ScriptDir "WinUI3_Optimizer.exe"
    if (Test-Path $ExePath) {
        Start-Process $ExePath
    } else {
        Write-Error "WinUI3_Optimizer.exe not found in module directory."
    }
}

function Save-OptimizationConfig {
    <#
    .SYNOPSIS
    Exports current optimization configuration to JSON.
    #>
    [CmdletBinding()]
    param([string]$OutputFile = "OptimizationConfig.json")
    
    $ExportScript = Join-Path $CoreDir "Export-Config.ps1"
    if (Test-Path $ExportScript) {
        & powershell.exe -ExecutionPolicy Bypass -File $ExportScript -OutputFile $OutputFile
    }
}

function Load-OptimizationConfig {
    <#
    .SYNOPSIS
    Imports optimization configuration from JSON.
    #>
    [CmdletBinding()]
    param([string]$ConfigFile = "OptimizationConfig.json")
    
    $ImportScript = Join-Path $CoreDir "Import-Config.ps1"
    if (Test-Path $ImportScript) {
        Start-Process powershell.exe -ArgumentList "-ExecutionPolicy Bypass -NoExit -File `"$ImportScript`" -ConfigFile `"$ConfigFile`"" -Verb RunAs
    }
}

Export-ModuleMember -Function Invoke-WindowsOptimization, Invoke-SystemAudit, Invoke-DiskCleanup, Invoke-SystemBenchmark, Invoke-MemoryFlush, Open-WinUI3ControlPanel, Save-OptimizationConfig, Load-OptimizationConfig
