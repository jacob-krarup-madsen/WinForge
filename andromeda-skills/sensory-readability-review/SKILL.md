---
name: sensory-readability-review
description: Shared sensory-readability synthesis for Council reviews. Six-level visual hierarchy, audio masking and priority, HUD legibility and handheld readability. Used by Sakurai to integrate Jen Zee, Darren Korb, and gameplay findings into one recommendation.
---

# Sensory Readability Review

Shared skill for the Game Experience Council. It does not replace
`a11y-floor` (minimum settings floor), `godot-audio` (mix implementation),
or the delivery-protocol visual lenses (Lens A/B scoring). It gives Sakurai
a common language to synthesize visual, audio, and gameplay readability
findings into one integrated recommendation.

Sakurai is the primary consumer. Jen Zee (visuals) and Darren Korb (audio)
produce domain findings; Sakurai integrates them with gameplay, UX, and tech
input. Studio Crew implementers do not take this skill directly.

## Six-level visual hierarchy (authoritative order)

1. Lethal threats and telegraphs
2. Player position and state
3. Active objectives and pickups
4. Enemy intent (non-lethal)
5. Ambient effects and decoration
6. Background and UI chrome

Rule: a lower level must never obscure or out-compete a higher level.
An effect-density or contrast failure that hides a hitbox or telegraph is a
hierarchy violation, not a taste issue. Cite the two levels in conflict.

## Visual readability checks (Jen Zee input)

For each supplied capture (normal, projectile-heavy, 1280x800,
reduced-effects, clip, annotated reference), report only what is visible:

- Player location and how it is identified (shape, outline, motion)
- Immediate lethal threat and its telegraph lead time
- Elite telegraph distinctness from normal enemies
- Pickups or objectives and their glanceability
- Effects obscuring active hitboxes (name the effect and the frames)
- Color-only communication with no shape, texture, or label backup
- Handheld and small-window readability at native resolution
- Hierarchy violations by level number (for example: level 5 over level 1)

Generic art-direction advice without a specific frame, coordinate, or asset
reference is not a finding. If the model cannot inspect images, require the
fallback package before scoring: contact sheet, annotated screenshots,
color-vision simulations (deuteranopia, protanopia, tritanopia), contrast
measurements, effect-density comparisons, player-position overlays, hazard
masks, short written event timeline.

## Audio readability checks (Darren Korb input)

For each supplied cue (gameplay video with sound, weapon and impact stems,
enemy-warning stems, loudness measurements, spectrograms, voice-count
timeline, bus-routing snapshot, animation and impact timestamps), report
only what is grounded in the evidence:

- Masked warnings (which warning, masked by what, at which timestamp)
- Priority violations (low-priority cue louder or longer than a warning)
- Transient-timing problems (impact transient late or smeared vs animation)
- Audio and animation desynchronization (milliseconds and direction)
- Repetition fatigue (which cue, repetition rate, variation state)
- Excessive polyphony (voice count at peak, bus, limiter behavior)
- Music masking gameplay-critical cues (frequency range and sidechain state)
- Unclear perceived power (weak-feeling but competitive damage is the
  canonical case: do not assume damage values are wrong, investigate
  audiovisual feedback first)

If direct audio analysis is unavailable, require the derived-evidence
package for every audio review: loudness table, spectrogram images,
voice-count timeline, bus routing, timestamped impact list. No PASS on
audio readability without one of the two evidence paths.

## Sakurai synthesis rules

1. Route first: involve only relevant specialists with a specific domain
   question each. Exclude run-pacing, enemy-response, input-latency, and
   performance specialists unless evidence shows that dependency.
2. Specialists advise; they do not modify the repository.
3. Wait for required responses before synthesizing.
4. Distinguish in the synthesis: findings (observed), hypotheses (to test),
   preferences (taste), uncertainties (UNKNOWN with what would resolve them).
5. Return one integrated recommendation plus one bounded validation test
   (scene, seed, capture mode, expected vs actual, PASS threshold).
6. No implementation brief until the human decision is Accepted or Modified.
   The brief must not silently include rejected Council suggestions.

## Verification

- Visual PASS requires specific frame or asset citations, not general advice.
- Audio PASS requires timestamped or measured properties, not general mix notes.
- Cross-disciplinary PASS requires the hierarchy check: no level-5-over-1
  violation and no masked level-1 warning remain unaddressed or explicitly
  accepted with rationale.
- Attach raw plus annotated captures per `delivery-protocol`; the auditor
  re-captures independently.
