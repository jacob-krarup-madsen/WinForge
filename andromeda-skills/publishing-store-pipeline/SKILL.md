---
name: publishing-store-pipeline
description: Store submission pipeline: Steam depots, store asset specs, ratings paperwork assembly, signing mechanics, human-ledger of member-only actions.
---

# Skill: publishing-store-pipeline

Take a Godot 4 build from green CI to a store-ready submission package. This skill owns store pipelines, build upload mechanics, ratings paperwork assembly, and signing mechanics. QA & Release Lead keeps the release train and cert checklists (`qa-release`); this skill is the storefront plumbing underneath them.

## The human ledger (never attempt these ╬ô├ç├╢ prepare only)

These require Svinet's accounts, money, or legal identity. The agent prepares artifacts and exact click-paths; Svinet executes. Record each item as `HUMAN: <what> ╬ô├ç├╢ prepared by <issue>, executed by member`.

- Platform account creation (Steamworks partner, Epic, console portals), banking, tax forms
- Fees: ratings board submissions, platform fees, SDK access
- NDAs, devkit registration, store agreement signatures
- Credential storage ╬ô├ç├╢ never write tokens/keys into issues, comments, or logs; reference by name

## Steam (primary target)

- **Pipeline:** Steamworks SDK app ╬ô├Ñ├å `steamcmd +login <builder> +run_app_build ../scripts/app_build.vdf` ╬ô├ç├╢ the `.vdf` build script lives in `ci/steam/` in the repo, branch-targeted (`branch: "playtest"` for pre-release, `default` for live).
- **Depots:** one depot per platform; `ContentBuilder` folders mirror `build/` outputs from `godot.yml` (Linux x86_64, Windows exe+pck). Include `steam_appid.txt`.
- **Rollout:** staged rollout percentages for patches; never 100% on day-one of a big patch without a rollback plan (`live-ops-telemetry`).
- **Playtest branches** are the pre-release gate ╬ô├ç├╢ closed beta keys distributed manually by the member.

## Store assets (assembly is agent work)

| asset | spec |
|---|---|
| capsule (main) | 460Γö£├╣215 PNG |
| capsule (small/icon) | 123Γö£├╣123 |
| library hero | 3840Γö£├╣1240 |
| screenshots | ╬ô├½├æ 1920Γö£├╣1080, 4╬ô├ç├┤8 required, real gameplay |
| trailer | 720p/1080p mp4 (H.264), ╬ô├½├▒ 3 min for the main slot |

Capsule art production is **Technical Artist / Technical Animator** work (briefs authored by Marketing per `marketing-storefront`); this skill only validates specs and assembles the submission folder.

## Ratings

- **IARC** questionnaire (covers PEGI/ESRB/USK-class territories for digital-only): assemble answers from the design report's content descriptors (violence level, UGC, gambling-adjacent mechanics ╬ô├ç├╢ the roguelite's meta-progression must be described honestly). Draft lives in `docs/publishing/ratings-iarc.md`; Svinet submits and pays.
- Never claim a rating the board has not issued; store pages must show the real badge only.

## Signing

- **Windows:** `signtool sign /fd SHA256 /td SHA256 /tr http://timestamp.digicert.com` with the member-provided cert (path referenced, never the password).
- **macOS:** codesign + notarization (`notarytool`) ╬ô├ç├╢ requires Apple Developer account (HUMAN).
- **Linux:** unsigned; ship `.x86_64` + `.pck` per `godot-headless-ci`.

## Verification chain

1. Export artifacts exist per platform (CI green: `build`, `build-windows`).
2. `steamcmd` validate script: `+list_depots <appid>` exit 0 (dry run, no upload).
3. Store asset folder passes spec checks (sizes, formats) ╬ô├ç├╢ scripted in `tools/validate_store_assets.py`.
4. Ratings draft has an answer for every IARC question ╬ô├ç├╢ checklist in the issue.

## Delivery comment format

- `Package` ╬ô├ç├╢ artifact paths + sizes + checksums.
- `Store readiness` ╬ô├ç├╢ checklist with `[x]`/`[ ]` per asset/spec.
- `HUMAN items` ╬ô├ç├╢ exactly what Svinet must click/pay/sign, with links.
- `Verification` ╬ô├ç├╢ commands + exit codes.

