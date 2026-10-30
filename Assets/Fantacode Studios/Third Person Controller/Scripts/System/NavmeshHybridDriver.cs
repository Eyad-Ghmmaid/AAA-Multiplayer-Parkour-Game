using FS_Util;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

namespace FS_Core
{
    /// <summary>
    /// Uses Navmesh for normal locomotion, but uses characterController in rootMotion Mode
    /// </summary>
    public class NavmeshHybridDriver : MonoBehaviour, IMotionDriver
    {
        NavMeshAgent navAgent;
        CharacterController characterController;
        ActionExecutor actionExecutor;

        Vector3 velocity;
        Vector3 lastPosition;
        int blockCount;
        public bool IsBlocked => blockCount > 0;

        private void Awake()
        {
            navAgent = GetComponent<NavMeshAgent>();
            characterController = GetComponent<CharacterController>();
            actionExecutor = GetComponent<ActionExecutor>();
            lastPosition = transform.position;
        }

        private void OnEnable()
        {
            if (actionExecutor != null)
            {
                actionExecutor.OnActionStarted += OnActionStarted;
                actionExecutor.OnActionEnded += OnActionEnded;
            }
        }

        private void OnDisable()
        {
            if (actionExecutor != null)
            {
                actionExecutor.OnActionStarted -= OnActionStarted;
                actionExecutor.OnActionEnded -= OnActionEnded;
            }
        }

        void OnActionStarted(CharacterAction action)
        {
            // Block for any action that takes over locomotion or drives the character via root motion.
            if (action.usesRootMotion || action.blocksLocomotion)
                SetMovementBlocked(true);
        }

        void OnActionEnded(CharacterAction action)
        {
            if (action.usesRootMotion || action.blocksLocomotion)
                SetMovementBlocked(false);
        }

        void Update()
        {
            if (Time.deltaTime > 0)
                velocity = (transform.position - lastPosition) / Time.deltaTime;
            lastPosition = transform.position;
        }

        public bool SupportsManualMovement => false;

        public Vector3 GetVelocity()
        {
            return velocity;
        }

        public bool IsGrounded()
        {
            if (blockCount > 0) return false;
            
            return true;
        }

        public void Move(Vector3 deltaPos, bool rootMotionMovement=false, bool ignoreCollision=false)
        {
            // AI is controlled automatically for locomotion. But characterController can be used for root motion movement

            if (rootMotionMovement)
            {
                // If NavAgent is active, then don't go out of the Navmesh
                bool keepOnNavesh = actionExecutor == null || !actionExecutor.CanLeaveNavMesh;
                if (keepOnNavesh && navAgent.isActiveAndEnabled && NavMesh.Raycast(transform.position, transform.position + deltaPos, out NavMeshHit hit, navAgent.areaMask))
                    return;

                if (ignoreCollision || characterController == null || !characterController.enabled)
                    transform.position += deltaPos;
                else
                    characterController.Move(deltaPos);

            }
        }

        public void SetMovementBlocked(bool blocked)
        {
            if (blocked)
            {
                blockCount++;
                if (blockCount == 1 && navAgent.enabled)
                {
                    navAgent.velocity = Vector3.zero;
                    navAgent.isStopped = true;
                    navAgent.updatePosition = false;
                }
            }
            else
            {
                if (blockCount == 0)
                {
                    Debug.LogWarning($"{nameof(NavmeshHybridDriver)}.SetMovementBlocked(false) called with blockCount already at 0");
                    return;
                }
                blockCount--;
                if (blockCount == 0)
                {
                    if (!navAgent.isActiveAndEnabled) return;

                    // Warp re-syncs the agent onto the NavMesh, but it also resets the current path. 
                    // So save the destination and call set it again
                    bool hadPath = navAgent.hasPath;
                    Vector3 dest = navAgent.destination;

                    navAgent.Warp(transform.position);
                    navAgent.updatePosition = true;
                    navAgent.isStopped = false;

                    if (hadPath) navAgent.SetDestination(dest);
                }
            }
        }

        public void WarpToPosition(Vector3 position)
        {
            navAgent.Warp(position);
        }

        // ── Target navigation ───────────────────────────────────────────────────────

        Vector3 _alignPos;
        Quaternion _alignRot;
        float _defaultStoppingDistance;
        float _defaultMoveSpeed;
        bool _positionReached;
        bool updateRotation;
        bool updatePosition;

        public void SetTarget(Vector3 position, Quaternion rotation, float moveSpeed, bool driverHandlesRotation = false)
        {
            _alignPos = position;
            _alignRot = rotation;
            _positionReached = false;

            _defaultStoppingDistance = navAgent.stoppingDistance;
            _defaultMoveSpeed = navAgent.speed;
            // Allow the agent to auto-rotate only in single-directional mode.
            // In multi-directional mode LocomotionCore rotates the character each frame.
            updateRotation = navAgent.updateRotation;
            updatePosition = navAgent.updatePosition;
            navAgent.updateRotation = driverHandlesRotation;
            navAgent.updatePosition = updatePosition;
            navAgent.stoppingDistance = 0;
            navAgent.speed = moveSpeed;
            navAgent.SetDestination(position);
        }

        public void TickTarget(float dt, Vector3? newPosition = null, Quaternion? newRotation = null, bool ignoreCollision = false)
        {
            if (newRotation.HasValue) _alignRot = newRotation.Value;

            if (newPosition.HasValue)
            {
                _alignPos = newPosition.Value;
                _positionReached = false;
                navAgent.SetDestination(_alignPos);
            }
        }

        public bool IsPositionReached(float threshold)
        {
            if (_positionReached) return true;

            Vector3 delta = _alignPos - transform.position;
            delta.y = 0f;
            _positionReached = delta.sqrMagnitude <= threshold * threshold;

            if (_positionReached)
            {
                // Stop the agent and hand rotation control back to LocomotionCore.
                navAgent.isStopped = true;
                navAgent.updateRotation = updateRotation;
                navAgent.updatePosition = updatePosition;
                navAgent.stoppingDistance = _defaultStoppingDistance;
                navAgent.speed = _defaultMoveSpeed;
            }

            return _positionReached;
        }

        public void SnapToTarget()
        {
            navAgent.Warp(_alignPos);
            navAgent.isStopped = false;
            transform.rotation = _alignRot;
        }
    }

}
