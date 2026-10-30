#if UNITY_EDITOR

using FS_Core;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace FS_Util
{

    public class FSObjectListView<T> where T : ScriptableObject
    {
        public List<T> allObjects = new List<T>();
        List<T> filteredObjects = new List<T>();
        string objectName = "";

        int selectedIndex = -1;
        T selectedObject = null;

        public T SelectedObject => selectedObject;

        string searchTerm = "";
        bool isAdding = false;
        string newObjectName = "";

        private Vector2 objectsScroll;

        public GUIStyle containerStyle, headerStyle;

        public FSObjectListView()
        {
            objectName = typeof(T).Name;
            newObjectName = "New " + objectName;
        }

        public virtual void ListObjects(Rect position)
        {
            float headerHeight = 20f;
            float toolbarHeight = 24f;
            float addUIHeight = 100f;
            float listHeaderHeight = 20f;
            float itemHeight = 24f;
            float spacing = 5f;
            float padding = 10f;

            // Calculate available height for the entire view
            float availableHeight = position.height - (padding * 2);

            using (new EditorGUILayout.VerticalScope(containerStyle, GUILayout.Width(position.width / 3), GUILayout.MaxHeight(availableHeight)))
            {
                // Main container box
                using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                {
                    // Header with item count
                    using (new EditorGUILayout.HorizontalScope(EditorStyles.helpBox))
                    {
                        EditorGUILayout.LabelField($"{objectName} List ({filteredObjects.Count})", headerStyle);
                        
                        GUILayout.FlexibleSpace();
                    }
                    GUILayout.Space(5);
                    if (!isAdding)
                    {
                        // Toolbar buttons
                        using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
                        {
                            //GUILayout.Space(5);
                            if (GUILayout.Button(EditorGUIUtility.IconContent("d_Toolbar Plus", "Add New"), EditorStyles.toolbarButton, GUILayout.Width(28)))
                            {
                                newObjectName = searchTerm;
                                isAdding = true;
                            }

                            GUI.enabled = selectedIndex >= 0;
                            if (GUILayout.Button(EditorGUIUtility.IconContent("d_Toolbar Minus", "Remove Selected"), EditorStyles.toolbarButton, GUILayout.Width(28)))
                            {
                                if (selectedIndex >= 0 && selectedIndex < filteredObjects.Count)
                                {
                                    if (EditorUtility.DisplayDialog("Delete Confirmation",
                                        $"Are you sure you want to delete '{selectedObject.name}'?", "Delete", "Cancel"))
                                    {
                                        RemoveObject();
                                    }
                                }
                            }
                            GUI.enabled = true;

                            GUILayout.FlexibleSpace();

                            // Search bar with immediate update
                            GUILayout.Space(5);
                            EditorGUI.BeginChangeCheck();
                            searchTerm = EditorGUILayout.TextField(searchTerm, EditorStyles.toolbarSearchField,GUILayout.ExpandWidth(true));
                            if (EditorGUI.EndChangeCheck())
                            {
                                FilterObjects();
                            }

                            // Clear search button
                            if (!string.IsNullOrEmpty(searchTerm))
                            {
                                if (GUILayout.Button(EditorGUIUtility.IconContent("d_winbtn_win_close", "Clear"), EditorStyles.toolbarButton, GUILayout.Width(22)))
                                {
                                    searchTerm = "";
                                    FilterObjects();
                                    GUI.FocusControl(null);
                                }
                            }
                        }

                        EditorGUILayout.Space(2);
                    }
                    else
                    {
                        EditorGUILayout.Space(2);

                        // Add new item UI
                        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                        {
                            EditorGUILayout.LabelField($"Create New {objectName}", EditorStyles.boldLabel);
                            EditorGUILayout.Space(2);

                            GUI.SetNextControlName("NewItemName");
                            newObjectName = EditorGUILayout.TextField("Name", newObjectName);
                            EditorGUILayout.Space(5);

                            if (Event.current.type == EventType.Repaint)
                            {
                                GUI.FocusControl("NewItemName");
                            }

                            using (new EditorGUILayout.HorizontalScope())
                            {
                                GUILayout.FlexibleSpace();
                                GUI.enabled = !string.IsNullOrWhiteSpace(newObjectName);

                                if (Event.current.type == EventType.KeyDown &&
                                    Event.current.keyCode == KeyCode.Return &&
                                    GUI.enabled)
                                {
                                    HandleCreateNewItem();
                                    Event.current.Use();
                                }

                                if (GUILayout.Button("Create", GUILayout.Width(60), GUILayout.Height(20)))
                                {
                                    HandleCreateNewItem();
                                }
                                GUI.enabled = true;

                                if (GUILayout.Button("Cancel", GUILayout.Width(60), GUILayout.Height(20)))
                                {
                                    isAdding = false;
                                    GUI.FocusControl(null);
                                }
                            }
                        }

                        EditorGUILayout.Space(2);
                    }

                    // List header
                    //using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
                    //{
                    //    GUILayout.Space(5);
                    //    EditorGUILayout.LabelField("Name", EditorStyles.miniLabel, GUILayout.ExpandWidth(true));
                    //}

                    // Calculate content height and available space
                    float contentHeight = filteredObjects.Count * itemHeight;
                    float usedHeight = headerHeight + toolbarHeight + listHeaderHeight + (isAdding ? addUIHeight : 0) + (spacing * 3);
                    float availableContentHeight = availableHeight - usedHeight - (padding * 2);

                    // Only use scroll view if content exceeds available space
                    bool needsScroll = contentHeight > availableContentHeight;

                    if (needsScroll)
                    {
                        objectsScroll = EditorGUILayout.BeginScrollView(objectsScroll, false, true,
                            GUILayout.Height(availableContentHeight));
                    }

                    // List content
                    if (filteredObjects.Count > 0)
                    {
                        for (int i = 0; i < filteredObjects.Count; i++)
                        {
                            bool isSelected = selectedIndex == i;

                            Rect itemRect = GUILayoutUtility.GetRect(GUIContent.none, EditorStyles.helpBox,
                                GUILayout.ExpandWidth(true), GUILayout.Height(itemHeight));

                            Event evt = Event.current;
                            bool isHovered = itemRect.Contains(evt.mousePosition);

                            // Background
                            if (isSelected)
                            {
                                EditorGUI.DrawRect(itemRect, new Color(0.23f, 0.49f, 0.9f, 0.5f));
                            }
                            else if (isHovered && evt.type == EventType.Repaint)
                            {
                                EditorGUI.DrawRect(itemRect, new Color(0.23f, 0.49f, 0.9f, 0.2f));
                            }

                            // Selection handling
                            if (evt.type == EventType.MouseDown && isHovered && evt.button == 0)
                            {
                                selectedIndex = i;
                                selectedObject = filteredObjects[i];
                                evt.Use();
                                GUI.changed = true;
                            }

                            // Item content
                            using (new EditorGUILayout.HorizontalScope())
                            {
                                GUILayout.Space(8);
                                GUI.Label(new Rect(itemRect.x + 8, itemRect.y + 4, itemRect.width - 16, itemRect.height - 8),
                                    filteredObjects[i].name, EditorStyles.label);
                            }
                        }
                    }
                    else
                    {
                        EditorGUILayout.HelpBox(
                            string.IsNullOrEmpty(searchTerm)
                                ? $"No {objectName} items found."
                                : "No matches found for your search.",
                            MessageType.Info);
                    }

                    if (needsScroll)
                    {
                        EditorGUILayout.EndScrollView();
                    }
                }
            }
        }

        private void FilterObjects()
        {
            if (string.IsNullOrEmpty(searchTerm))
            {
                filteredObjects = new List<T>(allObjects);
            }
            else
            {
                filteredObjects = allObjects
                    .Where(obj => obj.name.IndexOf(searchTerm, StringComparison.OrdinalIgnoreCase) >= 0)
                    .ToList();
            }

            // Update selection index in filtered list
            if (selectedObject != null)
            {
                selectedIndex = filteredObjects.IndexOf(selectedObject);
            }
            else
            {
                selectedIndex = -1;
            }
        }

        private void HandleCreateNewItem()
        {
            newObjectName = newObjectName.Trim();
            if (!allObjects.Any(c => c.name.Equals(newObjectName, StringComparison.OrdinalIgnoreCase)))
            {
                AddObject();
                isAdding = false;
                GUI.FocusControl(null);
            }
            else
            {
                EditorUtility.DisplayDialog("Duplicate Name",
                    $"A {objectName} with this name already exists.", "OK");
            }
        }

        private void AddObject()
        {
            string path = EditorUtility.SaveFilePanelInProject($"Create {objectName}", newObjectName, "asset",
                $"Specify a location to save the {objectName}");

            if (!string.IsNullOrEmpty(path))
            {
                var newObject = ScriptableObject.CreateInstance<T>();
                newObject.name = newObjectName;

                if(typeof(T) == typeof(ItemCategory))
                {
                    ItemCategory category = (ItemCategory)(object)newObject;
                    category.SetCategoryName(newObjectName);
                }
                else if (typeof(T) == typeof(Item))
                {
                    Item item = (Item)(object)newObject;
                    item.Init(newObjectName);
                }

                AssetDatabase.CreateAsset(newObject, path);
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();

                LoadObjects();

                selectedObject = newObject;
                selectedIndex = filteredObjects.IndexOf(selectedObject);
            }
        }

        private void RemoveObject()
        {
            if (selectedObject == null) return;

            AssetDatabase.DeleteAsset(AssetDatabase.GetAssetPath(selectedObject));
            AssetDatabase.Refresh();
            LoadObjects();

            selectedObject = null;
            selectedIndex = -1;
        }

        public void LoadObjects()
        {
            allObjects.Clear();

            string[] guids = AssetDatabase.FindAssets("t:" + objectName);
            foreach (var guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var obj = AssetDatabase.LoadAssetAtPath<T>(path);
                if (obj != null)
                {
                    allObjects.Add(obj);
                }
            }

            FilterObjects();
        }
    }
}

#endif
