using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif
using System;
using System.Collections.Generic;

namespace FS_Core { 

[AttributeUsage(AttributeTargets.Class, AllowMultiple = true)]
public class FoldoutGroupAttribute : Attribute
{
    public string GroupName { get; private set; }
    public string[] FieldNames { get; private set; }
    public bool StartExpanded { get; private set; }

    public FoldoutGroupAttribute(string groupName, bool startExpanded, params string[] fieldNames)
    {
        this.GroupName = groupName;
        this.FieldNames = fieldNames;
        this.StartExpanded = startExpanded;
    }
}

#if UNITY_EDITOR
// Custom editor that handles drawing the fields in foldout groups
[CanEditMultipleObjects]  
[CustomEditor(typeof(MonoBehaviour), true)]
public class FieldFoldoutEditor : Editor
{
    // Dictionary to store foldout states
    private Dictionary<string, bool> foldoutStates = new Dictionary<string, bool>();

    // Dictionary to store which fields belong to which groups
    private Dictionary<string, HashSet<string>> fieldGroups = new Dictionary<string, HashSet<string>>();

    // Dictionary to track if a field has been drawn
    private HashSet<string> drawnFields = new HashSet<string>();

    private void OnEnable()
    {
        // Get all FieldFoldout attributes on the target
        var attributes = target.GetType().GetCustomAttributes(typeof(FoldoutGroupAttribute), true);

        // Process each attribute
        foreach (FoldoutGroupAttribute attr in attributes)
        {
            // Initialize the foldout state
            if (!foldoutStates.ContainsKey(attr.GroupName))
            {
                foldoutStates[attr.GroupName] = attr.StartExpanded;
            }

            // Create a set for this group if it doesn't exist
            if (!fieldGroups.ContainsKey(attr.GroupName))
            {
                fieldGroups[attr.GroupName] = new HashSet<string>();
            }

            // Add all fields to this group
            foreach (string fieldName in attr.FieldNames)
            {
                fieldGroups[attr.GroupName].Add(fieldName);
            }
        }
    }

    // Custom inspector GUI
    public override void OnInspectorGUI()
    {
        serializedObject.Update();
        drawnFields.Clear();

        // Draw the script field
        GUI.enabled = false;
        EditorGUILayout.PropertyField(serializedObject.FindProperty("m_Script"));
        GUI.enabled = true;

        // First draw all non-grouped properties
        SerializedProperty iterator = serializedObject.GetIterator();
        bool enterChildren = true;

        while (iterator.NextVisible(enterChildren))
        {
            enterChildren = false;

            // Skip script property and grouped properties
            if (iterator.name == "m_Script" || IsFieldInAnyGroup(iterator.name))
            {
                continue;
            }

            EditorGUILayout.PropertyField(iterator, true);
            drawnFields.Add(iterator.name);
        }

        // Then draw grouped properties
        foreach (var group in fieldGroups)
        {
            string groupName = group.Key;
            HashSet<string> fields = group.Value;

            // Draw the foldout header
            GUILayout.Space(10);
            foldoutStates[groupName] = EditorGUILayout.Foldout(foldoutStates[groupName], groupName, true, EditorStyles.foldoutHeader);

            if (foldoutStates[groupName])
            {
                EditorGUI.indentLevel++;

                foreach (string fieldName in fields)
                {
                    SerializedProperty property = serializedObject.FindProperty(fieldName);
                    if (property != null)
                    {
                        EditorGUILayout.PropertyField(property, true);
                        drawnFields.Add(fieldName);
                    }
                }

                EditorGUI.indentLevel--;
            }
            else
            {
                // Mark fields as drawn even when not shown
                foreach (string fieldName in fields)
                {
                    drawnFields.Add(fieldName);
                }
            }
        }

        serializedObject.ApplyModifiedProperties();
    }

    private bool IsFieldInAnyGroup(string fieldName)
    {
        foreach (var group in fieldGroups)
        {
            if (group.Value.Contains(fieldName))
                return true;
        }
        return false;
    }
}
#endif
}