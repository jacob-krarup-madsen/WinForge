---
name: godot-art-design
description: Godot 4.7.2 art and design pack: glTF import pipeline and material/LOD budgets, headless Blender authoring and .glb export conventions, GDD/systems/balancing templates, dialogue and quest data with CSV localization, accessibility floor (rebinding, sliders, palettes, font scaling, pause) with headless verification. Use for art pipeline, Blender authoring, design docs, narrative, or accessibility work.
---

---

# From skill: art-asset-pipeline

# Godot 4 Art & Asset Pipeline

Target: **Godot 4**. This skill covers getting DCC content (Blender, Substance, Houdini, ZBrush) into the engine correctly and keeping it fast.

## glTF import (the default interchange format)

- Export from Blender as **glTF 2.0 `.glb`** (binary, single file) or `.gltf` + bin + textures. Prefer `.glb`.
- Blender export settings that matter:
  - ╬ô├┐├ª Selected Objects, ╬ô├┐├ª Apply Modifiers, ╬ô├┐├ª **Merge vertices / deformation deltas** as needed
  - ╬ô├┐├ª **Animation ╬ô├Ñ├å Always Sample Animations**, ╬ô├┐├ª NLA Tracks / explicit actions as required
  - ╬ô├┐├ë "Don't export via selection only" pitfalls ╬ô├ç├╢ clear the export path to `res://art/...`
  - Forward `-Z`, Up `Y` (glTF default ╬ô├ç├╢ do not change unless your DCC demands it)
  - Y-up conversion must be done in the exporter, never by rotating the object manually
- Godot's `gltf` importer then produces a `.tscn` scene + `.import` sidecars. Tune via **FileSystem dock ╬ô├Ñ├å Import tab ╬ô├Ñ├å Reimport**.
- **Blend shapes / morphs**: supported; enable `Blend Shapes` on the MeshInstance3D.
- **Skins**: single skeleton preferred. Multi-skeleton rigs cost draw calls and break animation retargeting.
- **KHR_materials_** extensions: `KHR_materials_emissive_strength`, `KHR_texture_transform`, `KHR_materials_transmission` map to Godot's `StandardMaterial3D`. `KHR_materials_specular` maps only partially ╬ô├ç├╢ verify in-engine.
- **Do not** import FBX in 4.x ╬ô├ç├╢ the FBX path is deprecated/removed in favour of glTF. Convert to glTF at source.

Reimport from CLI (no editor):

```powershell
godot --headless --import          # reimports everything changed
# or target one file by touching its .import then running --import
```

There is no per-file CLI reimport flag in 4.7.2 ╬ô├ç├╢ the reliable method is: modify the `.import`/source, run `godot --headless --import`.

## Textures: CompressedTexture2D / CompressedTexture3D

- Godot compresses to **VRAM (S3TC/BPTC/ETC2/ASTC)** by default for 3D, **Lossless/Lossy (PNG/WebP)** for 2D. The stored resource type is `CompressedTexture2D` / `CompressedTexture3D`.
- Per-texture settings (Import tab ╬ô├Ñ├å Compression):
  - `mode`: Lossy (2D default, smallest), Lossless, VRAM Compressed, Video RAM, Brotli (lossless 4.x)
  - `compress_to_1_bit` for masks/UI
  - `mipmaps/generate` ╬ô├ç├╢ **on for 3D and for scaled-down 2D**; off for pixel art at 1:1
  - `fix_alpha_border` / `filter` for 2D: pixel art ╬ô├Ñ├å `filter = Nearest`, `mipmaps off`
  - `normal_map` flag on normal textures (or use `map_to_3d` workflows)
  - sRGB: albedo/emissive = sRGB on; normal/ORM/metallic/roughness = **sRGB off**
- Sizes must be powers of two for VRAM compression on some targets; non-PoT falls back to software path on mobile.
- Import presets: set once under **Project Settings ╬ô├Ñ├å Import Defaults** so every new texture inherits them.

## Import presets

- **Project Settings ╬ô├Ñ├å Import Defaults** ╬ô├ç├╢ global defaults for each importer (texture, mesh, glTF, wav).
- Per-asset override lives in the `.import` file's `[params]` section ╬ô├ç├╢ edit via the Import tab and press **Reimport**, never by hand.
- Common preset families: `2d_pixel` (nearest, no mipmaps, lossless), `2d_ui` (lossless, no mipmaps), `3d_opaque` (VRAM, mipmaps, no alpha), `3d_normal` (VRAM, sRGB off, normal flag), `voxel/palette` (lossless, no filter).

## Materials & ORM

- **`ORMMaterial3D`** is the standard PBR path: one texture with **R = ambient occlusion, G = roughness, B = metallic**. One texture, one sampler, big win on mobile.
  - Blender: bake AO into the R channel, pack AO/Rough/Metal into one image (use a Map Range / Separate-Color chain), export as `.glb`.
  - In Godot: assign as `ORMTexture`, set `albedo_texture` separately, `uv1_scale`/`uv1_triplanar` for tiling.
- **`StandardMaterial3D`** for everything else; `ShaderMaterial` only when ORM/Standard cannot express the effect.
- Feature flags to enable explicitly: `heightmap_enabled`, `rim_enabled`, `clearcoat_enabled`, `backlight_enabled`, `uv1_animation_enabled`. Disabled features compile out ╬ô├ç├╢ keep them off unless used.
- Transparency: prefer `alpha_scissor`/`alpha_hash` over `alpha_blend` (depth-sort cost). Opaque materials must have `transparency = disabled`.
- **Mobile renderer**: check `Compatibility` vs `Forward+` feature support for your shader features (` Rendering Method` in Project Settings). Shaders written for Forward+ may silently degrade on Compatibility.
- Emissive on mobile: use `emission_energy_multiplier` sparingly; it multiplies fragment cost indirectly via HDR.

## VFX particles

- **GPUParticles2D / GPUParticles3D** for anything > ~50 particles; **CPUParticles** for tiny effects and mobile-critical paths.
- Always set `amount`, `lifetime`, `preprocess`, and an `emission_shape` (`EMISSION_SHAPE_SPHERE` etc.).
- Must have a **process material** (`ParticleProcessMaterial`) and a **draw pass / mesh** (3D) or texture (2D).
- Visibility: wrap in `VisibleOnScreenNotifier3D` so off-screen effects stop simulating; set `visibility_aabb` correctly or effects pop at screen edges.
- Trails: `Trail` tab on the particle (3D), or a second emitter with `local_coords = false`.
- Determinism for tests: keep `randomness` low and seed the emitter (`particle_material.seed`) so headless screenshots are stable.
- Draw budget: a particle system with a large mesh is one draw call per particle in some cases ╬ô├ç├╢ prefer simple quads/instanced meshes.

## LOD & mesh budgets

- **`MeshLOD` / `LOD levels`** on the Mesh resource: LOD0 (full), LOD1 (~50%), LOD2 (~10%) with screen-size thresholds (`lod_meshes` in import settings, or author LODs in DCC and import as `importer="scene"` with LOD suffixes).
- Godot 4 `MeshInstance3D` has `lod_bias` ╬ô├ç├╢ negative values show lower LODs sooner (for perf).
- **Budgets (starting point, tune per platform):**
  | Metric | PC (mid) | Mobile | Switch-class |
  |---|---|---|---|
  | Tris / frame | 1╬ô├ç├┤2 M | 150╬ô├ç├┤400 K | 400╬ô├ç├┤800 K |
  | Draw calls | 1000╬ô├ç├┤2000 | 150╬ô├ç├┤300 | 400╬ô├ç├┤700 |
  | Texture memory | 1╬ô├ç├┤2 GB | 200╬ô├ç├┤400 MB | 512 MB╬ô├ç├┤1 GB |
  | Bone count / skinned mesh | 100╬ô├ç├┤200 | ╬ô├½├▒ 100 | ╬ô├½├▒ 150 |
- Use **multi-mesh instances** (`MultiMeshInstance3D`) for repeated props/foliage ╬ô├ç├╢ one draw call for N instances.
- **LOD for foliage**: use `GeometryInstance3D` + `lod` and alpha-scissor cards, not real geometry.
- Measure with the built-in monitors: `Performance.get_monitor(Performance.TIME_FPS)`, `RENDER_TOTAL_DRAW_CALLS_IN_FRAME`, `RENDER_TOTAL_PRIMITIVES_IN_FRAME`, and the **Debugger ╬ô├Ñ├å Monitors** tab headlessly via a script.

## CLI reimport workflow

```powershell
# after replacing art/*.glb or changing .import params:
godot --headless --import
if ($LASTEXITCODE -ne 0) { throw "import failed" }

# verify a texture landed as expected by loading it in a script:
godot --headless --script tools/verify_assets.gd
```

`tools/verify_assets.gd` (run with `godot --headless --script`):

```gdscript
@tool
extends SceneTree
func _init() -> void:
    var tex := load("res://art/textures/hero_albedo.tex") as Texture2D
    print("tex=", tex, " size=", tex.get_size() if tex else "MISSING")
    quit(0 if tex else 1)
```

(Use the real imported path from the FileSystem dock; `.import`-generated paths are under `res://.godot/imported/` ╬ô├ç├╢ prefer referencing the source path, Godot resolves it.)

## Pipeline rules

1. DCC ╬ô├Ñ├å `.glb` ╬ô├Ñ├å `godot --headless --import`. Never hand-place texture files without an import pass.
2. Every new asset gets a preview in-engine before merge: open the scene/prefab, check materials, LOD, and draw-call impact.
3. Commit `.import` files and the asset sources; commit the generated `.godot/` **never**.
4. Reimport after moving files ╬ô├ç├╢ moving in the file system breaks uid references (see `godot-scene-tooling`).
5. Screenshot-diff the results when art changes are visual: baseline compare via `godot-qa` (see `godot-testing`).

---

# From skill: blender-asset-pipeline

# Skill: blender-asset-pipeline

Author game assets headlessly in Blender and land them in Godot 4 through the sanctioned import pipeline. This skill is the creation half; `art-asset-pipeline` remains the Godot import/budget half.

## Hard rules

- **Never open the Blender GUI in automation.** Every asset is produced by `blender --background --factory-startup --python <script>.py` (short: `-b`). A script that needs the GUI is the wrong script.
- **Never hand-edit `.tscn`, `.tres`, `.import`, or `project.godot`.** Export `.glb` into the repo; Godot import settings live in the Import tab / `.import` / CLI `--import` per `godot-scene-tooling`.
- **Determinism is mandatory.** Every generator script seeds `random` and Blender's RNG (`bpy.context.preferences.edit.use_mouse_held_autoadjust` N/A ╬ô├ç├╢ set seeds explicitly); re-running must produce a byte-identical `.glb` or the script is not done.

## Blender invocation

```
blender --background --factory-startup --python tools/gen_prop_barrel.py -- --out assets/props/prop_barrel.glb --seed 42
blender --background assets/props/prop_barrel.blend --python-expr "import bpy; bpy.ops.export_scene.gltf(filepath='assets/props/prop_barrel.glb', export_format='GLB', export_yup=True, export_apply=True, export_animations=True)"
```

- Pin the Blender major version in the issue ╬ô├ç├╢ installed here: **Blender 5.2.2 LTS** at `C:\Program Files\Blender Foundation\Blender 5.2\blender.exe` (on PATH as `blender`). Bare `blender` should resolve; if PATH lookup fails in a run's shell, use the full path. Record `blender --version` output in every delivery comment.
- Scripts exit non-zero on any assertion failure; CI treats exit code as the gate (same contract as Godot gates).

## Conventions (binding)

- **Scale/units:** 1 Blender unit = 1 meter. Apply all transforms before export (`export_apply=True`).
- **Origin:** props/characters at ground contact, origin between feet; meshes centered; no stray camera/light in the export collection.
- **Forward/up:** export Y-up glTF (default); Godot consumes it with its standard glTF importer. Never rotate scenes to "fix" orientation post-export ╬ô├ç├╢ fix the source.
- **Naming:** `snake_case`; prefixes `chr_`, `env_`, `prop_`, `wep_`, `vfx_`; LODs as `_lod0`╬ô├ç┬¬`_lod3` mesh variants inside one `.glb`.
- **Armatures:** one root bone (`root`), humanoid chain `hips╬ô├Ñ├åspine╬ô├Ñ├åchest╬ô├Ñ├åneck╬ô├Ñ├åhead`, limbs `upper_arm_l/r ╬ô├Ñ├å hand_l/r`; face bones only when authored. No control rigs in exports ╬ô├ç├╢ bind pose only.
- **Animations:** bake at 30 fps into NLA╬ô├Ñ├åactions; one action per clip (`idle`, `run`, `attack_a`, ╬ô├ç┬¬); export with `export_animations=True`. Root motion is off by default; flag it in the delivery comment when enabled.
- **Materials:** export PBR via KHR_materials_pbrMetallicRoughness so Godot's ORM path (R=AO, G=roughness, B=metallic) applies ╬ô├ç├╢ see `art-asset-pipeline`.

## Budgets (check before shipping, not after)

| asset class | tris (lod0) | texture set |
|---|---|---|
| hero character | ╬ô├½├▒ 60k | 2Γö£├╣ 2K ORM + normal |
| enemy | ╬ô├½├▒ 25k | 1Γö£├╣ 1K ORM + normal |
| prop (common) | ╬ô├½├▒ 3k | atlas or 512 |
| environment module | ╬ô├½├▒ 15k | 2K tile set |

Numbers come from the design report / GDD when it specifies them; these are defaults. Over budget ╬ô├Ñ├å decimate/remesh in the generator, never post-hoc in Godot.

## Verification chain (every asset)

1. Generator script exit 0; `blender --version` + command logged.
2. `.glb` loads headless: `blender -b <file>.glb --python-expr "import bpy; assert len(bpy.data.objects)>0"` exit 0.
3. Drop into the repo, then `godot --headless --import` exit 0 (`godot-headless-ci`).
4. Godot-side load check + tri counts reported (`art-asset-pipeline`).
5. If a visual baseline exists, regenerate screenshots only via `godot-qa` per `godot-testing` ╬ô├ç├╢ attach diffs, never silent overwrites.

## Delivery comment format

- `What changed` ╬ô├ç├╢ generator script path, `.glb` path, tri/texture counts.
- `Determinism` ╬ô├ç├╢ seed used, re-run byte-compare result.
- `Verification` ╬ô├ç├╢ the exact commands + exit codes from the chain above.
- `Artist notes` ╬ô├ç├╢ what a human DCC artist must know if they ever touch the source.

---

# From skill: game-design-docs

# Game Design Docs & Technical Design (Godot Studio)

This skill owns the **documents**: GDDs, technical designs, level/system briefs, balancing data, iteration notes. It does not own implementation ╬ô├ç├╢ hand that to the engineer agents.

## Where documents live

```
res://docs/
  gdd/            # game design documents
    00-vision.md
    01-core-loop.md
    02-systems/
    03-levels/
  tech/           # technical design documents
    architecture.md
    <feature>-tdd.md
  balancing/      # balancing tables as data files (.csv / .tres / .json)
  notes/          # iteration notes, playtest logs, decision records
  templates/      # copy these to start a new doc
```

Docs live in the repo so they version with the code. Long-lived decisions get an ADR entry in `notes/adr/NNNN-title.md`.

## GDD template

```markdown
# <Game Title> ╬ô├ç├╢ Game Design Document
Status: draft | review | approved | deprecated
Owner: <agent/person>
Last updated: YYYY-MM-DD

## 1. Vision
One paragraph: the game, the audience, the feeling, the 30-second pitch.
## 2. Core loop
The 30s / 5m / 30m loop as a sequence. What the player does, what they get, why they repeat it.
## 3. Pillars
3╬ô├ç├┤5 design pillars. Every later decision is checked against these.
## 4. Mechanics
Per mechanic: intent, rules, inputs, edge cases, dependencies, tuning knobs (with defaults).
## 5. Systems
Economy, progression, meta ╬ô├ç├╢ see system template below.
## 6. Level design
Pacing model, encounter grammar, per-level briefs.
## 7. Controls & UX
Input map actions (matches `project.godot`), screens, HUD elements, accessibility options.
## 8. Content scope
Quantities: N levels, M enemies, K items. This is the scope contract.
## 9. Monetisation / shipping (if applicable)
## 10. Open questions
Numbered, each with an owner and a due date.
```

## Technical design template

```markdown
# <Feature> ╬ô├ç├╢ Technical Design
Status / Owner / Date as above

## Problem
What must be true that is not true today.
## Context
Current code paths (file:line), constraints (Godot 4, mobile target, save-compat).
## Proposal
The design. Include node/scene structure, signals, data flow, state machine shape.
## Alternatives considered
╬ô├½├æ 2, with why rejected.
## Data model
Resources, custom Resource classes, save-game schema, migration path.
## Failure modes & rollback
What breaks, how we detect it, how we revert.
## Test plan
Which GUT/GdUnit tests, which godot-qa scenarios (name files).
## Rollout
Steps, feature flag if any, who reviews.
```

## Systems design

Write systems as **explicit rules with inputs and outputs**, not prose:

- **Economy**: sources (what grants currency, rate/hour), sinks (what consumes it, rate), inflation controls, caps. A table beats a paragraph.
- **Progression**: XP curve as a formula or data table, unlock order, soft/hard gates.
- **Combat/abilities**: per-ability spec ╬ô├ç├╢ cost, cooldown, damage formula, status effects, counters, frame data if relevant.
- **AI behaviour**: behaviour states, perception rules, aggression model, difficulty scaling hooks.

Every system doc ends with **"tuning knobs"** ╬ô├ç├╢ the exported vars / data rows a designer can change without code (`@export` names, data file keys).

## Level design

- Brief per level: goal, intended skill, pacing curve (tension graph as a mermaid chart), encounter list, secrets, checkpoint policy.
- Represent pacing as data where possible (a `.tres`/`.csv` timeline), so tooling can validate it.
- Reference the actual scene path (`res://scenes/levels/level_03.tscn`) and the layout blocks used.

## Balancing tables as data files

Balancing numbers must **never** live only in prose. Author them as data:

- `.csv` for spreadsheet-friendly iteration (`res://balancing/weapons.csv`): `id,name,damage,cooldown,crit_chance,...`
- `.tres` for designer-tunable Resources (`res://balancing/hero_stats.tres` ╬ô├ç├╢ a `Resource` with `@export` fields), editable in the inspector without touching code.
- Load in code: `load("res://balancing/weapons.csv")` ╬ô├Ñ├å parse with `FileAccess` + `CSV` semantics, or a small `@tool` script that converts CSV ╬ô├Ñ├å `.tres` on import.

```gdscript
static func load_table(path: String) -> Array[Dictionary]:
    var out: Array[Dictionary] = []
    var f := FileAccess.open(path, FileAccess.READ)
    if f == null: push_error("missing " + path); return out
    var header := f.get_csv_line()
    while not f.eof_reached():
        var row := f.get_csv_line()
        if row.size() < header.size(): continue
        var d := {}
        for i in header.size(): d[header[i]] = row[i]
        out.append(d)
    return out
```

Rules: one row per entity; ids are stable `StringName` keys; never reorder/delete columns without a migration note; version the file in git so balance changes are reviewable diffs.

## Iteration notes

After every playtest or balance pass, append to `docs/notes/YYYY-MM-DD-<topic>.md`:

- What we changed and why (one line per change, referencing the data row or doc section)
- What we observed (evidence: playtest session, metric, bug id)
- What we will try next, with an owner
- Decisions recorded (and superseded ADRs linked)

These notes are the memory that keeps design decisions from being re-litigated. When a decision reverses a prior one, add an ADR rather than editing history.

## Process

1. Proposal in a doc (draft) ╬ô├Ñ├å review comment on the issue ╬ô├Ñ├å approved.
2. Approved GDD/TDD is the contract for the engineer's sub-issue; link it in the issue description.
3. Implementation changes that invalidate a doc section ╬ô├Ñ├å update the doc in the same PR, or open a follow-up issue.
4. Status field must always be current: `draft`, `review`, `approved`, `deprecated`.

## Quality bar

- Every number has a source (data file row, playtest, or "assumption ╬ô├ç├╢ validate by <date>").
- No vague verbs ("feel better", "more fun") without a measurable proxy.
- Diagrams inline as mermaid (`flowchart`, `gantt`, `stateDiagram-v2`) ╬ô├ç├╢ they render in the issue/doc surface.
- Keep GDDs under ~15 pages; split into `02-systems/<name>.md` files rather than growing one document.

---

# From skill: narrative-design

# narrative design

Narrative and localization for **Godot 4**, authored as data so writers can work without an engineer and agents can edit without breaking binary formats.

## Core rule: narrative is data, not code

Dialogue, quests, branching conditions, and string tables live in **CSV and JSON** ╬ô├ç├╢ text formats that merge cleanly in git and are safe for anyone (or any agent) to edit. GDScript interprets them at runtime.

Never author narrative by hand editing `.tres` resources ╬ô├ç├╢ those break silently when written as raw text. If a Resource is genuinely needed, generate it from CSV/JSON with an editor script.

## File layout

```
res://data/narrative/dialogue/    chapter01.json      branching graph
res://data/narrative/quests/      quests.csv          quest definitions
res://data/narrative/items/       narrative_items.csv dialogue linked items
res://locale/                     strings.csv         localization table
res://src/narrative/              dialogue_runner.gd  interpreter
```

## Dialogue graph format (JSON)

```json
{
  "id": "ch01_greeting",
  "speaker": "captain",
  "text_key": "dialogue.ch01.greeting",
  "choices": [
    {"text_key": "dialogue.ch01.greeting.opt_a", "next": "ch01_accept", "set_flag": "accepted"},
    {"text_key": "dialogue.ch01.greeting.opt_b", "next": "ch01_refuse"}
  ],
  "conditions": [{"flag": "met_captain", "equals": true}],
  "next": "ch01_end"
}
```

  `text_key` is always a localization key, never literal prose. Literal prose in the graph means the string cannot be translated.
  Conditions reference **flags/variables only** ╬ô├ç├╢ no code snippets in data files.
  Validate on load: every `next` target must exist; every `text_key` must resolve. Fail loudly in debug builds.

## Quest tables (CSV)

Columns: `id, title_key, description_key, giver, objectives, reward, prerequisites, repeatable`

Objectives are a semicolon separated list of `type:target:count` tokens (`kill:drone:5;reach:hangar`). The runtime parses tokens; designers never write scripts.

## Localization

Godot imports CSV directly as translations. First row is the key column, remaining columns are locales:

```csv
id,en,de,ja
dialogue.ch01.greeting,"Welcome aboard.","Willkommen an Bord.","╧Ç├⌐├¬╧Ç├╝├Ñ╧Ç├╝├┤╧Ç├╝┬Ñ╧Ç├ç├⌐"
```

Register it in **Project Settings > Localization > Translations** (this edits `project.godot` ╬ô├ç├╢ an editor or MCP operation, not a raw text edit).

Runtime:

```gdscript
tr("dialogue.ch01.greeting")
TranslationServer.set_locale("de")
TranslationServer.get_locale()
```

Key convention: `domain.chapter.line` ╬ô├ç├╢ lowercase dot separated, stable forever. **Renaming a key breaks every locale**; if a rename is unavoidable, update every language column in the same commit and say so in the comment.

## VO and subtitles

  Subtitle timing: author as `.srt` or a CSV of `key,start_ms,end_ms`; convert with a tool script rather than embedding times in code.
  VO files follow `vo_<character>_<line_key>.ogg`.
  Missing VO must degrade to text, never to a blank line ╬ô├ç├╢ the runner checks `ResourceLoader.exists()`.

## Testing (headless)

```bash
godot   headless   check only   script res://tests/test_narrative.gd
godot   headless   path .   quit after 120
```

Required assertions before narrative work is done:

1. Every dialogue `next` target resolves; no orphan nodes
2. Every `text_key` exists in **every** locale column ╬ô├ç├╢ report missing/extra counts
3. No literal prose in any `text_key` field of a data file
4. All quest prerequisites reference real quest ids
5. No empty strings in the source locale

## Boundaries

  Prose quality and story direction are judgment calls ╬ô├ç├╢ propose, don't unilaterally rewrite established canon.
  Never machine translate and commit as final; mark machine-produced strings `needs_review` and rewrite them properly. No human review step exists.
  Never hand edit `.tres` or `project.godot`.

---

# From skill: a11y-floor

## Scope (report section 17 ΓÇö accessibility floor)

The minimum accessibility set from the binding design report. Every item is a hard floor, not a nice-to-have; none of them may be deferred past the phase that needs them.

## The floor (report section 17)

- Full input rebinding
- Keyboard, mouse, and controller support
- Auto-aim strength slider
- Manual-aim option
- Aim-assist cone settings
- Damage number toggle
- Screen shake slider
- Flash intensity slider
- Projectile outline settings
- Enemy outline settings
- Color-blind palettes
- High-contrast pickups
- Game-speed modifier
- Enemy health and damage sliders
- Automatic fire
- Hold/toggle options for every hold-based input
- Font scaling
- Pause at any time in solo mode

## Rules

- Difficulty assistance must not disable unlocks unless a specific achievement explicitly requires standard settings (report section 17).
- Every setting lives in an options resource as data ΓÇö no hard-coded toggles ΓÇö so the options UI is generated from the same data the game reads (report section 18, data-driven definitions).
- Settings persist across sessions and are part of the save schema from the moment they exist.
- Each accessibility feature ships with a GUT test or headless assertion proving it changes runtime behaviour (e.g. screen-shake slider at 0 actually zeroes the shake amplitude).

## Verification

- The `godot-testing` and `qa-release` skills define the test harness.
- A scenario pass per palette (deuteranopia, protanopia, tritanopia simulation via shader or texture override) checks pickup and enemy legibility with screenshots attached.

