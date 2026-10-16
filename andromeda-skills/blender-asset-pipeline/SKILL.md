---
name: blender-asset-pipeline
description: Headless Blender asset authoring for Godot 4.7.2: generator scripts, .glb export conventions, determinism and budget gates.
---

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

