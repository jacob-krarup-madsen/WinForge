---
name: godot-scene-tooling
description: Godot 4.7.2 scene and resource mutation via MCP tooling, gda and the Godot CLI. The never-hand-edit-.tscn rule, UID handling, resource authoring, --check-only validation.
---

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

