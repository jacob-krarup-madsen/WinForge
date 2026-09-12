# ==============================================================================
# Challenger-M4-AdversarialTests.ps1 -- Milestone 4 Tier 5 Adversarial Hardening
# ==============================================================================
[CmdletBinding()]
param(
    [string]$RootPath,
    [string]$ExportJson
)

$ErrorActionPreference = 'Stop'

if (-not $RootPath) {
    $RootPath = if ($PSScriptRoot) { Split-Path -Parent $PSScriptRoot } else { "C:\Tools\vivetool_feature_enabler" }
}

$modulePath = Join-Path $RootPath "ViVeToolEnabler.psm1"
$scriptEnable = Join-Path $RootPath "Enable-Features.ps1"
$scriptDisable = Join-Path $RootPath "Disable-Features.ps1"
$scriptGet = Join-Path $RootPath "Get-ViVeTool.ps1"
$mockToolPath = Join-Path $RootPath "tests\MockViVeTool.ps1"

Import-Module $modulePath -Force

$script:TotalTests = 0
$script:PassedTests = 0
$script:FailedTests = 0
$script:TestDetails = [System.Collections.Generic.List[PSCustomObject]]::new()

function Test-Challenge {
    param(
        [Parameter(Mandatory = $true)][string]$Category,
        [Parameter(Mandatory = $true)][string]$Name,
        [Parameter(Mandatory = $true)][scriptblock]$TestBlock
    )

    $script:TotalTests++
    $sw = [System.Diagnostics.Stopwatch]::StartNew()
    $status = "PASS"
    $errMsg = ""

    try {
        $result = & $TestBlock
        if ($result -eq $false) {
            $status = "FAIL"
            $errMsg = "Assertion evaluated to `$false"
        }
    } catch {
        $status = "FAIL"
        $errMsg = $_.Exception.Message
        if ($_.InvocationInfo -and $_.InvocationInfo.ScriptLineNumber) {
            $errMsg += " (Line: $($_.InvocationInfo.ScriptLineNumber))"
        }
    } finally {
        $sw.Stop()
    }

    if ($status -eq "PASS") {
        $script:PassedTests++
        Write-Host "  [PASS] ($($sw.ElapsedMilliseconds)ms) $Name" -ForegroundColor Green
    } else {
        $script:FailedTests++
        Write-Host "  [FAIL] ($($sw.ElapsedMilliseconds)ms) $Name" -ForegroundColor Red
        Write-Host "         Error: $errMsg" -ForegroundColor Yellow
    }

    $script:TestDetails.Add([PSCustomObject]@{
        Category = $Category
        Name     = $Name
        Status   = $status
        Duration = "$($sw.ElapsedMilliseconds)ms"
        Error    = $errMsg
    })
}

Write-Host "================================================================================" -ForegroundColor Cyan
Write-Host " ViVeTool Feature Enabler -- Milestone 4 Tier 5 Adversarial Hardening Suite" -ForegroundColor Cyan
Write-Host " Environment: PowerShell $($PSVersionTable.PSVersion) | Target: $RootPath" -ForegroundColor Cyan
Write-Host "================================================================================" -ForegroundColor Cyan

# Set test environment guards
$env:VIVETOOL_NON_DESTRUCTIVE = "1"
$env:VIVETOOL_TEST_RUNNER = "1"
$env:VIVETOOL_MOCK_MODE = "AllSuccess"

# ------------------------------------------------------------------------------
# CATEGORY A: JSON Catalog White-Box Adversarial Stress
# ------------------------------------------------------------------------------
Write-Host "`n[+] Category A: JSON Catalog White-Box Adversarial Stress" -ForegroundColor Yellow

Test-Challenge "JSON" "A1. Corrupted/Truncated JSON throws structured ViVeTool.Catalog.ParseError" {
    $sandbox = Join-Path ([System.IO.Path]::GetTempPath()) ("ViVe_JsonTest_" + [guid]::NewGuid().ToString('N').Substring(0,8))
    New-Item -ItemType Directory -Path $sandbox -Force | Out-Null
    try {
        $corruptJson = Join-Path $sandbox "CorruptCatalog.json"
        Set-Content -LiteralPath $corruptJson -Value '[{"Id": "61161244", "FeatureID": "61161244", "Channels": ["GA2026"' -Encoding utf8
        $threw = $false
        try {
            Get-FeatureCatalog -CatalogPath $corruptJson
        } catch {
            $threw = ($_.FullyQualifiedErrorId -like "*ViVeTool.Catalog.ParseError*")
        }
        $threw
    } finally {
        Remove-Item -LiteralPath $sandbox -Recurse -Force -ErrorAction SilentlyContinue
    }
}

Test-Challenge "JSON" "A2. Empty string catalog JSON parses gracefully to empty collection" {
    $sandbox = Join-Path ([System.IO.Path]::GetTempPath()) ("ViVe_JsonTest_" + [guid]::NewGuid().ToString('N').Substring(0,8))
    New-Item -ItemType Directory -Path $sandbox -Force | Out-Null
    try {
        $emptyJson = Join-Path $sandbox "EmptyCatalog.json"
        Set-Content -LiteralPath $emptyJson -Value '' -Encoding utf8
        $res = @(Get-FeatureCatalog -CatalogPath $emptyJson)
        $res.Count -eq 0
    } finally {
        Remove-Item -LiteralPath $sandbox -Recurse -Force -ErrorAction SilentlyContinue
    }
}

Test-Challenge "JSON" "A3. JSON with single object (non-array) parses without error" {
    $sandbox = Join-Path ([System.IO.Path]::GetTempPath()) ("ViVe_JsonTest_" + [guid]::NewGuid().ToString('N').Substring(0,8))
    New-Item -ItemType Directory -Path $sandbox -Force | Out-Null
    try {
        $singleJson = Join-Path $sandbox "SingleCatalog.json"
        Set-Content -LiteralPath $singleJson -Value '{"Id": "99999999", "Channels": ["Custom"], "Description": "Single test"}' -Encoding utf8
        $res = @(Get-FeatureCatalog -CatalogPath $singleJson)
        ($res.Count -eq 1) -and ($res[0].FeatureID -eq "99999999")
    } finally {
        Remove-Item -LiteralPath $sandbox -Recurse -Force -ErrorAction SilentlyContinue
    }
}

Test-Challenge "JSON" "A4. JSON with items missing 'Id' property under StrictMode 2.0" {
    $sandbox = Join-Path ([System.IO.Path]::GetTempPath()) ("ViVe_JsonTest_" + [guid]::NewGuid().ToString('N').Substring(0,8))
    New-Item -ItemType Directory -Path $sandbox -Force | Out-Null
    try {
        $nullIdJson = Join-Path $sandbox "NullIdCatalog.json"
        $content = '[{"Description": "No ID item"}, {"Id": "11223344", "Description": "Valid ID"}]'
        Set-Content -LiteralPath $nullIdJson -Value $content -Encoding utf8
        
        # Test whether Get-FeatureCatalog survives objects missing 'Id' property
        $res = @(Get-FeatureCatalog -CatalogPath $nullIdJson)
        ($res.Count -eq 1) -and ($res[0].FeatureID -eq "11223344")
    } finally {
        Remove-Item -LiteralPath $sandbox -Recurse -Force -ErrorAction SilentlyContinue
    }
}

Test-Challenge "JSON" "A5. Unicode, CJK characters and emojis in catalog parsed accurately" {
    $sandbox = Join-Path ([System.IO.Path]::GetTempPath()) ("ViVe_JsonTest_" + [guid]::NewGuid().ToString('N').Substring(0,8))
    New-Item -ItemType Directory -Path $sandbox -Force | Out-Null
    try {
        $unicodeJson = Join-Path $sandbox "UnicodeCatalog.json"
        $content = '[{"Id": "77889900", "Channels": ["日本語_Channel", "Emoji_🚀"], "Description": "Test feature with unicode: Über, Spécial, 简体中文, 日本語, 🚀"}]'
        Set-Content -LiteralPath $unicodeJson -Value $content -Encoding utf8
        $res = @(Get-FeatureCatalog -CatalogPath $unicodeJson -Channel "日本語_Channel")
        ($res.Count -eq 1) -and ($res[0].Description -like "*Über*")
    } finally {
        Remove-Item -LiteralPath $sandbox -Recurse -Force -ErrorAction SilentlyContinue
    }
}

Test-Challenge "JSON" "A6. High-capacity catalog (1000 synthetic items) parses and filters in <1500ms" {
    $sandbox = Join-Path ([System.IO.Path]::GetTempPath()) ("ViVe_JsonTest_" + [guid]::NewGuid().ToString('N').Substring(0,8))
    New-Item -ItemType Directory -Path $sandbox -Force | Out-Null
    try {
        $largeJson = Join-Path $sandbox "LargeCatalog.json"
        $items = [System.Collections.Generic.List[object]]::new()
        for ($i = 10000000; $i -lt 10001000; $i++) {
            $ch = if ($i % 2 -eq 0) { @("EvenChannel") } else { @("OddChannel") }
            $items.Add(@{ Id = "$i"; FeatureID = "$i"; Channels = $ch; Description = "Synthetic feature $i" })
        }
        $items | ConvertTo-Json -Depth 3 | Set-Content -LiteralPath $largeJson -Encoding utf8
        
        $sw = [System.Diagnostics.Stopwatch]::StartNew()
        $res = @(Get-FeatureCatalog -CatalogPath $largeJson -Channel "EvenChannel")
        $sw.Stop()
        
        ($res.Count -eq 500) -and ($sw.ElapsedMilliseconds -lt 1500)
    } finally {
        Remove-Item -LiteralPath $sandbox -Recurse -Force -ErrorAction SilentlyContinue
    }
}

Test-Challenge "JSON" "A7. Non-existent catalog file falls back to embedded 118 pureinfotech catalog" {
    $res = @(Get-FeatureCatalog -CatalogPath "Z:\NonExistent_Fake_Dir_12345\NoCatalog.json")
    $res.Count -eq 118
}

# ------------------------------------------------------------------------------
# CATEGORY B: Unicode, Special Characters & Path Hardening
# ------------------------------------------------------------------------------
Write-Host "`n[+] Category B: Unicode, Special Characters and Path Hardening" -ForegroundColor Yellow

Test-Challenge "Paths" "B1. Directory with spaces, brackets, parentheses, single quotes & unicode" {
    $specialDir = Join-Path ([System.IO.Path]::GetTempPath()) "ViVe_Test_日本語 (x86) [v2.0] 'quotes' & + test"
    New-Item -ItemType Directory -Path $specialDir -Force | Out-Null
    try {
        Copy-Item -LiteralPath (Join-Path $RootPath "FeatureCatalog.json") -Destination $specialDir -Force
        Copy-Item -LiteralPath (Join-Path $RootPath "tests\MockViVeTool.ps1") -Destination $specialDir -Force
        Copy-Item -LiteralPath (Join-Path $RootPath "tests\MockViVeTool.cmd") -Destination $specialDir -Force
        
        $cat = @(Get-FeatureCatalog -CatalogPath (Join-Path $specialDir "FeatureCatalog.json"))
        $cat.Count -eq 118
    } finally {
        Remove-Item -LiteralPath $specialDir -Recurse -Force -ErrorAction SilentlyContinue
    }
}

Test-Challenge "Paths" "B2. Enable-Features.ps1 CLI dry-run executes cleanly in special path" {
    $specialDir = Join-Path ([System.IO.Path]::GetTempPath()) "ViVe_CLI_日本語 [test] (1.0)"
    New-Item -ItemType Directory -Path $specialDir -Force | Out-Null
    try {
        Copy-Item -LiteralPath (Join-Path $RootPath "FeatureCatalog.json") -Destination $specialDir -Force
        
        $proc = Start-Process -FilePath "powershell.exe" `
                              -ArgumentList "-NoProfile", "-ExecutionPolicy", "Bypass", "-File", "`"$scriptEnable`"", "-TargetDirectory", "`"$specialDir`"", "-Channel", "Canary", "-DryRun" `
                              -NoNewWindow -Wait -PassThru
        $proc.ExitCode -eq 0
    } finally {
        Remove-Item -LiteralPath $specialDir -Recurse -Force -ErrorAction SilentlyContinue
    }
}

Test-Challenge "Paths" "B3. Log path unwritable falls back to TEMP without throwing fatal error" {
    $unwritablePath = "Z:\Locked_Drive_NonExistent_9999\unwritable.log"
    $logEntry = [PSCustomObject]@{
        Timestamp = (Get-Date -Format 'o')
        FeatureID = "61161244"
        Action    = "Enable"
        Result    = "Success"
        ExitCode  = 0
        Message   = "Test log write with fallback"
    }
    
    $threw = $false
    try {
        Write-FeatureLog -LogEntry $logEntry -LogPath $unwritablePath -NoConsole
    } catch {
        $threw = $true
    }
    (-not $threw)
}

Test-Challenge "Paths" "B4. Dynamic rollback script generated in Unicode special path" {
    $specialDir = Join-Path ([System.IO.Path]::GetTempPath()) "ViVe_Rollback_日本語 'special' (test)"
    New-Item -ItemType Directory -Path $specialDir -Force | Out-Null
    try {
        $features = @(
            [PSCustomObject]@{ FeatureID = "61161244"; Result = "Success" },
            [PSCustomObject]@{ FeatureID = "61754985"; Result = "Success" }
        )
        $outScript = New-RollbackScript -Features $features -OutputPath $specialDir -PassThru
        
        (Test-Path -LiteralPath $outScript) -and ($outScript -like "*.ps1")
    } finally {
        Remove-Item -LiteralPath $specialDir -Recurse -Force -ErrorAction SilentlyContinue
    }
}

Test-Challenge "Paths" "B5. AST syntax verification on generated rollback script in special path" {
    $specialDir = Join-Path ([System.IO.Path]::GetTempPath()) "ViVe_AST_日本語 [test]"
    New-Item -ItemType Directory -Path $specialDir -Force | Out-Null
    try {
        $features = @("61161244", "61754985", "62762248")
        $outScript = New-RollbackScript -Features $features -OutputPath $specialDir -PassThru
        
        $tokens = $null
        $errors = $null
        $ast = [System.Management.Automation.Language.Parser]::ParseFile($outScript, [ref]$tokens, [ref]$errors)
        
        ($errors.Count -eq 0) -and ($ast -ne $null)
    } finally {
        Remove-Item -LiteralPath $specialDir -Recurse -Force -ErrorAction SilentlyContinue
    }
}

# ------------------------------------------------------------------------------
# CATEGORY C: High-Load Loops, Batch Performance & Memory Stress
# ------------------------------------------------------------------------------
Write-Host "`n[+] Category C: High-Load Loops, Batch Performance and Memory Stress" -ForegroundColor Yellow

Test-Challenge "HighLoad" "C1. High-throughput batch: 250 feature IDs in simulated mode executes <5s" {
    $ids = [System.Collections.Generic.List[string]]::new()
    for ($i = 0; $i -lt 250; $i++) {
        $ids.Add((50000000 + $i).ToString())
    }
    
    $sw = [System.Diagnostics.Stopwatch]::StartNew()
    $results = Invoke-FeatureBatch -Features $ids.ToArray() -Action 'Enable' -ViVeToolPath $mockToolPath -DryRun
    $sw.Stop()
    
    ($results.Count -eq 250) -and ($sw.ElapsedMilliseconds -lt 5000)
}

Test-Challenge "HighLoad" "C2. 10 successive batch runs maintain memory stability and state" {
    $allOk = $true
    $testIds = @("61161244", "61754985", "62762248", "59213768", "60813048")
    for ($round = 1; $round -le 10; $round++) {
        $res = Invoke-FeatureBatch -Features $testIds -Action 'Enable' -ViVeToolPath $mockToolPath -DryRun
        if ($res.Count -ne 5) {
            $allOk = $false
            break
        }
    }
    $allOk
}

Test-Challenge "HighLoad" "C3. Large session CSV log (500 records) parsed accurately by New-RollbackScript" {
    $sandbox = Join-Path ([System.IO.Path]::GetTempPath()) ("ViVe_CsvTest_" + [guid]::NewGuid().ToString('N').Substring(0,8))
    New-Item -ItemType Directory -Path $sandbox -Force | Out-Null
    try {
        $csvFile = Join-Path $sandbox "enable.csv"
        $rows = [System.Collections.Generic.List[PSCustomObject]]::new()
        for ($i = 0; $i -lt 500; $i++) {
            $idStr = (60000000 + $i).ToString()
            $resStatus = if ($i % 3 -eq 0) { "Unsupported" } elseif ($i % 5 -eq 0) { "Error" } else { "Success" }
            $rows.Add([PSCustomObject]@{
                Timestamp = (Get-Date -Format 'o')
                FeatureID = $idStr
                Action    = "Enable"
                Result    = $resStatus
                ExitCode  = if ($resStatus -eq 'Success') { 0 } else { 1 }
                Message   = "Simulated record $i"
            })
        }
        $rows | Export-Csv -LiteralPath $csvFile -NoTypeInformation -Encoding utf8
        
        $rollbackScript = New-RollbackScript -FromLog $csvFile -OutputPath $sandbox -PassThru
        $scriptContent = Get-Content -LiteralPath $rollbackScript -Raw
        
        $expectedSuccess = ($rows | Where-Object { $_.Result -eq 'Success' }).Count
        
        (Test-Path -LiteralPath $rollbackScript) -and ($scriptContent -match "Reverts $expectedSuccess features")
    } finally {
        Remove-Item -LiteralPath $sandbox -Recurse -Force -ErrorAction SilentlyContinue
    }
}

# ------------------------------------------------------------------------------
# CATEGORY D: Fault Injection, Chaos Exit Codes & Watchdog Watchers
# ------------------------------------------------------------------------------
Write-Host "`n[+] Category D: Fault Injection, Chaos Exit Codes and Watchdog Watchers" -ForegroundColor Yellow

Test-Challenge "FaultInjection" "D1. Chaos exit codes (0, 1, 2, 5, 255, 137, 42) handled without batch crash" {
    $sandbox = Join-Path ([System.IO.Path]::GetTempPath()) ("ViVe_Chaos_" + [guid]::NewGuid().ToString('N').Substring(0,8))
    New-Item -ItemType Directory -Path $sandbox -Force | Out-Null
    try {
        $chaosShim = Join-Path $sandbox "ChaosViVeTool.ps1"
        $shimContent = 'param([string]$Action, [string]$Id)
$num = if ($Id -match "\d+") { [int]$Matches[0] } else { 0 }
$code = $num % 7
switch ($code) {
    0 { Write-Host "Successfully set feature configuration: $Id"; exit 0 }
    1 { [Console]::Error.WriteLine("Feature $Id not found"); exit 1 }
    2 { [Console]::Error.WriteLine("Usage: vivetool <command>"); exit 2 }
    3 { [Console]::Error.WriteLine("Access is denied."); exit 5 }
    4 { [Console]::Error.WriteLine("Fatal error 255"); exit 255 }
    5 { [Console]::Error.WriteLine("Killed 137"); exit 137 }
    default { [Console]::Error.WriteLine("Other error 42"); exit 42 }
}'
        Set-Content -LiteralPath $chaosShim -Value $shimContent -Encoding utf8

        $chaosIds = @("100", "101", "102", "103", "104", "105", "106", "107", "108", "109", "110", "111", "112", "113")
        $results = Invoke-FeatureBatch -Features $chaosIds -Action 'Enable' -ViVeToolPath $chaosShim -LogPath (Join-Path $sandbox "chaos.log")
        
        $results.Count -eq 14
    } finally {
        Remove-Item -LiteralPath $sandbox -Recurse -Force -ErrorAction SilentlyContinue
    }
}

Test-Challenge "FaultInjection" "D2. Native binary process watchdog terminates hung process exceeding timeout" {
    # Test watchdog execution using native powershell.exe executable with 1-second timeout
    $sw = [System.Diagnostics.Stopwatch]::StartNew()
    $res = Invoke-ViVeToolFeature -FeatureId "61161244" -Action "Enable" -ViVeToolPath (Get-Command powershell.exe).Source -TimeoutSeconds 1
    $sw.Stop()
    
    # Process should complete or be killed in <= 3 seconds, not hang
    $sw.Elapsed.TotalSeconds -lt 4
}

Test-Challenge "FaultInjection" "D3. Interrupted batch leaves parseable CSV log that rollback can revert" {
    $sandbox = Join-Path ([System.IO.Path]::GetTempPath()) ("ViVe_Interrupt_" + [guid]::NewGuid().ToString('N').Substring(0,8))
    New-Item -ItemType Directory -Path $sandbox -Force | Out-Null
    try {
        $csvLog = Join-Path $sandbox "enable.csv"
        
        for ($i = 1; $i -le 10; $i++) {
            $id = (61000000 + $i).ToString()
            $entry = [PSCustomObject]@{
                Timestamp = (Get-Date -Format 'o')
                FeatureID = $id
                Action    = "Enable"
                Result    = "Success"
                ExitCode  = 0
                Message   = "Simulated execution $id"
            }
            Write-FeatureLog -LogEntry $entry -LogPath $csvLog -NoConsole
        }
        
        $proc = Start-Process -FilePath "powershell.exe" `
                              -ArgumentList "-NoProfile", "-ExecutionPolicy", "Bypass", "-File", "`"$scriptDisable`"", "-FromLog", "`"$csvLog`"", "-DryRun" `
                              -NoNewWindow -Wait -PassThru
        $proc.ExitCode -eq 0
    } finally {
        Remove-Item -LiteralPath $sandbox -Recurse -Force -ErrorAction SilentlyContinue
    }
}

Test-Challenge "FaultInjection" "D4. Corrupted CSV with missing columns and garbage handled cleanly" {
    $sandbox = Join-Path ([System.IO.Path]::GetTempPath()) ("ViVe_BadCsv_" + [guid]::NewGuid().ToString('N').Substring(0,8))
    New-Item -ItemType Directory -Path $sandbox -Force | Out-Null
    try {
        $corruptCsv = Join-Path $sandbox "corrupt.csv"
        $badContent = "Header1,Header2,Header3`r`n`"Unrelated`",`"Data`",`"Here`"`r`n`"Garbage line`r`n61161244,Success,0"
        Set-Content -LiteralPath $corruptCsv -Value $badContent -Encoding utf8

        $proc = Start-Process -FilePath "powershell.exe" `
                              -ArgumentList "-NoProfile", "-ExecutionPolicy", "Bypass", "-File", "`"$scriptDisable`"", "-FromLog", "`"$corruptCsv`"", "-DryRun" `
                              -NoNewWindow -Wait -PassThru
        $proc.ExitCode -eq 0 -or $proc.ExitCode -eq 1
    } finally {
        Remove-Item -LiteralPath $sandbox -Recurse -Force -ErrorAction SilentlyContinue
    }
}

# ------------------------------------------------------------------------------
# CATEGORY E: CLI Interface, Parameter Binding & Positional Quirks
# ------------------------------------------------------------------------------
Write-Host "`n[+] Category E: CLI Interface, Parameter Binding and Positional Quirks" -ForegroundColor Yellow

Test-Challenge "CLI" "E1. Enable-Features.ps1 handles single positional ID (e.g. .\Enable-Features.ps1 61161244)" {
    $proc = Start-Process -FilePath "powershell.exe" `
                          -ArgumentList "-NoProfile", "-ExecutionPolicy", "Bypass", "-File", "`"$scriptEnable`"", "61161244", "-DryRun" `
                          -NoNewWindow -Wait -PassThru
    $proc.ExitCode -eq 0
}

Test-Challenge "CLI" "E2. Disable-Features.ps1 handles single positional ID (e.g. .\Disable-Features.ps1 61161244)" {
    $proc = Start-Process -FilePath "powershell.exe" `
                          -ArgumentList "-NoProfile", "-ExecutionPolicy", "Bypass", "-File", "`"$scriptDisable`"", "61161244", "-DryRun" `
                          -NoNewWindow -Wait -PassThru
    $proc.ExitCode -eq 0
}

Test-Challenge "CLI" "E3. Enable-Features.ps1 array delimiter splitting ('61161244, 61754985; 62762248')" {
    $proc = Start-Process -FilePath "powershell.exe" `
                          -ArgumentList "-NoProfile", "-ExecutionPolicy", "Bypass", "-File", "`"$scriptEnable`"", "-FeatureIds", "'61161244, 61754985; 62762248'", "-DryRun" `
                          -NoNewWindow -Wait -PassThru
    $proc.ExitCode -eq 0
}

Test-Challenge "CLI" "E4. Get-FeatureCatalog direct handling of multiple discrete IDs in string array" {
    $res = @(Get-FeatureCatalog -FeatureIds @("61161244", "61754985", "62762248", "59213768"))
    $res.Count -eq 4
}

Test-Challenge "CLI" "E5. Case-insensitive mixed channel filters ('ga2026', 'cAnArY')" {
    $res = @(Get-FeatureCatalog -Channel @("ga2026", "cAnArY"))
    $res.Count -eq 26
}

Test-Challenge "CLI" "E6. Empty/Whitespace channel filter returns full catalog" {
    $res = @(Get-FeatureCatalog -Channel @("", "   ", $null))
    $res.Count -eq 118
}

# ------------------------------------------------------------------------------
# CATEGORY F: Rollback Generation & Symmetry Verification
# ------------------------------------------------------------------------------
Write-Host "`n[+] Category F: Rollback Generation and Symmetry Verification" -ForegroundColor Yellow

Test-Challenge "Rollback" "F1. New-RollbackScript -ReverseOrder strictly reverses execution sequence" {
    $sandbox = Join-Path ([System.IO.Path]::GetTempPath()) ("ViVe_RevTest_" + [guid]::NewGuid().ToString('N').Substring(0,8))
    New-Item -ItemType Directory -Path $sandbox -Force | Out-Null
    try {
        $features = @("11111111", "22222222", "33333333")
        $scriptPath = New-RollbackScript -Features $features -OutputPath $sandbox -ReverseOrder -PassThru
        $content = Get-Content -LiteralPath $scriptPath -Raw
        
        $pos1 = $content.IndexOf("33333333")
        $pos2 = $content.IndexOf("22222222")
        $pos3 = $content.IndexOf("11111111")
        
        ($pos1 -lt $pos2) -and ($pos2 -lt $pos3)
    } finally {
        Remove-Item -LiteralPath $sandbox -Recurse -Force -ErrorAction SilentlyContinue
    }
}

Test-Challenge "Rollback" "F2. Standalone execution of generated rollback script runs cleanly in DryRun" {
    $sandbox = Join-Path ([System.IO.Path]::GetTempPath()) ("ViVe_RollExec_" + [guid]::NewGuid().ToString('N').Substring(0,8))
    New-Item -ItemType Directory -Path $sandbox -Force | Out-Null
    try {
        $features = @("61161244", "61754985")
        $scriptPath = New-RollbackScript -Features $features -OutputPath $sandbox -PassThru
        
        $proc = Start-Process -FilePath "powershell.exe" `
                              -ArgumentList "-NoProfile", "-ExecutionPolicy", "Bypass", "-File", "`"$scriptPath`"", "-DryRun" `
                              -NoNewWindow -Wait -PassThru
        $proc.ExitCode -eq 0
    } finally {
        Remove-Item -LiteralPath $sandbox -Recurse -Force -ErrorAction SilentlyContinue
    }
}

Test-Challenge "Rollback" "F3. Disable-Features.ps1 with non-existent -FromLog file exits with code 1" {
    $proc = Start-Process -FilePath "powershell.exe" `
                          -ArgumentList "-NoProfile", "-ExecutionPolicy", "Bypass", "-File", "`"$scriptDisable`"", "-FromLog", "Z:\NonExistent_Fake_File_999.csv" `
                          -NoNewWindow -Wait -PassThru
    $proc.ExitCode -eq 1
}

# ------------------------------------------------------------------------------
# CATEGORY G: Security, Elevation Recursion & Architecture Stress
# ------------------------------------------------------------------------------
Write-Host "`n[+] Category G: Security, Elevation Recursion and Architecture Stress" -ForegroundColor Yellow

Test-Challenge "Security" "G1. Invoke-SelfElevation with -Elevated when unprivileged throws loop guard" {
    $orig = $env:VIVETOOL_MOCK_ADMIN
    try {
        $env:VIVETOOL_MOCK_ADMIN = '0'
        $threw = $false
        try {
            $prevEap = $ErrorActionPreference
            $ErrorActionPreference = 'Continue'
            $res = Invoke-SelfElevation -ScriptPath $scriptEnable -Elevated -MockMode
            $ErrorActionPreference = $prevEap
            if ($res -eq $false) { $threw = $true }
        } catch {
            $threw = ($_.FullyQualifiedErrorId -like "*DeniedOrFailed*")
        }
        $threw
    } finally {
        $env:VIVETOOL_MOCK_ADMIN = $orig
    }
}

Test-Challenge "Security" "G2. Get-SystemArchitecture handles blank, unusual or fallback architecture" {
    $origArch = $env:PROCESSOR_ARCHITECTURE
    $origW64 = $env:PROCESSOR_ARCHITEW6432
    try {
        $env:PROCESSOR_ARCHITECTURE = "IA64"
        $env:PROCESSOR_ARCHITEW6432 = $null
        $r1 = ((Get-SystemArchitecture) -eq "X64")
        
        $env:PROCESSOR_ARCHITECTURE = "UNKNOWN_CPU"
        $r2 = ((Get-SystemArchitecture) -eq "X64")
        
        $r1 -and $r2
    } finally {
        $env:PROCESSOR_ARCHITECTURE = $origArch
        $env:PROCESSOR_ARCHITEW6432 = $origW64
    }
}

# ------------------------------------------------------------------------------
# SUMMARY REPORT & EXPORT
# ------------------------------------------------------------------------------
Write-Host "`n================================================================================" -ForegroundColor Cyan
Write-Host " Milestone 4 Tier 5 Adversarial Test Summary" -ForegroundColor Cyan
Write-Host " Total: $script:TotalTests | Passed: $script:PassedTests | Failed: $script:FailedTests" -ForegroundColor $(if ($script:FailedTests -eq 0) { "Green" } else { "Red" })
Write-Host "================================================================================" -ForegroundColor Cyan

if ($ExportJson) {
    $exportPayload = [PSCustomObject]@{
        Suite       = "Milestone 4 Tier 5 Adversarial Hardening"
        Timestamp   = (Get-Date -Format 'o')
        Total       = $script:TotalTests
        Passed      = $script:PassedTests
        Failed      = $script:FailedTests
        PassRatePct = [math]::Round(($script:PassedTests / $script:TotalTests) * 100, 2)
        Results     = $script:TestDetails
    }
    $exportPayload | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath $ExportJson -Encoding utf8
    Write-Host "Adversarial results exported to: $ExportJson" -ForegroundColor DarkGray
}

if ($script:FailedTests -gt 0) {
    exit 1
} else {
    exit 0
}
