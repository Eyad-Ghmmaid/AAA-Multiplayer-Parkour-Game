using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace FS_Util
{
    public interface ISelectableItem
    {
        void Init(SelectorBase selector, int index, Action<int> onClicked = null, Action<int> onPointerEntered = null, 
            Action<int> onPointeredExit = null);
        void Clear();
        void OnSelectionChanged(bool selected, bool hovered=false);
    }
}
