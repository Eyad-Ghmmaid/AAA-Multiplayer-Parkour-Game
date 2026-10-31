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
        if (player == null || !player.HasStateAuthority)
            return;

        var raceManager = FindAnyObjectByType<InGame_Manager>();
        if (raceManager != null)
            raceManager.ReportCheckpoint(checkpointIndex);
    }
}
