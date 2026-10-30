using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace FS_Util {

    // This class allows to link different SelectionUIs with different selectable items
    public abstract class SelectorBase : MonoBehaviour
    {
        public abstract void SetFocus(bool setSelectionToLastCol = false, bool setSelectionToLastRow = false, int selection = -1);
        public abstract void RemoveFocus();

        public abstract void PauseSelection();
        public abstract void UnPauseSelection();

        public abstract List<SelectorLink> GetLinkedSelectors();
        public abstract bool GetIsInFocus();
        public abstract int GetSelectedItem();

        public event Action OnFocusGained;
        public event Action OnFocusLost;

        protected void InvokeFocusGained() => OnFocusGained?.Invoke();
        protected void InvokeFocusLost() => OnFocusLost?.Invoke();
    }

}
