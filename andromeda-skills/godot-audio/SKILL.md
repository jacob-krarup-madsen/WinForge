---
name: godot-audio
description: Godot 4.7.2 audio: import formats, bus layout, playback nodes, procedural SFX, headless CI verification.
---

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

