using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace FS_Core
{

    public interface IMotionDriver
    {
        void Move(Vector3 deltaPos, bool rootMotionMovement = false, bool ignoreCollision = false);

        Vector3 GetVelocity();

        bool IsGrounded();

        void SetMovementBlocked(bool blocked);

        void WarpToPosition(Vector3 position);

        bool SupportsManualMovement { get; }

        // ── Target navigation (used by LocomotionCore.SetTarget / Tick / TargetReached) ──

        /// <summary>
        /// Sets the destination and speed for the next guided move.
        /// Rotation is handled by LocomotionCore. <paramref name="driverHandlesRotation"/>
        /// tells drivers that support autonomous rotation (e.g. NavMeshAgent) whether
        /// they may rotate the character during movement — set true for single-directional
        /// mode and false for multi-directional mode where LocomotionCore rotates each frame.
        /// </summary>
        void SetTarget(Vector3 position, Quaternion rotation, float moveSpeed, bool driverHandlesRotation = false);

        /// <summary>
        /// Advances position one step toward the target. Rotation is handled by LocomotionCore.Tick().
        /// If <paramref name="newPosition"/> or <paramref name="newRotation"/> are provided, the
        /// stored target is updated before moving — use this when the destination changes every frame
        /// (e.g. following a moving vehicle).
        /// </summary>
        void TickTarget(float dt, Vector3? newPosition = null, Quaternion? newRotation = null, bool ignoreCollision = false);

        /// <summary>True when the character is within <paramref name="threshold"/> metres of the target position.</summary>
        bool IsPositionReached(float threshold);

        /// <summary>Teleports the character to the exact target position/rotation (end-of-alignment snap).</summary>
        void SnapToTarget();
    }
}
