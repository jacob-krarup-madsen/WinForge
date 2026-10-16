---
name: game-design-docs
description: GDD and technical design templates, systems/economy/level design, balancing tables as data files, iteration notes and decision records.
---

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

