using FS_Core;
using FS_Util;
#if UNITY_EDITOR
#endif
using UnityEngine;

namespace FS_ParkourSystem
{
    public enum VaultType { Any, VaultOver, VaultOn }

    public enum MotionWarpType
    {
        ScaleRootMotion,   // scales root motion curves per-axis to reach target (good for vertical moves)
        Linear             // linearly interpolates position from start to target over warp window
    }

    [CreateAssetMenu(menuName = "Parkour System/New Parkour Action")]
    public class ParkourAction : ScriptableObject
    {
        [Tooltip("AnimGraph clip to play for this parkour action")]
        [SerializeField] public AnimGraphClipInfo clipInfo;

        [Tooltip("Minimum height of the obstacle on which this parkour action can be performed")]
        [SerializeField] float minHeight;
        [Tooltip("Maximum height of the obstacle on which this parkour action can be performed")]
        [SerializeField] float maxHeight;

        [SerializeField] bool useHands = true;

        [Tooltip("If true, the player will rotate towards the obstacle while performing the parkour action")]
        [SerializeField] bool rotateToObstacle;

        [Tooltip("Determines if this parkour action makes the player vault over or vault onto the obstacle")]
        [SerializeField] VaultType vaultType;

        [Tooltip("If obstacle tag is given, then this parkour action will only be performed on obstacles with the same tag")]
        [SerializeField] string obstacleTag;

        [Tooltip("Delay before giving the control to the player after the parkour action. Useful for action's that requires an additional transition animation.")]
        [SerializeField] float postActionDelay;

        [Header("Target Matching")]

        [Tooltip("If true, the parkour action will use target matching. This is useful for adapting the same animation to obstacles of different sizes")]
        [SerializeField] bool enableTargetMatching = true;

        [ShowIf("enableTargetMatching", true)]
        [Tooltip("The body part that should be used for target matching")]
        [SerializeField] protected AvatarTarget matchBodyPart;

        [ShowIf("enableTargetMatching", true)]
        [Tooltip("Normalized time of the animation at which the target matching should start")]
        [SerializeField] float matchStartTime;

        [ShowIf("enableTargetMatching", true)]
        [Tooltip("Normalized time of the animation at which the target matching should end")]
        [SerializeField] float matchTargetTime;

        [ShowIf("enableTargetMatching", true)]
        [Tooltip("Determines the axes that the target matching should affect")]
        [SerializeField] Vector3 matchPosWeight = new Vector3(0, 1, 0);

        [ShowIf("enableTargetMatching", true)]
        [Tooltip("Warping mode: ScaleRootMotion scales root motion curves to reach target; Linear interpolates position evenly over the warp window.")]
        [SerializeField] MotionWarpType warpMatchType = MotionWarpType.ScaleRootMotion;

        [Header("Additional Settings")]
        [Tooltip("Minimum horizontal length of the obstacle on which this parkour action can be performed")]
        [SerializeField] float minimumObstacleHorizontalDistance = 0f;

        [Tooltip("Required movement speed to perform parkour")]
        [SerializeField] public float movementThreshold = 0f;

        [Header("Baked Curves")]

        [Tooltip("Extracted Root Motion from the animation clip")]
        public PositionCurves rootMotionCurves;

        [Tooltip("Pre-baked offset from the matched bone to the character root at matchTargetTime, in animation-local space. " +
                 "Used to compute the correct root target position when X/Z weight axes are non-zero.")]
        [SerializeField] Vector3 boneRootOffset;

        public Quaternion TargetRotation { get; set; }
        public Vector3 MatchPos { get; set; }
        public bool Mirror { get; set; }

        public virtual bool CheckIfPossible(ObstacleHitData hitData, Transform player)
        {
            if (vaultType == VaultType.VaultOn && !hitData.hasSpaceToVault)
                return false;

            if (vaultType == VaultType.VaultOver && minimumObstacleHorizontalDistance == 0 && hitData.hasSpaceToVault)
                return false;


            // Check Tag
            if (!string.IsNullOrEmpty(obstacleTag) && hitData.forwardHit.transform.tag != obstacleTag)
                return false;

            // Height Tag
            float height = hitData.heightHit.point.y - player.position.y;
            if (height < minHeight || height > maxHeight)
                return false;

            var dir = hitData.heightHit.point - player.position;
            dir.y = 0;
            if (rotateToObstacle)
                TargetRotation = Quaternion.LookRotation(dir);
            //if (rotateToObstacle)
            //    TargetRotation = Quaternion.LookRotation(Vector3.Scale(-hitData.forwardHit.normal, new Vector3(1, 0, 1)));

            if (enableTargetMatching)
                MatchPos = hitData.heightHit.point;

            if (minimumObstacleHorizontalDistance > 0)
            {
                if (!hitData.ledgeHitFound || (hitData.ledgeHit.point - hitData.heightHit.point).magnitude > minimumObstacleHorizontalDistance)
                    return false;

                if (enableTargetMatching)
                    MatchPos = hitData.ledgeHit.point;
            }

            return true;
        }

        public string AnimName => clipInfo?.clip?.name ?? string.Empty;
        public AnimGraphClipInfo ClipInfo => clipInfo;
        public Vector3 BoneRootOffset => boneRootOffset;
        public PositionCurves RootMotionCurves => rootMotionCurves;
        public bool RotateToObstacle => rotateToObstacle;
        public bool UseHands => useHands;
        public float PostActionDelay => postActionDelay;

        public bool EnableTargetMatching => enableTargetMatching;
        public AvatarTarget MatchBodyPart => matchBodyPart;
        public float MatchStartTime => matchStartTime;
        public float MatchTargetTime => matchTargetTime;
        public Vector3 MatchPosWeight => matchPosWeight;
        public MotionWarpType WarpMatchType => warpMatchType;
    }

}