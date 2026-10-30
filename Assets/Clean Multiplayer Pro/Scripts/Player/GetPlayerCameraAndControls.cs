#if CMPSETUP_COMPLETE
using System;
using System.Collections;
using Cinemachine;
using UnityEngine;
using Fusion;
using StarterAssets;
using UnityEngine.InputSystem;
using Random = UnityEngine.Random;

namespace AvocadoShark
{
    public class GetPlayerCameraAndControls : NetworkBehaviour
    {
        [SerializeField] Transform playerCameraRoot;
        [SerializeField] StarterAssetsInputs AssetInputs;
        [SerializeField] PlayerInput PlayerInput;
        [SerializeField] Transform PlayerModel;
        [SerializeField] Transform InterpolationPoint;
        private Rigidbody _rigidbody;
        public bool UseMobileControls;

        private void Awake()
        {
            _rigidbody=GetComponent<Rigidbody>();
        }

        public override void Spawned()
        {
            var thirdPersonController = GetComponent<ThirdPersonController>();
            if (HasStateAuthority)
            {
                // _rigidbody.MovePosition(new Vector3(Random.Range(-7.6f, 14.2f), 0,
                //     Random.Range(-31.48f, -41.22f)));
                var virtualCamera = GameObject.Find("Player Follow Camera");
                virtualCamera.GetComponent<CinemachineVirtualCamera>().Follow = playerCameraRoot;

                if (UseMobileControls)
                {
                    var mobileControls = GameObject.Find("Mobile Controls");
                    mobileControls.GetComponent<UICanvasControllerInput>().starterAssetsInputs = AssetInputs;
                    mobileControls.GetComponent<MobileDisableAutoSwitchControls>().playerInput = PlayerInput;
                }
                //thirdPersonController.enabled = true;
                StartCoroutine(EnableTpc());
                IEnumerator EnableTpc()
                {
                    var spawnPos = FusionConnection.Instance.UseCustomLocation
                        ? FusionConnection.Instance.CustomLocation
                        : new Vector3(Random.Range(-7.6f, 14.2f), 0, Random.Range(-31.48f, -41.22f));
                    // Teleport so the initial placement is not interpolated from the prefab
                    // pose. The CharacterController caches its internal pose - toggle it so
                    // it picks up the new position instead of reverting the move.
                    var characterController = GetComponent<CharacterController>();
                    if (characterController != null)
                        characterController.enabled = false;
                    var networkTransform = GetComponent<NetworkTransform>();
                    if (networkTransform != null)
                        networkTransform.Teleport(spawnPos);
                    else
                        transform.position = spawnPos;
                    if (characterController != null)
                        characterController.enabled = true;
                    yield return null;
                    thirdPersonController.enabled = true;
                }
            }
            else
            {
                PlayerModel.SetParent(InterpolationPoint);
            }
        }
    }
}
#endif
