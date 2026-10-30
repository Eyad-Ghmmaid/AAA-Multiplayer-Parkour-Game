using FS_Core;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace FS_Core
{
    public class ArmorHandler : MonoBehaviour
    {
        public float TotalDefence => attachedArmors.Sum(a => a.GetAttributeValue<float>("defence"));

        List<ArmorItem> attachedArmors = new List<ArmorItem>();

        ItemAttacher itemAttacher;

        private void Start()
        {
            itemAttacher = GetComponent<ItemAttacher>();
            itemAttacher.OnItemAttach += Attachitem;
            itemAttacher.OnItemDettach += DittachItem;
        }

        private void OnDestroy()
        {
            if (itemAttacher != null)
            {
                itemAttacher.OnItemAttach -= Attachitem;
                itemAttacher.OnItemDettach -= DittachItem;
            }
        }

        private void Attachitem(Item item)
        {
            if (item is ArmorItem armor)
            {
                if (!attachedArmors.Contains(armor))
                {
                    attachedArmors.Add(armor);
                }
            }
        }
        private void DittachItem(Item item)
        {
            if (item is ArmorItem armor)
            {
                if (attachedArmors.Contains(armor))
                {
                    attachedArmors.Remove(armor);
                }
            }
        }
    }
}
