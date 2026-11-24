---
name: fs-rope-swinging-system-2-setup-and-overview
description: "Use this skill whenever the user has, wants to install, or asks anything general about FS Rope Swinging System 2 by Fantacode Studios — e.g. 'is the rope swinging asset installed', 'set up fantacode rope swing', 'what comes with the rope swinging package', 'the crosshair never appears on my bar', 'where is the swinging demo'. Covers verifying the install, first-time setup, package layout across Rope Swinging System and Third Person Controller (equip system, environment scanner), the demo scene and an index of the other skills. Do NOT use for building the player and giving it the hook (see fs-rope-swinging-system-2-create-swinging-player) or for placing SwingLedge hook points and tuning the swing (see fs-rope-swinging-system-2-setup-swing-ledges). When in doubt whether a rope-swing, Uncharted-style rope, hook-point or swing-across-the-gap request could involve FS Rope Swinging System 2, use this skill — Prerequisites shows how to confirm the asset is installed."
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

# Set Up FS Rope Swinging System 2

FS Rope Swinging System 2 adds Uncharted-style rope swinging to a Third Person Controller player: aim at a bar or beam that carries a **SwingLedge** hook point, hold **F** to throw the rope, then swing with full control — steer with WASD relative to the camera, climb up/down the rope with **Shift + W/S**, release with **Space** (jump off with landing force) or **F**, and land or climb up onto ledges. The rope is a procedural line with damping and throw arc. Hook points are authored by the designer (`SwingLedge` component + a green gizmo dot) and detected through the **Swing Ledge Layer**. The rope hook is an `EquippableItem` (`Hook`) drawn from the quick-switch wheel, picked up from an `EquipHotspot`, or not required at all (**Swing Without Equip**). The package bundles **Rope Swinging System** and **Third Person Controller**. Configuration is Inspector-driven; full C# source. This skill confirms the install, completes one-time setup, maps the package and routes to the sibling skills.

## When to use this skill

- "Is the Fantacode rope swing installed? Set it up."
- "No crosshair on my beam."
- "What's in the package / where's the demo?"
- "Works with parkour / melee?"
- Any rope-swing request in a project with `Assets/Fantacode Studios/Rope Swinging System`.

Not for:
- Building the player, giving it the hook, swing/rope tuning, input — `fs-rope-swinging-system-2-create-swinging-player`.
- Placing hook points, layers, rope length/reach, landing — `fs-rope-swinging-system-2-setup-swing-ledges`.
- A gun that pulls you to any edge — FS Grappling Hook System 2 (separate asset; both can coexist on one player).

## Prerequisites

- Unity 6000.0 or newer; render-pipeline agnostic (rope material is a simple material you can replace).
- Compiles into `Assembly-CSharp`.

**Programmatic install check** (Editor):

```csharp
using UnityEditor;
using UnityEngine;

public static class FSSwingInstallCheck
{
    [MenuItem("Tools/Check FS Rope Swing Install")]
    public static void Check()
    {
        bool folder = AssetDatabase.IsValidFolder("Assets/Fantacode Studios/Rope Swinging System");
        bool tpc = AssetDatabase.IsValidFolder("Assets/Fantacode Studios/Third Person Controller");
        bool t1 = System.Type.GetType("FS_SwingSystem.SwingController, Assembly-CSharp") != null;
        bool t2 = System.Type.GetType("FS_SwingSystem.SwingLedge, Assembly-CSharp") != null;
        bool item = Resources.Load("Hook") != null;
        Debug.Log($"FS Rope Swing install — folders:{folder}/{tpc} Controller:{t1} SwingLedge:{t2} HookAsset:{item}");
    }
}
```

- All `true` → ready. Folders `true`, types `false` → compile errors. Folders `false` → tell the user to import from https://assetstore.unity.com/packages/tools/game-toolkits/fs-rope-swinging-system-2-388274.

Manual check: `Tools > FS System > Rope Swinging > Create Character` exists and the FS Systems window (`Tools > FS System > FS Window`) → Setup → Player lists **Rope Swing**.

## Quick start

1. Import the package. The **FS Systems** window opens after import (and on project open unless "Don't Show on Start") and imports project settings on first open. Otherwise `Tools > FS System > FS Window` (exact path `Tools/FS System/ FS Window`) → Welcome → **Import Project Settings**.
2. Open `Assets/Fantacode Studios/Rope Swinging System/Demo/DemoScene.unity`, press Play. Pick up the `Hook` (**E**), look at a yellow bar until the crosshair appears, hold **F**, steer with WASD, press **Space** to let go.
3. `Tools > FS System > FS Window` → Setup → Player → **Rope Swing** builds your own character (create-swinging-player skill).

Expected observable result: the crosshair appears on bars that have `SwingLedge`; on F the rope arcs out and hooks, the character swings under the point; Space releases with a forward/upward push; `PlayerController.CurrentSystemState == SystemState.Swing` while swinging; no Console errors.

## Workflows

### Workflow: Import project settings

`Tools > FS System > FS Window` → Welcome → **Import Project Settings** (or `FS_Core.FSSystemsSetupEditorWindow.ShowWindow(); FS_Core.FSSystemsSetupEditorWindow.setupScript.ImportProjectSettings();`). Shared effects: FS layers/tags, the `inputsystem` define when the new Input System is active. This package registers **Rope Swing** as a Player-type system (`FSSystemsSetup.RopeSwingingSystemSetup`, donor prefab `Swinging Controller`, mobile UI `Swing Mobile Controller`); hook points use whatever layer you put in the scanner's **Swing Ledge Layer** (Default by default).

**Expected result:** The Setup tab lists **Rope Swing**.

### Workflow: Explore the demo scene

`Demo/DemoScene.unity`: ProBuilder room (`Environment`, cubes, prisms), `FS System` root with the drop-in `Prefab/Swinging Controller.prefab` player, a `Hook` pickup (`EquipHotspot` → item `Hook`), three `Ledge` bars with `SwingLedge` (green hook-point gizmo), `Hook Object` markers and a `RopePoint`. Everything is on layer Default.

### Workflow: Locate package content

| Need | Path (under `Assets/Fantacode Studios/`) |
|---|---|
| Drop-in player (fixed model) | `Rope Swinging System/Prefab/Swinging Controller.prefab` |
| FS Window donor (not for scenes) | `Rope Swinging System/Resources/Swinging Controller.prefab` |
| Hook item + category | `Rope Swinging System/Resources/Hook.asset` (`SwingHookItem`), `Resources/Category/Swing Hook.asset` |
| Hook model prefab | `Rope Swinging System/Prefab/Hook.prefab` (`SwingHookObject`: Rope Hook Point) |
| Crosshair | `Rope Swinging System/Prefab/Crosshair.prefab` |
| Mobile UI | `Rope Swinging System/Resources/Swing Mobile Controller.prefab` |
| Runtime scripts | `Rope Swinging System/Scripts/Controller/{SwingController, SwingInputManager, SwingUI, SwingRope, SwingLedge, EnvironmentScannerSwing}.cs`, `Scripts/Item/` |
| Editor | `Rope Swinging System/Scripts/Editor/{CreateSwingCharacterWindow, SwingControllerEditor, SwingHookDataEditor}.cs` |
| Equip/quick-switch framework | `Third Person Controller/Equip And Attach/` |
| Docs | `Rope Swinging System/Documentation.pdf` |

### Workflow: Pick the right sibling skill

| User wants to… | Skill |
|---|---|
| Player with own model that swings; give/equip the hook; swing feel, rope look, keys, mobile, events | `fs-rope-swinging-system-2-create-swinging-player` |
| Which bars can be hooked, hook point placement, layers, reach, climbing and landing | `fs-rope-swinging-system-2-setup-swing-ledges` |

## Verification

- Folders exist; `FS_SwingSystem.SwingController`, `SwingLedge`, `SwingHookItem`, `SwingHookObject`, `SwingInputManager` resolve; `FS_Core.SystemState.Swing` exists.
- `Resources.Load<FS_SwingSystem.SwingHookItem>("Hook") != null`.
- Demo plays: crosshair on bars; F swings; Space releases; no Console errors.

## API quick reference

| Entry point | Type | What it does |
|---|---|---|
| `Tools/FS System/ FS Window` → Setup → **Rope Swing** | EditorWindow | Builds the player with any combination of FS systems. |
| `Tools/FS System/Rope Swinging/Create Character` | MenuItem | Opens the FS Window with Rope Swing pre-selected (Player type). |
| `FS_SwingSystem.SwingController` | `EquippableSystemBase` (`SystemState.Swing`) | Aim, throw, swing, climb, release; fields and events. |
| `FS_SwingSystem.SwingLedge` | MonoBehaviour | Hook point on a bar (`hookPoint`, gizmo). |
| `FS_SwingSystem.SwingHookItem` / `SwingHookObject` | `EquippableItem` / `EquippableItemObject` | The hook and its rope attach transform. |
| `FS_SwingSystem.SwingInputManager` | `FSInputManagerBase` | Hook (F), release (F), climb modifier (Shift), jump from Locomotion input. |
| `FS_ThirdPerson.EnvironmentScanner.SwingLedgeLayer` / `GetSwingLedgeData` | partial | Target detection. |
| `FS_SwingSystem.SwingRope` | class | Procedural rope with states Normal / Swinging / Throwing / Climbing. |

## Common issues

- **Symptom:** No crosshair. **Cause:** Bar has no `SwingLedge`, wrong layer, hook not owned/equipped and Swing Without Equip off, or beyond rope length. **Fix:** Ledges skill / player skill.
- **Symptom:** Old docs mention `Tools > Rope Swinging System`. **Fix:** It is `Tools > FS System > Rope Swinging`.
- **Symptom:** Character swings but can't climb the rope. **Cause:** Rope too angled (Max Angle For Climb 15). **Fix:** Climb when hanging nearly vertical.

## Boundaries

- Player-only; no AI swinging.
- Hook points must be authored (`SwingLedge`); the rope does not attach to arbitrary geometry.
- Rope is a visual/kinematic simulation on the character, not a physics joint on props.
