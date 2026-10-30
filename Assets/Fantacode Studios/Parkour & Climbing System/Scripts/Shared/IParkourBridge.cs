using FS_Core;
using System;
using System.Collections;
using UnityEngine;

namespace FS_ParkourSystem
{
    /// <summary>
    /// Strategy interface that abstracts the divergence points between Player and AI
    /// parkour/climbing systems. Implemented by ParkourController (player) and ParkourAI (AI).
    /// Engines call bridge methods at each divergence point instead of player/AI-specific code.
    /// </summary>
    public interface IParkourBridge
    {
        // --- Character access (true divergence) ---
        float Gravity { get; }

        // --- Lifecycle (focus system vs NavMesh callbacks) ---
        /// <summary>Player: FocusSystem(this) | AI: OnStartParkourAction()</summary>
        void OnActionStart(bool needHands = false);

        /// <summary>Player: UnFocusSystem(this) | AI: OnEndParkourAction()</summary>
        void OnActionEnd();

        /// <summary>Player: player.WaitToStartSystem | AI: player.WaitToStartSystem</summary>
        bool WaitToStart { get; }

        /// <summary>Player: IsInFocus (can be interrupted) | AI: true (always continues)</summary>
        bool ShouldContinueAction { get; }

        // --- Divergence hooks ---

        /// <summary>Player: playerController.IsInAir = value | AI: no-op</summary>
        void SetIsInAir(bool value);

        /// <summary>Player: playerController.OnLand?.Invoke(intensity, duration) | AI: no-op</summary>
        void OnLand(float intensity, float duration);

        /// <summary>
        /// Player: environmentScanner.getJumpHeight | AI: own getJumpHeight method
        /// </summary>
        float GetJumpHeight(float displacementY, Vector3 displacementXZ);

        /// <summary>
        /// Player: raycast foot collider + foot distance + timer fallback
        /// AI: pure timer (timer >= jumpTime)
        /// Returns true when character should land.
        /// </summary>
        bool CheckGrounded(float timer, float jumpTime, Vector3 footPosition, Vector3 deltaPosition, bool mirrorJump);
    }

    /// <summary>
    /// Strategy interface for climb-specific divergence points.
    /// Implemented by ClimbController (player) and ClimbAI (AI).
    /// </summary>
    public interface IClimbBridge
    {
        /// <summary>
        /// Lifecycle for climb-specific actions.
        /// Player: FocusSystem(climbController, needHands) | AI: OnStartParkourAction()
        /// </summary>
        void OnClimbActionStart(bool needHands);

        /// <summary>
        /// Mirror jump computation.
        /// Player: foot-based logic | AI: simple toggle. Sets core.MirrorJump.
        /// </summary>
        void ComputeMirrorJump(ParkourCore core);

        /// <summary>
        /// Called each frame during hanging phase. Returns true if combo was initiated (caller should yield break).
        /// Player: accumulates input flags, checks camera angle + SwingableLedge + timing, does env scan + jump.
        /// AI: checks NextJumpPoint + conditions, creates JumpData + starts jump.
        /// </summary>
        bool HandlePredictiveClimbLanding(ClimbPoint point, float comboTime, float peakTime);

        /// <summary>
        /// Resets per-call state. Player: clears combo flags | AI: no-op.
        /// </summary>
        void ResetPredictiveClimbState();

        /// <summary>
        /// Player: checks currentPoint.useManualOptions for handSpacing | AI: always uses default
        /// Returns the effective hand spacing for the given point.
        /// </summary>
        float GetHandSpacing(ClimbPoint point, float defaultSpacing);

        /// <summary>Player: ClimbController.IsInFocus | AI: true (always continues)</summary>
        bool ShouldContinueAction { get; }

        /// <summary>
        /// Called after back jump init phase completes, before chaining to next action.
        /// Player: UnFocusSystem(climbController). AI: if noChain, calls OnEndParkourAction(); otherwise no-op.
        /// </summary>
        void OnBackJumpComplete(bool noChain);
    }
}
