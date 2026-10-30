using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace FS_Core
{
    public class ItemCategory : ScriptableObject
    {
        public new string name;
        [TextArea]
        public string description;
        public Sprite icon;

        public bool stackable;
        public bool equippable;
        public bool consumable;
        public bool sellable = true;
        public bool droppable = true;

        public ItemAction itemAction;

        public int equipmentSlotIndex;

        public List<ItemAttribute> attributes;

        public string Name => name;
        public string Description => description;
        public Sprite Icon => icon;

        public bool Stackable => stackable;
        public bool Equippable => equippable;
        public bool Consumable => consumable;
        public bool Sellable => sellable;
        public bool Droppable => droppable;

        public List<ItemAttribute> Attributes => attributes;

        public void SetCategoryName(string name)
        {
            this.name = name;
        }

        public void SetCategoryDetails(
            string name,
            string description,
            Sprite icon,
            bool stackable,
            bool equippable,
            bool consumable,
            bool sellable,
            bool droppable,
            List<ItemAttribute> attributes)
        {
            this.name = name;
            this.description = description;
            this.icon = icon;
            this.stackable = stackable;
            this.equippable = equippable;
            this.consumable = consumable;
            this.sellable = sellable;
            this.droppable = droppable;

            // Replace the existing attributes list with a new one
            this.attributes = attributes != null ? new List<ItemAttribute>(attributes) : new List<ItemAttribute>();
        }

        public string duplicateAttributName;
        public void UpdateItemAttributeBasedOnCategory(Item item, Action repaint = null)
        {
            // Early return if category is null or override is enabled
            if (item.category != this || item.overrideCategoryAttributes)
                return;

            List<string> duplicates = item.category.Attributes
            .GroupBy(attr => attr.attributeName)
            .Where(g => g.Count() > 1)
            .Select(g => g.Key)
            .ToList();

            if (duplicates.Count > 0)
            {
                if (string.IsNullOrEmpty(duplicateAttributName)) repaint?.Invoke();

                duplicateAttributName = duplicates.First();
                return;
            }
            else
            {
                if (!string.IsNullOrEmpty(duplicateAttributName)) repaint?.Invoke();
                duplicateAttributName = "";
            }

            // Create lookup dictionaries for faster comparisons
            var categoryAttributeDict = item.category.attributes
                .ToDictionary(attr => new { attr.attributeName, attr.attributeType });

            var itemAttributeDict = item.attributeValues
                .ToDictionary(attr => new { attr.attributeName, attr.attributeType });

            // Find attributes to add (in category but not in item)
            var attributesToAdd = item.category.attributes
                .Where(catAttr => !itemAttributeDict.ContainsKey(new { catAttr.attributeName, catAttr.attributeType }));

            // Find attributes to remove (in item but not in category)
            var attributesToRemove = item.attributeValues
                .Where(itemAttr => !categoryAttributeDict.ContainsKey(new { itemAttr.attributeName, itemAttr.attributeType }))
                .ToList();

            // Only proceed with modifications if changes are needed
            if (attributesToAdd.Any() || attributesToRemove.Any())
            {
                // Add new attributes
                foreach (var attrToAdd in attributesToAdd)
                {
                    item.attributeValues.Add(new ItemAttribute(
                        attrToAdd.attributeType,
                        attrToAdd.attributeName));
                }

                // Remove old attributes
                foreach (var attrToRemove in attributesToRemove)
                {
                    item.attributeValues.Remove(attrToRemove);
                }
#if UNITY_EDITOR
                // Mark the item as dirty since we modified it
                EditorUtility.SetDirty(item);
#endif
            }
        }
    }

    public enum ItemAttributeType { Integer, Decimal, Text }

    [System.Serializable]
    public class ItemAttribute
    {
        public ItemAttributeType attributeType;
        public string attributeName;
        public string stringValue;
        public int intValue;
        public float floatValue;

        public ItemAttribute(ItemAttributeType attributeType, string attributeName)
        {
            this.attributeType = attributeType;
            this.attributeName = attributeName;
        }

        public string GetValueAsString()
        {
            if (attributeType == ItemAttributeType.Integer)
                return intValue.ToString();
            else if (attributeType == ItemAttributeType.Decimal)
                return floatValue.ToString();
            else
                return stringValue;
        }

        public float GetValueAsFloat()
        {
            return float.Parse(GetValueAsString());
        }
    }

}
