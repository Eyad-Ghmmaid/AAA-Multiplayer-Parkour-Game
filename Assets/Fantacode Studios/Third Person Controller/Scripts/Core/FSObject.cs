using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace FS_Core
{
    // A scriptable object with a unique id
    public class FSObject : ScriptableObject
    {
        [HideInInspector] public string id;

        private void OnValidate()
        {
            // Only assign if ID is missing
            if (string.IsNullOrEmpty(id))
            {
                GenerateID();
                
#if UNITY_EDITOR
                UnityEditor.EditorUtility.SetDirty(this); // Mark asset as dirty so it saves
#endif
            }
        }

        public void GenerateID()
        {
            id = Guid.NewGuid().ToString();
        }
    }
}
