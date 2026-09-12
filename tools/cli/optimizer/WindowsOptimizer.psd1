@{
    RootModule = 'WindowsOptimizer.psm1'
    ModuleVersion = '1.0.0'
    GUID = 'a4b8c9d1-e2f3-4567-8901-23456789abcd'
    Author = 'Antigravity AI Pair Programmer'
    CompanyName = 'Windows Optimization Suite'
    Copyright = '(c) 2026. All rights reserved.'
    Description = 'Master Windows 11 Speed, DPC Latency, Benchmark & Maintenance Optimization Module'
    PowerShellVersion = '5.1'
    FunctionsToExport = @(
        'Invoke-WindowsOptimization',
        'Invoke-SystemAudit',
        'Invoke-DiskCleanup',
        'Invoke-SystemBenchmark',
        'Invoke-MemoryFlush',
        'Open-WinUI3ControlPanel',
        'Save-OptimizationConfig',
        'Load-OptimizationConfig'
    )
    CmdletsToExport = @()
    VariablesToExport = '*'
    AliasesToExport = @()
}
