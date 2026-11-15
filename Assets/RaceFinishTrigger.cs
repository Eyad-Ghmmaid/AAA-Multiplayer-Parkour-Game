using AvocadoShark;
using Fusion;
using UnityEngine;

/// <summary>
/// Put this on a map-specific BoxCollider at the finish line. Each player's
/// owning client requests a finish; the Fusion State Authority validates its position.
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

    /// <summary>Allows a small replication delay while still requiring the player to be at the finish.</summary>
    public bool ContainsPosition(Vector3 worldPosition, float tolerance = 0.5f)
    {
        var box = GetComponent<BoxCollider>();
        if (box == null || !box.enabled || !gameObject.activeInHierarchy)
            return false;

        var closestPoint = box.ClosestPoint(worldPosition);
        tolerance = Mathf.Max(0f, tolerance);
        return (closestPoint - worldPosition).sqrMagnitude <= tolerance * tolerance;
    }

    private void OnTriggerEnter(Collider other)
    {
        NotifyRaceManager(other);
    }

    private void OnTriggerStay(Collider other)
    {
        NotifyRaceManager(other);
    }

    private void NotifyRaceManager(Collider other)
    {
        var player = other.GetComponentInParent<NetworkObject>();
        if (player == null)
            return;

        var raceManager = FindAnyObjectByType<InGame_Manager>();
        if (raceManager == null)
            return;

        if (raceManager.IsHostAuthoritativeSession)
        {
            // The owning client can stop its local timer immediately; only the Host
            // accepts the finish after checking its authoritative player position.
            if (player.HasInputAuthority)
                raceManager.FinishLocalRace();
            if (raceManager.HasStateAuthority && player.HasStateAuthority)
                raceManager.ReportAuthoritativeFinish(player);
            return;
        }

        if (player.HasStateAuthority)
            raceManager.FinishLocalRace();
    }
}
