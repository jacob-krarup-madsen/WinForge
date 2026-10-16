# Andromeda — process-overhead thresholds (audit item 16)

Diagnostic thresholds, not hard stop rules. Use the next 10 real issues to
detect excessive workflow burden. If warnings fire regularly, fix routing —
do not add roles, labels, layers, or audit steps.

## Tier 1 warning (routine, non-player-facing)

Investigate if a Tier 1 issue regularly requires:

- More than 1 author + 1 auditor
- More than 1 handoff
- More than 30–60 minutes of agent runtime
- Player-facing evidence unrelated to the task (screenshots, PDF, QA package)

Expected Tier 1 shape: one author, one auditor, no Council, no full Studio
Crew, no screenshots, no PDF, no external QA package, quick completion,
correct status transitions. Example probe: typo fix in an internal dev note.

## Ordinary Tier 2 warning (reversible player-facing)

Investigate if a normal Tier 2 issue regularly requires:

- More than 3 implementation agents
- More than 1 auditor
- Full-Council participation
- Tier 3 documentation
- A formal external-QA PDF

Expected Tier 2 shape: measurable acceptance criteria, one primary
implementer plus specialist only if required, relevant automated or scenario
test, raw evidence (annotated only if useful), independent auditor, safe
merge if no protected condition applies.

## 10-issue log (fill in Multica issue keys)

| # | Issue key + title | Tier | Authors (count + names) | Auditors | Handoffs | Agent runtime | Evidence attached | Warning? | Notes |
|---|---|---|---|---|---|---|---|---|---|
| 1 | | | | | | | | | |
| 2 | | | | | | | | | |
| 3 | | | | | | | | | |
| 4 | | | | | | | | | |
| 5 | | | | | | | | | |
| 6 | | | | | | | | | |
| 7 | | | | | | | | | |
| 8 | | | | | | | | | |
| 9 | | | | | | | | | |
| 10 | | | | | | | | | |

## Decision rule

- 0–1 warnings in 10 issues: setup is correctly sized, freeze it.
- 2–3 warnings: inspect the flagged issues for over-routing (full-squad
  fan-out, duplicate implementation, conditional specialists dispatched
  without need) before changing anything.
- 4+ warnings: routing or tiering is wrong — fix squad instructions and
  delivery-tier selection. Do not add Council members, Crew members,
  labels, decision layers, or audit roles.
