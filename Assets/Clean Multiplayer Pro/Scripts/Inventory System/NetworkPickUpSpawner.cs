#if CMPSETUP_COMPLETE
using System.Collections.Generic;
using Fusion;
using UnityEngine;

public class NetworkPickUpSpawner : NetworkBehaviour
{
    public static NetworkPickUpSpawner Instance { get; private set; }

    [SerializeField] private List<InventoryItem> allInventoryItems = new List<InventoryItem>();
    private readonly Dictionary<int, InventoryItem> _playerInventory = new Dictionary<int, InventoryItem>();

    private void Awake()
    {
        Instance = this;
        SetPlayerInventory();
    }

    private void SetPlayerInventory()
    {
        foreach (var item in allInventoryItems)
        {
            _playerInventory.Add(item.id, item);
        }
    }

    [Rpc]
    public void SpawnPickUpsRPC(int id, Vector3 position, PlayerRef requester)
    {
        if (!Runner.IsSharedModeMasterClient) //Master Client Should Spawn all Pickups
            return;
        if (!_playerInventory.TryGetValue(id, out var inventoryItem))
        {
            Debug.LogError($"Inventory with an id : {id} Item not found.");
            return;
        }

        Runner.Spawn(inventoryItem.prefab, position, Quaternion.identity);
        // Confirm to the dropper only now that the pickup actually exists in the world -
        // removing the item optimistically would destroy it if the spawn never happened.
        ConfirmDropRPC(requester, id);
    }

    // Only the spawner's state authority (the master client, which performed the spawn
    // just above) may confirm a drop.
    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void ConfirmDropRPC([RpcTarget] PlayerRef requester, int id)
    {
        if (_playerInventory.TryGetValue(id, out var item))
            Inventory.RemoveItemFromInventory?.Invoke(item);
    }

    /// <summary>
    /// Adds the picked-up item to the winning player's inventory (see ItemPickup).
    /// State authority only - otherwise any client could grant itself unlimited items and
    /// bypass the claim arbitration entirely.
    /// </summary>
    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    public void GrantPickUpRPC([RpcTarget] PlayerRef winner, int itemId)
    {
        if (_playerInventory.TryGetValue(itemId, out var item))
            Inventory.AddItemToInventory?.Invoke(item);
    }
}
#endif