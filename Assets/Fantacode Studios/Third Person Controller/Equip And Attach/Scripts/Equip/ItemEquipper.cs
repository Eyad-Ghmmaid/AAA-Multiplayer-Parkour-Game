using FS_LocomotionSystem;
using FS_Util;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace FS_Core
{
    [RequireComponent(typeof(BoneMapper))]
    public class ItemEquipper : MonoBehaviour
    {
        [field: HideInInspector] public List<EquippableItem> equippableItems = new List<EquippableItem>();

        public EquippableItem EquippedItem { get; set; }

        public bool IsCurrentItemUnusable { get; private set; }
        public EquippableItemHolder LeftHandHolder { get; private set; }
        public EquippableItemHolder RightHandHolder { get; private set; }

        public EquippableItemObject EquippedItemRight => RightHandHolder?.currentItem;
        public EquippableItemObject EquippedItemLeft => LeftHandHolder?.currentItem;
        public EquippableItemObject EquippedItemObject => EquippedItem?.holderBone == HumanBodyBones.RightHand ? RightHandHolder.currentItem : LeftHandHolder.currentItem;



        public bool IsChangingItem => IsEquippingItem || IsUnEquippingItem;
        public bool IsEquippingItem { get; set; }
        public bool IsUnEquippingItem { get; set; }
        public bool PreventItemSwitching { get; set; } = false;
        public bool PreventItemUnEquip { get; set; } = false;
        public bool InterruptItemSwitching { get; set; } = false;

        /// <summary>
        /// Invoked when the item becomes completely unusable and can no longer be used (e.g., out of ammo, broken, disabled by game state).
        /// </summary>
        public Action<EquippableItem> OnItemBecameUnusable;

        /// <summary>
        /// Invoked when the item becomes restricted and cannot be used anymore (e.g., due to conditions like being stunned, disarmed, or out of bounds).
        /// </summary>
        public Action<EquippableItem> OnItemBecameRestricted;

        /// <summary>
        /// Invoked when a previously restricted item becomes usable again (e.g., restriction lifted, re-enabled).
        /// </summary>
        public Action<EquippableItem> OnItemBecameUnrestricted;

        public Action<EquippableItem> OnEquip;
        public Action OnUnEquip;

        public Action<EquippableItem> OnEquipComplete;
        public Action<EquippableItem> OnBeforeItemDisable;
        public Action OnUnEquipComplete;


        AnimGraph animGraph;
        ItemAttacher itemAttacher;
        BoneMapper boneMapper;
        LocomotionCore locomotion;

        public Animator Animator { get; private set; }
        public CharacterID CharacterId { get; private set; }


        public void Awake()
        {
            Animator = GetComponent<Animator>();
            animGraph = GetComponent<AnimGraph>();
            itemAttacher = GetComponent<ItemAttacher>();
            locomotion = GetComponent<LocomotionCore>();
            boneMapper = GetComponent<BoneMapper>();
            CharacterId = GetComponent<CharacterID>();

            if (CharacterId == null)
                CharacterId = gameObject.AddComponent<CharacterID>();

            LeftHandHolder = boneMapper.GetBone(BoneType.LeftHand).GetComponentInChildren<EquippableItemHolder>();
            RightHandHolder = boneMapper.GetBone(BoneType.RightHand).GetComponentInChildren<EquippableItemHolder>();

        }

        /// <summary>
        /// Adds an equippable item to the equippable items list if it is not already present.
        /// </summary>
        /// <param name="item">The equippable item to add.</param>
        public void AddItem(EquippableItem item)
        {
            if (!equippableItems.Contains(item))
                equippableItems.Add(item);
        }

        public void RemoveItem(EquippableItem item)
        {
            if (equippableItems.Contains(item))
            {
                equippableItems.Remove(item);
            }
        }

        #region Equip

        public void EquipItem(EquippableItem itemData, bool playEquipAnimation = true, Action onItemEnabled = null, Action onPrevItemDisabled = null, EquippableItemObject itemObject = null)
        {
            StartCoroutine(Equip(itemData, playEquipAnimation, onItemEnabled, onPrevItemDisabled, itemObject));
        }


        /// <summary>
        /// Handles the process of equipping a new item, including unequipping the current item if necessary,
        /// playing equip animations, attaching the item to the correct hand(s), and invoking relevant callbacks.
        /// Supports equipping based on item slot, interruption, and optional item instantiation.
        /// <param name="dropItem">If true, drops the item after equipping (used during slot replacement).</param>
        /// </summary>
        private IEnumerator Equip(EquippableItem itemData, bool playEquipAnimation = true, Action onItemEnabled = null, Action onPrevItemDisabled = null, EquippableItemObject itemObject = null, bool dropItem = false)
        {
            // Exit if the item is null, the same as the currently equipped item, or if switching is prevented
            if (itemData == null || PreventItemSwitching || IsChangingItem)
                yield break;

            // When an explicit ItemObject reference is provided (e.g. picking up an
            // enemy's weapon), a same-itemData instance may already be equipped. Drop
            // the existing one into the world — same flow as the enemy-death drop —
            // so the incoming physical object can replace it instead of being silently
            // ignored by downstream same-item early-outs. UnEquipItem is called with
            // disableItem:false so the dropped object stays active in the world.
            if (itemObject != null && EquippedItem == itemData)
            {
                DropItem(itemData, destroyItem: false, unEquipAfterDrop: false);
                UnEquipItem(playUnEquipAnimation: false, disableItem: false);
                yield return new WaitUntil(() => !IsChangingItem);
            }

            if (EquipSettings.i.EquipItemsBasedOnSlot && !dropItem)
            {
                var currentItemWithSameSlot = equippableItems.FirstOrDefault(i => i.equipmentSlotIndex == itemData.equipmentSlotIndex);
                if (currentItemWithSameSlot != null && itemData != currentItemWithSameSlot)
                {
                    if (EquippedItem != currentItemWithSameSlot)
                    {
                        yield return Equip(currentItemWithSameSlot, false, dropItem: true);
                    }
                    // unEquipAfterDrop:false — DropItem normally schedules an UnEquipItem one
                    // frame later, which can fire AFTER this outer Equip already finished
                    // (when playEquipAnimation=false) and clobber EquippedItem back to null.
                    // We perform the unequip ourselves below.
                    DropItem(currentItemWithSameSlot, false, unEquipAfterDrop: false);
                    // autoEquipDefault:false — same reason: the outer flow handles the unequip;
                    // letting DetachItem start a parallel Equip(DefaultItem)/UnEquipItem races
                    // with this flow.
                    itemAttacher.DetachItem(currentItemWithSameSlot, autoEquipDefault: false);
                    yield return new WaitUntil(() => !IsUnEquippingItem);
                }

            }

            // For items that can be attached to the body
            itemAttacher.AttachItem(itemData, disableItemAfterAttach: true);

            if (EquippedItem != null)
            {
                // Unequip the currently equipped item before equipping the new one
                UnEquipItem(playEquipAnimation, onItemDisabled: onPrevItemDisabled);

                // Wait until the item is completely unequipped before proceeding
                yield return new WaitUntil(() => !IsChangingItem);
            }

            // Exit if an interruption occurs during item switching
            if (InterruptItemSwitching)
                yield break;

            IsEquippingItem = true;

            // Attach the new item to the character (returns whether the attachment was successful)
            var hasItem = SetItem(itemData, itemObject);

            bool weaponEnabled = false;
            // Create an action to enable the item at the correct animation timing
            var itemEnableAction = new ActionData()
            {
                normalizeTime = itemData.itemEnableTime,
                action = () =>
                {
                    EnableItem(itemData, playEquipAnimation);
                    onItemEnabled?.Invoke();
                    weaponEnabled = true;
                }
            };

            EquippedItem = itemData;
            OnEquip?.Invoke(itemData);

            // If an equip animation exists, play it and trigger the enable action at the right moment
            if (playEquipAnimation && itemData.equipClip.clip != null)
                yield return animGraph.CrossfadeAsync(null, itemData.equipClip, mask: Mask.Arm, actions: itemEnableAction);
            else
                itemEnableAction.action?.Invoke(); // Immediately enable the item if no animation is played

            if (!weaponEnabled)
            {
                itemEnableAction.action?.Invoke();
            }

            if (itemData.overrideIdleTurns)
                locomotion.OverrideTurns(itemData.idleTurns);

            if (itemData.overrideController)
                yield return new WaitUntil(() => !animGraph.IsUpdatingOverrideController);

            OnEquipComplete?.Invoke(itemData);
            IsEquippingItem = false;
        }
        bool SetItem(EquippableItem equippableItemData, EquippableItemObject itemObject = null)
        {
            var hasItem = false;

            if (equippableItemData.isDualItem)
            {
                hasItem = SpawnItem(equippableItemData, RightHandHolder, equippableItemData.LocalPositionR(CharacterId, Animator.avatar), equippableItemData.LocalRotationR(CharacterId, Animator.avatar), HumanBodyBones.RightHand, itemObject);
                hasItem = SpawnItem(equippableItemData, LeftHandHolder, equippableItemData.LocalPositionL(CharacterId, Animator.avatar), equippableItemData.LocalRotationL(CharacterId, Animator.avatar), HumanBodyBones.LeftHand, itemObject);
            }
            else if (equippableItemData.holderBone == HumanBodyBones.RightHand)
            {
                hasItem = SpawnItem(equippableItemData, RightHandHolder, equippableItemData.LocalPositionR(CharacterId, Animator.avatar), equippableItemData.LocalRotationR(CharacterId, Animator.avatar), HumanBodyBones.RightHand, itemObject);
            }
            else if (equippableItemData.holderBone == HumanBodyBones.LeftHand)
            {
                hasItem = SpawnItem(equippableItemData, LeftHandHolder, equippableItemData.LocalPositionL(CharacterId, Animator.avatar), equippableItemData.LocalRotationL(CharacterId, Animator.avatar), HumanBodyBones.LeftHand, itemObject);
            }

            if (hasItem)
                animGraph.PlayLoopingAnimation(equippableItemData.itemEquippedIdleClip, mask: equippableItemData.itemEquippedIdleClipMask);
            else
                Debug.LogWarning("Cannot equip item because the ItemObject is null.");


            return hasItem;
        }
        bool SpawnItem(EquippableItem itemData, EquippableItemHolder itemHolder, Vector3 localPosition, Vector3 localRotation, HumanBodyBones bodyBone, EquippableItemObject itemObject = null)
        {
            GameObject weaponObject = null;
            if (itemObject != null)
            {
                weaponObject = itemObject.gameObject;
                weaponObject.transform.parent = itemHolder.transform;
                weaponObject.transform.localPosition = localPosition;
                weaponObject.transform.localRotation = Quaternion.Euler(localRotation);
                weaponObject.SetActive(false);
            }

            EquippableItemObject currentItem = weaponObject?.GetComponent<EquippableItemObject>();

            if (currentItem == null && Animator.isHuman)
            {
                var items = Animator.GetBoneTransform(bodyBone).GetComponentsInChildren<EquippableItemObject>(true);
                currentItem = items.FirstOrDefault(i => i.itemData == itemData && i.GetComponentInParent<EquippableItemHolder>() == itemHolder);
            }

            if (currentItem == null)
            {
                if (itemData.modelPrefab != null)
                {
                    weaponObject = Instantiate(itemData.modelPrefab, itemHolder.transform);
                    weaponObject.transform.localPosition = localPosition;
                    weaponObject.transform.localRotation = Quaternion.Euler(localRotation);
                    weaponObject.SetActive(false);
                }
                else
                {
                    // For Hand Combat
                    weaponObject = boneMapper.GetBone(BoneMapper.HumanBodyBoneToBoneType(itemData.holderBone))?.transform.Find(itemData.holderBone.ToString() + "Collider")?.gameObject;
                }

                if (weaponObject == null) return false;

                currentItem = weaponObject.GetComponent<EquippableItemObject>();
                currentItem.itemData = itemData;
            }

            // Always refresh the cached local pose to match the holder we just attached to.
            // When the item is reused from another character (e.g. picked up from an enemy),
            // the stored defaults belong to that character's hand and would otherwise cause
            // ResetWeaponTransform to snap the item to the wrong pose on the new owner.
            currentItem.defaultLocalPos = currentItem.transform.localPosition;
            currentItem.defaultLocalRot = currentItem.transform.localRotation;

            AddItem(currentItem.itemData);

            itemHolder.currentItem = currentItem;

            return true;
        }
        void EnableItem(EquippableItem itemData, bool playEquipAnimation)
        {
            if (itemData.isDualItem)
            {
                EquippedItemRight?.gameObject?.SetActive(true);
                EquippedItemLeft?.gameObject?.SetActive(true);
            }
            else
            {
                EquippedItemObject?.gameObject?.SetActive(true);
            }
            if (itemData.overrideController)
                animGraph.UpdateOverrideController(itemData.overrideController, playEquipAnimation);

            itemAttacher.HideBodyAttachment(itemData);
        }

        #endregion

        #region UnEquip

        public void UnEquipItem(bool playUnEquipAnimation = true, bool disableItem = true, Action onItemDisabled = null)
        {
            StartCoroutine(UnEquipItemAsync(playUnEquipAnimation, disableItem, onItemDisabled));
        }

        public IEnumerator UnEquipItemAsync(bool playUnEquipAnimation = true, bool disableItem = true, Action onItemDisabled = null)
        {
            // Exit if there is no currently equipped item, an item switch is already in progress, 
            // or if unequipping is prevented
            if (EquippedItem == null || IsChangingItem || PreventItemUnEquip)
                yield break;

            // Store the currently equipped item reference before unequipping
            var itemData = EquippedItem;

            // Mark that an item is currently being unequipped
            IsUnEquippingItem = true;
            bool weaponDisabled = false;
            // Define an action that disables the weapon once the unequip animation reaches a certain point
            var weaponDisableAction = new ActionData()
            {
                normalizeTime = EquippedItem.itemDisableTime,
                action = () =>
                {
                    // Invoke the completion callback for unequipping
                    OnBeforeItemDisable?.Invoke(itemData);

                    // Disable the currently equipped weapon
                    DisableCurrentItem(disableItem);
                    onItemDisabled?.Invoke();

                    // Clear the currently equipped item reference
                    EquippedItem = null;

                    // Remove item references from both hand handlers
                    RightHandHolder.currentItem = null;
                    LeftHandHolder.currentItem = null;

                    // Stop any looping animations that were playing
                    animGraph.StopLoopingAnimations(true);

                    // Reset the animation override controller
                    animGraph.UpdateOverrideController(null, playUnEquipAnimation);
                    weaponDisabled = true;

                    if(disableItem)
                        itemAttacher.ShowBodyAttachement(itemData);
                }
            };

            OnUnEquip?.Invoke();

            if (playUnEquipAnimation && itemData.unEquipClip.clip != null)
            {
                yield return animGraph.CrossfadeAsync(null, itemData.unEquipClip, mask: Mask.Arm, actions: weaponDisableAction);
            }
            else
            {
                //// If no animation is played, immediately invoke the disable action
                //yield return null;      // Unity seems to crash if we do not wait for a frame
                //yield return null;      // Unity seems to crash if we do not wait for a frame 
                weaponDisableAction.action?.Invoke();
            }
            if (!weaponDisabled)
            {
                weaponDisableAction.action?.Invoke();
            }

            yield return new WaitUntil(() => !animGraph.IsUpdatingOverrideController);

            OnUnEquipComplete?.Invoke();

            IsUnEquippingItem = false;

        }
        void DisableCurrentItem(bool disableItem = true)
        {
            if (EquippedItem.isDualItem)
            {
                if (disableItem)
                {
                    EquippedItemRight?.gameObject.SetActive(false);
                    EquippedItemLeft?.gameObject.SetActive(false);
                }
            }
            else
            {
                if (disableItem)
                {
                    EquippedItemObject?.gameObject.SetActive(false);
                }
            }
        }

        /// <summary>
        /// Drop the specified equippable item from the equippable items list.
        /// </summary>
        /// <param name="item">The equippable item to be removed.</param>
        /// <param name="destroyItem">
        /// If true, the item will be destroyed. 
        /// If false, the item will remain but physics will be enabled, causing it to fall by gravity.
        public void DropItem(EquippableItem item = null, bool destroyItem = true, bool canEquip = true, bool unEquipAfterDrop = true)
        {
            if (item == null)
            {
                item = EquippedItem;
                if (item == null) return;
            }

            if (EquippedItem == item)
            {
                if (equippableItems.Contains(item))
                {
                    equippableItems.Remove(item);
                }
                if (!destroyItem && canEquip)
                {
                    var equipHotspot = Instantiate(EquipSettings.i.EquipHotspot, EquippedItemObject.transform);
                    equipHotspot.transform.localRotation = Quaternion.identity;
                    equipHotspot.transform.localPosition = Vector3.zero;
                    equipHotspot.item = EquippedItemObject.itemData;
                    equipHotspot.itemObject = EquippedItemObject;

                    //var hotspot = EquippedItemObject.GetComponent<EquipHotspot>();
                    //hotspot.gameObject.layer = LayerMask.NameToLayer("Hotspot");
                    //hotspot.item = EquippedItemObject.itemData;
                    //hotspot.enabled = true;
                }


                if (unEquipAfterDrop)
                {
                    StartCoroutine(AsyncUtil.RunAfterFrames(1, () =>
                    {
                        // Pass true — DisableCurrentItem receives !disableItem, so !true = false
                        // = don't disable. The dropped object stays active in the world.
                        UnEquipItem(false, false);
                    }));
                }
                if (item.isDualItem)
                {
                    EquippedItemLeft.transform.parent = null;
                    EquippedItemRight.transform.parent = null;
                    if (destroyItem)
                    {
                        Destroy(EquippedItemRight.gameObject);
                        Destroy(EquippedItemLeft.gameObject);
                    }
                    else
                    {
                        EquippedItemRight.HandlePhysics(true);
                        EquippedItemLeft.HandlePhysics(true);
                    }
                }
                else
                {
                    EquippedItemObject.transform.parent = null;

                    if (destroyItem)
                    {
                        Destroy(EquippedItemObject.gameObject);
                    }
                    else
                    {
                        EquippedItemObject.HandlePhysics(true);
                    }
                }
                OnItemBecameUnusable?.Invoke(item);
            }
        }

        /// <summary>
        /// Drops every item the character holds — the equipped one plus anything placed under an
        /// EquippableItemHolder on the body — into the world as a proper, re-equippable object:
        /// physics enabled and an EquipHotspot attached so it can be picked up again. Used e.g. when
        /// an AI dies and should shed all the weapons it carries.
        /// </summary>
        public void DropAllItems()
        {
            foreach (var holder in GetComponentsInChildren<EquippableItemHolder>())
            {
                var itemObject = holder.currentItem;

                // Only drop real, droppable weapon objects — those whose item has a model prefab.
                // Items without one (e.g. natural/built-in weapons) are left in place.
                if (itemObject == null || itemObject.itemData == null || itemObject.itemData.modelPrefab == null)
                    continue;

                DropItemObject(itemObject);
                holder.currentItem = null;
            }

            EquippedItem = null;

            DropAttachedBodyItems();
        }

        /// <summary>
        /// Drops weapons that are only attached on the body (sheathed/holstered, not equipped) and so
        /// have no real EquippableItemObject — only ItemAttacher's visual model. A fresh, droppable
        /// object is spawned from the item's model prefab at the attachment's pose, then the visual
        /// attachment is removed. The hidden body model of an already-equipped weapon (dropped above
        /// from its holder) and items without a model prefab (skinned armor / natural items) are skipped.
        /// </summary>
        void DropAttachedBodyItems()
        {
            if (itemAttacher == null) return;

            foreach (var attached in itemAttacher.AttachedItems.Values.ToList())
            {
                if (attached.isHidden) continue;
                if (!(attached.item is EquippableItem equippable) || equippable.modelPrefab == null) continue;

                var model = attached.itemModel;
                var pos = model != null ? model.transform.position : transform.position;
                var rot = model != null ? model.transform.rotation : transform.rotation;

                var dropObject = Instantiate(equippable.modelPrefab, pos, rot).GetComponent<EquippableItemObject>();
                if (dropObject != null)
                {
                    dropObject.itemData = equippable;
                    DropItemObject(dropObject);
                }

                // Remove the now-redundant visual attachment from the body.
                itemAttacher.DetachItem(attached.item, enableDefaultItems: false, autoEquipDefault: false);
            }
        }

        // Releases a single equippable object into the world and makes it pickup-able, mirroring the
        // drop branch of DropItem (physics on + an EquipHotspot referencing the item).
        void DropItemObject(EquippableItemObject itemObject)
        {
            itemObject.transform.parent = null;
            itemObject.HandlePhysics(true);

            if (itemObject.itemData == null || EquipSettings.i?.EquipHotspot == null) return;

            var equipHotspot = Instantiate(EquipSettings.i.EquipHotspot, itemObject.transform);
            equipHotspot.transform.localRotation = Quaternion.identity;
            equipHotspot.transform.localPosition = Vector3.zero;
            equipHotspot.item = itemObject.itemData;
            equipHotspot.itemObject = itemObject;
        }

        #endregion

        #region Utilities

        public void ResumeIdleAnimation()
        {
            if (IsCurrentItemUnusable)
            {
                PlayIdleAnimation();
                IsCurrentItemUnusable = false;
                OnItemBecameUnrestricted?.Invoke(EquippedItem);
            }
        }

        public void StopIdleAnimation()
        {
            animGraph.StopLoopingAnimations(true);
            IsCurrentItemUnusable = true;
            OnItemBecameRestricted?.Invoke(EquippedItem);
        }

        public void PlayIdleAnimation()
        {
            animGraph.PlayLoopingAnimation(EquippedItem.itemEquippedIdleClip, mask: EquippedItem.itemEquippedIdleClipMask);
        }

        #endregion
    }



    public class CustomAction
    {
        public Action action = null;
    }
}