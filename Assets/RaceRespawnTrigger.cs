using AvocadoShark;
using Fusion;
using UnityEngine;

/// <summary>Map-specific fall volume that sends a locally owned player to their last checkpoint.</summary>
[RequireComponent(typeof(BoxCollider))]
public sealed class RaceRespawnTrigger : MonoBehaviour
{
    private void Reset()
    {
        GetComponent<BoxCollider>().isTrigger = true;
    }

    private void OnValidate()
    {
        var box = GetComponent<BoxCollider>();
        if (box != null)
            box.isTrigger = true;
    }

    private void OnTriggerEnter(Collider other)
    {
        var player = other.GetComponentInParent<NetworkObject>();
        if (player == null || !player.HasStateAuthority)
            return;

        var raceManager = FindAnyObjectByType<InGame_Manager>();
        if (raceManager != null)
            raceManager.RequestLocalRespawn();
    }
}
