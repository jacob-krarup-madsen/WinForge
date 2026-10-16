---
name: marketing-storefront
description: Store copy, capsule/trailer briefs, press kit drafts under the IP gate: placeholder naming until counsel clearance, internal-only, zero public actions.
---

# Skill: marketing-storefront

Own everything a player sees before they buy: store page copy, capsule briefs, trailer outlines, social and press materials. All of it is **drafted internally**; nothing publishes without Svinet's explicit action.

## IP GATE (read first, every run)

ADR 0006 adopted the working commercial title, but `docs/design/design-report.md` ┬º14 forbids the title appearing in any public surface until EU games/IP counsel clears it (gate issue: ANDR-156; domains/handles: ANDR-163). Therefore:

1. **Every draft uses the placeholder `Γƒ¿TITLEΓƒ⌐`** until ANDR-156 is `done` AND written clearance is posted there. Check the gate's live status every run ΓÇö never assume.
2. **Zero public actions, ever.** No store page submission, no social post, no press email. This skill produces internal files and drafts only. Publishing is Svinet's call on his accounts.
3. When clearance lands, a fold-in pass replaces `Γƒ¿TITLEΓƒ⌐` everywhere and records the cleared-name citation.

## Store page copy (primary deliverable)

- **Short description (Γëñ 300 chars):** hook from the design report's pillars ΓÇö one sentence of fantasy, one of loop, one of stakes.
- **Long description:** feature bullets (each maps to a documented system, no invented features), 2ΓÇô3GIF/screenshot callouts, a "why this roguelite" paragraph grounded in the Keep/Remove/Add summary (┬º24).
- **Tags & genre** from the GDD; never claim tags the game does not deliver (Steam punishes tag mismatch with visibility loss).
- Every claim must trace to a design doc section ΓÇö cite it in the draft's margin comments.

## Capsule & trailer briefs

- Capsule briefs go to Technical Artist / Technical Artist-animator pipeline: mood refs from the design report's art direction (ANDR-17 owns final art direction ΓÇö do not contradict landed captures), must read at 460├ù215 thumbnail size.
- Trailer outline: 5-beat structure (hook ΓåÆ loop ΓåÆ escalation ΓåÆ boss/identity beat ΓåÆ title card), shot list mapped to achievable in-engine captures (`godot-qa` scenarios can generate most shots headlessly).

## Social & press (internal drafts only)

- Press kit folder: Γƒ¿TITLEΓƒ⌐ one-liner, 5 bullets, 3 screenshots, logo specs ΓÇö assembled in-repo under `docs/press/`, never uploaded.
- Social calendar as a CSV (date, platform, draft copy) ΓÇö Svinet posts manually or later grants a channel.

## Tone & honesty rules

- Never overclaim: every advertised mechanic must exist in `main` or be visibly in-development-marked.
- The design report's IP guardrails (┬º14) apply to every name and description this skill produces.
- Competitor comparison claims are forbidden (legal risk).

## Verification chain

1. `git grep` for the placeholder discipline: no real title strings in `docs/press/` drafts pre-clearance.
2. Every feature bullet maps to a GDD/report section ΓÇö table in the draft.
3. Capsule brief passes the 460├ù215 readability test (thumbnail mock attached).

## Delivery comment format

- `Draft` ΓÇö file paths (repo-relative).
- `Claims audit` ΓÇö bullet ΓåÆ design-doc citation table.
- `Gate status` ΓÇö ANDR-156 state read this run.
- `Blocked on` ΓÇö any HUMAN action (posting, submitting) with what Svinet needs to do.

