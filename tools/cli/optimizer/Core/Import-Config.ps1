# Import-Config.ps1 - Load & Apply Optimization Settings from JSON Configuration File
param(
    [string]$ConfigFile = "OptimizationConfig.json"
)

$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Definition
$FullPath = Join-Path $ScriptDir $ConfigFile

Write-Host "==========================================" -ForegroundColor Cyan
Write-Host "   Importing Windows Optimization Config  " -ForegroundColor Cyan
Write-Host "==========================================" -ForegroundColor Cyan

if (-not (Test-Path $FullPath)) {
    Write-Error "Configuration file not found: $FullPath"
    exit 1
}

$Config = Get-Content -Path $FullPath -Raw | ConvertFrom-Json
Write-Host "Loaded config created on $($Config.ExportDate) for $($Config.System.ComputerName)" -ForegroundColor Green

# Execute Optimize-Windows.ps1
$OptScript = Join-Path $ScriptDir "Optimize-Windows.ps1"
if (Test-Path $OptScript) {
    Write-Host "Executing main optimization engine..." -ForegroundColor Yellow
    & powershell.exe -ExecutionPolicy Bypass -File $OptScript
} else {
    Write-Error "Optimize-Windows.ps1 script not found!"
}
