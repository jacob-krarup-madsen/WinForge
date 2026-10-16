---
name: live-ops-telemetry
description: Post-launch observability: crash reporting, consent-gated telemetry, staged patch train, player feedback triage into issues.
---

# Skill: live-ops-telemetry

Keep the game observable after launch: crash reporting, opt-in telemetry, the patch train, and the player-feedback intake loop. Extends the release train (`qa-release`) rather than replacing it.

## Crash reporting

- **Self-hosted Sentry** is the default target (free tier / self-host; HUMAN creates the DSN account). Wire the Godot client via a small GDScript breadcrumb handler + `SentryStoreMinidump`-style native handler on desktop exports; document the integration in `docs/live-ops/crash-setup.md`.
- **Headless-safety:** crash handler must be compile-time optional (`OS.has_feature("editor")` / custom feature flag) so CI exports never phone home.
- Every crash event carries: build version, platform, scene path, last 20 log lines. **Never** PII, never auth material.

## Telemetry (opt-in only)

- Event schema in `docs/live-ops/telemetry-events.md`: stable event names (`run_started`, `run_ended`, `death_cause`, `shop_purchase`, `boss_encounter`), typed properties, and a `schema_version`.
- Consent: telemetry off by default; a settings toggle (data-driven, per `a11y-floor` settings patterns) gates all emission. No event fires without consent ΓÇö enforce in one chokepoint autoload.
- Headless test: a GUT test asserts zero events emitted with consent off, expected events with consent on (simulated).

## Patch train

- **Versioning:** semver-ish `MAJOR.MINOR.PATCH` aligned to GDD milestones; build number ties to CI run id.
- **Hotfix criteria:** S1 crash affecting >5% sessions, progression blockers, security issues ΓÇö ship within the freeze exception path of `qa-release`.
- **Rollout:** staged store rollout (10 ΓåÆ 50 ΓåÆ 100%) for non-hotfix patches; rollback plan = previous build kept on a store branch, documented before rollout starts.
- Every patch ships with player-facing notes assembled from PR titles (the release train already drafts these).

## Player feedback intake

- Sources: store reviews, community channels (post-clearance per marketing gate), playtest surveys. The agent **triages into issues** (one bug per issue, per `qa-release` format); it never posts publicly.
- Feedback batches become a weekly triage issue assigned to QA & Release Lead with severity proposals and counts.

## Privacy & compliance baseline

- Telemetry policy text draft for the store page (`docs/live-ops/privacy-draft.md`) ΓÇö what is collected, what is not, retention. Svinet's legal review is HUMAN.
- No cookie banners needed for a desktop premium title with opt-in telemetry; state that explicitly in the draft.

## Verification chain

1. GUT consent tests green (events off/on).
2. `godot --headless --import` + export clean with crash handler feature-flagged off.
3. Schema doc and events code agree ΓÇö a test enumerates emitted event names against the schema.

## Delivery comment format

- `Shipped` ΓÇö events/handlers/docs paths.
- `Consent proof` ΓÇö test names + exit codes.
- `HUMAN items` ΓÇö account/DSN/legal steps Svinet owns.
- `Patch train state` ΓÇö current version, rollout stage, rollback build ref.

