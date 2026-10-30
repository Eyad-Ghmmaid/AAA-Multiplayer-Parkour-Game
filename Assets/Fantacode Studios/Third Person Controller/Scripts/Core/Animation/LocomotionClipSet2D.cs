using System;
using UnityEngine;

namespace FS_LocomotionSystem
{
    [CreateAssetMenu(menuName = "FS System/Locomotion Clip Set 2D")]
    public class LocomotionClipSet2D : ScriptableObject
    {
        [Serializable]
        public struct ClipEntry
        {
            public AnimationClip clip;
            public Vector2 position; // x = strafeAmount, y = moveAmount
        }

        public ClipEntry[] clips;

        [Tooltip("Optional crouch locomotion clips. When assigned, a parent 1D blend driven by idleType blends between normal and crouch sets.")]
        public ClipEntry[] crouchClips;

        public bool HasCrouchSet => crouchClips != null && crouchClips.Length > 0;
    }
}
