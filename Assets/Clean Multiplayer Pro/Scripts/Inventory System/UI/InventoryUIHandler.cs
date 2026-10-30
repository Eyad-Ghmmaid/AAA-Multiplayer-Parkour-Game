#if CMPSETUP_COMPLETE
using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class InventoryUIHandler : MonoBehaviour
{
    [SerializeField] private Canvas canvas;
    [SerializeField] private Inventory inventory;
    [SerializeField] private Transform scrollViewContent;
    [SerializeField] private GameObject uiParent;
    [SerializeField] private TextMeshProUGUI inventoryToggleText;
    [SerializeField] private ItemUI itemUIPrefab;

    [SerializeField] private List<GameObject> overlappingUIObjects;

    [SerializeField] private UIPool<ItemUI> _spawnedItemUIObjects;
    [SerializeField] private GameObject errorMessage;
    private bool _isOpen = false;

    private void Awake()
    {
        _spawnedItemUIObjects = new UIPool<ItemUI>(itemUIPrefab,scrollViewContent);
    }
    private bool _uiDirty;

    private void OnEnable()
    {
        Inventory.AddItemToInventory += OnItemAddOrRemove;
        Inventory.RemoveItemFromInventory += OnItemAddOrRemove;
        Inventory.UIRefreshRequested += RefreshIfOpen;
        Inventory.DropFailed += ShowItemDropErrorMessage;
    }

    private void OnDisable()
    {
        Inventory.AddItemToInventory -= OnItemAddOrRemove;
        Inventory.RemoveItemFromInventory -= OnItemAddOrRemove;
        Inventory.UIRefreshRequested -= RefreshIfOpen;
        Inventory.DropFailed -= ShowItemDropErrorMessage;
    }

    private void RefreshIfOpen()
    {
        if (!_isOpen)
            return;
        // Rebuilding while a drag is in progress would return the dragged ItemUI to the
        // pool mid-gesture (orphaned drag icon, rebound item) - defer until the drag ends.
        if (ItemUI.IsDragInProgress)
        {
            _uiDirty = true;
            return;
        }
        _spawnedItemUIObjects.ReturnAllObjects();
        ShowInventoryUI();
    }

    public void NotifyDragEnded()
    {
        if (!_uiDirty)
            return;
        _uiDirty = false;
        RefreshIfOpen();
    }
    public void ToggleInventory()
    {
        _isOpen = !_isOpen;
        if (_isOpen)
            ShowInventoryUI();
        else
            HideInventory();

        SetOverlappingUIObjectsState(!_isOpen);
    }

    private void HideInventory()
    {
        _spawnedItemUIObjects.ReturnAllObjects();
        uiParent.SetActive(false);
        inventoryToggleText.SetText("Inventory");
    }

    private void ShowInventoryUI()
    {
        var allItems = inventory.GetAllInventoryItems();
        foreach (var item in allItems)
        {
            var itemUI = _spawnedItemUIObjects.RentObject();
            itemUI.SetValue(
                new ValueTuple<Canvas, InventoryUIHandler,Inventory, InventoryItem, int>(canvas,this, inventory, item.Key, item.Value));
        }

        uiParent.SetActive(true);
        inventoryToggleText.SetText("Close Inventory");
    }

    private void SetOverlappingUIObjectsState(bool state)
    {
        foreach (var uiObject in overlappingUIObjects)
        {
            uiObject.SetActive(state);
        }
    }

    private void OnItemAddOrRemove(InventoryItem item)
    {
        RefreshIfOpen();
    }

    public void ShowItemDropErrorMessage()
    {
        StartCoroutine(Show());
        return;

        IEnumerator Show()
        {
            errorMessage.SetActive(true);
            yield return new WaitForSeconds(1f);
            errorMessage.SetActive(false);
        }
    }
}
#endif