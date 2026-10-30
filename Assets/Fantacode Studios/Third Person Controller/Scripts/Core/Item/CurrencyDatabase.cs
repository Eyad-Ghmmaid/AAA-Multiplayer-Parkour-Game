using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace FS_Core
{

    public class CurrencyDatabase : ScriptableObject
    {
        public List<Currency> currencyList;

        public Currency GetCurrencyByIndex(int index) => currencyList[index];

        // Make it singleton
        private static CurrencyDatabase _instance;
        public static CurrencyDatabase Instance {
            get {
                if (_instance == null)
                {
                    _instance = Resources.LoadAll<CurrencyDatabase>("").FirstOrDefault();
                    if (_instance == null)
                        Debug.LogError("CurrencyDatabase not found in Resources folder!");
                }
                return _instance;
            }
        }
    }

    [Serializable]
    public class Currency
    {
        public string name;
        public string symbol;
        public Sprite icon;
    }

    [Serializable]
    public class CurrencyAmount
    {
        public CurrencyAmount(float amount, int currencyIndex)
        {
            this.amount = amount;
            this.currencyIndex = currencyIndex;
        }

        public float amount;

        [ShowCurrencyDropdown]
        public int currencyIndex;
    }

    public class ShowCurrencyDropdownAttribute : PropertyAttribute { }

#if UNITY_EDITOR

    [CustomPropertyDrawer(typeof(ShowCurrencyDropdownAttribute))]
    public class ShowCurrencyDropdownDrawer : PropertyDrawer
    {
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            // Only work for int properties
            if (property.propertyType != SerializedPropertyType.Integer)
            {
                EditorGUI.LabelField(position, label.text, "Use ShowCurrencyDropdown with int");
                return;
            }

            var database = CurrencyDatabase.Instance;
            if (database == null || database.currencyList == null || database.currencyList.Count == 0)
            {
                EditorGUI.LabelField(position, label.text, "Currency database not found or empty");
                return;
            }

            // Get currency names
            string[] currencyNames = database.currencyList.Select(c => c.name).ToArray();
            int index = property.intValue;

            // Clamp index to avoid out-of-bounds error
            if (index < 0 || index >= currencyNames.Length)
                index = 0;

            // Draw popup
            int selected = EditorGUI.Popup(position, label.text, index, currencyNames);
            property.intValue = selected;
        }
    }

#endif

}
