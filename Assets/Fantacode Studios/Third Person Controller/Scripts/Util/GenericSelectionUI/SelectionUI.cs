using FS_Core;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace FS_Util
{
    public enum SelectionType { List, Grid }

    [FoldoutGroup("Selection Settings", false, "selectionType", "selectionSpeed", "numberOfColumns", "canScroll", "totalRows", "rowHeight", "scrollContent", "selectItemOnClicked", "selectClip", "focusedByDefault", "linkedSelectors", "selectionInputManager")]
    public class SelectionUI<T> : SelectorBase where T : ISelectableItem
    {
        [SerializeField] float selectionSpeed = 5;
        [SerializeField] SelectionType selectionType = SelectionType.Grid;

        [ShowIf("selectionType", 1)]
        [SerializeField] protected int numberOfColumns = 1;

        [SerializeField] bool canScroll;

        [ShowIf("canScroll", true)]
        [SerializeField] protected int totalRows = 7;
        [ShowIf("canScroll", true)]
        [SerializeField] int rowHeight = 100;
        [ShowIf("canScroll", true)]
        [SerializeField] RectTransform scrollContent;

        [SerializeField] bool selectItemOnClicked = false; 

        [SerializeField] AudioClip selectClip;

        [SerializeField] bool focusedByDefault = true;
        [SerializeField] protected List<SelectorLink> linkedSelectors = new List<SelectorLink>();

        [SerializeField] UIInteractionInputManager selectionInputManager;

        List<T> items;
        protected int selectedItem = 0;
        protected int hoveredItem = -1;

        float selectionTimer = 0;

        // Variables for scrolling
        int currentScrollRow = 0;

        public bool IsInFocus { get; private set; } = true;
        public bool SelectionPaused { get; private set; } = false;

        public event Action<int> OnSelected;
        public event Action OnBack;
        public event Action<int> OnSelectionChanged;
        public event Action<int> OnHoverChanged;

        public void Init()
        {
            if (selectionInputManager == null)
                selectionInputManager = FindAnyObjectByType<UIInteractionInputManager>();

            if (selectionType == SelectionType.List)
                numberOfColumns = 1;

            if (!focusedByDefault)
            {
                IsInFocus = false;
                UpdateSelection();
            }

            ConnectOppositeLinks();
        }

        void ConnectOppositeLinks()
        {
            foreach (var link in linkedSelectors)
            {
                if (link.isBothWay)
                {
                    link.selector.GetLinkedSelectors().Add(new SelectorLink()
                    {
                        selector = this,
                        isBothWay = true,
                        direction = link.GetOppositeDirection(),
                        goToFirstItemInDirection = link.goToFirstItemInDirection
                    });
                }
            }
        }

        public void SetItems(List<T> items)
        {
            this.items = items;

            for (int i = 0; i < items.Count; i++)
                items[i].Init(this, i, OnItemClicked, OnItemPointerEnter, OnItemPointerExit);

            UpdateSelection();
        }

        public void ClearItems()
        {
            items?.ForEach(i => i.Clear());

            this.items = null;
        }

        public virtual void HandleUpdate()
        {
            if (!IsInFocus || SelectionPaused) return;

            UpdateSelectionTimer();
            int prevSelection = selectedItem;

            if (selectionType == SelectionType.List)
                HandleListSelection();
            else if (selectionType == SelectionType.Grid)
                HandleGridSelectionClamped();

            selectedItem = Mathf.Clamp(selectedItem, 0, items.Count - 1);

            if (selectedItem != prevSelection)
            {
                UpdateSelection();
                FSAudioUtil.PlaySfx(selectClip, overridePlayingAudio: false);
            }

            if (selectionInputManager.Select)
                OnSelected?.Invoke(selectedItem);
            else if (selectionInputManager.Back)
                OnBack?.Invoke();
        }

        void HandleListSelection()
        {
            float v = selectionInputManager.NavigationInput.y;
            float h = selectionInputManager.NavigationInput.x;

            if (selectionTimer == 0 && (Mathf.Abs(v) > 0.2f || Mathf.Abs(h) > 0.2f))
            {
                if (Mathf.Abs(v) > Mathf.Abs(h))
                {
                    selectedItem += -(int)Mathf.Sign(v);
                    selectionTimer = 1 / selectionSpeed;
                }
                else
                {
                    int increment = (int)Mathf.Sign(h);
                    GotoLinkedSelector(increment < 0 ? SelectorLinkDirection.Left : SelectorLinkDirection.Right);
                }
            }
        }

        void HandleGridSelectionClamped()
        {
            float v = selectionInputManager.NavigationInput.y;
            float h = selectionInputManager.NavigationInput.x;

            if (selectionTimer == 0 && (Mathf.Abs(v) > 0.2f || Mathf.Abs(h) > 0.2f))
            {
                if (Mathf.Abs(h) > Mathf.Abs(v))
                {
                    int col = selectedItem % numberOfColumns;
                    int increment = (int)Mathf.Sign(h);

                    if ((increment < 0 && col > 0) || (increment > 0 && col < numberOfColumns - 1))
                        selectedItem += increment;
                    else
                        GotoLinkedSelector(increment < 0 ? SelectorLinkDirection.Left : SelectorLinkDirection.Right);
                }
                else
                {
                    int row = selectedItem / numberOfColumns;
                    int totalRows = items.Count / numberOfColumns;
                    int increment = -(int)Mathf.Sign(v) * numberOfColumns;

                    if ((increment < 0 && row > 0) || (increment > 0 && row < totalRows - 1))
                        selectedItem += increment;
                    else
                        GotoLinkedSelector(increment < 0 ? SelectorLinkDirection.Up : SelectorLinkDirection.Down);
                }

                selectionTimer = 1 / selectionSpeed;
            }
        }

        public virtual void UpdateSelection()
        {
            if (items == null) return;

            int selection = IsInFocus ? selectedItem : -1;

            OnSelectionChanged?.Invoke(selection);
            HandleScrolling();

            for (int i = 0; i < items.Count; i++)
            {
                items[i].OnSelectionChanged(i == selection, i == hoveredItem);
            }
        }

        void HandleScrolling()
        {
            if (!canScroll || selectedItem < 0) return;

            int totalColumns = numberOfColumns;
            int currentRow = selectedItem / totalColumns;

            if (currentRow < currentScrollRow)
                currentScrollRow = currentRow;
            else if (currentRow >= currentScrollRow + totalRows)
                currentScrollRow = currentRow + 1 - totalRows;

            var orginalPos = scrollContent.anchoredPosition;
            scrollContent.anchoredPosition = new Vector2(orginalPos.x, currentScrollRow * rowHeight);
        }

        void UpdateSelectionTimer()
        {
            if (selectionTimer > 0)
                selectionTimer = Mathf.Clamp(selectionTimer - Time.unscaledDeltaTime, 0, selectionTimer);
        }

        public override void PauseSelection()
        {
            SelectionPaused = true;
        }

        public override void UnPauseSelection()
        {
            SelectionPaused = false;
        }

        public void OnItemClicked(int index)
        {
            ChangeSelection(index);
            if (selectItemOnClicked)
                OnSelected?.Invoke(selectedItem);
        }

        public void ChangeSelection(int newSelection)
        {
            if (newSelection >= items.Count)
                return;

            if (!IsInFocus)
                SetFocus();

            selectedItem = newSelection;
            UpdateSelection();
            FSAudioUtil.PlaySfx(selectClip);
        }

        public void OnItemPointerEnter(int index)
        {
            int prevHoveredItem = hoveredItem;

            hoveredItem = index;
            if (hoveredItem != prevHoveredItem)
                OnHoverChanged?.Invoke(hoveredItem);
        }

        public void OnItemPointerExit(int index)
        {
            if (index == hoveredItem)
            {
                hoveredItem = -1;
                OnHoverChanged?.Invoke(hoveredItem);
            }
        }

        void GotoLinkedSelector(SelectorLinkDirection linkDirection)
        {
            var selectorLink = linkedSelectors.FirstOrDefault(s => s.direction == linkDirection);
            if (selectorLink != null)
            {
                bool goToLastRow = false;
                bool goToLastColumn = false;

                if (selectorLink.goToFirstItemInDirection)
                {
                    goToLastColumn = linkDirection == SelectorLinkDirection.Left ? true : false;
                    goToLastRow = linkDirection == SelectorLinkDirection.Up ? true : false;
                }

                selectorLink.selector.SetFocus(setSelectionToLastCol: goToLastColumn,
                    setSelectionToLastRow: goToLastRow);
                RemoveFocus();
            }
        }

        public override void SetFocus(bool setSelectionToLastCol = false,
            bool setSelectionToLastRow = false, int selection = -1)
        {
            if (setSelectionToLastCol || setSelectionToLastRow) 
            {
                int col = setSelectionToLastCol ? numberOfColumns-1 : 0;
                int row = setSelectionToLastRow ? (items.Count / numberOfColumns) - 1 : 0;

                selectedItem = row * numberOfColumns + col;
            } 
            else if (selection != -1)
                selectedItem = selection;

            selectionTimer = 1 / selectionSpeed;
            IsInFocus = true;
            UpdateSelection();
            FSAudioUtil.PlaySfx(selectClip);

            InvokeFocusGained();
        }

        public override void RemoveFocus()
        {
            IsInFocus = false;
            UpdateSelection();

            InvokeFocusLost();
        }

        public void Disable()
        {
            hoveredItem = -1;
        }

        public override List<SelectorLink> GetLinkedSelectors() => linkedSelectors;

        public override bool GetIsInFocus() => IsInFocus;

        public override int GetSelectedItem() => selectedItem;

        public int SelectedItem => selectedItem;
        public int HoveredItem => hoveredItem;
    }

    [Serializable]
    public class SelectorLink
    {
        public SelectorBase selector;
        public SelectorLinkDirection direction;
        public bool isBothWay = true;
        public bool goToFirstItemInDirection = false;

        public SelectorLinkDirection GetOppositeDirection()
        {
            switch (direction)
            {
                case SelectorLinkDirection.Left:
                    return SelectorLinkDirection.Right;
                case SelectorLinkDirection.Right:
                    return SelectorLinkDirection.Left;
                case SelectorLinkDirection.Up:
                    return SelectorLinkDirection.Down;
                case SelectorLinkDirection.Down:
                    return SelectorLinkDirection.Up;
            }

            return SelectorLinkDirection.Left;
        }
    }

    public enum SelectorLinkDirection { Left, Right, Up, Down }
}
