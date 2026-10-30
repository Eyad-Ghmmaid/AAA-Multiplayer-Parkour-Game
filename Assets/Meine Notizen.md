# Build-Szenen
    - Welche Szenen sind drin?  Menu, Lobby, Game, Environment 1, Environment 2
# Fusion-Prefab-Registrierung
    -- Welches Prefab ist aktuell der CMP-Spieler (NetworkRunner)? 
    --- Red Ball
    --- Yellow Ball
    --- Player Prefab Green
    --- Player Prefab Red
    --- Player Prefab Yellow
    --- NetworkObject
    
    
# CMP-Spieler-Prefab-Komponenten
    -  Öffne das Prefab Player Prefab Green
        - Welche Fusion-Komponenten sind drauf? (Animator, Character Controller, Network Object, Network Mecanim Animator, Network Transform (Script),Player Stats (Script),Get Player Camera And Controls (Script), Player Pos Resetter (Script), Player World UI Manager (Script),Rigidbody)
        - Welche Input-Komponenten? (Third Person Controller (Script),Basic Rigid Body Push (Script), Starter Assets Inputs (Script), Player Input).
        - Welche UI-/Voice-Komponenten? (Chat Player (Script) )

# Player prefab Green Aufbau
- Player Prefab Green (ist der Root) {
 Child von Root: 
    - Player Render {
        Children Vom PLayer Render{
            - Player Name ( ist Text Mesh pro und """ hat den Skript look at Camera""")
            - IpMaleG (hat den Komponent Skinned MeshRenderer)
            - rig_CharRoot ( die Einzel Kochen des Charackter)
            - Glow light ( Visual Effect, Komponenten: Light, Universal Additional Light Data (Script))
            - Canvas {
                - Background (Komponenten: Canvas Renderer, Image, Content Size Fitter, Resize Background To Text (Script)) {
                    - Text (TMP) (ist ein Text Mesh Pro)
                }
            }
            - Voice Object ( ist ein Image und hat, Kopenenten: Look at Camera)
            - CanvasContainer ( Empty Objekt mit Kopenenten: Look at Camera )
        }
    }
 Children vom Player Prefab Green:{
    - PlayerCameraRoot (Empty Objekt)
    - Interpolation target (Empty Objekt)
    - VoiceNetworkObject (hat Kopenenten: Voice Network Object)
    }   
} 
## Ende vom Player prefab Green

# FS PLayer Prefab (Offline/ Parkour Asset)
- Parkour Player(ist der Root und hat diese Komponenten: Equip Settings (Script),FS Settings (Script)){
    - Player (Transform, Player Controller (Script), Locomotion Controller (Script), Environment Scanner (Script), Foot Step Effects (Script), Item Equipper (Script), Item Attacher (Script), Quick Switch Handler (Script), Damagable (Script), Bone Mapper (Script), Anim Graph (Script), Action Executor (Script), Locomotion Core (Script), Animator,  Character Controller, Character Capsule (Script), Character Controller Driver (Script), Input Manager Hub (Script), Character ID (Script), Parkour Core (Script), Parkour Controller (Script), Climb Core (Script), Climb Controller (Script), Sliding Controller (Script), Armor Handler (Script), Hotspot Controller (Script), Throwable Handler (Script), Throwable Controller (Script), Active Ragdoll System (Script), Interaction Executor (Script)) {
      
        Default Character(Clone){
            Armature{
                Hips ( Konchen vom player)
            }
            Decal
            Retopo_geometry_0
        }
    }
    - Third Person Camera (Komponenten: Transform, Camera, Audio Listener, Camera Controller (Script), Universal Additional Camera Data (Script))
    - Game UI (ist ein Canvas mit folgende Skripte: FS_GameUi){
        // die Drei Objekte sind für Mobil (nicht Wichtig für mein Game Ziel)
        - Locomotion Mobile Controller
        - Interaction Mobile Controller
        - Parkour Mobile Controller
    }
    - Quick Switch UI Canvas (ist ein canva){
        Quick Switch {
            - Center Image (Image)
        }
    }
    - EventSystem
    - HP Bar (HP Bar (Script)) {
    - Canvas {
            - BG (Image)
            - BG1 (Image)
        }
    }
}
## Ende vom Player prefab (Parkour Game Asset)

# Menu Scene
## Objekte in der Hierachie:
- Character_selectionUI
- Environment
- EventSystem
- Fusion Manager (Fusion Connection)
- Global Volume
- Main Camera
- Menu Manager (Menu (skript))
- MenuCanvas
- VoiceLogger (Voice Logger)
# Game Scene
- EventSystem
- GameCanvas
- Global Volume
- Inventory System
- Main Camera (CinemachineBrain)
- Manager (In Game_Manager (Script))
- Network PickUp Spawner
- Mobile Controls
- Player Follow Camera (CinemachineVirtualCamera)
- VoiceLogger


 

# Tests Ergbnisse:
    ## FS-Parkour offline testen: Ergbnis: ohne Probleme
    ## CMP-Lobby mit zwei Instanzen testen: Alles funktioniert aber man wenn der Host Player Prefab Yellow auswählt, und der andere Spieler red auswählt, bekommen beide Players das gleiche Farbe, also man kann nicht unterschiedliche Charaktere auswählen. 


