---
name: art-asset-pipeline
description: Godot 4.7.2 art pipeline: glTF import, CompressedTexture2D/3D, import presets, ORM materials, VFX particles, LOD and mesh budgets, CLI reimport.
---

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

