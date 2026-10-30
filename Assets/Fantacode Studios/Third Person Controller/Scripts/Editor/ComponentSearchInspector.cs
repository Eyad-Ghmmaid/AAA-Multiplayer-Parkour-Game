using UnityEditor;
using UnityEngine;
using System;

namespace FS_Util
{

    [InitializeOnLoad]
    public class ComponentSearchInspector
    {
        static string search = "";
        static GameObject lastSelection;
        static bool isInitialized = false;

        static ComponentSearchInspector()
        {
            EditorApplication.delayCall += Initialize;
        }

        static void Initialize()
        {
            if (!isInitialized)
            {
                Editor.finishedDefaultHeaderGUI -= DrawSearchBar;
                Editor.finishedDefaultHeaderGUI += DrawSearchBar;
                Selection.selectionChanged += OnSelectionChanged;
                isInitialized = true;
            }
        }

        static void OnSelectionChanged()
        {
            // Reset search when selection changes
            if (Selection.activeGameObject != lastSelection)
            {
                if (lastSelection != null)
                {
                    ResetAllComponentsVisible(lastSelection);
                }
                lastSelection = Selection.activeGameObject;
                search = "";
            }
        }

        static void DrawSearchBar(Editor editor)
        {
            // Safety checks
            if (editor == null || editor.target == null) return;
            if (!(editor.target is GameObject)) return;
            if (Selection.activeGameObject == null) return;

            GameObject selectedGO = Selection.activeGameObject;

            // Update lastSelection if needed
            if (selectedGO != lastSelection)
            {
                if (lastSelection != null)
                {
                    ResetAllComponentsVisible(lastSelection);
                }
                lastSelection = selectedGO;
                search = "";
            }

            EditorGUILayout.Space(2);

            // Draw search field with proper layout
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Filter Components:", GUILayout.Width(110));

            string newSearch = EditorGUILayout.TextField(search, EditorStyles.toolbarSearchField);

            EditorGUILayout.EndHorizontal();

            // If search changed, update visibility
            if (newSearch != search)
            {
                search = newSearch;
                ApplySearchFilter(selectedGO, search);
            }
        }

        static void ResetAllComponentsVisible(GameObject go)
        {
            if (go == null) return;

            try
            {
                Component[] components = go.GetComponents<Component>();
                if (components == null) return;

                foreach (var comp in components)
                {
                    if (comp != null && comp.hideFlags.HasFlag(HideFlags.HideInInspector))
                    {
                        comp.hideFlags &= ~HideFlags.HideInInspector;
                    }
                }

                // Force inspector refresh
                //ActiveEditorTracker.sharedTracker.ForceRebuild();
                EditorUtility.SetDirty(go);
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"Error resetting component visibility: {e.Message}");
            }
        }

        static void ApplySearchFilter(GameObject go, string query)
        {
            if (go == null) return;

            try
            {
                // If query is empty or whitespace, show everything
                if (string.IsNullOrWhiteSpace(query))
                {
                    ResetAllComponentsVisible(go);
                    return;
                }

                string lowerQuery = query.Trim().ToLowerInvariant();
                Component[] components = go.GetComponents<Component>();

                if (components == null) return;

                foreach (var comp in components)
                {
                    if (comp == null) continue;

                    // Skip Transform component (never hide it)
                    if (comp is Transform) continue;

                    string typeName = comp.GetType().Name.ToLowerInvariant();
                    bool matches = typeName.Contains(lowerQuery);

                    if (matches)
                    {
                        // Remove hide flag if it exists
                        if (comp.hideFlags.HasFlag(HideFlags.HideInInspector))
                        {
                            comp.hideFlags &= ~HideFlags.HideInInspector;
                        }
                    }
                    else
                    {
                        // Add hide flag
                        comp.hideFlags |= HideFlags.HideInInspector;
                    }
                }

                // Force inspector refresh
                ActiveEditorTracker.sharedTracker.ForceRebuild();
                EditorUtility.SetDirty(go);
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"Error applying search filter: {e.Message}");
            }
        }

        // Cleanup when domain reloads
        [InitializeOnLoadMethod]
        static void Cleanup()
        {
            EditorApplication.delayCall += () =>
            {
                if (lastSelection != null)
                {
                    ResetAllComponentsVisible(lastSelection);
                }
            };
        }
    }
}