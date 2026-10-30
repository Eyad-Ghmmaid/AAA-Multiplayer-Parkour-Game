using Fusion;
using UnityEngine;
using FS_Core;
using FS_LocomotionSystem;

public class FSNetworkAuthoritySetup : NetworkBehaviour
{
    [SerializeField] private PlayerController playerController;
    [SerializeField] private LocomotionInputManager inputManager;
    [SerializeField] private GameObject localCameraRig;

    public override void Spawned()
    {
        bool isLocalOwner = Object.HasStateAuthority;

        if (playerController != null)
            playerController.enabled = isLocalOwner;

        if (inputManager != null)
            inputManager.enabled = isLocalOwner;

        if (localCameraRig != null)
            localCameraRig.SetActive(isLocalOwner);
    }
}