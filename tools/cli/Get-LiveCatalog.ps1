#Requires -Version 5.1
# Get-LiveCatalog.ps1
# Scrapes https://pureinfotech.com/vivetool-codes-enable-features-windows-11/
# and returns a list of [PSCustomObject]@{Group; BuildLabel; Description; IDsRaw; IDs}
# Call: $catalog = . .\Get-LiveCatalog.ps1  -or-  $catalog = Get-LiveCatalog

function Get-LiveCatalog {
    param(
        [string]$Url = "https://pureinfotech.com/vivetool-codes-enable-features-windows-11/",
        [int]$TimeoutSeconds = 20
    )

    # --- 1. Fetch HTML ---
    $html = $null
    try {
        $response = Invoke-WebRequest -Uri $Url -UseBasicParsing -TimeoutSec $TimeoutSeconds `
            -Headers @{ "User-Agent" = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) ViVeToolGUI/2.0" }
        $html = $response.Content
    } catch {
        throw "Failed to fetch page: $_"
    }

    # --- 2. Strip script/style blocks to clean the HTML ---
    $html = [regex]::Replace($html, '<script[^>]*>[\s\S]*?</script>', '', 'IgnoreCase')
    $html = [regex]::Replace($html, '<style[^>]*>[\s\S]*?</style>',  '', 'IgnoreCase')

    # --- 3. Extract just the article entry-content div (between markers) ---
    $startMarker = 'class="entry-content"'
    $endMarker   = '<!-- CONTENT END'
    $startIdx = $html.IndexOf($startMarker)
    $endIdx   = $html.IndexOf($endMarker, [math]::Max(0, $startIdx))
    if ($startIdx -gt 0 -and $endIdx -gt $startIdx) {
        $html = $html.Substring($startIdx, $endIdx - $startIdx)
    }

    # --- 4. Parse sections and items ---
    # Strategy: walk the HTML line by line tracking:
    #   - <h3>/<h4> = new top-level section (GA, 26H2, 25H2, Canary, etc.)
    #   - <strong> containing "Build" = build context label
    #   - <li> containing <code>...numbers...</code> = feature entry

    $results = [System.Collections.Generic.List[PSCustomObject]]::new()

    # Helper: strip all HTML tags
    function Strip-Html([string]$s) {
        $s = [regex]::Replace($s, '<[^>]+>', ' ')
        $s = [System.Net.WebUtility]::HtmlDecode($s)
        $s = $s -replace '\s+', ' '
        return $s.Trim()
    }

    # Normalise IDs: extract all 7-9 digit numbers from a raw code string
    function Parse-IDs([string]$raw) {
        $clean = $raw -replace '[^0-9,]', ' '
        $nums = [regex]::Matches($clean, '\b\d{7,9}\b') | ForEach-Object { [int64]$_.Value }
        return @($nums | Where-Object { $_ -ge 1000000 -and $_ -le 999999999 } | Select-Object -Unique)
    }

    $currentSection  = "General"
    $currentBuild    = ""

    # Split into lines for processing
    $lines = $html -split "`n"

    foreach ($line in $lines) {
        $trimmed = $line.Trim()
        if (-not $trimmed) { continue }

        # Section headers (h3 / h4)
        if ($trimmed -match '<h[34][^>]*>(.*?)</h[34]>') {
            $heading = Strip-Html $Matches[1]
            # Map to clean group names
            $currentSection = switch -Regex ($heading) {
                "General Availability"   { "GA" }
                "26H2"                   { "26H2" }
                "25H2"                   { "25H2" }
                "26H1|Feature Platforms|Canary" { "Canary" }
                default                  { $heading }
            }
            $currentBuild = ""
            continue
        }

        # Build context from <strong> containing "Build" or year like "2026"
        if ($trimmed -match '<strong[^>]*>(.*?)</strong>') {
            $boldText = Strip-Html $Matches[1]
            if ($boldText -match 'Build\s+\d|20\d\d\s+update|20\d\d\s+codes|January|February|March|April|May|June|July|August|September|October|November|December') {
                $currentBuild = $boldText -replace ':$', '' -replace '\s+', ' '
            }
            continue
        }

        # Feature list items: look for <code> in <li>
        if ($trimmed -match '<li[^>]*>.*?<code[^>]*>(.*?)</code>(.*?)</li>') {
            $codeRaw = $Matches[1].Trim() -replace '\s', ''
            $descHtml = $Matches[2]

            # Skip if code doesn't look like feature IDs (no digits)
            if ($codeRaw -notmatch '\d{6,}') { continue }

            $ids = Parse-IDs $codeRaw
            if ($ids.Count -eq 0) { continue }

            $desc = Strip-Html $descHtml
            # Prepend any text before <code> in the li
            if ($trimmed -match '<li[^>]*>(.*?)<code') {
                $beforeCode = Strip-Html $Matches[1]
                if ($beforeCode -and $beforeCode.Length -gt 1) {
                    $desc = "$beforeCode $desc"
                }
            }
            $desc = $desc.Trim(' :.,-')
            if (-not $desc) { $desc = "(No description)" }

            # Clean section label
            $groupLabel = switch ($currentSection) {
                "GA" {
                    # Try to extract year/month from build label
                    if ($currentBuild -match '(20\d\d)\s+(update|codes)') { "GA $($Matches[1])" }
                    elseif ($currentBuild -match '(January|February|March|April|May|June|July|August|September|October|November|December)\s+(20\d\d)') { "GA $($Matches[2])" }
                    elseif ($currentBuild -match '(20\d\d)') { "GA $($Matches[1])" }
                    else { "GA" }
                }
                default { $currentSection }
            }

            $results.Add([PSCustomObject]@{
                Group      = $groupLabel
                BuildLabel = $currentBuild
                Description= $desc
                IDsRaw     = $codeRaw
                IDs        = $ids
            })
        }
    }

    return $results
}
