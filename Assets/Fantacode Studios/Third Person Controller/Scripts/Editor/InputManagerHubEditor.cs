using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace FS_Core
{
    [CustomEditor(typeof(InputManagerHub))]
    public class InputManagerHubEditor : Editor
    {
        InputManagerHub hub;
        List<FSInputManagerBase> inputManagers;
        Dictionary<FSInputManagerBase, bool> foldouts = new();
        Dictionary<FSInputManagerBase, SerializedObject> serializedObjects = new();

        List<Type> allInputManagerTypes;

        static GUIStyle headerStyle;
        static GUIStyle sectionBoxStyle;
        static GUIStyle keyLabelStyle;
        static GUIStyle statusEnabledStyle;
        static GUIStyle statusDisabledStyle;
        static GUIStyle addButtonStyle;
        static bool stylesInitialized;

        static void InitStyles()
        {
            if (stylesInitialized) return;
            stylesInitialized = true;

            headerStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                fontSize = 15,
                padding = new RectOffset(4, 4, 0, 0)
            };

            sectionBoxStyle = new GUIStyle("helpBox")
            {
                padding = new RectOffset(8, 8, 6, 6),
                margin = new RectOffset(0, 0, 2, 4)
            };

            keyLabelStyle = new GUIStyle(EditorStyles.label)
            {
                alignment = TextAnchor.MiddleLeft,
                padding = new RectOffset(4, 4, 2, 2)
            };

            statusEnabledStyle = new GUIStyle(EditorStyles.miniLabel)
            {
                normal = { textColor = new Color(0.3f, 0.8f, 0.3f) },
                alignment = TextAnchor.MiddleRight,
                fontStyle = FontStyle.Bold
            };

            statusDisabledStyle = new GUIStyle(EditorStyles.miniLabel)
            {
                normal = { textColor = new Color(0.8f, 0.4f, 0.3f) },
                alignment = TextAnchor.MiddleRight,
                fontStyle = FontStyle.Bold
            };

            addButtonStyle = new GUIStyle(GUI.skin.button)
            {
                fontSize = 12,
                fixedHeight = 28,
                fontStyle = FontStyle.Bold
            };
        }

        void OnEnable()
        {
            hub = (InputManagerHub)target;
            CacheInputManagerTypes();
            RefreshInputManagers();
            hub.HideInputManagers();
        }
        void OnDisable()
        {
            serializedObjects.Clear();
        }

        void CacheInputManagerTypes()
        {
            allInputManagerTypes = AppDomain.CurrentDomain.GetAssemblies()
                .SelectMany(assembly =>
                {
                    try { return assembly.GetTypes(); }
                    catch { return Array.Empty<Type>(); }
                })
                .Where(type =>
                    !type.IsAbstract &&
                    typeof(FSInputManagerBase).IsAssignableFrom(type) &&
                    type != typeof(InputManagerHub) &&
                    IsValidFSInputManagerType(type))
                .OrderBy(type => type.Name)
                .ToList();
        }

        static bool IsValidFSInputManagerType(Type type)
        {
            return typeof(FSInputManagerBase).IsAssignableFrom(type);
        }

        void RefreshInputManagers()
        {
            inputManagers = hub.GetAllInputManagers();

            var toRemove = new List<FSInputManagerBase>();
            foreach (var kvp in serializedObjects)
            {
                if (kvp.Key == null || !inputManagers.Contains(kvp.Key))
                    toRemove.Add(kvp.Key);
            }

            foreach (var key in toRemove)
            {
                serializedObjects.Remove(key);
                foldouts.Remove(key);
            }

            hub.HideInputManagers();
        }

        SerializedObject GetSerializedObject(FSInputManagerBase mgr)
        {
            if (!serializedObjects.ContainsKey(mgr) || serializedObjects[mgr] == null)
                serializedObjects[mgr] = new SerializedObject(mgr);
            return serializedObjects[mgr];
        }

        public override void OnInspectorGUI()
        {
            InitStyles();
            serializedObject.Update();

            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField("Input Manager Hub", headerStyle);
            EditorGUILayout.Space(4);
            DrawSeparator();

            EditorGUILayout.Space(4);

            if (inputManagers == null || inputManagers.Count == 0)
            {
                EditorGUILayout.HelpBox(
                    "No input managers on this character.\nUse the button below to add one.",
                    MessageType.Info);
            }
            else
            {
                for (int i = 0; i < inputManagers.Count; i++)
                {
                    var mgr = inputManagers[i];
                    if (mgr == null) continue;
                    DrawInputManagerSection(mgr);
                }
            }

            EditorGUILayout.Space(6);
            DrawAddDropdown();

            serializedObject.ApplyModifiedProperties();
        }

        void DrawInputManagerSection(FSInputManagerBase mgr)
        {
            if (!foldouts.ContainsKey(mgr))
                foldouts[mgr] = false;

            var so = GetSerializedObject(mgr);
            so.Update();

            EditorGUILayout.BeginVertical(sectionBoxStyle);

            string displayName = FormatDisplayName(mgr.GetType().Name);
            var headerRect = EditorGUILayout.GetControlRect(false, 20f);
            const float removeWidth = 20f;
            const float toggleWidth = 18f;
            const float statusWidth = 64f;
            const float spacing = 4f;

            var removeRect = new Rect(headerRect.xMax - removeWidth, headerRect.y + 1f, removeWidth, 18f);
            var toggleRect = new Rect(removeRect.x - spacing - toggleWidth, headerRect.y + 1f, toggleWidth, 18f);
            var statusRect = new Rect(toggleRect.x - spacing - statusWidth, headerRect.y + 1f, statusWidth, 18f);
            var foldoutRect = new Rect(headerRect.x, headerRect.y, statusRect.x - headerRect.x - spacing, headerRect.height);
            EditorGUI.indentLevel++;
            foldouts[mgr] = EditorGUI.Foldout(foldoutRect, foldouts[mgr], displayName, true, EditorStyles.foldoutHeader);
            EditorGUI.indentLevel--;
            GUI.Label(
                statusRect,
                mgr.enabled ? "ACTIVE" : "DISABLED",
                mgr.enabled ? statusEnabledStyle : statusDisabledStyle);

            EditorGUI.BeginChangeCheck();
            bool enabled = GUI.Toggle(toggleRect, mgr.enabled, GUIContent.none);
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(mgr, "Toggle Input Manager");
                mgr.enabled = enabled;
                EditorUtility.SetDirty(mgr);
            }

            var removeContent = new GUIContent("-", "Remove this input manager");
            if (GUI.Button(removeRect, removeContent, EditorStyles.miniButton))
            {
                if (EditorUtility.DisplayDialog(
                    "Remove Input Manager",
                    $"Remove {displayName} from this character?",
                    "Remove", "Cancel"))
                {
                    Undo.DestroyObjectImmediate(mgr);
                    RefreshInputManagers();
                    EditorGUILayout.EndVertical();
                    GUIUtility.ExitGUI();
                    return;
                }
            }

            if (foldouts[mgr])
            {
                EditorGUILayout.Space(4);
                DrawKeybindingTable(mgr, so);
                DrawExtraFields(mgr, so);
            }

            EditorGUILayout.EndVertical();
            so.ApplyModifiedProperties();
        }

        void DrawKeybindingTable(FSInputManagerBase mgr, SerializedObject so)
        {
            var bindings = mgr.GetInputBindings();
            if (bindings == null || bindings.Count == 0) return;

            var headerRect = EditorGUILayout.GetControlRect(false, EditorGUIUtility.singleLineHeight);
            GetKeybindingColumnRects(headerRect, out var actionHeaderRect, out var keyHeaderRect, out var buttonHeaderRect, out var uiHeaderRect);

            EditorGUI.LabelField(actionHeaderRect, "Action", EditorStyles.miniBoldLabel);
            EditorGUI.LabelField(keyHeaderRect, "Key", EditorStyles.miniBoldLabel);
            EditorGUI.LabelField(buttonHeaderRect, "Button", EditorStyles.miniBoldLabel);
            EditorGUI.LabelField(uiHeaderRect, "UI Button", EditorStyles.miniBoldLabel);

            DrawThinSeparator();

            int previousIndent = EditorGUI.indentLevel;
            EditorGUI.indentLevel = 0;

            foreach (var binding in bindings)
            {
                var bindingProp = so.FindProperty(binding.Id);
                if (bindingProp == null) continue;

                var keyProp = bindingProp.FindPropertyRelative("key");
                var buttonProp = bindingProp.FindPropertyRelative("button");
                var uiProp = bindingProp.FindPropertyRelative("uiButton");

                var rowRect = EditorGUILayout.GetControlRect(false, EditorGUIUtility.singleLineHeight);
                GetKeybindingColumnRects(rowRect, out var actionRect, out var keyRect, out var buttonRect, out var uiRect);

                EditorGUI.LabelField(actionRect, binding.DisplayName, keyLabelStyle);

                EditorGUI.BeginDisabledGroup(!mgr.enabled);
                EditorGUI.PropertyField(keyRect, keyProp, GUIContent.none);
                EditorGUI.PropertyField(buttonRect, buttonProp, GUIContent.none);
                EditorGUI.PropertyField(uiRect, uiProp, GUIContent.none);
                EditorGUI.EndDisabledGroup();
            }

            EditorGUI.indentLevel = previousIndent;
        }

        static void GetKeybindingColumnRects(Rect rowRect, out Rect actionRect, out Rect keyRect, out Rect buttonRect, out Rect uiRect)
        {
            const float spacing = 6f;
            const float minActionWidth = 50f;
            const float keyWidth = 95f;
            const float buttonWidth = 105f;
            const float uiWidth = 110f;

            float availableWidth = Mathf.Max(0f, rowRect.width - (spacing * 3f));
            float actionWidth = Mathf.Max(minActionWidth, availableWidth - keyWidth - buttonWidth - uiWidth);

            if (actionWidth < minActionWidth)
            {
                actionWidth = minActionWidth;
            }

            actionRect = new Rect(rowRect.x, rowRect.y, actionWidth, rowRect.height);
            keyRect = new Rect(actionRect.xMax + spacing, rowRect.y, keyWidth, rowRect.height);
            buttonRect = new Rect(keyRect.xMax + spacing, rowRect.y, buttonWidth, rowRect.height);
            uiRect = new Rect(buttonRect.xMax + spacing, rowRect.y, uiWidth, rowRect.height);
        }

        void DrawExtraFields(FSInputManagerBase mgr, SerializedObject so)
        {
            var type = mgr.GetType();
            var fields = type.GetFields(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public)
                .Where(f => f.GetCustomAttribute<SerializeField>() != null || f.IsPublic)
                .ToList();

            var inputFieldNames = new HashSet<string>(
                fields.Where(f => f.FieldType == typeof(FSInput)).Select(f => f.Name));

            var extraFields = new List<SerializedProperty>();
            var iter = so.GetIterator();
            iter.NextVisible(true);
            while (iter.NextVisible(false))
            {
                string name = iter.name;
                if (name == "m_Script" || name == "m_Enabled" || name == "m_EditorHideFlags")
                    continue;
                if (inputFieldNames.Contains(name))
                    continue;
                extraFields.Add(iter.Copy());
            }

            if (extraFields.Count == 0) return;

            EditorGUILayout.Space(4);
            DrawThinSeparator();
            EditorGUILayout.LabelField("Settings", EditorStyles.miniBoldLabel);

            EditorGUI.BeginDisabledGroup(!mgr.enabled);
            foreach (var prop in extraFields)
            {
                EditorGUILayout.PropertyField(prop, true);
            }
            EditorGUI.EndDisabledGroup();
        }

        void DrawAddDropdown()
        {
            if (allInputManagerTypes == null || allInputManagerTypes.Count == 0)
                return;

            var existingTypes = new HashSet<Type>(inputManagers
                .Where(m => m != null)
                .Select(m => m.GetType()));

            var available = allInputManagerTypes
                .Where(type => !existingTypes.Contains(type))
                .ToList();

            if (available.Count == 0)
            {
                EditorGUI.BeginDisabledGroup(true);
                GUILayout.Button("All Input Managers Added", addButtonStyle);
                EditorGUI.EndDisabledGroup();
                return;
            }

            if (GUILayout.Button("+ Add Input Manager", addButtonStyle))
            {
                var menu = new GenericMenu();
                foreach (var type in available)
                {
                    var capturedType = type;
                    menu.AddItem(
                        new GUIContent(FormatDisplayName(capturedType.Name)),
                        false,
                        () =>
                        {
                            Undo.AddComponent(hub.gameObject, capturedType);
                            RefreshInputManagers();
                        });
                }
                menu.ShowAsContext();
            }
        }

        static string FormatDisplayName(string typeName)
        {
            string name = typeName;
            foreach (var suffix in new[] { "InputManager", "InputManger", "InputHandler" })
            {
                if (name.EndsWith(suffix))
                {
                    name = name.Substring(0, name.Length - suffix.Length);
                    break;
                }
            }

            if (string.IsNullOrEmpty(name))
                name = typeName;

            return System.Text.RegularExpressions.Regex.Replace(name, "(?<!^)([A-Z])", " $1").Trim();
        }

        static string FormatFieldName(string fieldName)
        {
            string name = fieldName;
            foreach (var suffix in new[] { "ToggleKey", "Key", "key" })
            {
                if (name.EndsWith(suffix, StringComparison.Ordinal))
                {
                    name = name.Substring(0, name.Length - suffix.Length);
                    break;
                }
            }

            if (string.IsNullOrEmpty(name))
                name = fieldName;

            name = char.ToUpper(name[0]) + name.Substring(1);
            return System.Text.RegularExpressions.Regex.Replace(name, "(?<!^)([A-Z])", " $1").Trim();
        }

        static void DrawSeparator()
        {
            EditorGUILayout.Space(2);
            var rect = EditorGUILayout.GetControlRect(false, 1);
            EditorGUI.DrawRect(rect, new Color(0.5f, 0.5f, 0.5f, 0.3f));
            EditorGUILayout.Space(2);
        }

        static void DrawThinSeparator()
        {
            var rect = EditorGUILayout.GetControlRect(false, 1);
            EditorGUI.DrawRect(rect, new Color(0.5f, 0.5f, 0.5f, 0.15f));
        }
    }
}
