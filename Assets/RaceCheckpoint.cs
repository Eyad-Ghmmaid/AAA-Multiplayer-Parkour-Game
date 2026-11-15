using AvocadoShark;
using Fusion;
using UnityEngine;

/// <summary>A map-specific checkpoint. Place its transform at the safe respawn floor position.</summary>
[RequireComponent(typeof(BoxCollider))]
public sealed class RaceCheckpoint : MonoBehaviour
{
    [SerializeField, Min(1)] private int checkpointIndex = 1;
    [SerializeField] private Transform respawnPoint;

    public int CheckpointIndex => checkpointIndex;
    public Vector3 RespawnPosition => respawnPoint != null ? respawnPoint.position : transform.position;

    /// <summary>Checks the checkpoint against an authoritative world position, with collider scale/rotation applied.</summary>
    public bool ContainsPosition(Vector3 worldPosition, float tolerance = 0.25f)
    {
        var box = GetComponent<BoxCollider>();
        if (box == null || !box.enabled || !gameObject.activeInHierarchy)
            return false;

        var closestPoint = box.ClosestPoint(worldPosition);
        tolerance = Mathf.Max(0f, tolerance);
        return (closestPoint - worldPosition).sqrMagnitude <= tolerance * tolerance;
    }

    private void Reset()
    {
        GetComponent<BoxCollider>().isTrigger = true;
    }

    private void OnValidate()
    {
        checkpointIndex = Mathf.Max(1, checkpointIndex);
        var box = GetComponent<BoxCollider>();
        if (box != null)
            box.isTrigger = true;
    }

    private void OnTriggerEnter(Collider other)
    {
        var player = other.GetComponentInParent<NetworkObject>();
        if (player == null)
            return;

        var raceManager = FindAnyObjectByType<InGame_Manager>();
        if (raceManager == null)
            return;

        if (raceManager.IsHostAuthoritativeSession)
        {
            if (raceManager.HasStateAuthority && player.HasStateAuthority)
                raceManager.ReportAuthoritativeCheckpoint(player, checkpointIndex);
            return;
        }

        if (player.HasStateAuthority)
            raceManager.ReportCheckpoint(checkpointIndex);
    }
}
