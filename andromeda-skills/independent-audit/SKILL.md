---
name: independent-audit
description: Mandatory independent review: no agent audits their own work; a designated different agent re-runs all gates, inspects the diff, and holds merge authority. No guessing rule: every claim carries evidence, unverifiable = UNKNOWN.
---



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


