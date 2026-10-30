using UnityEngine;
using System;
using FS_Util;



#if UNITY_EDITOR
using UnityEditor;
#endif
namespace FS_Core
{
#if UNITY_EDITOR
    [DisallowMultipleComponent]
#endif
    public class CharacterID : MonoBehaviour
    {
#if UNITY_EDITOR
        [ReadOnly]
#endif
        public string id;

        public CharacterID()
        {
            GenerateUniqueID();
        }

        public void GenerateUniqueID()
        {
            id = Guid.NewGuid().ToString();
        }
    }
#if UNITY_EDITOR
    [CustomEditor(typeof(CharacterID))]
    public class CharacterIDEditor : Editor
    {
        SerializedProperty idProp;

        private void OnEnable()
        {
            CharacterID script = (CharacterID)target;

            if (script != null && string.IsNullOrEmpty(script.id))
            {
                Undo.RecordObject(script, "Generate Unique ID");
                //script.GenerateUniqueID();
                EditorUtility.SetDirty(script);
            }
            idProp = serializedObject.FindProperty("id");
        }
        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.PropertyField(idProp, new GUIContent("ID"), GUILayout.ExpandWidth(true));

            if (GUILayout.Button("Copy", GUILayout.Width(50)))
            {
                EditorGUIUtility.systemCopyBuffer = idProp.stringValue;
                Debug.Log($"Copied Character ID: {idProp.stringValue}");
            }
            EditorGUILayout.EndHorizontal();

            serializedObject.ApplyModifiedProperties();
        }
    }
#endif
}
