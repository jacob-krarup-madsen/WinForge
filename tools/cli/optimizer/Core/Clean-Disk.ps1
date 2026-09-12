# Clean-Disk.ps1 - Automated System Storage & DISM Component Store Cleaner
# Must be executed in an elevated Administrator PowerShell prompt.

Write-Host "==========================================" -ForegroundColor Cyan
Write-Host "   Windows 11 DISM & Disk Cleanup Module " -ForegroundColor Cyan
Write-Host "==========================================" -ForegroundColor Cyan

$IsAdmin = ([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
if (-not $IsAdmin) {
    Write-Error "Administrator privileges are required to run this disk cleanup script."
    exit 1
}

# 1. DISM Component Store Cleanup & ResetBase
Write-Host "`n[1/4] Running DISM Component Store Cleanup (ResetBase)..." -ForegroundColor Yellow
try {
    dism.exe /Online /Cleanup-Image /StartComponentCleanup /ResetBase
    Write-Host "  DISM Component Store cleaned successfully." -ForegroundColor Green
} catch {
    Write-Warning "  DISM Cleanup encountered an issue: $($_.Exception.Message)"
}

# 2. System & User Temp Files Cleanup
Write-Host "`n[2/4] Purging System & User Temporary Files..." -ForegroundColor Yellow
$TempFolders = @(
    "C:\Windows\Temp\*",
    "$env:LOCALAPPDATA\Temp\*"
)

foreach ($folder in $TempFolders) {
    Get-ChildItem -Path $folder -Recurse -ErrorAction SilentlyContinue | Remove-Item -Recurse -Force -ErrorAction SilentlyContinue
}
Write-Host "  Temporary files purged." -ForegroundColor Green

# 3. Crash Dumps & Windows Error Reporting (WER) Purge
Write-Host "`n[3/4] Clearing Memory Dumps & WER Error Reports..." -ForegroundColor Yellow
$DumpPaths = @(
    "C:\Windows\MEMORY.DMP",
    "C:\Windows\Minidump\*",
    "C:\ProgramData\Microsoft\Windows\WER\ReportArchive\*",
    "C:\ProgramData\Microsoft\Windows\WER\ReportQueue\*"
)

foreach ($dump in $DumpPaths) {
    Remove-Item -Path $dump -Recurse -Force -ErrorAction SilentlyContinue
}
Write-Host "  Memory dumps & WER error reports cleared." -ForegroundColor Green

# 4. Windows Log Files Cleanup
Write-Host "`n[4/4] Trimming System Log Files..." -ForegroundColor Yellow
Get-ChildItem -Path "C:\Windows\Logs\*" -Recurse -ErrorAction SilentlyContinue | Remove-Item -Recurse -Force -ErrorAction SilentlyContinue
Write-Host "  Log files trimmed." -ForegroundColor Green

Write-Host "`n==========================================" -ForegroundColor Green
Write-Host "   Disk & DISM Cleanup Complete!         " -ForegroundColor Green
Write-Host "==========================================" -ForegroundColor Green
