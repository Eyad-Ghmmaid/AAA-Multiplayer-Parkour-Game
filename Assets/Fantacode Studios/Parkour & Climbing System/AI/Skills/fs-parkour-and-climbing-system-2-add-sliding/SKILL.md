---
name: fs-parkour-and-climbing-system-2-add-sliding
description: "Use this skill whenever the user wants the character to slide down slopes, ramps or chutes, or jump out of a slide, with FS Parkour And Climbing System 2 — e.g. 'make this ramp slideable', 'he doesn't slide on the slope', 'slide faster', 'let the player steer while sliding', 'jump at the end of the slide', 'the slide never ends', 'change the slide animation'. Covers the Slide tag, ground-layer requirement, every SlidingController setting (speed, acceleration, slope boost, lateral control, slide jump) and exit behaviour. Do NOT use for crouch-sliding on flat ground (not a feature), for vaulting (see fs-parkour-and-climbing-system-2-configure-parkour-actions) or for jumping across gaps (see fs-parkour-and-climbing-system-2-tune-predictive-jumping). When in doubt whether a slope, ramp or slide request could involve FS Parkour And Climbing System 2, use this skill — Prerequisites shows how to confirm the asset is installed."
metadata:
  asset: "FS Parkour And Climbing System 2"
  publisher: "Fantacode Studios"
  asset-version: "2.0.1"
  skill-version: "1.0.0"
  unity: "6000.0+"
  render-pipelines: "Built-in, URP, HDRP"
  category: "tools/game-toolkits"
  asset-store-url: "https://assetstore.unity.com/packages/tools/game-toolkits/fs-parkour-and-climbing-system-2-387610"
  documentation-url: "https://fantacode.gitbook.io/fantacode-studios/fantacode-studios/fs-parkour-and-climbing-system/sliding"
  support-url: "mailto:studios@fantacode.com"
  last-verified: "2026-09-13"
---

# Add Slide Surfaces

Sliding is surface-driven: whenever the ground under the character's hips is tagged **`Slide`**, `SlidingController` takes focus, plays the slide loop, accelerates downhill along the sampled slope, lets the player steer sideways, and hands control back 0.15 s after leaving the surface. Pressing Jump during a slide performs a slide-jump that keeps the slide momentum. This skill lets an agent make a surface slideable and tune the feel.

## When to use this skill

- "Make this ramp / chute / slope a slide."
- "The character just walks down the slope instead of sliding."
- "Slide faster / slower / with less steering."
- "Jump off the end of the slide."
- "Slide keeps going on flat ground."

Not for:
- Crouch-slide on flat floors from a sprint — not a feature of this asset.
- Vaults and step-ups — `fs-parkour-and-climbing-system-2-configure-parkour-actions`.
- Gap jumps — `fs-parkour-and-climbing-system-2-tune-predictive-jumping`.

## Prerequisites

- Player with `FC_ParkourSystem.SlidingController` on `Player` (added by the FS Window when Parkour and Climbing is selected, and present on `Prefabs/Parkour Player.prefab`). Check: `Object.FindFirstObjectByType<FC_ParkourSystem.SlidingController>() != null`. Note the namespace is `FC_ParkourSystem`, not `FS_`.
- Install check: `System.Type.GetType("FC_ParkourSystem.SlidingController, Assembly-CSharp") != null`. If null, tell the user to import FS Parkour And Climbing System 2 from https://assetstore.unity.com/packages/tools/game-toolkits/fs-parkour-and-climbing-system-2-387610.
- The `Slide` tag exists (created by `Tools > FS System > FS Window` → Welcome → **Import Project Settings**): `UnityEditorInternal.InternalEditorUtility.tags` contains `"Slide"`.
- The slide surface has a collider on a layer inside `FSSettings.GroundLayer` (default `Default`).

## Quick start

Goal: a sloped plane becomes a slide.

1. Select the ramp GameObject (must have a collider; a `MeshCollider` on a sloped mesh or a rotated `BoxCollider` both work).
2. Inspector → **Tag** → `Slide`.
3. Keep its layer `Default` (or any layer in `Player` → `FSSettings` → **Ground Layer**).
4. Press Play and walk onto the ramp.

Expected observable result: the character drops into the slide pose and accelerates downhill; `PlayerController.CurrentSystemState == SystemState.Sliding`; leaving the ramp returns to `Locomotion` within ~0.15 s; pressing Space mid-slide jumps forward.

## Workflows

### Workflow: Mark a surface as a slide

**Goal:** Any collider triggers sliding.

**Steps:**
1. Tag the object `Slide`. The check is `slideHit.transform.CompareTag("Slide")` on the collider hit directly under the hips, so tag the object that owns the collider, not a parent.
2. Detection is a 0.3 m sphere cast (then a ray fallback) from `hips + 0.5 m up + 0.2 m forward` straight down over **Raycast Distance** (`raycastDistance`, default `1.5`) against `FSSettings.GroundLayer`. Thin or trigger colliders that other ground overlaps can be missed — give the slide its own solid collider.
3. Slope direction is sampled from 16 rays on a 2 m ring around the character (5 m long, ground layer); only surfaces steeper than 10° contribute, and the lowest sample sets the downhill direction. On a flat `Slide` surface the character keeps his entry direction at least at Min Slide Speed.

**Expected result:** Walking onto the surface starts the slide; walking off it ends it.

### Workflow: Tune slide speed and steering

**Goal:** Faster/slower, more/less control.

`Player` → `SlidingController` (shipped prefab values in parentheses; these differ from the script defaults in a few places):
- **Slide Speed** (`slideSpeed`, 12) — target speed.
- **Slide Acceleration** (`slideAcceleration`, 20) — m/s² toward Slide Speed while below it.
- **Slide Deceleration** (`slideDeceleration`, 8) — lerp rate back down when above Slide Speed.
- **Min Slide Speed** (`minSlideSpeed`, 4) — speed is never allowed below this while sliding; also the entry speed when walking on slowly.
- **Slope Slide Boost** (`slopeSlideBoost`, 5) — extra downhill acceleration scaled by slope angle / 90°.
- **Lateral Control Strength** (`lateralControlStrength`, 5) — how much A/D (stick X) can push sideways; sideways input never adds uphill velocity. `0` = no steering.
- **Raycast Distance** (`raycastDistance`, 1.5) — surface detection depth.

**Expected result:** On a 30° ramp the character reaches roughly Slide Speed within a second; steering deflects the path without climbing back up.

### Workflow: Tune the slide jump

**Goal:** Jumping out of a slide.

- Press **Jump** (Space by default) at any moment during the slide, including the entry frame; a 0.15 s input buffer catches early presses.
- **Slide Jump Height** (`slideJumpHeight`, 1 on the shipped prefab; script default 2) — apex height.
- **Slide Jump Forward Boost** (`slideJumpForwardBoost`, 1) — multiplier on the current slide velocity carried into the jump.
- **Jump Force Time** (`jumpForceTime`, 0.45 on the shipped prefab; range 0–0.9, script default 0.65) — normalized time of the slide-jump clip at which the upward velocity is applied, so the launch matches the animation's push-off.
- **Slide Jump Clip Info** (`slideJumpClipInfo`) — the `AnimGraphClipInfo` played for the jump; **Sliding Clip** (`slidingClip`) — the looping slide animation.

After the jump lands, momentum is handed to locomotion (`LocomotionCore.SetCurrentVelocity`) so the character keeps moving.

**Expected result:** Pressing Space mid-slide launches forward and up; the character keeps sliding speed through the air.

### Workflow: Control where a slide ends

**Goal:** Slides stop where intended.

- The slide ends when the surface under the hips has not been `Slide`-tagged for **0.15 s** (`slideExitGracePeriod`, constant) — make sure the run-out floor is not tagged `Slide`.
- Exiting resets velocity to zero (no run-on) unless it was a slide-jump.
- To stop the slide from code: `player.UnFocusSystem(slidingController)` is what `EndSlide` does; prefer changing the tag on the surface instead.

**Expected result:** Character stands up on the first untagged floor.

## Verification

- `GameObject.FindGameObjectsWithTag("Slide")` includes every intended surface, and each has a collider on a `FSSettings.GroundLayer` layer.
- In Play mode on the surface: `FindFirstObjectByType<FS_Core.PlayerController>().CurrentSystemState == SystemState.Sliding` and `FindFirstObjectByType<FC_ParkourSystem.SlidingController>().IsInFocus == true`.
- Off the surface for > 0.2 s: state returns to `Locomotion`, `IsInFocus == false`.
- Space during the slide: state passes through the jump and lands in `Locomotion` with forward velocity.
- No Console errors; no `MissingReferenceException` about `slidingClip`.

## API quick reference

| Entry point | Type | What it does |
|---|---|---|
| Tag `Slide` | tag on the surface collider | Enables sliding on contact. |
| `FC_ParkourSystem.SlidingController` | `SystemBase` (Priority 20, `SystemState.Sliding`) | All slide settings; `IsInFocus` tells you a slide is active. |
| `FC_ParkourSystem.SlidingController.GetSlideDirection()` | method → `SlopeSample` | Current downhill sample (`position`, `normal`, `slopeAngle`, `downhill`). |
| `FS_Core.FSSettings.GroundLayer` | `LayerMask` | Which layers the slide raycasts hit. |
| `FS_Core.PlayerController.CurrentSystemState` | `SystemState` | `Sliding` while sliding. |

## Common issues

- **Symptom:** No slide on a tagged ramp. **Cause:** Tag on a parent, collider layer not in Ground Layer, or ramp shallower than 10° with no entry speed. **Fix:** Tag the collider object; check `FSSettings.GroundLayer`; steepen or enter at speed.
- **Symptom:** Slide starts at the top edge and continues on flat ground forever. **Cause:** Run-out floor is also tagged `Slide`. **Fix:** Remove the tag from the run-out.
- **Symptom:** Character slides uphill when steering. **Cause:** Not possible by design (uphill component is removed); if it appears, the slope samples come from a different mesh. **Fix:** Isolate the slide collider from surrounding geometry.
- **Symptom:** Slide-jump barely leaves the ground. **Cause:** `slideJumpHeight` 1 on the shipped prefab. **Fix:** Raise it (script default is 2).
- **Symptom:** Slide animation stays after jumping. **Cause:** `slidingClip` loop not stopped — handled by `StopLoopingAnimations(true)`; only occurs if the clip fields were cleared. **Fix:** Re-assign Sliding Clip and Slide Jump Clip Info from `Resources/Parkour Controller.prefab`.
- **Symptom:** Sprint-slide on flat ground requested. **Cause:** Not supported. **Fix:** Explain the limitation; sliding needs a `Slide` surface.

## Boundaries

- Sliding is triggered only by tagged surfaces; there is no input-triggered crouch slide.
- No slide-into-ledge grab; leaving a slide off an edge falls normally (ledge catch works if Jump is held).
- Slide direction follows the terrain sample, not a spline; for a scripted chute, shape the collider.
- Slide surfaces must be in the Ground layer mask; layers used only for obstacles are ignored.
