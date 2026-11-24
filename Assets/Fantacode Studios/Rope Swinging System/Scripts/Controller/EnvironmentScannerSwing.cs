using FS_SwingSystem;
using FS_Util;
using UnityEngine;

namespace FS_Core
{
    public partial class EnvironmentScanner : MonoBehaviour
    {
        [field: SerializeField] public LayerMask SwingLedgeLayer { get; set; } = 1;

        private const float SwingAimAngle = 35f;
        private static readonly Vector3 SwingPathHalfExtents = new Vector3(0.05f, 0.1f, 0.01f);
        private Collider[] swingCandidates = new Collider[32];
        private PlayerController swingPlayer;

        public SwingData GetSwingLedgeData(float maxDistance, float minDistance, Transform ropeHoldPoint, bool debug)
        {
            if (ropeHoldPoint == null || maxDistance <= 0f || maxDistance < minDistance)
                return default;

            if (swingPlayer == null)
                swingPlayer = GetComponent<PlayerController>();
            var view = swingPlayer != null && swingPlayer.cameraGameObject != null
                ? swingPlayer.cameraGameObject.transform
                : Camera.main != null ? Camera.main.transform : null;
            if (view == null)
                return default;

            return GetSwingLedgeData(maxDistance, minDistance, ropeHoldPoint, view.position, view.forward, debug);
        }

        internal SwingData GetSwingLedgeData(float maxDistance, float minDistance, Transform ropeHoldPoint,
            Vector3 viewOrigin, Vector3 viewForward, bool debug)
        {
            if (ropeHoldPoint == null || maxDistance <= 0f || maxDistance < minDistance)
                return default;

            Vector3 hand = ropeHoldPoint.position;
            int count = Physics.OverlapSphereNonAlloc(hand, maxDistance, swingCandidates, SwingLedgeLayer, QueryTriggerInteraction.Collide);
            while (count == swingCandidates.Length)
            {
                System.Array.Resize(ref swingCandidates, swingCandidates.Length * 2);
                count = Physics.OverlapSphereNonAlloc(hand, maxDistance, swingCandidates, SwingLedgeLayer, QueryTriggerInteraction.Collide);
            }

            Vector3 aim = (viewForward + transform.up * 0.25f).normalized;
            float bestAlignment = Mathf.Cos(SwingAimAngle * Mathf.Deg2Rad);
            float bestDistance = float.PositiveInfinity;
            SwingData best = default;
            for (int i = 0; i < count; i++)
            {
                var ledge = swingCandidates[i].GetComponentInParent<SwingLedge>();
                if (ledge == null || !ledge.isActiveAndEnabled)
                    continue;

                Vector3 hook = ledge.transform.TransformPoint(ledge.hookPoint);
                float distance = Vector3.Distance(hand, hook);
                if (distance < minDistance || distance > maxDistance)
                    continue;

                float alignment = Vector3.Dot(aim, (hook - viewOrigin).normalized);
                if (alignment < bestAlignment || (alignment == bestAlignment && distance >= bestDistance) ||
                    !HasSwingPath(ledge, hand, hook, debug))
                    continue;

                bestAlignment = alignment;
                bestDistance = distance;
                best = new SwingData
                {
                    hasLedge = true,
                    ledge = ledge,
                    hookPosition = hook,
                    forwardDirection = Vector3.ProjectOnPlane(hook - transform.position, Vector3.up).normalized,
                    directionToHook = (hook - transform.position).normalized,
                    distance = distance
                };
            }

            return best;
        }

        private bool HasSwingPath(SwingLedge ledge, Vector3 start, Vector3 end, bool debug)
        {
            Vector3 path = end - start;
            float distance = path.magnitude;
            if (distance <= 0.01f)
                return false;

            Vector3 direction = path / distance;
            Quaternion orientation = Quaternion.FromToRotation(Vector3.forward, direction);
            bool blocked = Physics.BoxCast(start, SwingPathHalfExtents, direction, out var hit, orientation,
                distance, ObstacleLayer, QueryTriggerInteraction.Ignore) &&
                hit.collider.GetComponentInParent<SwingLedge>() != ledge;
            if (debug)
                BoxCastDebug.DrawBoxCastBox(start, SwingPathHalfExtents, orientation, direction, distance,
                    blocked ? Color.red : Color.green);
            return !blocked;
        }
    }

    public struct SwingData
    {
        public SwingLedge ledge;
        public Vector3 hookPosition;
        public bool hasLedge;
        public Vector3 directionToHook;
        public Vector3 forwardDirection;
        public float distance;
    }
}
