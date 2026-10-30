using FS_Core;
using UnityEngine;

namespace FS_LocomotionSystem
{
    [CreateAssetMenu(menuName = "Third Person/Idle Turns")]
    public class IdleTurns : ScriptableObject
    {
        [Header("Idle Turn Clips (AnimGraph)")]
        public AnimGraphClipInfo idleTurn45;
        public AnimGraphClipInfo idleTurn90;
        public AnimGraphClipInfo idleTurn135;
        public AnimGraphClipInfo idleTurn180;
        public AnimGraphClipInfo idleTurn45Mirror;
        public AnimGraphClipInfo idleTurn90Mirror;
        public AnimGraphClipInfo idleTurn135Mirror;
        public AnimGraphClipInfo idleTurn180Mirror;

        public AnimGraphClipInfo GetClip(float animationAngle, bool mirrored)
        {
            if (mirrored)
            {
                if (animationAngle == 45f) return idleTurn45Mirror;
                if (animationAngle == 90f) return idleTurn90Mirror;
                if (animationAngle == 135f) return idleTurn135Mirror;
                if (animationAngle == 180f) return idleTurn180Mirror;
            }
            else
            {
                if (animationAngle == 45f) return idleTurn45;
                if (animationAngle == 90f) return idleTurn90;
                if (animationAngle == 135f) return idleTurn135;
                if (animationAngle == 180f) return idleTurn180;
            }

            return null;
        }
    }
}
