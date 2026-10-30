using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace FS_Core
{
    public class QuickSwitchButton : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        public Image backgroundImage;
        [SerializeField] Sprite highlightedBackground;
        QuickSwitchHandler quickSwitchHandler;

        private void OnEnable()
        {
            quickSwitchHandler = FindAnyObjectByType<QuickSwitchHandler>();
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (quickSwitchHandler.GetEquippedItemImage() != backgroundImage)
            {
                backgroundImage.enabled = true;
                backgroundImage.sprite = highlightedBackground;
            }
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            if (quickSwitchHandler.GetEquippedItemImage() != backgroundImage)
            {
                backgroundImage.enabled = false;
                backgroundImage.sprite = null;
            }
        }
    }
}