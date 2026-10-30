using System;
using System.Collections.Generic;
using UnityEngine;

namespace FS_Util
{
    /// <summary>
    /// Result produced by <see cref="ClipSampler.Sample"/>. Contains per-frame snapshot data
    /// and pre-built AnimationCurves for root motion and the tracked bone position.
    ///
    /// All positions and rotations are expressed in animation-local space:
    ///   +Z = character forward at the first frame of the clip
    ///   +Y = world up
    ///   +X = character right at the first frame of the clip
    /// </summary>
    [Serializable]
    public class ClipSampleResult
    {
        // ── Nested frame data ─────────────────────────────────────────────────────

        [Serializable]
        public struct SampledFrame
        {
            /// <summary>Time within the clip (seconds).</summary>
            public float time;

            /// <summary>
            /// Root displacement from the clip's start position, in animation-local space.
            /// e.g. (0, 0, 2) means the root moved 2 m forward along the starting facing direction.
            /// </summary>
            public Vector3 rootPosition;

            /// <summary>
            /// Signed Y-axis rotation in degrees, relative to the clip start.
            /// Range [-180, 180]. Positive = left turn (Unity left-hand Y convention).
            /// </summary>
            public float rootYAngle;

            /// <summary>
            /// Tracked bone position in root-local space at this frame,
            /// equivalent to <c>root.InverseTransformPoint(boneWorldPosition)</c>.
            /// +Z = root forward, +Y = root up, +X = root right at this specific frame.
            /// Zero if the bone was not found.
            /// </summary>
            public Vector3 bonePosition;
        }

        // ── Metadata ──────────────────────────────────────────────────────────────

        public AnimationClip clip;
        public string        targetBoneName;
        public float         sampleInterval;
        public float         duration;

        // ── Raw frame list ────────────────────────────────────────────────────────

        public List<SampledFrame> frames = new List<SampledFrame>();

        // ── Root position curves (animation-local displacement from start) ─────────

        public AnimationCurve rootPosX = new AnimationCurve();
        public AnimationCurve rootPosY = new AnimationCurve();
        public AnimationCurve rootPosZ = new AnimationCurve();

        // ── Root Y-angle curve (signed degrees, [-180, 180]) ─────────────────────

        public AnimationCurve rootYAngleCurve = new AnimationCurve();

        // ── Bone position curves (animation-local space) ──────────────────────────

        public AnimationCurve bonePosX = new AnimationCurve();
        public AnimationCurve bonePosY = new AnimationCurve();
        public AnimationCurve bonePosZ = new AnimationCurve();

        // ── Convenience helpers ───────────────────────────────────────────────────

        /// <summary>Total root displacement at the last sampled frame.</summary>
        public Vector3 TotalDisplacement =>
            frames.Count > 0 ? frames[frames.Count - 1].rootPosition : Vector3.zero;

        /// <summary>Total signed Y rotation at the last sampled frame (degrees).</summary>
        public float TotalYRotation =>
            frames.Count > 0 ? frames[frames.Count - 1].rootYAngle : 0f;

        /// <summary>Evaluate the root position at any time using the built curves.</summary>
        public Vector3 EvaluateRootPosition(float time) =>
            new Vector3(rootPosX.Evaluate(time), rootPosY.Evaluate(time), rootPosZ.Evaluate(time));

        /// <summary>Evaluate the tracked bone position at any time using the built curves.</summary>
        public Vector3 EvaluateBonePosition(float time) =>
            new Vector3(bonePosX.Evaluate(time), bonePosY.Evaluate(time), bonePosZ.Evaluate(time));
    }
}
