---
name: fs-parkour-and-climbing-system-2-tune-predictive-jumping
description: "Use this skill whenever the user wants the character to jump across gaps, onto platforms, up to ledges or back off a hang with FS Parkour And Climbing System 2 — e.g. 'he won't jump to that platform', 'make the jump reach further', 'jump too high / too short', 'why does he jump to the closest box', 'turn off the auto jump', 'jump back from the ledge', 'jump where I'm facing not where the camera looks'. Covers predictive jumping (arc simulation, landing-spot selection, MaxJumpHeight, MaxJumpDistance, MinJumpDistance, jumpToTheClosestLedge, alwaysUsePlayerForward), predictive climb to ledges, back jumps from a hang, and diagnosing missed jumps. Do NOT use for vaulting obstacles (see fs-parkour-and-climbing-system-2-configure-parkour-actions) or authoring ledges (see fs-parkour-and-climbing-system-2-make-ledges-climbable). When in doubt whether a jump or platform request could involve FS Parkour And Climbing System 2, use this skill — Prerequisites shows how to confirm the asset is installed."
metadata:
  asset: "FS Parkour And Climbing System 2"
  publisher: "Fantacode Studios"
  asset-version: "2.0.1"
  skill-version: "1.0.0"
  unity: "6000.0+"
  render-pipelines: "Built-in, URP, HDRP"
  category: "tools/game-toolkits"
  asset-store-url: "https://assetstore.unity.com/packages/tools/game-toolkits/fs-parkour-and-climbing-system-2-387610"
  documentation-url: "https://fantacode.gitbook.io/fantacode-studios/fantacode-studios/fs-parkour-and-climbing-system/parkour"
  support-url: "mailto:studios@fantacode.com"
  last-verified: "2026-09-13"
---

# Tune Predictive Jumping

Predictive jumping means the player never aims a jump manually: while Jump is held and the character is grounded, `EnvironmentScanner.FindPointToJump` simulates a jump arc in the input direction, box-casts along it against the Obstacle layer to find surfaces the character could land on, picks the best one, and `ParkourCore.DoPredictiveJump` plays a jump that is warped to land exactly there. If a Ledge-layer collider with ClimbPoints is found along the arc before a landing spot (or is closer), a **predictive climb** grabs it instead. From a hang, the same search runs backwards for back-jumps. This skill lets an agent adjust reach, height and target choice, and explain why a given jump is or is not attempted.

## When to use this skill

- "Make him jump to the next rooftop / across this gap."
- "The jump doesn't reach; increase the jump distance."
- "He keeps picking the near platform instead of the one I'm aiming at."
- "Disable the automatic jump and use a normal jump."
- "Jump in the direction the character faces, not the camera."
- "Jump back from the ledge onto the platform behind."
- "He jumps but lands short / clips into the edge."

Not for:
- Vault / step-up over obstacles directly in front — `fs-parkour-and-climbing-system-2-configure-parkour-actions`.
- Making the target ledge grabbable — `fs-parkour-and-climbing-system-2-make-ledges-climbable`.
- Slide-jumps — `fs-parkour-and-climbing-system-2-add-sliding`.

## Prerequisites

- Player with `FS_ParkourSystem.ParkourController` and `FS_Core.EnvironmentScanner` on `Player`: `Object.FindFirstObjectByType<FS_ParkourSystem.ParkourController>() != null`.
- Install check: `System.Type.GetType("FS_ParkourSystem.ParkourCore, Assembly-CSharp") != null`. If null, tell the user to import FS Parkour And Climbing System 2 from https://assetstore.unity.com/packages/tools/game-toolkits/fs-parkour-and-climbing-system-2-387610.
- Landing surfaces have colliders on a layer inside `FSSettings.ObstacleLayer` (default `Default`; `Ledge` auto-added).
- Predictive jump is on: `ParkourController.enablePredictiveJump == true` (default).

## Quick start

Goal: jump from one 1 m-high box to another 3 m away.

1. Both boxes: colliders on layer `Default`, flat tops.
2. Press Play. Stand on box A, push the stick/WASD toward box B and **hold Space**.
3. The character runs to the edge (a jump only starts within **Min Distance From Ledge For Jumping** = 0.5 m of the edge when an edge is detected) and jumps, landing on B.

Expected observable result: `PlayerController.CurrentSystemState` switches to `Parkour` for the flight and back to `Locomotion` on landing; no manual aiming; the landing spot is on B's top surface, inset from its edge.

## Workflows

### Workflow: Adjust reach and height

**Goal:** Longer / shorter / higher jumps.

All on `Player`:

`ParkourController` (`FS_ParkourSystem`):
- **Enable Predictive Jump** (`enablePredictiveJump`, default `true`) — off = normal locomotion jump only; predictive climb to ledges still works if `ClimbController.enableClimbing` is on.
- **Max Jump Height** (`MaxJumpHeight`, default `1.2`) — vertical apex of the simulated arc (metres above the start). Targets higher than the arc are never found.
- **Max Jump Distance** (`MaxJumpDistance`, default `7`) — passed to the simulation as the arc's horizontal launch speed (m/s), so it controls how far the arc travels, not a hard metre cap. Raise it to reach farther platforms; the character's own jump animation is warped to whatever target is chosen.
- **Min Distance From Ledge For Jumping** (`minDistanceFromLedgeForJumping`, default `0.5`) — when the scan sees a drop (edge) before the target, the jump is delayed until the character is within this distance of the edge, so he runs to the edge before leaping.
- **Foot Collider Radius** (`0.1`) / **Foot Collider Offset** — sphere at the leading foot used to detect touchdown during the flight.

`EnvironmentScanner` (`FS_Core`, parkour partial):
- **Min Jump Distance** (`MinJumpDistance`, default `0.8`) — landing spots closer than this along the arc are ignored; also rejects lower targets closer than this. Raise it if the character hops to spots right in front of him.
- **Jump To The Closest Ledge** (`jumpToTheClosestLedge`, default `false`) — see next workflow.
- **Always Use Player Forward** (`alwaysUsePlayerForward`, default `false`) — see below.

**Expected result:** Holding Space toward a farther platform now produces a jump; targets beyond the new arc are still ignored (character does a normal jump or nothing).

### Workflow: Change which landing spot is chosen

**Goal:** Prefer far vs near targets.

How selection works (`FindPointToJump`): every arc sample that hits a surface (top normal within 45° of up) farther than `MinJumpDistance`, after the ground height has changed (a gap or step), becomes a candidate. Candidates are grouped by height (0.2 m bands) and groups are tried from highest to lowest. Within a group, the default (`jumpToTheClosestLedge = false`) picks a spot around the middle of the group (`index = min(count/2, 3)` from the far end), so the character lands well onto the platform rather than on its lip; `true` picks the nearest candidate. Each candidate is rejected if the head-height line from the start to the apex or from the apex to the target is blocked on the Obstacle layer, or if a 0.2 m sphere 0.5 m above the target is blocked (no standing room). The final target is nudged 0.15 m away from any edge that has no floor beyond it.

**Steps:**
1. To land as early as possible (small platforms, pillars): `Player` → `EnvironmentScanner` → tick **Jump To The Closest Ledge**.
2. To keep landing deeper on wide platforms: leave it off.
3. For a higher platform to win over a lower one, keep it inside the arc (raise `MaxJumpHeight`); higher groups are always tried first.

**Expected result:** The chosen landing point moves toward the near edge (true) or platform interior (false).

### Workflow: Jump in facing direction instead of camera/input direction

**Goal:** Aim by character orientation.

**Steps:**
1. `Player` → `EnvironmentScanner` → tick **Always Use Player Forward**.
2. Now `FindPointToJump` uses `transform.forward` instead of `ICharacter.MoveDir` (camera-relative input), and back-jumps from a hang also use the character's forward instead of the camera direction.

**Expected result:** Holding Space with the character facing a platform jumps to it even if the camera looks elsewhere.

### Workflow: Jump to a ledge (predictive climb) and back-jump from a hang

**Goal:** Reach hanging positions and leave them by jumping.

- **Predictive climb:** along the same arc, a box cast on the Ledge layer looks for a ledge collider with ClimbPoints. If one is found and either no landing spot exists or the ledge is closer than the landing spot, `ClimbController.ClimbToLedge` is called and the character jumps to hang from the nearest facing ClimbPoint (within 2 m of the hit). This needs `ClimbController.enableClimbing = true` and a correctly baked ledge (see the ledge skill).
- **Back-jump from a hang:** while hanging, press **Q** (Jump From Hang) or hold **Space** with no move input. `ParkourController.HandlePredictiveBackJump` runs `FindPointToJump` in the camera direction (or character forward with Always Use Player Forward) with a fixed launch speed of 4.5 and apex of 1 m, no ledge scanning beyond 2 m up. If it finds a ledge, the character jumps to it; if it finds a landing spot, `DoPredictiveBackJump` plays the back-jump; otherwise nothing happens. In free hang on a normal ledge, directions more than 90° from the facing are refused (see `SwingableLedge` in the ledge skill).
- **Drop:** **R** with no move input drops from the point.

**Expected result:** Space toward a ledge above the arc's landing spots grabs it; Q from a hang jumps to the platform behind when one is within ~4.5 m and 1 m up.

### Workflow: Diagnose a missed jump

**Goal:** Find why a jump is not attempted or lands wrong.

**Steps:**
1. Input: predictive scan only runs while `LocomotionInputManager.JumpHold` is true and `ICharacter.IsGrounded`. Tap = normal jump. Hold it.
2. Direction: `MoveDir` must be non-zero (push the stick); with zero input the character's forward is used.
3. Layer: target collider layer ∈ `FSSettings.i.ObstacleLayer`. Trigger colliders are hit too when Project Settings > Physics > Queries Hit Triggers is on (Unity default).
4. Arc: horizontal distance reachable with `MaxJumpDistance` as launch speed and `MaxJumpHeight` apex; target top must be ≤ apex height. Increase either.
5. Height change: a candidate is only accepted after the scanned ground height changed by more than 0.2 m relative to the start — a flat continuous floor never triggers a predictive jump (by design; use the normal jump).
6. Head clearance: a low ceiling or arch across the arc blocks the linecasts at 1.4 m head height.
7. Landing room: 0.2 m sphere 0.5 m above the target must be free; low railings on the target platform reject it.
8. Edge wait: if the character stops at an edge without jumping, he is farther than `minDistanceFromLedgeForJumping` from it; keep pushing forward.
9. Touchdown: if he lands inside geometry, the foot collider missed the surface; reduce `Foot Collider Offset` or make the surface a solid collider.

**Expected result:** One of the checks fails and points to the fix.

## Verification

- `pc = FindFirstObjectByType<FS_ParkourSystem.ParkourController>()`: `pc.enablePredictiveJump`, `pc.MaxJumpHeight`, `pc.MaxJumpDistance` show the intended values.
- `es = pc.GetComponent<FS_Core.EnvironmentScanner>()`: `es.MinJumpDistance`, `es.jumpToTheClosestLedge`, `es.alwaysUsePlayerForward` as intended.
- Programmatic probe in Play mode from the start position toward the target: `es.FindPointToJump(dir, pc.MaxJumpDistance, pc.MaxJumpHeight).jumpPointFound == true` and `.rootPosition` lies on the target's top surface (within 0.3 m of its bounds).
- During the jump `pc.InAction == true` and `pc.GetComponent<FS_ParkourSystem.ParkourCore>().InAirOfPredictiveJump == true`; after landing both are `false` and `PlayerController.CurrentSystemState == SystemState.Locomotion`.
- No Console errors.

## API quick reference

| Entry point | Type | What it does |
|---|---|---|
| `FS_Core.EnvironmentScanner.FindPointToJump(Vector3 dir, float forwardSpeed, float jumpHeight, bool checkClimb = true)` | method → `JumpData` | Simulates the arc and returns `jumpPointFound`, `rootPosition`, `footPosition`, `isClimbable`, `climbHitData`, `hasLedge`, `pointBeforeledge`, `hasSpaceToLand`. |
| `FS_Core.EnvironmentScanner.MinJumpDistance` / `jumpToTheClosestLedge` / `alwaysUsePlayerForward` | fields | Selection tuning. |
| `FS_ParkourSystem.ParkourController.enablePredictiveJump` / `MaxJumpHeight` / `MaxJumpDistance` | fields | Arc tuning; `MaxJumpHeight`/`MaxJumpDistance` are read-only from code (`private set`). |
| `FS_ParkourSystem.ParkourController.HandlePredictiveBackJump(Vector3 checkDir)` | method | Back-jump / climb from a hang in a direction. |
| `FS_ParkourSystem.ParkourCore.DoPredictiveJump(JumpData, ...)` | `IEnumerator` | Plays a warped jump to `JumpData`: `StartCoroutine(core.DoPredictiveJump(data))`. |
| `FS_ParkourSystem.ParkourCore.InPredictiveJump` / `InAirOfPredictiveJump` | bools | Flight state. |
| `FS_ParkourSystem.ParkourController.PermitsWhileFocused(SystemState)` | override | Lets Grappling Hook / Rope Swing systems run mid-flight. |

## Common issues

- **Symptom:** Character does a plain hop instead of jumping to the platform. **Cause:** Space tapped, not held; or no height change detected (same floor level). **Fix:** Hold Space; predictive jump only targets platforms at a different height or across a gap.
- **Symptom:** Never jumps to a platform that is clearly reachable. **Cause:** Platform above `MaxJumpHeight` (1.2 m) or beyond the arc. **Fix:** Raise Max Jump Height / Max Jump Distance.
- **Symptom:** Jumps to a nearby step instead of the far platform. **Cause:** Nearer candidate in a higher or equal group; default picks by height first. **Fix:** Lower the step's collider out of the Obstacle layer, or move it; enabling Jump To The Closest Ledge makes this worse.
- **Symptom:** Stops at the edge and waits. **Cause:** `minDistanceFromLedgeForJumping` not yet reached. **Fix:** Keep pushing forward or increase the value.
- **Symptom:** Back-jump does nothing. **Cause:** Nothing within the fixed back-jump arc (speed 4.5, height 1 m), or free hang facing away. **Fix:** Bring the target closer/lower; use a braced hang wall.
- **Symptom:** Jumps toward the camera direction, not where the character faces. **Cause:** Default uses camera-relative input. **Fix:** Always Use Player Forward.

## Boundaries

- The arc is a fixed parabola per jump; there is no mid-air steering (except systems the parkour controller explicitly permits, such as grappling).
- Moving platforms are targeted at their position when the scan runs; the landing warp does not track them.
- No jump-to-target API takes a world position directly; supply a `JumpData` from `FindPointToJump` or construct one with `rootPosition`/`footPosition`/`jumpPointFound` set.
- Landing on slopes steeper than 45° is not supported (those surfaces are rejected as candidates).
