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

- **Proxy-Hand-IK beim Hängen ergänzt, Build-Prüfung offen:** State Authority sendet während eines stabilen Hängens die beiden Handziele. Der Proxy verwendet diese Weltpositionen im Animator-IK-Pass, weil lokale `ClimbPoint`- und IK-Laufzeitreferenzen dort nicht existieren.

### Etappe 5 — Wettkampf und Komfortfunktionen

- **Bestätigt:** Zwei unabhängige Clients sehen und steuern ihre Spieler samt Animationen korrekt. Szenenwechsel, Leave, erneutes Erstellen und Beitreten wurden ebenfalls erfolgreich getestet.
- **Ready/Countdown im Build geprüft:** Netzwerkweiter Ready-Status, Teleport aller bereiten Spieler an eine Formation, gemeinsamer 3-Sekunden-Countdown, lokale Eingabesperre und individuelle Zeitmessung. Der Ready-Button ruft `InGame_Manager.ToggleReady()` über `RaceReadyButtonUI` auf.
- **Pro Map einzurichten:** Ein leeres `RaceStartPoint`-Objekt auf dem Boden am Start platzieren. Seine Vorwärtsrichtung zeigt die Laufrichtung. `Player Spacing`, `Row Spacing` und `Players Per Row` lassen sich pro Map einstellen. Ein separates Zielobjekt mit `BoxCollider` und `RaceFinishTrigger` beendet die Zeit beim Betreten.
- Ready-Button und Status-Text sind verbunden; Startpunkt und Ziellinie sind in der Testmap eingerichtet.
- **Checkpoints/Respawn implementiert und vom Nutzer im Build geprüft:** Checkpoints speichern den höchsten erreichten Index je Spieler; ein map-spezifischer Fall-Trigger respawnt nur den betroffenen Spieler am Checkpoint bzw. Start.
- **Ergebnisanzeige/Rematch im Build geprüft:** Zielzeiten werden netzwerkweit gespeichert und nach Platzierung sortiert. Die Ergebnisanzeige bleibt während der Rematch-Bereitschaft sichtbar; der Ready-Button wird zur Rematch-Bestätigung. Nach Bereitschaft aller Room-Spieler beginnt die nächste Runde.
- **Late Join während eines Rennens, vom Nutzer mehrfach geprüft:** Späte Beitritte werden für diese Runde als Zuschauer behandelt, erhalten keinen Renn-Timer und blockieren das Rundenende nicht. Sie können beim Rematch bereit stimmen und ab der nächsten Runde regulär teilnehmen. Der Nutzer meldet nach mehreren Tests bisher keine weiteren Probleme.
- **Stabilitätsdurchgang:** Zwei unabhängige Clients konnten Room, Rennen und Bewegungen ohne Probleme testen. Der Nutzer bestätigte zusätzlich, dass ein dritter Spieler beim Beitritt während eines Rennens bis zum Rundenende zuschaut. Bei weiteren Sessions sollten Room-Leave, erneutes Beitreten und Peer-Abbruch weiter beobachtet werden.
- **Erste Ergebnis-Härtung:** Clients übermitteln keine Zeit mehr. Die Fusion-State-Authority berechnet sie aus ihrer Simulationszeit und akzeptiert einen Zielbericht nur, wenn die replizierte Spielerposition an einem Ziel-Trigger liegt.
- **Gewählte Sicherheitsstufe: Trusted Host.** Der Raumersteller soll Host mit State Authority über Spielerbewegung, Rennzustand und Zieleinlauf werden. Beitretende Clients liefern Eingaben; sie dürfen weder Position noch Zeit/Platzierung selbst festlegen. Der Host bleibt vertrauenswürdig. Ein manipulierter Host kann weiterhin falsche Ergebnisse erzeugen.
- **Trusted Host aktiviert, Zwei-Build-Prüfung offen:** Ein typisierter Fusion-Eingabedatensatz bildet FS-Bewegung, Parkour-Tasten und Blickrichtung ab. `FusionConnection.OnInput` erfasst die Eingabe am lokalen Spieler; `FSNetworkAuthoritySetup` speist sie nur im Host/Server-Modus bei entfernten Spielern ein. Kamera folgt Input Authority, FS-Bewegung folgt State Authority. `FusionConnection` spawnt im Host-Modus genau ein Spielerobjekt je Teilnehmer mit gesetzter Input Authority und despawnt es beim Verlassen. Host-State-Authority sperrt Bewegung während Countdown, für Zuschauer und für bereits fertige Spieler; Checkpoint-Reihenfolge/-Position, Zielkontakt und Zielzeit werden serverseitig geprüft. Rennen-Teleports laufen im Host-Modus direkt über die State Authority. Raumersteller starten jetzt als `GameMode.Host`, Beitretende als `GameMode.Client`.
- **Input-Erfassung korrigiert, Build-Prüfung offen:** `NetworkRunner.ProvideInput` wird beim Einrichten des Runners eingeschaltet, damit Fusion `OnInput` für Host und Client tatsächlich aufruft. Fehlt in einem Tick ein Datensatz, setzt die Host-Bewegungsbrücke die Eingaben auf neutral statt den alten Wert weiterzuverwenden.
- **Host-Bewegung/Spawn weiterhin fehlerhaft:** In den zuletzt verfügbaren Player-Logs sind Verbindung und Spawn belegt, aber der Eingabefluss und die Race-Bewegungssperre nicht protokolliert. Temporäre, auf höchstens eine Meldung alle zwei Sekunden begrenzte Diagnosen ergänzen Eingabesammlung, Authorities, Spawnhöhen und Host-Bewegung.
- **Log-Diagnose und Korrektur:** Beide Runner hatten `ProvideInput=True`, die Spielerobjekte/Authorities und Host-Bewegungsfreigabe waren gültig, aber `DirectionInput` kam als `(0,0)` an. Host/Client-Eingabesammlung liest WASD/Gamepad daher zusätzlich direkt aus dem aktiven Input System; State Authority speist Fusion-Input auch für den Host-Spieler ein. Lokale Ready-Aktion wird auf dem Host direkt angewendet, damit sie nicht von einer lokalen RPC-Source abhängt. Spawnlog mit CC-Höhe 1,85 und Mittelpunkt Y≈0,94 bei Root Y≈0,40 ergibt einen Kapselboden nahe Y=0; das Schweben ist damit zunächst ein Animator-/Grounded-Zustand, keine offensichtlich falsche Spawnhöhe.
- **Input-Nachkorrektur, Build-Prüfung offen:** Host-eigene Eingaben werden wieder lokal vom FS-Inputmanager verarbeitet (Kamera bleibt lokal steuerbar); nur entfernte Spieler nutzen externe Host-Eingabe. WASD/Gamepad direkter Fallback ist im FS-Inputmanager ergänzt. Fusion-Tastenereignisse werden auf Flanken reduziert, damit Mehrfachsimulation in einem Renderframe Jump/Toggle nicht wiederholt auslöst.
- **Host-Modus-Regression nach Rennen-Teleport, Korrektur offen zur Build-Prüfung:** Logs belegten, dass die Host-Instanz den entfernten Client lokal mit `RaceInputLocked=True` sperrte, obwohl die Eingabesperre des Clients lokal verwaltet wird. Das Sperren lokaler FS-Eingaben geschieht beim Teleport nun nur für den eigenen Spieler; entfernte Spieler werden über die autoritative Rennphase gesteuert. Der `RaceStartPoint` in `Environment 1` steht auf Y=0,54, sein BoxCollider ist ein Trigger und der Ground liegt auf Y=0. Da der Teleport den Marker als Bodenhöhe behandelt, ist der Kartenversatz jetzt **-0,54 m**; so landet der CharacterController auf dem Ground, nicht auf dem Trigger. Der Start-Respawn verwendet dieselbe Bodenposition. Nächster Schritt: neuen Host/Client-Build prüfen; falls der Host danach noch feststeht, die `[FS Host Local]`- und `[FS Host Authority]`-Zeilen vergleichen (Input, Sperre, Player-Child-Position und Kapselbodenhöhe).
- **Client zeigt Fallanimation direkt beim Beitritt, Ursachenbereich eingegrenzt:** Der neueste Client-Log zeigt Root Y=0,40 und CharacterController-Mitte Y≈0,94; bei der Prefab-Kapsel liegt der Fußpunkt damit ungefähr auf Y=0. Außerdem ist der Client erwartungsgemäß kein State Authority und führt seinen lokalen PlayerController nicht aus. Das spricht für einen initialen Animator-Grounded-Sync, nicht für einen falschen lokalen Spawn-Y-Wert. Beim Host-Spawn wird nun nach `Physics.SyncTransforms()` einmal `LocomotionCore.GroundCheck()` ausgeführt, damit der initiale `IsGrounded`-Animatorwert vor dem ersten Snapshot gesetzt ist. Lokale Diagnosen erfassen auf Client und Host jetzt getrennt Physik-Grounded und Animator-Grounded. Build-Prüfung offen.
- **Nachtest: Client-Animator kippt nach dem Join auf Fallpose; Host-Bewegung fehlt:** Der neue Client-Log zeigt zunächst `physicsGrounded=True`, danach weiterhin `physicsGrounded=True`, aber `animatorGrounded=False`; die Kapselunterkante bleibt bei Y≈0,02 und `inputLocked=False`. Auf Nicht-State-Authority-Proxies berechnet `FSNetworkAuthoritySetup` `LocomotionCore.GroundCheck()` jetzt in `LateUpdate`, damit ein veralteter Animator-Bool nicht die Fallpose festhält. Bewegungsdiagnosen protokollieren nicht-null Eingaben beim Client und bei der Host-State-Authority sofort. Build-Prüfung offen.
- **Neuer Test: WASD erreicht Host, Client sieht seine eigene Animation nicht zuverlässig:** Build-Logs zeigen nicht-null Client-Eingaben auf dem Host und offene Bewegungssperren. Im Host/Server-Modus führt der Input-Authority-Spielercontroller jetzt zusätzlich lokale FS-Bewegungsvorhersage aus; der Host behält die State Authority über die replizierte Position. Auf dem Input-Authority-Client ist `NetworkMecanimAnimator` deaktiviert, damit die empfangenen Host-Parameter die lokal berechnete eigene Pose nicht überschreiben. Ergänzte Bewegungslogs zeigen `CurrentVelocity`, `PreventAllSystems` und Position zur Abgrenzung von blockiertem Controller und NetworkTransform-Korrektur. Build-Prüfung offen.
- **Animationen stimmen, Translation fehlt weiterhin:** Aktuelle Logs zeigen gültige Eingaben, Grounded=true, keine Rennsperre und FS-Geschwindigkeit bis etwa 4 m/s, während die Child-Position nur gering wandert. `CharacterControllerDriver` protokolliert jetzt auf Network-Spielern den angeforderten Move, tatsächlich angewandte Delta, `CharacterController.velocity` und Kollisionsflags. Damit lässt sich feststellen, ob Physik blockiert oder ein späterer Transform-Schritt die Position zurücksetzt. Build-Prüfung offen.
- **Aktueller Sicherheitsstand:** Das Projekt startet noch in `GameMode.Shared`. Jeder Spieler besitzt Autorität über sein eigenes Bewegungsobjekt; die Raum-Master-Instanz besitzt Autorität über das Szenen-Rennen. Die Finish-Prüfung schützt vor falschen Zeitwerten und offensichtlichen Zielberichten, aber nicht vor einem Client, der seinen eigenen Avatar teleportiert. Die aktuelle Implementierung ist daher noch keine manipulationsresistente Rangliste.
- **Host-Migration: verbleibende Arbeit.** Das aktive Prefab hat keinen `FSInputAdapter`/`FSNetworkInputBridge`; deshalb erfasst `FusionConnection.OnInput` die Werte der vorhandenen `LocomotionInputManager`-/`ParkourInputManager`-Instanzen. Der Host-Spawn und die FS-Eingabeübernahme sind vorbereitet, aber noch nicht im Host-Modus geprüft. Vor Änderung der Session-Modi müssen (1) Unity-Skripte und Prefab kompilieren, (2) Host-seitige Rennphase/Teilnahme/Eingabesperren gegen Client-Manipulation geprüft und autoritativ durchgesetzt werden, (3) FS-Bewegung mit Root Motion und Unity-`Update`/`FixedUpdate` unter Host-Latenz geprüft werden, (4) Rennen, Late Join, Leave und Rematch in zwei Builds funktionieren und (5) erst danach Erstellen auf `GameMode.Host` und Beitreten auf `GameMode.Client` umgestellt werden.
- **Abnahmekriterien Trusted Host:** Zwei unabhängige Builds; Host und Client können sich bewegen und Parkour ausführen; der Host bleibt flüssig steuerbar; beide sehen dieselben autoritativen Positionen/Aktionen; ein Client kann durch lokale Transform-Manipulation keine anderen Spieler oder Rennresultate verschieben; Join/Leave und Late-Join blockieren das Rundenende nicht.
- **Spätere Ranglisten:** Host Authority schützt vor normalen Clients, aber nicht vor einem böswilligen Host. Für öffentliche/kompetitive Ranglisten müssten Bewegung und Zieleinlauf durch einen Dedicated Server autorisiert werden; ein persistentes Backend darf nur bestätigte Ergebnisse annehmen.

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

Als nächstes Host und Client mit zwei Builds testen (Bewegung, Parkour, Rennen/Countdown, Checkpoint/Respawn, Ziel, Late Join, Leave/Rematch). Bei einem Fehler zuerst die Console-Logs beider Builds sichern; die Shared-Mode-Verbindung kann für einen schnellen Rückweg wiederhergestellt werden.
