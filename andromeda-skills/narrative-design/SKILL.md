---
name: narrative-design
description: Godot 4.7.2 narrative: dialogue graphs and quests as data, CSV localization via TranslationServer, headless validation.
---

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

