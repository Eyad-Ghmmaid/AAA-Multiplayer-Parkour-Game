#if UNITY_EDITOR
using FS_InteractionSystem;
using FS_LocomotionSystem;
using FS_Util;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;
using AnimatorController = UnityEditor.Animations.AnimatorController;

namespace FS_Core
{
    public partial class FSSystemsSetup : MonoBehaviour
    {
        public static string welcomeWindowOpenKey = "FS_WelcomeWindow_Opened";

        // Per-project persistence for each system's "welcome window already shown" flag.
        // PlayerPrefs persist per-machine, not per-project: the very first time a system is
        // installed on this machine its flag is set and never cleared, which then suppresses
        // the setup window in every other project (and on every reinstall). Scoping the key to
        // the project's unique GUID makes first-install detection work per-project. Presence of
        // the key means "already shown".
        public static bool HasShownWelcomeWindow(string welcomeEditorShowKey)
            => !string.IsNullOrEmpty(welcomeEditorShowKey)
               && PlayerPrefs.HasKey(ProjectScopedWelcomeKey(welcomeEditorShowKey));

        public static void MarkWelcomeWindowShown(string welcomeEditorShowKey)
        {
            if (!string.IsNullOrEmpty(welcomeEditorShowKey))
            {
                PlayerPrefs.SetString(ProjectScopedWelcomeKey(welcomeEditorShowKey), "");
                PlayerPrefs.Save();
            }
        }

        // Clears a system's "already shown" flag so the setup window re-triggers on the next
        // reload/import. Used by the Tools > FS System > Reset Setup Popup menu item.
        public static void ClearWelcomeWindowShown(string welcomeEditorShowKey)
        {
            if (!string.IsNullOrEmpty(welcomeEditorShowKey))
            {
                PlayerPrefs.DeleteKey(ProjectScopedWelcomeKey(welcomeEditorShowKey));
                PlayerPrefs.Save();
            }
        }

        static string ProjectScopedWelcomeKey(string welcomeEditorShowKey)
            => $"{PlayerSettings.productGUID}_{welcomeEditorShowKey}";

        // ----- New-install popup coordination ------------------------------------------------
        // Called by each system's install detection ([InitializeOnLoadMethod]) on first sight.
        // The "already shown" PlayerPrefs flag is committed ONLY after the window actually opens
        // (CommitPendingWelcomeKeys), not here. A package import triggers several domain reloads
        // in a row; the pending list lives in SessionState (survives reloads within a session),
        // so if a reload wipes the scheduled open before it runs, the next reload re-schedules
        // it. Previously the flag was written up-front and a lost/late reload swallowed the
        // popup permanently.
        const string PendingWelcomeKeysSessionKey = "FS_PendingWelcomeKeys";

        public static void NotifyNewInstall(string welcomeEditorShowKey)
        {
            if (string.IsNullOrEmpty(welcomeEditorShowKey))
                return;

            if (HasShownWelcomeWindow(welcomeEditorShowKey))
                return;

            AddPendingWelcomeKey(welcomeEditorShowKey);
            FSSystemsSetupEditorWindow.RequestInstallPopup();
        }

        public static List<string> GetPendingWelcomeKeys()
        {
            var raw = SessionState.GetString(PendingWelcomeKeysSessionKey, "");
            return string.IsNullOrEmpty(raw)
                ? new List<string>()
                : raw.Split(';').Where(s => !string.IsNullOrEmpty(s)).Distinct().ToList();
        }

        static void AddPendingWelcomeKey(string welcomeEditorShowKey)
        {
            var pending = GetPendingWelcomeKeys();
            if (!pending.Contains(welcomeEditorShowKey))
            {
                pending.Add(welcomeEditorShowKey);
                SessionState.SetString(PendingWelcomeKeysSessionKey, string.Join(";", pending));
            }
        }

        // Commits every pending system as "shown" — call this only once the window has actually
        // opened, so an interrupted import can never suppress a popup that was never seen.
        public static void CommitPendingWelcomeKeys()
        {
            var pending = GetPendingWelcomeKeys();
            if (pending.Count == 0)
                return;

            foreach (var key in pending)
                MarkWelcomeWindowShown(key);

            SessionState.EraseString(PendingWelcomeKeysSessionKey);
        }

        // Drops any pending (not-yet-committed) install popups without marking them shown.
        public static void ClearPendingWelcomeKeys()
            => SessionState.EraseString(PendingWelcomeKeysSessionKey);

        public static List<string> layers = new List<string> { "Ledge", "Player", "Enemy", "RagdollBone", "Deadbody", "NPC", "Vehicle", "VisionSensor", "FootTrigger", "Cover", "Hotspot", "PlayerAlly", "EnvironmentFinisherProp", "OcuppantObject" };
        public static List<string> tags = new List<string>() { "Hitbox", "NarrowBeam", "SwingableLedge", "Slide", "NavmeshLink", "NavmeshSurface" };

        public static FSSystemInfo ThirdPersonControllerSystemSetup = new FSSystemInfo
        (
            characterType: CharacterType.Player,
            selected: true,
            systemName: "Locomotion System",
            displayName: "Locomotion",

            prefabName: "Locomotion Controller",
            welcomeEditorShowKey: "LocomotionSystem_WelcomeWindow_Opened_3",
            uiPrefabNames: new[] { "Locomotion Mobile Controller", "Interaction Mobile Controller" },

            extraSetupActionPlayer: (GameObject playerObject, GameObject playerParentObjectPrefab, GameObject createdPlayerParentObject) =>
            {
                 var mc = createdPlayerParentObject.transform.Find("Game UI/Locomotion Mobile Controller");
                 var locomotionUI = mc.GetComponent<LocomotionUI>();
                 var locomotionInputManager = playerObject.GetComponent<LocomotionInputManager>();
                 locomotionInputManager.SetLocomotionUI(locomotionUI);
                 
                 var cameraInputController = locomotionUI.GetComponentInChildren<MobileCameraInputController>();
                 if (cameraInputController != null)
                     cameraInputController.locomotionInput = locomotionInputManager;

                mc = createdPlayerParentObject.transform.Find("Game UI/Interaction Mobile Controller");
                var interactionUI = mc.GetComponent<InteractionUI>();
                var interactionInputManager = playerObject.GetComponent<InteractionInputManager>();
                interactionInputManager.SetInteractionUI(interactionUI);
            }
        );

        static string LocomotionSystemWelcomeEditorKey => ThirdPersonControllerSystemSetup.welcomeEditorShowKey;

        [InitializeOnLoadMethod]
        public static void LoadLocomotionSystem()
        {
            NotifyNewInstall(LocomotionSystemWelcomeEditorKey);
        }

        private void Awake()
        {
            this.enabled = false;
        }

        // Filtered systems by current selected charcater type from window
        public Dictionary<string, FSSystemInfo> CurrentFSSystemsForSetup = new Dictionary<string, FSSystemInfo>();
        // All installed FS Systems
        public Dictionary<string, FSSystemInfo> AllInstalledSystems = new Dictionary<string, FSSystemInfo>();


        public void FindSystem()
        {
            AllInstalledSystems.Clear();
            CurrentFSSystemsForSetup.Clear();
            FieldInfo[] fields = GetType().GetFields(BindingFlags.Public | BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Static);

            foreach (var field in fields)
            {
                // Check if the field's type is FSSystem
                if (field.FieldType == typeof(FSSystemInfo))
                {
                    // Get the value of the field and cast it to FSSystem
                    FSSystemInfo system = field.GetValue(this) as FSSystemInfo;

                    if (system != null)
                    {
                        if (system.characterType == FSSystemsSetupEditorWindow.characterType)
                            CurrentFSSystemsForSetup.TryAdd(field.Name, system);
                        AllInstalledSystems.TryAdd(field.Name, system);
                    }
                }
            }
        }
        public void EnableSystems(params string[] systemNames)
        {
            foreach (var s in CurrentFSSystemsForSetup)
            {
                if (systemNames.Contains(s.Value.systemName))
                    s.Value.selected = true;
                else
                    s.Value.selected = false;
            }
        }

        /// <summary>
        /// Loads the prefab from Resources and copies all its components to this GameObject.
        /// </summary>
        public GameObject CopyComponentsAndAnimControllerFromPrefab(string prefabName, AnimatorMergerUtility animatorMergerUtility, GameObject characterObject)
        {
            if (!string.IsNullOrEmpty(prefabName))
            {
                // Load the prefab from Resources
                GameObject prefab = Resources.Load<GameObject>(prefabName);
                bool isPlayer = FSSystemsSetupEditorWindow.characterType == CharacterType.Player;
                if (prefab != null)
                {
                    var characterPrefab = isPlayer ? prefab.GetComponentInChildren<PlayerController>().gameObject : prefab;

                    var animatorController = characterPrefab.GetComponent<Animator>().runtimeAnimatorController as AnimatorController;

                    animatorMergerUtility.MergeAnimatorControllers(animatorController);

                    FSSystemsSetupEditorWindow.CopyComponents(characterPrefab, characterObject);

                    //if (isPlayer)
                    //{
                    //    var managedScript = characterObject.GetComponents<SystemBase>().ToList();
                    //    managedScript.Sort((x, y) => x.Priority.CompareTo(y.Priority));
                    //    characterObject.GetComponent<PlayerController>().managedScripts = managedScript;
                    //}
                }
                return prefab;
            }
            else
            {
                //Debug.LogWarning("Prefab name is not specified.");
            }
            return null;
        }

        public static List<(string, string)> ignoredLayerCollisions = new()
        {
            ("RagdollBone", "Player"),
            ("RagdollBone", "Enemy"),
            ("RagdollBone", "PlayerAlly"),
            ("RagdollBone", "NPC"),
        };

        static void ApplyIgnoredLayerCollisions()
        {
            foreach (var (a, b) in ignoredLayerCollisions)
                SetLayerCollision(a, b, ignore: true);
        }

        static void SetLayerCollision(string layerA, string layerB, bool ignore)
        {
            int a = LayerMask.NameToLayer(layerA);
            int b = LayerMask.NameToLayer(layerB);
            if (a < 0 || b < 0)
            {
                Debug.LogWarning($"[FS Systems Setup] Cannot set collision between '{layerA}' and '{layerB}' — layer missing.");
                return;
            }
            Physics.IgnoreLayerCollision(a, b, ignore);
        }

        public void ImportProjectSettings()
        {
            FindSystem();
            SerializedObject tagManager = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
            SerializedProperty layersProp = tagManager.FindProperty("layers");


            for (int i = 0; i < FSSystemsSetup.layers.Count; i++)
            {
                string layerName = FSSystemsSetup.layers[i];

                if (!string.IsNullOrEmpty(layerName))
                {
                    bool layerExists = false;
                    for (int j = 0; j < layersProp.arraySize; j++)
                    {
                        SerializedProperty layerProp = layersProp.GetArrayElementAtIndex(j);
                        if (layerProp.stringValue == layerName)
                        {
                            layerExists = true;
                            break;
                        }
                    }

                    if (!layerExists)
                    {
                        for (int j = 7; j < layersProp.arraySize; j++)
                        {
                            SerializedProperty layerProp = layersProp.GetArrayElementAtIndex(j);
                            if (string.IsNullOrEmpty(layerProp.stringValue))
                            {
                                layerProp.stringValue = layerName;
                                break;
                            }
                        }
                    }
                }
            }
            tagManager.ApplyModifiedProperties();

            ApplyIgnoredLayerCollisions();

            foreach (var tag in FSSystemsSetup.tags)
            {
                if (!InternalEditorUtility.tags.ToList().Contains(tag))
                    InternalEditorUtility.AddTag(tag);
            }


            foreach (var systemProjectSettingsData in AllInstalledSystems.Values)
            {
                if (systemProjectSettingsData.OnInstallation == null) 
                    continue;

                systemProjectSettingsData.OnInstallation?.Invoke();

                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
            }

            var asset = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/ProjectSettings.asset")[0];
            var serializedObject = new SerializedObject(asset);
            var property = serializedObject.FindProperty("activeInputHandler");

            if (property.intValue == 1)
            {
                ScriptingDefineSymbolController.ToggleScriptingDefineSymbol(FSSystemsSetupEditorWindow.inputsystem, true);
            }
        }
    }
    public enum CharacterType
    {
        Player,
        AI,
        None
    }

    [Serializable]
    public class FSSystemInfo
    {
        public CharacterType characterType;
        public bool selected;
        public string systemName;
        public string displayName;
        public string prefabName;
        public string[] uiPrefabNames;
        public Action<GameObject, GameObject, GameObject> extraSetupActionPlayer;
        public Action<GameObject, GameObject> extraSetupActionAI;
        public Action OnInstallation;
        public string welcomeEditorShowKey;

        public Action<bool> OnSelectionChanged;
        public FSSystemInfo(CharacterType characterType, string systemName, string displayName = "", string prefabName = "", bool selected = false, string welcomeEditorShowKey = "", Action OnInstallation = null, string[] uiPrefabNames = null, Action<GameObject, GameObject, GameObject> extraSetupActionPlayer = null, Action<GameObject, GameObject> extraSetupActionAI = null)
        {
            this.characterType = characterType;
            this.selected = selected;
            this.systemName = systemName;
            this.displayName = displayName;
            this.prefabName = prefabName;
            this.OnInstallation = OnInstallation;
            this.uiPrefabNames = uiPrefabNames ?? System.Array.Empty<string>();

            if (extraSetupActionPlayer != null)
                this.extraSetupActionPlayer = extraSetupActionPlayer;
            if (extraSetupActionAI != null)
                this.extraSetupActionAI = extraSetupActionAI;

            if (!string.IsNullOrEmpty(welcomeEditorShowKey))
                this.welcomeEditorShowKey = welcomeEditorShowKey;
            else
                this.welcomeEditorShowKey = FSSystemsSetup.welcomeWindowOpenKey;
        }

    }
}
#endif