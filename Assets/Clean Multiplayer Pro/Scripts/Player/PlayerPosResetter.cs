#if CMPSETUP_COMPLETE
using System;
using Fusion;
using UnityEngine;
using Random = UnityEngine.Random;

namespace AvocadoShark
{
    public class PlayerPosResetter : NetworkBehaviour
    {
        public float minYValue = -10f;
        private NetworkTransform _networkTransform;
        private CharacterController _characterController;

        public override void Spawned()
        {
            _networkTransform = GetComponent<NetworkTransform>();
            _characterController = GetComponent<CharacterController>();
        }

        private void LateUpdate()
        {
            if (!HasStateAuthority)
                return;
            if (transform.position.y < minYValue)
            {
                ResetPlayerPosition();
            }
        }

        private void OnControllerColliderHit(ControllerColliderHit hit)
        {
            if (!HasStateAuthority)
                return;
            if (!hit.gameObject.TryGetComponent(out ItemPickup item))
                return;

            item.PickUp(Object);
        }

        private void ResetPlayerPosition()
        {
            var target = FusionConnection.Instance.UseCustomLocation
                ? FusionConnection.Instance.CustomLocation
                : new Vector3(Random.Range(-7.6f, 14.2f), 0, Random.Range(-31.48f, -41.22f));
            // Teleport marks the move as non-interpolatable - a raw transform write would
            // make remote peers lerp the player across the map (and fight local interpolation).
            // The CharacterController caches its internal PhysX pose and would silently
            // revert the move on its next Move() - toggle it around the teleport.
            if (_characterController != null)
                _characterController.enabled = false;
            if (_networkTransform != null)
                _networkTransform.Teleport(target);
            else
                transform.position = target;
            if (_characterController != null)
                _characterController.enabled = true;
        }
    }
}
#endif