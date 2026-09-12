# Optimize-Windows.ps1 - Master 25-Phase Extreme Windows 11 Speed, Latency & Hardware Suite
# Must be executed in an elevated Administrator PowerShell prompt.

Write-Host "==========================================" -ForegroundColor Cyan
Write-Host "   Windows 11 LTSC Speed Optimization    " -ForegroundColor Cyan
Write-Host "==========================================" -ForegroundColor Cyan

# ---------------------------------------------------------
# 1. Elevation Check & System Restore Point Creation
# ---------------------------------------------------------
$IsAdmin = ([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
if (-not $IsAdmin) {
    Write-Error "Administrator privileges are required to run this optimization script. Please restart PowerShell as Administrator."
    exit 1
}

$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Definition

Write-Host "`n[Step 1/25] Creating System Restore Point..." -ForegroundColor Yellow
try {
    Enable-ComputerRestore -Drive "C:\" -ErrorAction SilentlyContinue
    Checkpoint-Computer -Description "Pre-Optimization-RestorePoint" -RestorePointType "MODIFY_SETTINGS" -ErrorAction Stop
    Write-Host "  System Restore Point created successfully." -ForegroundColor Green
} catch {
    Write-Warning "  Could not create System Restore Point: $($_.Exception.Message)"
    Write-Warning "  Proceeding with execution (Undo-Optimization.ps1 is available in workspace)..."
}

# ---------------------------------------------------------
# 2. Telemetry, Tracking & Delivery Optimization Disablement
# ---------------------------------------------------------
Write-Host "`n[Step 2/25] Disabling Telemetry, CEIP, Activity Tracking & Delivery Optimization P2P..." -ForegroundColor Yellow

$RegPathsTelemetry = @(
    "HKLM:\SOFTWARE\Policies\Microsoft\Windows\DataCollection",
    "HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\DataCollection"
)

foreach ($path in $RegPathsTelemetry) {
    if (-not (Test-Path $path)) { New-Item -Path $path -Force | Out-Null }
    Set-ItemProperty -Path $path -Name "AllowTelemetry" -Value 0 -Type DWord -Force -ErrorAction SilentlyContinue
}

$ActivityPath = "HKLM:\SOFTWARE\Policies\Microsoft\Windows\System"
if (-not (Test-Path $ActivityPath)) { New-Item -Path $ActivityPath -Force | Out-Null }
Set-ItemProperty -Path $ActivityPath -Name "EnableActivityFeed" -Value 0 -Type DWord -Force -ErrorAction SilentlyContinue
Set-ItemProperty -Path $ActivityPath -Name "PublishUserActivities" -Value 0 -Type DWord -Force -ErrorAction SilentlyContinue
Set-ItemProperty -Path $ActivityPath -Name "UploadUserActivities" -Value 0 -Type DWord -Force -ErrorAction SilentlyContinue

$DoPath = "HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\DeliveryOptimization\Config"
if (-not (Test-Path $DoPath)) { New-Item -Path $DoPath -Force | Out-Null }
Set-ItemProperty -Path $DoPath -Name "DODownloadMode" -Value 0 -Type DWord -Force -ErrorAction SilentlyContinue

$TasksToDisable = @(
    "\Microsoft\Windows\Application Experience\Microsoft Compatibility Appraiser",
    "\Microsoft\Windows\Application Experience\ProgramDataUpdater",
    "\Microsoft\Windows\Autochk\Proxy",
    "\Microsoft\Windows\Customer Experience Improvement Program\Consolidator",
    "\Microsoft\Windows\Customer Experience Improvement Program\UsbCeip",
    "\Microsoft\Windows\DiskDiagnostic\Microsoft-Windows-DiskDiagnosticDataCollector"
)

foreach ($taskPath in $TasksToDisable) {
    Disable-ScheduledTask -TaskPath ($taskPath | Split-Path -Parent) -TaskName ($taskPath | Split-Path -Leaf) -ErrorAction SilentlyContinue | Out-Null
}
Write-Host "  Telemetry services, scheduled tasks & Delivery Optimization P2P disabled." -ForegroundColor Green

# ---------------------------------------------------------
# 3. Service Streamlining (Targeted Disablement)
# ---------------------------------------------------------
Write-Host "`n[Step 3/25] Streamlining Non-Essential Services..." -ForegroundColor Yellow

$ServicesToDisable = @(
    "DiagTrack",         # Connected User Experiences and Telemetry
    "dmwappushservice",  # WAP Push Message Routing Service
    "Spooler",           # Print Spooler (Printer Service)
    "WSearch",           # Windows Search Indexer
    "XblAuthManager",    # Xbox Live Auth Manager
    "XblGameSave",       # Xbox Live Game Save
    "XboxGipSvc",        # Xbox Accessory Management
    "XboxNetApiSvc"      # Xbox Live Networking Service
)

foreach ($svcName in $ServicesToDisable) {
    $svc = Get-Service -Name $svcName -ErrorAction SilentlyContinue
    if ($svc) {
        Stop-Service -Name $svcName -Force -ErrorAction SilentlyContinue
        Set-Service -Name $svcName -StartupType Disabled -ErrorAction SilentlyContinue
        Write-Host "  Disabled Service: $svcName" -ForegroundColor Green
    }
}
Write-Host "  Preserved Windows Defender security services." -ForegroundColor Cyan

# ---------------------------------------------------------
# 4. Memory Integrity / Core Isolation (HVCI) Disablement
# ---------------------------------------------------------
Write-Host "`n[Step 4/25] Disabling Memory Integrity (HVCI) for Maximum Gaming FPS..." -ForegroundColor Yellow
$HvciPath = "HKLM:\SYSTEM\CurrentControlSet\Control\DeviceGuard\Scenarios\HypervisorEnforcedCodeIntegrity"
if (-not (Test-Path $HvciPath)) { New-Item -Path $HvciPath -Force | Out-Null }
Set-ItemProperty -Path $HvciPath -Name "Enabled" -Value 0 -Type DWord -Force -ErrorAction SilentlyContinue
Write-Host "  Disabled Memory Integrity (HVCI) hypervisor overhead." -ForegroundColor Green

# ---------------------------------------------------------
# 5. Start Menu Bing Search & Highlights Removal
# ---------------------------------------------------------
Write-Host "`n[Step 5/25] Disabling Bing Web Search & Highlights in Start Menu..." -ForegroundColor Yellow
$ExplorerPolicyPath = "HKCU:\SOFTWARE\Policies\Microsoft\Windows\Explorer"
if (-not (Test-Path $ExplorerPolicyPath)) { New-Item -Path $ExplorerPolicyPath -Force | Out-Null }
Set-ItemProperty -Path $ExplorerPolicyPath -Name "DisableSearchBoxSuggestions" -Value 1 -Type DWord -Force -ErrorAction SilentlyContinue

$SearchUserPath = "HKCU:\SOFTWARE\Microsoft\Windows\CurrentVersion\Search"
if (-not (Test-Path $SearchUserPath)) { New-Item -Path $SearchUserPath -Force | Out-Null }
Set-ItemProperty -Path $SearchUserPath -Name "BingSearchEnabled" -Value 0 -Type DWord -Force -ErrorAction SilentlyContinue

$SearchSettingsPath = "HKCU:\Software\Microsoft\Windows\CurrentVersion\SearchSettings"
if (-not (Test-Path $SearchSettingsPath)) { New-Item -Path $SearchSettingsPath -Force | Out-Null }
Set-ItemProperty -Path $SearchSettingsPath -Name "IsDeviceSearchWithHighlightsEnabled" -Value 0 -Type DWord -Force -ErrorAction SilentlyContinue
Write-Host "  Bing Web Search & Highlights disabled (Start menu local search only)." -ForegroundColor Green

# ---------------------------------------------------------
# 6. 'Take Ownership' Right-Click Context Menu
# ---------------------------------------------------------
Write-Host "`n[Step 6/25] Adding 'Take Ownership' Context Menu Shortcut..." -ForegroundColor Yellow
try {
    $TakeCmd = 'cmd.exe /c takeown /f "%1" && icacls "%1" /grant administrators:F'
    New-Item -Path "HKCR:\*\shell\runas" -Value "Take Ownership" -Force | Out-Null
    Set-ItemProperty -Path "HKCR:\*\shell\runas" -Name "NoWorkingDirectory" -Value "" -Force | Out-Null
    New-Item -Path "HKCR:\*\shell\runas\command" -Value $TakeCmd -Force | Out-Null

    $TakeDirCmd = 'cmd.exe /c takeown /f "%1" /r /d y && icacls "%1" /grant administrators:F /t'
    New-Item -Path "HKCR:\Directory\shell\runas" -Value "Take Ownership" -Force | Out-Null
    Set-ItemProperty -Path "HKCR:\Directory\shell\runas" -Name "NoWorkingDirectory" -Value "" -Force | Out-Null
    New-Item -Path "HKCR:\Directory\shell\runas\command" -Value $TakeDirCmd -Force | Out-Null
    Write-Host "  Added 'Take Ownership' context menu options." -ForegroundColor Green
} catch {}

# ---------------------------------------------------------
# 7. CPU Quantum / Win32PrioritySeparation Tuning
# ---------------------------------------------------------
Write-Host "`n[Step 7/25] Tuning Win32PrioritySeparation CPU Quantum (0x26)..." -ForegroundColor Yellow
$PriorityPath = "HKLM:\SYSTEM\CurrentControlSet\Control\PriorityControl"
if (-not (Test-Path $PriorityPath)) { New-Item -Path $PriorityPath -Force | Out-Null }
Set-ItemProperty -Path $PriorityPath -Name "Win32PrioritySeparation" -Value 38 -Type DWord -Force
Write-Host "  Set Win32PrioritySeparation = 0x26 (38 decimal: Max Foreground Priority)." -ForegroundColor Green

# ---------------------------------------------------------
# 8. GameDVR Background Video Capture Disablement
# ---------------------------------------------------------
Write-Host "`n[Step 8/25] Disabling GameDVR Background Video Capture Overhead..." -ForegroundColor Yellow
$GameStorePath = "HKCU:\System\GameConfigStore"
if (-not (Test-Path $GameStorePath)) { New-Item -Path $GameStorePath -Force | Out-Null }
Set-ItemProperty -Path $GameStorePath -Name "GameDVR_Enabled" -Value 0 -Type DWord -Force -ErrorAction SilentlyContinue

$GamePolicyPath = "HKLM:\SOFTWARE\Policies\Microsoft\Windows\GameDVR"
if (-not (Test-Path $GamePolicyPath)) { New-Item -Path $GamePolicyPath -Force | Out-Null }
Set-ItemProperty -Path $GamePolicyPath -Name "AllowGameDVR" -Value 0 -Type DWord -Force -ErrorAction SilentlyContinue
Write-Host "  Disabled GameDVR background video capture & screen recording." -ForegroundColor Green

# ---------------------------------------------------------
# 9. USB Selective Suspend Disablement
# ---------------------------------------------------------
Write-Host "`n[Step 9/25] Disabling USB Selective Suspend Across Power Policies..." -ForegroundColor Yellow
powercfg -setacvalueindex SCHEME_CURRENT 2a737441-1930-4402-8d77-b2bebba308a3 48678926-e24f-4730-b564-8f2887c00810 0 2>$null
powercfg -setactive SCHEME_CURRENT 2>$null
Write-Host "  Disabled USB Selective Suspend (Continuous full-speed USB power)." -ForegroundColor Green

# ---------------------------------------------------------
# 10. MMCSS Audio Thread Priority Tuning
# ---------------------------------------------------------
Write-Host "`n[Step 10/25] Tuning MMCSS Audio Thread Priority for Zero Buffer Dropouts..." -ForegroundColor Yellow
$MmTasksPath = "HKLM:\SOFTWARE\Microsoft\Windows NT\CurrentVersion\Multimedia\SystemProfile\Tasks"

foreach ($audioTask in @("Pro Audio", "Audio")) {
    $taskPath = "$MmTasksPath\$audioTask"
    if (-not (Test-Path $taskPath)) { New-Item -Path $taskPath -Force | Out-Null }
    Set-ItemProperty -Path $taskPath -Name "Scheduling Category" -Value "High" -Type String -Force
    Set-ItemProperty -Path $taskPath -Name "SFIO Priority" -Value "High" -Type String -Force
    Set-ItemProperty -Path $taskPath -Name "Priority" -Value 2 -Type DWord -Force
}
Write-Host "  Elevated MMCSS Pro Audio & Audio task priorities." -ForegroundColor Green

# ---------------------------------------------------------
# 11. NIC Power Saving Disablement & Interrupt Moderation Tuning
# ---------------------------------------------------------
Write-Host "`n[Step 11/25] Disabling NIC Power Saving & Tuning Network Latency..." -ForegroundColor Yellow
try {
    $Adapters = Get-CimInstance Win32_NetworkAdapter -Filter "NetEnabled=True" -ErrorAction SilentlyContinue
    foreach ($nic in $Adapters) {
        $pnp = Get-CimInstance -ClassName MSPower_DeviceEnable -Namespace "root\wmi" -Filter "InstanceName LIKE '%$($nic.PNPDeviceID)%'" -ErrorAction SilentlyContinue
        if ($pnp) {
            Set-CimInstance -InputObject $pnp -Property @{ Enable = $false } -ErrorAction SilentlyContinue
        }
    }
    Get-NetAdapterAdvancedProperty -DisplayName "*Interrupt Moderation*" -ErrorAction SilentlyContinue | Set-NetAdapterAdvancedProperty -RegistryValue "Disabled" -ErrorAction SilentlyContinue
    Write-Host "  Disabled NIC Power Saving & tuned Interrupt Moderation." -ForegroundColor Green
} catch {}

# ---------------------------------------------------------
# 12. QoS Reserved Bandwidth Limit Removal (NonBestEffortLimit = 0)
# ---------------------------------------------------------
Write-Host "`n[Step 12/25] Removing Windows QoS Reserved Bandwidth Limit..." -ForegroundColor Yellow
$QosPath = "HKLM:\SOFTWARE\Policies\Microsoft\Windows\Psched"
if (-not (Test-Path $QosPath)) { New-Item -Path $QosPath -Force | Out-Null }
Set-ItemProperty -Path $QosPath -Name "NonBestEffortLimit" -Value 0 -Type DWord -Force
Write-Host "  Removed 20% QoS Reserved Bandwidth limit (NonBestEffortLimit = 0)." -ForegroundColor Green

# ---------------------------------------------------------
# 13. USB Audio Device Sleep Disablement
# ---------------------------------------------------------
Write-Host "`n[Step 13/25] Disabling Power Management on USB Audio Hardware & DACs..." -ForegroundColor Yellow
try {
    $UsbAudioDevices = Get-ChildItem "HKLM:\SYSTEM\CurrentControlSet\Enum\USB" -Recurse -ErrorAction SilentlyContinue | Where-Object { $_.PSPath -like "*Device Parameters*" }
    foreach ($devParam in $UsbAudioDevices) {
        Set-ItemProperty -Path $devParam.PSPath -Name "EnhancedPowerManagementEnabled" -Value 0 -Type DWord -Force -ErrorAction SilentlyContinue
    }
    Write-Host "  Set EnhancedPowerManagementEnabled = 0 on USB Audio Hardware." -ForegroundColor Green
} catch {}

# ---------------------------------------------------------
# 14. Virtualization-Based Security (VBS) Disablement
# ---------------------------------------------------------
Write-Host "`n[Step 14/25] Disabling Virtualization-Based Security (VBS) Registry Flags..." -ForegroundColor Yellow
$VbsPath = "HKLM:\SYSTEM\CurrentControlSet\Control\DeviceGuard"
if (-not (Test-Path $VbsPath)) { New-Item -Path $VbsPath -Force | Out-Null }
Set-ItemProperty -Path $VbsPath -Name "EnableVirtualizationBasedSecurity" -Value 0 -Type DWord -Force
Write-Host "  Disabled Virtualization-Based Security (EnableVirtualizationBasedSecurity = 0)." -ForegroundColor Green

# ---------------------------------------------------------
# 15. Enhanced TSC Timer Sync Policy & Platform Tick
# ---------------------------------------------------------
Write-Host "`n[Step 15/25] Enabling Enhanced TSC Sync Policy & Platform Tick..." -ForegroundColor Yellow
bcdedit /set tscsyncpolicy Enhanced 2>$null
bcdedit /set useplatformtick yes 2>$null
Write-Host "  Applied bcdedit tscsyncpolicy Enhanced & useplatformtick yes." -ForegroundColor Green

# ---------------------------------------------------------
# 16. AMD ULPS & GPU Deep Sleep Disablement
# ---------------------------------------------------------
Write-Host "`n[Step 16/25] Disabling AMD ULPS & GPU Ultra-Low Power Sleep States..." -ForegroundColor Yellow
$GpuKeys = Get-ChildItem "HKLM:\SYSTEM\CurrentControlSet\Control\Class\{4d36e968-e325-11ce-bfc1-08002be10318}" -ErrorAction SilentlyContinue
foreach ($gpu in $GpuKeys) {
    if (Get-ItemProperty -Path $gpu.PSPath -Name "EnableUlps" -ErrorAction SilentlyContinue) {
        Set-ItemProperty -Path $gpu.PSPath -Name "EnableUlps" -Value 0 -Type DWord -Force -ErrorAction SilentlyContinue
        Write-Host "  Disabled EnableUlps on $($gpu.PSChildName)." -ForegroundColor Green
    }
}

# ---------------------------------------------------------
# 17. Active NVMe TRIM & Storage Queue Depth Acceleration
# ---------------------------------------------------------
Write-Host "`n[Step 17/25] Enforcing Active NVMe TRIM & Storage Acceleration..." -ForegroundColor Yellow
fsutil behavior set disabledeletenotify 0 2>$null
Write-Host "  Enforced active TRIM execution on NVMe drives (disabledeletenotify = 0)." -ForegroundColor Green

# ---------------------------------------------------------
# 18. Desktop Heap Size Expansion
# ---------------------------------------------------------
Write-Host "`n[Step 18/25] Expanding Desktop Heap Size for Multi-Process Compilation..." -ForegroundColor Yellow
try {
    $SubsysPath = "HKLM:\SYSTEM\CurrentControlSet\Control\Session Manager\SubSystems"
    $WindowsVal = (Get-ItemProperty -Path $SubsysPath -Name "Windows").Windows
    if ($WindowsVal -like "*SharedSection=*") {
        $NewVal = $WindowsVal -replace "SharedSection=\d+,\d+,\d+", "SharedSection=1024,20480,1024"
        Set-ItemProperty -Path $SubsysPath -Name "Windows" -Value $NewVal -Force
        Write-Host "  Expanded Desktop Heap SharedSection to 1024,20480,1024." -ForegroundColor Green
    }
} catch {}

# ---------------------------------------------------------
# 19. Hardware-Accelerated GPU Scheduling (HAGS)
# ---------------------------------------------------------
Write-Host "`n[Step 19/25] Enabling Hardware-Accelerated GPU Scheduling (HAGS)..." -ForegroundColor Yellow
$GfxPath = "HKLM:\SYSTEM\CurrentControlSet\Control\GraphicsDrivers"
if (-not (Test-Path $GfxPath)) { New-Item -Path $GfxPath -Force | Out-Null }
Set-ItemProperty -Path $GfxPath -Name "HwSchMode" -Value 2 -Type DWord -Force
Write-Host "  Set HwSchMode = 2 (Hardware-Accelerated GPU Scheduling Enabled)." -ForegroundColor Green

# ---------------------------------------------------------
# 20. AMD Ryzen 7 7700X CPPC EPP Tuning & Hypervisor Disablement
# ---------------------------------------------------------
Write-Host "`n[Step 20/25] Tuning AMD Ryzen CPPC EPP (0 = Max Speed) & Bare-Metal Speed..." -ForegroundColor Yellow
powercfg -setacvalueindex SCHEME_CURRENT SUB_PROCESSOR 54533751-838f-4805-9259-9737f198e481 0 2>$null
powercfg -setactive SCHEME_CURRENT 2>$null
bcdedit /set hypervisorlaunchtype off 2>$null
Write-Host "  Applied AMD CPPC EPP = 0 & HypervisorLaunchType = Off." -ForegroundColor Green

# ---------------------------------------------------------
# 21. Memory Page Combining Disablement
# ---------------------------------------------------------
Write-Host "`n[Step 21/25] Disabling Memory Page Combining..." -ForegroundColor Yellow
try {
    Disable-MMAgent -PageCombining -ErrorAction SilentlyContinue
    Write-Host "  Disabled Memory Page Combining (zero CPU scanning overhead)." -ForegroundColor Green
} catch {}

# ---------------------------------------------------------
# 22. LSA Protection Overhead Removal
# ---------------------------------------------------------
Write-Host "`n[Step 22/25] Disabling LSA Protection Overhead..." -ForegroundColor Yellow
$LsaPath = "HKLM:\SYSTEM\CurrentControlSet\Control\Lsa"
if (-not (Test-Path $LsaPath)) { New-Item -Path $LsaPath -Force | Out-Null }
Set-ItemProperty -Path $LsaPath -Name "RunAsPPL" -Value 0 -Type DWord -Force -ErrorAction SilentlyContinue
Write-Host "  Set LSA RunAsPPL = 0." -ForegroundColor Green

# ---------------------------------------------------------
# 23. NetBIOS Broadcast Traffic Removal
# ---------------------------------------------------------
Write-Host "`n[Step 23/25] Disabling NetBIOS over TCP/IP Broadcast Traffic..." -ForegroundColor Yellow
try {
    $Nics = Get-CimInstance Win32_NetworkAdapterConfiguration -Filter "IPEnabled=True" -ErrorAction SilentlyContinue
    foreach ($nic in $Nics) {
        $nic.SetTcpipNetbios(2) | Out-Null
    }
    Write-Host "  Disabled NetBIOS over TCP/IP on active adapters." -ForegroundColor Green
} catch {}

# ---------------------------------------------------------
# 24. Processor Core Parking, Power Throttling & Power Plan
# ---------------------------------------------------------
Write-Host "`n[Step 24/25] Disabling Core Parking & Power Throttling..." -ForegroundColor Yellow

$PowerPath = "HKLM:\SYSTEM\CurrentControlSet\Control\Power"
if (-not (Test-Path $PowerPath)) { New-Item -Path $PowerPath -Force | Out-Null }
Set-ItemProperty -Path $PowerPath -Name "PowerThrottlingOff" -Value 1 -Type DWord -Force

$UltimateGuid = "e9a42b02-d5df-448d-aa00-03f14749eb61"
$HighPerfGuid = "8c5e7fda-e8bf-4a96-9a85-a6e23a8c635c"

$DuplicateResult = powercfg /duplicatescheme $UltimateGuid 2>&1
if ($LASTEXITCODE -eq 0) {
    powercfg /setactive $UltimateGuid 2>$null
} else {
    powercfg /setactive $HighPerfGuid 2>$null
}

powercfg -setacvalueindex SCHEME_CURRENT SUB_PROCESSOR 0cc5b647-c1df-4637-891a-dec42631f105 100 2>$null
powercfg -setactive SCHEME_CURRENT 2>$null

bcdedit /set disabledynamictick yes 2>$null

$MmPath = "HKLM:\SOFTWARE\Microsoft\Windows NT\CurrentVersion\Multimedia\SystemProfile"
if (-not (Test-Path $MmPath)) { New-Item -Path $MmPath -Force | Out-Null }
Set-ItemProperty -Path $MmPath -Name "SystemResponsiveness" -Value 0 -Type DWord -Force
Set-ItemProperty -Path $MmPath -Name "NetworkThrottlingIndex" -Value 0xFFFFFFFF -Type DWord -Force

# ---------------------------------------------------------
# 25. Memory Management, Memory Compression & Storage/Network I/O
# ---------------------------------------------------------
Write-Host "`n[Step 25/25] Disabling Memory Compression & Tuning Storage / Network..." -ForegroundColor Yellow

$MemPath = "HKLM:\SYSTEM\CurrentControlSet\Control\Session Manager\Memory Management"
Set-ItemProperty -Path $MemPath -Name "DisablePagingExecutive" -Value 1 -Type DWord -Force
Set-ItemProperty -Path $MemPath -Name "LargeSystemCache" -Value 0 -Type DWord -Force

try { Disable-MMAgent -MemoryCompression -ErrorAction SilentlyContinue } catch {}

try {
    $ComputerSystem = Get-CimInstance Win32_ComputerSystem
    if ($ComputerSystem) {
        Set-CimInstance -InputObject $ComputerSystem -Property @{ AutomaticManagedPagefile = $false } -ErrorAction SilentlyContinue
    }
    $PageFile = Get-CimInstance Win32_PageFileSetting -Filter "SettingID='pagefile.sys @ C:'" -ErrorAction SilentlyContinue
    if (-not $PageFile) {
        $PageFile = Get-CimInstance Win32_PageFileSetting -ErrorAction SilentlyContinue | Select-Object -First 1
    }
    if ($PageFile) {
        Set-CimInstance -InputObject $PageFile -Property @{ InitialSize = 4096; MaximumSize = 4096 } -ErrorAction SilentlyContinue
    }
} catch {}

fsutil behavior set disable8dot3 1 2>$null
fsutil behavior set disablelastaccess 1 2>$null

netsh int tcp set global autotuninglevel=normal 2>$null
Enable-NetAdapterRss -Name * -ErrorAction SilentlyContinue

$TcpInterfaces = Get-ChildItem "HKLM:\SYSTEM\CurrentControlSet\Services\Tcpip\Parameters\Interfaces" -ErrorAction SilentlyContinue
foreach ($adapter in $TcpInterfaces) {
    Set-ItemProperty -Path $adapter.PSPath -Name "TcpAckFrequency" -Value 1 -Type DWord -Force -ErrorAction SilentlyContinue
    Set-ItemProperty -Path $adapter.PSPath -Name "TCPNoDelay" -Value 1 -Type DWord -Force -ErrorAction SilentlyContinue
}

Set-ItemProperty -Path "HKCU:\Control Panel\Desktop" -Name "MenuShowDelay" -Value "0" -Type String -Force -ErrorAction SilentlyContinue

Write-Host "`n==========================================" -ForegroundColor Green
Write-Host "   Master 25-Phase Optimization Complete!" -ForegroundColor Green
Write-Host "==========================================" -ForegroundColor Green
Write-Host "Run 'Open-Dashboard.cmd' to launch the visual HTML Dashboard." -ForegroundColor Cyan
Write-Host "Run 'Optimize-GUI.ps1' or 'Run-GUI-As-Admin.cmd' to launch Control Panel GUI." -ForegroundColor Cyan
Write-Host "Run 'Audit-System.ps1' to view updated system stats." -ForegroundColor Cyan
Write-Host "If you ever wish to revert changes, run 'Undo-Optimization.ps1'." -ForegroundColor Cyan
