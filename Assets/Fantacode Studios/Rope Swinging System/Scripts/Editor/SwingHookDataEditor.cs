using FS_Core;
using UnityEditor;
using UnityEngine;

namespace FS_SwingSystem
{
    [CustomEditor(typeof(SwingHookItem))]
    public class SwingHookDataEditor : EquippableItemEditor
    {
        private SerializedProperty ropeThrowingClip;

        public override void OnEnable()
        {
            ropeThrowingClip = serializedObject.FindProperty("ropeThrowingClip");

            base.OnEnable();
        }

        public override void OnInspectorGUI()
        {
            base.OnInspectorGUI();

            serializedObject.Update();
            EditorGUILayout.PropertyField(ropeThrowingClip);
            serializedObject.ApplyModifiedProperties();
        }
    }
}
