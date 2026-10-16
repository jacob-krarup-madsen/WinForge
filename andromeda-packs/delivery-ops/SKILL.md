---
name: delivery-ops
description: Delivery and live-ops pack: autonomous delivery contract with sibling-run awareness and branch discipline, mandatory independent audit with evidence rule, QA/release train and certification checklists, crash reporting and staged patch train, localization LQA gates, store copy under IP gate, Steam submission pipeline. Use for delivery process, audits, QA, release, telemetry, localization, or publishing work.
---

---

# From skill: delivery-protocol

# Delivery Protocol



The autonomous delivery contract every agent follows so work ships without a

human in the loop. This workspace runs fully parallel: several agents deliver

concurrently on different files and open separate pull requests. Your job is

never to wait for a person ΓÇö it is to deliver, verify, request independent

audit, and land.



**Two laws govern this protocol:** work is accepted only after an INDEPENDENT

agent audits it (see the `independent-audit` skill ΓÇö you never audit your own

work), and no one guesses: every claim carries a command + exit code, and what

cannot be verified is reported as UNKNOWN.



## Before you write code



1. **Know who else is working.** Run `multica issue runs <issue-id> --siblings

   --output json` before you touch the repo. If a sibling agent is editing files

   you also need, coordinate through the issue comments ΓÇö do not open a second

   PR against the same code.

2. **Branch from a fresh main.** `git checkout main && git pull` then branch.

   Never stack on a stale base; rebase before you open the PR.



## QA targets (defined up front, for every task)



Before you implement, every task's issue must carry **QA targets**: specific,

measurable, falsifiable acceptance criteria ΓÇö one per thing that must be true,

across all types (code, audio, graphics/UI, gameplay, config). The auditor scores

these into a HIT/MISS/PARTIAL scorecard, and `done` is only reachable when every

target is HIT.



- If the issue's **Done when** already states measurable criteria, adopt them as

  the targets.

- If it is vague ("works correctly", "feels good", "all tests pass" with no

  thresholds), do NOT guess intent. Write the target list yourself first ΓÇö one

  numbered, typed, measurable, verifiable row per acceptance criterion ΓÇö post it

  as a comment, then implement against it. Naming the targets is part of the work

  and never waits for a person.



A valid target is: **typed** (code/audio/graphics/gameplay/ui/config),

**measurable** (a threshold, count, name, or exact condition), **verifiable**

(names the command/scenario or the screenshot criterion), and **scoped** (names

the exact file, cue, scene, or behaviour). "Telegraph visible ΓëÑ0.4s before

damage, seed 4242" is a target; "telegraphs are readable" is not.



## Delivery loop (do all of it, every task)



1. Confirm the QA targets (above), then implement. Run the gates locally and

   record exit codes: `godot --headless --import`, the check scripts, and the GUT

   suite. A red gate is a blocker you fix ΓÇö never a reason to stop and ask.

2. Commit, push the branch, and open a PR whose **title carries the issue key**

   (`ANDR-17: ...`). Without the key the PR will not link to the issue ΓÇö and

   without the key the required `audit-gate` check cannot resolve the issue, so

   the PR stays red.

   Do NOT merge yet, and do NOT arm auto-merge ΓÇö merging is the auditor's call.

   Auto-merge may be armed only AFTER the auditor's PASS verdict; `main`

   protection enforces this mechanically (`audit-gate` stays red until a

   `Verdict: PASS` that references your PR exists on the linked issue), so

   arming early only parks the PR.

3. Post your evidence comment on the issue: PR URL, commands run, exact exit

   codes, files changed (`path:line`). Attach your PROOF ΓÇö the artifact type

   follows the work's type (see the proof table below): screenshots for

   visuals, audio recordings for cues, a playtest recording or checkpoint set

   for gameplay, gate-transcript logs for code, measurement tables for perf,

   live re-read output for config. Attach files with `--attachment <path>` on

   the comment; a filename mentioned in prose with no attachment is not proof.

   Then request your independent audit with

   a real mention link to your designated auditor (per the `independent-audit`

   matrix): `[@Name](mention://agent/<uuid>) ΓÇö audit requested`.

4. Wait for the verdict. If the auditor PASSes, they merge and set the issue

   `done` ΓÇö you are finished. If they FAIL, fix, push to the same branch, and

   reply with the new evidence; the verdict loop repeats.

5. If CI fails while you wait, fix it yourself ΓÇö the auditor will re-run it

   anyway, but do not leave a red PR sitting.



## Proof of resolution (every task, every type ΓÇö three stages)



Every delivered task that fixes a bug, changes UI, adds a visual feature, or

delivers a test report attaches THREE-STAGE proof to its evidence comment:



1. **Raw artifact** ΓÇö unmodified capture: raw screenshot, original render, test

   log, or capture clip. No overlays.

2. **Annotated artifact** ΓÇö the SAME capture with arrows, highlights, bounding

   boxes, or overlaid labels pinpointing what changed / what was fixed / where

   the bug was. Pair filenames (`title-raw.png` / `title-annotated.png`).

3. **Execution context** ΓÇö text on the same comment: reproduction step /

   trigger state, expected vs. actual behavior, and the exact environment

   parameters or scene-tree configuration tested (Godot version, seed, scene

   path, platform, capture mode).



Proof form by type:



| Type | Proof you attach |

|---|---|

| graphics / UI | Raw + annotated screenshots of each touched visual from an off-screen/headless run |

| audio | Raw audio recording (`.wav`/`.ogg`) of each cue + annotated waveform/timeline image marking the cue |

| gameplay | Raw playtest recording + annotated frame marking the behaviour + seed in the context |

| code (non-visual) | Raw gate-transcript log + annotated excerpt highlighting the proof lines |

| performance / balance | Raw measurement output + annotated table (threshold vs measured, misses marked) |

| config / CI | Raw live re-read output + annotated screenshot of the changed setting boxed |



Several types in one change ΓåÆ all matching kinds, all three stages. Proof must

come from the final push, not an earlier iteration. The auditor re-captures

their own proof on the verdict; yours supports the audit, theirs seals it.

An issue missing any stage of its type-appropriate proof cannot reach `done`.



## QA handoff PDF (every delivery-significant update)

Your evidence comment and your final delivery comment each attach ONE
self-contained PDF for an external QA team that has no Multica or GitHub
access: `ANDR-NN-qa-brief.pdf`, generated with the repo tool
(stdlib-only, no installs):

    python tools/qa_pdf.py brief.md ANDR-NN-qa-brief.pdf --title "QA Handoff Brief - ANDR-NN <short title>"

Mandatory contents (all six, in one file):

1. stable game context (engine/stack, relevant systems, headless verify
   command, environment/seed facts) -- reuse the studio preamble
2. this issue's change (key, title, what/why, files, what did NOT change)
3. QA targets scorecard table (target, result, evidence per row)
4. external QA repro script (numbered steps, expected vs. actual)
5. proof of resolution (issue, PR, CI runs, three-stage attachments)
6. links (repo, issue, design docs)

The brief must EMBED the issue's full evidence, not merely reference it:
screenshots and captures render inline via `![alt](path/to/file.png)`
(the tool embeds PNG and JPEG), scorecards and lens tables as text, and
video key-frames exported as PNG stills; the video file itself stays as an
issue attachment and is referenced by filename. A brief that links to
evidence without embedding it is non-conforming.

A missing PDF, or one that is not issue-specific, blocks the independent
audit verdict exactly like a missing proof stage. The auditor regenerates
their own copy on the verdict; yours supports the audit, theirs seals it.

## Visual self-check (author side of the audit lenses)

If your diff touches graphics, UI, HUD, VFX, sprites, portraits, icons, or
your evidence contains any screenshot/video with UI on screen, you self-score
BOTH audit lenses before requesting the audit ΓÇö the auditor will re-score them
independently, and a lens FAIL blocks PASS like a missed target.

**Policy source: ANDR-169** ΓÇö "Visual lenses protocol source-of-truth +
forward-scoped activation" (`01a12c52-d86a-7bef-b3e3-e8f4c337bdd4`) is the
single source of truth for both lenses and this self-check. Audit enforcement
is forward-scoped from ANDR-169's `done` timestamp: from that point, a
visual/UI diff whose independent-audit verdict lacks a `## Visual lenses`
section (Lens A + Lens B) FAILs the audit; verdicts predating it are
advisory-only and are never mass-demoted. Until ANDR-169 is `done` the lenses
are policy, not enforced. Score both lenses on every visual diff regardless ΓÇö
the auditor re-scores them independently.

**Lens A ΓÇö HUD/UI readability & usability** (judge at native resolution,
player's view, not an inspector zoom):

1. legibility ΓÇö no clipped/truncated/overlapping text; note smallest text px
2. contrast ΓÇö text ~4.5:1, icons ~3:1 vs background (sample the colors)
3. hierarchy ΓÇö combat-critical info (health/ammo/cooldowns/telegraphs)
   glanceable; decoration not competing equally
4. completeness ΓÇö no TODO/lorem/raw-key placeholders, no missing icons
5. layout integrity ΓÇö no collisions/out-of-safe-area at tested aspect
   ratio(s); state which you tested (16:9 minimum)

**Lens B ΓÇö fidelity-to-intent** (each new/changed visual must depict what it
is meant to depict):

1. recognition ΓÇö would an observer name it correctly without the issue text?
2. gameplay-scale readability ΓÇö reads at in-game size; not color-only
   differentiation (shape/texture backup for colorblind safety)
3. consistency ΓÇö matches established siblings (cite the reference asset)
4. no misrepresentation ΓÇö the visual never contradicts the gameplay it serves
   (fire look on ice damage, celebratory pose on a wind-up = wrong)

Put the results in your evidence comment as a compact table (item, result,
one-line observation with sizes/colors). Annotate your raw screenshot with
callouts on the text sizes and contrast pairs you claim. Empty PASS cells are
invalid ΓÇö every PASS carries its observation. The QA handoff PDF repeats the
lens results for external QA.

## Headless/off-screen execution (mandatory)



Tests, benchmarks, screenshot pipelines, and QA runs execute silently: Godot

`--headless` for script/test runners; visual captures rendered off-screen

(sub-viewports saved to PNG/WebM, or a virtual framebuffer on Linux). NEVER

open a visible window on the host desktop during automated testing or capture ΓÇö

stealing OS focus or interrupting the operator's workflows is a defect, even

when the test itself passes. State the capture mode in the execution context.



## Git & GitHub standards (elite workflow, no exceptions)



- **Strict PR-only:** no direct pushes to `main` or any trunk. All work ΓÇö

  including automated agent work ΓÇö flows through purpose-driven branches and a

  Pull Request. Branch names: `feat/<slug>`, `fix/<slug>`, `chore/<slug>`,

  `test/<slug>`, `ci/<slug>`, `docs/<slug>`.

- **Conventional Commits:** every commit message strictly

  `type(scope): summary` ΓÇö types `feat`, `fix`, `refactor`, `docs`, `test`,

  `ci`, `chore`. The body explains *why*, not what the diff already shows.

- **Branch & tag hygiene:** semantic version tags `vX.Y.Z` on releases,

  structured GitHub Releases with automated changelogs, no long-lived junk

  branches (delete on merge is enabled repo-wide).

- **Repository footprint:** first-class README, CONTRIBUTING, issue/PR

  templates under `.github/`, and CODEOWNERS are maintained ΓÇö treat changes to

  them like any other reviewed deliverable.



## Rules



- Never waive a red test gate. A waiver is a recorded decision naming evidence,

  risk, and follow-up ΓÇö not a silent skip.

- Never set your own issue to `done` unless you are that issue's designated

  auditor under the matrix. Authors deliver; auditors accept.

- Never end a run at "in_review" with uncommitted work or an unopened PR. If you

  cannot finish, say exactly what is missing on the issue.

- Never stall silently. If blocked, state the blocker on the issue and take the

  next useful action.

- Never guess. Read live state (`gh pr view`, `git log`, `multica issue get`)

  before stating it; mark the unverifiable as UNKNOWN.

- Never accept a vague Done-when. If a task arrives without measurable QA

  targets, write them before implementing ΓÇö targets are the acceptance

  contract, and the auditor scores them mechanically (all HIT to reach done).

- Status is yours to write, whenever the state changes ΓÇö mid-turn included.

- Prefer reversible operations; rely on git history before destructive work.

- Never write secrets, tokens, or key material into comments, logs, or output.

---

# From skill: independent-audit

# Independent Audit



The workspace rule: **no agent verifies its own work.** Every deliverable is audited by one other, independent agent before it counts as accepted. There are no exceptions and no assumed passes. Closely coupled with this: **no guessing, no assuming** ΓÇö every claim carries evidence you produced yourself.



## The two laws



1. **Independence.** You never audit work you authored ΓÇö not partially, not "just a quick check". The audit must be performed by a different agent than the one who did the work. If you authored it, you request the audit; you never perform it.

2. **Evidence, not claims.** You never audit by reading the author's summary of what they did. You re-run it. The author's comment tells you what to check; your own command output tells you whether it passed.



## No guessing, no assuming (applies to every run, audit or not)



- Every factual claim you make carries its evidence: command + exit code, or `path/to/file.gd:42`, or a PR URL with its check state. "Should work", "presumably", "looks correct" are not evidence.

- Read live state before stating it. Never report board, PR, or repo state from memory or from an earlier run ΓÇö query it (`multica issue get`, `gh pr view`, `git log`).

- When something cannot be verified, say exactly that: `UNKNOWN: <what and why>`. An unknown is an honest result; an assumption is a defect.

- If you must act on an assumption, name it as an assumption in the same sentence, with the risk it carries.



## The audit matrix



Your designated independent auditor for deliverables you author:



| Author | Independent auditor |

|---|---|

| Gameplay Engineer | QA & Release Lead |

| Engine & Tools Engineer | Technical Director |

| Technical Artist | QA & Release Lead |

| Audio Designer | QA & Release Lead |

| Game Designer | Technical Director |

| Narrative Designer | Localization Lead |

| Network Engineer | Engine & Tools Engineer |

| QA & Release Lead | Technical Director |

| Technical Director | QA & Release Lead |

| Studio Producer | QA & Release Lead |

| Technical Animator | Technical Artist |

| Publishing & Storefront Engineer | QA & Release Lead |

| Marketing & Community | Studio Producer |

| Localization Lead | QA & Release Lead |

| Mika | *(chat-only - never authors deliverables, no audits needed)* |



Fallback chain if the designated auditor is unavailable or IS the author: QA & Release Lead ΓåÆ Technical Director ΓåÆ Studio Producer. The author is NEVER a valid fallback for their own work.



## Domain auditors (GitHub config, Godot config, Godot language)



Some changes need domain competence in the auditor, not just independence. When a deliverable touches one of these three domains, the AUDIT MUST be performed by an agent who holds the relevant skill AND is not the author: prefer your matrix auditor if they hold the domain skill; otherwise take the domain list in order, skipping the author and anyone unavailable.



**1. GitHub config** ΓÇö `.github/` workflows, branch protection, repo settings, CI wiring.

- Domain auditors (hold `godot-headless-ci`): Engine & Tools Engineer ΓåÆ Network Engineer ΓåÆ Technical Director. If E&T authored: QA & Release Lead ΓåÆ TD.

- Verdict MUST include: live re-read of the config (`gh api repos/.../branches/main/protection`, `gh workflow list`, NOT memory or the author's paste), YAML/workflow validity (`gh workflow view` or a triggered run), and confirmation that required status checks match the check names CI actually reports (`gh pr checks` / `gh run view --json jobs`).



**2. Godot config** ΓÇö `project.godot`, `export_presets.cfg`, `.import` files, autoloads, input map, layer/tag setup, import presets.

- Domain auditors: project/export/CI side ΓåÆ Engine & Tools Engineer (then TD, Network Engineer); import pipeline / textures / materials side ΓåÆ Technical Artist (then Audio Designer, who holds `art-asset-pipeline`). Skip the author.

- Verdict MUST include: `godot --headless --import` ΓåÆ exit 0 recorded by the auditor, `--check-only` where applicable, a key-by-key diff review of the changed config (name every changed setting, not "looks fine"), and the GUT suite exit code.



**3. Godot language (GDScript)** ΓÇö any `.gd` change.

- Domain auditors (hold `godot-gameplay` / `godot-scene-tooling`): Technical Director ΓåÆ Gameplay Engineer ΓåÆ Engine & Tools Engineer. If TD authored: GE ΓåÆ E&T.

- Verdict MUST include: `godot --headless --check-only` (or the repo's equivalent static check) on every touched script with exit codes, the GUT suite exit code, and a diff review that names concrete findings (signal wiring, state transitions, `_physics_process`/`_process` misuse, node-path hardcoding) ΓÇö an empty findings list must be the result of reading the diff, not of skipping it.



Domain audit composes with the matrix: exactly one auditor performs the acceptance, and that auditor must be both independent and domain-competent. Both properties are checkable from this skill ΓÇö verify, never assume.



## QA targets ΓÇö defined up front, scored at audit



Every issue carries **QA targets**: specific, measurable, falsifiable acceptance criteria written when the issue is defined ΓÇö one target per thing that must be true, across ALL types (code, audio, graphics/UI, gameplay, config/CI). Targets turn "is it done?" from a judgment call into a scorecard.



### When targets are written



- The issue author (squad leader, TD, or whoever scopes the issue) writes targets into the issue's **Done when** as a numbered list at creation time.

- If you receive an issue whose Done-when is vague ("works correctly", "feels good", "all tests pass" with no named thresholds), do NOT guess the intent: write the target list yourself first as a comment ΓÇö derived from the issue's scope, one measurable row per acceptance criterion ΓÇö then implement against it. Naming the targets IS part of the work; it never waits for a human.



### What makes a target valid



Each target must be falsifiable without interpretation:



1. **Typed** ΓÇö `code`, `audio`, `graphics`, `gameplay`, `ui`, or `config`.

2. **Measurable** ΓÇö contains a threshold, count, name, or exact condition. "Telegraph visible ΓëÑ0.4s before damage, seed 4242" is a target; "telegraphs are readable" is not.

3. **Verifiable** ΓÇö names HOW it is checked: a command/scenario (for code/gameplay/audio/config) or a scenario+screenshot with a stated criterion (for graphics/ui).

4. **Scoped** ΓÇö names the exact artifact (file, cue, scene, behavior) it applies to.



### Scorecard ΓÇö required section in every audit verdict



The auditor scores EVERY target. All targets appear, hit or not; the scorecard is the done-decision:



```

## QA targets scorecard

| # | type | target (measurable) | verification | result | evidence |

|---|---|---|---|---|---|

| 1 | gameplay | boss phase-2 telegraph ΓëÑ0.4s before damage, seed 4242 | TestBossTelegraph scenario | HIT | telegraph 0.52s, t=00:37 |

| 2 | audio | boss stinger plays exactly once on phase transition | playback assertion in TestBossAudio | MISS | 0 playback events |

| 3 | ui | settings screen min font 18px at 720p | screenshot + measurement | PARTIAL | 16px on Audio tab only |

```



Result values: `HIT`, `MISS`, `PARTIAL`, `UNKNOWN` (with reason). The auditor scores by running the verification themselves ΓÇö same evidence laws as the QA coverage block; the QA coverage rows are HOW each verification ran, the scorecard is WHAT each target scored.



### Done mapping (mechanical, no interpretation)



- **All targets HIT** ΓåÆ verdict may be PASS ΓåÆ auditor merges and sets `done`.

- **Any MISS or PARTIAL** ΓåÆ verdict is FAIL; PARTIAL is not done. The FAIL lists exactly which targets missed and what result is required to flip them; the author fixes only those.

- **Any UNKNOWN on a required target** ΓåÆ verdict cannot be PASS.

- The watchdog's unaudited-done sweep also checks for a scorecard: a `done` issue with no all-HIT scorecard in its audit thread is reopened automatically.



A deliverable with no targets cannot be accepted at all ΓÇö an issue without measurable targets must have targets written before its first audit can PASS.



## QA coverage ΓÇö required evidence format



EVERY deliverable is QA'd: each code change, each audio asset, each graphic, each gameplay behaviour. Every audit verdict MUST contain a `## QA coverage` block in exactly the format below. The format is the enforcement: a missing class, a missing row, or a missing field is visible at a glance and cannot hide an assumption.



### Canonical block (all five classes always listed)



```

## QA coverage

All commands below were executed by the auditor in this audit. Author-copied values are invalid.



### code

| path | check | command | exit | output-excerpt |

|---|---|---|---|---|

| scripts/player.gd | static | `godot --headless --check-only scripts/player.gd` | 0 | clean |

| (suite) | unit | `godot --headless -s addons/gut/gut_cmdln.gd -gexit` | 0 | 92/92 pass |



### audio

| asset/cue | check | command | exit | output-excerpt |

|---|---|---|---|---|

| sfx/boss_telegraph.wav | import | `godot --headless --import` | 0 | no errors |

| cue:boss_telegraph | playback | `gut: assert playback state == playing` (scenario TestBossTelegraph) | 0 | playing at t=12.4s |



### graphics-ui

| asset/visual | check | command | exit | evidence-file |

|---|---|---|---|---|

| ui/title.png | import | `godot --headless --import` | 0 | ΓÇö |

| scene:TitleScreen | visual | scenario run + screenshot | 0 | audit-title-screen.png (attached) |



### gameplay

| behavior | seed/scenario | command | exit | outcome |

|---|---|---|---|---|

| boss phase 2 transition | seed=4242, TestBossFullRun | `godot --headless -s addons/gut/gut_cmdln.gd -gtest=res://tests/test_boss.gd -gexit` | 0 | phase-2 reached t=00:37, telegraph fired |



### config-ci

| item | check | command | exit | observed |

|---|---|---|---|---|

| branch protection | live read | `gh api repos/.../branches/main/protection` | 0 | contexts=["build"], strict=true |

```



### Format rules (violations block PASS)



1. **All five class headings always appear.** A class with no diff changes writes `none in diff (verified via gh pr diff <n>)` ΓÇö absence is a checked claim, not silence.

2. **One row per artifact.** No blanket rows: per file, per cue, per visual, per behavior. "all audio" / "tests" / "the scenes" are not artifact names.

3. **`exit` is an integer from a command YOU ran in THIS audit.** Copying the author's exit codes or summaries is fabrication and is an automatic FAIL if discovered.

4. **Commands are copy-paste runnable.** A paraphrase ("ran the tests", "checked imports") is an invalid row ΓÇö write the exact command.

5. **Missing row for a diff-touched artifact = verdict cannot be PASS** (UNKNOWN or FAIL, never assumed).

6. **`UNKNOWN` is only valid with a reason** in the last column, and any UNKNOWN row for a required artifact blocks PASS.

7. **Nonzero `exit`** must be investigated and annotated, or the verdict is FAIL.

8. **`evidence-file` values must be real attachments** on the verdict comment (screenshots via `--attachment`), referenced by exact filename.



QA & Release Lead is the standing QA auditor for code, art, and audio deliverables under the matrix (their own work is audited by Technical Director), and holds `godot-testing` + `qa-release` for exactly this. When in doubt about whether an artifact class was touched: read the diff ΓÇö `gh pr diff <n>` is the authority, not the PR description.



## Visual lenses ΓÇö HUD/UI readability + art fidelity (mandatory for visual audits)

Any change whose diff touches graphics, UI, HUD, VFX, sprites, portraits, icons, or any screenshot/video evidence containing them MUST carry a `## Visual lenses` section in the verdict, scoring BOTH lenses below. Green code tests never compensate for a lens FAIL: a graphics-ui FAIL blocks PASS the same way a missed target does.

**Policy source: ANDR-169** ΓÇö "Visual lenses protocol source-of-truth + forward-scoped activation" (`01a12c52-d86a-7bef-b3e3-e8f4c337bdd4`) is the single source of truth for this section, both lenses, and their scoring. Enforcement is **forward-scoped from ANDR-169's `done` timestamp**: an audit verdict on a visual/UI diff whose evidence and verdict comments are dated after that timestamp, and whose diff or evidence contains UI/HUD/screenshots/video, but which lacks a `## Visual lenses` section (Lens A + Lens B), is a violation ΓÇö revert to `in_review` and dispatch the auditor. Done issues predating ANDR-169 are NEVER mass-demoted for a missing section ΓÇö advisory line only. Until ANDR-169 is `done`, the section is required policy but is not enforced.

### Lens A ΓÇö HUD/UI readability & usability (every capture containing HUD/UI)

Judge the capture at its NATIVE resolution, as a player would see it, not zoomed into an inspector. Score each item PASS/FAIL with a one-line observation; cite pixel sizes, sampled colors, or aspect ratios where stated.

1. **Legibility** ΓÇö every string renders at its intended size: no clipping, no truncation/ellipsis on critical labels, no overlapping text layers. State the smallest text class in px at the capture's resolution (e.g. "body HUD 14px @1080p").
2. **Contrast** ΓÇö text and critical icons vs their background: approx. 4.5:1 luminance for text, 3:1 for large text/icons (WCAG-style). Sample and state the two colors, or mark UNKNOWN with a reason.
3. **Hierarchy & glanceability** ΓÇö mid-combat information (health, ammo, cooldowns, telegraphs, boss tells) is findable in a glance; decorative elements do not visually compete equally with critical state. State what the eye lands on first in the capture.
4. **Completeness** ΓÇö no placeholder/debug strings ("TODO", "lorem", raw keys, `res://` paths), no missing icons, no unlabeled controls, no empty state that should show content.
5. **Layout integrity** ΓÇö no HUD element collides with another or leaves the safe area at the tested aspect ratio(s). State which were tested (16:9 minimum; others if in scope).

Any FAIL in items 1ΓÇô4 = Lens A FAIL. Item 5 alone is waivable only when the issue explicitly scoped a single aspect ratio.

### Lens B ΓÇö fidelity-to-intent (every new/changed visual asset or effect)

The visual must actually depict what it is meant to depict. For each new or changed asset/effect, score PASS/FAIL with a one-line observation:

1. **Recognition** ΓÇö an observer shown the capture WITHOUT the issue text can name what it depicts. Auditor states: "I see X; the design/issue says it is Y" ΓÇö mismatch = FAIL.
2. **Gameplay-scale readability** ΓÇö the asset reads correctly at its in-game size, not only zoomed in. Silhouettes/icons distinguishable at a glance from siblings. If color is the only differentiator and the palette is not colorblind-safe (no shape/texture/pattern backup), = FAIL.
3. **Consistency with the visual language** ΓÇö palette, outline weight, VFX intensity match established siblings; cite the sibling used as reference ("matches elite nova ring style of enemy_nova.tres").
4. **No misrepresentation** ΓÇö the visual does not contradict the gameplay it serves (a fire-looking effect on ice damage, a celebratory pose on a wind-up, a "shield" icon on a damage buff = FAIL).

Any FAIL = Lens B FAIL. Assets the issue did not touch are noted but not scored.

### Verdict template addition (insert after the QA coverage block, before Proof of resolution)

```
## Visual lenses

### Lens A ΓÇö HUD/UI readability & usability
| # | item | result | observation (sizes/colors/ratios) |
|---|---|---|---|
| 1 | legibility | PASS/FAIL | |
| 2 | contrast | PASS/FAIL | |
| 3 | hierarchy | PASS/FAIL | |
| 4 | completeness | PASS/FAIL | |
| 5 | layout integrity | PASS/FAIL | |

### Lens B ΓÇö fidelity-to-intent
| asset/effect | intent (from design/issue) | I actually see | result |
|---|---|---|---|
| | | | |

**Visual verdict: PASS|FAIL** (A and B both PASS, or the overall audit cannot be PASS)
```

### Format rules (violations block PASS)

- A visual/UI diff with no `## Visual lenses` section in the verdict = FAIL (forward-scoped per ANDR-169 ΓÇö see Policy source above).
- Lens rows without per-row observations = FAIL (blank PASS cells are invalid).
- The auditor judges from THEIR OWN capture re-run or the attached evidence files ΓÇö never from the author's prose claim that it "looks good".

## Proof of resolution ΓÇö three-stage proof, attached artifacts



Every issue that fixes a bug, changes UI, adds a visual feature, or delivers a test report must carry THREE-STAGE proof of the solution. A sentence describing what you saw is not proof; a screenshot with no annotation is half-proof.



### The three stages (all required, per artifact)



1. **Raw artifact** ΓÇö an unmodified capture: raw screenshot (`.png`), original render, test log (`.log`/`.txt`), or capture clip (`.mp4`/`.webm`/frame sequence). No overlays, no edits ΓÇö the untouched evidence.

2. **Annotated artifact** ΓÇö the SAME capture with explicit annotations: arrows, highlights, bounding boxes, or overlaid text labels pinpointing exactly what changed, what was fixed, or where the bug occurred. The annotation must make the delta obvious to someone who never read the issue. Filename must clearly pair with the raw one (e.g. `title-raw.png` / `title-annotated.png`).

3. **Execution context** ΓÇö accompanying text on the same comment detailing: (a) reproduction step / trigger state, (b) expected vs. actual behavior, (c) the specific environment parameters or scene-tree configuration tested (Godot version, seed, scene path, platform, window/rendering settings).



### Required proof per type



| Type of change | Required proof (attached to the comment) |

|---|---|

| graphics / UI | Raw + annotated screenshots of every touched visual, captured from an off-screen/headless run at the named checkpoints, plus execution context |

| audio | Raw audio recording (`.wav`/`.ogg`) of every touched cue rendered during verification + annotated context (waveform screenshot with the cue region marked, or an annotated timeline image) + execution context |

| gameplay | Raw playtest recording (`.mp4`/frame sequence) + annotated version (frame with the behaviour marked) + execution context including the seed ΓÇö where recording is genuinely impossible, checkpoint screenshots + outcome JSON, limitation stated as `UNKNOWN: <why>` |

| code (non-visual) | Raw gate-transcript log (commands, exit codes, full output) + annotated log excerpt (highlighted lines proving the fix/behaviour) + execution context |

| performance / balance | Raw measurement output + annotated measurements table (threshold vs measured, per case, misses marked) + execution context |

| config / CI | Raw live re-read output (the actual `gh api` result / config dump) + annotated screenshot of the changed GitHub UI with the changed setting boxed + execution context |



One diff touching several types requires ALL matching proof kinds. Proof must show the state AFTER the final fix push ΓÇö artifacts from an earlier iteration do not count.



### Headless/off-screen execution rule (mandatory)



Tests, benchmarks, screenshot pipelines, and QA runs MUST execute silently: Godot `--headless` for script/test runners, and for visual captures render off-screen (sub-viewports saved to PNG/WebM, or a virtual framebuffer on Linux). NEVER open a visible window on the host desktop during automated testing or capture ΓÇö stealing OS focus or interrupting the operator's workflows is a defect, even if the test itself passes. The execution context must state the capture mode used (e.g. `--headless`, subviewport-render).



### Format rules (violations block PASS)



1. Proof is listed in a `## Proof of resolution` section on the accepting comment, one row per artifact: filename ΓåÆ stage (raw/annotated) ΓåÆ what it proves ΓåÆ how it was captured.

2. Files are attached to that very comment (`multica issue comment add <id> --content-file ... --attachment <path>`, or `multica attachment upload <path>`). A filename mentioned in prose with no attachment is NOT proof.

3. The auditor attaches THEIR OWN captured proof on the verdict comment. Author-provided artifacts support the audit but never substitute for the auditor's.

4. **Missing any stage for a touched type** (raw without annotated, or artifacts without execution context) = verdict cannot be PASS ΓÇö the same law as a missing QA-coverage row.

5. The watchdog's done-sweep reopens any `done` issue whose verdict lacks a `## Proof of resolution` section or whose referenced artifacts are not attachments on the thread.

6. A missing or non-conforming **QA handoff PDF** (`ANDR-*-qa-brief.pdf`) on the evidence thread or the verdict comment blocks PASS ΓÇö same weight as a missing proof stage. See "QA handoff PDF" below.



## QA handoff PDF ΓÇö external QA package (blocks PASS when missing)

Every delivery-significant update (author evidence comment, auditor verdict comment) attaches ONE self-contained PDF an external QA team can act on without Multica/GitHub access. Generate it with the repo's stdlib-only converter:

`python tools/qa_pdf.py brief.md ANDR-NN-qa-brief.pdf --title "QA Handoff Brief ΓÇö ANDR-NN <short title>"`

The auditor VERIFIES all six mandatory sections are present in the attached PDF (not merely claimed in prose):

1. stable game context (engine/stack, systems, headless verify command, env/seed facts)
2. this issue's change (key, title, what/why, files, what did not change)
3. QA targets scorecard table (target, result, evidence per row)
4. external QA repro script (numbered steps, expected vs actual)
5. proof of resolution (issue, PR, CI runs, three-stage attachments)
6. links (repo, issue, design docs)

A filename mentioned with no attachment, a PDF missing sections, a brief that is not issue-specific, or a brief that merely LINKS to screenshots/captures instead of EMBEDDING them (every screenshot on the issue must appear as an inline image inside the PDF itself - open the PDF and count the embedded images against the issue's image attachments; video key-frames must be embedded as PNG stills, the video file referenced by filename) = FAIL, exactly like a missing raw/annotated stage. The auditor attaches their own freshly generated PDF on the verdict comment; the author's copy supports but does not substitute for it.



## Performing an audit (for the auditor)



1. **Locate the deliverable.** Read the issue and thread for the author's evidence claim (PR URL, paths, test names, commands). Then query the real state yourself: `multica issue pull-requests <id>`, `gh pr view <n> --json state,mergeCommit,statusCheckRollup`, `git log --oneline -5`.

2. **Re-run the gates yourself.** In your own workdir: check out the PR branch (`gh pr checkout <n>` after `multica repo checkout`), run the same gates the author claims ΓÇö import check, check scripts, GUT suite, anything the issue's Done-when lists. Record your own commands and exit codes. If your exit codes differ from the author's claim, that is a FAIL, regardless of what the comment says.

3. **Inspect the diff, not just the tests.** `gh pr diff <n>` ΓÇö does the change do what the issue asked, and nothing it didn't? Flag scope creep and severity changes.

4. **Verify the claim set.** Check each Done-when condition against your own observations. Mark each: VERIFIED / FAIL / UNKNOWN (with reason).

5. **Verdict comment** on the issue, in this shape (the QA coverage block is mandatory and uses the canonical format from the QA coverage section ΓÇö all five classes, one row per artifact, auditor-run commands only):



```

## Independent audit ΓÇö PR #<n> (<deliverable>)

- Auditor: <your name> (author: <author name>)

- Diff review: <findings or "no scope issues">



## QA targets scorecard

| # | type | target (measurable) | verification | result | evidence |

|---|---|---|---|---|---|

[one row per target from the issue's Done-when; result = HIT/MISS/PARTIAL/UNKNOWN(reason)]



## QA coverage

[canonical block ΓÇö code / audio / graphics-ui / gameplay / config-ci,

 all five headings present, one row per artifact, exit codes from your own runs]



## Proof of resolution

| artifact (attached) | stage | proves | captured via |

|---|---|---|---|

| boss-phase2-raw.mp4 | raw | gameplay targets #1ΓÇô#3 | headless playtest seed 4242, off-screen capture, attached |

| boss-phase2-annotated.mp4 | annotated | telegraph window boxed at t=00:37 | same capture, arrows + labels, attached |

| context: repro seed 4242, TestBossFullRun, Godot 4.x --headless, expected telegraph ΓëÑ0.4s before damage vs actual 0.52s | context | execution context | ΓÇö |



**Verdict: PASS** (all targets HIT) or **Verdict: FAIL ΓÇö targets N/M missed: <which and what result flips them>**

```



6. **On PASS:** merge authority is yours, and it is now gated twice. First make

   the required `audit-gate` check green ΓÇö it stays red until it sees your

   verdict: `gh workflow run audit-gate -f pr=<n>` (or `gh run rerun --failed`

   if a run already exists), then `gh pr checks <n>` until `audit-gate` is

   green. Then merge: `gh pr merge <n> --squash --delete-branch`, confirm the

   merge SHA, and set the issue to `done` with a one-line comment naming the

   SHA. Your verdict heading MUST carry `PR #<n>` ΓÇö the CI gate binds a verdict

   to a pull request by that reference, and a verdict without it leaves every

   PR on the issue blocked.

7. **On FAIL:** comment the verdict with your evidence, set the issue to `blocked` ONLY if it truly cannot proceed, otherwise leave it `in_progress`, and the author (or the watchdog) dispatches the fix. Never waive a FAIL.



## For authors: requesting the audit



In your delivery loop, after opening the PR (and before you consider the work delivered):



- Post your evidence comment on the issue (PR URL, commands, exit codes), then mention your designated auditor with a real mention link so their run is enqueued: look up their UUID via `multica agent list --output json` and write `[@QA & Release Lead](mention://agent/<uuid>) ΓÇö audit requested`.

- Do not set the issue to `done` yourself unless you ARE the auditor for that issue under the matrix. Authors deliver; auditors accept.

- Do not enable merge before the audit verdict unless CI itself is what you are waiting on ΓÇö the auditor holds merge authority.



## For squad leaders



A member's delivery report is not acceptance. Before moving a parent to `in_review` on the strength of member reports, ensure each has an audit verdict comment from an agent other than the author, per the matrix. Dispatch the auditor yourself if the author did not.

---

# From skill: qa-release

# QA & Release (Godot 4)

Target: **Godot 4 stable**. This skill owns test strategy, regression selection, bug triage, certification checklists, and release discipline.

## Test strategy

Four layers, cheapest first. Each layer gates the next.

| Layer | What | Tool | When |
|---|---|---|---|
| 1. Unit | Pure logic, rules, data parsing | GUT / GdUnit4 headless | Every commit |
| 2. Scene/integration | Node behaviour, signals, scene wiring | GUT with fixtures | Every commit |
| 3. Scenario/playthrough | Input-replayed runs, screenshots | `godot-qa` | Every PR |
| 4. Evidence/exploratory | Feel, balance, platform quirks | Agent scenario runs, screenshot diffs, telemetry | Pre-milestone, pre-cert |

Scope statement per milestone: what is **in** (features), what is **out**, what is **riskiest** (rank by: new code Γö£├╣ player impact Γö£├╣ unreproducibility).

Test pyramid rules:
- Prefer a unit test over a scene test; a scene test over a scenario; a scenario over a manual check.
- Every bug fix gets a regression test at the lowest layer that can reproduce it. If only a scenario can reproduce it, add the scenario.
- Determinism is mandatory: no wall-clock sleeps, no random without a fixed seed, no screen-size-dependent assertions (see `godot-testing`).

## Regression selection

When a full run is too expensive, pick the smallest set that covers the change:

1. **Always run**: the layer-1 suite (fast) + any test file under a directory the diff touches.
2. **Path-based selection**:
   - `res://scenes/player/**` ╬ô├Ñ├å player unit + scene tests + `player_*.yaml` scenarios
   - `res://addons/**` or `res://tools/**` ╬ô├Ñ├å tooling tests only, plus one full smoke
   - `project.godot`, `*.import`, `*.tres` under `res://balancing/` ╬ô├Ñ├å load/economy tests + smoke
   - `export_presets.cfg`, CI files ╬ô├Ñ├å export job only
3. **Blast radius**: shared code (autoloads, base classes, resources used by many scenes) ╬ô├Ñ├å run everything.
4. **Nightly full**: entire suite + all scenarios + screenshot diff on clean import.
5. Record the selection rule in the PR so the reviewer can verify coverage, not just green.

If you cannot justify why a changed file's consumers are covered, run the full suite.

## Bug triage format

Every bug report uses this exact shape (post as an issue with the `bug` label):

```markdown
**Summary:** <one line, imperative>
**Build:** <version/commit> Γö¼Γòû <platform> Γö¼Γòû <renderer: Forward+|Mobile|Compatibility>
**Severity:** S1 blocker Γö¼Γòû S2 major Γö¼Γòû S3 minor Γö¼Γòû S4 cosmetic
**Frequency:** always / intermittent (<n>/<m>) / once
**Repro steps:**
1. ...
2. ...
**Expected:** <what should happen>
**Actual:** <what happens, with error text / exit code>
**Evidence:** screenshot, log excerpt, godot-qa scenario id, save file
**Suspected area:** <scene/script/data file path>
**Regression?** first bad commit / "not a regression"
```

Triage rules:
- Severity is **player impact**, not how hard it is to fix. S1 = blocks ship/build is unusable; S2 = feature broken with no workaround; S3 = workaround exists; S4 = polish.
- Repro steps must be runnable by someone else without context. "Sometimes crashes" is not a bug yet ╬ô├ç├╢ it needs a log or a scenario.
- One bug per issue. Split multi-part reports.
- Every S1/S2 gets an owner and a target milestone within one working day of triage.
- Duplicate ╬ô├Ñ├å `multica issue status <id> cancelled --duplicate-of <original>` so both directions are linked.

## Platform export & certification checklist

Run before every platform submission. Tick every item; a blank line is a blocker.

**Common (all platforms)**
- [ ] `godot --headless --import` clean on the release tag
- [ ] Full test suite green (unit + scene + scenario), JUnit XML archived
- [ ] No `SCRIPT ERROR` / `ERROR:` in captured logs of a headless smoke run
- [ ] Version, build number, and store metadata match across `project.godot` and `export_presets.cfg`
- [ ] Save/load round-trip on an existing save from the previous patch (no data loss)
- [ ] Third-party licences file present and accurate
- [ ] Debug symbols / symbols archive stored off-build

**Windows / macOS**
- [ ] Code-signed; timestamped; signature verified after export (`signtool verify /pa`, `spctl -a`)
- [ ] macOS: notarised, stapled, correct hardened-runtime entitlements, correct bundle id
- [ ] High-DPI + controller hot-plug tested; no console window in release export

**Linux**
- [ ] Executable bit set; runs on a clean container without editor deps
- [ ] `.x86_64` and, if required, `.arm64` builds both verified

**Android**
- [ ] Correct target SDK per store requirement; 64-bit `arm64-v8a` present
- [ ] Keystore not committed; release signed, debug signed only for QA
- [ ] Install ╬ô├Ñ├å upgrade-over-previous-release tested (data preserved)

**iOS**
- [ ] Provisioning profile + entitlements (permissions strings present in Info.plist)
- [ ] Privacy manifest (`PrivacyInfo.xcprivacy`) present as required
- [ ] App Store metadata, screenshots, age rating submitted

**Console / Web**
- [ ] Platform-specific cert checklist attached (each platform has its own doc)
- [ ] Web: threads/`COOP-COEP` headers configured, no audio autoplay block, load time within budget

**Store-side**
- [ ] Age rating questionnaire complete
- [ ] Achievements/leaderboards (if any) tested against sandbox
- [ ] Rollback plan documented: previous build retained, store rollback steps written

## Release train & patch notes

- **Cadence**: fixed train ╬ô├ç├╢ e.g. weekly patch on Tue, monthly content drop on the first Tue. A missed train waits for the next; it does not delay the line.
- **Branches**: `main` always releasable; `release/x.y` cut from `main` at train time and receives only fixes; feature branches merge to `main` only when their tests are green.
- **Freeze window**: no feature merges from T-24 h before cut. Only S1/S2 fixes with a regression test.
- **Tagging**: annotated tag `v1.2.3` on the exact commit exported; CI builds from the tag, never from a working tree.
- **Artifacts**: export both `--export-release` (ship) and `--export-debug` (QA reproduction), plus JUnit XML, logs, and the scenario baselines used for sign-off ╬ô├ç├╢ all archived with the tag.

**Patch notes discipline**
- Written as the PRs land, not at release time: each merge adds a line under `[Added] / [Changed] / [Fixed] / [Known issues]`.
- Player-facing language ╬ô├ç├╢ no internal ticket ids as the only text (link them as references).
- `[Known issues]` is mandatory when anything S2+ is open; silence is not an option.
- Breaking save/progress changes call out explicitly with a migration note.
- Final pass: producer agent reviews, QA verifies each `[Fixed]` line against a repro on the build, then the notes are locked with the tag.

**Sign-off** ╬ô├ç├╢ release is blocked until all four are recorded on the release issue:
1. QA lead: suite green + checklist complete
2. Technical Director: no open S1/S2, architecture/ perf budgets met
3. Producer: scope + patch notes approved
4. Owner on-call plan: who rolls back if the train derails (rollback = re-point store to previous build, within the platform's window)

## Escalation

- Test gate red and not obviously infra ╬ô├Ñ├å hold the train, post the failing log, assign the owning engineer.
- Flaky test (>2 fails in 5 runs with no code change) ╬ô├Ñ├å quarantine it with an issue linked, do not delete it, and do not let flakes pass silently.
- Cert risk discovered late ╬ô├Ñ├å S1 immediately, notify the producer, decide ship-with-known-issue vs hold (QA lead's recorded decision).

---

# From skill: live-ops-telemetry

# Skill: live-ops-telemetry

Keep the game observable after launch: crash reporting, opt-in telemetry, the patch train, and the player-feedback intake loop. Extends the release train (`qa-release`) rather than replacing it.

## Crash reporting

- **Self-hosted Sentry** is the default target (free tier / self-host; HUMAN creates the DSN account). Wire the Godot client via a small GDScript breadcrumb handler + `SentryStoreMinidump`-style native handler on desktop exports; document the integration in `docs/live-ops/crash-setup.md`.
- **Headless-safety:** crash handler must be compile-time optional (`OS.has_feature("editor")` / custom feature flag) so CI exports never phone home.
- Every crash event carries: build version, platform, scene path, last 20 log lines. **Never** PII, never auth material.

## Telemetry (opt-in only)

- Event schema in `docs/live-ops/telemetry-events.md`: stable event names (`run_started`, `run_ended`, `death_cause`, `shop_purchase`, `boss_encounter`), typed properties, and a `schema_version`.
- Consent: telemetry off by default; a settings toggle (data-driven, per `a11y-floor` settings patterns) gates all emission. No event fires without consent ΓÇö enforce in one chokepoint autoload.
- Headless test: a GUT test asserts zero events emitted with consent off, expected events with consent on (simulated).

## Patch train

- **Versioning:** semver-ish `MAJOR.MINOR.PATCH` aligned to GDD milestones; build number ties to CI run id.
- **Hotfix criteria:** S1 crash affecting >5% sessions, progression blockers, security issues ΓÇö ship within the freeze exception path of `qa-release`.
- **Rollout:** staged store rollout (10 ΓåÆ 50 ΓåÆ 100%) for non-hotfix patches; rollback plan = previous build kept on a store branch, documented before rollout starts.
- Every patch ships with player-facing notes assembled from PR titles (the release train already drafts these).

## Player feedback intake

- Sources: store reviews, community channels (post-clearance per marketing gate), playtest surveys. The agent **triages into issues** (one bug per issue, per `qa-release` format); it never posts publicly.
- Feedback batches become a weekly triage issue assigned to QA & Release Lead with severity proposals and counts.

## Privacy & compliance baseline

- Telemetry policy text draft for the store page (`docs/live-ops/privacy-draft.md`) ΓÇö what is collected, what is not, retention. Svinet's legal review is HUMAN.
- No cookie banners needed for a desktop premium title with opt-in telemetry; state that explicitly in the draft.

## Verification chain

1. GUT consent tests green (events off/on).
2. `godot --headless --import` + export clean with crash handler feature-flagged off.
3. Schema doc and events code agree ΓÇö a test enumerates emitted event names against the schema.

## Delivery comment format

- `Shipped` ΓÇö events/handlers/docs paths.
- `Consent proof` ΓÇö test names + exit codes.
- `HUMAN items` ΓÇö account/DSN/legal steps Svinet owns.
- `Patch train state` ΓÇö current version, rollout stage, rollback build ref.

---

# From skill: localization-lqa

# Skill: localization-lqa

Run localization as an industrial pipeline: source keys ΓåÆ machine draft ΓåÆ review flags ΓåÆ LQA ΓåÆ ship gate. Extends `narrative-design` (which owns key discipline and file formats); this skill owns volume, verification, and quality gates.

## Pipeline stages

1. **Source freeze.** Narrative Designer marks a loc milestone (`strings.csv` column `en` complete for a scope). Only then does translation start ΓÇö translating moving strings is waste.
2. **Machine draft.** Per-locale columns filled by translation (AI or human translators ΓÇö the agent runs the tooling; human review is scheduled by Svinet when quality bar demands). Every machine-produced cell is flagged `needs_review` in a parallel status column or via a ` REVIEW` suffix convention the team agrees ΓÇö never silently trusted.
3. **Review pass.** Glossary enforcement: game terms (ability names, faction names, item names) use the approved glossary per locale ΓÇö `docs/loc/glossary.csv`; deviations are corrections, not suggestions.
4. **LQA.** Structured pass per locale (checklist below), run headlessly where possible plus a human smoke test for the ship locales.
5. **Ship gate.** A locale ships only when: zero missing keys, zero untranslated `needs_review` cells, LQA checklist green. Partial locales fall back to `en` via TextServer fallback ΓÇö acceptable for early access, documented per release.

## LQA checklist (per locale)

- [ ] **Completeness:** every key in `en` exists in the locale column (script-enumerated count).
- [ ] **No placeholders lost:** `%s`, `{damage}`, `[img]` tokens survive translation (regex compare against source).
- [ ] **Expansion:** pseudo-loc (`en-XA`: accented, +30% length) run ΓÇö no truncated labels, no overlap in key UI scenes (`godot-qa` screenshot compare).
- [ ] **Fonts/glyphs:** the locale's script renders with the shipped font subset; missing-glyph boxes = blocker.
- [ ] **Bidi/RTL** (if applicable): layout mirrored, no embedded-LTR leaks in numbers.
- [ ] **Voice/subtitles:** if VO exists for the locale, subtitle timing keys align (per `narrative-design` conventions).

## Headless verification (every loc change)

```
godot --headless --import
godot --headless -s res://tools/loc_check.gd -- --locale all --strict
```

`loc_check.gd` (owned by Engine & Tools when tooling-shaped work is needed) asserts: key completeness per locale, placeholder integrity, glossary hits, and emits JUnit XML into `build/test/` like GUT does. Exit code is the gate ΓÇö never report a green loc run without the exit code.

## File & key discipline (inherited, restated)

- `res://locale/strings.csv` is the single source; stable lowercase dot-separated keys; renames update every locale column in the same change (per `narrative-design`).
- Never machine-translate and present as final: `needs_review` is a state, not a judgment call.

## Delivery comment format

- `Locales touched` ΓÇö column counts before/after, missing/extra deltas.
- `LQA results` ΓÇö checklist table per locale, with the headless run's exit code.
- `Ship gate` ΓÇö per-locale verdict: SHIP / FALLBACK-TO-EN / BLOCKED + reason.
- `Review queue` ΓÇö count of `needs_review` cells handed to human review, with the export file path.

---

# From skill: marketing-storefront

# Skill: marketing-storefront

Own everything a player sees before they buy: store page copy, capsule briefs, trailer outlines, social and press materials. All of it is **drafted internally**; nothing publishes without Svinet's explicit action.

## IP GATE (read first, every run)

ADR 0006 adopted the working commercial title, but `docs/design/design-report.md` ┬º14 forbids the title appearing in any public surface until EU games/IP counsel clears it (gate issue: ANDR-156; domains/handles: ANDR-163). Therefore:

1. **Every draft uses the placeholder `Γƒ¿TITLEΓƒ⌐`** until ANDR-156 is `done` AND written clearance is posted there. Check the gate's live status every run ΓÇö never assume.
2. **Zero public actions, ever.** No store page submission, no social post, no press email. This skill produces internal files and drafts only. Publishing is Svinet's call on his accounts.
3. When clearance lands, a fold-in pass replaces `Γƒ¿TITLEΓƒ⌐` everywhere and records the cleared-name citation.

## Store page copy (primary deliverable)

- **Short description (Γëñ 300 chars):** hook from the design report's pillars ΓÇö one sentence of fantasy, one of loop, one of stakes.
- **Long description:** feature bullets (each maps to a documented system, no invented features), 2ΓÇô3GIF/screenshot callouts, a "why this roguelite" paragraph grounded in the Keep/Remove/Add summary (┬º24).
- **Tags & genre** from the GDD; never claim tags the game does not deliver (Steam punishes tag mismatch with visibility loss).
- Every claim must trace to a design doc section ΓÇö cite it in the draft's margin comments.

## Capsule & trailer briefs

- Capsule briefs go to Technical Artist / Technical Artist-animator pipeline: mood refs from the design report's art direction (ANDR-17 owns final art direction ΓÇö do not contradict landed captures), must read at 460├ù215 thumbnail size.
- Trailer outline: 5-beat structure (hook ΓåÆ loop ΓåÆ escalation ΓåÆ boss/identity beat ΓåÆ title card), shot list mapped to achievable in-engine captures (`godot-qa` scenarios can generate most shots headlessly).

## Social & press (internal drafts only)

- Press kit folder: Γƒ¿TITLEΓƒ⌐ one-liner, 5 bullets, 3 screenshots, logo specs ΓÇö assembled in-repo under `docs/press/`, never uploaded.
- Social calendar as a CSV (date, platform, draft copy) ΓÇö Svinet posts manually or later grants a channel.

## Tone & honesty rules

- Never overclaim: every advertised mechanic must exist in `main` or be visibly in-development-marked.
- The design report's IP guardrails (┬º14) apply to every name and description this skill produces.
- Competitor comparison claims are forbidden (legal risk).

## Verification chain

1. `git grep` for the placeholder discipline: no real title strings in `docs/press/` drafts pre-clearance.
2. Every feature bullet maps to a GDD/report section ΓÇö table in the draft.
3. Capsule brief passes the 460├ù215 readability test (thumbnail mock attached).

## Delivery comment format

- `Draft` ΓÇö file paths (repo-relative).
- `Claims audit` ΓÇö bullet ΓåÆ design-doc citation table.
- `Gate status` ΓÇö ANDR-156 state read this run.
- `Blocked on` ΓÇö any HUMAN action (posting, submitting) with what Svinet needs to do.

---

# From skill: publishing-store-pipeline

# Skill: publishing-store-pipeline

Take a Godot 4 build from green CI to a store-ready submission package. This skill owns store pipelines, build upload mechanics, ratings paperwork assembly, and signing mechanics. QA & Release Lead keeps the release train and cert checklists (`qa-release`); this skill is the storefront plumbing underneath them.

## The human ledger (never attempt these ╬ô├ç├╢ prepare only)

These require Svinet's accounts, money, or legal identity. The agent prepares artifacts and exact click-paths; Svinet executes. Record each item as `HUMAN: <what> ╬ô├ç├╢ prepared by <issue>, executed by member`.

- Platform account creation (Steamworks partner, Epic, console portals), banking, tax forms
- Fees: ratings board submissions, platform fees, SDK access
- NDAs, devkit registration, store agreement signatures
- Credential storage ╬ô├ç├╢ never write tokens/keys into issues, comments, or logs; reference by name

## Steam (primary target)

- **Pipeline:** Steamworks SDK app ╬ô├Ñ├å `steamcmd +login <builder> +run_app_build ../scripts/app_build.vdf` ╬ô├ç├╢ the `.vdf` build script lives in `ci/steam/` in the repo, branch-targeted (`branch: "playtest"` for pre-release, `default` for live).
- **Depots:** one depot per platform; `ContentBuilder` folders mirror `build/` outputs from `godot.yml` (Linux x86_64, Windows exe+pck). Include `steam_appid.txt`.
- **Rollout:** staged rollout percentages for patches; never 100% on day-one of a big patch without a rollback plan (`live-ops-telemetry`).
- **Playtest branches** are the pre-release gate ╬ô├ç├╢ closed beta keys distributed manually by the member.

## Store assets (assembly is agent work)

| asset | spec |
|---|---|
| capsule (main) | 460Γö£├╣215 PNG |
| capsule (small/icon) | 123Γö£├╣123 |
| library hero | 3840Γö£├╣1240 |
| screenshots | ╬ô├½├æ 1920Γö£├╣1080, 4╬ô├ç├┤8 required, real gameplay |
| trailer | 720p/1080p mp4 (H.264), ╬ô├½├▒ 3 min for the main slot |

Capsule art production is **Technical Artist / Technical Animator** work (briefs authored by Marketing per `marketing-storefront`); this skill only validates specs and assembles the submission folder.

## Ratings

- **IARC** questionnaire (covers PEGI/ESRB/USK-class territories for digital-only): assemble answers from the design report's content descriptors (violence level, UGC, gambling-adjacent mechanics ╬ô├ç├╢ the roguelite's meta-progression must be described honestly). Draft lives in `docs/publishing/ratings-iarc.md`; Svinet submits and pays.
- Never claim a rating the board has not issued; store pages must show the real badge only.

## Signing

- **Windows:** `signtool sign /fd SHA256 /td SHA256 /tr http://timestamp.digicert.com` with the member-provided cert (path referenced, never the password).
- **macOS:** codesign + notarization (`notarytool`) ╬ô├ç├╢ requires Apple Developer account (HUMAN).
- **Linux:** unsigned; ship `.x86_64` + `.pck` per `godot-headless-ci`.

## Verification chain

1. Export artifacts exist per platform (CI green: `build`, `build-windows`).
2. `steamcmd` validate script: `+list_depots <appid>` exit 0 (dry run, no upload).
3. Store asset folder passes spec checks (sizes, formats) ╬ô├ç├╢ scripted in `tools/validate_store_assets.py`.
4. Ratings draft has an answer for every IARC question ╬ô├ç├╢ checklist in the issue.

## Delivery comment format

- `Package` ╬ô├ç├╢ artifact paths + sizes + checksums.
- `Store readiness` ╬ô├ç├╢ checklist with `[x]`/`[ ]` per asset/spec.
- `HUMAN items` ╬ô├ç├╢ exactly what Svinet must click/pay/sign, with links.
- `Verification` ╬ô├ç├╢ commands + exit codes.

