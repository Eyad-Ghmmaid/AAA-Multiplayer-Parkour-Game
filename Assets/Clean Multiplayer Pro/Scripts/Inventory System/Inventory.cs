#if CMPSETUP_COMPLETE
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
public class Inventory : MonoBehaviour
{
    public static Action<InventoryItem> AddItemToInventory;
    public static Action<InventoryItem> RemoveItemFromInventory;
    /// <summary>UI should re-read quantities (pending drop state changed).</summary>
    public static Action UIRefreshRequested;
    /// <summary>A drop request timed out without confirmation from the master client.</summary>
    public static Action DropFailed;
    [SerializeField] private List<InventoryItem> inventoryItems = new List<InventoryItem>();
    [SerializeField] private NetworkPickUpSpawner networkPickUpSpawner;
    private readonly Dictionary<int, InventoryItem> _inventoryItemsDict = new Dictionary<int, InventoryItem>();
    private Dictionary<int, int> _inventory = new Dictionary<int, int>();
    // Drop requests in flight (sent to the master client, not yet confirmed). Counted
    // against the held quantity everywhere so the same item can't be dropped twice and a
    // quit mid-flight can't persist an item whose pickup already exists in the world.
    private readonly Dictionary<int, int> _pendingDrops = new Dictionary<int, int>();
    private const string Key = "InventoryData";
    private const float DropConfirmTimeout = 4f;

    private void Start()
    {
        Init();
    }

    private void OnEnable()
    {
        AddItemToInventory += AddItem;   
        RemoveItemFromInventory += RemoveItem; 
    }

    private void OnDisable()
    {
        AddItemToInventory -= AddItem;   
        RemoveItemFromInventory -= RemoveItem;
    }

    private void OnDestroy()
    {
        SaveInventoryData();
    }

    private void Init()
    {
        LoadInventoryData();
        foreach (var item in inventoryItems)
        {
            _inventoryItemsDict.TryAdd(item.id, item);
        }

    }
    private void AddItem(InventoryItem inventoryItem)
    {
        if (!_inventory.TryAdd(inventoryItem.id, 1))
        {
            _inventory[inventoryItem.id]++;
        }
    }

    private void RemoveItem(InventoryItem inventoryItem)
    {
        // The only removal path is the master's drop confirmation - clear the matching
        // pending marker before adjusting the held count.
        if (_pendingDrops.TryGetValue(inventoryItem.id, out var pending))
        {
            if (pending <= 1)
                _pendingDrops.Remove(inventoryItem.id);
            else
                _pendingDrops[inventoryItem.id] = pending - 1;
        }

        if (!_inventory.ContainsKey(inventoryItem.id))
        {
            Debug.Log($"Item cannot be removed because Item with Id {inventoryItem.id} doesn't exist");
            return;
        }

        _inventory[inventoryItem.id]--;

        if (_inventory[inventoryItem.id] <= 0)
        { 
            _inventory.Remove(inventoryItem.id);
        }
    }
    public Dictionary<InventoryItem, int> GetAllInventoryItems()
    {
        return EffectiveQuantities().ToDictionary(item => _inventoryItemsDict[item.Key], item => item.Value);
    }

    /// <summary>Held quantities minus in-flight drop requests, entries &lt;= 0 omitted.</summary>
    private Dictionary<int, int> EffectiveQuantities()
    {
        var result = new Dictionary<int, int>();
        foreach (var item in _inventory)
        {
            var pending = _pendingDrops.TryGetValue(item.Key, out var p) ? p : 0;
            var effective = item.Value - pending;
            if (effective > 0)
                result.Add(item.Key, effective);
        }
        return result;
    }

    /// <summary>
    /// Requests a pickup spawn from the master client. Returns false when no droppable
    /// quantity remains (all held copies already have drop requests in flight).
    /// </summary>
    public bool TrySpawnNetworkPickUp(int id, Vector3 position)
    {
        var effective = EffectiveQuantities();
        if (!effective.ContainsKey(id))
            return false;
        _pendingDrops[id] = (_pendingDrops.TryGetValue(id, out var p) ? p : 0) + 1;
        networkPickUpSpawner.SpawnPickUpsRPC(id, position, networkPickUpSpawner.Runner.LocalPlayer);
        StartCoroutine(DropTimeout(id));
        UIRefreshRequested?.Invoke();
        return true;
    }

    private IEnumerator DropTimeout(int id)
    {
        yield return new WaitForSeconds(DropConfirmTimeout);
        // Still pending after the timeout: the master left/migrated before confirming.
        // Restore the item to droppable state and tell the player instead of re-sending -
        // a blind retry could duplicate the pickup if the spawn DID happen.
        if (_pendingDrops.TryGetValue(id, out var pending) && pending > 0)
        {
            if (pending <= 1)
                _pendingDrops.Remove(id);
            else
                _pendingDrops[id] = pending - 1;
            UIRefreshRequested?.Invoke();
            DropFailed?.Invoke();
        }
    }

    private void SaveInventoryData()
    {
        // Persist effective quantities: an item whose drop request is in flight must not
        // be saved (its pickup may already exist in the world).
        var json = JsonUtility.ToJson(new InventoryData(EffectiveQuantities()));
        PlayerPrefs.SetString(Key, json);
    }
    
    private void LoadInventoryData()
    {
        if(!PlayerPrefs.HasKey(Key))
            return;
        var json = PlayerPrefs.GetString(Key);
        _inventory = JsonUtility.FromJson<InventoryData>(json);
    }
}

[Serializable]
public class InventoryData
{
    [Serializable]
    public class ItemData
    {
        public int id;
        public int quantity;

        public ItemData(int iKey, int iValue)
        {
            id = iKey;
            quantity = iValue;
        }
    }
    public List<ItemData> inventory = new List<ItemData>();

    public InventoryData(Dictionary<int, int> inventory)
    {
        foreach (var i in inventory)
        {
            this.inventory.Add(new ItemData(i.Key, i.Value));
        }
    }

    public static implicit operator Dictionary<int, int>(InventoryData inventoryData)
    {
        return inventoryData.inventory.ToDictionary(i => i.id, i => i.quantity);
    }
}
#endif