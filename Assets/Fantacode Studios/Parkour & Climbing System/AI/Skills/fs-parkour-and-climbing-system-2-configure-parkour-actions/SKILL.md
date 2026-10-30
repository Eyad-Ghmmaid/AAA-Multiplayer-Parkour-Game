---
name: fs-parkour-and-climbing-system-2-configure-parkour-actions
description: "Use this skill whenever the user wants the character to vault, step up, hop over, climb onto low obstacles or wall-run with FS Parkour And Climbing System 2 — e.g. 'make him vault over this fence', 'he just bumps into the crate', 'add a new parkour animation', 'the vault picks the wrong animation', 'only vault objects tagged X', 'enable wall run', 'the hand doesn't touch the obstacle during the vault'. Covers obstacle detection (Obstacle layer), the shipped ParkourAction assets and their height bands, creating and baking a custom ParkourAction (target matching, Bake Curves), the parkourActions list, tag and speed filters, and Wall Run. Do NOT use for hanging from ledges (see fs-parkour-and-climbing-system-2-make-ledges-climbable) or jumping across gaps (see fs-parkour-and-climbing-system-2-tune-predictive-jumping). When in doubt whether a vault or obstacle request could involve FS Parkour And Climbing System 2, use this skill — Prerequisites shows how to confirm the asset is installed."
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

# Configure Vaults, Step-Ups and Wall Run

Parkour actions are **ParkourAction** ScriptableObjects: an animation clip plus the obstacle conditions under which it may play (height band, vault type, tag, width, speed) and target-matching data so a hand or foot lands exactly on the obstacle. When the player holds Jump while grounded and facing an obstacle, `ParkourController` measures the obstacle and plays the first matching action from its `parkourActions` list. This skill lets an agent make obstacles vaultable, adjust which action plays, add new actions, and enable wall running.

## When to use this skill

- "Make the player vault over this fence / crate / railing."
- "He runs into the box instead of climbing it."
- "Add my own vault animation."
- "Change which height triggers the climb-up."
- "Only vault objects with tag `Vaultable`."
- "Turn on wall run."
- "The hand goes through the obstacle during the vault."

Not for:
- Hanging and climbing ledges — `fs-parkour-and-climbing-system-2-make-ledges-climbable`.
- Jumping to platforms across gaps — `fs-parkour-and-climbing-system-2-tune-predictive-jumping`.
- Sliding — `fs-parkour-and-climbing-system-2-add-sliding`.
- Building the player — `fs-parkour-and-climbing-system-2-create-parkour-player`.

## Prerequisites

- A player with `FS_ParkourSystem.ParkourController` on its `Player` object: `Object.FindFirstObjectByType<FS_ParkourSystem.ParkourController>() != null`.
- Install check: `System.Type.GetType("FS_ParkourSystem.ParkourAction, Assembly-CSharp") != null`. If null, tell the user to import FS Parkour And Climbing System 2 from https://assetstore.unity.com/packages/tools/game-toolkits/fs-parkour-and-climbing-system-2-387610.
- The obstacle has a collider on a layer contained in `FSSettings.ObstacleLayer` (`FSSettings` component on `Player`; default mask is `Default`, and `Ledge` is added automatically at runtime).
- For custom actions: an `AnimationClip` with root motion (humanoid), and the `Third Person Controller/Resources/Default Character.prefab` (used as the reference animator when baking).

## Quick start

Goal: a 1 m tall box becomes vaultable.

1. Give the box a collider and leave it on layer `Default` (or any layer included in `Player` → `FSSettings` → **Obstacle Layer**).
2. Make sure its top is flat (surface normal within 45° of up) and its face is vertical (normal more than 45° from up).
3. Press Play, run at the box and hold **Space**.

Expected observable result: at 0.7–1.5 m the character plays **VaultOver** if the box is thin (no landing surface on top) or **VaultOn** if it is wide enough to land on; 0.3–0.8 m plays **MediumStepUp**; 0.2–0.3 m plays **StepUp**; 1.5–3.5 m plays **ClimbUp**. `PlayerController.CurrentSystemState` is `Parkour` during the action.

## Workflows

### Workflow: Understand and adjust obstacle detection

**Goal:** Know why an obstacle is or is not picked up.

Detection (`FS_Core.EnvironmentScanner.ObstacleCheck`, run every FixedUpdate while Jump is held and the player is grounded):
1. A box cast forward from the player over **Obstacle Check Range** (`obstacleCheckRange`, default `0.7` m) against `FSSettings.ObstacleLayer` finds the face (`forwardHit`).
2. From `heightRayLength` (default `4` m) above the hit point a sphere cast down finds the top (`heightHit`).
3. `hasSpace` — a box check ahead of the obstacle confirms room to land; `hasSpaceToVault` — a sphere cast down beyond the obstacle's near edge finds a surface on top within 1 m (wide obstacle).
4. `ParkourController.HandleParkourAction` requires: face normal more than 45° from up, top normal within 45° of up, `hasSpace`, and not already in an action.
5. Each `ParkourAction.CheckIfPossible(hitData, player)` is evaluated in list order; the first passing action whose `movementThreshold` ≤ the animator's `moveAmount` plays. If none passes the speed filter, a random passing action plays.

**Steps to adjust:**
- Change the reach: `Player` → `EnvironmentScanner` → **Obstacle Check Range**.
- Include a custom layer: `Player` → `FSSettings` → **Obstacle Layer**.

**Expected result:** Standing in front of the obstacle holding Jump triggers an action within the check range.

### Workflow: Change which action plays, and its height range

**Goal:** Tune the shipped actions.

Shipped assets in `Assets/Fantacode Studios/Parkour & Climbing System/Resources/Parkour Actions/` (values verified in the assets):

| Asset | Min–Max height (m) | Vault Type | Notes |
|---|---|---|---|
| StepUp | 0.2–0.3 | VaultOn | Foot-matched (Right Foot). |
| MediumStepUp | 0.3–0.8 | Any | Right Foot, Linear warp. |
| MediumStepUpM | 0.3–0.8 | Any | Mirrored variant (Left Foot); not in the default list. |
| VaultOver | 0.7–1.5 | VaultOver | Requires a thin obstacle (no surface to land on). Left Hand. |
| VaultOn | 0.8–1.5 | VaultOn | Requires a surface to land on. Left Hand. |
| ClimbUp | 1.5–3.5 | VaultOn | Right Hand. |
| Wall Run | 0–0 | Any | Used only through `wallRunAction`, not the list. |

Default `ParkourController.parkourActions` on the shipped prefab: `StepUp, MediumStepUp, VaultOver, VaultOn, ClimbUp`.

**Steps:**
1. Select the action asset. Edit **Min Height** / **Max Height** (obstacle top relative to the player's feet).
2. **Vault Type**: `VaultOver` only passes when there is no landing surface on top (`!hasSpaceToVault`) unless **Minimum Obstacle Horizontal Distance** > 0; `VaultOn` only passes when there is (`hasSpaceToVault`); `Any` ignores it.
3. **Obstacle Tag** — if set, only obstacles with exactly that tag pass.
4. **Movement Threshold** — required `moveAmount` (0 idle … 1 sprint) for this action to be preferred; e.g. `0.9` makes an action sprint-only.
5. **Minimum Obstacle Horizontal Distance** — for `VaultOver` on wide obstacles: the far edge must be within this distance; the match position moves to the far edge.
6. Reorder the list on `Player` → `ParkourController` → **Parkour Actions**: earlier entries win when several pass.

**Expected result:** The intended action plays for the given obstacle height; unrelated obstacles are untouched.

### Workflow: Create a custom ParkourAction

**Goal:** Add a new animation as a parkour action.

**Steps:**
1. In the Project window, right-click → **Create > Parkour System > New Parkour Action**. Name it (e.g. `HighVault`).
2. **Clip Info** → **Clip**: assign the AnimationClip. Transition in/out defaults to 0.2 s; other `AnimGraphClipInfo` fields (speed, gravity, events) are optional.
3. Set **Min Height**, **Max Height**, **Vault Type**, **Use Hands** (leave on for hand-supported moves), **Rotate To Obstacle** (face the obstacle before playing; on for all shipped actions).
4. **Target Matching** (on by default): choose **Match Body Part** (the `AvatarTarget` that must touch the obstacle — `LeftHand`, `RightHand`, `LeftFoot`, `RightFoot`), **Match Start Time** and **Match Target Time** (normalized clip times between which the root is warped so the body part reaches the obstacle), **Match Pos Weight** (`(0,1,0)` = height only; `(0,1,1)` = height and depth, used by the vaults), **Warp Match Type** (`ScaleRootMotion` scales the clip's root curves — best for vertical moves; `Linear` interpolates evenly).
5. **Bake Curves** (bottom of the Inspector): **Reference Animator** is pre-filled from `Resources/Default Character.prefab` — it must be a prefab, not a scene object. Click **Bake Curves**. This samples the clip through the humanoid avatar and writes **Root Motion Curves** and **Bone Root Offset**. Console logs `[ParkourActionEditor] Baked '<name>'…`.
6. Re-bake whenever the clip, Match Body Part or Match Target Time changes. Do not hand-edit the baked curves.
7. Add the asset to `Player` → `ParkourController` → **Parkour Actions**, ordered by priority.

Programmatic creation (Editor):

```csharp
using UnityEditor;
using UnityEngine;
using FS_ParkourSystem;

var action = ScriptableObject.CreateInstance<ParkourAction>();
AssetDatabase.CreateAsset(action, "Assets/Parkour Actions/HighVault.asset");
// Fields other than clipInfo/movementThreshold/rootMotionCurves are private [SerializeField];
// set them through a SerializedObject:
var so = new SerializedObject(action);
so.FindProperty("clipInfo.clip").objectReferenceValue = AssetDatabase.LoadAssetAtPath<AnimationClip>("Assets/Anims/HighVault.anim");
so.FindProperty("minHeight").floatValue = 1.2f;
so.FindProperty("maxHeight").floatValue = 1.8f;
so.FindProperty("vaultType").enumValueIndex = (int)VaultType.VaultOver;
so.FindProperty("matchBodyPart").enumValueIndex = (int)AvatarTarget.LeftHand;
so.FindProperty("matchStartTime").floatValue = 0.1f;
so.FindProperty("matchTargetTime").floatValue = 0.4f;
so.FindProperty("matchPosWeight").vector3Value = new Vector3(0, 1, 1);
so.ApplyModifiedProperties();
AssetDatabase.SaveAssets();
// Then select the asset and click "Bake Curves" in the Inspector (baking has no public API).
```

**Expected result:** Facing an obstacle in the new height band and holding Jump plays the new clip; the chosen body part touches the obstacle top at Match Target Time.

### Workflow: Enable wall run

**Goal:** Running at a tall wall with Jump held runs up it; pressing Jump again jumps away.

**Steps:**
1. `Player` → `ParkourController` → tick **Enable Wall Run**.
2. Assign **Wall Run Action** = `Resources/Parkour Actions/Wall Run.asset` (it appears when the toggle is on).
3. Condition at runtime: Jump held, grounded, a forward hit whose normal is more than 60° from up, no other parkour action chosen, not falling. During the run the player turns toward the wall; pressing Jump triggers a predictive jump backwards (`HandlePredictiveJumpAndClimb(-forward)`).

**Expected result:** Character runs up the wall for the clip's duration, then falls (`ClimbController.isFalling = true`) or jumps away on Jump.

## Verification

- `FindFirstObjectByType<FS_ParkourSystem.ParkourController>().parkourActions.Count >= 5` after setup (default list) or includes the custom asset.
- Custom asset: `action.ClipInfo.clip != null`, `action.RootMotionCurves` curves have keys, and `action.BoneRootOffset != Vector3.zero` when a hand/foot is matched (zero when `Root`/`Body`).
- Obstacle: `((1 << obstacle.layer) & FS_Core.FSSettings.i.ObstacleLayer) != 0` in Play mode, and its top height minus player feet height falls inside exactly one intended action band.
- Play test: `PlayerController.CurrentSystemState` becomes `Parkour` while the action plays and returns to `Locomotion` after; no Console errors; `[ParkourActionEditor] No clip assigned` never appears.

## API quick reference

| Entry point | Type | What it does |
|---|---|---|
| `Create > Parkour System > New Parkour Action` | CreateAssetMenu (`FS_ParkourSystem.ParkourAction`) | New action asset. |
| `FS_ParkourSystem.ParkourAction.CheckIfPossible(ObstacleHitData, Transform)` | virtual method | The selection rule; override in a subclass for custom conditions. |
| `FS_ParkourSystem.ParkourController.parkourActions` | `List<ParkourAction>` | Ordered candidate list. |
| `FS_ParkourSystem.ParkourController.EnableWallRun` / `wallRunAction` | bool / `ParkourAction` | Wall run switch and clip. |
| `FS_Core.EnvironmentScanner.ObstacleCheck(bool, float)` | method | Returns `ObstacleHitData` (`forwardHit`, `heightHit`, `hasSpace`, `hasSpaceToVault`, `ledgeHit`). |
| `FS_Core.FSSettings.ObstacleLayer` | `LayerMask` | Which layers count as obstacles. |
| `FS_ParkourSystem.ParkourCore.DoParkourAction(ParkourAction)` | `IEnumerator` | Plays an action from code: `StartCoroutine(parkourCore.DoParkourAction(action))`. |
| `FS_ParkourSystem.ParkourActionEditor` → **Bake Curves** | Inspector button | Bakes root motion curves and bone-root offset (Editor only). |

## Common issues

- **Symptom:** Nothing happens at the obstacle. **Cause:** Obstacle layer not in `FSSettings.ObstacleLayer`, collider missing, or top surface too steep. **Fix:** Layer/collider as above; flatten the top.
- **Symptom:** Character vaults where you expected a step-up. **Cause:** Height band overlap (0.7–0.8 m is shared by MediumStepUp and VaultOver); list order decides. **Fix:** Narrow the bands or reorder the list.
- **Symptom:** VaultOver never plays on a wide box. **Cause:** `VaultOver` requires no landing surface (thin obstacle). **Fix:** Use VaultOn, or set Minimum Obstacle Horizontal Distance to the box depth.
- **Symptom:** Hand hovers above or sinks into the obstacle. **Cause:** Curves not re-baked after changing clip/body part/time, or wrong Match Pos Weight. **Fix:** Bake Curves again; use `(0,1,1)` for vaults.
- **Symptom:** `[ParkourActionEditor] No reference animator provided`. **Cause:** Reference Animator empty (Default Character prefab missing) or a scene object was dragged. **Fix:** Drag a prefab with a humanoid Animator.
- **Symptom:** Action plays while walking slowly but not when sprinting (or vice versa). **Cause:** `movementThreshold` ordering. **Fix:** Set the threshold on the sprint action and list it first.
- **Symptom:** Wall Run field not visible. **Cause:** `[ShowIf("EnableWallRun")]`. **Fix:** Tick Enable Wall Run first.

## Boundaries

- Actions are chosen only from a forward obstacle check; there is no sideways or backwards parkour.
- One action per obstacle contact; chaining is not automatic.
- Clips must be humanoid with root motion; in-place clips will not travel over the obstacle.
- Predictive Actions (`PredictiveAction` assets under `Resources/Predictive Actions`) are inactive in this version and should not be used.
- Obstacles taller than 3.5 m need ledges (ClimbPoints) instead of a parkour action.
