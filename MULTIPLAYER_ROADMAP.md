# Multiplayer-Parkour: Umsetzungsplan

## Fortschritt

- **Etappen 1 und 2:** Der Nutzer hat bestätigt, dass lokale und entfernte Spieler Bewegung und Animationen korrekt ausführen.
- **Etappe 4 (vor Etappe 3 vorgezogen):** Die Parkour-Aktions- und Animationssynchronisierung wurde aktiviert; der Nutzer hat bestätigt, dass die zuvor gemeldeten Synchronisierungsprobleme behoben sind.
- **Etappe 3:** Szenen- und Shutdown-Robustheit wurde im Code verbessert. Der Nutzer hat die Zwei-Client-Tests (Szenenwechsel, Leave und erneutes Room-Erstellen/Beitreten) ohne Probleme abgeschlossen; Etappe 3 ist abgenommen.
- **Versionskontrolle:** Das Projekt hat inzwischen Git. Vor Änderungen an Rennlogik einen Commit/Checkpoint erstellen; bestehende lokale Änderungen vorher prüfen und nicht überschreiben.

## Ziel und Definition von „fertig“ für den ersten Meilenstein

Zwei Spieler können über das vorhandene Fusion-Raumsystem denselben Spielraum betreten. Jeder sieht den anderen mit plausibel flüssiger Bewegung. Laufen, Drehen, Springen und Landen funktionieren; lokale Eingabe und Kamera wirken ausschließlich auf den eigenen Spieler. Beide Clients laden dieselbe Spielumgebung und können die Sitzung sauber verlassen.

In diesem Meilenstein gibt es noch keine Parkour-Aktionen, Checkpoints, Rennwertung oder Voice-Politur. Der FS-Controller soll zunächst als gewöhnlicher Third-Person-Controller zuverlässig im Netzwerk laufen.

## Projektbefund, auf dem der Plan basiert

- Das Projekt nutzt Fusion 2 und das Multiplayer-Asset startet Sitzungen mit `GameMode.Shared`.
- `FusionConnection` erzeugt derzeit das im Character-Auswahl-Asset eingetragene CMP-Spieler-Prefab. Die Spawn- und Voice-Initialisierung erwartet unter anderem `StarterAssetsInputs` und `PlayerWorldUIManager`.
- FS verwendet eine getrennte Eingabekette aus `LocomotionInputManager` und `ParkourInputManager`.
- Das vorhandene `FSInputAdapter_FINAL_SUMMARY.md` nennt Adapterdateien, die aktuell nicht im Projekt liegen. Es ist keine verlässliche Implementierungsanleitung.
- Der Multiplayer-Ablauf lädt `Game` und lädt anschließend `Environment 1` additiv. Die FS-Demo-Szenen sind nicht Teil dieses Ablaufs.
- FS-Aktionen können Charaktertransforms durch Root Motion und Coroutines direkt bewegen. Diese Mechanik muss später ausdrücklich mit der Fusion-Transform-Replikation abgestimmt werden.

## Architekturentscheidungen für den MVP

1. **Vorhandenes Lobby- und Raumsystem zunächst behalten.** So beschränken sich Änderungen auf Spieler, Spielszene und Spawn. Lobby, Passwörter und Raumliste werden nicht gleichzeitig umgebaut.
2. **Für den ersten Prototyp bei Shared Mode bleiben.** Das entspricht dem bereits eingerichteten Projekt und eignet sich, um Besitz, Bewegung und Darstellung für einen kleinen Freundesraum zu validieren. Shared Mode bietet keinen vertrauenswürdigen zentralen Host für wettbewerbliche Ergebnisprüfung; diese Entscheidung wird vor einem kompetitiven Rennmodus erneut bewertet.
3. **Ein eigenes FS-basiertes Netzwerk-Spieler-Prefab erstellen.** Nicht beide vollständigen Player-Prefabs kombinieren. FS bleibt Quelle für Controller, Animator, Parkour-System, Kamera und FS-Eingabe. Aus CMP werden nur tatsächlich benötigte Netzwerk-, Voice- und Spieleridentitäts-Komponenten übernommen. Das `NetworkObject` bleibt am äußeren Spawn-Root; dort sitzt auch ein `NetworkTransform` als primärer Root-TRSP. Ein zweiter `NetworkTransform` am tatsächlich bewegten FS-`Player`-Kind repliziert dessen Bewegung. Fusion 2 unterstützt `NetworkTransform` an Kindern, aber der Projektlaufzeit-Assert verlangt hier offenbar, dass der erste TRSP am Spawn-Root sitzt.
4. **Für den MVP die lokale State Authority bewegen lassen.** Lokale FS-Eingabe bewegt den eigenen Spieler; Fusion synchronisiert dessen Netzwerk-Transform für andere Clients. Es gibt keinen zweiten Bewegungs-Simulator auf einem Proxy.
5. **Netzwerkzustand und Darstellung trennen.** Die Netzwerk-Transform-Komponente kontrolliert die replizierte Position des bewegten FS-`Player`-Kindes. Proxies zeigen interpolierte Bewegung und Animation, lesen aber keine lokale Eingabe und wenden keine Root Motion auf das synchronisierte Spieler-Transform an.

## Zielhierarchie für das FS-Netzwerkspieler-Prefab

```text
FS Network Player                         [NetworkObject + NetworkTransform]
├── Player                                [NetworkTransform; FS-Controller, Animator,
│   └── Default Character / Armature       CharacterController und Parkour-System]
├── Third Person Camera                   [FS CameraController + Camera + AudioListener;
│                                           nur lokal aktiv]
├── Game UI                               [nur nötige lokale UI; keine zweite EventSystem-Instanz]
├── Quick Switch UI Canvas                [für Lauf-/Sprung-MVP weglassen]
├── HP Bar                                [für MVP weglassen]
└── (keine globalen Settings-Komponenten) [FSSettings und EquipSettings leben einmal
                                            auf einem Szene-Objekt]
```

**Wichtig:** Auf das Kindobjekt `Player` kommt nur `NetworkTransform`, kein zweites `NetworkObject`. Das äußere Prefab hat ebenfalls einen `NetworkTransform`, damit Fusion den primären Root-TRSP zuerst im Netzwerkzustand anlegt; das Kind-`NetworkTransform` repliziert zusätzlich das tatsächlich bewegte FS-Transform. Der Laufzeit-Assert `networkObject.NetworkedBehaviours[0] ... is not NetworkTRSP` deutet darauf hin, dass beim aktuellen Aufbau der Child-TRSP als erster Eintrag serialisiert wird und der erwartete Root-Header fehlt. Nach dem Hinzufügen muss geprüft werden, dass Fusion am Root-`NetworkObject` den Root-`NetworkTransform` zuerst aufführt. Das CMP-Prefab benötigt nur einen Transform, weil dort Root und bewegter Character identisch sind.

`FSSettings` und `EquipSettings` enthalten beide statische Singletons (`FSSettings.i` bzw. `EquipSettings.i`) und gehören daher einmal auf ein nicht netzwerkgebundenes Szenenobjekt wie `FS Systems Settings` in die CMP-`Game`-Szene. Die Werte und Referenzen werden von den Komponenten am Original-Parkour-Prefab übernommen; die mehrfach gespawnten Netzwerkspieler erhalten keine eigenen Kopien dieser Singleton-Komponenten.

Die FS-Kamera ist an `PlayerController` und `LocomotionInputManager` gekoppelt. Für den MVP sollte sie deshalb als einzige aktive Spielkamera des lokalen Clients verwendet werden; die konkurrierende Cinemachine-Kamera aus CMP muss deaktiviert oder aus der Game-Szene genommen werden. Eine Cinemachine-Integration wäre ein eigener Kamera-Adapter-Schritt. Entfernte Spieler bekommen keine aktive Kamera, keinen Audio Listener, keine Eingabe und kein lokales HUD.

## Etappen und Abnahmekriterien

### Etappe 0 — Projekt absichern und Referenzverhalten festhalten

- Eine Sicherung bzw. ein sauberer Versionskontroll-Checkpoint vor Prefab- und Szenenänderungen anlegen.
- Im Unity-Editor notieren: Unity- und Fusion-Version, aktive Input-Handling-Einstellung, vorhandene Build-Szenen, Fusion-Prefab-Registrierung und tatsächliche Spieler-Prefab-Komponenten.
- FS-Parkour-Spieler offline in seiner Demo-Szene starten und normales Laufen, Kamera, Springen und Landen als Referenz festhalten.
- CMP-Lobby mit zwei lokalen Instanzen starten und Join/Leave sowie den bestehenden Szenenwechsel als Referenz festhalten.

**Abnahme:** Beide Assets funktionieren in ihren bestehenden Referenzabläufen, bevor die Verbindung verändert wird. Fehler sind dadurch eindeutig dem Integrationsschritt zuzuordnen.

### Etappe 1 — Netzwerk-Spieler integrieren (erster Umsetzungsschwerpunkt)

- Ein separates Prefab, z. B. `FS Network Player`, aus dem FS-Spieler aufbauen; Original-Prefabs unverändert lassen.
- Fusion-`NetworkObject` und primären `NetworkTransform` am äußeren Spawn-Root sowie einen zweiten `NetworkTransform` am bewegten FS-`Player`-Kind konfigurieren; kein zweites `NetworkObject` auf dem Kind anlegen. Prefab bei Fusion registrieren.
- CMP-Spawn auf das neue Prefab umstellen. Spawnpositionen aus expliziten Spawnpunkten beziehen, damit Spieler nicht ineinander oder außerhalb der Umgebung starten.
- Ownership beim Spawn korrekt setzen. Lokale Kamera, Audio Listener, Eingaben und lokale UI nur auf dem Spieler mit Input Authority aktivieren.
- Bei Proxies Kamera, Audio Listener, UI-Eingabe und FS-Eingabe deaktivieren. FS-Controller darf den Proxy-Root nicht bewegen.
- CMP-Aufrufe an `StarterAssetsInputs` nur ersetzen, wenn die betroffene Funktion (z. B. Voice Push-to-Talk oder Pause) tatsächlich gebraucht wird. Keine Adaptertypen voraussetzen, bevor sie implementiert sind.
- Spawn und Cleanup idempotent machen: pro Spieler genau ein Objekt; beim Verlassen oder Shutdown keine verwaisten Spieler oder Runner.

**Abnahme:** In zwei Builds oder zwei getrennten Editor-Instanzen kann jeder Client genau einen lokalen FS-Spieler steuern; beide Clients sehen beide Spieler; weder doppelte Spawns noch Kamerakonflikte treten auf.

### Etappe 2 — Laufen, Drehen, Springen und Landen stabilisieren

- Zuerst einfache Locomotion synchronisieren; Parkour-, Kletter- und Root-Motion-Aktionen in diesem Schritt noch deaktiviert lassen.
- Prüfen, dass die Netzwerk-Transform-Komponente und der FS-CharacterController nicht gleichzeitig die Proxyposition schreiben.
- Proxy-Animator aus replizierten Bewegungsgrößen/Zuständen speisen. Nicht gleichzeitig Root Motion auf den Netzwerk-Root anwenden.
- Spawn-/Respawn-Teleports über die Fusion-Transform-API ausführen und CharacterController-Zustand dabei kontrolliert aktualisieren.
- Bewegung bei schwankender Framerate, unterschiedlichem Ping, kurzen Paketverzögerungen und Kollisionen mit derselben Umgebung beobachten.
- Falls Standard-Transform-Synchronisierung sichtbar ruckelt, zuerst Sendetakt, Interpolation und Tick-Einstellungen messen und justieren, statt mehrere Bewegungs- oder Animator-Systeme übereinanderzulegen.

**Abnahme:** Beide Spieler laufen, drehen, springen und landen wiederholt; die eigene Steuerung fühlt sich direkt an; der entfernte Spieler bewegt sich ohne dauerhafte Teleports oder sichtbares Zittern; lokale Eingaben steuern niemals den Proxy.

### Etappe 3 — Spielszene und Sitzung verlässlich machen

- Einen eindeutigen Szenenfluss definieren: Menü/Lobby → Fusion-Sitzung → gemeinsame Spielumgebung → Rückkehr ins Menü.
- Sicherstellen, dass alle Clients dieselbe Umgebungs-Szene laden und additive Szenen beim Verlassen wieder entladen werden.
- Ungültigen Szenenindex, fehlendes Spawnziel, fehlendes Prefab und fehlgeschlagenes `StartGame` mit verständlichen Logs/Fehlerzuständen behandeln.
- Join während Szenenladung, Raumbeitritt nach Raumauflösung, Host-/Peer-Leave und Shutdown einmal gezielt durchspielen.

**Abnahme:** Zwei Clients können mehrmals hintereinander erstellen/beitreten/verlassen, ohne doppelte Runner, festhängende Ladebilder, verwaiste Spieler oder abweichende Umgebungen.

### Etappe 4 — Parkour-Funktionen kontrolliert aktivieren

Erst nach Abnahme der Etappen 1–3 werden Vault, Predictive Jump, Climb, Hang und Wall Run einzeln wieder aktiviert. Für jede Aktion werden mindestens Aktionstyp, Start/Ende und notwendige Ziel-/Hang-Referenz repliziert. Es wird festgelegt, ob der State Authority die Aktion bestätigt oder ob die Clients sie anhand der identischen Umgebung lokal vorhersagen. Proxy-Animation und Netzwerkbewegung dürfen nie gleichzeitig denselben Root bewegen.

### Etappe 5 — Wettkampf-MVP (nächster Schwerpunkt)

In kleinen, getrennt abnehmbaren Schritten umsetzen:

1. **Rennzustand und Countdown:** `Waiting → Countdown → Running → Finished`; Start erst, wenn alle aktuell erwarteten Spieler geladen und bereit sind. Countdown und Starttick müssen für alle Clients identisch sein. Bis zum Start lokale FS-Eingabe sperren, ohne Netzwerkbewegung oder Kamera zu deaktivieren.
2. **Checkpoints:** Szenenmarker erhalten eine eindeutige Reihenfolge. Fortschritt und zuletzt gültiger Respawnpunkt werden pro Spieler geführt; ein Proxy darf nicht den Fortschritt eines anderen Spielers ändern.
3. **Ziel und Ergebnis:** Zielkontakt stoppt nur die eigene Zeit; anschließend werden Zeiten und Platzierung sichtbar. Bei Gleichstand dieselbe Platzierung oder klar definierte Zeitauflösung.
4. **Rematch und Abbruch:** Runde kontrolliert zurücksetzen; Leave während Countdown/Rennen darf keinen verwaisten Rundenzustand hinterlassen.

Für den ersten Prototyp Shared Mode beibehalten und Ergebnisse als **nicht vertrauenswürdig** behandeln: Spieler können ihre eigene State Authority besitzen und lokale Zeiten beeinflussen. Vor Ranglisten oder öffentlichen Wettbewerben muss die Autoritätsarchitektur neu bewertet werden. Voice-Feinschliff und Hostmigration bleiben außerhalb des Renn-MVP.

**Nächster konkreter Schritt:** Vor Codeänderungen einen Git-Checkpoint setzen und den Renn-MVP auf genau einen kleinen vertikalen Abschnitt begrenzen: synchroner Countdown plus Start/Ende der Zeitmessung auf einer Teststrecke. Checkpoints folgen erst, wenn dieser Ablauf in zwei Builds stabil ist.

## Vorgehen zur Fehlervermeidung

- Pro Etappe nur eine Zuständigkeit ändern (Spawn, Ownership, Bewegung, Animation, Szenenfluss) und danach den Abnahmepunkt prüfen.
- Asset-Originale nicht überschreiben; Änderungen an eigenen Prefab-Kopien und eigenen Integrationsskripten halten.
- Keine Netzwerk- oder Input-Adapterdateien als vorhanden behandeln, solange sie nicht im Projekt liegen und im Editor kompiliert sind.
- Nicht gleichzeitig `NetworkTransform`, CharacterController-/Root-Motion-Code und Proxy-Interpolation die Root-Position schreiben lassen.
- Keine Parkour-Aktionen hinzufügen, bevor gewöhnliches Laufen/Springen im Netzwerk stabil ist.
- Editor-Konsole nach jeder Änderung auf Compiler-, Prefab-, Fusion-Registrierungs- und Laufzeitfehler prüfen.
- Jede Etappe mit zwei unabhängigen Clients prüfen; ein einzelner Editor-Client kann Ownership- und Proxyfehler nicht zeigen.
- Änderungen vor dem nächsten Schritt sichern, damit Regressionen auf den letzten funktionierenden Zustand zurückführbar sind.

## Nächster konkreter Schritt

Etappe 3 ist abgenommen. Vor dem Renn-MVP einen Git-Checkpoint anlegen und dann Countdown sowie Start/Ende der Zeitmessung als ersten vertikalen Abschnitt umsetzen. Checkpoints und Rangliste kommen erst danach.
