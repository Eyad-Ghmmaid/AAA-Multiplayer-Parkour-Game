---
name: fs-parkour-and-climbing-system-2-setup-and-overview
description: "Use this skill whenever the user has, wants to install, or asks anything general about FS Parkour And Climbing System 2 by Fantacode Studios — e.g. 'is the parkour asset installed', 'set up the fantacode parkour system', 'what does this parkour package include', 'the FS window keeps popping up', 'import the layers and tags for climbing', 'where are the demo scenes', 'switch the parkour asset to the new input system'. Covers verifying the install, first-time project setup (layers, tags, input-system define), package layout, demo scenes, and an index of the other skills. Do NOT use for creating the player (see fs-parkour-and-climbing-system-2-create-parkour-player), authoring ledges (see fs-parkour-and-climbing-system-2-make-ledges-climbable), or tuning jumps, vaults, sliding or input (see the sibling skills). When in doubt whether a parkour, climbing, vaulting or ledge request could involve FS Parkour And Climbing System 2, use this skill — Prerequisites shows how to confirm the asset is installed."
metadata:
  asset: "FS Parkour And Climbing System 2"
  publisher: "Fantacode Studios"
  asset-version: "2.0.1"
  skill-version: "1.0.0"
  unity: "6000.0+"
  render-pipelines: "Built-in, URP, HDRP"
  category: "tools/game-toolkits"
  asset-store-url: "https://assetstore.unity.com/packages/tools/game-toolkits/fs-parkour-and-climbing-system-2-387610"
  documentation-url: "https://fantacode.gitbook.io/fantacode-studios/fantacode-studios/fs-parkour-and-climbing-system"
  support-url: "mailto:studios@fantacode.com"
  last-verified: "2026-09-13"
---

# Set Up FS Parkour And Climbing System 2

FS Parkour And Climbing System 2 is a third-person traversal system for Unity: obstacle-driven parkour actions (step-ups, vaults, climb-ups, wall run), predictive jumping to platforms and ledges, IK-driven ledge climbing with braced and free hang, and slope sliding. It ships with the Fantacode Third Person Controller (locomotion, camera, input, player system framework) and is driven entirely from Inspector settings and ScriptableObject assets; full C# source is included. This skill lets an agent confirm the asset is installed, complete the one-time project setup, find its way around the package, and pick the right sibling skill for a task.

## When to use this skill

- "Is the Fantacode parkour system installed in this project?"
- "Set up the FS parkour and climbing asset."
- "Import the layers and tags the climbing system needs."
- "Switch the parkour controller to the new Input System."
- "Where are the parkour demo scenes / prefabs / actions?"
- "What can this parkour asset actually do?"
- Any parkour, climbing, vault or ledge request in a project that has an `Assets/Fantacode Studios` folder.

Not for:
- Putting a controllable character in the scene — `fs-parkour-and-climbing-system-2-create-parkour-player`.
- Making ledges grabbable or tuning hang/IK — `fs-parkour-and-climbing-system-2-make-ledges-climbable`.
- Vaults, step-ups, climb-ups, wall run — `fs-parkour-and-climbing-system-2-configure-parkour-actions`.
- Jump-to-platform behaviour — `fs-parkour-and-climbing-system-2-tune-predictive-jumping`.
- Slide surfaces — `fs-parkour-and-climbing-system-2-add-sliding`.
- Rebinding keys or mobile buttons — `fs-parkour-and-climbing-system-2-customize-input`.

## Prerequisites

- Unity 6000.0 or newer. The package is render-pipeline agnostic (it contains no pipeline-specific shaders); the demo scene materials are authored for URP and appear magenta in Built-in or HDRP, which does not affect the system.
- Unity packages: Input System is required only if the project uses the new Input System (see Workflow: Enable the new Input System).
- The asset compiles into `Assembly-CSharp` (no assembly definitions).

**Programmatic install check.** Run this in the Editor (for example from a temporary Editor script or an Immediate-mode console) before doing anything else:

```csharp
using UnityEditor;
using UnityEngine;

public static class FSParkourInstallCheck
{
    [MenuItem("Tools/Check FS Parkour Install")]
    public static void Check()
    {
        bool parkourFolder = AssetDatabase.IsValidFolder("Assets/Fantacode Studios/Parkour & Climbing System");
        bool tpcFolder     = AssetDatabase.IsValidFolder("Assets/Fantacode Studios/Third Person Controller");
        bool parkourType   = System.Type.GetType("FS_ParkourSystem.ParkourController, Assembly-CSharp") != null;
        bool climbType     = System.Type.GetType("FS_ParkourSystem.ClimbController, Assembly-CSharp") != null;
        bool playerType    = System.Type.GetType("FS_Core.PlayerController, Assembly-CSharp") != null;
        bool ledgeLayer    = LayerMask.NameToLayer("Ledge") != -1;

        Debug.Log($"FS Parkour install — folder:{parkourFolder} tpcFolder:{tpcFolder} ParkourController:{parkourType} ClimbController:{climbType} PlayerController:{playerType} LedgeLayer:{ledgeLayer}");
    }
}
```

Interpretation:
- All `true` → installed and project settings imported. Proceed.
- Folders `true` but types `false` → the project has compile errors. Open the Console, fix them, then re-check.
- Folders `false` → the asset is not imported. Tell the user: "FS Parkour And Climbing System 2 is not in this project. Import it from the Asset Store: https://assetstore.unity.com/packages/tools/game-toolkits/fs-parkour-and-climbing-system-2-387610 (Window > Package Manager > My Assets)."
- `LedgeLayer` `false` → run Workflow: Import project settings.

Equivalent manual check: the menu `Tools > FS System > Parkour > ClimbPoint Editor` exists only when the asset is installed.

## Quick start

Goal: a freshly imported project, ready to build a parkour character.

1. Import the package (Window > Package Manager > My Assets > FS Parkour And Climbing System 2 > Import). Accept all files. Both `Assets/Fantacode Studios/Parkour & Climbing System` and `Assets/Fantacode Studios/Third Person Controller` are created.
2. Wait for the recompile. The **FS Systems** window opens by itself after every `.unitypackage` import (`FSSystemsSetupEditorWindow.OnImportPackageCompleted`) and once per project open unless "Don't Show on Start" is ticked. When it opens on first load it also runs `ImportProjectSettings()` automatically.
3. If the window did not open, open it: `Tools > FS System > FS Window` (the menu item text has a leading space; its exact `MenuItem` path is `Tools/FS System/ FS Window`).
4. On the **Welcome** tab click **Import Project Settings**. This is idempotent — safe to click again.
5. Check the Console for `[FS Systems]` errors. None expected.
6. Open `Assets/Fantacode Studios/Parkour & Climbing System/Demo/Demo Scene 1/DemoScene.unity` and press Play. Move with WASD, hold Space toward obstacles and gaps, walk into ledges to grab them. Press Enter to toggle the on-screen control panel.

Expected observable result: Project Settings > Tags and Layers now lists the layers `Ledge`, `Player`, `Enemy`, `RagdollBone`, `Deadbody`, `NPC`, `Vehicle`, `VisionSensor`, `FootTrigger`, `Cover`, `Hotspot`, `PlayerAlly`, `EnvironmentFinisherProp`, `OcuppantObject` and the tags `Hitbox`, `NarrowBeam`, `SwingableLedge`, `Slide`, `NavmeshLink`, `NavmeshSurface`; the demo scene plays without Console errors.

## Workflows

### Workflow: Import project settings

**Goal:** Create the layers, tags, collision matrix entries and scripting define the system depends on.

**Steps:**
1. `Tools > FS System > FS Window` → **Welcome** tab → **Import Project Settings**.
2. Or run it from code (Editor only):

```csharp
// Opens the window (which creates the hidden FSSystemsSetup helper) and imports settings.
FS_Core.FSSystemsSetupEditorWindow.ShowWindow();
FS_Core.FSSystemsSetupEditorWindow.setupScript.ImportProjectSettings();
```

What `FSSystemsSetup.ImportProjectSettings()` does (source: `Third Person Controller/Scripts/Util/FSSystemsSetup.cs`):
- Adds each missing layer from `FSSystemsSetup.layers` into the first empty user layer slot (index 8+). It never overwrites an existing layer name.
- Adds each missing tag from `FSSystemsSetup.tags`.
- Sets `Physics.IgnoreLayerCollision` for `RagdollBone` against `Player`, `Enemy`, `PlayerAlly`, `NPC`.
- If Project Settings > Player > Active Input Handling is **Input System Package**, adds the `inputsystem` scripting define so the FS input managers read the new Input System.

**Expected result:** Layers and tags listed in Quick start exist; no `[FS Systems Setup] Cannot set collision…` warnings in the Console. If the project already used all 24 user layers, a layer is silently skipped — check `LayerMask.NameToLayer("Ledge") != -1` and free a slot if needed.

### Workflow: Enable the new Input System

**Goal:** Make the FS input managers read `FSSystemsInputAction` instead of legacy `Input.GetKey`.

**Steps:**
1. Install the Input System package (Window > Package Manager > Unity Registry > Input System). Set Project Settings > Player > Active Input Handling to **Input System Package** or **Both**; let Unity restart the Editor.
2. `Tools > FS System > FS Window` → **Dependencies** tab → tick **New Input System** → confirm the dialog.
3. This toggles the `inputsystem` scripting define (`ScriptingDefineSymbolController.ToggleScriptingDefineSymbol("inputsystem", true)`) and triggers a script reimport.

**Expected result:** Project Settings > Player > Scripting Define Symbols contains `inputsystem`; controls still work in the demo scene. Bindings now come from `Assets/Fantacode Studios/Third Person Controller/InputAction/FSSystemsInputAction.inputactions` (see `fs-parkour-and-climbing-system-2-customize-input`).

### Workflow: Explore the demo scenes

**Goal:** See every feature working before replicating it.

**Steps:**
1. Open `Assets/Fantacode Studios/Parkour & Climbing System/Demo/Demo Scene 1/DemoScene.unity` (full obstacle course) or `Demo/Demo Scene 2/Demo Scene 2.unity` (second course).
2. Press Play. Controls with default legacy bindings: WASD move, mouse look, Shift sprint, Space jump / parkour / grab ledge (hold), R drop from ledge or drop to a ledge below, Q jump away from a hang, C crouch, Tab walk/run toggle, Enter toggles the control panel overlay (`ControlPanel.cs`).
3. Inspect the `Player` object under the player root in the Hierarchy: `ParkourController`, `ClimbController`, `ClimbCore`, `ParkourCore`, `SlidingController`, `EnvironmentScanner`, `ParkourInputManager` and the Third Person Controller components live on the same GameObject.

**Expected result:** Character vaults low walls, climbs ledges with ClimbPoint children, jumps gaps when Space is held toward them, slides on `Slide`-tagged slopes; no Console errors.

### Workflow: Locate package content

**Goal:** Find the file that owns a behaviour.

| Need | Path (under `Assets/Fantacode Studios/`) |
|---|---|
| Drop-in player prefab (fixed model) | `Parkour & Climbing System/Prefabs/Parkour Player.prefab` |
| Component donor used by the FS Window Setup | `Parkour & Climbing System/Resources/Parkour Controller.prefab` |
| Shipped parkour actions | `Parkour & Climbing System/Resources/Parkour Actions/*.asset` |
| Mobile parkour button UI | `Parkour & Climbing System/Resources/Parkour Mobile Controller.prefab` |
| Swingable ledge example | `Parkour & Climbing System/Prefabs/Swingalble Ledge.prefab` |
| Parkour/climb animator | `Parkour & Climbing System/Animators/Parkour Controller.controller` |
| Runtime scripts | `Parkour & Climbing System/Scripts/{Parkour System, Climbing System, Shared}` |
| Editor windows | `Parkour & Climbing System/Scripts/Editor` |
| Locomotion/camera/input framework | `Third Person Controller/Scripts/{Core, Player, System, Input}` |
| Input actions asset | `Third Person Controller/InputAction/FSSystemsInputAction.inputactions` |
| Layer/tag lists | `Third Person Controller/Scripts/Util/FSSystemsSetup.cs` |
| Generated animator controllers | `Third Person Controller/Resources/AnimatorControllers/` |

**Expected result:** You are editing settings on assets or components, never the scripts.

### Workflow: Pick the right sibling skill

| User wants to… | Skill |
|---|---|
| Get a playable character into a scene, use their own model | `fs-parkour-and-climbing-system-2-create-parkour-player` |
| Make a wall/beam/ledge grabbable, tune hanging, IK, hand spacing | `fs-parkour-and-climbing-system-2-make-ledges-climbable` |
| Vault / step up / climb up obstacles, add a custom action, wall run | `fs-parkour-and-climbing-system-2-configure-parkour-actions` |
| Jump across gaps / onto platforms, fix "he won't jump there" | `fs-parkour-and-climbing-system-2-tune-predictive-jumping` |
| Slide down slopes, slide-jump | `fs-parkour-and-climbing-system-2-add-sliding` |
| Change keys, gamepad, mobile buttons | `fs-parkour-and-climbing-system-2-customize-input` |

## Verification

- `AssetDatabase.IsValidFolder("Assets/Fantacode Studios/Parkour & Climbing System")` and `…/Third Person Controller` return `true`.
- `System.Type.GetType("FS_ParkourSystem.ParkourController, Assembly-CSharp")` is non-null.
- `LayerMask.NameToLayer("Ledge")`, `("Player")`, `("FootTrigger")` are all `!= -1`.
- `UnityEditorInternal.InternalEditorUtility.tags` contains `SwingableLedge` and `Slide`.
- If the new Input System is wanted: `PlayerSettings.GetScriptingDefineSymbols(NamedBuildTarget.Standalone)` contains `inputsystem`.
- Console is clear after entering and exiting Play in `DemoScene.unity`.

## API quick reference

| Entry point | Type | What it does |
|---|---|---|
| `Tools/FS System/ FS Window` | MenuItem (`FS_Core.FSSystemsSetupEditorWindow.ShowWindow`) | Opens the FS Systems window (Welcome / Setup / Packages / Dependencies tabs). Shared by all Fantacode packages: the Setup tab lists every installed FS system so Player and AI characters can be built with any combination of them. |
| `FS_Core.FSSystemsSetup.ImportProjectSettings()` | method | Adds layers, tags, ignored collisions, `inputsystem` define. |
| `FS_Core.FSSystemsSetup.layers` / `.tags` | `List<string>` | The canonical layer and tag names the systems expect. |
| `FS_Core.FSSettings.i` | singleton MonoBehaviour on the player | `GroundLayer`, `ObstacleLayer` masks; auto-adds `Ledge` to both at Awake. |
| `FS_Core.PlayerController` | MonoBehaviour on the player | Orchestrates `SystemBase` systems; `CurrentSystemState` tells you if the player is in `Parkour`, `Climbing`, `Sliding`, `Locomotion`. |
| `FS_ParkourSystem.ParkourController` | `SystemBase` | Obstacle actions, predictive jump, wall run. |
| `FS_ParkourSystem.ClimbController` | `SystemBase` | Ledge climbing. |
| `FC_ParkourSystem.SlidingController` | `SystemBase` | Slope sliding (note the `FC_` namespace). |
| `Tools/FS System/Parkour/Create Character` | MenuItem | Shortcut that opens the FS Window with Parkour pre-selected. |

## Common issues

- **Symptom:** FS Systems window opens on every project load. **Cause:** Default behaviour. **Fix:** Tick "Don't Show on Start" in the window; it still opens after package imports.
- **Symptom:** `[FS Systems] Setup window opened, but project-settings import failed`. **Cause:** `TagManager.asset` locked or all user layers full. **Fix:** Free a layer slot or make `ProjectSettings/` writable, then click **Import Project Settings** again.
- **Symptom:** Character does not respond to keyboard after enabling the Input System package. **Cause:** `inputsystem` define not set, or Active Input Handling still **Input Manager (Old)**. **Fix:** Workflow: Enable the new Input System.
- **Symptom:** Demo materials are magenta. **Cause:** Demo materials are URP; project is Built-in or HDRP. **Fix:** Cosmetic only. Convert with the pipeline's material upgrader or ignore; the system does not depend on them.
- **Symptom:** `Tools > FS System > Parkour` menu missing. **Cause:** Compile errors or asset not imported. **Fix:** Fix Console errors; re-import the package.
- **Symptom:** Menu path from older docs (`Tools > Parkour & Climbing System > …`) does not exist. **Cause:** Version 2 moved all menus under `Tools > FS System`. **Fix:** Use the paths in this skill.

## Boundaries

- The system controls a humanoid, Animator-driven character. Generic (non-humanoid) rigs are not supported — the FS Window setup refuses models without a valid humanoid avatar.
- It is a player-character system. AI characters that parkour need the separate **FS Parkour And Climbing AI 2** asset.
- It does not generate climbable geometry; ledges are authored by placing ClimbPoints on existing meshes (see the ledge skill).
- No multiplayer/netcode support is included.
- Integrations for GameCreator 2 and Invector exist under `Tools > FS System > Parkour > Integration` but require those third-party assets; they are outside this skill set.
