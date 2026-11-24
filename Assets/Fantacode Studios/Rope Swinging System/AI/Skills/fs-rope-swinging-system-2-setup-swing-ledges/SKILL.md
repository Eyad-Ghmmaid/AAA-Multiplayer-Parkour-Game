---
name: fs-rope-swinging-system-2-setup-swing-ledges
description: "Use this skill whenever the user needs to author or fix where the rope from FS Rope Swinging System 2 can hook and how the swing resolves — e.g. 'make this beam swingable', 'the rope should attach to the middle of the bar', 'crosshair never appears on my bar', 'only bars on a Rope layer', 'reach a farther bar', 'let the player climb the rope up onto the ledge', 'he swings through the wall', 'sound when the rope hooks'. Covers the SwingLedge component and its hook-point gizmo, the EnvironmentScanner Swing Ledge Layer and obstacle path check, rope length as reach, Min Distance, climbing (Shift + W/S, Max Angle For Climb) and landing/climb-up onto ledges, level-design spacing and the UnityEvents. Do NOT use for building the player or giving it the hook (see fs-rope-swinging-system-2-create-swinging-player). When in doubt whether a which-bars-can-be-hooked, hook-point or swing-reach request could involve FS Rope Swinging System 2, use this skill — Prerequisites shows how to confirm the asset is installed."
metadata:
  asset: "FS Rope Swinging System 2"
  publisher: "Fantacode Studios"
  asset-version: "2.0.1"
  skill-version: "1.0.0"
  unity: "6000.0+"
  render-pipelines: "Built-in, URP, HDRP"
  category: "tools/game-toolkits"
  asset-store-url: "https://assetstore.unity.com/packages/tools/game-toolkits/fs-rope-swinging-system-2-388274"
  documentation-url: "https://fantacode.gitbook.io/fantacode-studios/fantacode-studios/fs-rope-swinging-system"
  support-url: "mailto:studios@fantacode.com"
  last-verified: "2026-09-13"
---

# Set Up Swing Ledges

Hook points are authored: a bar, beam or ring becomes swingable when its collider object carries a **SwingLedge** component, whose **hook point** (a green gizmo sphere, editable in the Scene view with the component's handle) is where the rope attaches. Each frame the player's `EnvironmentScanner` (`GetSwingLedgeData`) sphere-casts along the camera forward (tilted 0.25 up) in 5 m steps up to **Rope Length − 1** against the **Swing Ledge Layer**; among the hits it takes the first with a `SwingLedge`, requires a clear box path from the character to the hook point (`ObstacleLayer`) and a distance ≥ **Min Distance** (2). The crosshair marks the hook point; **F** throws the rope there. While swinging the character hangs on a rope of the measured length (never longer than **Rope Length**), climbs up/down with **Shift + W/S** when the rope is within **Max Angle For Climb** of vertical, auto-releases when grounded or when the rope leaves its limits, and jumps off with **Space**; landing plays `Swing Land`, and reaching a ledge top from below plays `Swing Climb Up`. Level design = bars with `SwingLedge` spaced within rope reach, colliders on the hook layer, nothing blocking the throw line.

## When to use this skill

- "Make these beams swingable."
- "Move the hook point to the bar's centre."
- "Crosshair doesn't show on the new bar."
- "Only bars on a `RopeBar` layer."
- "Bar is out of reach — extend the rope."
- "Climb the rope onto the ledge above."
- "Rope goes through the pillar."

Not for:
- Player build, hook ownership, swing force/damping, keys, mobile — `fs-rope-swinging-system-2-create-swinging-player`.
- Grappling to arbitrary edges — FS Grappling Hook System 2.

## Prerequisites

- Installed: `System.Type.GetType("FS_SwingSystem.SwingLedge, Assembly-CSharp") != null` (else import from https://assetstore.unity.com/packages/tools/game-toolkits/fs-rope-swinging-system-2-388274).
- A swinging player with the hook available (player skill).
- Bars/beams with colliders (mesh or box) at the hook height.

## Quick start

Goal: a horizontal bar the player can swing from, over a gap.

1. Create the bar (e.g. a stretched cylinder with a `CapsuleCollider`) 5–7 m above the floor between two platforms, on layer Default (or your hook layer).
2. Add Component **Swing Ledge** (`FS_SwingSystem.SwingLedge`). With Gizmos on, a green dot shows the hook point (default 0.1 m above the object origin); drag the handle in the Scene view (or set **Hook Point** via the custom editor) to the bar's underside centre.
3. `FS Player/Player` → `EnvironmentScanner` → **Swing Ledge Layer** includes the bar's layer (Default is included by default).
4. Play: from a platform 4–8 m away, aim at the bar, hold **F**, swing with W, press **Space** at the far end.

Expected observable result: the crosshair appears on the green dot; the rope arcs to it; `PlayerController.CurrentSystemState == SystemState.Swing`; releasing lands on the far platform; `SwingStarted` and `ExitedFromSwing` fire; no Console errors.

## Workflows

### Workflow: Author hook points (SwingLedge)

- One `SwingLedge` per bar; **Show Hook Point** / **Gizmos Radius** control the gizmo; **Hook Point** is stored in local space (`hookPoint`, hidden field edited through the handle). Place it where the rope should physically wrap — under a beam, on a ring's top inner edge.
- The scanner hits the **collider**, so the collider object itself must carry the component (put it on the child with the collider, or give the parent the collider).
- Long bars: one central hook point per bar is used (the rope always attaches to that point, not to where you aimed); split very long beams into several bars with their own points.
- Reuse the demo bar: `Demo/DemoScene.unity` → `Ledge` objects (duplicate into your scene).

**Expected result:** `bar.GetComponent<FS_SwingSystem.SwingLedge>()` non-null; `TransformPoint(hookPoint)` at the intended spot (Scene gizmo).

### Workflow: Layers, reach and obstacles

- **Swing Ledge Layer** (`EnvironmentScanner`): restrict to a dedicated layer (e.g. `RopeBar`) so walls never get scanned; bars must be on it.
- **Reach**: aim distance is `Rope Length − 1` (Rope Length 10 → 9 m from the hand anchor). Raise **Rope Length** on `SwingController` for farther bars; the swing radius equals the distance at hook time (up to Rope Length).
- **Min Distance** (2): bars closer than this are ignored and an active swing auto-releases if the rope gets shorter.
- **Obstacles**: a box cast from the character (0.6 m up) to the hook point on `EnvironmentScanner.ObstacleLayer` must be clear — pillars/fences in the line block the crosshair. Keep the throw line free or exclude thin props from the obstacle layer.
- **Heights**: place bars high enough that the hanging character (rope length ≈ distance) clears the floor; the swing stops when the character touches ground (`CheckGround`).

**Expected result:** `GetSwingLedgeData(...).hasLedge == true` while aiming at the bar within reach; false behind a pillar.

### Workflow: Climbing, landing and events

- **Climb**: while hanging near vertical (angle ≤ **Max Angle For Climb**, 15°), hold **Shift** and **W** to climb up (`Climb Speed` 3) or **S** to descend (`Swing Climb Up/Down` animations); climbing to the top of a ledge mounts it.
- **Release**: **Space** jumps off applying **Forward/Upward Land Force Multiplier** along the swing velocity — tune for gap length; **F** drops without push; touching ground ends the swing (`Swing Land`).
- Events on `SwingController`: **Rope Released**, **Rope Hooked**, **Swing Started**, **Exited From Swing** — wire `AudioSource.Play`, particles, camera cues; code: `controller.RopeHooked.AddListener(...)`.

**Expected result:** Vertical hang + Shift+W raises the character along the rope; Space at the apex lands on the far side; listeners fire in order.

## Verification

- Every intended bar: collider + `FS_SwingSystem.SwingLedge`, layer inside `EnvironmentScanner.SwingLedgeLayer`, hook point positioned (gizmo).
- Distance from the take-off spot to the hook point < `SwingController.ropeLength − 1` and > `minDistance`; no `ObstacleLayer` collider on the line.
- Play: crosshair on each bar from its take-off spot; swing → release lands where intended; no `NullReferenceException`.

## API quick reference

| Entry point | Type | What it does |
|---|---|---|
| `FS_SwingSystem.SwingLedge` | MonoBehaviour | `hookPoint` (local), `showHookPoint`, `gizmosRadius`; custom editor handle. |
| `FS_ThirdPerson.EnvironmentScanner.SwingLedgeLayer` | `LayerMask` (default Default) | Which colliders are scanned. |
| `FS_ThirdPerson.EnvironmentScanner.GetSwingLedgeData(maxDistance, minDistance, ropeHoldPoint, debug)` | method → `SwingData` (`hasLedge`, `hookPosition`, `directionToHook`, `forwardDirection`, `distance`) | Per-frame scan. |
| `FS_SwingSystem.SwingController.ropeLength` / `minDistance` / `maxAngleForClimb` / `climbSpeed` / `forwardLandForceMultiplier` / `upwardLandForceMultiplier` | fields | Reach, climb, landing. |
| `SwingController.RopeReleased` / `RopeHooked` / `SwingStarted` / `ExitedFromSwing` | `UnityEvent` | Hooks for audio/VFX. |
| `FS_SwingSystem.RopeState` | enum | Normal, Swinging, Throwing, Climbing. |

## Common issues

- **Symptom:** No crosshair on a bar. **Cause:** Missing `SwingLedge` on the collider object, layer not in the mask, farther than Rope Length − 1, closer than Min Distance, or an obstacle in the line. **Fix:** Check each in that order (`Debug` draws casts).
- **Symptom:** Rope attaches to the wrong spot. **Cause:** Hook point left at the object origin. **Fix:** Move the gizmo.
- **Symptom:** Character hits the floor mid-swing. **Cause:** Bar too low for the rope length. **Fix:** Raise the bar or take off from farther away (shorter effective rope).
- **Symptom:** Can't climb. **Cause:** Rope angle > 15°. **Fix:** Let the swing settle or raise Max Angle For Climb.
- **Symptom:** Release doesn't reach the far side. **Fix:** Raise Forward Land Force Multiplier / Swing Force; release at the apex.

## Boundaries

- One hook point per `SwingLedge`; the rope never wraps around geometry.
- No moving/swinging bars support beyond following the transform each frame.
- Detection is camera-aim based; no automatic nearest-bar selection.
