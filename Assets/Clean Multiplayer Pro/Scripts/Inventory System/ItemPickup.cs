#if CMPSETUP_COMPLETE
using System;
using Fusion;
using UnityEngine;

public class ItemPickup : NetworkBehaviour
{
    const float ClaimTimeout = 2f;

    [SerializeField] private InventoryItem inventoryItem;
    private Rigidbody _rb;
    private bool _isPickedUp;      // local guard against double-triggering on the toucher
    private float _requestTime = -1f;

    // Survives master-client migration, unlike a plain field on the new master's instance.
    [Networked] private NetworkBool ClaimGranted { get; set; }

    private void Awake()
    {
        TryGetComponent(out _rb);
    }

    private void Update()
    {
        // The claim request can be lost (its target state authority left mid-flight).
        // Unlatch after a timeout so the item does not stay permanently untouchable
        // for this player. Update, not FixedUpdateNetwork - the toucher is usually a
        // non-authority proxy of this object and is not simulated in shared mode.
        if (_isPickedUp && _requestTime >= 0 && Time.time - _requestTime > ClaimTimeout)
        {
            _isPickedUp = false;
            _requestTime = -1f;
            if (_rb != null)
                _rb.isKinematic = false;
        }
    }

    public void PickUp(NetworkObject networkObject)
    {
        if(_isPickedUp)
            return;
        if (Object == null || !Object.IsValid || ClaimGranted)
            return;
        if (networkObject.StateAuthority != Runner.LocalPlayer)
            return;

        if (!networkObject.CompareTag("Player"))
            return;

        if (inventoryItem == null)
        {
            Debug.LogError("No inventory item found!");
            return;
        }

        _isPickedUp = true;
        _requestTime = Time.time;
        if (_rb != null)
            _rb.isKinematic = true;
        RequestPickUpRPC(Runner.LocalPlayer);
    }

    // The pickup's state authority arbitrates claims so two players touching the item in
    // the same moment cannot both receive it: only the first request is granted, the item
    // is added to the winner's inventory via a targeted RPC, then the object despawns.
    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    private void RequestPickUpRPC(PlayerRef requester)
    {
        if (ClaimGranted)
            return;
        // If we cannot grant, do NOT despawn - the item would be destroyed and delivered
        // to no one. Leave it claimable for the next toucher instead.
        if (NetworkPickUpSpawner.Instance == null)
            return;
        var requesterStillHere = false;
        foreach (var player in Runner.ActivePlayers)
        {
            if (player == requester)
            {
                requesterStillHere = true;
                break;
            }
        }
        if (!requesterStillHere)
            return;
        ClaimGranted = true;
        NetworkPickUpSpawner.Instance.GrantPickUpRPC(requester, inventoryItem.id);
        Runner.Despawn(Object);
    }
}
#endif