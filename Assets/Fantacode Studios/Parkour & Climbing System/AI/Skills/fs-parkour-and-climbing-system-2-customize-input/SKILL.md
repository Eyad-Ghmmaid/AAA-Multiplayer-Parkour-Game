---
name: fs-parkour-and-climbing-system-2-customize-input
description: "Use this skill whenever the user wants to change how FS Parkour And Climbing System 2 is controlled — e.g. 'rebind jump to F', 'use the gamepad for parkour', 'change the drop key', 'jump from hang on a different button', 'switch the parkour controller to the new input system', 'add touch buttons for mobile', 'the on-screen buttons don't work', 'read the current jump binding from code'. Covers legacy Input Manager bindings (FSInput fields on LocomotionInputManager / ParkourInputManager), the FSSystemsInputAction asset and its Locomotion / Parkour maps, the inputsystem define, InputManagerHub, and the mobile controller prefabs. Do NOT use for changing what a key does in gameplay (see the other fs-parkour-and-climbing-system-2 skills) or for camera settings. When in doubt whether a controls, keys, gamepad or touch-button request could involve FS Parkour And Climbing System 2, use this skill — Prerequisites shows how to confirm the asset is installed."
metadata:
  asset: "FS Parkour And Climbing System 2"
  publisher: "Fantacode Studios"
  asset-version: "2.0.1"
  skill-version: "1.0.0"
  unity: "6000.0+"
  render-pipelines: "Built-in, URP, HDRP"
  category: "tools/game-toolkits"
  asset-store-url: "https://assetstore.unity.com/packages/tools/game-toolkits/fs-parkour-and-climbing-system-2-387610"
  documentation-url: "https://fantacode.gitbook.io/fantacode-studios/fantacode-studios/fs-parkour-and-climbing-system/input-manager"
  support-url: "mailto:studios@fantacode.com"
  last-verified: "2026-09-13"
---

# Customize Parkour Input

Input for the parkour player flows through small **input manager** components on the `Player` object (`LocomotionInputManager` for movement, jump, drop, sprint, crouch; `ParkourInputManager` for jump-from-hang), coordinated by `InputManagerHub`. Each action is an `FSInput` binding holding a legacy `KeyCode`, an optional legacy Input Manager button name, and an optional on-screen UI button. When the `inputsystem` scripting define is set, the same managers additionally read the `FSSystemsInputAction` asset (Unity Input System), so gamepad and rebinding go through that asset. This skill lets an agent change bindings in either mode and enable the touch UI.

## When to use this skill

- "Change jump to F / drop to E / jump-from-hang to Shift."
- "Add gamepad support for climbing."
- "Switch the FS controller to the new Input System."
- "Enable mobile touch controls for parkour."
- "Get the current key for Jump at runtime."

Not for:
- What the inputs do (jump reach, grab rules, vault selection) — the sibling skills.
- Camera sensitivity/inversion — `Third Person Camera` → `CameraController` settings (Third Person Controller).

## Prerequisites

- Player with `FS_Core.InputManagerHub`, `FS_LocomotionSystem.LocomotionInputManager` and `FS_ParkourSystem.ParkourInputManager` on `Player`: `Object.FindFirstObjectByType<FS_ParkourSystem.ParkourInputManager>() != null`.
- Install check: `System.Type.GetType("FS_ParkourSystem.ParkourInputManager, Assembly-CSharp") != null`. If null, tell the user to import FS Parkour And Climbing System 2 from https://assetstore.unity.com/packages/tools/game-toolkits/fs-parkour-and-climbing-system-2-387610.
- Know the mode: Project Settings > Player > **Active Input Handling** and whether `inputsystem` is in Scripting Define Symbols. `#if inputsystem` selects the new-system reading path; `ENABLE_LEGACY_INPUT_MANAGER` (set by Unity when Active Input Handling is Old or Both) enables the `KeyCode` path.

## Quick start

Goal: rebind Jump to **F** in a legacy-input project.

1. Select `Player` (child of `FS Player` / `Parkour Player`).
2. `LocomotionInputManager` → **Jump Input** → **Key** = `F`.
3. Press Play; F now jumps, vaults and grabs ledges (Jump is one binding for all three).

Expected observable result: Space no longer triggers a jump; F does.

Default legacy keys (source: `LocomotionInputManager.cs`, `ParkourInputManager.cs`):

| Action | Field | Default | Used for |
|---|---|---|---|
| Jump | `LocomotionInputManager.jumpInput` | Space | Jump, hold for parkour/predictive jump/ledge grab, climb up (press-to-climb), slide-jump |
| Drop | `dropInput` | R | Drop from hang; drop to a ledge below when standing at an edge |
| Move Type | `moveTypeInput` | Tab | Walk/run toggle |
| Sprint | `sprintInput` | Left Shift | Sprint |
| Crouch | `crouchInput` | C | Crouch |
| Quick Switch Item | `quickSwitchItemInput` | Caps Lock | Third Person Controller item switch |
| Jump From Hang | `ParkourInputManager.jumpFromHangInput` | Q | Jump away from a hang toward camera/input direction; swing combo |

## Workflows

### Workflow: Rebind a key (legacy Input Manager)

**Goal:** Change a `KeyCode` or map to a named Input Manager button.

**Steps:**
1. `Player` → the owning input manager component → the `FSInput` field → set **Key** (a `KeyCode`), and/or **Button** (a name from Edit > Project Settings > Input Manager, read with `Input.GetButton`). Either one being pressed counts.
2. Leave **UI Button** empty unless wiring a custom on-screen button (`UIImageInteraction`).
3. From code (Editor):

```csharp
using UnityEditor;
using UnityEngine;

var lim = Object.FindFirstObjectByType<FS_LocomotionSystem.LocomotionInputManager>();
var so = new SerializedObject(lim);
so.FindProperty("jumpInput.key").enumValueIndex = System.Array.IndexOf(System.Enum.GetValues(typeof(KeyCode)), KeyCode.F);
so.ApplyModifiedProperties();
```

(`FSInput.key` is a private serialized field; there is no public setter, so use `SerializedObject`. `enumValueIndex` is the index into `KeyCode` values, not the enum value.)

**Expected result:** The new key drives the action in Play mode; `InputManagerHub.TryGetInput("jumpInput", out var info)` reports `info.Input.Key == KeyCode.F`.

### Workflow: Switch to the new Input System and rebind there

**Goal:** Gamepad support / rebinding via the Input Actions asset.

**Steps:**
1. Install **Input System** (Package Manager) and set Active Input Handling to **Input System Package (New)** or **Both**; restart when prompted.
2. `Tools > FS System > FS Window` → **Dependencies** tab → tick **New Input System** → OK. This adds the `inputsystem` define and reimports scripts. (Import Project Settings also adds the define automatically when Active Input Handling is already the new package.)
3. Open `Assets/Fantacode Studios/Third Person Controller/InputAction/FSSystemsInputAction.inputactions`.
4. Action maps used by this asset: **Locomotion** — `MoveInput` (WASD / left stick), `Jump` (Space / South button), `Drop` (R / East button), `MoveType` (Tab / left shoulder), `SprintKey` (Shift / right trigger), `Crouch` (C / right shoulder), `CameraInput` (mouse delta / right stick); **Parkour** — `JumpFromHang` (Q / right trigger).
5. Select the action, click a binding to change its path, or **+** to add one per device. Save the asset (Auto-Save or Save Asset).
6. With the define set, the managers read `input.Locomotion.Jump` etc. **or** the legacy `FSInput` key — both work at once when Active Input Handling is Both.

**Expected result:** The gamepad controls movement, jump, drop and jump-from-hang; keyboard bindings follow the asset.

### Workflow: Enable mobile touch controls

**Goal:** On-screen joystick and buttons.

The player root's `Game UI` object contains prefab instances **Locomotion Mobile Controller** (`Camera Swipe` area, `Jump`, `Drop` buttons and joystick), **Parkour Mobile Controller** (`Jump From Hang` button) and **Interaction Mobile Controller**. On `Prefabs/Parkour Player.prefab` the Parkour and Interaction controllers are inactive by default; the FS Window build instantiates all three.

**Steps:**
1. Expand `FS Player` (or `Parkour Player`) → `Game UI`.
2. Set `Locomotion Mobile Controller` and `Parkour Mobile Controller` active (checkbox next to the name).
3. Buttons are bound by name at Awake: `ParkourInputManager` looks for a `UIImageInteraction` named `Jump From Hang` under `Game UI` (`ParkourUI.CacheButtons`), `LocomotionInputManager` binds `Jump`, `Drop`, etc. Keep those names if you restyle the UI.
4. Older docs say "enable the Input UI object" — the objects are the ones named above.

**Expected result:** Touch (or mouse in the Editor) on the buttons triggers the actions; `FSInput.UIButton` on each binding is non-null in Play mode.

### Workflow: Read or enumerate bindings from code

**Goal:** Show controls in a UI or check them in a test.

```csharp
var hub = Object.FindFirstObjectByType<FS_Core.InputManagerHub>();
foreach (var b in hub.GetAllInputBindings())
    Debug.Log($"{b.Id} ({b.DisplayName}) -> key {b.Input.Key}, button '{b.Input.Button}'");

if (hub.TryGetInput("jumpFromHangInput", out var jfh))
    Debug.Log($"Jump from hang: {jfh.Input.Key}");

var parkourInput = hub.GetInputManager<FS_ParkourSystem.ParkourInputManager>();
bool held = parkourInput.JumpFromHang; // runtime state, true while held
```

`Id` is the serialized field name (e.g. `jumpInput`, `dropInput`, `jumpFromHangInput`); lookup is case-insensitive.

**Expected result:** Console lists every binding with its key.

## Verification

- Legacy: `hub.TryGetInput("jumpInput", out var i) && i.Input.Key == <expected KeyCode>`.
- New Input System: `PlayerSettings.GetScriptingDefineSymbols(UnityEditor.Build.NamedBuildTarget.Standalone)` contains `inputsystem`; the `.inputactions` asset's `Locomotion/Jump` action has the intended binding path.
- Play mode: pressing the bound key sets `LocomotionInputManager.JumpHold` / `JumpKeyDown` (Jump), `.Drop`, or `ParkourInputManager.JumpFromHang` to `true` that frame.
- Mobile: `Game UI/Parkour Mobile Controller` active and `FindFirstObjectByType<FS_ParkourSystem.ParkourUI>().JumpFromHangUIButton != null`.
- The scene `EventSystem` has an input module matching Active Input Handling (`FSInputManagerBase.EnsureEventSystemInputModule` adds one at Awake if missing).

## API quick reference

| Entry point | Type | What it does |
|---|---|---|
| `FS_LocomotionSystem.LocomotionInputManager` | `FSInputManagerBase` on `Player` | `jumpInput`, `dropInput`, `sprintInput`, `crouchInput`, `moveTypeInput`…; runtime `JumpHold`, `JumpKeyDown`, `Drop`, `DirectionInput`. |
| `FS_ParkourSystem.ParkourInputManager` | `FSInputManagerBase` on `Player` | `jumpFromHangInput`; runtime `JumpFromHang`; `SetParkourUI(ParkourUI)`. |
| `FS_Core.FSInput` | serializable class | `Key` (`KeyCode`), `Button` (Input Manager name), `UIButton`; `GetButton()/GetButtonDown()/GetButtonUp()`. |
| `FS_Core.InputManagerHub` | MonoBehaviour on `Player` | `GetAllInputBindings()`, `TryGetInput(id, out FSInputBindingInfo)`, `GetInputManager<T>()`. |
| `FS_Core.FSInputBindingInfo` | struct | `Id`, `DisplayName`, `Input`, `FieldInfo`. |
| `Third Person Controller/InputAction/FSSystemsInputAction.inputactions` | Input Actions asset | Maps `Locomotion`, `Parkour` (and other FS systems). |
| `Tools/FS System/ FS Window` → Dependencies | EditorWindow | **New Input System** toggle → `inputsystem` define. |
| `FS_ParkourSystem.ParkourUI` | `FSInputUIBase` on `Parkour Mobile Controller` | `JumpFromHangUIButton`. |

## Common issues

- **Symptom:** Keyboard dead after installing the Input System package. **Cause:** Active Input Handling set to New only, but `inputsystem` define missing — legacy path compiled out. **Fix:** Dependencies tab → New Input System, or set handling to Both.
- **Symptom:** Gamepad ignored. **Cause:** Define not set; the legacy path never reads gamepads. **Fix:** Same as above; verify bindings in the asset.
- **Symptom:** Changed a key in `FSSystemsInputAction` but nothing happens. **Cause:** Project still on legacy path (no define). **Fix:** Set the define, or edit the `FSInput` Key instead.
- **Symptom:** On-screen buttons don't respond. **Cause:** `EventSystem` missing an input module for the active handling, or button renamed so `CacheButtons` can't find it. **Fix:** Keep the names `Jump`, `Drop`, `Jump From Hang`; `EnsureEventSystemInputModule` adds a `StandaloneInputModule` or `InputSystemUIInputModule` at Awake — confirm one is present on the `EventSystem` in Play mode.
- **Symptom:** Two actions on one key. **Cause:** Jump intentionally drives jump, parkour, ledge grab and slide-jump. **Fix:** Expected; only Jump From Hang and Drop are separate.

## Boundaries

- There is no runtime rebinding UI; rebinding at runtime means editing the Input Actions asset bindings via the Input System API or changing `FSInput` via reflection/`SerializedObject` in the Editor.
- Input for other FS systems (combat, shooter, vehicles) lives in their own managers and maps; only Locomotion and Parkour are covered here.
- Touch UI is a simple button set; gestures (swipe to vault) are not supported.
