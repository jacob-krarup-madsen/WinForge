---
name: a11y-floor
description: Accessibility floor from the design report section 17: input rebinding, sliders, palettes, hold/toggle, font scaling, pause ΓÇö as data-driven settings with headless verification.
---

## Scope (report section 17 ΓÇö accessibility floor)

The minimum accessibility set from the binding design report. Every item is a hard floor, not a nice-to-have; none of them may be deferred past the phase that needs them.

## The floor (report section 17)

- Full input rebinding
- Keyboard, mouse, and controller support
- Auto-aim strength slider
- Manual-aim option
- Aim-assist cone settings
- Damage number toggle
- Screen shake slider
- Flash intensity slider
- Projectile outline settings
- Enemy outline settings
- Color-blind palettes
- High-contrast pickups
- Game-speed modifier
- Enemy health and damage sliders
- Automatic fire
- Hold/toggle options for every hold-based input
- Font scaling
- Pause at any time in solo mode

## Rules

- Difficulty assistance must not disable unlocks unless a specific achievement explicitly requires standard settings (report section 17).
- Every setting lives in an options resource as data ΓÇö no hard-coded toggles ΓÇö so the options UI is generated from the same data the game reads (report section 18, data-driven definitions).
- Settings persist across sessions and are part of the save schema from the moment they exist.
- Each accessibility feature ships with a GUT test or headless assertion proving it changes runtime behaviour (e.g. screen-shake slider at 0 actually zeroes the shake amplitude).

## Verification

- The `godot-testing` and `qa-release` skills define the test harness.
- A scenario pass per palette (deuteranopia, protanopia, tritanopia simulation via shader or texture override) checks pickup and enemy legibility with screenshots attached.

