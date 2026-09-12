# Export-Config.ps1 - Save Optimization Settings to JSON Configuration File
param(
    [string]$OutputFile = "OptimizationConfig.json"
)

$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Definition
$FullPath = Join-Path $ScriptDir $OutputFile

Write-Host "==========================================" -ForegroundColor Cyan
Write-Host "   Exporting Windows Optimization Config  " -ForegroundColor Cyan
Write-Host "==========================================" -ForegroundColor Cyan

$Config = [ordered]@{
    ExportDate = (Get-Date).ToString("yyyy-MM-dd HH:mm:ss")
    System = @{
        ComputerName = $env:COMPUTERNAME
        OS = (Get-CimInstance Win32_OperatingSystem).Caption
        CPU = (Get-CimInstance Win32_Processor).Name
    }
    Toggles = @{
        DisableTelemetry = $true
        DisableServices = $true
        DisableHVCI = $true
        DisableBingSearch = $true
        AddTakeOwnership = $true
        EnableHAGS = $true
        DisablePowerThrottling = $true
        DisableCoreParking = $true
        Win32PrioritySeparation = "0x26"
        DisableUsbSelectiveSuspend = $true
        DisableGameDVR = $true
        DisableMemoryCompression = $true
        SetStaticPagefile = $true
        TuneMMCSSAudio = $true
        DisableNicPowerSaving = $true
        RegisterWeeklyTask = $true
    }
}

$Config | ConvertTo-Json -Depth 5 | Set-Content -Path $FullPath -Encoding UTF8
Write-Host "`nConfiguration exported successfully to: $FullPath" -ForegroundColor Green
