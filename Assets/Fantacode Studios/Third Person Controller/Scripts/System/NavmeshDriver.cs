using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

namespace FS_Core
{

    public class NavmeshDriver : MonoBehaviour, IMotionDriver
    {
        NavMeshAgent navAgent;
        ActionExecutor actionExecutor;
        Vector3 velocity;
        Vector3 lastPosition;
        int blockCount;
        public bool IsBlocked => blockCount > 0;

        private void Awake()
        {
            navAgent = GetComponent<NavMeshAgent>();
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
            // Navmesh will always be grounded
            return true;
        }

        public void Move(Vector3 deltaPos, bool rootMotionMovement = false, bool ignoreCollision = false)
        {
            if (rootMotionMovement)
                transform.position += deltaPos;
        }

        // Refcounted so multiple sources (ActionExecutor lifecycle events, ad-hoc
        // state code, future systems) can stack blocks without stomping each other.
        // On block: stop the agent and surrender transform ownership so root motion
        // can drive the body unimpeded. On unblock: sync the agent's internal
        // nextPosition to wherever the body ended up before re-enabling path control.
        public void SetMovementBlocked(bool blocked)
        {
            if (blocked)
            {
                blockCount++;
                if (blockCount == 1)
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
                    Debug.LogWarning($"{nameof(NavmeshDriver)}.SetMovementBlocked(false) called with blockCount already at 0");
                    return;
                }
                blockCount--;
                if (blockCount == 0)
                {
                    navAgent.nextPosition = transform.position;
                    navAgent.updatePosition = true;
                    navAgent.isStopped = false;
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

        public void SetTarget(Vector3 position, Quaternion rotation, float moveSpeed, bool driverHandlesRotation = false)
        {
            _alignPos = position;
            _alignRot = rotation;
            navAgent.isStopped = false;
            navAgent.SetDestination(position);
        }

        public void TickTarget(float dt, Vector3? newPosition = null, Quaternion? newRotation = null, bool ignoreCollision = false)
        {
            if (newRotation.HasValue) _alignRot = newRotation.Value;

            if (newPosition.HasValue)
            {
                _alignPos = newPosition.Value;
                navAgent.SetDestination(_alignPos);
            }
        }

        public bool IsPositionReached(float threshold)
        {
            return !navAgent.pathPending && navAgent.remainingDistance <= threshold;
        }

        public void SnapToTarget()
        {
            navAgent.Warp(_alignPos);
            transform.rotation = _alignRot;
        }

        private void OnGUI()
        {
            // GUILayout.Label("Nav Agent Stopped: " + navAgent.isStopped, new GUIStyle() { fontSize = 20 });
        }
    }
}
