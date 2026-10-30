using System;
#if UNITY_EDITOR
using UnityEditor;
#endif
using UnityEngine;

namespace FS_Util
{

    [AttributeUsage(AttributeTargets.Field, AllowMultiple = false)]
    public class SetFromResourcesAttribute : PropertyAttribute
    {
        public string ResourcePath { get; }

        public SetFromResourcesAttribute(string resourcePath)
        {
            ResourcePath = resourcePath;
        }
    }

#if UNITY_EDITOR
    [CustomPropertyDrawer(typeof(SetFromResourcesAttribute))]
    public class SetFromResourcesDrawer : PropertyDrawer
    {
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            SetFromResourcesAttribute setFromResources = (SetFromResourcesAttribute)attribute;
            if (property.propertyType == SerializedPropertyType.ObjectReference)
            {
                if (property.objectReferenceValue == null)
                {
                    property.objectReferenceValue = Resources.Load(setFromResources.ResourcePath, fieldInfo.FieldType);
                }
            }

            EditorGUI.PropertyField(position, property, label);
        }
    }
#endif
}