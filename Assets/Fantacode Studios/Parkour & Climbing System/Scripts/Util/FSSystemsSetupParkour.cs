#if UNITY_EDITOR
using FS_ParkourSystem;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace FS_Core
{
    public partial class FSSystemsSetup
    {
        public static FSSystemInfo ParkourAndClimbingSystemSetup = new FSSystemInfo
        (
            characterType: CharacterType.Player,
            selected: false,
            systemName: "Parkour And Climbing System",
            displayName: "Parkour and Climbing",
            prefabName: "Parkour Controller",
            welcomeEditorShowKey: "ParkourAndClimbingSystem_WelcomeWindow_Opened_1",
            uiPrefabNames: new[] { "Parkour Mobile Controller"},

            extraSetupActionPlayer: (GameObject playerObject, GameObject playerParentObjectPrefab, GameObject createdPlayerParentObject) =>
            {
                var mc = createdPlayerParentObject.transform.Find("Game UI/Parkour Mobile Controller");
                var parkourUI = mc.GetComponent<ParkourUI>();
                var parkourInputManager = playerObject.GetComponent<ParkourInputManager>();
                parkourInputManager.SetParkourUI(parkourUI);
            }
        );

        static string ParkourAndClimbingSystemWelcomeEditorKey => ParkourAndClimbingSystemSetup.welcomeEditorShowKey;


        [InitializeOnLoadMethod]
        public static void LoadParkourAndClimbingSystem()
        {
            NotifyNewInstall(ParkourAndClimbingSystemWelcomeEditorKey);
        }
    }
}
#endif