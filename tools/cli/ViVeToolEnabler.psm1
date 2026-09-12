# ==============================================================================
# ViVeToolEnabler.psm1 -- Core PowerShell Automation Module
# ==============================================================================

Set-StrictMode -Version 2.0
$ErrorActionPreference = 'Stop'

Add-Type -AssemblyName System.IO.Compression.FileSystem -ErrorAction SilentlyContinue
Add-Type -AssemblyName System.IO.Compression -ErrorAction SilentlyContinue

# Module-scoped configuration constants
$script:DefaultTargetDirectory  = "C:\Tools\vivetool_feature_enabler"
$script:GitHubApiUrl            = "https://api.github.com/repos/thebookisclosed/ViVe/releases/latest"
$script:FallbackReleaseUrlX64   = "https://github.com/thebookisclosed/ViVe/releases/download/v0.3.4/ViVeTool-v0.3.4-IntelAmd.zip"
$script:FallbackReleaseUrlArm64 = "https://github.com/thebookisclosed/ViVe/releases/download/v0.3.4/ViVeTool-v0.3.4-SnapdragonArm64.zip"
$script:RequiredBinaries        = @("ViVeTool.exe", "Albacore.ViVe.dll", "FeatureDictionary.pfs", "Newtonsoft.Json.dll")

# ------------------------------------------------------------------------------
# SECTION 1: Internal Helper Functions (Private)
# ------------------------------------------------------------------------------

function New-ViVeToolError {
    <#
    .SYNOPSIS
        Constructs a structured PowerShell ErrorRecord conforming to the ViVeTool namespace.
    #>
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true)][string]$Message,
        [Parameter(Mandatory = $true)][string]$ErrorId,
        [Parameter()][System.Management.Automation.ErrorCategory]$Category = [System.Management.Automation.ErrorCategory]::NotSpecified,
        [Parameter()][object]$TargetObject = $null,
        [Parameter()][Exception]$InnerException = $null
    )

    $ex = if ($InnerException) {
        [System.InvalidOperationException]::new($Message, $InnerException)
    } else {
        [System.InvalidOperationException]::new($Message)
    }

    return [System.Management.Automation.ErrorRecord]::new(
        $ex,
        $ErrorId,
        $Category,
        $TargetObject
    )
}

function Set-TlsSecurityProtocols {
    <#
    .SYNOPSIS
        Enforces TLS 1.2 and TLS 1.3 in the current AppDomain for secure GitHub API/CDN access.
    #>
    [CmdletBinding()]
    param()

    try {
        [System.Net.ServicePointManager]::SecurityProtocol = [System.Net.ServicePointManager]::SecurityProtocol -bor [System.Net.SecurityProtocolType]::Tls12
        if ([enum]::GetNames([System.Net.SecurityProtocolType]) -contains 'Tls13') {
            [System.Net.ServicePointManager]::SecurityProtocol = [System.Net.ServicePointManager]::SecurityProtocol -bor [System.Net.SecurityProtocolType]::Tls13
        }
    } catch {
        Write-Verbose "TLS protocol configuration notice: $($_.Exception.Message)"
    }
}

function Format-ArgumentForPowerShell {
    <#
    .SYNOPSIS
        Internal helper to serialize parameter values into PowerShell literals for process forwarding.
    #>
    [CmdletBinding()]
    param([Parameter(Mandatory = $false)]$Value)

    if ($null -eq $Value) {
        return '$null'
    }
    if ($Value -is [System.Management.Automation.SwitchParameter] -or $Value -is [bool]) {
        return if ($Value) { '$true' } else { '$false' }
    }
    if ($Value -is [System.Collections.IEnumerable] -and -not ($Value -is [string])) {
        $items = foreach ($item in $Value) {
            if ($null -ne $item) {
                "'" + ($item.ToString().Replace("'", "''")) + "'"
            }
        }
        return "@(" + ($items -join ", ") + ")"
    }
    if ($Value -is [int] -or $Value -is [long] -or $Value -is [double]) {
        return $Value.ToString()
    }
    return "'" + ($Value.ToString().Replace("'", "''")) + "'"
}

# ------------------------------------------------------------------------------
# SECTION 2: Public Functions (Milestone 1: Provisioning, Elevation, Architecture)
# ------------------------------------------------------------------------------

function Test-IsAdministrator {
    <#
    .SYNOPSIS
        Verifies if the current process holds elevated Administrator privileges.
    #>
    [CmdletBinding()]
    [OutputType([bool])]
    param()

    if ($env:VIVETOOL_MOCK_ADMIN -eq '1' -or $env:VIVETOOL_MOCK_ADMIN -eq 'true') {
        return $true
    }
    if ($env:VIVETOOL_MOCK_ADMIN -eq '0' -or $env:VIVETOOL_MOCK_ADMIN -eq 'false') {
        return $false
    }

    try {
        $identity = [Security.Principal.WindowsIdentity]::GetCurrent()
        $principal = [Security.Principal.WindowsPrincipal]::new($identity)
        return [bool]($principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator))
    } catch {
        Write-Verbose "Could not determine WindowsPrincipal elevation status: $($_.Exception.Message)"
        return $false
    }
}

function Get-SystemArchitecture {
    <#
    .SYNOPSIS
        Detects the host CPU architecture, handling 64-bit native, WOW64 emulation, and ARM64.
    #>
    [CmdletBinding()]
    [OutputType([string])]
    param()

    $arch = $env:PROCESSOR_ARCHITECTURE
    $w64Arch = $env:PROCESSOR_ARCHITEW6432

    if (-not [string]::IsNullOrEmpty($w64Arch)) {
        $arch = $w64Arch
    }

    if ([string]::IsNullOrEmpty($arch)) {
        $arch = [System.Runtime.InteropServices.RuntimeInformation]::ProcessArchitecture.ToString()
    }

    switch -Regex ($arch) {
        '(?i)ARM64' { return 'ARM64' }
        '(?i)AMD64|x64|IA64' { return 'X64' }
        '(?i)x86' { return 'X86' }
        default { return 'X64' }
    }
}

function Invoke-SelfElevation {
    <#
    .SYNOPSIS
        Evaluates current privileges and re-spawns the caller script elevated via UAC if unprivileged.
    #>
    [CmdletBinding()]
    [OutputType([bool])]
    param(
        [Parameter()]
        [string]$ScriptPath,

        [Parameter()]
        [System.Collections.IDictionary]$BoundParameters,

        [Parameter()]
        [string[]]$ArgumentList,

        [Parameter()]
        [switch]$Elevated,

        [Parameter()]
        [switch]$MockMode
    )

    # 1. Check if already elevated
    $isAdmin = Test-IsAdministrator
    if ($isAdmin) {
        Write-Verbose "[Invoke-SelfElevation] Already running with elevated Administrator privileges."
        return $true
    }

    # 2. Recursion Guard
    $isRecursive = $Elevated.IsPresent -or ($BoundParameters -and $BoundParameters.ContainsKey('Elevated') -and $BoundParameters['Elevated'])
    if ($isRecursive) {
        $err = New-ViVeToolError -Message "UAC Elevation loop guard tripped. Elevated process failed to gain Administrator token." `
                                 -ErrorId "ViVeTool.Elevation.DeniedOrFailed" `
                                 -Category PermissionDenied
        $PSCmdlet.ThrowTerminatingError($err)
        return $false
    }

    # 3. DryRun / Mock Mode Non-Destructive Simulation
    if ($MockMode -or $env:VIVETOOL_MOCK_MODE -eq '1' -or ($BoundParameters -and $BoundParameters.ContainsKey('DryRun') -and $BoundParameters['DryRun'])) {
        Write-Warning "[DryRun/Mock] Unprivileged session detected. In live execution, UAC prompt would be displayed to elevate."
        return $false
    }

    Write-Host "[*] Administrative privileges required. Requesting UAC elevation..." -ForegroundColor Yellow

    # 4. Host Binary Preservation
    $hostExe = $null
    try {
        $proc = Get-Process -Id $PID -ErrorAction SilentlyContinue
        if ($proc -and $proc.Path -and (Test-Path -LiteralPath $proc.Path -PathType Leaf)) {
            $hostExe = $proc.Path
        }
    } catch {
        Write-Verbose "Could not resolve host process path via Get-Process: $($_.Exception.Message)"
    }

    if (-not $hostExe) {
        try {
            $mainModulePath = [System.Diagnostics.Process]::GetCurrentProcess().MainModule.FileName
            if ($mainModulePath -and (Test-Path -LiteralPath $mainModulePath -PathType Leaf)) {
                $hostExe = $mainModulePath
            }
        } catch {
            Write-Verbose "Could not resolve host process path via MainModule: $($_.Exception.Message)"
        }
    }

    if (-not $hostExe) {
        $defaultCmd = if ($PSVersionTable.PSEdition -eq 'Core') { 'pwsh.exe' } else { 'powershell.exe' }
        $cmdInfo = Get-Command -Name $defaultCmd -ErrorAction SilentlyContinue
        if ($cmdInfo -and $cmdInfo.Source) {
            $hostExe = $cmdInfo.Source
        } else {
            $hostExe = $defaultCmd
        }
    }

    Write-Verbose "Identified host executable: $hostExe"

    # 5. Script Path Resolution
    if (-not $ScriptPath) {
        $ScriptPath = $MyInvocation.PSCommandPath
    }
    if (-not $ScriptPath) {
        try {
            $ScriptPath = (Get-Variable -Name MyInvocation -Scope 1 -ErrorAction SilentlyContinue).Value.PSCommandPath
        } catch {}
    }
    if (-not $ScriptPath -or -not (Test-Path -LiteralPath $ScriptPath)) {
        $err = New-ViVeToolError -Message "Cannot determine script path for UAC auto-elevation. Provide -ScriptPath explicitly." `
                                 -ErrorId "ViVeTool.Elevation.ScriptPathUnresolved" `
                                 -Category InvalidArgument
        $PSCmdlet.ThrowTerminatingError($err)
    }

    $resolvedScriptPath = (Resolve-Path -LiteralPath $ScriptPath).Path
    $escapedScriptPath = "'" + $resolvedScriptPath.Replace("'", "''") + "'"

    # 6. Parameter Serialization
    $paramTokens = [System.Collections.Generic.List[string]]::new()

    if ($BoundParameters) {
        foreach ($key in $BoundParameters.Keys) {
            if ($key -eq 'Elevated') { continue }
            $val = $BoundParameters[$key]

            if ($val -is [System.Management.Automation.SwitchParameter] -or $val -is [bool]) {
                if ($val -eq $true -or ($val -is [System.Management.Automation.SwitchParameter] -and $val.IsPresent)) {
                    $paramTokens.Add("-$key")
                } else {
                    $paramTokens.Add("-${key}:`$false")
                }
            } elseif ($val -is [System.Collections.IEnumerable] -and -not ($val -is [string])) {
                $arrayItems = foreach ($item in $val) {
                    if ($null -ne $item) {
                        "'" + ($item.ToString().Replace("'", "''")) + "'"
                    }
                }
                $paramTokens.Add("-$key")
                $paramTokens.Add("@(" + ($arrayItems -join ", ") + ")")
            } else {
                $paramTokens.Add("-$key")
                $formattedVal = Format-ArgumentForPowerShell -Value $val
                $paramTokens.Add($formattedVal)
            }
        }
    }

    if ($ArgumentList) {
        foreach ($arg in $ArgumentList) {
            if ($arg -ne '-Elevated') {
                $paramTokens.Add($arg)
            }
        }
    }

    $paramTokens.Add('-Elevated')

    # 7. Launch Elevated Process
    $fullScriptInvocation = "& $escapedScriptPath " + ($paramTokens -join " ")
    $launchArgs = @(
        "-NoProfile",
        "-ExecutionPolicy", "Bypass",
        "-Command", $fullScriptInvocation
    )

    $psi = [System.Diagnostics.ProcessStartInfo]::new()
    $psi.FileName = $hostExe
    $psi.Arguments = [string]::Join(" ", $launchArgs)
    $psi.Verb = "runas"
    $psi.UseShellExecute = $true
    $psi.WindowStyle = [System.Diagnostics.ProcessWindowStyle]::Normal

    try {
        $proc = [System.Diagnostics.Process]::Start($psi)
        if ($proc) {
            $proc.WaitForExit()
            [System.Environment]::ExitCode = $proc.ExitCode
            exit $proc.ExitCode
        }
    } catch {
        $err = New-ViVeToolError -Message "User declined UAC elevation or elevation process failed: $($_.Exception.Message)" `
                                 -ErrorId "ViVeTool.Elevation.UserDeclinedOrFailed" `
                                 -Category PermissionDenied `
                                 -InnerException $_.Exception
        $PSCmdlet.ThrowTerminatingError($err)
    }

    return $false
}

function Ensure-ViVeTool {
    <#
    .SYNOPSIS
        Verifies local ViVeTool binary presence or provisions it from GitHub releases.
    #>
    [CmdletBinding(DefaultParameterSetName = 'Default', SupportsShouldProcess = $true)]
    [OutputType([string])]
    param(
        [Parameter(Position = 0)]
        [ValidateNotNullOrEmpty()]
        [string]$TargetDirectory = $script:DefaultTargetDirectory,

        [Parameter()]
        [string]$ViVeToolPath,

        [Parameter()]
        [switch]$ForceDownload,

        [Parameter()]
        [ValidateRange(5, 300)]
        [int]$TimeoutSeconds = 30,

        [Parameter()]
        [switch]$DryRun
    )

    # 1. Check Mock Shim Mode
    if ($env:VIVETOOL_MOCK_MODE -eq '1') {
        $mockPath = Join-Path -Path $PSScriptRoot -ChildPath "tests\MockViVeTool.cmd"
        if (-not (Test-Path -LiteralPath $mockPath)) {
            $mockPath = Join-Path -Path $TargetDirectory -ChildPath "tests\MockViVeTool.cmd"
        }
        Write-Verbose "[Ensure-ViVeTool] VIVETOOL_MOCK_MODE=1 active; returning mock shim path: $mockPath"
        return $mockPath
    }

    # 2. Check Explicit Path
    if ($ViVeToolPath) {
        if (Test-Path -LiteralPath $ViVeToolPath -PathType Leaf) {
            $resolvedExplicit = (Resolve-Path -LiteralPath $ViVeToolPath).Path
            Write-Verbose "[Ensure-ViVeTool] Using explicit ViVeTool path: $resolvedExplicit"
            return $resolvedExplicit
        } else {
            $err = New-ViVeToolError -Message "Explicitly specified ViVeTool binary does not exist at: $ViVeToolPath" `
                                     -ErrorId "ViVeTool.Provisioning.ExplicitPathNotFound" `
                                     -Category ObjectNotFound `
                                     -TargetObject $ViVeToolPath
            $PSCmdlet.ThrowTerminatingError($err)
        }
    }

    # 3. Check Target Directory Location First (if not ForceDownload)
    if (-not $ForceDownload) {
        $targetExeCandidates = @(
            (Join-Path -Path $TargetDirectory -ChildPath "ViVeTool.exe"),
            (Join-Path -Path $TargetDirectory -ChildPath "vivetool.exe")
        )

        foreach ($cand in $targetExeCandidates) {
            if ($cand -and (Test-Path -LiteralPath $cand -PathType Leaf)) {
                $dir = [System.IO.Path]::GetDirectoryName($cand)
                $compDll = Join-Path -Path $dir -ChildPath "Albacore.ViVe.dll"
                $compDict = Join-Path -Path $dir -ChildPath "FeatureDictionary.pfs"
                $compJson = Join-Path -Path $dir -ChildPath "Newtonsoft.Json.dll"

                # Verify companion files exist alongside binary
                if ((Test-Path -LiteralPath $compDll -PathType Leaf) -and (Test-Path -LiteralPath $compDict -PathType Leaf) -and (Test-Path -LiteralPath $compJson -PathType Leaf)) {
                    $resolved = (Resolve-Path -LiteralPath $cand).Path
                    Write-Verbose "[Ensure-ViVeTool] Located verified existing ViVeTool binary at: $resolved"
                    return $resolved
                } else {
                    Write-Warning "[Ensure-ViVeTool] Found ViVeTool.exe at '$cand' but companion dependencies are missing. Re-provisioning..."
                    break
                }
            }
        }
    }

    # 4. DryRun short-circuit
    $defaultExe = Join-Path -Path $TargetDirectory -ChildPath "ViVeTool.exe"
    if ($DryRun) {
        Write-Warning "[DryRun] ViVeTool binary not present in target directory. In live mode, it would be downloaded to: $defaultExe"
        return $defaultExe
    }

    # 5. Check Fallback Candidate Locations & PATH (if not ForceDownload)
    if (-not $ForceDownload) {
        $fallbackCandidates = @(
            (Join-Path -Path $PSScriptRoot -ChildPath "ViVeTool.exe"),
            (Join-Path -Path $PSScriptRoot -ChildPath "vivetool.exe"),
            (Join-Path -Path (Join-Path -Path $PSScriptRoot -ChildPath "bin") -ChildPath "ViVeTool.exe"),
            "C:\Tools\vivetool_feature_enabler\ViVeTool.exe"
        )

        foreach ($cand in $fallbackCandidates) {
            if ($cand -and (Test-Path -LiteralPath $cand -PathType Leaf)) {
                $dir = [System.IO.Path]::GetDirectoryName($cand)
                $compDll = Join-Path -Path $dir -ChildPath "Albacore.ViVe.dll"
                $compDict = Join-Path -Path $dir -ChildPath "FeatureDictionary.pfs"
                $compJson = Join-Path -Path $dir -ChildPath "Newtonsoft.Json.dll"

                if ((Test-Path -LiteralPath $compDll -PathType Leaf) -and (Test-Path -LiteralPath $compDict -PathType Leaf) -and (Test-Path -LiteralPath $compJson -PathType Leaf)) {
                    $resolved = (Resolve-Path -LiteralPath $cand).Path
                    Write-Verbose "[Ensure-ViVeTool] Located verified fallback ViVeTool binary at: $resolved"
                    return $resolved
                }
            }
        }

        # Check system PATH
        $pathCmd = Get-Command -Name "vivetool.exe" -ErrorAction SilentlyContinue
        if ($pathCmd -and (Test-Path -LiteralPath $pathCmd.Source -PathType Leaf)) {
            Write-Verbose "[Ensure-ViVeTool] Found ViVeTool on system PATH: $($pathCmd.Source)"
            return $pathCmd.Source
        }
    }

    # 5. Provision binary from GitHub
    if ($PSCmdlet.ShouldProcess($TargetDirectory, "Download and extract ViVeTool release binary")) {
        if (-not (Test-Path -LiteralPath $TargetDirectory)) {
            New-Item -ItemType Directory -Path $TargetDirectory -Force | Out-Null
        }

        Set-TlsSecurityProtocols
        $arch = Get-SystemArchitecture
        $isArm64 = ($arch -eq 'ARM64')
        Write-Host "[*] System architecture detected: $arch" -ForegroundColor Cyan

        $downloadUrl = $null
        $headers = @{
            "User-Agent" = "ViVeTool-Feature-Enabler-PowerShell/1.0"
            "Accept"     = "application/vnd.github.v3+json"
        }

        # Query GitHub Releases API
        $archLabel = if ($isArm64) { "ARM64" } else { "x64" }
        Write-Host "[*] Querying GitHub API for latest ViVeTool release ($archLabel)..." -ForegroundColor Cyan
        try {
            $release = Invoke-RestMethod -Uri $script:GitHubApiUrl -Headers $headers -TimeoutSec $TimeoutSeconds -ErrorAction Stop
            if ($isArm64) {
                $asset = $release.assets | Where-Object { $_.name -match 'SnapdragonArm64|ARM64' -and $_.name -like '*.zip' } | Select-Object -First 1
            } else {
                $asset = $release.assets | Where-Object { ($_.name -match 'IntelAmd' -or $_.name -match '^ViVeTool-v[\d\.]+\.zip$') -and $_.name -notmatch 'GUI' } | Select-Object -First 1
            }

            if ($asset -and $asset.browser_download_url) {
                $downloadUrl = $asset.browser_download_url
                Write-Verbose "[Ensure-ViVeTool] Discovered release asset '$($asset.name)' -> $downloadUrl"
            }
        } catch {
            Write-Warning "[Ensure-ViVeTool] GitHub API query failed ($($_.Exception.Message)). Using resilient direct fallback URL."
        }

        # Fallback Direct URLs
        if (-not $downloadUrl) {
            if ($isArm64) {
                $downloadUrl = $script:FallbackReleaseUrlArm64
            } else {
                $downloadUrl = $script:FallbackReleaseUrlX64
            }
            Write-Verbose "[Ensure-ViVeTool] Static fallback download URL selected: $downloadUrl"
        }

        # Download & Extract Archive
        $tempZip = Join-Path -Path ([System.IO.Path]::GetTempPath()) -ChildPath ("ViVeTool_download_" + [guid]::NewGuid().ToString('N').Substring(0,8) + ".zip")
        try {
            Write-Host "    Downloading ViVeTool package from $downloadUrl..." -ForegroundColor Gray
            try {
                Invoke-WebRequest -Uri $downloadUrl -OutFile $tempZip -UseBasicParsing -TimeoutSec ($TimeoutSeconds * 2) -ErrorAction Stop
            } catch {
                Write-Verbose "[Ensure-ViVeTool] Invoke-WebRequest failed. Attempting .NET WebClient fallback..."
                $wc = New-Object System.Net.WebClient
                $wc.Headers.Add("User-Agent", "ViVeTool-Feature-Enabler-PowerShell/1.0")
                $wc.DownloadFile($downloadUrl, $tempZip)
            }

            Write-Host "    Extracting files to $TargetDirectory..." -ForegroundColor Gray
            Expand-Archive -LiteralPath $tempZip -DestinationPath $TargetDirectory -Force -ErrorAction Stop

            # Unblock files to eliminate Zone.Identifier Mark-of-the-Web
            Get-ChildItem -LiteralPath $TargetDirectory -File | ForEach-Object {
                Unblock-File -LiteralPath $_.FullName -ErrorAction SilentlyContinue
            }
        } catch {
            $err = New-ViVeToolError -Message "Failed to download or extract ViVeTool package: $($_.Exception.Message)" `
                                     -ErrorId "ViVeTool.Provisioning.DownloadFailed" `
                                     -Category ReadError `
                                     -TargetObject $downloadUrl `
                                     -InnerException $_.Exception
            $PSCmdlet.ThrowTerminatingError($err)
        } finally {
            if (Test-Path -LiteralPath $tempZip) {
                Remove-Item -LiteralPath $tempZip -Force -ErrorAction SilentlyContinue
            }
        }

        # Validate Extraction Integrity
        $finalExe = Join-Path -Path $TargetDirectory -ChildPath "ViVeTool.exe"
        if (-not (Test-Path -LiteralPath $finalExe -PathType Leaf)) {
            $anyExe = Get-ChildItem -LiteralPath $TargetDirectory -Filter "*vivetool*.exe" | Select-Object -First 1
            if ($anyExe -and (Test-Path -LiteralPath $anyExe.FullName -PathType Leaf)) {
                $finalExe = $anyExe.FullName
            } else {
                $err = New-ViVeToolError -Message "ViVeTool provisioning failed: ViVeTool.exe was not found in '$TargetDirectory' after archive extraction." `
                                         -ErrorId "ViVeTool.Provisioning.BinaryMissingAfterExtraction" `
                                         -Category ObjectNotFound `
                                         -TargetObject $TargetDirectory
                $PSCmdlet.ThrowTerminatingError($err)
            }
        }

        foreach ($dep in $script:RequiredBinaries) {
            $depPath = Join-Path -Path $TargetDirectory -ChildPath $dep
            if (-not (Test-Path -LiteralPath $depPath -PathType Leaf)) {
                Write-Warning "[Ensure-ViVeTool] Expected dependency '$dep' is missing from '$TargetDirectory'."
            }
        }

        # Verification Execution
        try {
            $verifyOutput = & $finalExe /? 2>&1
            if (($verifyOutput -join ' ') -notmatch 'ViVeTool') {
                Write-Warning "[Ensure-ViVeTool] Verification test output did not match expected ViVeTool banner."
            } else {
                Write-Host "[+] ViVeTool successfully provisioned and verified at: $finalExe" -ForegroundColor Green
            }
        } catch {
            Write-Warning "[Ensure-ViVeTool] Verification execution threw an error: $($_.Exception.Message)"
        }

        return (Resolve-Path -LiteralPath $finalExe).Path
    }

    return $defaultExe
}

# ------------------------------------------------------------------------------
# SECTION 3: Public Functions (Milestone 2: Catalog, Execution, Logging, Restart)
# ------------------------------------------------------------------------------

function Get-FeatureCatalog {
    <#
    .SYNOPSIS
        Retrieves the catalog of feature IDs and metadata, optionally filtered by Channel or explicit FeatureIds.
    .PARAMETER CatalogPath
        Path to the FeatureCatalog.json file. Defaults to FeatureCatalog.json in module root or TargetDirectory.
    .PARAMETER Channel
        One or more channel names to filter by: 'GA2026', 'GA2025', '26H2', '25H2', 'Canary', or 'All'.
        Matching is case-insensitive.
    .PARAMETER FeatureIds
        One or more explicit feature IDs to filter or include. If specified, only features matching these IDs are returned.
    #>
    [CmdletBinding()]
    [OutputType([PSCustomObject[]])]
    param(
        [Parameter()]
        [string]$CatalogPath,

        [Parameter()]
        [string[]]$Channel,

        [Parameter()]
        [Alias('Id', 'Ids', 'FeatureList')]
        [string[]]$FeatureIds
    )

    # 1. Resolve Catalog JSON file
    $resolvedCatalogPath = $null
    $candidates = @()
    if ($CatalogPath) {
        $candidates += $CatalogPath
    }
    $candidates += (Join-Path -Path $PSScriptRoot -ChildPath "FeatureCatalog.json")
    $candidates += (Join-Path -Path $script:DefaultTargetDirectory -ChildPath "FeatureCatalog.json")
    $candidates += "C:\Tools\vivetool_feature_enabler\FeatureCatalog.json"

    foreach ($cand in $candidates) {
        if ($cand -and (Test-Path -LiteralPath $cand -PathType Leaf)) {
            $resolvedCatalogPath = (Resolve-Path -LiteralPath $cand).Path
            break
        }
    }

    $rawCatalog = [System.Collections.Generic.List[object]]::new()
    if ($resolvedCatalogPath) {
        try {
            $rawText = Get-Content -LiteralPath $resolvedCatalogPath -Raw -Encoding utf8
            $parsedJson = $rawText | ConvertFrom-Json
            if ($parsedJson -is [System.Collections.IEnumerable] -and -not ($parsedJson -is [string])) {
                foreach ($elem in $parsedJson) { $rawCatalog.Add($elem) }
            } elseif ($null -ne $parsedJson) {
                $rawCatalog.Add($parsedJson)
            }
        } catch {
            $err = New-ViVeToolError -Message "Failed to parse feature catalog JSON from '$resolvedCatalogPath': $($_.Exception.Message)" `
                                     -ErrorId "ViVeTool.Catalog.ParseError" `
                                     -Category ParserError `
                                     -TargetObject $resolvedCatalogPath `
                                     -InnerException $_.Exception
            $PSCmdlet.ThrowTerminatingError($err)
        }
    } else {
        # Resilient embedded catalog fallback (118 unique pureinfotech IDs)
        $rawGA2026 = @("61161244", "61754985", "62762248", "59213768", "60813048", "61090762", "59728252", "27829265", "61457898", "61160789", "58989177", "58989092", "60716524", "48433719", "61391826", "58989070", "58989021", "58989002", "57741219", "55994763", "58988972")
        $rawGA2025 = @("57048237", "59162732", "41356296", "45690266", "59265307", "57882334", "53343270", "57048231", "47205210", "57048226", "57048218", "57048216")
        $raw26H2   = @("60813048", "62141177", "62068874", "63194003", "62915050", "61483244", "60490208", "60730253", "61384404", "60414189", "48433719", "61161244", "61161268", "61160789", "61161304", "61161283", "61441697", "61267302", "61344081", "61482515", "61532758", "61760679", "61465695", "61465915", "62261462", "60511437", "51406324", "60288851", "58989092", "58989177", "61754985", "61225604", "61596616", "61596617", "61596618", "61596619", "61372722", "59213768", "61090762", "60716524", "61391826", "61014711", "59728252", "60897831", "60662124", "57156807", "59956305", "57751666", "57751687", "61157505", "61410885", "60772592", "60911173", "58429068", "58111409", "27829265", "59149945", "58989070", "58989021", "58989002", "59764273", "60772996", "53343270", "59265307", "60597402", "60825171", "58988972", "57741219", "49059846", "60063638", "58182453", "57118881")
        $raw25H2   = @("59359094", "58978959", "58381341", "58527096", "57259990", "58938944", "57900749", "58324036", "58680439", "38679741", "41118774", "55805655", "59213523", "59193521", "59765208", "55324166", "59673297", "58423575", "58778013", "59339532", "55994763", "59162732", "57739723", "57941090", "58970402", "58383338", "59270880", "59203365", "41356296", "57703775", "57645315")
        $rawCanary = @("61121285", "58288238", "53283713", "59065581", "45425284")

        $allRefs = $rawGA2026 + $rawGA2025 + $raw26H2 + $raw25H2 + $rawCanary
        $unique = $allRefs | Select-Object -Unique
        foreach ($id in $unique) {
            $ch = [System.Collections.Generic.List[string]]::new()
            if ($rawGA2026 -contains $id) { $ch.Add("GA2026") }
            if ($rawGA2025 -contains $id) { $ch.Add("GA2025") }
            if ($raw26H2 -contains $id)   { $ch.Add("26H2") }
            if ($raw25H2 -contains $id)   { $ch.Add("25H2") }
            if ($rawCanary -contains $id) { $ch.Add("Canary") }
            $rawCatalog += [PSCustomObject]@{
                Id          = $id
                FeatureID   = $id
                Channels    = $ch.ToArray()
                Description = "Pureinfotech Windows 11 feature velocity override $id"
            }
        }
    }

    # Normalize properties (ensure both Id and FeatureID are populated)
    $normalized = [System.Collections.Generic.List[PSCustomObject]]::new()
    foreach ($item in $rawCatalog) {
        $id = if ($item.Id) { $item.Id.ToString().Trim() } elseif ($item.FeatureID) { $item.FeatureID.ToString().Trim() } else { "" }
        if (-not $id) { continue }
        $channels = if ($item.Channels) { @($item.Channels | ForEach-Object { $_.ToString().Trim() }) } else { @("All") }
        $desc = if ($item.Description) { $item.Description } else { "Feature $id" }

        $normalized.Add([PSCustomObject]@{
            Id          = $id
            FeatureID   = $id
            Channels    = $channels
            Description = $desc
        })
    }

    # 2. Filter by Channel
    $filteredByChannel = [System.Collections.Generic.List[PSCustomObject]]::new()
    if ($Channel -and $Channel.Count -gt 0) {
        $cleanChannels = @($Channel | Where-Object { -not [string]::IsNullOrWhiteSpace($_) } | ForEach-Object { $_.Trim() })
        if ($cleanChannels.Count -eq 0) {
            $filteredByChannel.AddRange($normalized)
        } elseif ($cleanChannels -contains "All" -or $cleanChannels -contains "all") {
            $filteredByChannel.AddRange($normalized)
        } else {
            foreach ($item in $normalized) {
                $matched = $false
                foreach ($reqCh in $cleanChannels) {
                    foreach ($itemCh in $item.Channels) {
                        if ($itemCh -like "(?i)$reqCh" -or $itemCh -eq $reqCh -or $itemCh.ToLowerInvariant() -eq $reqCh.ToLowerInvariant()) {
                            $matched = $true
                            break
                        }
                    }
                    if ($matched) { break }
                }
                if ($matched) {
                    $filteredByChannel.Add($item)
                }
            }
        }
    } else {
        $filteredByChannel.AddRange($normalized)
    }

    # 3. Filter by explicit FeatureIds (overrides / intersects)
    if ($FeatureIds -and $FeatureIds.Count -gt 0) {
        $cleanIds = @($FeatureIds | Where-Object { -not [string]::IsNullOrWhiteSpace($_) } | ForEach-Object { $_.Trim() })
        $finalList = [System.Collections.Generic.List[PSCustomObject]]::new()
        foreach ($id in $cleanIds) {
            $foundInCatalog = $filteredByChannel | Where-Object { $_.Id -eq $id -or $_.FeatureID -eq $id } | Select-Object -First 1
            if ($foundInCatalog) {
                $finalList.Add($foundInCatalog)
            } else {
                $finalList.Add([PSCustomObject]@{
                    Id          = $id
                    FeatureID   = $id
                    Channels    = @("Custom")
                    Description = "Custom feature ID override $id"
                })
            }
        }
        return $finalList.ToArray()
    }

    return $filteredByChannel.ToArray()
}

function Invoke-ViVeToolFeature {
    <#
    .SYNOPSIS
        Executes ViVeTool CLI for a single feature ID with exit code and status parsing.
    .PARAMETER FeatureId
        Numeric feature code (e.g., '61161244').
    .PARAMETER Action
        Action to execute: 'Enable', 'Disable', or 'Reset'. Defaults to 'Enable'.
    .PARAMETER ViVeToolPath
        Path to ViVeTool executable or test shim.
    .PARAMETER DryRun
        If set, performs simulated execution without invoking binary.
    .PARAMETER TimeoutSeconds
        Process execution timeout in seconds. Defaults to 30.
    #>
    [CmdletBinding(DefaultParameterSetName = 'Default', SupportsShouldProcess = $true)]
    [OutputType([PSCustomObject])]
    param(
        [Parameter(Mandatory = $true, Position = 0)]
        [ValidateNotNullOrEmpty()]
        [string]$FeatureId,

        [Parameter(Position = 1)]
        [ValidateSet('Enable', 'Disable', 'Reset')]
        [string]$Action = 'Enable',

        [Parameter()]
        [string]$ViVeToolPath,

        [Parameter()]
        [switch]$DryRun,

        [Parameter()]
        [ValidateRange(1, 300)]
        [int]$TimeoutSeconds = 30
    )

    $FeatureId = $FeatureId.Trim()
    $timestamp = (Get-Date -Format 'o')
    $actionLower = $Action.ToLowerInvariant()
    $actionFlag = "/$actionLower"
    $idFlag = "/id:$FeatureId"

    # 1. DryRun Simulation
    if ($DryRun) {
        Write-Verbose "[DryRun] Simulated $Action for feature ID $FeatureId"
        return [PSCustomObject]@{
            Timestamp  = $timestamp
            FeatureID  = $FeatureId
            Action     = $Action
            Result     = "Skipped"
            ExitCode   = 0
            Message    = "DryRun simulation -- execution bypassed"
            DurationMs = 0
        }
    }

    # 2. Resolve ViVeTool binary path if not provided
    if (-not $ViVeToolPath) {
        $ViVeToolPath = Ensure-ViVeTool -TargetDirectory $script:DefaultTargetDirectory
    }

    if (-not (Test-Path -LiteralPath $ViVeToolPath)) {
        $err = New-ViVeToolError -Message "ViVeTool executable not found at: $ViVeToolPath" `
                                 -ErrorId "ViVeTool.Execution.BinaryNotFound" `
                                 -Category ObjectNotFound `
                                 -TargetObject $ViVeToolPath
        $PSCmdlet.ThrowTerminatingError($err)
    }

    # 3. Execution Guard / ShouldProcess
    if (-not $PSCmdlet.ShouldProcess("Feature ID $FeatureId", "$Action feature via ViVeTool")) {
        return [PSCustomObject]@{
            Timestamp  = $timestamp
            FeatureID  = $FeatureId
            Action     = $Action
            Result     = "Skipped"
            ExitCode   = 0
            Message    = "Execution bypassed by ShouldProcess (-WhatIf)"
            DurationMs = 0
        }
    }

    $sw = [System.Diagnostics.Stopwatch]::StartNew()
    $stdout = ""
    $stderr = ""
    $exitCode = -1

    try {
        if ($ViVeToolPath.EndsWith(".ps1", [System.StringComparison]::OrdinalIgnoreCase)) {
            $output = & $ViVeToolPath $actionFlag $idFlag 2>&1
            $exitCode = $LASTEXITCODE
            $stdout = ($output | ForEach-Object { $_.ToString() }) -join "`n"
        } elseif ($ViVeToolPath.EndsWith(".cmd", [System.StringComparison]::OrdinalIgnoreCase) -or $ViVeToolPath.EndsWith(".bat", [System.StringComparison]::OrdinalIgnoreCase)) {
            $output = & $ViVeToolPath $actionFlag $idFlag 2>&1
            $exitCode = $LASTEXITCODE
            $stdout = ($output | ForEach-Object { $_.ToString() }) -join "`n"
        } else {
            $psi = [System.Diagnostics.ProcessStartInfo]::new()
            $psi.FileName = $ViVeToolPath
            $psi.Arguments = "$actionFlag $idFlag"
            $psi.UseShellExecute = $false
            $psi.RedirectStandardOutput = $true
            $psi.RedirectStandardError = $true
            $psi.CreateNoWindow = $true

            $proc = [System.Diagnostics.Process]::new()
            $proc.StartInfo = $psi
            $proc.Start() | Out-Null

            $timeoutMs = $TimeoutSeconds * 1000
            $finished = $proc.WaitForExit($timeoutMs)
            if (-not $finished) {
                try { $proc.Kill() } catch {}
                $exitCode = 255
                $stderr = "Process timed out and was terminated by watchdog after ${TimeoutSeconds}s."
            } else {
                $stdout = $proc.StandardOutput.ReadToEnd()
                $stderr = $proc.StandardError.ReadToEnd()
                $exitCode = $proc.ExitCode
            }
        }
    } catch {
        $exitCode = 255
        $stderr = $_.Exception.Message
    } finally {
        $sw.Stop()
    }

    $combinedOutput = ($stdout + "`n" + $stderr).Trim()

    # 4. Status Classification Logic
    $resultStatus = "Success"

    if ($exitCode -eq 0) {
        if ($combinedOutput -match "Access is denied") {
            $resultStatus = "AccessDenied"
        } elseif ($combinedOutput -match "Feature.*not found|not found|Failed to set") {
            $resultStatus = "Unsupported"
        } elseif ($combinedOutput -match "Invalid parameter|syntax|Usage:") {
            $resultStatus = "SyntaxError"
        } elseif ($combinedOutput -match "error occurred" -and $combinedOutput -notmatch "Successfully") {
            $resultStatus = "Error"
        } else {
            $resultStatus = "Success"
        }
    } elseif ($exitCode -eq 1) {
        if ($combinedOutput -match "Access is denied") {
            $resultStatus = "AccessDenied"
        } else {
            $resultStatus = "Unsupported"
        }
    } elseif ($exitCode -eq 5) {
        $resultStatus = "AccessDenied"
    } elseif ($exitCode -eq 2) {
        $resultStatus = "SyntaxError"
    } elseif ($exitCode -eq 255) {
        $resultStatus = "FatalError"
    } else {
        if ($combinedOutput -match "Access is denied") {
            $resultStatus = "AccessDenied"
        } elseif ($combinedOutput -match "not found") {
            $resultStatus = "Unsupported"
        } else {
            $resultStatus = "Error"
        }
    }

    $msg = if ($combinedOutput) { $combinedOutput } elseif ($resultStatus -eq 'Success') { "Successfully set feature configuration: $FeatureId" } else { "Feature operation $Action completed with status $resultStatus" }

    return [PSCustomObject]@{
        Timestamp  = $timestamp
        FeatureID  = $FeatureId
        Action     = $Action
        Result     = $resultStatus
        ExitCode   = $exitCode
        Message    = $msg
        DurationMs = $sw.ElapsedMilliseconds
    }
}

function Write-FeatureLog {
    <#
    .SYNOPSIS
        Multi-sink persistent logger writing to plain text (.log), structured CSV (.csv), and console.
    .PARAMETER LogEntry
        PSCustomObject containing Timestamp, FeatureID, Action, Result, ExitCode, Message.
    .PARAMETER LogPath
        Base file path or directory for log output.
    .PARAMETER NoConsole
        Suppresses host console output.
    #>
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true, Position = 0, ValueFromPipeline = $true)]
        [PSCustomObject]$LogEntry,

        [Parameter(Position = 1)]
        [string]$LogPath,

        [Parameter()]
        [switch]$NoConsole
    )

    process {
        # 1. Resolve Effective Log Directory & Paths
        $effectiveDir = $null
        $baseName = "enable"
        if ($LogEntry.Action -and $LogEntry.Action -eq 'Disable') {
            $baseName = "disable"
        }

        if ($LogPath) {
            try {
                if ($LogPath.EndsWith(".log", [System.StringComparison]::OrdinalIgnoreCase) -or $LogPath.EndsWith(".csv", [System.StringComparison]::OrdinalIgnoreCase) -or $LogPath.EndsWith(".json", [System.StringComparison]::OrdinalIgnoreCase)) {
                    $effectiveDir = [System.IO.Path]::GetDirectoryName($LogPath)
                    $baseName = [System.IO.Path]::GetFileNameWithoutExtension($LogPath)
                } else {
                    $effectiveDir = $LogPath
                }

                if ($effectiveDir -and -not (Test-Path -LiteralPath $effectiveDir)) {
                    New-Item -ItemType Directory -Path $effectiveDir -Force -ErrorAction Stop | Out-Null
                }
            } catch {
                # Fallback to TEMP directory if target is unwritable
                $effectiveDir = $env:TEMP
                $baseName = "vivetool_fallback_$(Get-Date -Format 'yyyyMMdd_HHmmss')"
            }
        }

        if (-not $effectiveDir) {
            $defaultLogsDir = Join-Path -Path $script:DefaultTargetDirectory -ChildPath "logs"
            try {
                if (-not (Test-Path -LiteralPath $defaultLogsDir)) {
                    New-Item -ItemType Directory -Path $defaultLogsDir -Force -ErrorAction Stop | Out-Null
                }
                $effectiveDir = $defaultLogsDir
            } catch {
                $effectiveDir = $env:TEMP
            }
        }

        $logFile = Join-Path -Path $effectiveDir -ChildPath "$baseName.log"
        $csvFile = Join-Path -Path $effectiveDir -ChildPath "$baseName.csv"

        # 2. Write Plain Text Log Entry
        $timeStr = if ($LogEntry.Timestamp) {
            try { [datetime]::Parse($LogEntry.Timestamp).ToString('yyyy-MM-dd HH:mm:ss') } catch { Get-Date -Format 'yyyy-MM-dd HH:mm:ss' }
        } else {
            Get-Date -Format 'yyyy-MM-dd HH:mm:ss'
        }
        $logLine = "[$timeStr] [INFO] [$($LogEntry.FeatureID)] Action: $($LogEntry.Action) | Result: $($LogEntry.Result) | ExitCode: $($LogEntry.ExitCode) | Msg: $($LogEntry.Message.Replace("`n", ' '))"
        try {
            Add-Content -LiteralPath $logFile -Value $logLine -Encoding utf8 -ErrorAction SilentlyContinue
        } catch {}

        # 3. Write Structured CSV Entry
        try {
            $csvRow = [PSCustomObject]@{
                Timestamp = if ($LogEntry.Timestamp) { $LogEntry.Timestamp } else { Get-Date -Format 'o' }
                FeatureID = $LogEntry.FeatureID
                Action    = $LogEntry.Action
                Result    = $LogEntry.Result
                ExitCode  = $LogEntry.ExitCode
                Message   = $LogEntry.Message.Replace("`n", ' ')
            }
            if (-not (Test-Path -LiteralPath $csvFile)) {
                @($csvRow) | Export-Csv -LiteralPath $csvFile -NoTypeInformation -Encoding utf8
            } else {
                $csvLine = '"' + $csvRow.Timestamp + '","' + $csvRow.FeatureID + '","' + $csvRow.Action + '","' + $csvRow.Result + '","' + $csvRow.ExitCode + '","' + ($csvRow.Message -replace '"', '""') + '"'
                Add-Content -LiteralPath $csvFile -Value $csvLine -Encoding utf8 -ErrorAction SilentlyContinue
            }
        } catch {}

        # 4. Console Output
        if (-not $NoConsole) {
            $statusColor = switch ($LogEntry.Result) {
                'Success'     { 'Green' }
                'Skipped'     { 'Yellow' }
                'Unsupported' { 'DarkYellow' }
                'AccessDenied'{ 'Red' }
                'SyntaxError' { 'Red' }
                default       { 'Red' }
            }
            Write-Host "  [$($LogEntry.Result)] Feature ID $($LogEntry.FeatureID) ($($LogEntry.Action)) -- $($LogEntry.Message.Replace("`n", ' '))" -ForegroundColor $statusColor
        }
    }
}

function Invoke-FeatureBatch {
    <#
    .SYNOPSIS
        Executes a batch of features sequentially with non-aborting fault tolerance, persistent logging, and metrics summary.
    .PARAMETER Features
        Array of feature IDs (strings) or feature catalog objects.
    .PARAMETER Action
        Action to execute: 'Enable', 'Disable', or 'Reset'. Defaults to 'Enable'.
    .PARAMETER ViVeToolPath
        Path to ViVeTool executable or simulator shim.
    .PARAMETER LogPath
        Base file path or directory for log output.
    .PARAMETER DryRun
        If set, performs simulated run without invoking binaries.
    .PARAMETER NonDestructive
        Enforces test protection mode.
    #>
    [CmdletBinding(DefaultParameterSetName = 'Default', SupportsShouldProcess = $true)]
    [OutputType([PSCustomObject[]])]
    param(
        [Parameter(Mandatory = $true, Position = 0)]
        [object[]]$Features,

        [Parameter(Position = 1)]
        [ValidateSet('Enable', 'Disable', 'Reset')]
        [string]$Action = 'Enable',

        [Parameter()]
        [string]$ViVeToolPath,

        [Parameter()]
        [string]$LogPath,

        [Parameter()]
        [switch]$DryRun,

        [Parameter()]
        [switch]$NonDestructive
    )

    $startTime = Get-Date

    # 1. Normalize and deduplicate Feature IDs
    $idList = [System.Collections.Generic.List[string]]::new()
    $featuresQueue = [System.Collections.Queue]::new()
    if ($Features -is [System.Collections.IEnumerable] -and -not ($Features -is [string]) -and -not ($Features -is [System.Management.Automation.PSCustomObject])) {
        foreach ($f in $Features) { $featuresQueue.Enqueue($f) }
    } else {
        $featuresQueue.Enqueue($Features)
    }

    while ($featuresQueue.Count -gt 0) {
        $item = $featuresQueue.Dequeue()
        if ($null -eq $item) { continue }
        if ($item -is [System.Collections.IEnumerable] -and -not ($item -is [string]) -and -not ($item -is [System.Management.Automation.PSCustomObject])) {
            foreach ($sub in $item) { $featuresQueue.Enqueue($sub) }
            continue
        }

        $id = if ($item -is [System.Management.Automation.PSCustomObject] -and $item.PSObject.Properties['FeatureID'] -and $item.FeatureID) {
            $item.FeatureID.ToString().Trim()
        } elseif ($item -is [System.Management.Automation.PSCustomObject] -and $item.PSObject.Properties['Id'] -and $item.Id) {
            $item.Id.ToString().Trim()
        } else {
            $item.ToString().Trim()
        }

        if ($id -and -not [string]::IsNullOrWhiteSpace($id)) {
            if (-not $idList.Contains($id)) {
                $idList.Add($id)
            }
        }
    }

    if ($idList.Count -eq 0) {
        Write-Warning "[Invoke-FeatureBatch] No feature IDs specified or matched filter criteria."
        return @()
    }

    Write-Host "================================================================================" -ForegroundColor Cyan
    Write-Host " ViVeTool Feature Batch Execution ($Action) -- $($idList.Count) Unique Features" -ForegroundColor Cyan
    Write-Host " Mode: $(if ($DryRun) { 'DryRun (Simulation)' } else { 'Live Execution' })" -ForegroundColor DarkCyan
    Write-Host "================================================================================" -ForegroundColor Cyan

    $results = [System.Collections.Generic.List[PSCustomObject]]::new()
    $successCount = 0
    $unsupportedCount = 0
    $skippedCount = 0
    $errorCount = 0
    $accessDeniedCount = 0

    $index = 0
    $total = $idList.Count

    foreach ($id in $idList) {
        $index++
        Write-Progress -Activity "Executing ViVeTool Batch ($Action)" -Status "Processing feature $id ($index of $total)" -PercentComplete ([int](($index / $total) * 100))

        try {
            $entry = Invoke-ViVeToolFeature -FeatureId $id `
                                            -Action $Action `
                                            -ViVeToolPath $ViVeToolPath `
                                            -DryRun:$DryRun

            $results.Add($entry)
            Write-FeatureLog -LogEntry $entry -LogPath $LogPath

            switch ($entry.Result) {
                'Success'      { $successCount++ }
                'Unsupported'  { $unsupportedCount++ }
                'Skipped'      { $skippedCount++ }
                'AccessDenied' { $accessDeniedCount++; $errorCount++ }
                default        { $errorCount++ }
            }
        } catch {
            Write-Warning "[Invoke-FeatureBatch] Unexpected failure executing ID $id : $($_.Exception.Message)"
            $errEntry = [PSCustomObject]@{
                Timestamp  = (Get-Date -Format 'o')
                FeatureID  = $id
                Action     = $Action
                Result     = "Error"
                ExitCode   = 255
                Message    = "Exception: $($_.Exception.Message)"
                DurationMs = 0
            }
            $results.Add($errEntry)
            Write-FeatureLog -LogEntry $errEntry -LogPath $LogPath
            $errorCount++
        }
    }

    Write-Progress -Activity "Executing ViVeTool Batch ($Action)" -Completed
    $endTime = Get-Date
    $durationSec = [math]::Round(($endTime - $startTime).TotalSeconds, 2)

    # Summary Object
    $summaryObj = [PSCustomObject]@{
        Action             = $Action
        StartTime          = $startTime.ToString('o')
        EndTime            = $endTime.ToString('o')
        DurationSeconds    = $durationSec
        Total              = $total
        TotalFeatures      = $total
        SuccessCount       = $successCount
        UnsupportedCount   = $unsupportedCount
        SkippedCount       = $skippedCount
        AccessDeniedCount  = $accessDeniedCount
        ErrorCount         = $errorCount
        ExecutionMode      = if ($DryRun) { "DryRun" } else { "Live" }
    }

    # Write summary JSON sink
    try {
        $baseName = if ($Action -eq 'Disable') { "disable_summary" } else { "summary" }
        $effectiveDir = $null
        if ($LogPath) {
            if ($LogPath.EndsWith(".log", [System.StringComparison]::OrdinalIgnoreCase) -or $LogPath.EndsWith(".csv", [System.StringComparison]::OrdinalIgnoreCase) -or $LogPath.EndsWith(".json", [System.StringComparison]::OrdinalIgnoreCase)) {
                $effectiveDir = [System.IO.Path]::GetDirectoryName($LogPath)
            } else {
                $effectiveDir = $LogPath
            }
        }
        if (-not $effectiveDir) {
            $effectiveDir = Join-Path -Path $script:DefaultTargetDirectory -ChildPath "logs"
        }
        if (-not (Test-Path -LiteralPath $effectiveDir)) {
            New-Item -ItemType Directory -Path $effectiveDir -Force -ErrorAction SilentlyContinue | Out-Null
        }
        $jsonSummaryPath = Join-Path -Path $effectiveDir -ChildPath "$baseName.json"
        $summaryObj | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath $jsonSummaryPath -Encoding utf8 -ErrorAction SilentlyContinue
    } catch {}

    Write-Host "`n================================================================================" -ForegroundColor Cyan
    Write-Host " Batch Execution Summary ($Action)" -ForegroundColor Cyan
    Write-Host "================================================================================" -ForegroundColor Cyan
    Write-Host " Total Processed : $total" -ForegroundColor White
    Write-Host " Success         : $successCount" -ForegroundColor Green
    Write-Host " Unsupported     : $unsupportedCount" -ForegroundColor $(if ($unsupportedCount -gt 0) { 'DarkYellow' } else { 'Gray' })
    Write-Host " Skipped         : $skippedCount" -ForegroundColor $(if ($skippedCount -gt 0) { 'Yellow' } else { 'Gray' })
    Write-Host " Errors / Denied : $errorCount" -ForegroundColor $(if ($errorCount -gt 0) { 'Red' } else { 'Gray' })
    Write-Host " Elapsed Time    : ${durationSec}s" -ForegroundColor White
    Write-Host "================================================================================" -ForegroundColor Cyan

    return $results.ToArray()
}

function Restart-ExplorerProcess {
    <#
    .SYNOPSIS
        Restarts the Windows Explorer process (explorer.exe) to apply new shell configurations with safety guards and watchdog.
    .PARAMETER Force
        Forces immediate termination of explorer.exe.
    .PARAMETER WatchdogTimeoutSec
        Watchdog timeout in seconds to wait for explorer.exe auto-recovery before manually launching it. Defaults to 3.
    #>
    [CmdletBinding(DefaultParameterSetName = 'Default', SupportsShouldProcess = $true)]
    [OutputType([bool])]
    param(
        [Parameter()]
        [switch]$Force,

        [Parameter()]
        [ValidateRange(1, 30)]
        [int]$WatchdogTimeoutSec = 3
    )

    # Test / Non-destructive guard
    if ($env:VIVETOOL_NON_DESTRUCTIVE -eq '1' -or $env:VIVETOOL_TEST_RUNNER -eq '1' -or $env:VIVETOOL_MOCK_MODE) {
        Write-Host "[*] [TEST MODE] Explorer restart intercepted safely (non-destructive test mode active)." -ForegroundColor Cyan
        return $true
    }

    if (-not $PSCmdlet.ShouldProcess("Windows Explorer (explorer.exe)", "Restart shell process")) {
        Write-Verbose "[Restart-ExplorerProcess] Restart skipped by ShouldProcess (-WhatIf)."
        return $true
    }

    Write-Host "[*] Restarting Windows Explorer (explorer.exe) to load new shell configurations..." -ForegroundColor Yellow

    try {
        Stop-Process -Name explorer -Force -ErrorAction SilentlyContinue
    } catch {
        Write-Warning "Could not stop explorer process: $($_.Exception.Message)"
    }

    # Watchdog: Windows normally respawns explorer automatically. If not alive after timeout, start it manually.
    $sw = [System.Diagnostics.Stopwatch]::StartNew()
    $restarted = $false
    while ($sw.Elapsed.TotalSeconds -lt $WatchdogTimeoutSec) {
        Start-Sleep -Milliseconds 500
        $procs = Get-Process -Name explorer -ErrorAction SilentlyContinue
        if ($procs -and $procs.Count -gt 0) {
            $restarted = $true
            break
        }
    }

    if (-not $restarted) {
        Write-Host "    Auto-restart watchdog timeout reached. Explicitly spawning explorer.exe..." -ForegroundColor DarkGray
        try {
            Start-Process explorer.exe
        } catch {
            Write-Warning "Failed to start explorer.exe: $($_.Exception.Message)"
        }
    }

    Write-Host "[+] Windows Explorer successfully restarted." -ForegroundColor Green
    return $true
}

function New-RollbackScript {
    <#
    .SYNOPSIS
        Generates a standalone executable PowerShell rollback script (.ps1) for reverting applied features.
    .DESCRIPTION
        Parses features from explicit IDs, batch execution results, or a CSV session log, and constructs
        a robust, self-contained rollback script configured to execute ViVeTool with the '/disable' verb.
    .PARAMETER Features
        Array of feature IDs, PSCustomObject results from Invoke-FeatureBatch, or catalog objects.
    .PARAMETER FromLog
        Path to a CSV session log file from which to extract successfully applied feature IDs.
    .PARAMETER OutputPath
        Target file path or directory for the generated rollback script.
    .PARAMETER ViVeToolPath
        Path to ViVeTool executable to reference. Defaults to local or auto-discovery.
    .PARAMETER TargetDirectory
        Base project directory. Defaults to C:\Tools\vivetool_feature_enabler.
    .PARAMETER ReverseOrder
        If specified, orders rollback operations in reverse order of application.
    .PARAMETER RestartExplorer
        If specified, includes Windows Explorer restart in the generated script.
    .PARAMETER PassThru
        If specified, outputs the resolved string path of the generated script.
    #>
    [CmdletBinding(DefaultParameterSetName = 'Default', SupportsShouldProcess = $true)]
    [OutputType([string])]
    param(
        [Parameter(Position = 0, ValueFromPipeline = $true, ParameterSetName = 'Default')]
        [object[]]$Features,

        [Parameter(ParameterSetName = 'FromLog')]
        [string]$FromLog,

        [Parameter()]
        [string]$OutputPath,

        [Parameter()]
        [string]$ViVeToolPath,

        [Parameter()]
        [string]$TargetDirectory = $script:DefaultTargetDirectory,

        [Parameter()]
        [switch]$ReverseOrder,

        [Parameter()]
        [switch]$RestartExplorer,

        [Parameter()]
        [switch]$PassThru = $true
    )

    # 1. Determine Target Feature IDs
    $idList = [System.Collections.Generic.List[string]]::new()

    if ($FromLog) {
        if (-not (Test-Path -LiteralPath $FromLog)) {
            throw [System.IO.FileNotFoundException]::new("Log file not found: $FromLog")
        }

        try {
            $records = Import-Csv -LiteralPath $FromLog -ErrorAction Stop
            if ($records) {
                foreach ($rec in $records) {
                    if ($rec.PSObject.Properties['Result']) {
                        if ($rec.Result -eq 'Success' -or $rec.Result -eq 'Skipped') {
                            $id = if ($rec.PSObject.Properties['FeatureID']) { $rec.FeatureID } elseif ($rec.PSObject.Properties['Id']) { $rec.Id } else { $null }
                            if ($id -and -not [string]::IsNullOrWhiteSpace($id)) {
                                $idStr = $id.ToString().Trim()
                                if (-not $idList.Contains($idStr)) {
                                    $idList.Add($idStr)
                                }
                            }
                        }
                    } else {
                        $id = if ($rec.PSObject.Properties['FeatureID']) { $rec.FeatureID } elseif ($rec.PSObject.Properties['Id']) { $rec.Id } else { $null }
                        if ($id -and -not [string]::IsNullOrWhiteSpace($id)) {
                            $idStr = $id.ToString().Trim()
                            if (-not $idList.Contains($idStr)) {
                                $idList.Add($idStr)
                            }
                        }
                    }
                }
            }
        } catch [System.IO.FileNotFoundException] {
            throw
        } catch {
            Write-Warning "[New-RollbackScript] Error parsing CSV log: $($_.Exception.Message)"
        }
    } elseif ($Features -and $Features.Count -gt 0) {
        $queue = [System.Collections.Queue]::new()
        foreach ($f in $Features) { $queue.Enqueue($f) }

        while ($queue.Count -gt 0) {
            $item = $queue.Dequeue()
            if ($null -eq $item) { continue }
            if ($item -is [System.Collections.IEnumerable] -and -not ($item -is [string]) -and -not ($item -is [System.Management.Automation.PSCustomObject])) {
                foreach ($sub in $item) { $queue.Enqueue($sub) }
                continue
            }

            if ($item -is [System.Management.Automation.PSCustomObject]) {
                if ($item.PSObject.Properties['Result']) {
                    if ($item.Result -ne 'Success' -and $item.Result -ne 'Skipped') {
                        continue
                    }
                }
                $id = if ($item.PSObject.Properties['FeatureID'] -and $item.FeatureID) {
                    $item.FeatureID.ToString().Trim()
                } elseif ($item.PSObject.Properties['Id'] -and $item.Id) {
                    $item.Id.ToString().Trim()
                } else {
                    $item.ToString().Trim()
                }
                if ($id -and -not [string]::IsNullOrWhiteSpace($id) -and -not $idList.Contains($id)) {
                    $idList.Add($id)
                }
            } else {
                $id = $item.ToString().Trim()
                if ($id -and -not [string]::IsNullOrWhiteSpace($id) -and -not $idList.Contains($id)) {
                    $idList.Add($id)
                }
            }
        }
    } else {
        # Fallback to entire catalog
        $catalogPath = Join-Path -Path $TargetDirectory -ChildPath "FeatureCatalog.json"
        $catalog = Get-FeatureCatalog -CatalogPath $catalogPath
        foreach ($item in $catalog) {
            $id = $item.FeatureID
            if ($id -and -not $idList.Contains($id)) {
                $idList.Add($id)
            }
        }
    }

    # 2. Reverse ordering if requested
    if ($ReverseOrder -and $idList.Count -gt 1) {
        $idArray = $idList.ToArray()
        [array]::Reverse($idArray)
        $idList.Clear()
        foreach ($x in $idArray) { $idList.Add($x) }
    }

    # 3. Determine Output File Path
    $timestamp = Get-Date -Format 'yyyyMMdd_HHmmss'
    $defaultLogsDir = Join-Path -Path $TargetDirectory -ChildPath "logs"
    $targetFile = $null

    if ($OutputPath) {
        if ($OutputPath.EndsWith(".ps1", [System.StringComparison]::OrdinalIgnoreCase)) {
            $targetFile = $OutputPath
            $parentDir = [System.IO.Path]::GetDirectoryName($targetFile)
            if ($parentDir -and -not (Test-Path -LiteralPath $parentDir)) {
                New-Item -ItemType Directory -Path $parentDir -Force -ErrorAction SilentlyContinue | Out-Null
            }
        } else {
            if (-not (Test-Path -LiteralPath $OutputPath)) {
                New-Item -ItemType Directory -Path $OutputPath -Force -ErrorAction SilentlyContinue | Out-Null
            }
            $targetFile = Join-Path -Path $OutputPath -ChildPath "rollback_$timestamp.ps1"
        }
    } else {
        try {
            if (-not (Test-Path -LiteralPath $defaultLogsDir)) {
                New-Item -ItemType Directory -Path $defaultLogsDir -Force -ErrorAction Stop | Out-Null
            }
            $targetFile = Join-Path -Path $defaultLogsDir -ChildPath "rollback_$timestamp.ps1"
        } catch {
            $targetFile = Join-Path -Path $env:TEMP -ChildPath "rollback_$timestamp.ps1"
        }
    }

    # 4. Generate Script Body
    $sb = [System.Text.StringBuilder]::new()
    [void]$sb.AppendLine("<#")
    [void]$sb.AppendLine(".SYNOPSIS")
    [void]$sb.AppendLine("    Auto-generated ViVeTool Rollback Script.")
    [void]$sb.AppendLine(".DESCRIPTION")
    [void]$sb.AppendLine("    Generated: $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')")
    [void]$sb.AppendLine("    Reverts $($idList.Count) features via 'vivetool /disable /id:<id>'.")
    [void]$sb.AppendLine("#>")
    [void]$sb.AppendLine("[CmdletBinding(SupportsShouldProcess = `$true)]")
    [void]$sb.AppendLine("param(")
    [void]$sb.AppendLine("    [Parameter()] [string]`$ViVeToolPath,")
    [void]$sb.AppendLine("    [Parameter()] [switch]`$DryRun,")
    [void]$sb.AppendLine("    [Parameter()] [switch]`$RestartExplorer$(if ($RestartExplorer) { ' = $true' }),")
    [void]$sb.AppendLine("    [Parameter()] [string]`$LogPath")
    [void]$sb.AppendLine(")")
    [void]$sb.AppendLine("")
    [void]$sb.AppendLine("`$ErrorActionPreference = 'Continue'")
    [void]$sb.AppendLine("")
    [void]$sb.AppendLine("# Auto-generated ViVeTool Rollback Script")
    [void]$sb.AppendLine("Write-Host '================================================================================' -ForegroundColor Cyan")
    [void]$sb.AppendLine("Write-Host ' Executing ViVeTool Feature Rollback ($($idList.Count) Features)' -ForegroundColor Cyan")
    [void]$sb.AppendLine("Write-Host '================================================================================' -ForegroundColor Cyan")
    [void]$sb.AppendLine("")
    [void]$sb.AppendLine("# Locate ViVeTool Executable")
    [void]$sb.AppendLine("`$resolvedExe = `$ViVeToolPath")
    [void]$sb.AppendLine("if (-not `$resolvedExe -and `$env:VIVETOOL_MOCK_MODE) {")
    [void]$sb.AppendLine("    `$mockCandidates = @(")
    [void]$sb.AppendLine("        (Join-Path `$PSScriptRoot 'MockViVeTool.ps1'),")
    [void]$sb.AppendLine("        (Join-Path (Split-Path `$PSScriptRoot -Parent) 'tests\MockViVeTool.ps1'),")
    [void]$sb.AppendLine("        (Join-Path (Split-Path `$PSScriptRoot -Parent) 'MockViVeTool.ps1'),")
    [void]$sb.AppendLine("        'C:\Tools\vivetool_feature_enabler\tests\MockViVeTool.ps1'")
    [void]$sb.AppendLine("    )")
    [void]$sb.AppendLine("    foreach (`$mc in `$mockCandidates) {")
    [void]$sb.AppendLine("        if (Test-Path -LiteralPath `$mc) { `$resolvedExe = `$mc; break }")
    [void]$sb.AppendLine("    }")
    [void]$sb.AppendLine("}")
    [void]$sb.AppendLine("if (-not `$resolvedExe) {")
    [void]$sb.AppendLine("    `$exeCandidates = @(")
    [void]$sb.AppendLine("        (Join-Path `$PSScriptRoot 'ViVeTool.exe'),")
    [void]$sb.AppendLine("        (Join-Path (Split-Path `$PSScriptRoot -Parent) 'ViVeTool.exe'),")
    [void]$sb.AppendLine("        'C:\Tools\vivetool_feature_enabler\ViVeTool.exe'")
    [void]$sb.AppendLine("    )")
    [void]$sb.AppendLine("    foreach (`$c in `$exeCandidates) {")
    [void]$sb.AppendLine("        if (Test-Path -LiteralPath `$c) { `$resolvedExe = `$c; break }")
    [void]$sb.AppendLine("    }")
    [void]$sb.AppendLine("}")
    [void]$sb.AppendLine("if (-not `$resolvedExe) { `$resolvedExe = 'vivetool.exe' }")
    [void]$sb.AppendLine("")
    [void]$sb.AppendLine("`$successCount = 0")
    [void]$sb.AppendLine("`$totalFeatures = $($idList.Count)")
    [void]$sb.AppendLine("")

    foreach ($id in $idList) {
        [void]$sb.AppendLine('if ($DryRun) {')
        [void]$sb.AppendLine("    Write-Host '[DryRun] Would execute: vivetool /disable /id:$id' -ForegroundColor Yellow")
        [void]$sb.AppendLine('} else {')
        [void]$sb.AppendLine("    & `$resolvedExe /disable /id:$id")
        [void]$sb.AppendLine('    if ($LASTEXITCODE -eq 0) { $successCount++ }')
        [void]$sb.AppendLine('}')
    }

    [void]$sb.AppendLine('')
    [void]$sb.AppendLine('Write-Host "`n[+] Rollback complete: $successCount of $totalFeatures features reverted." -ForegroundColor Green')
    [void]$sb.AppendLine('')
    [void]$sb.AppendLine('if ($RestartExplorer -and -not $DryRun -and -not $env:VIVETOOL_NON_DESTRUCTIVE) {')
    [void]$sb.AppendLine("    Write-Host '[*] Restarting Windows Explorer...' -ForegroundColor Yellow")
    [void]$sb.AppendLine("    Stop-Process -Name explorer -Force -ErrorAction SilentlyContinue")
    [void]$sb.AppendLine('}')
    [void]$sb.AppendLine('')
    [void]$sb.AppendLine('exit 0')

    # 5. Write to Target File
    if ($PSCmdlet.ShouldProcess($targetFile, "Write generated rollback script")) {
        try {
            [System.IO.File]::WriteAllText($targetFile, $sb.ToString(), [System.Text.Encoding]::UTF8)
        } catch {
            Set-Content -LiteralPath $targetFile -Value $sb.ToString() -Encoding utf8 -ErrorAction Stop
        }
    }

    return (Resolve-Path -LiteralPath $targetFile).Path
}

# ------------------------------------------------------------------------------
# SECTION 4: Export Module Members
# ------------------------------------------------------------------------------

Export-ModuleMember -Function @(
    'Ensure-ViVeTool',
    'Invoke-SelfElevation',
    'Test-IsAdministrator',
    'Get-SystemArchitecture',
    'Get-FeatureCatalog',
    'Invoke-ViVeToolFeature',
    'Invoke-FeatureBatch',
    'Write-FeatureLog',
    'Restart-ExplorerProcess',
    'New-RollbackScript'
)
