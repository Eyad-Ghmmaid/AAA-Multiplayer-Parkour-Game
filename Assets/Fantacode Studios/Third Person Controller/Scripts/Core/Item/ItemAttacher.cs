using FS_Core;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace FS_Core
{
    /// <summary>
    /// Item attacher is used to attach items like weapons, helmets, armor, etc to the body
    /// Note that it cannot equip an item. For example, it can attach a sword to the back of 
    /// the body but it cannot equip it. For equipping items, use ItemEquipper class
    /// </summary>
    public class ItemAttacher : MonoBehaviour
    {
        [SerializeField] List<ItemHolderSlot> itemHolderSlots = new List<ItemHolderSlot>();

        public Dictionary<int, AttachedItem> AttachedItems { get; private set; } = new Dictionary<int, AttachedItem>();

        SkinnedMeshRenderer characterSMR;
        Dictionary<string, Transform> characterBones;

        public Action<Item> OnItemAttach;
        public Action<Item> OnItemDettach;

        public ItemEquipper ItemEquipper { get; private set; }
        public EquippableItem DefaultItem { get; set; }
        Animator animator;

        private void Awake()
        {
            ItemEquipper = GetComponent<ItemEquipper>();
            animator = GetComponent<Animator>();
            characterSMR = GetComponentInChildren<SkinnedMeshRenderer>();
            SaveCharacterBones();
        }

        public void AttachItem(Item item, bool disableItemAfterAttach = false)
        {
            if (!item.Equippable || !item.attachOnBody || item.ModelToAttach == null) return;

            GameObject itemInstance = null;
            GameObject holderParent = null;
            GameObject configurableJointObject = null;
            Joint configurableJoint = null;

            // Parent to corresponding attachment point
            ItemHolderSlot slot = itemHolderSlots.FirstOrDefault(i => i.equipmentSlotIndex == item.equipmentSlotIndex);

            if (AttachedItems.ContainsKey(item.equipmentSlotIndex))
            {

                var prevItem = AttachedItems[item.equipmentSlotIndex];
                if (prevItem.item == item) return;

                if (prevItem != null)
                    DetachItem(prevItem.item, enableDefaultItems: false, autoEquipDefault: false);
            }

            if (item.isSkinnedMesh)
            {
                var obj = Instantiate(item.modelPrefab, transform);

                var smr = obj.GetComponentInChildren<SkinnedMeshRenderer>();
                smr.rootBone = characterSMR.rootBone;

                // Match bone names between equipment and character
                Transform[] boneMap = new Transform[smr.bones.Length];
                for (int i = 0; i < smr.bones.Length; i++)
                {
                    string boneName = smr.bones[i].name;
                    boneMap[i] = FindBoneByName(smr.rootBone, boneName);
                }

                smr.bones = boneMap;
                smr.transform.SetParent(transform, true);
                Destroy(obj);

                itemInstance = smr.gameObject;
                if (itemInstance != null)
                {
                    itemInstance.transform.localPosition = item.ItemLocalPosition(ItemEquipper.CharacterId, animator.avatar);
                    itemInstance.transform.localRotation = Quaternion.Euler(item.ItemLocalRotation(ItemEquipper.CharacterId, animator.avatar));
                }
            }
            else
            {

                if (!AttachedItems.ContainsKey(item.EquipmentSlotIndex))
                {
                    var attachBone = animator.GetBoneTransform(item.attachBone);

                    if (item.usePhysicsForAttachedItem)
                    {
                        GameObject attachmentPhysicsPrefab = item.overridePhysicsPrefab ? item.physicsPrefab : Resources.Load<GameObject>("Attachment Physics Prefab");
                        if (attachmentPhysicsPrefab != null)
                        {
                            configurableJointObject = Instantiate(attachmentPhysicsPrefab);
                            configurableJoint = configurableJointObject.GetComponent<Joint>();
                            if (configurableJoint != null)
                            {
                                configurableJointObject.transform.SetParent(attachBone);
                                configurableJointObject.transform.localPosition = Vector3.zero;
                                configurableJointObject.transform.localRotation = Quaternion.identity;

                                GameObject connectedBody = attachBone.transform.Find("FS_Attach")?.gameObject;
                                if (connectedBody == null)
                                {
                                    connectedBody = new GameObject("FS_Attach");
                                    connectedBody.transform.SetParent(attachBone);
                                    connectedBody.transform.localPosition = Vector3.zero;
                                    connectedBody.transform.localRotation = Quaternion.identity;
                                    var _rb = connectedBody.AddComponent<Rigidbody>();
                                    _rb.isKinematic = true;
                                }
                                var rb = connectedBody.GetComponent<Rigidbody>();
                                configurableJoint.connectedBody = rb;
                            }
                        }
                    }


                    if (item.hasHolder && item.holderPrefab != null)
                    {
                        holderParent = new GameObject("Holder Parent");
                        holderParent.transform.SetParent(attachBone, true);
                        holderParent.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
                        var holderObject = Instantiate(item.holderPrefab);
                        holderObject.transform.SetParent(holderParent.transform, true);
                        holderObject.transform.localPosition = item.HolderLocalPosition(ItemEquipper.CharacterId, animator.avatar);
                        holderObject.transform.localRotation = Quaternion.Euler(item.HolderLocalRotation(ItemEquipper.CharacterId, animator.avatar));
                    }
                    itemInstance = Instantiate(item.ModelToAttach);
                    itemInstance.transform.SetParent(holderParent != null ? holderParent.transform : (configurableJointObject != null ? configurableJointObject.transform : attachBone));

                    if (holderParent != null)
                    {
                        itemInstance.transform.position = holderParent.transform.TransformPoint(item.ItemLocalPosition(ItemEquipper.CharacterId, animator.avatar));
                        itemInstance.transform.rotation = holderParent.transform.rotation * Quaternion.Euler(item.ItemLocalRotation(ItemEquipper.CharacterId, animator.avatar));
                    }
                    else
                    {
                        itemInstance.transform.localPosition = item.ItemLocalPosition(ItemEquipper.CharacterId, animator.avatar);
                        itemInstance.transform.localRotation = Quaternion.Euler(item.ItemLocalRotation(ItemEquipper.CharacterId, animator.avatar));
                    }
                }
            }

            // Disable objects for the equipment slot when attaching this item
            if (slot != null)
            {
                foreach (var obj in slot.objectsToDisable)
                {
                    obj.SetActive(false);
                }
            }

            // Track the attached item
            var attachedItem = new AttachedItem()
            {
                item = item,
                itemModel = itemInstance,
                parentObject = configurableJointObject != null ? configurableJointObject : holderParent,
                itemHolder = holderParent
            };
            if (AttachedItems.ContainsKey(item.equipmentSlotIndex))
                AttachedItems[item.equipmentSlotIndex] = attachedItem;
            else
                AttachedItems.Add(item.equipmentSlotIndex, attachedItem);

            if (disableItemAfterAttach)
            {
                if (attachedItem.itemHolder != null)
                    attachedItem.itemHolder.SetActive(false);
                else if (attachedItem.itemModel != null)
                    attachedItem.itemModel.SetActive(false);
            }

            OnItemAttach?.Invoke(item);
        }

        public void DetachItem(Item item, bool enableDefaultItems = true, bool autoEquipDefault = true)
        {
            var kvp = AttachedItems.FirstOrDefault(i => i.Value.item == item);
            var attachedItem = kvp.Value;

            if (attachedItem != null)
            {
                // If the item was currently equipped, then unequip it.
                // The outer ItemEquipper.Equip slot-replacement flow handles the
                // unequip itself, so it passes autoEquipDefault=false to avoid
                // starting a parallel Equip(DefaultItem)/UnEquipItem coroutine that
                // would race with it and can leave EquippedItem null on completion.
                if (attachedItem.isHidden && autoEquipDefault)
                {
                    if (DefaultItem != null)
                        ItemEquipper.EquipItem(DefaultItem, playEquipAnimation: false);
                    else
                        ItemEquipper.UnEquipItem(playUnEquipAnimation: false);
                }

                AttachedItems.Remove(kvp.Key);
                Destroy(attachedItem.itemModel);
                if (attachedItem.parentObject != null)
                    Destroy(attachedItem.parentObject);

                if (enableDefaultItems)
                {
                    // Re-enable any objects that were disabled when this item was attached
                    ItemHolderSlot slot = itemHolderSlots.FirstOrDefault(i => i.equipmentSlotIndex == item.equipmentSlotIndex);
                    if (slot != null)
                    {
                        foreach (var obj in slot.objectsToDisable)
                        {
                            obj.SetActive(true);
                        }
                    }
                }
            }


            OnItemDettach?.Invoke(item);
        }

        // Hides the attached model. UseCase: Called when item is equipped and moves to hand.
        public void HideBodyAttachment(Item item)
        {
            SetBodyAttachmentVisible(item, false);
        }


        // Shows the attached model. UseCase: Called when item is unequipped and moves to body
        public void ShowBodyAttachement(Item item)
        {
            SetBodyAttachmentVisible(item, true);
        }

        void SetBodyAttachmentVisible(Item item, bool visible)
        {
            var kvp = AttachedItems.FirstOrDefault(i => i.Value.item == item);
            var attachedItem = kvp.Value;

            if (attachedItem != null)
            {
                attachedItem.isHidden = !visible;

                if (attachedItem.itemHolder != null)
                    attachedItem.itemHolder.SetActive(visible);
                else if (attachedItem.itemModel != null)
                    attachedItem.itemModel.SetActive(visible);
            }
        }

        public Item GetAttachedItem(int equipmentSlot)
        {
            if (AttachedItems.ContainsKey(equipmentSlot))
                return AttachedItems[equipmentSlot].item;
            else
                return null;
        }


        Transform FindBoneByName(Transform root, string name)
        {
            if (characterBones.ContainsKey(name))
                return characterBones[name];

            return null;
        }

        void SaveCharacterBones()
        {
            characterBones = new Dictionary<string, Transform>();

            var root = characterSMR.rootBone;
            var childTransforms = root.GetComponentsInChildren<Transform>(true);
            foreach (var bone in characterSMR.bones)
            {
                var boneTransform = childTransforms.FirstOrDefault(child => child.name == bone.name);
                if (boneTransform != null)
                    characterBones.Add(bone.name, boneTransform);
            }
        }

        public float GetTotalDefense()
        {
            var armorCategory = Resources.Load<ItemCategory>("Category/Armor");
            var armorItems = AttachedItems.Values.Where(i => i.item.category == armorCategory).Select(i => i.item).ToList();

            float totalDefense = 0;
            for (int i = 0; i < armorItems.Count; i++)
                totalDefense += armorItems[i].GetAttributeValue<float>("defense");

            return totalDefense;
        }
    }

    public class AttachedItem
    {
        public Item item;
        public GameObject itemModel;
        public GameObject itemHolder;
        public GameObject parentObject;
        public bool isHidden = false;
    }

    [Serializable]
    public class ItemHolderSlot
    {
        [ShowEquipmentDropdown]
        public int equipmentSlotIndex;
        //public Transform holder;

        [Tooltip("Objects that will be disabled while attaching an item")]
        public List<GameObject> objectsToDisable = new List<GameObject>();
    }
}
