#Requires -Version 5.1
<#
.SYNOPSIS
  Provisions the WinForge Multica workspace: workspace, repo, labels, skills,
  agents, squad, and starter issues.
.DESCRIPTION
  Idempotent best-effort automation over the Multica CLI. Flag names vary by
  CLI version — when a command fails the script warns and continues, and the
  README in this folder gives the manual UI fallback for that step.
  Requires: multica CLI (>= 0.4.26), `multica setup`/`login` done, one online
  runtime (`multica daemon start`, preferably with the OpenCode tool installed).
.EXAMPLE
  .\provision.ps1
.EXAMPLE
  .\provision.ps1 -RuntimeId <runtime-uuid> -SkipIssues
#>
[CmdletBinding()]
param(
  [string]$WorkspaceSlug = "winforge",
  [string]$WorkspaceName = "WinForge",
  [string]$IssuePrefix = "WF",
  [string]$RepoUrl = "https://github.com/jacob-krarup-madsen/WinForge.git",
  [string]$RuntimeId = "",
  [switch]$SkipIssues
)

$ErrorActionPreference = "Stop"
$BundleRoot = Split-Path -Parent $MyInvocation.MyCommand.Path

function Write-Step([string]$Message) { Write-Host ""; Write-Host "==> $Message" -ForegroundColor Cyan }
function Write-Warn2([string]$Message) { Write-Warning $Message }

function Invoke-Multica {
  param([string[]]$Args, [switch]$AllowFail)
  $out = & multica @Args 2>&1
  $code = $LASTEXITCODE
  if ($code -ne 0 -and -not $AllowFail) { throw "multica $($Args -join ' ') failed:`n$out" }
  return @{ Code = $code; Output = ($out | Out-String) }
}

function Get-JsonList {
  param([string[]]$Args)
  $r = Invoke-Multica -Args $Args -AllowFail
  if ($r.Code -ne 0) { return @() }
  try { $parsed = $r.Output | ConvertFrom-Json; if ($parsed -is [array]) { return $parsed }; return @($parsed) }
  catch { return @() }
}

# 0. Preconditions -------------------------------------------------------------
Write-Step "Checking multica CLI + auth"
try { Invoke-Multica -Args @("version") | Out-Null } catch { throw "multica CLI not found. Install it first (see README.md). $_" }
Invoke-Multica -Args @("auth", "status") -AllowFail | Out-Null

# 1. Workspace ----------------------------------------------------------------
Write-Step "Workspace '$WorkspaceSlug'"
$workspaces = Get-JsonList -Args @("workspace", "list", "--output", "json")
if ($workspaces | Where-Object { $_.slug -eq $WorkspaceSlug -or $_.name -eq $WorkspaceName }) {
  Write-Host "Workspace already exists, reusing it."
} else {
  $r = Invoke-Multica -Args @("workspace", "create", "--name", $WorkspaceName, "--slug", $WorkspaceSlug, "--issue-prefix", $IssuePrefix) -AllowFail
  if ($r.Code -ne 0) { Write-Warn2 "workspace create failed (may already exist or flag mismatch). Output:`n$($r.Output)" }
}
$r = Invoke-Multica -Args @("workspace", "switch", $WorkspaceSlug) -AllowFail
if ($r.Code -ne 0) { Write-Warn2 "workspace switch failed. Run: multica workspace switch $WorkspaceSlug. Output:`n$($r.Output)" }

Write-Host ""
Write-Host "ACTION NEEDED (UI): open workspace Settings and paste this file into Context:" -ForegroundColor Yellow
Write-Host "  $(Join-Path $BundleRoot 'workspace-context.md')"

# 2. Repo link (best effort) ----------------------------------------------------
Write-Step "Linking repo $RepoUrl"
$repos = Get-JsonList -Args @("repo", "list", "--output", "json")
$already = $false
foreach ($repo in $repos) { if ("$($repo.url) $($repo.remote) $($repo.name)" -like "*WinForge*") { $already = $true } }
if (-not $already) {
  $r = Invoke-Multica -Args @("repo", "add", "--url", $RepoUrl) -AllowFail
  if ($r.Code -ne 0) { Write-Warn2 "repo add failed. Add it in the UI under repos. Output:`n$($r.Output)" }
} else { Write-Host "Repo already linked." }

# 3. Labels (best effort) -------------------------------------------------------
Write-Step "Labels"
$labelDefs = @(
  @{ Name = "area:ui"; Color = "#3b82f6"; Description = "WinUI frontend work" },
  @{ Name = "area:winget"; Color = "#22c55e"; Description = "Winget store work" },
  @{ Name = "area:vive"; Color = "#eab308"; Description = "ViVeTool features work" },
  @{ Name = "area:optimizer"; Color = "#ef4444"; Description = "Optimizer safety work" },
  @{ Name = "area:quality"; Color = "#a855f7"; Description = "Build/test quality gate" }
)
foreach ($label in $labelDefs) {
  $r = Invoke-Multica -Args @("label", "create", "--name", $label.Name, "--color", $label.Color,
    "--resource-type", "issue", "--description", $label.Description) -AllowFail
  if ($r.Code -ne 0) { Write-Host "label '$($label.Name)' skipped (exists or flag mismatch)." }
}

# 4. Skills --------------------------------------------------------------------
Write-Step "Skills (zip + import, then resolve IDs)"
$skillDirs = @(
  (Join-Path $BundleRoot "skills/winforge-conventions"),
  (Join-Path $BundleRoot "skills/optimizer-safety-review")
)
foreach ($dir in $skillDirs) {
  $zip = "$dir.skill.zip"
  try {
    if (Test-Path -LiteralPath $zip) { Remove-Item -LiteralPath $zip -Force }
    Compress-Archive -LiteralPath (Join-Path $dir "*") -DestinationPath $zip -Force
    $r = Invoke-Multica -Args @("skill", "import", "--file", $zip, "--on-conflict", "skip") -AllowFail
    if ($r.Code -ne 0) { Write-Warn2 "skill import failed for $(Split-Path $dir -Leaf). Import the folder in the UI (Skills > New skill > Import from local). Output:`n$($r.Output)" }
    else { Write-Host "imported $(Split-Path $dir -Leaf)" }
  } catch { Write-Warn2 "skill packaging failed for $dir : $_" }
}
$skills = Get-JsonList -Args @("skill", "list", "--output", "json")
function Find-SkillId([string]$Name) {
  $hit = $skills | Where-Object { $_.name -eq $Name } | Select-Object -First 1
  if ($hit) { return $hit.id } ; return ""
}
$conventionsId = Find-SkillId "winforge-conventions"
$safetyId = Find-SkillId "optimizer-safety-review"
Write-Host "skill IDs: winforge-conventions='$conventionsId' optimizer-safety-review='$safetyId'"

# 5. Runtime --------------------------------------------------------------------
Write-Step "Runtime discovery"
if ([string]::IsNullOrWhiteSpace($RuntimeId)) {
  $runtimes = Get-JsonList -Args @("runtime", "list", "--output", "json")
  if ($runtimes.Count -eq 1) { $RuntimeId = $runtimes[0].id; Write-Host "Using the only runtime: $($runtimes[0].name) ($RuntimeId)" }
  elseif ($runtimes.Count -gt 1) {
    Write-Host "Multiple runtimes found:"
    $runtimes | ForEach-Object { Write-Host "  $($_.id)  $($_.name)" }
    throw "Pass the runtime explicitly: .\provision.ps1 -RuntimeId <runtime-uuid>. Prefer a runtime with the OpenCode tool installed (Multica supports OpenCode as an execution tool)."
  } else {
    throw "No runtimes found. Start one first: multica daemon start (Desktop app registers automatically), then re-run with -RuntimeId. See README.md."
  }
} else { Write-Host "Using runtime: $RuntimeId" }

# 6. Agents ---------------------------------------------------------------------
Write-Step "Agents"
$agentFiles = @("winforge-lead", "winui-mvvm", "winget-core", "vive-features", "optimizer-safety", "dotnet-quality")
$agentStarters = @{
  "winforge-lead"    = '[{"label":"Triage an issue","prompt":"Read the open issue I link, decide which specialist owns it, and delegate it with an @-mention and a short brief."},{"label":"Plan a feature","prompt":"Break the feature I describe into sub-issues per specialist with acceptance criteria."},{"label":"Status roundup","prompt":"Summarize squad workload: working, queued, idle, blocked-on-human."}]'
  "winui-mvvm"       = '[{"label":"Fix a page bug","prompt":"Fix the WinUI bug I describe; keep business logic out of code-behind."},{"label":"Add a ViewModel command","prompt":"Add the command I describe to the right ViewModel with [RelayCommand]/[ObservableProperty]."},{"label":"Review XAML","prompt":"Review the XAML diff I link for MVVM, nullable, and layout issues."}]'
  "winget-core"      = '[{"label":"Harden parsing","prompt":"Harden WingetParser against the winget output sample I paste and add edge-case tests."},{"label":"Fix caching","prompt":"Fix the duplicate/stale-cache behavior I describe in CachingWingetService."},{"label":"Triage a failure","prompt":"Triage the winget failure I describe: CLI vs parser vs cache vs task lifecycle."}]'
  "vive-features"    = '[{"label":"Fix a filter","prompt":"Fix the release-track search/filter behavior I describe with pure unit tests."},{"label":"Add batch op","prompt":"Add the ViVe batch flow I describe with progress, logging, undo — dry-run only."},{"label":"Triage catalog sync","prompt":"Triage the Pureinfotech sync failure I describe with offline fallback."}]'
  "optimizer-safety" = '[{"label":"Audit a tweak","prompt":"Safety-audit the tweak I describe: snapshot, undo, elevation, opt-in."},{"label":"Add a tweak","prompt":"Add the optimizer tweak I describe with snapshot + undo + mocked tests. No live Apply."},{"label":"Review cleanup","prompt":"Review the disk/WinSxS/memory change I describe for reparse-point safety."}]'
  "dotnet-quality"   = '[{"label":"Gate a change","prompt":"Run the build + test gate on the change I describe and triage failures."},{"label":"Fix warnings","prompt":"Fix the TreatWarningsAsErrors warnings I paste by root cause with warning IDs."},{"label":"Add tests","prompt":"Add xUnit v3 coverage for the service/ViewModel/helper I name."}]'
}
$agentConcurrency = @{ "optimizer-safety" = "1" }
$existingAgents = Get-JsonList -Args @("agent", "list", "--output", "json")
$agentIds = @{}
foreach ($name in $agentFiles) {
  $hit = $existingAgents | Where-Object { $_.name -eq $name } | Select-Object -First 1
  if ($hit) { Write-Host "agent '$name' exists, reusing."; $agentIds[$name] = $hit.id; continue }
  $md = Join-Path $BundleRoot "agents/$name.md"
  $instructions = Get-Content -LiteralPath $md -Raw
  $desc = switch ($name) {
    "winforge-lead"    { "Routes WinForge work to the right specialist and coordinates delivery. Squad leader." }
    "winui-mvvm"       { "WinUI 3 + MVVM frontend specialist for Pages, Controls, ViewModels, navigation and Fluent/Mica UI." }
    "winget-core"      { "Winget package-store specialist for WingetService, parser, caching, CLI streaming and install tasks." }
    "vive-features"    { "ViVeTool velocity-features specialist for feature flags, ViVe interop, filtering and catalog sync." }
    "optimizer-safety" { "Native optimizer safety owner for 25-phase tweaks, snapshots, disk/memory/audit/benchmark reversibility." }
    "dotnet-quality"   { ".NET quality gate for zero-warning builds, xUnit v3 tests, nullable and Roslyn enforcement." }
    default { "$name agent for WinForge" }
  }
  $createArgs = @("agent", "create", "--name", $name, "--runtime-id", $RuntimeId,
    "--description", $desc, "--instructions", $instructions,
    "--conversation-starters", $agentStarters[$name])
  if ($agentConcurrency.ContainsKey($name)) { $createArgs += @("--max-concurrent-tasks", $agentConcurrency[$name]) }
  $r = Invoke-Multica -Args $createArgs -AllowFail
  if ($r.Code -ne 0) { Write-Warn2 "agent create failed for '$name'. Create it in the UI from multica-workspace/agents/$name.md. Output:`n$($r.Output)" }
  else {
    try { $created = $r.Output | ConvertFrom-Json; $agentIds[$name] = $created.id } catch { }
    Write-Host "created agent '$name'"
  }
}
if ($agentIds.Count -eq 0) {
  $existingAgents = Get-JsonList -Args @("agent", "list", "--output", "json")
  foreach ($a in $existingAgents) { if ($agentFiles -contains $a.name) { $agentIds[$a.name] = $a.id } }
}
Write-Host ""
Write-Host "ACTION NEEDED (UI): set each agent's Access to 'Entire workspace' so the squad leader can delegate." -ForegroundColor Yellow

# 7. Skill bindings --------------------------------------------------------------
Write-Step "Attaching skills to agents"
function Add-Skills([string]$AgentName, [string[]]$SkillIds) {
  if (-not $agentIds.ContainsKey($AgentName)) { Write-Warn2 "unknown agent '$AgentName', skipping skill bind."; return }
  $valid = $SkillIds | Where-Object { -not [string]::IsNullOrWhiteSpace($_) }
  if ($valid.Count -eq 0) { Write-Host "no skill IDs to bind for $AgentName (import them first)."; return }
  $r = Invoke-Multica -Args (@("agent", "skills", "add", $agentIds[$AgentName], "--skill-ids") + $valid) -AllowFail
  if ($r.Code -ne 0) { Write-Warn2 "skill bind failed for $AgentName. Bind in the UI (agent > Skills). Output:`n$($r.Output)" }
}
Add-Skills "winforge-lead" @($conventionsId)
Add-Skills "winui-mvvm" @($conventionsId)
Add-Skills "winget-core" @($conventionsId)
Add-Skills "vive-features" @($conventionsId)
Add-Skills "optimizer-safety" @($conventionsId, $safetyId)
Add-Skills "dotnet-quality" @($conventionsId)

# 8. Squad -----------------------------------------------------------------------
Write-Step "Squad 'WinForge Delivery'"
$squads = Get-JsonList -Args @("squad", "list", "--output", "json")
$squad = $squads | Where-Object { $_.name -eq "WinForge Delivery" } | Select-Object -First 1
$squadInstructions = "Routing: UI/XAML/navigation -> @winui-mvvm; search/install/cache/parsing -> @winget-core; flags/ViVe/catalog -> @vive-features; tweaks/registry/WMI/cleanup/audit/bench -> @optimizer-safety (always include on optimizer work, concurrency 1); build/warnings/tests -> @dotnet-quality as final gate. Cross-cutting features fan out to relevant specialists then the quality gate. Enforce: no live winget/ViVe/optimizer runs without explicit approval; Tools/ViVeTool.exe never modified; MVVM strict; zero-warning."
if (-not $squad) {
  $r = Invoke-Multica -Args @("squad", "create", "--name", "WinForge Delivery", "--leader", "winforge-lead") -AllowFail
  if ($r.Code -ne 0) { Write-Warn2 "squad create failed. Create 'WinForge Delivery' (leader winforge-lead) in the UI. Output:`n$($r.Output)" }
  else { $squads = Get-JsonList -Args @("squad", "list", "--output", "json"); $squad = $squads | Where-Object { $_.name -eq "WinForge Delivery" } | Select-Object -First 1 }
}
if ($squad) {
  try {
    $r = Invoke-Multica -Args @("squad", "update", $squad.id, "--instructions", $squadInstructions) -AllowFail
    if ($r.Code -ne 0) { Write-Host "squad instructions left for UI (update flag mismatch). Paste from this script's `$squadInstructions`." }
  } catch { Write-Host "squad instructions left for UI." }
  $roles = @{
    "winui-mvvm"       = "Owns WinUI frontend implementation (Pages, Controls, ViewModels, navigation)."
    "winget-core"      = "Owns winget services, parsing, caching, CLI streaming, install tasks."
    "vive-features"    = "Owns ViVeTool flags, interop, filtering, catalog sync (dry-run default)."
    "optimizer-safety" = "Owns optimizer tweaks with mandatory snapshot/undo; safety veto on risky changes."
    "dotnet-quality"   = "Owns final build + xUnit v3 gate and triages failures to owners."
  }
  foreach ($member in $roles.Keys) {
    if (-not $agentIds.ContainsKey($member)) { continue }
    $r = Invoke-Multica -Args @("squad", "member", "add", $squad.id, "--member-id", $agentIds[$member], "--type", "agent", "--role", $roles[$member]) -AllowFail
    if ($r.Code -ne 0) { Write-Host "squad member '$member' skipped (exists or flag mismatch)." }
  }
}

# 9. Starter issues ---------------------------------------------------------------
if (-not $SkipIssues) {
  Write-Step "Starter issues"
  $issueFiles = @(
    @{ File = "01-winget-parser-hardening.md"; Title = "Harden WingetParser against locale/version variance"; Assignee = "winget-core"; Priority = "high" },
    @{ File = "02-vive-filter-batch.md"; Title = "ViVe feature filtering + batch flow dry-run"; Assignee = "vive-features"; Priority = "high" },
    @{ File = "03-optimizer-safety-audit.md"; Title = "Optimizer safety audit: snapshot/undo + cleanup guarantees"; Assignee = "optimizer-safety"; Priority = "highest" },
    @{ File = "04-ui-responsive-pass.md"; Title = "Responsive layout + navigation pass (Mica/Fluent intact)"; Assignee = "winui-mvvm"; Priority = "medium" },
    @{ File = "05-quality-gate.md"; Title = "Quality gate: zero-warning Release + full xUnit v3 suite"; Assignee = "dotnet-quality"; Priority = "high" }
  )
  foreach ($issue in $issueFiles) {
    $r = Invoke-Multica -Args @("issue", "create", "--title", $issue.Title,
      "--description-file", (Join-Path $BundleRoot "issues/$($issue.File)"),
      "--priority", $issue.Priority, "--assignee", $issue.Assignee) -AllowFail
    if ($r.Code -ne 0) { Write-Warn2 "issue create failed for '$($issue.Title)'. Create it in the UI from issues/$($issue.File). Output:`n$($r.Output)" }
    else { Write-Host "created: $($issue.Title)" }
  }
}

Write-Step "Done"
Write-Host "Workspace: $WorkspaceSlug | Squad: WinForge Delivery (leader winforge-lead + 5 specialists)"
Write-Host "Reminder: set agent Access to 'Entire workspace' and paste workspace-context.md into workspace Settings > Context."
