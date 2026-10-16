---
name: godot-engine
description: Godot 4.7.2 engine pack: GDScript gameplay conventions, .tscn-safe scene tooling via MCP and CLI, headless CI import/export gates, GUT/GdUnit4 testing with input replay and screenshot diff, audio bus layout and procedural SFX, ENet/WebSocket netcode and dedicated servers. Use when writing or validating Godot gameplay, scenes, audio, networking, tests, or CI pipelines.
---

---

# From skill: godot-gameplay

# Godot 4 Gameplay

Target: **Godot 4 (stable, released 18 Aug 2026)**. Never write against 4.8 dev snapshots. GDScript is the primary language; use C# only when justified (hot-path math, third-party .NET libs, team C# expertise).

## GDScript conventions (4.x)

- Header must be `class_name Foo extends Node2D` (or `@tool`, `@icon` before `class_name` when needed).
- Typed everything: `var speed := 320.0`, `func take_damage(amount: int, source: Node) -> void`. No untyped `:=` inference from `null`.
- Signals declared with `signal health_changed(old: int, new: int)` ╬ô├ç├╢ typed args, past-tense names (`player_died`, `item_collected`).
- Exports: `@export var speed: float = 320.0`, `@export_range(0, 100) var armor := 0`, `@export var jump_curve: Curve`, `@export_file("*.tscn") var intro_scene: String`.
- Groups for cross-cutting lookup: `add_to_group("enemies")` in `_ready()`; query with `get_tree().get_nodes_in_group("enemies")`.
- Autoloads for globals (`Game`, `Audio`, `Save`), declared in `project.godot` `[autoload]`. Never reach across the tree with `get_node("/root/Game/Player/...")` from unrelated code ╬ô├ç├╢ emit a signal or use a group.
- Avoid `process`/`physics_process` flags toggled at runtime unless profiling proves it; prefer `_physics_process` returning early.
- Deprecated 3.x APIs are hard errors: `KinematicBody2D`, `move_and_slide(vel)`, `yield`, `onready`, `export var`, `OS.window_fullscreen`, `.instance()`. Use `CharacterBody2D`, `move_and_slide()` with `velocity`, `await`, `@onready`, `@export`, `DisplayServer`, `instantiate()`.

## Nodes, scenes, signals

- A scene = one root node + children; scripts attach to roots, not to every node. Composition over deep inheritance.
- Prefer signals for "something happened", callables for "do this now". Connect with `button.pressed.connect(_on_button_pressed)`; one-shot with `CONNECT_ONE_SHOT`. Disconnect in `_exit_tree()` when the emitter outlives the receiver.
- `@onready var label: Label = $HealthLabel` ╬ô├ç├╢ never cache node refs in `@onready` for nodes that get freed (`queue_free()`); re-fetch or use `is_instance_valid()`.
- Scene ownership: children created at runtime need `add_child()`; packed scenes from disk need `instantiate()` and, if reparented, `owner` set when inside an editor script.

## `_physics_process(delta)` vs `_process(delta)`

| Use `_physics_process` | Use `_process` |
|---|---|
| Character movement, jump, coyote time | Camera follow/smoothing, look-at |
| Rigid/body collisions, raycasts | Tweening, sprite animation, VFX |
| Gameplay timers tied to simulation | UI, HUD, damage numbers |
| Fixed timestep logic (60 Hz default, `physics_ticks_per_second` in Project Settings) | Anything visual-only |

Rules:
- Never move a `CharacterBody2D` in `_process` ╬ô├ç├╢ collisions desync at uneven frame rates.
- Read input **once per physics frame** via `Input.get_vector()` / `Input.is_action_just_pressed()`, not in both `_process` and `_physics_process`.
- `delta` is seconds. Movement: `velocity = direction * speed; move_and_slide()`. Never multiply by `delta` inside `move_and_slide()` (it applies delta itself).
- For frame-rate-independent lerp use `lerpf(a, b, 1.0 - exp(-lambda * delta))` (exponential smoothing), not `lerpf(a, b, delta)`.

## Input map

- Define actions in **Project Settings ╬ô├Ñ├å Input Map** (`[input]` block of `project.godot`), never raw keycodes: `move_left`, `move_right`, `move_jump`, `attack_primary`, `ui_cancel`.
- Godot 4: `Input.get_vector("move_left", "move_right", "move_up", "move_down")` returns a normalized Vector2 with deadzone handling.
- Mouse: `event.button_index == MOUSE_BUTTON_LEFT` in `_unhandled_input`, or use actions with `InputEventMouseButton` bindings.
- Handle gameplay input in `_unhandled_input(event)` / `_input(event)` so UI (`Control` nodes with `mouse_filter`) consumes clicks first; check `get_viewport().gui_get_hovered_control()` only as a last resort.
- For replays and tests, drive actions programmatically: `Input.action_press("move_jump")`, or feed `Input.parse_input_event()` ╬ô├ç├╢ never synthesize raw mouse motion.

## CharacterBody2D / CharacterBody3D movement

```gdscript
extends CharacterBody2D

const ACCEL := 2400.0
const FRICTION := 3000.0
const JUMP_VELOCITY := -780.0
const MAX_FALL := 1400.0

@export var speed := 320.0
var _gravity: float = ProjectSettings.get_setting("physics/2d/default_gravity")

func _physics_process(delta: float) -> void:
    if not is_on_floor():
        velocity.y = minf(velocity.y + _gravity * delta, MAX_FALL)

    var dir := Input.get_vector("move_left", "move_right", "move_up", "move_down")
    if dir != Vector2.ZERO:
        velocity.x = move_toward(velocity.x, dir.x * speed, ACCEL * delta)
    else:
        velocity.x = move_toward(velocity.x, 0.0, FRICTION * delta)

    if Input.is_action_just_pressed("move_jump") and is_on_floor():
        velocity.y = JUMP_VELOCITY

    move_and_slide()
```

3D specifics: read gravity from `physics/3d/default_gravity`; use `up_direction = Vector3.UP` (default) and `floor_max_angle`; for climbing/ceiling use `get_floor_normal()`, `is_on_wall()`, `is_on_ceiling()`. Rotate the model node, **not** the body, to face the movement direction (`look_at` with `up_direction`).

For camera-relative movement in 3D: `dir = global_basis * dir` or transform by the camera's `global_transform.basis`.

## State machines

Small games: enum + match (fast, greppable, no node overhead).

```gdscript
enum State { IDLE, RUN, JUMP, ATTACK, HURT, DEAD }
var state: State = State.IDLE

func _physics_process(delta: float) -> void:
    match state:
        State.IDLE: _tick_idle(delta)
        State.RUN:  _tick_run(delta)
        State.ATTACK: _tick_attack(delta)
```

Larger games: one child `Node` per state, parent holds `current_state: State` and delegates `_physics_process`; each state implements `enter(params)`, `exit()`, `physics_update(delta)`, `handle_input(event)`. Transition via `transition_to(&"Attack")` which calls `exit()`/`enter()` and emits `state_changed(old, new)`.

Rules for both:
- Exactly one place decides transitions ╬ô├ç├╢ no transition logic inside `_physics_process` of the controller *and* the state.
- Never `queue_free()` the current state node while it is executing; set state first, free after.
- Timers are state data (`attack_time_left`), not `await get_tree().create_timer()` chains, unless the state machine is built on coroutines deliberately.

## Game feel (cheap, high value)

- Screen shake: a `Camera2D` offset driven by a `NoiseTexture2D` or a decaying trauma value; clamp `offset.length()`.
- Hit pause: `Engine.time_scale = 0.05` for 60╬ô├ç├┤90 ms real time, then restore to `1.0`. Always restore, including on scene change.
- `_ready` order: children first, parent last. Use `call_deferred` or `await get_tree().physics_frame` when a child needs parent state.
- Tween with `create_tween().set_trans(Tween.TRANS_CUBIC).set_ease(Tween.EASE_OUT)` and store the `Tween`; `tween.kill()` before re-triggering.

## GUT-friendly testable design

Tests must run **headless without a display** and without touching globals you don't control.

- Keep rules in plain classes with no node dependencies where possible: `class_name DamageCalculator extends RefCounted` with `static func compute(base: int, armor: int, crit: bool) -> int`. Test those directly ╬ô├ç├╢ zero scene setup.
- Anything that must be a node should be testable as a bare scene: root script, no hardcoded `$Path/To/Node` absolute paths, no autoload dependency in `_ready` (resolve lazily via `get_node_or_null("/root/Game")`).
- Never read `Input` directly inside rule code. Pass `dir: Vector2` / `actions: Dictionary` in; a thin controller reads `Input` and calls the rule. This is what makes input-replay tests possible.
- Expose state for assertions: `var state: State`, `var health: int` public; don't hide everything behind getters that need the scene tree.
- Signals are the assertion surface: tests `await player.player_died`.
- Guard rendering/audio: `if Engine.is_editor_hint(): return`, and gate audio behind an autoload `Audio` that is a no-op when the tree is headless.
- One scene per testable unit in `test/scenes/`, kept minimal (root + the nodes under test).

## Validation before you hand off

```powershell
godot --headless --check-only --script res://path/to/script.gd
godot --headless --import
```

Parse-check every `.gd` you touched. Never edit `.tscn`/`.tres`/`project.godot` as raw text ╬ô├ç├╢ see `godot-scene-tooling`.

---

# From skill: godot-scene-tooling

# Godot 4 Scene & Resource Tooling

Target: **Godot 4**. This skill covers *how to mutate scenes and resources safely* ╬ô├ç├╢ via MCP tooling or the Godot CLI, never by hand.

## The hard rule: never hand-edit `.tscn`, `.tres`, or `project.godot`

These files are text, but they are a **serialized engine format**, not a data format:

- Node entries carry a stable `id`, `parent` string path, and `owner` linkage. Renaming or reordering by hand silently changes `get_path()` strings and breaks `$Node` lookups at runtime without a parse error.
- Sub-resources carry `sub_resource type="X" id="..."` and are referenced by `id="..."`. A duplicate or dangling id loads as `null` or as the *wrong* resource ╬ô├ç├╢ no error.
- `[ext_resource type="PackedScene" path="res://..." id="1_abcde"]` ids are 5-char hashes that get rewritten on save; scripts use `uid://` refs (`[ext_resource type="Script" script_class="..." id=...]`). Break the uid and the script silently detaches from the node (node runs with no script, no error in the log).
- `[node name="Player" instance=ExtResource("2_xk4")]` inheritance chains re-save parent scene state; a wrong indent inside `node/parent/...` section keys drops overrides with no warning.
- `project.godot` requires exact section/key syntax (`input` action events are an array of dictionaries with `deadzone` and `events`). A malformed entry can fall back to defaults ╬ô├ç├╢ the game still runs, with the wrong controls.

**Symptom of a bad edit:** project opens, game runs, behaviour is just wrong. That is the failure mode ╬ô├ç├╢ not a crash.

### Allowed edits

| File | Hand edit? |
|---|---|
| `.gd`, `.gdshader`, `.cs` | Yes ╬ô├ç├╢ plain text source |
| `.import` sidecars | Only to copy a known-good preset block; prefer CLI reimport |
| `export_presets.cfg` | Yes, with care (see `godot-headless-ci`) |
| `.tscn`, `.tres` | **No** ╬ô├ç├╢ MCP tooling or Godot CLI only |
| `project.godot` | **No** ╬ô├ç├╢ use `--editor` scripting or Project Settings via MCP |

If you must inspect a `.tscn`, **read** it ╬ô├ç├╢ reading is fine and is often the fastest way to understand a scene. Only mutation is forbidden.

## Driving scene edits: MCP tooling and CLIs

Preferred order for any scene/resource mutation:

1. **Godot MCP server tools** (Agent Tools / Godot AI style servers expose 70╬ô├ç├┤150 tools). Typical tool names you'll see:
   - `scene_create`, `scene_open`, `scene_save`, `scene_get_node_tree`
   - `node_add`, `node_remove`, `node_set_property`, `node_reparent`, `node_set_script`
   - `resource_create`, `resource_set_property`, `resource_assign_to_node`
   - `script_create`, `script_edit`, `script_parse_check`
   - `signal_connect`, `signal_disconnect`
   - `project_settings_get` / `project_settings_set`
   - `editor_inspect`, `editor_undo`, `editor_redo`
2. **`gda`** ╬ô├ç├╢ the Godot agent CLI. Use for scripted, repeatable editor operations: `gda scene inspect res://scenes/player.tscn`, `gda node add ...`, `gda run --headless`. Check `gda --help` for the exact subcommands on the installed version; the contract is the same: it talks to a running/headless editor rather than editing text.
3. **Godot CLI with a script**: when no MCP server is configured, run an `@tool` script headlessly:

```gdscript
# tools/mutate_scene.gd  ╬ô├ç├╢ run with:
# godot --headless --script tools/mutate_scene.gd
@tool
extends SceneTree

func _init() -> void:
    var packed := load("res://scenes/player.tscn") as PackedScene
    var inst := packed.instantiate()
    var hitbox := Area2D.new()
    hitbox.name = "Hitbox"
    inst.add_child(hitbox)
    hitbox.owner = inst          # required, or the node is not saved
    var new_packed := PackedScene.new()
    new_packed.pack(inst)
    var err := ResourceSaver.save(new_packed, "res://scenes/player.tscn")
    print("saved err=", err)
    quit(0 if err == OK else 1)
```

4. **`@tool` editor scripts** (`Tools ╬ô├Ñ├å Run`) for in-editor batch work.

Anything that runs headlessly must `quit(code)` explicitly.

## UID handling

- Every asset gets a uid in its `.import` file (`uid://bq7k2...`). Scenes/scripts store the uid, the path, or both.
- Moving/renaming an asset **through the editor or MCP** rewrites references. Moving it in the file system (`mv`) leaves dangling paths.
- If you must relocate files, do it via an MCP file-move tool or by running the editor once with `--import` afterwards so it can reconcile.
- Never invent a uid string. Copy one only from the same asset's own `.import` / header.
- To check a scene's script linkage after an operation, read the node tree via MCP and confirm `script` is non-null on the expected nodes.

## Resource authoring

- Create `.tres` through MCP `resource_create` or an editor script, not by pasting a `gd_resource` header.
- Custom resources: define `class_name ItemData extends Resource`, `@export var id: StringName`, then instantiate via the FileSystem dock or `ResourceSaver.save(ItemData.new(), "res://data/items/sword.tres")`.
- Shared materials: one `.tres` material referenced by many meshes. Per-instance overrides live on the **mesh instance node** as a sub-resource ╬ô├ç├╢ mutating the shared material changes every user, which is usually what you want.
- Sub-resources created in code must be assigned (`mesh_instance.material_override = mat`) *before* `pack()`, and `owner` set for anything that should persist.
- `uid://` vs path: prefer `load("res://...")` in code; `ResourceUID` lookups only when you already have a uid.

## Validation after every mutation

```powershell
# 1. Parse-check every script you touched (fast, no scene load)
godot --headless --check-only --script res://scenes/player/player.gd

# 2. Full import ╬ô├ç├╢ catches broken uids, missing deps, bad ext_resources
godot --headless --import

# 3. Open the project once to surface load errors in the log
godot --headless --quit-after 2
```

Exit code `0` plus no `ERROR`/`SCRIPT ERROR` lines in the captured output is the pass condition. `--import` regenerates `.godot/` ╬ô├ç├╢ run it in CI and after any asset move (details in `godot-headless-ci`).

## Undo discipline

- Prefer MCP/CLI operations that go through the editor's `UndoRedo` so a human can Ctrl+Z.
- Batch mutations then save once; do not save between each node add (each save rewrites the whole file and creates noisy diffs).
- After a scripted mutation, diff the `.tscn` in git: a sane diff touches only the nodes you intended. A diff that rewrites ids everywhere means the scene was saved by a different engine version ╬ô├ç├╢ stop and investigate.

## Quick decision table

| Task | Do this |
|---|---|
| Add a node to a scene | MCP `node_add`, set `owner`, `scene_save` |
| Attach a script | Write the `.gd` by hand, then MCP `node_set_script` |
| Change an exported default used by one scene | MCP `node_set_property` |
| Change a shared material/`.tres` | MCP `resource_set_property` |
| Change input bindings / autoloads | MCP `project_settings_set` |
| Verify it worked | `--check-only` + `--headless --import` |

---

# From skill: godot-headless-ci

# Godot 4 Headless CI

Target: **Godot 4 stable**. Everything below runs without a display; no editor, no GUI.

## Get the right binary

- Download the official **Godot_v4.7.2-stable_linux.x86_64.zip`** (or `_win64.zip` / `_macos.universal.zip`) from the Godot releases and pin the version in a CI variable (`GODOT_VERSION=4.7.2`). Never `latest`.
- Also needed: **`.NET`/mono build** only if the project has `.cs` files. Otherwise use the standard build ╬ô├ç├╢ it is smaller and faster.
- First run needs the export templates: `Godot_v4.7.2-stable_export_templates.tpz`, installed to:
  - Linux: `~/.local/share/godot/export_templates/4.7.2.stable/`
  - Windows: `%APPDATA%\Godot\export_templates\4.7.2.stable\`
  - macOS: `~/Library/Application Support/Godot/export_templates/4.7.2.stable/`
- Install templates by unpacking `tpz` (it is a zip; rename to `.zip`) and copying the inner `templates/` folder to the path above. Cache this directory in CI ╬ô├ç├╢ it is ~1 GB.

## The canonical CI steps

```powershell
# 1. Import ╬ô├ç├╢ generates .godot/, resolves uids, compiles import plugins.
#    ALWAYS run this before anything else on a fresh checkout.
$env:GODOT = "godot"   # or the full path to Godot_v4.7.2-stable
& $GODOT --headless --import
if ($LASTEXITCODE -ne 0) { throw "import failed" }

# 2. Script parse/compile check over every script
Get-ChildItem -Recurse -Filter *.gd | ForEach-Object {
    & $GODOT --headless --check-only --script ($_.FullName -replace '\\','/')
    if ($LASTEXITCODE -ne 0) { throw "script check failed: $($_.Name)" }
}

# 3. Tests (see godot-testing) ╬ô├ç├╢ must be a gate, not advisory

# 4. Export artifacts
& $GODOT --headless --export-release "Linux" build/game.x86_64
```

### Flag reference (4.7.2)

| Flag | Purpose |
|---|---|
| `--headless` | No display/audio server window. Required for CI. |
| `--import` | Run the import pipeline then exit. Idempotent. |
| `--check-only --script <path>` | Parse/type-check one `.gd` without running it. Exit 1 on error. |
| `--quit-after N` | Quit after N **iterations of the main loop** (frames), not seconds. Good smoke test: `--quit-after 60`. |
| `--script <path>` | Run a `SceneTree`/`MainLoop` script headlessly. |
| `--editor` | Editor mode (never in CI except for `--quit-after` editor smoke). |
| `--export-release <preset> <path>` | Export with the named preset. Requires `export_presets.cfg`. |
| `--export-debug <preset> <path>` | Debug export (keeps debug symbols, godot physics debug). |
| `--editor-pid N`, `--verbose`, `--log-file <path>` | Diagnostics. Capture `--log-file` on failure. |

Exit-code contract: **0 = success, non-zero = failure**. Treat any non-zero as a failed build. Godot sometimes prints `SCRIPT ERROR` while still exiting 0 ╬ô├ç├╢ grep the log as well as checking the code.

## `export_presets.cfg`

Lives at the project root. It is plain INI and **is** safe to author/edit by hand (one of the few Godot files that is), but prefer generating it once in the editor and committing it.

```ini
[preset.0]
name="Linux"
platform="Linux"
runnable=true
export_filter="all_resources"
include_filter=""
exclude_filter="test/*, *.md"
export_path="build/game.x86_64"
encrypt_pck=false
seed=0

[preset.0.options]
custom_template/debug=""
custom_template/release=""
binary_format/architecture="x86_64"
texture_format/s3tc_bptc=true
```

Key points:
- `platform` must match exactly: `Linux`, `Windows Desktop`, `macOS`, `Android`, `iOS`, `Web`.
- Preset **name** is what `--export-release` takes; names must be unique.
- Secrets (Android keystore, notarization) must not be committed ╬ô├ç├╢ pass via environment and a generated preset, or use `--export-pack` plus a separate signing step.
- `export_filter`: `all_resources` / `scenes` / `resources` / `selected_scenes` (with `export_files=`).
- Adding `exclude_filter` for `test/*` keeps test scenes out of shipping builds.

## Template install (once per runner)

```powershell
$ver = "4.7.2.stable"
$dest = "$env:APPDATA\Godot\export_templates\$ver"
New-Item -ItemType Directory -Force -Path $dest | Out-Null
Expand-Archive -Path ".\Godot_v4.7.2-stable_export_templates.tpz" -DestinationPath "$env:TEMP\tpz" -Force
Copy-Item "$env:TEMP\tpz\templates\*" $dest -Recurse -Force
```

Cache `$dest` keyed on `GODOT_VERSION`. Missing templates produce `Cannot export project: export template not found` and a non-zero exit.

## CI gate wiring

Required gates, in order ╬ô├ç├╢ each blocks the next:

1. **Import** (`--headless --import`) ╬ô├ç├╢ fails on missing/broken deps, bad uids, import plugin errors.
2. **Script check** (`--check-only` per script, or one wrapper script that walks `res://`) ╬ô├ç├╢ syntax and type errors.
3. **Unit/integration tests** ╬ô├ç├╢ GUT/GdUnit4/headless scenario runner, must exit non-zero on failure (see `godot-testing`).
4. **Export** (`--export-release`) ╬ô├ç├╢ proves the preset and templates work; upload `build/` as the artifact.
5. Optional: a **headless smoke run** ╬ô├ç├╢ `--headless --quit-after 120 --log-file smoke.log`, then fail if `smoke.log` matches `SCRIPT ERROR|ERROR: Cannot`.

Wire-up sketch (any CI system):

```yaml
- name: Cache export templates
  uses: actions/cache@v4
  with: { path: ~/.local/share/godot/export_templates/4.7.2.stable, key: godot-templates-4.7.2 }

- name: Import
  run: godot --headless --import

- name: Script check
  run: pwsh ./ci/check-scripts.ps1

- name: Test
  run: godot --headless -s res://addons/gut/run.gd -gexit
  # exact runner differs per framework ╬ô├ç├╢ see godot-testing

- name: Export
  run: godot --headless --export-release "Linux" build/game.x86_64
```

Rules:
- Cache `.godot/` only if you also cache the source revision it was built from; a stale `.godot/` is the #1 source of phantom CI failures. Safest: don't cache it, `--import` takes seconds to a few minutes.
- Always run `--import` on a clean checkout before any other Godot invocation.
- Parallelise script checks only if the Godot binary supports it safely ╬ô├ç├╢ one `--check-only` process per script is fine; avoid two Godot processes writing `.godot/` at once.
- Fail the job on any non-zero exit and on `SCRIPT ERROR` in captured logs.

## Troubleshooting

| Symptom | Cause / fix |
|---|---|
| `Cannot open file: res://.godot/global_script_class_cache.cfg` | Forgot `--import`. |
| Export template not found | Templates installed under the wrong version dir (`4.7.2.stable`, not `4.7.2`). |
| `No export template found for platform X` | Preset `platform` string mismatch. |
| Headless run hangs | Something awaits a display/audio; add `--quit-after N` and check for `await` on signals that never fire headless. |
| Exports 0 bytes / missing scenes | Import never ran, so `*.import` and uid cache are stale. |
| Exit 0 but build broken | Errors only printed; grep `--log-file` for `ERROR`/`SCRIPT ERROR`. |

---

# From skill: godot-testing

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

---

# From skill: godot-audio

# godot audio

Audio production in **Godot 4** ╬ô├ç├╢ import, bus layout, playback, procedural SFX, and headless safe operation.

## Import formats and when to use each

| Format | Use | Notes |
|   |   |   |
| `.wav` | Short SFX, UI, anything needing instant zero latency start | Imported as `AudioStreamWAV`; keep 16 bit PCM |
| `.ogg` | Music, ambience, long loops | Imported as `AudioStreamOGGVorbis`; enable **Loop** in the import defaults for anything that repeats |
| `.mp3` | Compressed music when file size matters | Imported as `AudioStreamMP3`; supports gapless looping |

Import settings live on the `.import` sidecar ╬ô├ç├╢ change them via the FileSystem dock **Import** tab, or in bulk by editing the preset under `Project Settings > Import Defaults`. After changing defaults, reimport:

```bash
godot   headless   path .   import
```

Never hand edit `.import` or `.uid` files. If a stream must decompress in memory (low latency SFX), set **Edit > Import > Keep** versus **Decompress from Disk** deliberately: decompress from disk costs a frame of latency, in memory costs RAM.

## Nodes

  `AudioStreamPlayer` ╬ô├ç├╢ 2D independent (music, UI, global loops)
  `AudioStreamPlayer2D` / `AudioStreamPlayer3D` ╬ô├ç├╢ positional, with attenuation and max distance
  `AudioStreamRandomizer` ╬ô├ç├╢ pick a child stream at random with pitch/volume jitter; **use this instead of hand rolling random SFX selection**
  `AudioStreamInteractive` ╬ô├ç├╢ adaptive music with transitions between clips (the 4.x replacement for ad hoc crossfade scripts)
  `AudioStreamPolyphonic` ╬ô├ç├╢ play many streams from one player (footsteps, crowds)

## Bus layout

Never route everything to Master. Create `Master > Music`, `Master > SFX`, `Master > UI`, `Master > Voice` in the **Audio** bus panel, then save the layout ╬ô├ç├╢ Godot writes it to `res://default_bus_layout.tres`.

  A `.tres` written by the editor is safe; hand editing one is not. If the layout must change without the editor, drive it from code in an autoload:

```gdscript
AudioServer.set_bus_layout(load("res://default_bus_layout.tres"))
AudioServer.set_bus_volume_db(idx, linear_to_db(0.8))
AudioServer.set_bus_mute(idx, true)
```

Standard effects chain: `Limiter` on Master (last), `Compressor` on SFX, `Reverb` on a dedicated environment send.

## Procedural SFX

Generate WAV data in code ╬ô├ç├╢ this is the fully automatic path, no assets to import:

```gdscript
var stream := AudioStreamWAV.new()
stream.format = AudioStreamWAV.FORMAT_16_BITS
stream.mix_rate = 44100
stream.stereo = false
stream.data = PackedByteArray(your_pcm_bytes)  # little endian signed 16 bit
$Player.stream = stream
$Player.play()
```

Good candidates for procedural generation: UI clicks, hit confirms, impact variations (noise burst + exponential decay envelope), and pitch jittered variants of one base sample via `pitch_scale`.

## Naming and layout

```
res://assets/audio/sfx/     sfx_impact_metal_01.wav
res://assets/audio/music/   mus_combat_loop.ogg
res://assets/audio/amb/     amb_forest_night.ogg
res://assets/audio/ui/      ui_click_confirm.wav
```

Lowercase, underscore separated, numbered variants (`_01`, `_02`) when a set exists.

## Headless and CI

Audio is a first class failure point in automation ╬ô├ç├╢ a missing bus layout or a null stream crashes only at runtime. Always pass:

```bash
godot   headless   path .   quit after 60          # audio driver Dummy is implied by   headless
```

Headless runs use the **Dummy** audio driver; playback calls succeed but produce no sound, which is what you want for tests.

Verification checklist before calling audio work done:

1. `godot   headless   check only   script res://path/to/audio_script.gd` parses clean
2. A headless boot with `  quit after N` exits 0 with no `AUDIO` errors in stdout
3. Every referenced stream path exists (`FileAccess.file_exists` in a test)
4. The bus layout loads and required bus names are present
5. Music loops report `loop_mode != AudioStreamWAV.LOOP_DISABLED` (or the ogg loop flag) where looping is intended

## Boundaries

  Do not hand edit `.import`, `.uid`, `.ogg.import`, or `default_bus_layout.tres`.
  Do not touch Master volume as a substitute for mixing ╬ô├ç├╢ route to buses.
  Licensed/commercial audio: generate procedurally by default; if a licensed track is genuinely needed, purchase it with ordinary judgment and record the spend on the issue. No human decision step exists.

---

# From skill: godot-networking

# godot networking

Multiplayer and netcode in **Godot 4** ╬ô├ç├╢ transports, authority, RPCs, dedicated servers, and how to test it all without a second human.

## Architecture defaults

Pick deliberately, in this order:

1. **Authoritative server** ╬ô├ç├╢ server owns state, clients send intents. Default for anything competitive.
2. **Client side prediction + server reconciliation** ╬ô├ç├╢ for movement; never trust client positions.
3. **Lockstep/determinism** ╬ô├ç├╢ only for RTS like games; Godot gives you no built in determinism guarantee, so you must provide fixed tick simulation and fixed point math yourself.

## Transports

| Peer | Use | Notes |
|   |   |   |
| `ENetMultiplayerPeer` | Default LAN/internet UDP | Channels, reliable/unreliable, `create_server(port, max_clients)` / `create_client(ip, port)` |
| `WebSocketPeer` | Browser builds, proxied connections | No UDP; expect higher latency |
| `WebRTCMultiplayerPeer` | P2P with NAT traversal | Complex signaling server required |
| `OfflineMultiplayerPeer` | Single player | Keeps the same code path so MP code does not fork SP |

```gdscript
var peer := ENetMultiplayerPeer.new()
var err := peer.create_server(7777, 8)
if err == OK:
    multiplayer.multiplayer_peer = peer
```

`multiplayer.multiplayer_peer = null` tears down. Always handle `ENetMultiplayerPeer.CONNECTION_*` signals and `multiplayer.connected_to_server` / `connection_failed` / `server_disconnected`.

## Scene replication

  `MultiplayerSpawner` ╬ô├ç├╢ spawns/despawns nodes for peers (set `spawn_path`, `auto_spawn` list)
  `MultiplayerSynchronizer` ╬ô├ç├╢ replicates declared properties; configure **Replication** in the inspector
  Both are editor authored configuration ╬ô├ç├╢ set them up via the editor/MCP, never by hand editing `.tscn`

## Authority

```gdscript
$Player.set_multiplayer_authority(1)              # server owns it
$Player.set_multiplayer_authority(peer_id)         # hand ownership to a client
$Player.get_multiplayer_authority()
$Player.is_multiplayer_authority()                 # gate local input on this
```

Rule: **input is read only where authority lives; state is written only where authority lives.** A client that moves its own body without server approval is a bug, not an optimization.

## RPCs

```gdscript
@rpc("authority", "call_local", "reliable")
func fire_weapon(target: Vector3)  > void: ...

@rpc("any_peer", "call_local", "unreliable_ordered")
func submit_input(state: InputState)  > void:
    if not multiplayer.is_server(): return
    var sender := multiplayer.get_remote_sender_id()
    validate(sender, state)
```

  Direction flags: `"authority"` (only the authority may call), `"any_peer"` (any client ╬ô├ç├╢ **validate the sender server side, always**).
  Transport: `reliable` for discrete events, `unreliable_ordered` for continuous state, `unreliable` for per tick input where a lost packet is acceptable.
  `call_local` decides whether the caller also executes ╬ô├ç├╢ usually what you want for server broadcasts.
  RPCs only work on nodes present in the tree on both sides with matching paths. A path mismatch fails silently ╬ô├ç├╢ check it first when "nothing happens".

## Dedicated / headless server

```bash
godot   headless   path . res://scenes/server.tscn
```

Branch on `DisplayServer.is_headless()` to load server only scenes and skip rendering/audio work. Use `  fixed fps` and `  disable render loop` for deterministic simulation runs.

## Latency handling

  Interpolate remote entities on a 100╬ô├ç├┤200 ms buffer; never snap to the last received transform.
  Predict local movement immediately, reconcile on server correction ╬ô├ç├╢ send corrections as `unreliable_ordered`, not reliable.
  Separate **input** (high rate, lossy ok) from **events** (low rate, reliable) onto different RPCs and, on ENet, different channels.

## Testing without a human

1. **Headless client+server in one process:** instantiate both peers against `127.0.0.1` in a test scene; assert state convergence after N ticks.
2. **Two processes:** launch `godot   headless` twice from a script ╬ô├ç├╢ one server, one client ╬ô├ç├╢ and assert exit codes.
3. **Simulated latency/loss:** inject delay and packet drop in a wrapper peer for regression tests.
4. **Replay:** record the server's authoritative tick stream, replay it, and diff ╬ô├ç├╢ catches desync.

Required assertions before netcode work is done:

  Server rejects malformed/hostile input from any peer
  A late joining client converges to current state
  Disconnect/reconnect leaves no orphaned nodes
  No RPC fires on a path that does not exist on the other side

## Boundaries

  Never trust client data ╬ô├ç├╢ validate and clamp everything on the server.
  Never put secrets (tokens, keys) in RPC payloads or logs.
  Console/platform certification networking requirements: research them, record the requirements as data on the issue, and build to them. No human decision step exists.

