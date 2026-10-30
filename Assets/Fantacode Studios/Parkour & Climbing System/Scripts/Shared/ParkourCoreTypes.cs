using System;
using UnityEngine;

namespace FS_ParkourSystem
{
    public enum HangType { bracedHang, freeHang }

    [Serializable]
    public class IKweights
    {
        public float current;
        public float target;
        public float weight;
        public ClimbPoint currentPoint;
        public ClimbPoint previousPoint;

        public Vector3 currentIkVecPoint;
        public Vector3 previousIkVecPoint;
        public Vector3 currentVecPoint;
        public Vector3 previousVecPoint;

        public bool currentVecPointOn;
        public bool previousVecPointOn;

        public Vector3 boneTransform;
        public Vector3 bodyPartOffset;

        public Vector3 startBodyPartOffset;
        public Vector3 endBodyPartOffset;
        public Vector3 IKPoint;
        public float lerpValue;
    }

    public struct WallInfo
    {
        public bool isWall;
        public RaycastHit rightFootInfo;
        public RaycastHit leftFootInfo;
    }
}
