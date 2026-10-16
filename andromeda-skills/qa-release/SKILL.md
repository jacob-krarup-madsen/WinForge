---
name: qa-release
description: Test strategy, regression selection, bug triage format, platform export and certification checklists, release train and patch-note discipline.
---

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

