using FS_Core;
using UnityEditor;
using UnityEngine;

namespace FS_Util
{

    [CustomPropertyDrawer(typeof(BarkTagAttribute))]
    public class BarkTagDrawer : PropertyDrawer
    {
        static DialogueBarkTags cachedRegistry;

        static DialogueBarkTags GetRegistry()
        {
            if (cachedRegistry == null)
                cachedRegistry = Resources.Load<DialogueBarkTags>("Data/DialogueBarkTags");
            return cachedRegistry;
        }

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            var registry = GetRegistry();

            if (registry == null || registry.Tags == null || registry.Tags.Count == 0)
            {
                EditorGUI.PropertyField(position, property, label);
                return;
            }

            var tags = registry.Tags;
            string current = property.stringValue;

            // Build display array with current value at correct index
            int selectedIndex = tags.IndexOf(current);
            if (selectedIndex < 0)
            {
                // Current value not in registry — show it as first entry so it's visible
                var options = new string[tags.Count + 1];
                options[0] = string.IsNullOrEmpty(current) ? "(none)" : current + " (missing)";
                for (int i = 0; i < tags.Count; i++)
                    options[i + 1] = tags[i];

                int newIndex = EditorGUI.Popup(position, label.text, 0, options);
                if (newIndex > 0)
                    property.stringValue = tags[newIndex - 1];
            }
            else
            {
                var options = new string[tags.Count];
                for (int i = 0; i < tags.Count; i++)
                    options[i] = tags[i];

                int newIndex = EditorGUI.Popup(position, label.text, selectedIndex, options);
                property.stringValue = tags[newIndex];
            }
        }
    }
}