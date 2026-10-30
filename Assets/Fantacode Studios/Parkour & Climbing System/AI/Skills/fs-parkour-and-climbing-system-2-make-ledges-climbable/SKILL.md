---
name: fs-parkour-and-climbing-system-2-make-ledges-climbable
description: "Use this skill whenever the user wants the player to grab, hang from, shimmy along or climb ledges with FS Parkour And Climbing System 2 — e.g. 'make this wall climbable', 'the player won't grab the ledge', 'add climb points to my building', 'let him hang off this beam', 'bake climb points', 'hands are floating off the ledge', 'he can't climb up onto the roof', 'make a swingable ledge'. Covers the Ledge layer, the ClimbPoint Editor (Bake / Clear Points, Distance Between Points, Both Side), Mount Point and manual ClimbPoint options, SwingableLedge, and tuning ClimbController / ClimbCore (max distance, press-to-climb, hand and foot offsets). Do NOT use for vaulting obstacles (see fs-parkour-and-climbing-system-2-configure-parkour-actions) or jumping across gaps (see fs-parkour-and-climbing-system-2-tune-predictive-jumping). When in doubt whether a climbing or ledge request could involve FS Parkour And Climbing System 2, use this skill — Prerequisites shows how to confirm the asset is installed."
metadata:
  asset: "FS Parkour And Climbing System 2"
  publisher: "Fantacode Studios"
  asset-version: "2.0.1"
  skill-version: "1.0.0"
  unity: "6000.0+"
  render-pipelines: "Built-in, URP, HDRP"
  category: "tools/game-toolkits"
  asset-store-url: "https://assetstore.unity.com/packages/tools/game-toolkits/fs-parkour-and-climbing-system-2-387610"
  documentation-url: "https://fantacode.gitbook.io/fantacode-studios/fantacode-studios/fs-parkour-and-climbing-system/climbing"
  support-url: "mailto:studios@fantacode.com"
  last-verified: "2026-09-13"
---

# Make Ledges Climbable

The climbing system hangs the character from **ClimbPoint** components placed along the top edge of a mesh that sits on the **Ledge** layer. Points are generated with one click by the ClimbPoint Editor and can be moved or flagged by hand. From a hang the character shimmies between neighbouring points, jumps to points further away, climbs up over mount points, drops down, or jumps backwards to another ledge; the hang type (braced with feet on the wall, or free hang) is decided at runtime by whether a wall exists below the point. This skill lets an agent author climbable geometry and tune how the character hangs.

## When to use this skill

- "Make this wall / beam / roof edge climbable."
- "The player runs into the ledge instead of grabbing it."
- "Generate climb points for everything under `Ledges`."
- "He grabs the ledge but can't climb onto the platform."
- "Only let him climb up on this side."
- "Hands are inside / floating off the ledge while hanging."
- "Make a ledge he can swing from."

Not for:
- Vaulting, stepping onto or climbing up low obstacles from the ground without hanging — `fs-parkour-and-climbing-system-2-configure-parkour-actions`.
- Jumping across gaps and onto platforms — `fs-parkour-and-climbing-system-2-tune-predictive-jumping`.
- No player in the scene yet — `fs-parkour-and-climbing-system-2-create-parkour-player`.

## Prerequisites

- A player built with Parkour and Climbing (`FS_ParkourSystem.ClimbController` on the `Player` object). Check: `Object.FindFirstObjectByType<FS_ParkourSystem.ClimbController>() != null`.
- The `Ledge` layer exists: `LayerMask.NameToLayer("Ledge") != -1`. If not, `Tools > FS System > FS Window` → Welcome → **Import Project Settings**.
- Install check: `System.Type.GetType("FS_ParkourSystem.ClimbPoint, Assembly-CSharp") != null`. If null, tell the user to import FS Parkour And Climbing System 2 from https://assetstore.unity.com/packages/tools/game-toolkits/fs-parkour-and-climbing-system-2-387610.
- The ledge object must have a `MeshRenderer` (the baker uses its bounds) and a collider (raycasts hit colliders, not renderers).
- Gizmos must be visible in the Scene view to see the points (Scene view toolbar → Gizmos toggle).

## Quick start

Goal: an existing wall-top or beam becomes grabbable.

1. Select the ledge GameObject(s) in the Hierarchy — one object per straight edge; a long beam or the top slab of a wall is ideal. Each must have a `MeshRenderer` and a collider.
2. `Tools > FS System > Parkour > ClimbPoint Editor`.
3. Leave **Distance Between Points** at `0.75` and **Both Side** off. Click **Bake**.
4. Look at the ledge in the Scene view: small yellow spheres with green arrows along the top edge are the climb points; arrows point outward, toward where the player approaches from.
5. Press Play, walk toward the ledge and hold **Space** (Jump) as you reach it; or jump toward it — the character grabs and hangs. Use **A/D** to shimmy, **W** to climb up, **R** to drop, **Q** to jump away.

Expected observable result: the selected object is now on layer `Ledge` and has child GameObjects named `Climb Point 0`, `Climb Point 1`, … each with a `ClimbPoint` component; `PlayerController.CurrentSystemState` reads `Climbing` while hanging.

## Workflows

### Workflow: Bake climb points on selected ledges

**Goal:** Generate evenly spaced ClimbPoints along the longest axis of each selected mesh.

**Steps:**
1. Select one or more ledge objects. Objects without a `MeshRenderer` are skipped silently.
2. `Tools > FS System > Parkour > ClimbPoint Editor` (window title "Climb Point").
3. **Distance Between Points** — spacing along the edge; also the shimmy step length. Minimum 0.05 (clamped). Default 0.75. Use ~0.5 for short ledges so at least two points fit; a ledge shorter than 1.5 × distance gets a single centre point.
4. **Both Side** — also creates a mirrored row facing the opposite direction, for a free-standing beam or thin wall the player can approach from either side.
5. Click **Bake**. For each object: existing child ClimbPoints are destroyed (Undo-able), `obj.layer` is set to `Ledge`, the object is temporarily zeroed in rotation to measure `MeshRenderer.bounds`, points are created along the longest of X/Z at the top (`bounds.max.y`), neighbouring points on the same ledge are linked with `ConnectionType.Move`, and rotation is restored.
6. Repeat per edge. A four-sided rooftop needs four ledge objects (or one object per side) — the baker only runs along the single longest axis.

**Expected result:** Hierarchy shows child objects with `ClimbPoint` under each ledge; Scene view shows the gizmos; layer of each ledge is `Ledge`. To remove: select the ledges and click **Clear Points**.

Programmatic equivalent (Editor only):

```csharp
using UnityEngine;
using UnityEditor;
using FS_ParkourSystem;

// Bake a single ledge with 0.75 spacing, one side.
GameObject ledge = GameObject.Find("Ledge_01");
ledge.layer = LayerMask.NameToLayer("Ledge");
ClimbingPointUtils.totalPointCount = 0;
ClimbingPointUtils.BakePoints(ledge, 0.75f, bothSide: false, progress: 0f);
```

`BakePoints` is `async static void`; it finishes over a few Editor frames. Verify afterwards with `ledge.GetComponentsInChildren<ClimbPoint>().Length > 0`.

### Workflow: Control where the player can climb up (Mount Point)

**Goal:** Allow climbing onto the surface only from certain points, or force it where the automatic check fails.

By default (`ClimbPoint.useManualOptions == false`) climbing up is automatic: pressing forward from a hang does an obstacle check at the point's height and mounts if the top surface is within 0.2 m of the point and there is standing room (`ClimbController.HandleClimbUpdate`).

**Steps:**
1. Select a baked point (child of the ledge).
2. Tick **Use Manual Options** on the `ClimbPoint` component.
3. Set **Mount Point**: `true` = pressing forward always climbs up here (the obstacle check is skipped); `false` = climbing up is blocked at this point even if there is room.
4. Optionally set **Hand Spacing** (metres between the hands while hanging; `0` = use `ClimbCore.handSpacing`).

**Expected result:** With Mount Point on, W from the hang plays the mount animation; with it off, the character stays hanging. Points with `useManualOptions` off keep automatic behaviour.

### Workflow: Make a swingable ledge

**Goal:** A free-standing bar the character catches mid-jump and can immediately launch off again, in any direction, to chain jumps.

**Steps:**
1. Tag the ledge object (the parent of the points, not the points) **`SwingableLedge`** (the tag is created by Import Project Settings; older docs call it "Swingable Ledge" — the tag has no space).
2. Bake points on it (Both Side is typical for a bar).
3. Ensure there is **no wall** within `hipRayLength` (0.3 m) behind the points, so the hang resolves to free hang — the swing combo only exists in free hang.
4. Reference: `Assets/Fantacode Studios/Parkour & Climbing System/Prefabs/Swingalble Ledge.prefab`.

What the tag changes (source: `ParkourCore.DoPredictiveBackJump`, `ClimbController.HandlePredictiveClimbLanding`):
- From a free hang on a normal ledge, a back-jump is refused when the requested direction is more than 90° from the character's facing; on a `SwingableLedge` any direction is allowed.
- While the character is flying toward a `SwingableLedge` (predictive climb), pressing **Q** (Jump From Hang) with a move direction, or **Space**, arms a combo: as soon as the bar is caught, `FindPointToJump` runs in the camera/input direction and the character chains straight into the next jump or climb instead of settling into the hang. The chain fires only if the camera-to-character angle is under 60° or over 130° (i.e. roughly forward or backward through the bar).

**Expected result:** Approaching the bar with Space held catches it; holding Space (or Q + direction) during the approach makes the character swing through and jump to the next platform without stopping.

### Workflow: Tune hanging and IK

**Goal:** Fix hand/foot placement, reach, or when grabbing happens.

All settings are on the player's `Player` object.

`ClimbController` (`FS_ParkourSystem`):
- **Enable Climbing** (`enableClimbing`, default `true`) — master switch.
- **Max Distance To Climb** (`maxDistanceToClimb`, default `1`) — radius of the search for the next point when shimmying/jumping from a hang (`OverlapSphere` on the Ledge layer; candidates up to 2 × this value away are considered).
- **Press Input To Climb** (`pressInputToClimb`, default `false`) — when `true`, from a braced hang holding Jump + forward plays a "try jump up" before mounting instead of mounting immediately.

`ClimbCore` (`FS_ParkourSystem`):
- **Hand Offsets** (`handOffsets`, default `(0, -0.08, 0.05)`) — hand position relative to the point while braced-hanging. Raise Y if palms sink into the ledge.
- **Hand Spacing** (`handSpacing`, default `0`) — default distance between hands; per-point override via manual options.
- **Foot Placement Offset** (`footPlacementOffset`, default `0.15`) — foot distance from the wall in braced hang.
- **Foot IK Ray Length** (`footIkRayLength`, default `0.5`) — how far the foot IK rays search for the wall.
- **Hip Ray Length** (`hipRayLength`, default `0.3`) — length of the wall check that decides braced vs free hang.
- **Wall Ray Offset** (`wallRayOffset`, default `(-0.15, -0.9, 0.14)`) — origin of the wall-check spherecasts relative to the point (x mirrored for each foot).

**Expected result:** After changing a value, re-enter Play and hang from the same point; hands/feet land on the surface without penetration, and braced/free hang resolves as intended.

### Workflow: Diagnose "won't grab" / "won't climb up"

**Goal:** Find why a ledge is ignored.

**Steps:**
1. Ledge layer: `LayerMask.LayerToName(ledge.layer) == "Ledge"`. Bake sets it; manually placed ledges often miss it.
2. Points exist: `ledge.GetComponentsInChildren<ClimbPoint>().Length > 0`.
3. Points face the player: `GetNearestPoint` rejects points whose `transform.forward` has a non-negative dot with the approach direction — arrows must point toward the side the player approaches from. Rotate the point 180° or re-bake with Both Side.
4. Distance: the nearest point must be within 2 m of the raycast hit on the ledge and at least 0.03 m away.
5. Space: `CheckSpaceForClimb` box-checks in front of and below the point on `FSSettings.ObstacleLayer`; a wall or prop there blocks the grab. Also nothing may block the line from the player's chest to just in front of the point.
6. Height: on foot, `ClimbLedgeCheck` casts 15 rays 1 m forward from 1 m to ~3.1 m above the player's feet. Higher ledges are only reachable by predictive climb (jump) or from another ledge.
7. Climb up: with `useManualOptions` off, the surface above the point must be within 0.2 m of the point's height and have standing room; otherwise use Mount Point.

**Expected result:** Each check passes; the grab works.

## Verification

- `ledge.layer == LayerMask.NameToLayer("Ledge")` for every ledge.
- `ledge.GetComponentsInChildren<FS_ParkourSystem.ClimbPoint>().Length >= 1`, and `>= 2` for any ledge longer than 1.5 × Distance Between Points.
- Each point's `transform.forward` points away from the mesh toward open space.
- In Play mode, after walking into the ledge holding Jump: `FindFirstObjectByType<FS_Core.PlayerController>().CurrentSystemState == SystemState.Climbing` and `FindFirstObjectByType<FS_ParkourSystem.ClimbController>().currentPoint != null`.
- No Console errors while hanging, shimmying and mounting.

## API quick reference

| Entry point | Type | What it does |
|---|---|---|
| `Tools/FS System/Parkour/ClimbPoint Editor` | EditorWindow (`FS_ParkourSystem.ClimbPointAndCreateCharacterEditorWindow`) | Bake / Clear Points on the selection; Distance Between Points, Both Side. |
| `FS_ParkourSystem.ClimbingPointUtils.BakePoints(GameObject, float, bool, float)` | static (Editor) | Generates points and Move connections along the longest axis. |
| `FS_ParkourSystem.ClimbingPointUtils.ClearPoints(List<GameObject>)` | static (Editor) | Removes child ClimbPoints. |
| `FS_ParkourSystem.ClimbPoint` | MonoBehaviour | `useManualOptions`, `MountPoint`, `handSpacing`, `Neighbours`, `CreateConnection(...)`. |
| `FS_ParkourSystem.ClimbController` | `SystemBase` (Priority 11) | `enableClimbing`, `maxDistanceToClimb`, `pressInputToClimb`, `currentPoint`, `HangType`, `ClimbToLedge(...)`, `DropFromPoint()`. |
| `FS_ParkourSystem.ClimbCore` | MonoBehaviour | Hand/foot offsets, `CheckWall(point)` (braced vs free), IK. |
| `FS_Core.EnvironmentScanner.LedgeLayer` | `LayerMask` | Forced to include `Ledge` at Start. |
| Tag `SwingableLedge` | tag on the ledge parent | Enables swinging in free hang. |

## Common issues

- **Symptom:** Bake does nothing for an object. **Cause:** No `MeshRenderer` on that object (e.g. the mesh is on a child). **Fix:** Select the object that owns the `MeshRenderer`, or add points by hand (add `ClimbPoint` to empty children at the edge, forward facing outward).
- **Symptom:** Points appear along the wrong edge. **Cause:** The baker uses the longest bounds axis at the top face. **Fix:** Split the mesh into one object per edge, or scale a thin invisible collider strip per edge and bake that.
- **Symptom:** Points visible in Hierarchy but not in Scene view. **Cause:** Gizmos off, or camera farther than 15 m (points only draw within 15 m). **Fix:** Enable Gizmos; move closer.
- **Symptom:** Grabs from one side only. **Cause:** Points face one way. **Fix:** Re-bake with **Both Side**.
- **Symptom:** Character hangs in free hang against a solid wall. **Cause:** Wall not on `FSSettings.ObstacleLayer`, or thicker gap than `hipRayLength` (0.3). **Fix:** Put the wall on an obstacle layer / increase Hip Ray Length.
- **Symptom:** Cannot shimmy to the next ledge segment. **Cause:** Neighbour beyond 2 × `maxDistanceToClimb`, or the two points' forward directions differ by more than 100°. **Fix:** Reduce the gap, increase Max Distance To Climb, or align the ledges.
- **Symptom:** Free hang won't go up to a higher point. **Cause:** By design: from free hang, upward targets more than 0.6 m higher (or any higher target farther than 1 m) are rejected. **Fix:** Give the wall a surface for braced hang or add an intermediate point.
- **Symptom:** Falling character does not catch the ledge. **Cause:** Jump not held during the fall, or the ledge is not within a 0.4 m sphere 1.5 m above and 0.2 m ahead of the player. **Fix:** Hold Jump while falling past the edge.

## Boundaries

- Points are authored per mesh; the system does not scan arbitrary geometry at runtime for hand-holds. Only Ledge-layer colliders with ClimbPoint children are climbable.
- Curved or diagonal edges are approximated by straight segments — bake several short ledge objects.
- Ceilings/overhangs and rope climbing are not supported.
- Ledge animations are shipped clips; custom hang animations require editing `Parkour Controller.controller` and are outside this skill.
- AI characters climbing requires FS Parkour And Climbing AI 2.
