---
name: fs-parkour-and-climbing-system-2-create-parkour-player
description: "Use this skill whenever the user wants a playable character that can run, jump, vault and climb using FS Parkour And Climbing System 2 — e.g. 'add a player to my scene', 'make my character do parkour', 'use my own model with the parkour controller', 'set up the fantacode player', 'the parkour prefab uses the wrong character', 'create a third person controller with climbing'. Covers the FS Systems window Setup tab (Player type, Locomotion + Parkour and Climbing, custom humanoid model), the drag-and-drop Parkour Player prefab, what gets generated, and how to verify it. Do NOT use for making ledges climbable (see fs-parkour-and-climbing-system-2-make-ledges-climbable), for tuning vaults or jumps (see fs-parkour-and-climbing-system-2-configure-parkour-actions and -tune-predictive-jumping), or for AI characters. When in doubt whether a 'put a character in the scene' request could involve FS Parkour And Climbing System 2, use this skill — Prerequisites shows how to confirm the asset is installed."
metadata:
  asset: "FS Parkour And Climbing System 2"
  publisher: "Fantacode Studios"
  asset-version: "2.0.1"
  skill-version: "1.0.0"
  unity: "6000.0+"
  render-pipelines: "Built-in, URP, HDRP"
  category: "tools/game-toolkits"
  asset-store-url: "https://assetstore.unity.com/packages/tools/game-toolkits/fs-parkour-and-climbing-system-2-387610"
  documentation-url: "https://fantacode.gitbook.io/fantacode-studios/fantacode-studios/fs-parkour-and-climbing-system/quick-start"
  support-url: "mailto:studios@fantacode.com"
  last-verified: "2026-09-13"
---

# Create a Parkour-Ready Player

This skill puts a controllable third-person character with parkour, climbing and sliding into the open scene. There are two routes: the **FS Systems window** builds a player around any humanoid model the user supplies (the normal route), and the **Parkour Player prefab** is a ready-made drop-in that is locked to the bundled Default Character model. Both produce a root object containing the player, its camera and its UI.

## When to use this skill

- "Add a parkour player to this scene."
- "I want to use my own character model with the climbing system."
- "Create a third-person controller that can climb and vault."
- "The player prefab has the wrong model, how do I swap it?"
- "Set up a player with the FS window."

Not for:
- Making walls grabbable or tuning hanging — `fs-parkour-and-climbing-system-2-make-ledges-climbable`.
- Which obstacles get vaulted — `fs-parkour-and-climbing-system-2-configure-parkour-actions`.
- Jump reach / platform detection — `fs-parkour-and-climbing-system-2-tune-predictive-jumping`.
- Input rebinding or mobile controls — `fs-parkour-and-climbing-system-2-customize-input`.
- AI characters (needs FS Parkour And Climbing AI 2, a separate asset).

## Prerequisites

- FS Parkour And Climbing System 2 imported and project settings imported (layers `Player`, `Ledge`, `FootTrigger` exist). Check: `System.Type.GetType("FS_ParkourSystem.ParkourController, Assembly-CSharp") != null && LayerMask.NameToLayer("Player") != -1`. If the type is null, tell the user to import the asset from https://assetstore.unity.com/packages/tools/game-toolkits/fs-parkour-and-climbing-system-2-387610; if the layer is missing, run `Tools > FS System > FS Window` → Welcome → **Import Project Settings**.
- For a custom model: a prefab or scene object with an `Animator` whose Avatar is **Humanoid** and valid (`animator.isHuman && animator.avatar.isValid`). Set the model's import settings Rig > Animation Type to Humanoid first. The FS window refuses anything else.
- The scene should not already contain another camera tagged `MainCamera` or another `EventSystem`; the generated player brings its own.

## Quick start

Goal: player with the user's own model.

1. Open the target scene. Delete the default `Main Camera` (the player root brings a `Third Person Camera` tagged `MainCamera`).
2. `Tools > FS System > FS Window` → **Setup** tab. (Or `Tools > FS System > Parkour > Create Character`, which opens the same tab with Parkour pre-ticked.)
3. Character type dropdown at the top: **Player**. Click **Load Systems** if the list is empty.
4. Tick **Locomotion** (ticked by default and required) and **Parkour and Climbing**. The list shows every installed Fantacode system (each package registers itself with the window); if the project also owns Melee Combat, Shooter, Swimming, Grappling Hook, etc., tick them here too and one character is built with all of them — this is the intended way to combine FS systems.
5. Drag the humanoid model prefab from the Project window onto the character preview area. A validation message drawn over the preview means the model is invalid (no Animator / not Humanoid / invalid avatar).
6. Leave **Animator Name** as `Combined Controller` (or name it after the character).
7. Click **Setup**.

Expected observable result: the Hierarchy contains a new root `FS Player` with children `Player` (layer `Player`), `Third Person Camera`, `Game UI`, `Quick Switch UI Canvas`, `EventSystem`; the model is a child of `Player`; the Console logs `A new Animator Controller named Combined Controller has been created…`; `Assets/Fantacode Studios/Third Person Controller/Resources/AnimatorControllers/Combined Controller.controller` exists. Pressing Play lets you move with WASD and hold Space to vault/climb.

## Workflows

### Workflow: Build a player from a custom model (FS Systems window)

**Goal:** Full parkour player wrapped around the user's humanoid model.

**Steps:**
1. Follow Quick start steps 1–7.
2. What **Setup** does (source: `FSSystemsSetupEditorWindow.HandleSetup` / `SetupPlayer`):
   - Copies `Resources/AnimatorControllers/Third Person Controller.controller` to `<Animator Name>.controller` in the same folder (adds ` 1`, ` 2`… if the name is taken) and merges each selected system's animator into it.
   - Instantiates `Resources/Locomotion Controller.prefab`, renames the root `FS Player`, parents the model under `Player`, points the camera's follow targets at the player and a generated `Head Follow Target` under the head bone.
   - Adds `FootTrigger` prefabs on both feet (layer `FootTrigger`), `Right Hand Item Handler`/`Left Hand Item Handler` empties, a `CharacterID`, and sets `Player` to layer `Player`.
   - For **Parkour and Climbing** it copies every component from `Resources/Parkour Controller.prefab`'s player object onto `Player` (`ParkourController`, `ClimbController`, `ClimbCore`, `ParkourCore`, `SlidingController`, `ParkourInputManager`, plus the Third Person Controller components) and instantiates `Parkour Mobile Controller` under `Game UI`, wiring `ParkourInputManager.SetParkourUI`.
   - Orders `PlayerController.managedScripts` by `SystemBase.Priority` descending.
3. Position `FS Player` where the character should start (move the root, not `Player`).

**Expected result:** `FS Player/Player` has, among others: `PlayerController`, `LocomotionController`, `LocomotionCore`, `CharacterCapsule`, `CharacterControllerDriver`, `EnvironmentScanner`, `FSSettings`, `ActionExecutor`, `AnimGraph`, `BoneMapper`, `Damagable`, `InputManagerHub`, `LocomotionInputManager`, `ParkourInputManager`, `ParkourController`, `ClimbController`, `ClimbCore`, `ParkourCore`, `SlidingController`. Its `Animator` uses the new `Combined Controller` and the model's avatar.

### Workflow: Drop in the ready-made prefab

**Goal:** Fastest possible test, model choice does not matter.

**Steps:**
1. Delete the scene's default `Main Camera`.
2. Drag `Assets/Fantacode Studios/Parkour & Climbing System/Prefabs/Parkour Player.prefab` into the Hierarchy.
3. Position the root `Parkour Player` on the ground.

**Expected result:** Root `Parkour Player` with `Player` (layer `Player`, model `Default Character(Clone)` inside), `Third Person Camera` (tag `MainCamera`), `Game UI`, `Quick Switch UI Canvas`, `EventSystem`, `HP Bar`. Play mode works immediately. This prefab is bound to the bundled Default Character; to use a different model, use the FS Systems window workflow instead of trying to swap the mesh inside this prefab (bone mapping, foot triggers, item handlers and the head follow target are all generated per model).

### Workflow: Add parkour to an existing FS Third Person Controller player

**Goal:** The scene already has an `FS Player` built with only Locomotion.

**Steps:**
1. The FS window does not modify an existing player. Rebuild: note the model used and which systems the old player had, delete the old `FS Player`, run Workflow: Build a player from a custom model with **Parkour and Climbing** plus every other system the player needs ticked. Rebuilding with all systems selected is simpler and more reliable than copying components.
2. If rebuilding is not acceptable, copy components manually: select `Resources/Parkour Controller.prefab` → its `Player` child → for each of `ParkourCore`, `ClimbCore`, `ParkourController`, `ClimbController`, `SlidingController`, `ParkourInputManager`, use the component menu **Copy Component** then **Paste Component As New** on the existing `Player`. Then `Tools > FS System > Merge Animator Controllers` (Source Controller = `Parkour & Climbing System/Animators/Parkour Controller.controller`, Target Controller = the player's controller, **Merge**), and add `Resources/Parkour Mobile Controller.prefab` under `Game UI` if mobile UI is used. Finally register the new systems: run `player.GetComponent<FS_Core.PlayerController>().FindSystems()` from an Editor script, or add them to the `PlayerController` → `Managed Scripts` list in the Inspector.

**Expected result:** Same component set as the FS-window build; `ParkourController.parkourActions` lists `StepUp`, `MediumStepUp`, `VaultOver`, `VaultOn`, `ClimbUp`.

### Workflow: Set the spawn position and camera

**Goal:** Player starts at a specific place, camera behaves.

**Steps:**
1. Move the root (`FS Player` / `Parkour Player`), not the `Player` child — the camera rig is a sibling and follows at runtime.
2. Camera settings live on `Third Person Camera` → `CameraController` (`thirdPersonCamera.defaultSettings`, `firstPersonCamera.defaultSettings`).
3. The player's `CharacterID` component must be unique per character (auto-generated). Duplicating the whole root in the scene keeps the same ID; prefer instantiating from a prefab.

**Expected result:** Character appears at the root's position; camera orbits with mouse / right stick.

## Verification

Run in Play mode or as an Editor check:

```csharp
var pc = Object.FindFirstObjectByType<FS_Core.PlayerController>();
bool ok = pc != null
    && pc.GetComponent<FS_ParkourSystem.ParkourController>() != null
    && pc.GetComponent<FS_ParkourSystem.ClimbController>() != null
    && pc.GetComponent<FS_ParkourSystem.ClimbCore>() != null
    && pc.GetComponent<FS_ParkourSystem.ParkourCore>() != null
    && pc.gameObject.layer == LayerMask.NameToLayer("Player")
    && pc.GetComponentInChildren<Animator>().isHuman
    && pc.GetComponentInChildren<Animator>().runtimeAnimatorController != null;
Debug.Log(ok ? "PASS player set up" : "FAIL player set up");
```

Also confirm:
- Exactly one object tagged `MainCamera` in the scene and one `EventSystem`.
- Two `FootTrigger` objects exist under the model's foot bones (layer `FootTrigger`).
- In Play mode, `pc.CurrentSystemState` is `Locomotion` while idle and becomes `Parkour` / `Climbing` during actions.
- Console has no `NullReferenceException` from `FS_` namespaces on entering Play.

## API quick reference

| Entry point | Type | What it does |
|---|---|---|
| `Tools/FS System/ FS Window` → Setup tab | EditorWindow (`FS_Core.FSSystemsSetupEditorWindow`) | Builds Player or AI characters from selected systems + humanoid model. |
| `Tools/FS System/Parkour/Create Character` | MenuItem | Opens the Setup tab with Parkour and Climbing pre-selected. |
| `Resources/Locomotion Controller.prefab` | prefab (Third Person Controller) | Base root instantiated by Setup (`FS Player`). |
| `Resources/Parkour Controller.prefab` | prefab | Component donor for parkour; not meant to be dropped in scenes directly. |
| `Prefabs/Parkour Player.prefab` | prefab | Complete drop-in player with the Default Character model. |
| `FS_Core.PlayerController.managedScripts` | `List<SystemBase>` | All systems on the player, ordered by priority. |
| `FS_Core.PlayerController.CurrentSystemState` | `SystemState` | `Locomotion`, `Parkour`, `Climbing`, `Sliding`, … |
| `Tools/FS System/Merge Animator Controllers` | EditorWindow (`AnimatorMerger`) | Merge a system animator into an existing controller. |

## Common issues

- **Symptom:** Setup button does nothing. **Cause:** Model invalid (`ValidForCreateCharacter` false). **Fix:** Model needs an `Animator`, Humanoid rig, valid avatar; check the red overlay on the preview.
- **Symptom:** Two cameras / split view after adding the player. **Cause:** Scene's original `Main Camera` still present. **Fix:** Delete it; keep `Third Person Camera`.
- **Symptom:** Character falls through the floor. **Cause:** Ground collider not on a layer in `FSSettings.GroundLayer` (default `Default`, plus `Ledge`/`Hotspot` auto-added). **Fix:** Put ground on `Default` or add its layer to `FSSettings` → Ground Layer on `Player`.
- **Symptom:** Character T-poses. **Cause:** Animator controller missing or avatar mismatch. **Fix:** `Player` → `Animator` → assign the generated `Combined Controller` and the model's avatar.
- **Symptom:** Model swapped inside `Parkour Player.prefab` and hands/feet misbehave. **Cause:** Foot triggers, bone mapper and item handlers were generated for the original model. **Fix:** Build with the FS window instead.
- **Symptom:** `Combined Controller 1`, `Combined Controller 2`… pile up. **Cause:** Each Setup creates a new controller. **Fix:** Delete unused ones in `Third Person Controller/Resources/AnimatorControllers/`.

## Boundaries

- Humanoid rigs only; no Generic rig support.
- One player per scene is the supported configuration (singletons such as `FSSettings.i`).
- The FS window cannot add systems to an already-built player; rebuild or copy components by hand.
- Character stats, health UI and item equipping come from the Third Person Controller and are outside this skill.
- AI parkour requires the separate FS Parkour And Climbing AI 2 asset.
