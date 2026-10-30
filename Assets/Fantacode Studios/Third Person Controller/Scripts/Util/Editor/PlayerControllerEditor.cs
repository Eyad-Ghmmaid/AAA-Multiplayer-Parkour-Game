using UnityEngine;
using UnityEditor;
using System;
using System.Linq;
using System.Collections.Generic;

namespace FS_Core
{

    //[CustomEditor(typeof(PlayerController))]
    [CanEditMultipleObjects]
    public class PlayerControllerEditor : Editor
    {
        private SerializedProperty _searchProp;
        private bool _showSearchResults = true;
        private readonly List<Editor> _componentEditors = new List<Editor>();
        private Component[] _lastComponentsSnapshot;
        private GUIStyle _componentHeadingStyle;

        private void OnEnable()
        {
            _searchProp = serializedObject.FindProperty("_editorSearchString");

            _componentHeadingStyle = new GUIStyle(EditorStyles.boldLabel)
            {
                normal = { textColor = new Color(0.2f, 0.6f, 1f) }
            };

            CacheComponentEditors();
        }

        private void OnDisable()
        {
            foreach (var ed in _componentEditors)
                DestroyImmediate(ed);
            _componentEditors.Clear();
        }

        public override void OnInspectorGUI()
        {
            // Only rebuild editors if component list has actually changed
            var pc = (PlayerController)target;
            var current = pc.GetComponents<Component>();
            if (_lastComponentsSnapshot == null ||
                _lastComponentsSnapshot.Length != current.Length ||
                !_lastComponentsSnapshot.SequenceEqual(current))
            {
                CacheComponentEditors();
            }

            // Draw default inspector (includes your hidden search field)
            serializedObject.Update();
            DrawDefaultInspector();
            serializedObject.ApplyModifiedProperties();

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Component Search", EditorStyles.boldLabel);

            // Search field
            serializedObject.Update();
            EditorGUILayout.BeginHorizontal();
            GUI.SetNextControlName("ComponentSearchField");
            EditorGUILayout.PropertyField(_searchProp, GUIContent.none, GUILayout.MinWidth(100));

            var e = Event.current;
            if (e.type == EventType.KeyDown &&
                e.keyCode == KeyCode.Return &&
                GUI.GetNameOfFocusedControl() == "ComponentSearchField")
            {
                _showSearchResults = true;
                e.Use();
            }

            if (GUILayout.Button("Search", GUILayout.MaxWidth(75)))
            {
                _showSearchResults = true;
                GUI.FocusControl(null);
            }
            if (GUILayout.Button("Clear", GUILayout.MaxWidth(75)))
            {
                _searchProp.stringValue = string.Empty;
                _showSearchResults = false;
                GUI.FocusControl(null);
            }
            EditorGUILayout.EndHorizontal();
            serializedObject.ApplyModifiedProperties();

            // Show results
            if (_showSearchResults && !String.IsNullOrEmpty(_searchProp.stringValue))
            {
                EditorGUILayout.Space();
                EditorGUILayout.LabelField("Search Results", EditorStyles.boldLabel);
                EditorGUILayout.BeginVertical("box");

                bool foundAny = false;
                var search = _searchProp.stringValue;
                foreach (var editor in _componentEditors)
                {
                    if (!(editor.target is Component comp)) continue;
                    var name = comp.GetType().Name;
                    if (name.IndexOf(search, StringComparison.OrdinalIgnoreCase) < 0) continue;

                    foundAny = true;
                    EditorGUILayout.BeginVertical("box");
                    EditorGUILayout.LabelField(name, _componentHeadingStyle);
                    editor.OnInspectorGUI();
                    EditorGUILayout.EndVertical();
                    EditorGUILayout.Space(4);
                }

                if (!foundAny)
                    EditorGUILayout.HelpBox("No components matched your search.", MessageType.Info);

                EditorGUILayout.EndVertical();
            }
        }

        private void CacheComponentEditors()
        {
            var pc = (PlayerController)target;
            var comps = pc.GetComponents<Component>();

            // Remove editors for removed components
            _componentEditors.RemoveAll(ed =>
            {
                if (!(ed.target is Component c) || !comps.Contains(c))
                {
                    DestroyImmediate(ed);
                    return true;
                }
                return false;
            });

            // Create editors for new components
            foreach (var c in comps)
            {
                if (c is PlayerController) continue;
                if (_componentEditors.Any(ed => ed.target == c)) continue;
                _componentEditors.Add(Editor.CreateEditor(c));
            }

            _lastComponentsSnapshot = comps;
        }
    }
}