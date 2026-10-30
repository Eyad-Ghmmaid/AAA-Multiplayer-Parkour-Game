using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace FS_Util
{
    public class TextSlot : MonoBehaviour, ISelectableItem
    {
        [SerializeField] Text text;
        [SerializeField] Color highlightedColor = Color.blue;

        Color originalColor;
        public void Init(SelectorBase selector, int index, Action<int> onClicked = null, 
            Action<int> onPointerEnter = null, Action<int> onPointerExit = null)
        {
            originalColor = text.color;
        }

        public void Clear()
        {
            text.color = originalColor;
        }

        public void OnSelectionChanged(bool selected, bool hovered = false)
        {
            text.color = (selected) ? highlightedColor : originalColor;
        }

        public void SetText(string s)
        {
            text.text = s;
        }
    }
}
