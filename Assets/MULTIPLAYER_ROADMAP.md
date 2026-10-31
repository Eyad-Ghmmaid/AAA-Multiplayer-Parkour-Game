# Multiplayer-Parkour: Umsetzungsplan

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
3. **Ein eigenes FS-basiertes Netzwerk-Spieler-Prefab erstellen.** Nicht beide vollständigen Player-Prefabs kombinieren. FS bleibt Quelle für Controller, Animator, Parkour-System, Kamera und FS-Eingabe. Aus CMP werden nur tatsächlich benötigte Netzwerk-, Voice- und Spieleridentitäts-Komponenten übernommen.
4. **Für den MVP die lokale State Authority bewegen lassen.** Lokale FS-Eingabe bewegt den eigenen Spieler; Fusion synchronisiert dessen Netzwerk-Transform für andere Clients. Es gibt keinen zweiten Bewegungs-Simulator auf einem Proxy.
5. **Netzwerkzustand und Darstellung trennen.** Die Netzwerk-Transform-Komponente kontrolliert die replizierte Position. Proxies zeigen interpolierte Bewegung und Animation, lesen aber keine lokale Eingabe und wenden keine Root Motion auf den replizierten Root-Transform an.

## Etappen und Abnahmekriterien

### Etappe 0 — Projekt absichern und Referenzverhalten festhalten

- Eine Sicherung bzw. ein sauberer Versionskontroll-Checkpoint vor Prefab- und Szenenänderungen anlegen.
- Im Unity-Editor notieren: Unity- und Fusion-Version, aktive Input-Handling-Einstellung, vorhandene Build-Szenen, Fusion-Prefab-Registrierung und tatsächliche Spieler-Prefab-Komponenten.
- FS-Parkour-Spieler offline in seiner Demo-Szene starten und normales Laufen, Kamera, Springen und Landen als Referenz festhalten.
- CMP-Lobby mit zwei lokalen Instanzen starten und Join/Leave sowie den bestehenden Szenenwechsel als Referenz festhalten.

**Abnahme:** Beide Assets funktionieren in ihren bestehenden Referenzabläufen, bevor die Verbindung verändert wird. Fehler sind dadurch eindeutig dem Integrationsschritt zuzuordnen.

### Etappe 1 — Netzwerk-Spieler integrieren (erster Umsetzungsschwerpunkt)

- Ein separates Prefab, z. B. `FS Network Player`, aus dem FS-Spieler aufbauen; Original-Prefabs unverändert lassen.
- Fusion-`NetworkObject` und eine geeignete Fusion-Transform-Replikation am Netzwerk-Root konfigurieren und das Prefab bei Fusion registrieren.
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

### Etappe 5 — Wettkampf und Komfortfunktionen

- **Bestätigt:** Zwei unabhängige Clients sehen und steuern ihre Spieler samt Animationen korrekt. Szenenwechsel, Leave, erneutes Erstellen und Beitreten wurden ebenfalls erfolgreich getestet.
- **Implementiert, Editor-Einrichtung offen:** Netzwerkweiter Ready-Status, Teleport aller bereiten Spieler an eine Formation, gemeinsamer 3-Sekunden-Countdown, lokale Eingabesperre und individuelle Zeitmessung. Der Ready-Button ruft `InGame_Manager.ToggleReady()` über `RaceReadyButtonUI` auf.
- **Pro Map einzurichten:** Ein leeres `RaceStartPoint`-Objekt auf dem Boden am Start platzieren. Seine Vorwärtsrichtung zeigt die Laufrichtung. `Player Spacing`, `Row Spacing` und `Players Per Row` lassen sich pro Map einstellen. Ein separates Zielobjekt mit `BoxCollider` und `RaceFinishTrigger` beendet die Zeit beim Betreten.
- **Nächster Prüfschritt:** Ready-Button und Status-Text in der Game-UI mit `RaceReadyButtonUI` verbinden; in jeder Map `RaceStartPoint` und Ziellinie platzieren. Dann mit zwei Clients prüfen, dass beide Spieler erst bei vollständiger Bereitschaft nebeneinander aufgestellt werden und denselben Countdown erhalten.
- **Checkpoints/Respawn implementiert und vom Nutzer im Build geprüft:** Checkpoints speichern den höchsten erreichten Index je Spieler; ein map-spezifischer Fall-Trigger respawnt nur den betroffenen Spieler am Checkpoint bzw. Start.
- **Ergebnisanzeige/Rematch implementiert, UI-Verknüpfung und Build-Prüfung offen:** Zielzeiten werden netzwerkweit gespeichert und nach Platzierung sortiert. Die Ergebnisanzeige bleibt während der Rematch-Bereitschaft sichtbar; der Ready-Button wird zur Rematch-Bestätigung. Nach Bereitschaft aller Room-Spieler beginnt die nächste Runde.
- Nächster Prüfschritt: Ergebnis-Panel und TMP-Text in `RaceResultsPanelUI` zuweisen; mit zwei Clients Zielreihenfolge, angezeigte Zeiten und Rematch testen.
- Für verbindliche Ranglisten wird die Eignung von Shared Mode neu geprüft; Ergebnisse aus einem Peer-authoritativen Modus sind nicht serverseitig vertrauenswürdig.

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

Ergebnis-Panel und Ergebnistext mit `RaceResultsPanelUI` verbinden. Danach in zwei Builds Zielreihenfolge, geteilte Zeiten, Rematch-Bereitschaft und gemeinsamen Countdown prüfen.
