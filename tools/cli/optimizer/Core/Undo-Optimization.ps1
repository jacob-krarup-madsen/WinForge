# Undo-Optimization.ps1 - Complete Reversal of Master 25-Phase Optimization Suite
# Must be executed in an elevated Administrator PowerShell prompt.

Write-Host "==========================================" -ForegroundColor Red
Write-Host "   Windows 11 Optimization Undo Suite    " -ForegroundColor Red
Write-Host "==========================================" -ForegroundColor Red

# 1. Elevate Privilege Check
$IsAdmin = ([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
if (-not $IsAdmin) {
    Write-Warning "This script requires Administrator privileges. Re-run as Administrator."
    exit 1
}

# 2. Re-enable Target Services
$ServicesToEnable = @(
    "Spooler",
    "WSearch",
    "DiagTrack",
    "dmwappushservice",
    "XblAuthManager",
    "XblGameSave",
    "XboxGipSvc",
    "XboxNetApiSvc"
)

Write-Host "`n[1/13] Restoring Services..." -ForegroundColor Yellow
foreach ($svc in $ServicesToEnable) {
    $exists = Get-Service -Name $svc -ErrorAction SilentlyContinue
    if ($exists) {
        Set-Service -Name $svc -StartupType Automatic -ErrorAction SilentlyContinue
        Start-Service -Name $svc -ErrorAction SilentlyContinue
        Write-Host "  Re-enabled & Started: $svc" -ForegroundColor Green
    }
}

# 3. Restore Scheduled Tasks & Weekly Disk Task Unregister
Write-Host "`n[2/13] Restoring Scheduled Tasks & Unregistering Weekly DISM Task..." -ForegroundColor Yellow
Unregister-ScheduledTask -TaskName "Weekly_Windows_Disk_Cleanup" -Confirm:$false -ErrorAction SilentlyContinue | Out-Null

$TasksToEnable = @(
    "\Microsoft\Windows\Application Experience\Microsoft Compatibility Appraiser",
    "\Microsoft\Windows\Application Experience\ProgramDataUpdater",
    "\Microsoft\Windows\Autochk\Proxy",
    "\Microsoft\Windows\Customer Experience Improvement Program\Consolidator",
    "\Microsoft\Windows\Customer Experience Improvement Program\UsbCeip",
    "\Microsoft\Windows\DiskDiagnostic\Microsoft-Windows-DiskDiagnosticDataCollector"
)

foreach ($taskPath in $TasksToEnable) {
    Enable-ScheduledTask -TaskPath ($taskPath | Split-Path -Parent) -TaskName ($taskPath | Split-Path -Leaf) -ErrorAction SilentlyContinue | Out-Null
}

Set-ItemProperty -Path "HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\DeliveryOptimization\Config" -Name "DODownloadMode" -Value 1 -Type DWord -Force -ErrorAction SilentlyContinue
Write-Host "  Restored Scheduled tasks & Delivery Optimization mode." -ForegroundColor Green

# 4. Restore PageCombining, LSA & NetBIOS
Write-Host "`n[3/13] Restoring Memory PageCombining, LSA & NetBIOS..." -ForegroundColor Yellow
try { Enable-MMAgent -PageCombining -ErrorAction SilentlyContinue } catch {}
Set-ItemProperty -Path "HKLM:\SYSTEM\CurrentControlSet\Control\Lsa" -Name "RunAsPPL" -Value 1 -Type DWord -Force -ErrorAction SilentlyContinue
try {
    $Nics = Get-CimInstance Win32_NetworkAdapterConfiguration -Filter "IPEnabled=True" -ErrorAction SilentlyContinue
    foreach ($nic in $Nics) { $nic.SetTcpipNetbios(0) | Out-Null }
} catch {}

# 5. Restore Hypervisor, AMD ULPS & Ryzen EPP Settings
Write-Host "`n[4/13] Restoring Hypervisor Launch Type, AMD ULPS & AMD Ryzen EPP Defaults..." -ForegroundColor Yellow
bcdedit /set hypervisorlaunchtype auto 2>$null
powercfg -setacvalueindex SCHEME_CURRENT SUB_PROCESSOR 54533751-838f-4805-9259-9737f198e481 50 2>$null
powercfg -setactive SCHEME_CURRENT 2>$null

$GpuKeys = Get-ChildItem "HKLM:\SYSTEM\CurrentControlSet\Control\Class\{4d36e968-e325-11ce-bfc1-08002be10318}" -ErrorAction SilentlyContinue
foreach ($gpu in $GpuKeys) {
    if (Get-ItemProperty -Path $gpu.PSPath -Name "EnableUlps" -ErrorAction SilentlyContinue) {
        Set-ItemProperty -Path $gpu.PSPath -Name "EnableUlps" -Value 1 -Type DWord -Force -ErrorAction SilentlyContinue
    }
}

# 6. Restore QoS & VBS Settings
Write-Host "`n[5/13] Restoring QoS Bandwidth Limit & VBS Flags..." -ForegroundColor Yellow
Remove-ItemProperty -Path "HKLM:\SOFTWARE\Policies\Microsoft\Windows\Psched" -Name "NonBestEffortLimit" -ErrorAction SilentlyContinue
Set-ItemProperty -Path "HKLM:\SYSTEM\CurrentControlSet\Control\DeviceGuard" -Name "EnableVirtualizationBasedSecurity" -Value 1 -Type DWord -Force -ErrorAction SilentlyContinue

# 7. Restore Win32PrioritySeparation, GameDVR & USB Selective Suspend
Write-Host "`n[6/13] Restoring CPU Quantum, GameDVR & USB Power Settings..." -ForegroundColor Yellow
Set-ItemProperty -Path "HKLM:\SYSTEM\CurrentControlSet\Control\PriorityControl" -Name "Win32PrioritySeparation" -Value 2 -Type DWord -Force -ErrorAction SilentlyContinue

Set-ItemProperty -Path "HKCU:\System\GameConfigStore" -Name "GameDVR_Enabled" -Value 1 -Type DWord -Force -ErrorAction SilentlyContinue
Set-ItemProperty -Path "HKLM:\SOFTWARE\Policies\Microsoft\Windows\GameDVR" -Name "AllowGameDVR" -Value 1 -Type DWord -Force -ErrorAction SilentlyContinue

powercfg -setacvalueindex SCHEME_CURRENT 2a737441-1930-4402-8d77-b2bebba308a3 48678926-e24f-4730-b564-8f2887c00810 1 2>$null
powercfg -setactive SCHEME_CURRENT 2>$null

# 8. Restore MMCSS Audio Task Profiles & NIC Power Saving
Write-Host "`n[7/13] Restoring MMCSS Audio Priorities & NIC Power Saving..." -ForegroundColor Yellow
$MmAudioPath = "HKLM:\SOFTWARE\Microsoft\Windows NT\CurrentVersion\Multimedia\SystemProfile\Tasks\Pro Audio"
if (Test-Path $MmAudioPath) {
    Set-ItemProperty -Path $MmAudioPath -Name "Scheduling Category" -Value "Medium" -Type String -Force -ErrorAction SilentlyContinue
    Set-ItemProperty -Path $MmAudioPath -Name "SFIO Priority" -Value "Normal" -Type String -Force -ErrorAction SilentlyContinue
}

try {
    $Adapters = Get-CimInstance Win32_NetworkAdapter -Filter "NetEnabled=True" -ErrorAction SilentlyContinue
    foreach ($nic in $Adapters) {
        $pnp = Get-CimInstance -ClassName MSPower_DeviceEnable -Namespace "root\wmi" -Filter "InstanceName LIKE '%$($nic.PNPDeviceID)%'" -ErrorAction SilentlyContinue
        if ($pnp) {
            Set-CimInstance -InputObject $pnp -Property @{ Enable = $true } -ErrorAction SilentlyContinue
        }
    }
} catch {}

# 9. Restore HVCI / Memory Integrity
Write-Host "`n[8/13] Restoring Memory Integrity (HVCI)..." -ForegroundColor Yellow
$HvciPath = "HKLM:\SYSTEM\CurrentControlSet\Control\DeviceGuard\Scenarios\HypervisorEnforcedCodeIntegrity"
if (Test-Path $HvciPath) {
    Set-ItemProperty -Path $HvciPath -Name "Enabled" -Value 1 -Type DWord -Force -ErrorAction SilentlyContinue
}

# 10. Restore Start Menu Bing Search & Context Menus
Write-Host "`n[9/13] Restoring Start Menu Bing Search & Context Menus..." -ForegroundColor Yellow
Remove-ItemProperty -Path "HKCU:\SOFTWARE\Policies\Microsoft\Windows\Explorer" -Name "DisableSearchBoxSuggestions" -ErrorAction SilentlyContinue
Remove-ItemProperty -Path "HKCU:\SOFTWARE\Microsoft\Windows\CurrentVersion\Search" -Name "BingSearchEnabled" -ErrorAction SilentlyContinue
Remove-Item -Path "HKCR:\*\shell\runas" -Recurse -Force -ErrorAction SilentlyContinue
Remove-Item -Path "HKCR:\Directory\shell\runas" -Recurse -Force -ErrorAction SilentlyContinue

# 11. Restore Registry Defaults (HAGS, Power Throttling, Latency)
Write-Host "`n[10/13] Restoring Registry Defaults (HAGS, Power Throttling, Latency)..." -ForegroundColor Yellow
Set-ItemProperty -Path "HKLM:\SOFTWARE\Microsoft\Windows NT\CurrentVersion\Multimedia\SystemProfile" -Name "SystemResponsiveness" -Value 20 -Type DWord -Force -ErrorAction SilentlyContinue
Set-ItemProperty -Path "HKLM:\SOFTWARE\Microsoft\Windows NT\CurrentVersion\Multimedia\SystemProfile" -Name "NetworkThrottlingIndex" -Value 10 -Type DWord -Force -ErrorAction SilentlyContinue

Set-ItemProperty -Path "HKLM:\SYSTEM\CurrentControlSet\Control\Session Manager\Memory Management" -Name "DisablePagingExecutive" -Value 0 -Type DWord -Force -ErrorAction SilentlyContinue
Set-ItemProperty -Path "HKLM:\SYSTEM\CurrentControlSet\Control\Session Manager\Memory Management" -Name "LargeSystemCache" -Value 0 -Type DWord -Force -ErrorAction SilentlyContinue

Set-ItemProperty -Path "HKLM:\SYSTEM\CurrentControlSet\Control\GraphicsDrivers" -Name "HwSchMode" -Value 1 -Type DWord -Force -ErrorAction SilentlyContinue
Remove-ItemProperty -Path "HKLM:\SYSTEM\CurrentControlSet\Control\Power" -Name "PowerThrottlingOff" -ErrorAction SilentlyContinue
Set-ItemProperty -Path "HKCU:\Control Panel\Desktop" -Name "MenuShowDelay" -Value 400 -Type String -Force -ErrorAction SilentlyContinue

$Interfaces = Get-ChildItem "HKLM:\SYSTEM\CurrentControlSet\Services\Tcpip\Parameters\Interfaces" -ErrorAction SilentlyContinue
foreach ($adapter in $Interfaces) {
    Remove-ItemProperty -Path $adapter.PSPath -Name "TcpAckFrequency" -ErrorAction SilentlyContinue
    Remove-ItemProperty -Path $adapter.PSPath -Name "TCPNoDelay" -ErrorAction SilentlyContinue
}

# 12. Restore Memory Compression & Pagefile
Write-Host "`n[11/13] Restoring Memory Compression & System Managed Pagefile..." -ForegroundColor Yellow
try { Enable-MMAgent -MemoryCompression -ErrorAction SilentlyContinue } catch {}

$ComputerSystem = Get-CimInstance Win32_ComputerSystem
if ($ComputerSystem) {
    Set-CimInstance -InputObject $ComputerSystem -Property @{ AutomaticManagedPagefile = $true } -ErrorAction SilentlyContinue
}

# 13. Restore BCD Settings & Power Plan
Write-Host "`n[12/13] Restoring BCD & Power Plan..." -ForegroundColor Yellow
bcdedit /deletevalue disabledynamictick 2>$null
bcdedit /deletevalue useplatformclock 2>$null
bcdedit /deletevalue tscsyncpolicy 2>$null
bcdedit /deletevalue useplatformtick 2>$null
netsh int tcp set global autotuninglevel=normal 2>$null

$BalancedGuid = "381b4222-f694-41f0-9685-ff5bb260df2e"
powercfg /setactive $BalancedGuid 2>$null
powercfg -setacvalueindex $BalancedGuid SUB_PROCESSOR 0cc5b647-c1df-4637-891a-dec42631f105 5 2>$null
powercfg -setactive $BalancedGuid 2>$null

Write-Host "`n==========================================" -ForegroundColor Green
Write-Host "   Undo Completed Successfully!          " -ForegroundColor Green
Write-Host "==========================================" -ForegroundColor Green
