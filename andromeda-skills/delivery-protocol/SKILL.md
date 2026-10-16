---
name: delivery-protocol
description: Autonomous delivery contract: sibling-run awareness, fresh-branch discipline, local gates, independent-audit gate before merge, issue status + evidence. Every agent ships without a human, nobody self-accepts.
---



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


