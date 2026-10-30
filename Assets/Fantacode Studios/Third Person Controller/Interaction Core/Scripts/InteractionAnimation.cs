using FS_Core;
using FS_Util;
using UnityEngine;
using UnityEngine.Serialization;

namespace FS_InteractionSystem
{
    /// <summary>
    /// One authored beat of an interaction — the animation played on the interacting character,
    /// plus an optional animation played on the interacted object itself (a box lid lifting, a
    /// vehicle door swinging) that runs alongside it.
    ///
    /// Used for the enter / exit halves of an <see cref="OccupiableHotspot"/>.
    /// </summary>
    [System.Serializable]
    public class InteractionAnimation
    {
        [Tooltip("Animation clip played on the interactor (player).")]
        public AnimGraphClipInfo clip;

        [Tooltip("Also animate the object itself — typically it opening (a box lid, a vehicle door). " +
                 "The interactor's clip then fires concurrently while the object's animation drives timing.")]
        [FormerlySerializedAs("isSynced")]
        public bool playOpenAnimation;

        [Tooltip("The object's AnimGraph that plays the open animation (e.g. the box's or vehicle's).")]
        [ShowIf("playOpenAnimation", true)]
        [FormerlySerializedAs("target")]
        public AnimGraph openTarget;

        [Tooltip("Open animation played on the object — the lid lifting, the door swinging. Its length " +
                 "drives how long the interaction takes.")]
        [ShowIf("playOpenAnimation", true)]
        [FormerlySerializedAs("targetClip")]
        public AnimGraphClipInfo openClip;

        /// <summary>True when the object has an open animation of its own to play (and drive timing with).</summary>
        public bool HasOpenAnimation => playOpenAnimation && openTarget != null && openClip.NotNull();
    }
}
