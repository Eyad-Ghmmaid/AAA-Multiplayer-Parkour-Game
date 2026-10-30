using FS_Core;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace FS_LocomotionSystem
{

    public class CharacterControllerDriver : MonoBehaviour, IMotionDriver
    {
        CharacterController controller;
        Vector3 velocity;
        bool blocked;


        private void Awake()
        {
            controller = GetComponent<CharacterController>();
        }

        public bool SupportsManualMovement => true;

        public Vector3 GetVelocity()
        {
            return controller.velocity;
        }

        public bool IsGrounded()
        {
            //return controller.isGrounded;
            return false;
        }

        public void Move(Vector3 deltaPos, bool rootMotionMovement = false, bool ignoreCollision = false)
        {
            if (ignoreCollision || !controller.enabled)
                transform.position += deltaPos;
            else
            {
                controller.Move(deltaPos);
            }
        }

        public void SetMovementBlocked(bool blocked)
        {
            this.blocked = blocked;
        }

        public void WarpToPosition(Vector3 position)
        {
            transform.position = position;
            Physics.SyncTransforms();
        }

        // ── Target navigation ───────────────────────────────────────────────────────

        Vector3 _alignPos;
        Quaternion _alignRot;
        float _alignMoveSpeed;

        public void SetTarget(Vector3 position, Quaternion rotation, float moveSpeed, bool driverHandlesRotation = false)
        {
            _alignPos = position;
            _alignRot = rotation;
            _alignMoveSpeed = moveSpeed;
        }

        public void TickTarget(float dt, Vector3? newPosition = null, Quaternion? newRotation = null, bool ignoreCollision = false)
        {
            if (newPosition.HasValue) _alignPos = newPosition.Value;
            if (newRotation.HasValue) _alignRot = newRotation.Value;

            Vector3 delta = _alignPos - transform.position;
            delta.y = 0f;
            float dist = delta.magnitude;
            if (dist > 0f)
            {
                float step = Mathf.Min(_alignMoveSpeed * dt, dist);
                if (controller.enabled && !ignoreCollision)
                    controller.Move(delta.normalized * step);
                else
                    transform.position += delta.normalized * step;
            }
        }

        public bool IsPositionReached(float threshold)
        {
            Vector3 delta = _alignPos - transform.position;
            delta.y = 0f;
            return delta.sqrMagnitude <= threshold * threshold;
        }

        public void SnapToTarget()
        {
            transform.position = _alignPos;
            transform.rotation = _alignRot;
            Physics.SyncTransforms();
        }
    }
}
