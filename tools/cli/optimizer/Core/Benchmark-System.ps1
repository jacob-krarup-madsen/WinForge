# Benchmark-System.ps1 - Automated System Performance & Latency Benchmark
param(
    [string]$ReportFile = "BenchmarkReport.txt"
)

$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Definition
$ReportPath = Join-Path $ScriptDir $ReportFile

Write-Host "==========================================" -ForegroundColor Cyan
Write-Host "   Windows 11 System Performance Benchmark" -ForegroundColor Cyan
Write-Host "==========================================" -ForegroundColor Cyan
Write-Host "Running benchmarks on AMD Ryzen 7 7700X..." -ForegroundColor Yellow

$BenchmarkDate = (Get-Date).ToString("yyyy-MM-dd HH:mm:ss")
$OS = Get-CimInstance Win32_OperatingSystem
$CPU = Get-CimInstance Win32_Processor
$RAM_TotalGB = [math]::Round($OS.TotalVisibleMemorySize / 1MB, 2)
$RAM_FreeGB  = [math]::Round($OS.FreePhysicalMemory / 1MB, 2)

# Benchmark 1: CPU Multi-Thread Prime Calculation Time
Write-Host "`n[1/3] Benchmarking CPU Multi-Core Performance (1,000,000 Operations)..." -ForegroundColor Yellow
$swCpu = [System.Diagnostics.Stopwatch]::StartNew()
1..1000000 | ForEach-Object { $_ * $_ } | Out-Null
$swCpu.Stop()
$CpuBenchmarkMs = [math]::Round($swCpu.Elapsed.TotalMilliseconds, 2)
Write-Host "  CPU Benchmark Execution Time: ${CpuBenchmarkMs} ms" -ForegroundColor Green

# Benchmark 2: RAM Memory Allocation & Throughput Benchmark
Write-Host "`n[2/3] Benchmarking RAM Allocation Speed (100MB Memory Block)..." -ForegroundColor Yellow
$swRam = [System.Diagnostics.Stopwatch]::StartNew()
$dummyArray = New-Object byte[] (100 * 1024 * 1024)
for ($i = 0; $i -lt $dummyArray.Length; $i += 4096) { $dummyArray[$i] = 1 }
$swRam.Stop()
$dummyArray = $null
[GC]::Collect()
$RamBenchmarkMs = [math]::Round($swRam.Elapsed.TotalMilliseconds, 2)
Write-Host "  RAM Allocation & Touch Time: ${RamBenchmarkMs} ms" -ForegroundColor Green

# Benchmark 3: System Responsiveness & Active Process Count
Write-Host "`n[3/3] Auditing System Responsiveness & Process Density..." -ForegroundColor Yellow
$ProcessCount = (Get-Process).Count
$ServiceCount = (Get-Service | Where-Object Status -eq 'Running').Count
$Hags = (Get-ItemProperty "HKLM:\SYSTEM\CurrentControlSet\Control\GraphicsDrivers" -Name "HwSchMode" -ErrorAction SilentlyContinue).HwSchMode
$PrioritySep = (Get-ItemProperty "HKLM:\SYSTEM\CurrentControlSet\Control\PriorityControl" -Name "Win32PrioritySeparation" -ErrorAction SilentlyContinue).Win32PrioritySeparation

# Generate Formatted Benchmark Report
$ReportLines = @(
    "=================================================="
    "   WINDOWS 11 SYSTEM PERFORMANCE BENCHMARK REPORT "
    "=================================================="
    "Date/Time:             $BenchmarkDate"
    "Computer Name:         $env:COMPUTERNAME"
    "OS Edition:            $($OS.Caption) ($($OS.Version))"
    "CPU Model:             $($CPU.Name)"
    "Total Installed RAM:   ${RAM_TotalGB} GB"
    "Currently Free RAM:    ${RAM_FreeGB} GB"
    "--------------------------------------------------"
    "CPU Execution Time:    ${CpuBenchmarkMs} ms (Lower is Faster)"
    "RAM Allocation Time:   ${RamBenchmarkMs} ms (Lower is Faster)"
    "Active Processes:      $ProcessCount"
    "Running Services:      $ServiceCount"
    "Win32PrioritySeparation: $PrioritySep"
    "HAGS State (HwSchMode): $Hags"
    "=================================================="
)

$ReportLines | Set-Content -Path $ReportPath -Encoding UTF8
Write-Host "`nBenchmark report generated: $ReportPath" -ForegroundColor Green
