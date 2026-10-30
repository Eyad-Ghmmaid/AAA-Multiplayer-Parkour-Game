# 🚀 MULTIPLAYER INPUT INTEGRATION — QUICK SETUP

## DONE ✅
- [x] FSMultiplayerInputHandler.cs erstellt
- [x] FSMultiplayerInputProvider.cs erstellt  
- [x] Neue "Multiplayer" InputAction Map in FSSystemsInputAction
- [x] Integration Guide geschrieben

---

## TODO NOW (in Unity)

### Phase 2A: Component Setup (5 Minuten)

- [ ] **1. FSMultiplayerInputHandler zum Player hinzufügen**
  - Player Prefab öffnen
  - **Add Component** → `FSMultiplayerInputHandler`
  - InputActionAsset: `FSSystemsInputAction.inputactions` zuweisen

- [ ] **2. ThirdPersonController deaktivieren**
  - Komponente suchen (von CMP)
  - Checkbox UNCHECKEN
  - **Nicht löschen!**

- [ ] **3. Test Play**
  - Play Button → Editor
  - Checke Console für "[Input]" logs
  - Teste: W/A/S/D (Move), Space (Jump), Shift (Sprint)
  - Teste: V (Voice/PushToTalk), P (Stats), T (Chat)

### Phase 2B: Code Updates (10 Minuten)

Datei `GetPlayerCameraAndControls.cs`:
```diff
- [SerializeField] StarterAssetsInputs AssetInputs;
+ [SerializeField] FSMultiplayerInputHandler AssetInputs;
```

Datei `VoiceManager.cs`:
```diff
- public void Init(StarterAssetsInputs input, ...)
+ public void Init(FSMultiplayerInputHandler input, ...)
```

Datei `ChatPlayer.cs`:
```diff
- [SerializeField] private StarterAssetsInputs _input;
+ [SerializeField] private FSMultiplayerInputHandler _input;
```

### Phase 2C: Network Provider (für Multiplayer)

- [ ] **Add Component** auf Player: `FSMultiplayerInputProvider`
- [ ] **Assign** `FSMultiplayerInputHandler` im Inspector
- [ ] (Optional) In FusionConnection.cs: `INetworkInput` auf diesen Provider setzen

---

## CHECK Points

**Console Logs beim Play (sollte sehen):**
```
[Input] Move: (x, y) values
[Input] Look: (x, y) values (wenn Maus bewegt)
[Voice] Push-to-Talk gestartet (wenn V gedrückt)
[Input] ToggleCursor triggered (wenn Backspace gedrückt)
```

**Was funktionieren sollte:**
- ✅ WASD — Move
- ✅ Space — Jump
- ✅ Shift — Sprint
- ✅ Mouse — Look
- ✅ V — Voice aktiviert/deaktiviert
- ✅ Escape/Backspace — Cursor frei/locked
- ✅ P — (noch nichts, aber Event feuert)
- ✅ T — (noch nichts, aber Event feuert)

---

## Nächster Großer Schritt: Parkour Network Authority (Phase 3)

Dann geht's um:
- **ParkourController Authority-Gates** einbauen
- **FSParkourNetworkController** mit `[Networked]` States
- **Action-RPCs** für Parkour/Climb Start

---

## Support

Wenn was nicht funktioniert:
1. Check Console — sind die Debug-Logs da?
2. Check Inspector — FSMultiplayerInputHandler hat InputActionAsset zugewiesen?
3. Check ThirdPersonController — ist wirklich deaktiviert?
4. Runterscrolle zu "HÄUFIGE FEHLER" im FSMultiplayerInputHandler_INTEGRATION_GUIDE.md

