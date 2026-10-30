using FS_Core;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace FS_Core {

    public class ItemAction : ScriptableObject
    {
        public virtual bool CanUse(Item item, GameObject character)
        {
            return true;
        }

        public virtual void OnUse(Item item, GameObject character)
        {
            
        }

        public virtual bool CanEquip(Item item, GameObject character)
        {
            return true;
        }

        public virtual void OnEquip(Item item, GameObject character)
        {

        }

        public virtual void OnUnequip(Item item, GameObject character)
        {

        }
    }
}
