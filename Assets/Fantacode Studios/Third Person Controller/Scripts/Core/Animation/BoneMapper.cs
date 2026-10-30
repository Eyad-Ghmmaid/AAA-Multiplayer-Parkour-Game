using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;


namespace FS_Core
{

    public enum BoneType
    {
        LeftHand,
        RightHand,
        LeftFoot,
        RightFoot,
        LeftElbow,
        RightElbow,
        LeftKnee,
        RightKnee,
        Head,
        Hips
        // Add more as needed
    }

    public class BoneMapper : MonoBehaviour
    {
        [Tooltip("Manual bone mapping for non-humanoid models.")]
        public List<BoneMapping> boneMappings = new List<BoneMapping>();

        private Dictionary<BoneType, Transform> boneMap = new Dictionary<BoneType, Transform>();
        private Animator animator;

        [Serializable]
        public class BoneMapping
        {
            public BoneType boneType;
            public Transform boneTransform;
        }

        void Awake()
        {
            animator = GetComponent<Animator>();
            if (animator == null || !animator.isHuman)
            {
                foreach (var mapping in boneMappings)
                {
                    if (mapping.boneTransform != null)
                        boneMap[mapping.boneType] = mapping.boneTransform;
                }
            }
        }

        public Transform GetBone(BoneType boneType)
        {
            if (animator == null)
                animator = GetComponent<Animator>();

            if (animator != null && animator.isHuman)
            {
                return animator.GetBoneTransform(BoneTypeToHumanBodyBone(boneType));
            }
            else
            {
                if (boneMap != null && boneMap.Count > 0)
                {
                    boneMap.TryGetValue(boneType, out var t);
                    return t;
                }
                else
                    return boneMappings.FirstOrDefault(m => m.boneType == boneType)?.boneTransform;
            }
        }

        /// <summary>
        /// Maps a HumanBodyBones value to a BoneType. Returns null if no mapping exists.
        /// </summary>
        public static BoneType HumanBodyBoneToBoneType(HumanBodyBones humanBone)
        {
            switch (humanBone)
            {
                case HumanBodyBones.LeftHand: return BoneType.LeftHand;
                case HumanBodyBones.RightHand: return BoneType.RightHand;
                case HumanBodyBones.LeftFoot: return BoneType.LeftFoot;
                case HumanBodyBones.RightFoot: return BoneType.RightFoot;
                case HumanBodyBones.LeftLowerArm: return BoneType.LeftElbow;
                case HumanBodyBones.RightLowerArm: return BoneType.RightElbow;
                case HumanBodyBones.LeftLowerLeg: return BoneType.LeftKnee;
                case HumanBodyBones.RightLowerLeg: return BoneType.RightKnee;
                case HumanBodyBones.Head: return BoneType.Head;
                case HumanBodyBones.Hips: return BoneType.Hips;
                default: return BoneType.Head;
            }
        }

        /// <summary>
        /// Maps a BoneType value to a HumanBodyBones. Returns null if no mapping exists.
        /// </summary>
        public static HumanBodyBones BoneTypeToHumanBodyBone(BoneType boneType)
        {
            switch (boneType)
            {
                case BoneType.LeftHand: return HumanBodyBones.LeftHand;
                case BoneType.RightHand: return HumanBodyBones.RightHand;
                case BoneType.LeftFoot: return HumanBodyBones.LeftFoot;
                case BoneType.RightFoot: return HumanBodyBones.RightFoot;
                case BoneType.LeftElbow: return HumanBodyBones.LeftLowerArm;
                case BoneType.RightElbow: return HumanBodyBones.RightLowerArm;
                case BoneType.LeftKnee: return HumanBodyBones.LeftLowerLeg;
                case BoneType.RightKnee: return HumanBodyBones.RightLowerLeg;
                case BoneType.Head: return HumanBodyBones.Head;
                case BoneType.Hips: return HumanBodyBones.Hips;
                default: return HumanBodyBones.Head;
            }
        }
    }
}