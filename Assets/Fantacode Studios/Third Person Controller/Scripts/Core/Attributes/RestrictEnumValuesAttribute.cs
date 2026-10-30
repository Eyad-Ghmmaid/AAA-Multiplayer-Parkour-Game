#if UNITY_EDITOR
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace FS_Util
{
    public class RestrictEnumValuesAttribute : PropertyAttribute
    {
        public int[] AllowedValues { get; private set; }

        public RestrictEnumValuesAttribute(params int[] allowedValues)
        {
            AllowedValues = allowedValues;
        }
    }

    [CustomPropertyDrawer(typeof(RestrictEnumValuesAttribute))]
    public class RestrictEnumValuesDrawer : PropertyDrawer
    {
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            RestrictEnumValuesAttribute restrictAttribute = (RestrictEnumValuesAttribute)attribute;

            if (property.propertyType == SerializedPropertyType.Enum)
            {
                string[] enumNames = property.enumNames;
                int[] allowedIndexes = restrictAttribute.AllowedValues;

                // Filter only the allowed names & values
                string[] filteredNames = allowedIndexes.Select(i => enumNames[i]).ToArray();
                int[] filteredIndexes = allowedIndexes;

                // Get current selected index
                int currentIndex = System.Array.IndexOf(filteredIndexes, property.enumValueIndex);
                if (currentIndex == -1) currentIndex = 0; // Default to first allowed

                // Show dropdown with filtered options
                int selectedIndex = EditorGUI.Popup(position, label.text, currentIndex, filteredNames);
                property.enumValueIndex = filteredIndexes[selectedIndex];
            }
            else
            {
                EditorGUI.LabelField(position, label.text, "Use with Enum only!");
            }
        }
    }
}
#endif