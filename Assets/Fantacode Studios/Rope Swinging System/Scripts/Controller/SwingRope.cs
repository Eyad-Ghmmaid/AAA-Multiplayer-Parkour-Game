using System;
using UnityEngine;

namespace FS_SwingSystem
{
    public enum RopeState { Normal, Swinging, Throwing, Climbing }

    public class SwingRope
    {
        private const int ConstraintIterations = 50;
        private const int HoldRopeResolution = 50;
        private const float ColliderRadius = 0.05f;

        public float ropeRadius;
        public Material ropeMaterial;
        public int hookRopeResolution;
        public float totalRopeLength;
        public float ropeWidth;
        public float dampening;
        public float smoothingFactor = 0.1f;
        public float throwHeight;
        public float throwDuration;

        public Transform ropeHoldTransformLeft;
        public Transform ropeHoldTransformRight;
        public Transform ropeAttachPointToHook;
        public Transform ropeAttachTransformInBody;
        public Transform hookObject;
        public Transform currentRopeHoldPointTransform;
        public float hookRopeLength;
        public RopeState ropeState = RopeState.Normal;

        private readonly Transform playerTransform;
        private readonly Collider[] ropeColliders = new Collider[12];
        private readonly Vector3[] holdPositions = new Vector3[HoldRopeResolution];
        private readonly Vector3[] holdPrevious = new Vector3[HoldRopeResolution];
        private readonly Vector3[] holdVelocities = new Vector3[HoldRopeResolution];

        private Vector3[] hookPositions;
        private Vector3[] hookPrevious;
        private Vector3[] hookVelocities;
        private LineRenderer hookRope;
        private LineRenderer holdRope;

        private Vector3 hookPoint;
        private Vector3 heldHookPosition;
        private Quaternion heldHookRotation;
        private Quaternion worldHookRotation;
        private Vector3 throwStart;
        private Vector3 throwTarget;
        private float throwElapsed;
        private bool hooked;

        public SwingRope(float ropeRadius, float ropeWidth, Material ropeMaterial, float throwDuration, float ropeLength,
            float dampening, int ropeResolution, float throwHeight, Transform transform, Transform ropeAttachTransformInBody,
            Transform ropeHoldTransformLeft, Transform ropeHoldTransformRight, Transform ropeAttachPointToHook, Transform hookObject)
        {
            this.ropeRadius = ropeRadius;
            this.ropeWidth = ropeWidth;
            this.ropeMaterial = ropeMaterial;
            this.throwDuration = throwDuration;
            this.totalRopeLength = ropeLength;
            this.dampening = dampening;
            this.hookRopeResolution = Mathf.Max(2, ropeResolution);
            this.throwHeight = throwHeight;
            playerTransform = transform;
            this.ropeAttachTransformInBody = ropeAttachTransformInBody;
            this.ropeHoldTransformLeft = ropeHoldTransformLeft;
            this.ropeHoldTransformRight = ropeHoldTransformRight;
            this.ropeAttachPointToHook = ropeAttachPointToHook;
            this.hookObject = hookObject;
            currentRopeHoldPointTransform = ropeHoldTransformRight;
            InitLineRenderer(transform);
        }

        public void InitLineRenderer(Transform transform)
        {
            if (hookRope != null)
                return;

            hookRope = CreateLine("Rope", transform);
            holdRope = CreateLine("PhysicsRope", transform);
            ResetHoldRope();
        }

        private LineRenderer CreateLine(string name, Transform parent)
        {
            var lineObject = new GameObject(name);
            lineObject.transform.SetParent(parent, false);
            var line = lineObject.AddComponent<LineRenderer>();
            line.sharedMaterial = ropeMaterial;
            line.numCapVertices = 1;
            line.startWidth = ropeRadius;
            line.endWidth = ropeRadius;
            line.positionCount = 0;
            return line;
        }

        public void DeleteRope()
        {
            ResetRope();
            if (hookRope != null)
                UnityEngine.Object.Destroy(hookRope.gameObject);
            if (holdRope != null)
                UnityEngine.Object.Destroy(holdRope.gameObject);
            hookRope = holdRope = null;
        }

        internal bool HasCast => hooked;
        internal Vector3 HookPoint => hookPoint;

        public void RopeUpdate(bool advanceThrow = true) => UpdateRope(Time.deltaTime, advanceThrow);

        private void UpdateRope(float dt, bool advanceThrow)
        {
            if (hookRope == null)
                return;

            if (advanceThrow && dt > 0f && ropeState == RopeState.Throwing)
                AdvanceThrow(dt);
            ApplyHookPose();

            if (dt <= 0f || ropeAttachTransformInBody == null)
                return;

            if (ropeState == RopeState.Swinging)
                currentRopeHoldPointTransform = ropeHoldTransformLeft;
            else if (!hooked)
                currentRopeHoldPointTransform = ropeHoldTransformRight;
            if (currentRopeHoldPointTransform == null)
                return;

            Vector3 hand = currentRopeHoldPointTransform.position;
            Vector3 hookEnd = ropeAttachPointToHook != null ? ropeAttachPointToHook.position : hookPoint;
            if (hooked)
            {
                hookRopeLength = Vector3.Distance(hand, hookPoint);
                if (ropeState is RopeState.Swinging or RopeState.Climbing)
                {
                    hookRope.positionCount = 2;
                    hookRope.SetPosition(0, hand);
                    hookRope.SetPosition(1, hookEnd);
                }
                else
                {
                    float length = Mathf.Max(hookRopeLength, Vector3.Distance(hand, hookEnd));
                    if (ropeState == RopeState.Normal)
                        length = Mathf.Max(length, Mathf.Min(totalRopeLength, length + 0.25f));
                    SimulateRope(hookPositions, hookPrevious, hookVelocities, hand, hookEnd, length, dt, true);
                    hookRope.positionCount = hookPositions.Length;
                    hookRope.SetPositions(hookPositions);
                }
            }

            Vector3 body = ropeAttachTransformInBody.position;
            Vector3 holdEnd = hooked || ropeAttachPointToHook == null ? hand : hookEnd;
            SimulateRope(holdPositions, holdPrevious, holdVelocities, body, holdEnd,
                Vector3.Distance(body, holdEnd) + 0.1f, dt, false);
            holdRope.SetPositions(holdPositions);
        }

        public void SetHookRopeAsStraight()
        {
            if (hookRope != null && hooked)
                hookRope.positionCount = 2;
        }

        private void SimulateRope(Vector3[] positions, Vector3[] previous, Vector3[] velocities,
            Vector3 start, Vector3 end, float length, float dt, bool checkCollisions)
        {
            Array.Copy(positions, previous, positions.Length);
            float velocityRetention = 1f - Mathf.Clamp01(dampening);
            int last = positions.Length - 1;
            for (int i = 1; i < last; i++)
            {
                Vector3 speed = velocities[i] + Physics.gravity * dt;
                positions[i] += speed * (velocityRetention * dt);
                if (checkCollisions)
                    ResolveSegmentCollision(ref positions[i]);
            }

            positions[0] = start;
            positions[last] = end;
            float segmentLength = length / last;
            for (int iteration = 0; iteration < ConstraintIterations; iteration++)
            {
                for (int i = 0; i < last; i++)
                {
                    Vector3 delta = positions[i + 1] - positions[i];
                    float distance = delta.magnitude;
                    if (distance <= 0.0001f)
                        continue;

                    Vector3 correction = delta * ((distance - segmentLength) / distance);
                    bool freeLeft = i > 0;
                    bool freeRight = i + 1 < last;
                    if (freeLeft)
                        positions[i] += freeRight ? correction * 0.5f : correction;
                    if (freeRight)
                        positions[i + 1] -= freeLeft ? correction * 0.5f : correction;
                }
            }

            for (int i = 1; i < last; i++)
            {
                positions[i] = Vector3.Lerp(positions[i], (positions[i - 1] + positions[i + 1]) * 0.5f, smoothingFactor);
                velocities[i] = (positions[i] - previous[i]) / dt;
            }
        }

        private void ResolveSegmentCollision(ref Vector3 position)
        {
            int count = Physics.OverlapSphereNonAlloc(position, ColliderRadius, ropeColliders,
                Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
            {
                var collider = ropeColliders[i];
                if (collider.transform.IsChildOf(playerTransform) || collider is MeshCollider { convex: false })
                    continue;

                Vector3 point = collider.ClosestPoint(position);
                Vector3 normal = (position - point).normalized;
                if (normal != Vector3.zero)
                {
                    position = point + normal * ColliderRadius;
                    return;
                }
            }
        }

        private void ResetHoldRope()
        {
            if (holdRope == null || ropeAttachTransformInBody == null || currentRopeHoldPointTransform == null)
                return;

            Vector3 end = ropeAttachPointToHook != null
                ? ropeAttachPointToHook.position
                : currentRopeHoldPointTransform.position;
            PlaceSegments(holdPositions, holdVelocities, ropeAttachTransformInBody.position, end);
            holdRope.positionCount = holdPositions.Length;
            holdRope.SetPositions(holdPositions);
        }

        private static void PlaceSegments(Vector3[] positions, Vector3[] velocities, Vector3 start, Vector3 end)
        {
            for (int i = 0; i < positions.Length; i++)
                positions[i] = Vector3.Lerp(start, end, (float)i / (positions.Length - 1));
            Array.Clear(velocities, 0, velocities.Length);
        }

        public void StartThrow(Transform startPoint, Vector3 endPoint)
        {
            if (hookRope == null || startPoint == null)
                return;

            if (!hooked && hookObject != null)
            {
                heldHookPosition = hookObject.localPosition;
                heldHookRotation = hookObject.localRotation;
                worldHookRotation = hookObject.rotation;
            }

            currentRopeHoldPointTransform = startPoint;
            throwStart = hookPoint = startPoint.position;
            throwTarget = endPoint;
            throwElapsed = 0f;
            hookRopeLength = 0f;
            hooked = true;
            ropeState = RopeState.Throwing;

            hookRopeResolution = Mathf.Max(2, hookRopeResolution);
            if (hookPositions == null || hookPositions.Length != hookRopeResolution)
            {
                hookPositions = new Vector3[hookRopeResolution];
                hookPrevious = new Vector3[hookRopeResolution];
                hookVelocities = new Vector3[hookRopeResolution];
            }
            PlaceSegments(hookPositions, hookVelocities, throwStart, throwStart);
            hookRope.positionCount = hookPositions.Length;
            hookRope.SetPositions(hookPositions);
            AdvanceThrow(0f);
            ApplyHookPose();
        }

        private void AdvanceThrow(float dt)
        {
            throwElapsed += dt;
            float progress = throwDuration > 0f ? Mathf.Clamp01(throwElapsed / throwDuration) : 1f;
            hookPoint = Vector3.Lerp(throwStart, throwTarget, progress) +
                Vector3.up * (throwHeight * 4f * progress * (1f - progress));
            if (progress >= 1f)
                ropeState = RopeState.Normal;
        }

        private void ApplyHookPose()
        {
            // Keep the equipment hierarchy intact and pin the thrown hook after player animation.
            if (hooked && hookObject != null)
                hookObject.SetPositionAndRotation(hookPoint, worldHookRotation);
        }

        public void ResetRope()
        {
            if (hooked && hookObject != null)
                hookObject.SetLocalPositionAndRotation(heldHookPosition, heldHookRotation);

            hooked = false;
            ropeState = RopeState.Normal;
            hookRopeLength = 0f;
            currentRopeHoldPointTransform = ropeHoldTransformRight;
            if (hookRope != null)
                hookRope.positionCount = 0;
            ResetHoldRope();
        }

        public bool HasReachedTarget() => hooked && ropeState != RopeState.Throwing;

        public void SetRopeState(RopeState newState) => ropeState = newState;

        internal void PresentHook(Vector3 point, RopeState state, bool newShot)
        {
            if (newShot && hooked)
                ResetRope();
            if (!hooked)
                StartThrow(ropeHoldTransformRight, point);
            hookPoint = point;
            ropeState = state;
        }
    }
}
