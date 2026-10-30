using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace FS_Util
{
    public enum HighlightAction { ChangeBackgroundImage, ChangeBackgroundColor }

    public class ImageSlot : MonoBehaviour, ISelectableItem, IPointerEnterHandler, IPointerExitHandler
    {
        [SerializeField] HighlightAction highlightAction;

        [ShowIf("highlightAction", 0)]
        [SerializeField] Sprite highlightedBackground;
        [ShowIf("highlightAction", 0)]
        [SerializeField] Sprite selectedBackground;

        [ShowIf("highlightAction", 1)]
        [SerializeField] Color selectedColor = Color.blue;
        [ShowIf("highlightAction", 1)]
        [SerializeField] Color highlightedColor = Color.blue;

        [Space(10)]

        [SerializeField] bool useCustomBackgroundImage = false;

        [ShowIf("useCustomBackgroundImage", true)]
        [Tooltip("Background Image to affect when the slot is highlighted. By default image attacted to the gameobject will be chosen")]
        [SerializeField] Image backgroundImage;

        Color normalColor;
        Sprite normalBackground;

        SelectorBase selector;
        int index = 0;
        Action<int> onPointerEnter;
        Action<int> onPointerExit;
        Action<int> onClicked;

        Button button;
        void Awake()
        {
            if (backgroundImage == null)
                backgroundImage = GetComponent<Image>();
            button = GetComponent<Button>();

            normalColor = backgroundImage.color;
            normalBackground = backgroundImage.sprite;
        }

        public void Init(SelectorBase selector, int index, Action<int> onClicked = null, 
            Action<int> onPointerEnter = null, Action<int> onPointerExit = null)
        {
            this.selector = selector;
            this.index = index;
            this.onClicked = onClicked;
            this.onPointerEnter = onPointerEnter;
            this.onPointerExit = onPointerExit;

            if (button != null && onClicked != null)
            {
                button.onClick.RemoveAllListeners();
                button.onClick.AddListener(() => onClicked?.Invoke(this.index));
            }
        }

        public void Clear()
        {
            if (highlightAction == HighlightAction.ChangeBackgroundImage)
                backgroundImage.sprite = normalBackground;
            else
                backgroundImage.color = normalColor;
        }

        public void OnSelectionChanged(bool selected, bool hovered = false)
        {
            if (selected)
            {
                if (highlightAction == HighlightAction.ChangeBackgroundImage)
                    backgroundImage.sprite = selectedBackground;
                else
                    backgroundImage.color = selectedColor;
            }
            else if (hovered)
            {
                if (highlightAction == HighlightAction.ChangeBackgroundImage)
                    backgroundImage.sprite = highlightedBackground;
                else
                    backgroundImage.color = highlightedColor;
            }
            else
            {
                if (highlightAction == HighlightAction.ChangeBackgroundImage)
                    backgroundImage.sprite = normalBackground;
                else
                    backgroundImage.color = normalColor;
            }
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (gameObject.activeInHierarchy)
            {
                onPointerEnter?.Invoke(index);

                // A selected Item should not be highlighted
                if (selector != null && (!selector.GetIsInFocus() || selector.GetSelectedItem() != index))
                {
                    if (highlightAction == HighlightAction.ChangeBackgroundImage)
                        backgroundImage.sprite = highlightedBackground;
                    else
                        backgroundImage.color = highlightedColor;
                }
            }
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            if (gameObject.activeInHierarchy)
            {
                onPointerExit?.Invoke(index);

                // A selected Item should not be unhighlighted to normal
                if (selector != null && (!selector.GetIsInFocus() || selector.GetSelectedItem() != index))
                {
                    if (highlightAction == HighlightAction.ChangeBackgroundImage)
                        backgroundImage.sprite = normalBackground;
                    else
                        backgroundImage.color = normalColor;
                }
            }
        }
    }
}
