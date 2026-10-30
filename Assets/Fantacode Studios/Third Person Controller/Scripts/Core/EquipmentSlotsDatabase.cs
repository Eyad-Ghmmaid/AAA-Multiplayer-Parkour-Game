using FS_Core;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace FS_Core {

    public class EquipmentSlotsDatabase : ScriptableObject
    {
        [SerializeField] List<string> equipmentSlotList;

        public List<string> EquipmentSlotList => equipmentSlotList;

        // Make it singleton
        private static EquipmentSlotsDatabase _instance;
        public static EquipmentSlotsDatabase Instance {
            get {
                if (_instance == null)
                {
                    _instance = Resources.LoadAll<EquipmentSlotsDatabase>("").FirstOrDefault();
                    if (_instance == null)
                        Debug.LogError("Equipment Slots not found!");
                }
                return _instance;
            }
        }
    }

    [System.Serializable]
    public class EquipmentSlot
    {
        [ShowEquipmentDropdown]
        public int equipmentSlotIndex = 0;

        public ItemStack ItemStack { get; set; }
    }

    public class ShowEquipmentDropdownAttribute : PropertyAttribute { }

#if UNITY_EDITOR

    [CustomPropertyDrawer(typeof(ShowEquipmentDropdownAttribute))]
    public class ShowEquipmentDropdownDrawer : PropertyDrawer
    {
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            // Only work for int properties
            if (property.propertyType != SerializedPropertyType.Integer)
            {
                EditorGUI.LabelField(position, label.text, "Use it with int");
                return;
            }

            var database = EquipmentSlotsDatabase.Instance;
            if (database == null || database.EquipmentSlotList == null || database.EquipmentSlotList.Count == 0)
            {
                EditorGUI.LabelField(position, label.text, "Equipment Slots database not found or empty");
                return;
            }

            string[] equipmentNames = database.EquipmentSlotList.ToArray();
            int index = property.intValue;

            // Clamp index to avoid out-of-bounds error
            if (index < 0 || index >= equipmentNames.Length)
                index = 0;

            // Draw popup
            int selected = EditorGUI.Popup(position, label.text, index, equipmentNames);
            property.intValue = selected;
        }
    }

#endif

}
