using FS_InteractionSystem;
using UnityEngine;

namespace FS_Core
{
    public class EquipSettings : MonoBehaviour
    {
        [SerializeField] EquipHotspot equipHotspot;
        [SerializeField] bool equipItemsBasedOnSlot;

        public EquipHotspot EquipHotspot => equipHotspot;
        public bool EquipItemsBasedOnSlot => equipItemsBasedOnSlot;


        public static EquipSettings i { get; private set; }
        private void Awake()
        {
            i = this;
        }


    }
}
