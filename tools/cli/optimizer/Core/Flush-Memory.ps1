# Flush-Memory.ps1 - RAM Working Set & Standby List Memory Cleaner
# Trims process working sets to instantly release cached memory back to Windows

Write-Host "==========================================" -ForegroundColor Cyan
Write-Host "   Windows 11 Memory Standby & RAM Cleaner" -ForegroundColor Cyan
Write-Host "==========================================" -ForegroundColor Cyan

$OS = Get-CimInstance Win32_OperatingSystem
$BeforeFreeGB = [math]::Round($OS.FreePhysicalMemory / 1MB, 2)
Write-Host "Initial Free RAM: ${BeforeFreeGB} GB" -ForegroundColor Yellow

# Trigger Process Working Set Trimming
Write-Host "`nTrimming working sets across running processes..." -ForegroundColor Yellow

$Code = @"
using System;
using System.Runtime.InteropServices;
public class MemoryCleaner {
    [DllImport("psapi.dll")]
    public static extern int EmptyWorkingSet(IntPtr hwnd);
}
"@
Add-Type -TypeDefinition $Code -ErrorAction SilentlyContinue

$Processes = Get-Process -ErrorAction SilentlyContinue
$TrimmedCount = 0

foreach ($proc in $Processes) {
    try {
        if ($proc.Handle -ne [IntPtr]::Zero) {
            [MemoryCleaner]::EmptyWorkingSet($proc.Handle) | Out-Null
            $TrimmedCount++
        }
    } catch {}
}

[GC]::Collect()
[GC]::WaitForPendingFinalizers()

$OSAfter = Get-CimInstance Win32_OperatingSystem
$AfterFreeGB = [math]::Round($OSAfter.FreePhysicalMemory / 1MB, 2)
$FreedGB = [math]::Round($AfterFreeGB - $BeforeFreeGB, 2)

Write-Host "`nProcess Working Sets Trimmed: $TrimmedCount" -ForegroundColor Green
Write-Host "Updated Free RAM: ${AfterFreeGB} GB" -ForegroundColor Green
if ($FreedGB -gt 0) {
    Write-Host "Freed Memory: +${FreedGB} GB RAM" -ForegroundColor Green
}
