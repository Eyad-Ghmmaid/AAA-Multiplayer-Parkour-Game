using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace FS_Util { 

[AttributeUsage(AttributeTargets.Field, AllowMultiple = false)]
public class StringDropdownAttribute : PropertyAttribute
{
    public string SourceName { get; }
    public string ListFieldName { get; }

    public StringDropdownAttribute(string sourceName, string listFieldName)
    {
        SourceName = sourceName;
        ListFieldName = listFieldName;
    }
}



#if UNITY_EDITOR

[CustomPropertyDrawer(typeof(StringDropdownAttribute))]
public class StringDropdownDrawer : PropertyDrawer
{
    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        if (property.propertyType == SerializedPropertyType.String)
        {
            StringDropdownAttribute dropdown = (StringDropdownAttribute)attribute;
            List<string> options = GetOptions(property.serializedObject.targetObject, dropdown.SourceName, dropdown.ListFieldName);

            if (options != null && options.Count > 0)
            {
                int index = options.IndexOf(property.stringValue);
                index = EditorGUI.Popup(position, label.text, index, options.ToArray());
                
                if (index >= 0)
                {
                    property.stringValue = options[index];
                }
            }
            else
            {
                EditorGUI.LabelField(position, label.text, "Invalid source or empty list");
            }
        }
        else
        {
            EditorGUI.PropertyField(position, property, label);
            EditorGUI.LabelField(position, "Use with string fields only", EditorStyles.helpBox);
        }
    }

    private List<string> GetOptions(UnityEngine.Object target, string sourceName, string listFieldName)
    {
        if (target == null) return null;

        Type targetType = target.GetType();
        FieldInfo listField = targetType.GetField(listFieldName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

        if (listField != null && listField.FieldType == typeof(List<string>))
        {
            return (List<string>)listField.GetValue(targetType);
        }

        return null;
    }
}
#endif
}