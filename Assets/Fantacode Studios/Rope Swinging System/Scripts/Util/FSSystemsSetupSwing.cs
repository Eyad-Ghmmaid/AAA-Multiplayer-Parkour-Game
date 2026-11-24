#if UNITY_EDITOR
using FS_SwingSystem;
using UnityEditor;
using UnityEngine;

namespace FS_Core
{
    public partial class FSSystemsSetup
    {
        public static FSSystemInfo RopeSwingingSystemSetup = new FSSystemInfo
        (
            characterType: CharacterType.Player,
            selected: false,
            systemName: "Rope Swinging System",
            displayName: "Rope Swing",
            prefabName: "Swinging Controller",
            welcomeEditorShowKey: "RopeSwingingSystem_WelcomeWindow_Opened",
            uiPrefabNames: new[] { "Swing Mobile Controller" },

            extraSetupActionPlayer: (GameObject playerObject, GameObject playerParentObjectPrefab, GameObject createdPlayerParentObject) =>
            {
                var mc = createdPlayerParentObject.transform.Find("Game UI/Swing Mobile Controller");
                if (mc == null) return;

                var ui = mc.GetComponent<SwingUI>();
                var inputManager = playerObject.GetComponent<SwingInputManager>();
                if (inputManager != null && ui != null)
                    inputManager.SetSwingUI(ui);
            }
        );

        static string RopeSwingingSystemWelcomeEditorKey => RopeSwingingSystemSetup.welcomeEditorShowKey;


        [InitializeOnLoadMethod]
        public static void LoadRopeSwingingSystem()
        {
            NotifyNewInstall(RopeSwingingSystemWelcomeEditorKey);
        }
    }
}
#endif

