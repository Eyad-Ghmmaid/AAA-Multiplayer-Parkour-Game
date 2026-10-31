using AvocadoShark;
using Fusion;
using UnityEngine;

/// <summary>
/// Put this on a map-specific BoxCollider at the finish line. Each player's
/// owning client stops only that player's timer when they enter the volume.
/// </summary>
[RequireComponent(typeof(BoxCollider))]
public sealed class RaceFinishTrigger : MonoBehaviour
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
            raceManager.FinishLocalRace();
    }
}
