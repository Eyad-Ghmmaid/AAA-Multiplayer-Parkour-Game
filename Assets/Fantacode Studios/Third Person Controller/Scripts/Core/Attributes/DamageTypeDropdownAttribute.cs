using System;
using System.Collections.Generic;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace FS_Core
{
    /// <summary>
    /// Draws an <c>int</c> field as a multi-select mask dropdown populated from the project's
    /// <see cref="DamageTypeDefinitions"/> ScriptableObject asset.
    ///
    /// The asset must be named <c>DamageTypeDefinitions</c> and placed inside <c>Resources/Data/</c>.
    /// Unlike Unity's built-in MaskField, this drawer shows only the defined types —
    /// no "Nothing" or "Everything" entries.
    ///
    /// Usage:
    /// <code>[DamageTypeDropdown] [SerializeField] int weaponDamageTypeMask;</code>
    /// </summary>
    [AttributeUsage(AttributeTargets.Field)]
    public class DamageTypeDropdownAttribute : PropertyAttribute { }

#if UNITY_EDITOR
    [CustomPropertyDrawer(typeof(DamageTypeDropdownAttribute))]
    public class DamageTypeDropdownDrawer : PropertyDrawer
    {
        DamageTypeDefinitions defs;

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            if (property.propertyType != SerializedPropertyType.Integer)
            {
                EditorGUI.HelpBox(position, "[DamageTypeDropdown] requires an int field.", MessageType.Error);
                return;
            }

            if (defs == null)
                defs = FindDefinitions();

            if (defs == null)
            {
                EditorGUI.LabelField(position, label.text, "Place DamageTypeDefinitions asset in Resources/Data/.");
                return;
            }

            var types = defs.damageTypes;
            if (types == null || types.Count == 0)
            {
                EditorGUI.LabelField(position, label.text, "DamageTypeDefinitions list is empty.");
                return;
            }

            // Label
            Rect labelRect = new Rect(position.x, position.y, EditorGUIUtility.labelWidth, position.height);
            Rect buttonRect = new Rect(position.x + EditorGUIUtility.labelWidth, position.y,
                position.width - EditorGUIUtility.labelWidth, position.height);

            EditorGUI.LabelField(labelRect, label);

            string display = BuildLabel(property.intValue, types);
            if (EditorGUI.DropdownButton(buttonRect, new GUIContent(display), FocusType.Keyboard))
            {
                var menu = new GenericMenu();

                // "Everything" — all defined bits set
                int allBits = (1 << types.Count) - 1;
                bool allOn = (property.intValue & allBits) == allBits;
                var propRef = property;
                menu.AddItem(new GUIContent("Everything"), allOn, () =>
                {
                    propRef.intValue = allOn ? 0 : allBits;
                    propRef.serializedObject.ApplyModifiedProperties();
                });
                menu.AddSeparator("");

                for (int i = 0; i < types.Count; i++)
                {
                    int bit = 1 << i;
                    bool isOn = (property.intValue & bit) != 0;

                    var prop = property;
                    var capturedBit = bit;

                    menu.AddItem(new GUIContent(types[i]), isOn, () =>
                    {
                        prop.intValue ^= capturedBit;
                        prop.serializedObject.ApplyModifiedProperties();
                    });
                }
                menu.DropDown(buttonRect);
            }
        }

        static string BuildLabel(int mask, List<string> types)
        {
            if (mask == 0) return "None";

            var selected = new List<string>();
            for (int i = 0; i < types.Count; i++)
                if ((mask & (1 << i)) != 0)
                    selected.Add(types[i]);

            return selected.Count == 0 ? "None" : string.Join(", ", selected);
        }

        internal static DamageTypeDefinitions FindDefinitions()
        {
            var asset = Resources.Load<DamageTypeDefinitions>("Data/DamageTypeDefinitions");
            if (asset != null) return asset;

            var guids = AssetDatabase.FindAssets("t:DamageTypeDefinitions");
            if (guids.Length == 0) return null;
            return AssetDatabase.LoadAssetAtPath<DamageTypeDefinitions>(
                AssetDatabase.GUIDToAssetPath(guids[0]));
        }
    }
#endif
}
