---
name: fs-rope-swinging-system-2-create-swinging-player
description: "Use this skill whenever the user wants a player that swings on ropes with FS Rope Swinging System 2 — e.g. 'build a swinging player with my model', 'add rope swinging to my FS player', 'the player has no hook', 'swing without holding the hook', 'swing faster / floatier', 'jump further off the rope', 'change the hook key', 'mobile swing buttons'. Covers the FS Systems window (Rope Swing ticked with other systems) and Create Character, the drop-in prefab, giving the hook via Quick Switch Items or an EquipHotspot, Swing Without Equip, SwingController fields (General, Swing, Rope, Camera, Crosshair, Events), SwingInputManager keys and the mobile controller. Do NOT use for placing hook points, layers, reach and landing rules (see fs-rope-swinging-system-2-setup-swing-ledges). When in doubt whether a swing-player, rope-hook or swing-feel request could involve FS Rope Swinging System 2, use this skill — Prerequisites shows how to confirm the asset is installed."
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

# Create a Swinging Player

The swinging player is a Third Person Controller character with `SwingController` (an `EquippableSystemBase` bound to the `SwingHookItem` type) and `SwingInputManager`. The controller aims while the player is in plain Locomotion and a hook is available — **equipped** (quick-switch wheel, **Caps Lock**; or given by an `EquipHotspot`) or merely **owned** with **Swing Without Equip** on. When the scanner finds a `SwingLedge` hook point within rope length, the crosshair appears; holding **F** plays the throw clip, the rope arcs to the point (Throw Height/Duration) and the swing starts: WASD steers relative to the camera with **Swing Force**, **Shift + W/S** climbs, **Space** jumps off with **Forward/Upward Land Force**, **F** releases. Build the character with the FS Systems window (any model, combinable with every other FS system) or drop in the shipped prefab.

## When to use this skill

- "Player with my model that can rope-swing (and parkour / fight)."
- "Add swinging to the player I already built."
- "Give the player the hook from the start / from a pickup."
- "Don't require the hook in hand."
- "Swing feels sluggish / too fast; jump further when releasing."
- "Thinner, longer rope; no camera shake."
- "Sound when the rope hooks."
- "Touch buttons for hook / release / climb."

Not for:
- Hook points, layers, reach, climbing onto ledges — `fs-rope-swinging-system-2-setup-swing-ledges`.
- Quick-switch wheel internals — FS Inventory / Third Person Controller docs.

## Prerequisites

- Installed: `System.Type.GetType("FS_SwingSystem.SwingController, Assembly-CSharp") != null` (else import from https://assetstore.unity.com/packages/tools/game-toolkits/fs-rope-swinging-system-2-388274).
- A Humanoid model prefab with a valid avatar (FS Window build).
- Project settings imported (FS Window → Welcome → Import Project Settings).

## Quick start

Goal: player with the user's model, owning the hook, swinging with F.

1. Delete `Main Camera`. `Tools > FS System > FS Window` → **Setup** → character type **Player** → tick **Locomotion** (required) and **Rope Swing**; tick every other owned FS system (Parkour and Climbing, Melee Combat, Grappling Hook, …) — one character is built with all of them, which is the intended way to combine FS packages. (`Tools > FS System > Rope Swinging > Create Character` opens the same window with Rope Swing pre-ticked.)
2. Drop the humanoid model on the preview, click **Setup**.
3. Select `FS Player/Player` → `QuickSwitchHandler` → **Quick Switch Items** → add `Rope Swinging System/Resources/Hook.asset`; tick **Equip First Item On Start** (or draw it with Caps Lock).
4. Play in a scene with `SwingLedge` bars (ledges skill), aim at one, hold **F**.

Expected observable result: `Player` has `SwingController` and `SwingInputManager`; `Game UI` contains `Swing Mobile Controller` (hidden on desktop); the hook spawns in the right hand; the crosshair shows on bars within reach; F throws and swings; `PlayerController.CurrentSystemState` reads `Swing`; no Console errors.

## Workflows

### Workflow: Build the player (FS Systems window)

What Setup does (`FSSystemsSetup.RopeSwingingSystemSetup`): instantiates the Third Person Controller player, copies the components of `Resources/Swinging Controller.prefab` (`SwingController`, `SwingInputManager`, plus shared `ItemEquipper`, `ItemAttacher`, `QuickSwitchHandler`, `EnvironmentScanner`, …) onto `Player`, merges the swing animator (Swing Throw, Rope Holding, `Swing Actions`, `Swing Land`, `Swing Climb Up/Down`) into the generated controller, adds `Swing Mobile Controller` under `Game UI` and wires it to the input manager. Other ticked systems add theirs.

Drop-in alternative: `Prefab/Swinging Controller.prefab` (fixed Default Character, no hook pre-assigned — the demo hands it over with an `EquipHotspot`).

**Expected result:** As in Quick start.

### Workflow: Give the player the hook

| Way | Steps | Notes |
|---|---|---|
| Owned from start | `QuickSwitchHandler` → Quick Switch Items → `Hook`; optional **Equip First Item On Start** | Draw with **Caps Lock** (wheel) or **3 / 4** (cycle). |
| Pickup | Empty + trigger collider + `FS_Core.EquipHotspot` (**Item** = Hook, **Item Object** = visible mesh to hide) | **E** equips it (demo `Hook`). |
| Inventory | With FS Inventory: hook in an equipment slot → quick-switch bridge | Inventory skills. |
| No equipping | `SwingController` → **Swing Without Equip** ✓ (hook still must be owned, e.g. Quick Switch Items) | Rope is held from the hand anchors. **Un Equip After Swing** holsters it after each swing. |

Custom hook: duplicate `Prefab/Hook.prefab` (keep `SwingHookObject` with **Rope Hook Point** = where the rope attaches to the hook mesh) and `Hook.asset` (`Assets > Create > Swing System > Create Hook`; Model Prefab, hand offsets, **Rope Throwing Clip**).

**Expected result:** `player.GetComponent<FS_Core.ItemEquipper>().equippableItems` contains a `SwingHookItem`; `SwingController` enables itself (`enableSwing`) when one is available.

### Workflow: Tune the controller (tabs General / Swing / Rope / Camera Settings / Crosshair / Events)

| Field | Default | Effect |
|---|---|---|
| Enable Swing | auto | On when a hook item is owned. |
| Min Distance | 2 | Closer hook points are ignored; also auto-releases when the rope gets shorter than this. |
| Hook Input / Hook Release Input / Rope Climb Modifier | F / F / Left Shift (`SwingInputManager`) | Hold F to throw; F again to release; Shift + W/S climbs. Jump (Space) from Locomotion input releases with force. |
| Rope Throwing Clip / Animation Speed / Rope Holding Clip | on item / 1 / Rope Holding | Animations. |
| Swing Without Equip / Un Equip After Swing | off / off | See above. |
| Max Angle For Climb | 15 | Climbing allowed only while the rope is within this angle of vertical. |
| Swing Force / Rotation Speed | 4 / 100 | Steering push and turn rate toward the camera. |
| Forward / Upward Land Force Multiplier | 1 / 2 | Push applied on Space release. |
| Damping / Gravity / Collision Friction | 0.5 / −20 / 0.2 | Pendulum feel and wall bumps. |
| Climb Speed | 3 | Rope climb speed. |
| Right/Left Hold Transform (offsets), Attach Transform, Hook Object | auto anchors | Where the rope is held / attached on the body. |
| Rope Radius / Material / Resolution / Rope Length / Width / Dampening / Throw Height / Throw Duration | 0.025 / Rope / 50 / 10 / 0.1 / 0.5 / 1 / 0.5 | Rope look, reach (max aim distance = Rope Length − 1) and throw arc. |
| Camera Shake Amount / Duration | 0.2 / 0.3 | Shake on swing start. |
| Crosshair Prefab / Size | Crosshair / 0.3 | Aim marker. |
| Events: Rope Released, Rope Hooked, Swing Started, Exited From Swing | — | UnityEvents for audio/VFX. |

**Expected result:** Changes apply on the next swing; no rebuild needed.

### Workflow: Input and mobile

`SwingInputManager`: **Hook Input** (F), **Hook Release Input** (F), **Rope Climb Modifier Input** (Left Shift) — KeyCode + optional Input System button name; direction and jump come from `LocomotionInputManager`. Mobile: `Game UI/Swing Mobile Controller` (`SwingUI`) provides hook / release / climb buttons, linked by `SetSwingUI` during Setup.

**Expected result:** `SwingInputManager.HookInputHolding`, `HookReleaseDown`, `RopeClimbModifierHolding` reflect the new bindings.

## Verification

- `Player` has `FS_SwingSystem.SwingController`, `SwingInputManager`, `FS_Core.ItemEquipper`, `QuickSwitchHandler`, `FS_ThirdPerson.EnvironmentScanner`.
- `QuickSwitchHandler` serialized `quickSwitchItems` contains the hook, or an `EquipHotspot` with it exists, or `swingWithoutEquip` is on with the hook owned.
- Play: crosshair on a `SwingLedge` within reach; after F `PlayerController.CurrentSystemState == SystemState.Swing` and `SwingController.InAction`; Space releases.
- No Console errors.

## API quick reference

| Entry point | Type | What it does |
|---|---|---|
| `Tools/FS System/ FS Window` → Setup → **Rope Swing** / `Tools/FS System/Rope Swinging/Create Character` | Editor | Build the player. |
| `FS_SwingSystem.SwingController` | `EquippableSystemBase` | Fields above; `InAction`; events `RopeReleased`, `RopeHooked`, `SwingStarted`, `ExitedFromSwing`. |
| `FS_SwingSystem.SwingHookItem` | `EquippableItem` | `ropeThrowingClip`; category Swing Hook. |
| `FS_SwingSystem.SwingHookObject` | `EquippableItemObject` | `ropeHookPoint`. |
| `FS_SwingSystem.SwingInputManager` | `FSInputManagerBase` | `HookInputHolding`, `HookReleaseDown`, `RopeClimbModifierHolding`, `DirectionInput`, `JumpKeyDown`, `SetSwingUI`. |
| `FS_SwingSystem.SwingRope` / `RopeState` | class / enum | Rope simulation (Normal, Swinging, Throwing, Climbing). |
| `FS_Core.QuickSwitchHandler.AddQuickSwitchItem(item)` / `FS_Core.EquipHotspot` | runtime | Give the hook. |

## Common issues

- **Symptom:** No crosshair, nothing on F. **Cause:** Hook not owned/equipped (Swing Without Equip off); player not in Locomotion; no `SwingLedge` in reach. **Fix:** Equip via wheel or tick Swing Without Equip; ledges skill.
- **Symptom:** Hook not in the wheel. **Fix:** Add `Hook` to Quick Switch Items.
- **Symptom:** Swing barely moves. **Fix:** Raise Swing Force, lower Damping.
- **Symptom:** Can't climb the rope. **Cause:** Rope angle > Max Angle For Climb. **Fix:** Wait for the vertical hang or raise the angle.
- **Symptom:** Model validation error in the FS Window. **Fix:** Humanoid rig with Animator.

## Boundaries

- Player only; no AI swinging.
- One hook item at a time; rope is single-segment (no wrapping around geometry).
- Wheel/holster behaviour comes from the Third Person Controller equip framework.
