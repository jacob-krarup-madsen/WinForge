---
name: localization-lqa
description: Localization pipeline at volume: source freeze, machine drafts flagged needs_review, LQA checklist, headless loc_check ship gates.
---

# Skill: localization-lqa

Run localization as an industrial pipeline: source keys ΓåÆ machine draft ΓåÆ review flags ΓåÆ LQA ΓåÆ ship gate. Extends `narrative-design` (which owns key discipline and file formats); this skill owns volume, verification, and quality gates.

## Pipeline stages

1. **Source freeze.** Narrative Designer marks a loc milestone (`strings.csv` column `en` complete for a scope). Only then does translation start ΓÇö translating moving strings is waste.
2. **Machine draft.** Per-locale columns filled by translation (AI or human translators ΓÇö the agent runs the tooling; human review is scheduled by Svinet when quality bar demands). Every machine-produced cell is flagged `needs_review` in a parallel status column or via a ` REVIEW` suffix convention the team agrees ΓÇö never silently trusted.
3. **Review pass.** Glossary enforcement: game terms (ability names, faction names, item names) use the approved glossary per locale ΓÇö `docs/loc/glossary.csv`; deviations are corrections, not suggestions.
4. **LQA.** Structured pass per locale (checklist below), run headlessly where possible plus a human smoke test for the ship locales.
5. **Ship gate.** A locale ships only when: zero missing keys, zero untranslated `needs_review` cells, LQA checklist green. Partial locales fall back to `en` via TextServer fallback ΓÇö acceptable for early access, documented per release.

## LQA checklist (per locale)

- [ ] **Completeness:** every key in `en` exists in the locale column (script-enumerated count).
- [ ] **No placeholders lost:** `%s`, `{damage}`, `[img]` tokens survive translation (regex compare against source).
- [ ] **Expansion:** pseudo-loc (`en-XA`: accented, +30% length) run ΓÇö no truncated labels, no overlap in key UI scenes (`godot-qa` screenshot compare).
- [ ] **Fonts/glyphs:** the locale's script renders with the shipped font subset; missing-glyph boxes = blocker.
- [ ] **Bidi/RTL** (if applicable): layout mirrored, no embedded-LTR leaks in numbers.
- [ ] **Voice/subtitles:** if VO exists for the locale, subtitle timing keys align (per `narrative-design` conventions).

## Headless verification (every loc change)

```
godot --headless --import
godot --headless -s res://tools/loc_check.gd -- --locale all --strict
```

`loc_check.gd` (owned by Engine & Tools when tooling-shaped work is needed) asserts: key completeness per locale, placeholder integrity, glossary hits, and emits JUnit XML into `build/test/` like GUT does. Exit code is the gate ΓÇö never report a green loc run without the exit code.

## File & key discipline (inherited, restated)

- `res://locale/strings.csv` is the single source; stable lowercase dot-separated keys; renames update every locale column in the same change (per `narrative-design`).
- Never machine-translate and present as final: `needs_review` is a state, not a judgment call.

## Delivery comment format

- `Locales touched` ΓÇö column counts before/after, missing/extra deltas.
- `LQA results` ΓÇö checklist table per locale, with the headless run's exit code.
- `Ship gate` ΓÇö per-locale verdict: SHIP / FALLBACK-TO-EN / BLOCKED + reason.
- `Review queue` ΓÇö count of `needs_review` cells handed to human review, with the export file path.

