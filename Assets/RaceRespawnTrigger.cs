using AvocadoShark;
using Fusion;
using UnityEngine;

/// <summary>Map-specific fall volume that sends the player who entered it to a respawn point.</summary>
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
        if (raceManager == null)
            return;

        if (raceManager.IsHostAuthoritativeSession)
            raceManager.RequestAuthoritativeRespawn(player);
        else
            raceManager.RequestLocalRespawn();
    }
}
