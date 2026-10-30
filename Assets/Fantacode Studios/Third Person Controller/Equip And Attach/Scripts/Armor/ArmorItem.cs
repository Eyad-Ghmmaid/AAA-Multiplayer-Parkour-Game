using FS_Core;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace FS_Core
{
    [CreateAssetMenu(menuName = "Armor/Create Armor")]
    public class ArmorItem : Item
    {

#if UNITY_EDITOR
        private void Awake()
        {
            if (category == null)
                EditorApplication.update += CallClickInput;
        }
        void CallClickInput()
        {
            if (EditorWindow.focusedWindow != null)
            {
                EditorApplication.update -= CallClickInput;
                EditorWindow.focusedWindow.SendEvent(new Event { keyCode = KeyCode.Return, type = EventType.KeyDown });
                EditorWindow.focusedWindow.SendEvent(new Event { keyCode = KeyCode.Return, type = EventType.KeyUp });
                category = Resources.Load<ItemCategory>("Category/Armor");
            }
        }
#endif
    }
}