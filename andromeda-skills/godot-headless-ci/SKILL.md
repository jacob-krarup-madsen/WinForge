---
name: godot-headless-ci
description: Godot 4.7.2 headless CI: --headless --import, --check-only, --quit-after, --export-release/--export-debug, export_presets.cfg, export template install, and CI gate wiring.
---

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

