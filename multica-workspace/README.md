# WinForge × Multica workspace bundle

Provisions a [Multica](https://multica.ai) workspace where AI coding agents
act as teammates: issues get assigned to agents, the squad leader routes
work, runs and history stay attached to the issue. Multica drives the agent
CLIs you already use — including **OpenCode**, so the `.opencode/agents/`
setup in this repo keeps working as the local execution layer.

## What this creates

| Item | Name | Notes |
|---|---|---|
| Workspace | `WinForge` (`winforge`, prefix `WF`) | Context = `workspace-context.md` |
| Repo link | `github.com/jacob-krarup-madsen/WinForge` | Agents check out this repo |
| Skills | `winforge-conventions` (all agents), `optimizer-safety-review` (optimizer + lead) | Imported from `skills/*/` |
| Agents (6) | `winforge-lead` (squad leader), `winui-mvvm`, `winget-core`, `vive-features`, `optimizer-safety` (concurrency 1), `dotnet-quality` | Definitions in `agents/*.md` |
| Squad | `WinForge Delivery` (leader `winforge-lead` + 5 specialists) | Routes by area; quality gates |
| Issues (5) | Parser hardening, ViVe filter/batch, optimizer audit, UI pass, quality gate | `issues/*.md`, pre-assigned |

Agent ↔ repo-area mapping mirrors the codebase: `Pages|Controls|ViewModels`
→ winui-mvvm; `Services/Winget*|CliProcessRunner|CachingWingetService` →
winget-core; `Models/ViVeTool|Services/ViVeTool` → vive-features;
`Services/Optimizer` → optimizer-safety; `Directory.Build.*|sln|csproj|Tests`
→ dotnet-quality.

## Prerequisites

1. Multica CLI ≥ 0.4.26: `irm https://raw.githubusercontent.com/multica-ai/multica/main/scripts/install.ps1 | iex` then `multica version`.
2. Sign in: `multica setup` (cloud) or `multica setup self-host ...`, then `multica auth status`.
3. One online runtime with an agent tool installed: `multica daemon start`
   (Multica Desktop registers automatically). For OpenCode execution, install
   the OpenCode CLI on the runtime machine and sign it in.

## Provision (scripted)

From the repo root (Windows PowerShell 5.1):

```powershell
.\multica-workspace\provision.ps1
# with an explicit runtime, or without starter issues:
.\multica-workspace\provision.ps1 -RuntimeId <runtime-uuid> -SkipIssues
```

The script is idempotent (reuses existing workspace/agents/squad) and
warns + continues when a flag differs from your CLI version — run
`multica <command> --help` to confirm exact flags.

## After the script (required UI steps)

1. **Workspace Settings → Context:** paste `workspace-context.md`.
2. **Each agent → Access → Entire workspace** (new agents default to Only
   me, which would block squad delegation).
3. **Each agent → model:** pick the model/thinking level for its runtime
   (blank = runtime default).
4. Verify: assign one starter issue to the `WinForge Delivery` squad and
   watch the leader delegate to the right specialist.

## Manual fallback (no script)

- Workspace: create `WinForge` / slug `winforge` / prefix `WF`, paste context.
- Skills: Skills → New skill → Import from local → pick
  `multica-workspace/skills/<name>/` (folder containing `SKILL.md`).
- Agents: New agent → Start blank → copy Name / Description /
  Instructions / Conversation starters from `agents/<name>.md` → attach
  skills → Access = Entire workspace (`optimizer-safety` concurrency = 1).
- Squad: New squad `WinForge Delivery`, leader `winforge-lead`, add the 5
  specialists with the roles from `provision.ps1` (`$roles`).
- Issues: create from `issues/*.md` bodies, assign to the named agent
  (or to the squad for routed work).

## Safety model

Multica-level rules duplicate the repo's: no live winget installs, no live
ViVe enable/disable, no optimizer Apply/Undo/cleanup/flush without explicit
issue approval; `Tools/ViVeTool.exe` never modified; human moves issues to
`done`. The `optimizer-safety` agent has concurrency 1 and veto power via
the `optimizer-safety-review` skill.
