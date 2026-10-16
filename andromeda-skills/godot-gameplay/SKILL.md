---
name: godot-gameplay
description: Godot 4.7.2 gameplay programming: GDScript conventions, nodes/scenes/signals, _physics_process vs _process, input map, CharacterBody2D/3D movement, state machines, GUT-testable design.
---

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

