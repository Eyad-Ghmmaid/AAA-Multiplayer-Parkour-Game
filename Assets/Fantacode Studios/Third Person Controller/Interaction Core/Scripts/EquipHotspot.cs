using FS_Core;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Events;

namespace FS_InteractionSystem
{
    public class EquipHotspot : Hotspot
    {
        public EquippableItem item;
        [Tooltip("If checked, the item's name will be shown as the interaction label")]
        public bool showItemNameForLabel;
        [Space(5)]
        public UnityEvent OnEquipEvent;

        public EquippableItemObject itemObject; // Reference of item object incase we want to disable it after interaction

        public override void ShowIndicator(bool state)
        {
            base.ShowIndicator(state);

            if (interactText != null)
                interactText.text = showItemNameForLabel && item != null && !string.IsNullOrEmpty(item.name) ? item.name : interactionLabel;
        }

        public override void Interact(HotspotController detector)
        {
            var itemEquipper = detector.GetComponent<ItemEquipper>();
            if (!itemEquipper.PreventItemSwitching && !itemEquipper.IsChangingItem)
            {
                itemEquipper.EquipItem(item, false, itemObject: itemObject);
                OnEquipEvent?.Invoke();

                base.Interact(detector);
                //if (itemObject != null)
                //    itemObject.gameObject.SetActive(false);
                
                gameObject.SetActive(false);
            }
        }
    }
}