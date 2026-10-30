using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Serialization;
using FS_Util;



#if UNITY_EDITOR
using UnityEditor;
#endif

namespace FS_Core
{
    public class Item : FSObject
    {
        public new string name;
        [TextArea]
        public string description;
        public Sprite icon;
        public ItemCategory category;
        public bool overrideCategorySettings;
        public bool overrideCategoryAttributes;

        public int equipmentSlotIndex = -1;
        public GameObject modelPrefab;

        [Tooltip("If true, the item will be attached to the character's body (e.g., holster, sheath, or directly to a bone).")]
        public bool attachOnBody;

        [Tooltip("The prefab of the item that will be attached to the character's body.")]
        public GameObject modelToAttach;

        public bool useCustomAttachmentModel = false;

        public bool usePhysicsForAttachedItem = false;
        public bool overridePhysicsPrefab = false;
        public GameObject physicsPrefab;

        public bool isSkinnedMesh = false;

        [Tooltip("If true, the item will use a holder object (e.g., holster or sheath) when attached to the body.")]
        [ShowIf("attachOnBody", true)]
        public bool hasHolder;

        [Tooltip("The holder object that will be used to attach the item (e.g., a holster or sheath).")]
        [ShowIf(LogicOperator.And, "hasHolder", true, "attachOnBody", true)]
        public GameObject holderPrefab;

        [Tooltip("The bone on the humanoid rig to which the holder object or item will be attached. If hasHolder is false, this bone is used to attach the item directly.")]
        [ShowIf("attachOnBody", true)]
        public HumanBodyBones attachBone = HumanBodyBones.Spine;
        


        [Tooltip("Local position offset for the holder object relative to the attach bone.")]
        [ShowIf(LogicOperator.And, "hasHolder", true, "attachOnBody", true)]
        public Vector3 holderLocalPosition;

        [Tooltip("Local rotation offset for the holder object relative to the attach bone.")]
        [ShowIf(LogicOperator.And, "hasHolder", true, "attachOnBody", true)]
        public Vector3 holderLocalRotation;


        [Tooltip("Local position of the item relative to the holder object.")]
        [ShowIf("attachOnBody", true)]
        public Vector3 itemLocalPosition;
        [Tooltip("Local rotation of the item relative to the holder object.")]
        [ShowIf("attachOnBody", true)]
        public Vector3 itemLocalRotation;


        [Tooltip("Per-character attach references for the holder object. " +
         "Each entry defines the correct local position and rotation values needed for different characters " +
         "to properly align the holder (e.g., on hip or back).")]
        public List<ItemAttachReference> holderAttachReferences = new List<ItemAttachReference>();

        [Tooltip("Per-character attach references for the item inside its holder. " +
                 "Each entry defines the local position and rotation values required for different characters " +
                 "to ensure the item fits correctly within the holder (e.g., sword angle or weapon grip alignment).")]
        public List<ItemAttachReference> itemAttachReferences = new List<ItemAttachReference>();

        public Vector3 HolderLocalPosition(CharacterID characterID, Avatar avatar)
        {
            var reference = FindAttachedHolderReferenceData(characterID, avatar);
            return reference != null ? reference.localPosition : holderLocalPosition;
        }

        // Get Right Hand Rotation
        public Vector3 HolderLocalRotation(CharacterID characterID, Avatar avatar)
        {
            var reference = FindAttachedHolderReferenceData(characterID, avatar);
            return reference != null ? reference.localRotation : holderLocalRotation;
        }

        // Get Left Hand Position
        public Vector3 ItemLocalPosition(CharacterID characterID, Avatar avatar)
        {
            var reference = FindAttachedItemReferenceData(characterID, avatar);
            return reference != null ? reference.localPosition : itemLocalPosition;
        }

        // Get Left Hand Rotation
        public Vector3 ItemLocalRotation(CharacterID characterID, Avatar avatar)
        {
            var reference = FindAttachedItemReferenceData(characterID, avatar);
            return reference != null ? reference.localRotation : itemLocalRotation;
        }

        public ItemAttachReference FindAttachedHolderReferenceData(CharacterID characterID, Avatar avatar)
        {
            if (characterID == null) return null;

            return holderAttachReferences.Find(r =>
                r.characterReference.characterId == characterID.id ||
                r.characterReference.avatar == avatar);
        }

        public ItemAttachReference FindAttachedItemReferenceData(CharacterID characterID, Avatar avatar)
        {
            if (characterID == null) return null;

            return itemAttachReferences.Find(r =>
                r.characterReference.characterId == characterID.id ||
                r.characterReference.avatar == avatar);
        }


        public GameObject ModelToAttach => useCustomAttachmentModel ? modelToAttach : modelPrefab;


        public bool sellable;
        public bool droppable;
        public bool stackable;

        public float weight;
        public bool canCraft;
        public CurrencyAmount priceToCraft;
        public bool canDismantle;
        public CurrencyAmount priceToDismantle;
        public List<ItemStack> ingredients;
        public bool overrideDismantleItems;
        public List<ItemStack> dismantleToItems;

        public List<ItemStack> DismantleItems => overrideDismantleItems ? dismantleToItems : ingredients;



        public CurrencyAmount price;
        public bool requiresMultipleCurrencies = false;
        public List<CurrencyAmount> currenciesRequired;

        public bool overrideAction;
        public ItemAction itemAction;

        public List<ItemAttribute> attributeValues = new List<ItemAttribute>();
        public List<ItemAttribute> overrideAttributes = new List<ItemAttribute>();

        public GameObject pickupPrefab;

        public string Name => name;
        public string Description => description;
        public Sprite Icon => icon;
        
        public GameObject PickupPrefab => pickupPrefab;

        public bool Equippable => category.Equippable;
        public int EquipmentSlotIndex => equipmentSlotIndex;

        public bool Consumable => category.Consumable;
        public bool Sellable => overrideCategorySettings ? sellable : category.Sellable;
        public bool Stackable => overrideCategorySettings ? stackable : category.Stackable;
        public bool Droppable => overrideCategorySettings ? droppable : category.Droppable;

        public List<ItemAttribute> Attributes => overrideCategoryAttributes ? overrideAttributes : attributeValues;

        public ItemAction ItemAction => overrideAction ? itemAction : category.itemAction;

        public List<ItemStack> Ingredients => ingredients;

        public ItemCategory Category => category;

        // Get Right Hand Position
        


        public T GetAttributeValue<T>(string attributeName)
        {
            var attribute = Attributes.FirstOrDefault(a => a.attributeName.ToLower() == attributeName.ToLower());
            if (attribute != null)
            {
                if (typeof(T) == typeof(int))
                    return (T)(object)attribute.intValue;
                else if (typeof(T) == typeof(float))
                    return (T)(object)attribute.floatValue;
                else
                    return (T)(object)attribute.stringValue;
            }
            else
            {
                return default(T);
            }
        }

        public virtual bool Use(GameObject character)
        {
            if (ItemAction != null)
            {
                if (!ItemAction.CanUse(this, character))
                    return false;

                ItemAction.OnUse(this, character);
                return true;
            }

            return true;
        }

        public virtual bool Equip(GameObject character, ItemAttacher itemAttacher = null)
        {
            if (attachOnBody)
            {
                //ItemEquipper itemEquipper = null;
                //if (itemAttacher != null)
                //    itemEquipper = itemAttacher.ItemEquipper;

                //if (itemEquipper?.EquippedItem != null && itemEquipper?.EquippedItem?.Category == category)
                //    itemEquipper?.EquipItem(this as EquippableItem, playEquipAnimation: false);
                //else

                itemAttacher?.AttachItem(this);

            }

            if (ItemAction != null)
            {
                if (!ItemAction.CanEquip(this, character))
                    return false;

                ItemAction.OnEquip(this, character);
                return true;
            }

            return false;
        }

        public virtual bool Unequip(GameObject character, ItemAttacher itemAttacher = null)
        {
            if (attachOnBody)
            {
                //ItemEquipper itemEquipper = null;
                //if (itemAttacher != null)
                //    itemEquipper = itemAttacher.ItemEquipper;

                //if (itemEquipper?.EquippedItem == this)
                //    itemEquipper?.UnEquipItem(playUnEquipAnimation: false);
                //else
                //    itemAttacher.DettachItem(this);

                itemAttacher.DetachItem(this);
            }

            if (ItemAction != null)
            {
                if (!ItemAction.CanEquip(this, character))
                    return false;

                ItemAction.OnUnequip(this, character);
                return true;
            }

            return false;
        }

        public void Init(string newName)
        {
            name = newName;
            category = Resources.Load<ItemCategory>("Category/Default Category");
        }


#if UNITY_EDITOR
        private void Awake()
        {
            if (category == null)
                EditorApplication.update += CallClickInput;
        }
        void CallClickInput()
        {
            if (EditorWindow.focusedWindow != null)
            {
                EditorApplication.update -= CallClickInput;
                EditorWindow.focusedWindow.SendEvent(new Event { keyCode = KeyCode.Return, type = EventType.KeyDown });
                EditorWindow.focusedWindow.SendEvent(new Event { keyCode = KeyCode.Return, type = EventType.KeyUp });
                SetCategory();
            }
        }
#endif

        public virtual void SetCategory() { }
    }

    // Stack of same items
    [Serializable]
    public class ItemStack : ISerializationCallbackReceiver
    {
        [SerializeField] Item item;
        [SerializeField] int count = 1;

        public Item Item {
            get => item;
            set => item = value;
        }
        public int Count {
            get => count;
            set => count = value;
        }

        [SerializeField, HideInInspector]
        private bool serialized;

        public void OnAfterDeserialize()
        {
            if (!serialized)
            {
                count = 1;
            }
        }

        public void OnBeforeSerialize()
        {
            if (serialized) return;
            serialized = true;
        }
    }

    [System.Serializable]
    public class ItemAttachReference
    {
        [Tooltip("The character this attach reference belongs to. " +
                 "Used to identify which character requires these local values for proper alignment.")]
        public CharacterReference characterReference;

        [Tooltip("Local position offset relative to the attach point. " +
                 "Defines where the holder or item should be placed for this specific character.")]
        public Vector3 localPosition;

        [Tooltip("Local rotation offset relative to the attach point. " +
                 "Ensures the holder or item is oriented correctly for this specific character.")]
        public Vector3 localRotation;
    }

    [System.Serializable]
    public class CharacterReference
    {
        [Tooltip("A unique identifier for the character. " +
                 "Used to look up the correct attach reference for this character (e.g., 'Hero01', 'EnemyKnight').")]
        [FormerlySerializedAs("charcaterId")]
        public string characterId;

        [Tooltip("The Avatar associated with this character. " +
                 "Provides the rig information needed to apply the correct local offsets for item attachment.")]
        public Avatar avatar;
    }

}
