using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace FS_Core
{
    public class ItemEquipAndAttachmentEditor : EditorWindow
    {
        static ItemEquipAndAttachmentEditor window;
        public static ItemEquipper itemEquipper { get; private set; }
        static ItemAttacher itemAttacher;

        static Color blue = new Color(.2f, 0.3f, 0.5f);
        static bool saveAsDefault = true;
        List<Item> attachedItems = new List<Item>();

        EquippableItem CurrentEquippedItem 
    => itemEquipper?.EquippedItem?.modelPrefab != null 
       ? itemEquipper.EquippedItem 
       : null;

        public static void SetItemEquipper(ItemEquipper itemEquippper)
        {
            if (!ReferenceEquals(itemEquipper, itemEquippper))
            {
                if (itemEquippper != null)
                {
                    itemEquipper = itemEquippper;
                    if (itemEquipper != null)
                    {
                        itemAttacher = itemEquipper.GetComponent<ItemAttacher>();
                        EditorGUIUtility.PingObject(itemEquipper.gameObject);
                    }
                }
            }
        }

        [MenuItem("Tools/FS System/FS Tools/Item Equip And Attachment Setup")]
        public static void ShowWindow()
        {
            window = GetWindow<ItemEquipAndAttachmentEditor>("Weapon");
            window.minSize = new Vector2(360, 350); // better default min size
            window.maxSize = new Vector2(360, 350); // better default min size
        }

        private void OnGUI()
        {
            if (itemEquipper == null)
            {
                if (Selection.activeGameObject != null)
                    SetItemEquipper(Selection.activeGameObject.GetComponent<ItemEquipper>());

                if (itemEquipper == null)
                {
                    EditorGUILayout.HelpBox("Please select a character", MessageType.Warning);
                    return;
                }
            }
            DrawWeaponHoldingSaveSection();
        }


        private void DrawWeaponHoldingSaveSection()
        {
            GUILayout.Space(15);

            GetAttachedItems();
            EditorGUILayout.BeginVertical("box");
            if (CurrentEquippedItem != null || attachedItems.Count > 0)
                saveAsDefault = EditorGUILayout.Toggle("Save as Default", saveAsDefault);
            EditorGUILayout.EndVertical();

            GUILayout.Space(5);

            EditorGUILayout.BeginVertical("box");
            EditorGUILayout.LabelField("Equip", EditorStyles.boldLabel);
            if (CurrentEquippedItem != null)
            {
                GUI.backgroundColor = blue;
                if (GUILayout.Button("Save Weapon Holding Data", GUILayout.Height(25)))
                    SaveWeaponHoldingData();
            }
            else
            {
                if (Application.isPlaying)
                    EditorGUILayout.HelpBox("No item equipped. Please equip a item first.", MessageType.Warning);
                else
                    EditorGUILayout.HelpBox("Enter Play Mode and equip a item to set up IK.", MessageType.Info);
            }
            GUI.backgroundColor = Color.white;
            EditorGUILayout.EndVertical();


            GUILayout.Space(5);


            EditorGUILayout.BeginVertical("box");
            EditorGUILayout.LabelField("Attach", EditorStyles.boldLabel);
            
            if (attachedItems.Count > 0)
            {
                GUI.backgroundColor = blue;
                if (GUILayout.Button("Save Weapon Attachment Data", GUILayout.Height(25)))
                    SaveWeaponAttachmentData();
            }
            else
            {
                if (Application.isPlaying)
                    EditorGUILayout.HelpBox("No item attched. Please attach a item first.", MessageType.Warning);
                else
                    EditorGUILayout.HelpBox("Enter Play Mode and attach a item to set up attachment data.", MessageType.Info);
            }

            GUI.backgroundColor = Color.white;
            EditorGUILayout.EndVertical();
        }

        private void GetAttachedItems()
        {
            if (itemAttacher != null && itemAttacher.AttachedItems.Count > 0)
            {
                attachedItems = itemAttacher.AttachedItems.Values
                                    .Where(item => item.item.modelPrefab != null && !item.isHidden)
                                    .ToList().Select(i => i.item).ToList();
            }
            else
            {
                attachedItems = new List<Item>();
            }
        }
        private void SaveWeaponAttachmentData()
        {
            foreach (var item in attachedItems)
            {
                Undo.RecordObject(item, "Attach configuration modified");
                AttachedItem currentAttachedItem = itemAttacher.AttachedItems.Values.ToList().Find(a => a.item == item);
                if (currentAttachedItem != null)
                {
                    if (saveAsDefault)
                    {
                        if (currentAttachedItem.parentObject != null)
                        {
                            item.holderLocalPosition = currentAttachedItem.parentObject.transform.localPosition;
                            item.holderLocalRotation = currentAttachedItem.parentObject.transform.localEulerAngles;
                        }
                        if (currentAttachedItem.itemModel != null)
                        {
                            item.itemLocalPosition = currentAttachedItem.itemModel.transform.localPosition;
                            item.itemLocalRotation = currentAttachedItem.itemModel.transform.localEulerAngles;
                        }
                    }
                    else
                    {
                        if (currentAttachedItem.parentObject != null)
                        {
                            ItemAttachReference existingHolderReference = item.FindAttachedHolderReferenceData(itemEquipper.CharacterId, itemEquipper.Animator.avatar);

                            if (existingHolderReference != null)
                            {
                                existingHolderReference.localPosition = currentAttachedItem.parentObject.transform.localPosition;
                                existingHolderReference.localRotation = currentAttachedItem.parentObject.transform.localEulerAngles;
                            }
                            else
                            {
                                var newAttachReference = new ItemAttachReference()
                                {
                                    characterReference = new CharacterReference
                                    {
                                        characterId =itemEquipper.CharacterId.id,
                                        avatar = itemEquipper.Animator.avatar
                                    },
                                    localPosition = currentAttachedItem.parentObject.transform.localPosition,
                                    localRotation = currentAttachedItem.parentObject.transform.localEulerAngles
                                };
                                item.holderAttachReferences.Add(newAttachReference);
                            }
                        }
                        if (currentAttachedItem.itemModel != null)
                        {
                            ItemAttachReference existingItemReference = item.FindAttachedItemReferenceData(itemEquipper.CharacterId, itemEquipper.Animator.avatar);
                            if (existingItemReference != null)
                            {
                                existingItemReference.localPosition = currentAttachedItem.itemModel.transform.localPosition;
                                existingItemReference.localRotation = currentAttachedItem.itemModel.transform.localEulerAngles;
                            }
                            else
                            {
                                var newAttachReference = new ItemAttachReference()
                                {
                                    characterReference = new CharacterReference
                                    {
                                        characterId =itemEquipper.CharacterId?.id,
                                        avatar = itemEquipper.Animator.avatar
                                    },
                                    localPosition = currentAttachedItem.itemModel.transform.localPosition,
                                    localRotation = currentAttachedItem.itemModel.transform.localEulerAngles
                                };
                                item.itemAttachReferences.Add(newAttachReference);
                            }
                        }
                    }
                    AssetDatabase.SaveAssets();
                    AssetDatabase.Refresh();
                    EditorUtility.SetDirty(item);
                }
            }
            Debug.Log("Weapon attachment data saved successfully for " + string.Join(", ", attachedItems.Select(i => i.name)));
        }
        void SaveWeaponHoldingData()
        {
            Undo.RecordObject(itemEquipper.EquippedItem, "Equip configuration modified");
            var weapon = itemEquipper.EquippedItem;
            if (weapon.isDualItem)
            {
                weapon.localPositionR = itemEquipper.EquippedItemRight.transform.localPosition;
                weapon.localRotationR = itemEquipper.EquippedItemRight.transform.localEulerAngles;
                weapon.localPositionL = itemEquipper.EquippedItemLeft.transform.localPosition;
                weapon.localRotationL = itemEquipper.EquippedItemLeft.transform.localEulerAngles;

                itemEquipper.EquippedItemLeft.defaultLocalPos = itemEquipper.EquippedItemLeft.transform.localPosition;
                itemEquipper.EquippedItemLeft.defaultLocalRot = itemEquipper.EquippedItemLeft.transform.localRotation;
                itemEquipper.EquippedItemRight.defaultLocalPos = itemEquipper.EquippedItemRight.transform.localPosition;
                itemEquipper.EquippedItemRight.defaultLocalRot = itemEquipper.EquippedItemRight.transform.localRotation;
            }
            else if (weapon.holderBone == HumanBodyBones.RightHand)
            {
                if (saveAsDefault)
                {
                    weapon.localPositionR = itemEquipper.EquippedItemRight.transform.localPosition;
                    weapon.localRotationR = itemEquipper.EquippedItemRight.transform.localEulerAngles;
                }
                else
                {
                    ItemAttachReference existingReference = weapon.FindItemEquipReferenceData(itemEquipper.CharacterId, itemEquipper.Animator.avatar);
                    if (existingReference != null)
                    {
                        existingReference.localPosition = itemEquipper.EquippedItemRight.transform.localPosition;
                        existingReference.localRotation = itemEquipper.EquippedItemRight.transform.localEulerAngles;
                    }
                    else
                    {
                        var newAttachReference = new ItemAttachReference()
                        {
                            characterReference = new CharacterReference
                            {
                                characterId =itemEquipper.CharacterId.id,
                                avatar = itemEquipper.Animator.avatar
                            },
                            localPosition = itemEquipper.EquippedItemObject.transform.localPosition,
                            localRotation = itemEquipper.EquippedItemObject.transform.localEulerAngles
                        };

                        itemEquipper.EquippedItem.itemEquipReferences.Add(newAttachReference);

                    }
                }
                itemEquipper.EquippedItemObject.defaultLocalPos = itemEquipper.EquippedItemObject.transform.localPosition;
                itemEquipper.EquippedItemObject.defaultLocalRot = itemEquipper.EquippedItemObject.transform.localRotation;
            }
            else if (weapon.holderBone == HumanBodyBones.LeftHand)
            {
                if (saveAsDefault)
                {
                    weapon.localPositionL = itemEquipper.EquippedItemObject.transform.localPosition;
                    weapon.localRotationL = itemEquipper.EquippedItemObject.transform.localEulerAngles;
                }
                else
                {
                    ItemAttachReference existingReference = weapon.FindItemEquipReferenceData(itemEquipper.CharacterId, itemEquipper.Animator.avatar);
                    if (existingReference != null)
                    {
                        existingReference.localPosition = itemEquipper.EquippedItemObject.transform.localPosition;
                        existingReference.localRotation = itemEquipper.EquippedItemObject.transform.localEulerAngles;
                    }
                    else
                    {
                        var newAttachReference = new ItemAttachReference()
                        {
                            characterReference = new CharacterReference
                            {
                                characterId =itemEquipper.CharacterId.id,
                                avatar = itemEquipper.Animator.avatar
                            },
                            localPosition = itemEquipper.EquippedItemObject.transform.localPosition,
                            localRotation = itemEquipper.EquippedItemObject.transform.localEulerAngles
                        };

                        itemEquipper.EquippedItem.itemEquipReferences.Add(newAttachReference);

                    }
                }
                itemEquipper.EquippedItemObject.defaultLocalPos = itemEquipper.EquippedItemObject.transform.localPosition;
                itemEquipper.EquippedItemObject.defaultLocalRot = itemEquipper.EquippedItemObject.transform.localRotation;
            }
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            EditorUtility.SetDirty(weapon);
            Debug.Log("Weapon holding data saved successfully for " + itemEquipper.EquippedItem.name);
        }

        private void OnEnable()
        {
            Selection.selectionChanged += () =>
            {
                if (Selection.activeGameObject != null)
                    SetItemEquipper(Selection.activeGameObject.GetComponent<ItemEquipper>());
                if (window != null)
                {
                    window.Repaint();
                }
            };
        }

        private void OnDisable()
        {
            itemEquipper = null;
        }

    }
}
