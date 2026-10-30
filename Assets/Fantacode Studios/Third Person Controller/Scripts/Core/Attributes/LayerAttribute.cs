using UnityEngine;

namespace FS_Util
{
    public class LayerAttribute : PropertyAttribute { }

    #if UNITY_EDITOR
    [UnityEditor.CustomPropertyDrawer(typeof(LayerAttribute))]
    public class LayerDrawer : UnityEditor.PropertyDrawer
    {
        public override void OnGUI(Rect position, UnityEditor.SerializedProperty property, GUIContent label)
        {
            property.intValue = UnityEditor.EditorGUI.LayerField(position, label, property.intValue);
        }
    }
    #endif

}