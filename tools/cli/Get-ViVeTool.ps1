#Requires -Version 5.1
param([string]$InstallDir = $PSScriptRoot)
function Get-ViveTool {
    param([string]$Dir)
    $found = Get-Command vivetool.exe -ErrorAction SilentlyContinue
    if ($found) { Write-Host "[ViveTool] Found on PATH: $($found.Source)" -ForegroundColor Green; return $found.Source }
    $local = Join-Path $Dir "vivetool.exe"
    if (Test-Path $local) { Write-Host "[ViveTool] Found locally: $local" -ForegroundColor Green; return $local }
    Write-Host "[ViveTool] Not found -- downloading latest release..." -ForegroundColor Yellow
    try { $release = Invoke-RestMethod -Uri "https://api.github.com/repos/thebookisclosed/ViVe/releases/latest" -Headers @{ "User-Agent" = "ViVeInstaller" } }
    catch { throw "[ViveTool] GitHub API error: $_" }
    $asset = $release.assets | Where-Object { $_.name -match "ViVeTool.*\.zip" } | Select-Object -First 1
    if (-not $asset) { throw "[ViveTool] No zip asset in release $($release.tag_name)" }
    $zipPath = Join-Path $env:TEMP $asset.name
    $sizeMB = [math]::Round($asset.size / 1MB, 1)
    Write-Host "[ViveTool] Downloading $($asset.name) ($sizeMB MB)..."
    try { Invoke-WebRequest -Uri $asset.browser_download_url -OutFile $zipPath -UseBasicParsing }
    catch { throw "[ViveTool] Download failed: $_" }
    Write-Host "[ViveTool] Extracting to $Dir ..."
    Expand-Archive -Path $zipPath -DestinationPath $Dir -Force
    Remove-Item $zipPath -Force
    if (Test-Path $local) { Write-Host "[ViveTool] Installed: $local" -ForegroundColor Green; return $local }
    throw "[ViveTool] vivetool.exe missing after extraction: $local"
}
$ViveToolPath = Get-ViveTool -Dir $InstallDir
