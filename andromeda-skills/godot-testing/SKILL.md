---
name: godot-testing
description: Godot 4.7.2 testing: GUT and GdUnit4 headless runners, godot-qa scenario format with input replay and screenshot diff, JUnit XML output, exit-code contract.
---

# Godot 4 Testing

Target: **Godot 4**. All runners are headless and CI-gateable. Choose one primary framework and keep `godot-qa` for scenario/playthrough coverage.

## Framework choice

| | **GUT 9.x (Godot 4)** | **GdUnit4 4.x** |
|---|---|---|
| Style | xUnit-ish, `test_*` methods, `assert_*` | xUnit-ish, `await assert_that(...)` fluent |
| Best for | Simple unit + scene tests, biggest community | Rich matchers, parameterised tests, mock support |
| Runner | `addons/gut/run.gd` | `addons/gdUnit4/runtest.gd` |

Both install as addons: copy into `res://addons/`, enable in Project Settings (or let `--import` pick them up). Commit `addons/` to the repo.

## GUT headless run

```powershell
godot --headless -s res://addons/gut/run.gd `
      -gdir=res://test/unit `
      -ginclude_subdirs `
      -gunit_test_reports=build/test/gut/junit.xml `
      -gjunit_xml_file=build/test/gut/junit.xml `
      -gexit
```

Common flags: `-gdir=<path>` (repeatable), `-gtest=<name>`, `-gselect=<script>`, `-glog=1..3`, `-gexit` (quit when done ╬ô├ç├╢ **required** in CI), `-gjunit_xml_file`, `-gunit_test_reports`.

Test script shape:

```gdscript
extends GutTest

func test_damage_reduces_health() -> void:
    var p := load("res://scenes/player/player.gd").new()
    p.health = 100
    p.take_damage(30, null)
    assert_eq(p.health, 70)

func test_player_dies_and_emits() -> void:
    var p := autofree(load("res://scenes/player/player.tscn").instantiate())
    add_child_autoqfree(p)
    var watcher := watch_signals(p)
    p.take_damage(9999, null)
    assert_signal_emitted(p, "player_died")
    await wait_for_signal(p, "player_died", 1.0)
```

Helpers: `autofree()` / `add_child_autoqfree()` (prevents leaks), `watch_signals()`, `assert_eq/assert_true/assert_null/assert_called`, `partial_double()` for spies, `yield_for(sec)` for timing.

## GdUnit4 headless run

```powershell
godot --headless -s res://addons/gdUnit4/runtest.gd `
      --path res://test `
      --output ./report `
      --reports JUnit_xml `
      --ignoreHeadlessMode
```

Test script shape:

```gdscript
extends GdUnitSuite

func test_damage_reduces_health() -> void:
    var p := DamageCalculator.new()
    assert_int(p.compute(100, 0, false)).is_equal(100)

func test_parameterised(arg: int, expected: int) -> void:
    assert_int(DamageCalculator.clamp(arg)).is_equal(expected)
# +func test_parameterised_build() -> Array: return [[-5,0],[50,50],[999,100]]
```

`--ignoreHeadlessMode` is needed when the suite touches scene-tree features headlessly.

## `godot-qa` scenario tests (playthrough + input replay)

`godot-qa` is a headless playtest harness: it launches the game headlessly, drives it with **recorded input**, captures screenshots, and diffs them. Scenario format (YAML/TOML depending on installed version ╬ô├ç├╢ check `godot-qa --help`):

```yaml
# test/scenarios/player_crosses_gap.yaml
scenario: player crosses gap
scene: res://scenes/levels/level_01.tscn
timeout_seconds: 20
steps:
  - wait: 0.5                      # let _ready / autoloads settle
  - input: { action: move_right, hold: 1.2 }
  - input: { action: move_jump, hold: 0.15 }
  - wait: 1.0
  - expect:
      player_position_x: { min: 800 }
      player_state: "IDLE"
      screenshot: baseline/level_01_gap.png
      diff_tolerance: 0.01          # % pixels allowed to differ
```

Core concepts:
- **Input replay**: `input:` steps are injected through the input system as *actions*, not raw scancodes, so the same scenario works on any device and is deterministic. Record new scenarios by capturing action events during a real run, then hand-normalise the timings.
- **Screenshot diff**: each `expect.screenshot` compares the current frame against a committed baseline; `diff_tolerance` absorbs AA/driver noise. Regenerate baselines explicitly (`godot-qa update-baselines`) and review the image diff in the PR ╬ô├ç├╢ never silently overwrite.
- **Assertions**: position/state/scene-tree predicates under `expect:`. Prefer them over screenshots for logic; use screenshots for rendering regressions.
- Run headless: `godot-qa run --headless --dir res://test/scenarios --report build/qa/junit.xml`.

Scenario authoring rules: one behaviour per file; no absolute timings under 100 ms; always `wait` after scene load before the first input; never assert on frame numbers, assert on state.

## JUnit XML output

Every runner can emit JUnit XML; CI systems parse it for the test report UI.

- GUT: `-gjunit_xml_file=build/test/junit.xml`
- GdUnit4: `--reports JUnit_xml --output ./report`
- godot-qa: `--report build/qa/junit.xml`

Write them to separate files and publish all of them, or merge in CI. Directory must exist before the run (`New-Item -ItemType Directory -Force build/test`).

## Exit-code contract (the important part)

| Runner | Success | Failure |
|---|---|---|
| GUT with `-gexit` | `0` | `1` when any test fails/errors (9.x guarantees this) |
| GdUnit4 | `0` | `1` (also emits a report with `is_success=false`) |
| godot-qa | `0` | `1` on assertion/diff failure |
| `--check-only --script` | `0` | `1` on parse/type error |

Rules:
1. **Always pass the exit-force flag** (`-gexit`, etc.). Without it Godot idles and the CI job hangs until timeout, reporting a false failure.
2. A runner that exits `0` after printing failures is a broken gate. Verify once on a deliberately failing test: introduce `assert_eq(1, 2)`, run, confirm exit `1`, revert.
3. Godot itself can print `SCRIPT ERROR` (autoload crash, missing node) while a test runner still exits `0`. In CI, **grep the captured log for `SCRIPT ERROR|FATAL` and fail the job** even when the code is 0.
4. Never mark a failing job `continue-on-error`. A red test gate that doesn't block is worse than no gate.

## CI wiring

```powershell
New-Item -ItemType Directory -Force build/test | Out-Null
godot --headless --import                      # or fail
godot --headless -s res://addons/gut/run.gd -gdir=res://test -gexit -gjunit_xml_file=build/test/junit.xml `
  --log-file build/test/gut.log
$code = $LASTEXITCODE
if ((Select-String -Path build/test/gut.log -Pattern 'SCRIPT ERROR|FATAL' -Quiet)) { $code = 1 }
exit $code
```

## Test layout and authoring rules

```
res://test/
  unit/          # pure logic, no scene load
  scenes/        # node behaviour, minimal fixtures
  scenarios/     # godot-qa playthroughs + baselines/
  fixtures/      # small .tscn used only by tests
```

- Unit-test rule classes as plain `RefCounted` with static/pure functions ╬ô├ç├╢ fastest and most reliable (see `godot-gameplay` on testable design).
- No wall-clock sleeps as synchronisation: `await wait_for_signal(...)` or `await get_tree().physics_frame`.
- Tests must not depend on execution order; each creates its own fixtures.
- Keep the suite under a few minutes; move slow playthroughs to a nightly job, keep fast unit tests on every PR.

