// using System.Linq;
// using UnityEditor;
// using UnityEditorInternal;
// using UnityEngine;
// using FS_Util;
// namespace FS_ParkourSystem
// {
//     public class WelcomeWindow : EditorWindow
//     {
//         public static WelcomeWindow welcomeWindow;
//         public const string inputsystem = "inputsystem";

//         public static string windowShowedKey = "FC_notShowed";


//         [MenuItem("Tools/FS System/Parkour/Support/Discord")]
//         public static void InitDiscord()
//         {
//             Application.OpenURL("https://discord.gg/QNe4AMYT");
//         }
//         [MenuItem("Tools/FS System/Parkour/Support/Youtube")]
//         public static void InitYoutude()
//         {
//             Application.OpenURL("https://www.youtube.com/playlist?list=PLnbdyws4rcAu8b3zKIyTMKhMK8iHsIaOn");
//         }


//         // [InitializeOnLoadMethod]
//         public static void ShowWindow()
//         {
//             if (PlayerPrefs.GetString(windowShowedKey) != "FC_showed")
//             {
//                 AddNewTags();
//                 InitEditorWindow();
//                 PlayerPrefs.SetString(windowShowedKey, "FC_showed");
//             }
//         }
//         // [MenuItem("Tools/FS System/Parkour/Welcome Window")]
//         public static void InitEditorWindow()
//         {
//             if (HasOpenInstances<WelcomeWindow>())
//                 return;
//             welcomeWindow = (WelcomeWindow)EditorWindow.GetWindow<WelcomeWindow>();
//             GUIContent titleContent = new GUIContent("Welcome");
//             welcomeWindow.titleContent = titleContent;
//             welcomeWindow.minSize = new Vector2(450, 290);
//             welcomeWindow.maxSize = new Vector2(450, 290);

//         }
//         private void OnGUI()
//         {

//             if (welcomeWindow == null)
//                 welcomeWindow = (WelcomeWindow)EditorWindow.GetWindow<WelcomeWindow>();
//             GUILayout.Space(10);

//             EditorGUI.HelpBox(new Rect(5, 10, position.width - 10, 80), "Parkour and Climbing system allows the player to traverse complex environments in the game using different parkour and climbing actions. The parkour system has predictive jumping that will automatically detect points to which the player can jump and execute precise jumps to reach them. The climbing system uses a mix of authored and procedural animations to adapt to dynamic climbing environments while looking realistic. ", MessageType.None);


//             if (GUI.Button(new Rect(55, 100, 110, 35), "QuickStart"))
//                 Application.OpenURL("https://fantacode.gitbook.io/parkour-and-climbing-system/quickstart");
//             if (GUI.Button(new Rect(170, 100, 110, 35), "Documentation"))
//                 Application.OpenURL("https://fantacode.gitbook.io/parkour-and-climbing-system/");
//             if (GUI.Button(new Rect(285, 100, 110, 35), "Videos"))
//                 Application.OpenURL("https://www.youtube.com/playlist?list=PLnbdyws4rcAu8b3zKIyTMKhMK8iHsIaOn");

//             GUILayout.Space(130);
//             AddOnModules();

//             GUI.Box(new Rect(0, 230, position.width, 2), "");

//             if (GUI.Button(new Rect(155, 243, 150, 35), "Create Character"))
//                 ClimbPointAndCreateCharacterEditorWindow.InitPlayerSetupWindow();

//         }
//         private void AddOnModules()
//         {
//             var _inputsystem = false;

//             var sybmols = PlayerSettings.GetScriptingDefineSymbolsForGroup(EditorUserBuildSettings.selectedBuildTargetGroup).Split(';');
//             for (int i = 0; i < sybmols.Length; i++)
//             {
//                 if (string.Equals(inputsystem, sybmols[i].Trim()))
//                     _inputsystem = true;
//             }

//             EditorGUI.BeginChangeCheck();
//             EditorGUILayout.LabelField(new GUIContent("Addon Module :"), EditorStyles.boldLabel);
//             GUILayout.Space(4);

//             GUILayout.BeginHorizontal();
//             var _input = EditorGUILayout.Toggle("", _inputsystem, GUILayout.Width(17), GUILayout.Height(17));
//             EditorGUILayout.LabelField(new GUIContent("New Input System", "Enabling this feature allows support for the New Input System. Ensure that you have installed the New InputSystem package before enabling this feature"), GUILayout.Width(110));
//             GUILayout.EndHorizontal();

//             var sybmolValueChanged = EditorGUI.EndChangeCheck();

//             if (_input != _inputsystem)
//             {
//                 if (_input)
//                 {
//                     if (EditorUtility.DisplayDialog("New Input System", "Enabling this feature allows support for the New Input System. Ensure that you have installed the New InputSystem package before enabling this feature", "OK", "Cancel"))
//                     {
//                         ScriptingDefineSymbolController.ToggleScriptingDefineSymbol(inputsystem, _input);
//                     }
//                     else
//                         sybmolValueChanged = false;
//                 }
//                 else
//                     ScriptingDefineSymbolController.ToggleScriptingDefineSymbol(inputsystem, _input);
//             }

//             if (sybmolValueChanged)
//                 ScriptingDefineSymbolController.ReimportScripts();

//         }
//         [MenuItem("Tools/FS System/Parkour/Import tags", false, 600, priority = 4)]
//         public static void AddTagsAndlayers()
//         {
//             EditorUtility.DisplayDialog("tags", "Tags imported successfully", "ok");
//             AddNewTags();
//         }
//         public static void AddNewTags()
//         {
//             if (!InternalEditorUtility.tags.ToList().Contains("NarrowBeam"))
//                 InternalEditorUtility.AddTag("NarrowBeam");
//             if (!InternalEditorUtility.tags.ToList().Contains("SwingableLedge"))
//                 InternalEditorUtility.AddTag("SwingableLedge");
//         }
//     }
// }
