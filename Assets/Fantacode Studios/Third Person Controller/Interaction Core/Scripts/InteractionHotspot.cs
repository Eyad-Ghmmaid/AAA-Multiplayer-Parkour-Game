using FS_Core;
using FS_Util;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;


#if UNITY_EDITOR
using UnityEditor;
#endif
using UnityEngine;

namespace FS_InteractionSystem
{
    public class InteractionHotspot : Hotspot
    {
        public bool disableObjectAfterCompletion = false;
        [ShowIf("disableObjectAfterCompletion", false)]
        public bool disableHotspotAfterCompletion = true;

        public bool alignToHotspot = true;
        public virtual bool AlignToHotspot => alignToHotspot;
        public bool snapToPosition = false;

        /// <summary>
        /// Called by <see cref="HotspotController"/> when the executor is currently carrying an object.
        /// Return true to keep this hotspot visible while carrying; false to suppress it (default).
        /// Override in subclasses that have carry-aware interactions (e.g. dump spots).
        /// </summary>
        public virtual bool CanInteractWhileCarrying(InteractionExecutor executor) => false;

        public bool useStoppingDistance = false;
        [ShowIf("useStoppingDistance", true)]
        public float stoppingDistance = 1.5f;

        [ShowIf("useStoppingDistance", false)]
        public List<InteractionPoint> interactionPoints = new List<InteractionPoint>() { new InteractionPoint(new Vector3(.5f, 0, .5f),0)};

        [SerializeField, Tooltip("Radius of the capsule used for the space check.")]
        float spaceCheckRadius = .05f;

        [SerializeField, Tooltip("Height of the capsule used for the space check.")]
        float spaceCheckHeight = 1.8f;

        public bool InteractionRunning { get; set; } = false;

        public InteractionExecutor CurrentInteractionExecutor { get; set; }

        public Action OnLockOnInteractioin;

        protected override void OnEnable()
        {
            base.OnEnable();
            OnStart += StartInteraction;
            OnComplete += CompleteInteraction;
        }

        private void OnDisable()
        {
            OnStart -= StartInteraction;
            OnComplete -= CompleteInteraction;
        }

        void StartInteraction()
        {
            InteractionRunning = true;
        }

        void CompleteInteraction()
        {
            if (disableObjectAfterCompletion)
            {
                gameObject.SetActive(false);
            }
            else if (disableHotspotAfterCompletion)
            {
                enabled = false;
            }
            InteractionRunning = false;
        }


        public override bool CanInteract()
        {
            if (InteractionRunning) return false;
            if (!HasSpaceAtInteractionPoint()) return false;
            return true;
        }

        /// <summary>
        /// Checks if there is enough space at the closest interaction point
        /// for a character to stand. Uses a capsule overlap test.
        /// </summary>
        bool HasSpaceAtInteractionPoint()
        {
            if (CurrentInteractionExecutor != null)
            {
                var (pos, _) = GetBestInteractionPoint(CurrentInteractionExecutor.transform.position);
                return HasSpaceAt(pos);
            }

            // No executor known yet — check all interaction points.
            if (useStoppingDistance)
            {
                // Can't check without knowing the interactor position for stopping distance mode.
                return true;
            }

            foreach (var point in interactionPoints)
            {
                if (HasSpaceAt(GetInteractionPosition(point)))
                    return true;
            }
            return false;
        }

        bool HasSpaceAt(Vector3 position)
        {
            float radius = spaceCheckRadius;
            Vector3 bottom = position + Vector3.up * (radius + 0.05f);
            Vector3 top = position + Vector3.up * (spaceCheckHeight - radius);

            var overlaps = Physics.OverlapCapsule(bottom, top, radius,
                FSSettings.i.InteractionBlockingLayers, QueryTriggerInteraction.Ignore);
            foreach (var col in overlaps)
            {

                // Ignore the hotspot's own colliders and its parent hierarchy.
                if (col.transform.IsChildOf(transform)) continue;
                if (transform.IsChildOf(col.transform)) continue;

                // Ignore the interactor themselves.
                if (CurrentInteractionExecutor != null && col.transform.IsChildOf(CurrentInteractionExecutor.transform))
                    continue;

                return false;
            }
            return true;
        }

        public virtual IEnumerator Interact(InteractionExecutor interactionExecutor)
        {
            yield break;
        }

        public override void Interact(HotspotController detector)
        {
            var executor = detector.GetComponent<InteractionExecutor>();
            if (executor != null)
                executor.TryExecute(this);
        }

        public override void OnGainFocus(HotspotController detector)
        {
            CurrentInteractionExecutor = detector.GetComponent<InteractionExecutor>();
        }

        public override void OnLoseFocus(HotspotController detector)
        {
            // Keep the executor while an interaction is in progress. Once it starts, InteractionRunning
            // makes CanInteract() return false, so DetectHotspots drops this hotspot and fires
            // OnLoseFocus mid-interaction. Clearing the executor here would break action-specific
            // alignment (e.g. dump / pull-out points fall back to the enter points). It is restored on
            // the next focus change once the interaction ends.
            if (InteractionRunning) return;
            CurrentInteractionExecutor = null;
        }


        // ── Interaction point frame ──────────────────────────────────────────────────
        // Authored offsets are relative to a frame, and that frame must be IDENTICAL while
        // authoring and at runtime — otherwise every point visibly jumps on entering play by
        // however far the two frames are apart.
        //
        // Two code paths:
        //
        //   1. Humanoid ragdoll (hipBone != null)
        //      Pivot  = hipBone.position  (root transform is NOT at the physics position)
        //      Facing = ProjectOnPlane(head - hips, up) → LookRotation(-fwd)
        //      Matches the convention in ActiveRagdollSystem.AlignAnimatorToRagdoll. A ragdoll
        //      has no meaningful edit-time pose, so this frame exists only at runtime.
        //
        //   2. Everything else — static props, loose rigidbodies, and hotspots parented under a
        //      moving body such as a vehicle seat.
        //      Pivot  = this hotspot's own transform. It already tracks whatever it is parented
        //               to, so moved / rotated / tumbling objects are handled without looking up
        //               a Rigidbody — and, crucially, it is the same pivot the Scene view draws.
        //      Facing = the hotspot's forward projected onto XZ, so a tipped object still yields
        //               upright, grounded points. Identical to transform.rotation when upright.
        //      Offsets are scaled by lossyScale, making this frame exactly
        //      transform.TransformPoint(offset) for an upright object.
        //
        // Deriving the pivot from a parent Rigidbody instead (the previous behaviour) broke as
        // soon as the hotspot was not ON that body: a vehicle seat sits inside the cabin, about
        // a metre from the car's root, so its interaction point was authored against the seat
        // and then evaluated against the car root.

        Transform hipBone;
        Transform headBone;

        protected override void Start()
        {
            base.Start();
            var anim = GetComponentInParent<Animator>();
            if (anim != null && anim.isHuman)
            {
                hipBone = anim.GetBoneTransform(HumanBodyBones.Hips);
                headBone = anim.GetBoneTransform(HumanBodyBones.Head);
            }
        }

        /// <summary>
        /// Stable horizontal rotation derived from the hip → head bone direction,
        /// projected onto the XZ plane. Matches the pivot convention used by
        /// ActiveRagdollSystem.AlignAnimatorToRagdoll so that authored offsets and
        /// the animation pivot always refer to the same reference frame.
        /// </summary>
        Quaternion GetBodyHorizontalRotation()
        {
            Vector3 fwd = Vector3.ProjectOnPlane(headBone.position - hipBone.position, Vector3.up);

            // Degenerate: body is exactly vertical. Fall back to the root transform's
            // horizontal forward, which is stable (root doesn't move during ragdoll).
            if (fwd.sqrMagnitude < 0.001f)
            {
                fwd = Vector3.ProjectOnPlane(headBone.parent
                          ? headBone.parent.forward : Vector3.forward, Vector3.up);
                if (fwd.sqrMagnitude < 0.001f) fwd = Vector3.forward;
            }

            // Mirror AlignAnimatorToRagdoll: canonical +Z is toward feet (-fwd).
            return Quaternion.LookRotation(-fwd.normalized, Vector3.up);
        }

        /// <summary>
        /// Horizontal-only rotation of this hotspot. Strips any tilt physics introduced so
        /// interaction points stay upright and grounded even when the object tips over.
        /// Exactly <c>transform.rotation</c> while the object is upright, which is the state
        /// it is authored in.
        /// </summary>
        Quaternion GetHorizontalRotation()
        {
            Vector3 fwd = Vector3.ProjectOnPlane(transform.forward, Vector3.up);

            // Degenerate: pointing straight up or down. Derive the yaw from the object's own
            // up axis instead, so a tipped object keeps its heading rather than snapping to
            // world forward.
            if (fwd.sqrMagnitude < 0.001f)
            {
                fwd = Vector3.ProjectOnPlane(transform.up, Vector3.up);
                if (fwd.sqrMagnitude < 0.001f) fwd = Vector3.forward;
            }
            return Quaternion.LookRotation(fwd.normalized, Vector3.up);
        }

        /// <summary>
        /// World position of an interaction point BEFORE the ground snap — the raw frame that
        /// authored offsets live in. The Scene view draws points with this while editing, so
        /// what is authored is what the interactor gets.
        /// </summary>
        public Vector3 GetInteractionPositionRaw(InteractionPoint point)
        {
            if (hipBone != null)
                // Offset is expressed in body-canonical space (same frame as the
                // animation pivot) and applied from the hip's physics world position.
                return hipBone.position + GetBodyHorizontalRotation() * point.positionOffset;

            // Pivot on this hotspot's own transform — it already tracks whatever body it is
            // parented to. Scaling by lossyScale makes this equal transform.TransformPoint
            // for an upright object.
            return transform.position
                   + GetHorizontalRotation() * Vector3.Scale(point.positionOffset, transform.lossyScale);
        }

        public Vector3 GetInteractionPosition(InteractionPoint point)
        {
            Vector3 p = GetInteractionPositionRaw(point);

            // Ground-snap: cast from 2 m above so the player stands on the surface,
            // not inside a slope or step.
            return Physics.Raycast(p + Vector3.up * 2f, Vector3.down, out RaycastHit hit, 4f,
                       FSSettings.i.GroundLayer, QueryTriggerInteraction.Ignore)
                   ? hit.point : p;
        }

        public Quaternion GetInteractionRotation(InteractionPoint point)
        {
            if (hipBone != null)
                return GetBodyHorizontalRotation() * Quaternion.Euler(0, point.facingDirection, 0);
            return GetHorizontalRotation() * Quaternion.Euler(0, point.facingDirection, 0);
        }

        /// <summary>
        /// Returns the physics-driven world pivot of this object.
        /// Used by both GetBestInteractionPoint (stopping distance mode) and the editor visualizer.
        /// </summary>
        public Vector3 GetPivotPosition()
        {
            if (hipBone != null) return hipBone.position;
            return transform.position;
        }

        public virtual (Vector3, Quaternion) GetBestInteractionPoint(Vector3 interactorPos, List<InteractionPoint> points = null)
        {
            if (useStoppingDistance)
            {
                Vector3 pivot = GetPivotPosition();

                // Direction from pivot to interactor, horizontal only.
                Vector3 dir = Vector3.ProjectOnPlane(interactorPos - pivot, Vector3.up);
                if (dir.sqrMagnitude < 0.001f) dir = Vector3.forward;
                dir.Normalize();

                // Stop at stoppingDistance from the pivot.
                Vector3 pos = pivot + dir * stoppingDistance;

                // Ground-snap the stop position.
                if (Physics.Raycast(pos + Vector3.up * 2f, Vector3.down, out RaycastHit hit, 4f,
                        FSSettings.i.GroundLayer, QueryTriggerInteraction.Ignore))
                    pos = hit.point;

                // Face toward the pivot.
                Vector3 lookDir = Vector3.ProjectOnPlane(pivot - pos, Vector3.up);
                if (lookDir.sqrMagnitude < 0.001f) lookDir = -dir;
                Quaternion rot = Quaternion.LookRotation(lookDir.normalized, Vector3.up);

                return (pos, rot);
            }
            var _points = points == null?interactionPoints.ToList():points.ToList();
            return GetBestFromPoints(_points, interactorPos);
        }
        

        /// <summary>
        /// Returns the closest point (world position + rotation) from <paramref name="points"/> to
        /// the interactor. Subclasses use this to align to action-specific interaction point sets.
        /// </summary>
        protected (Vector3, Quaternion) GetBestFromPoints(List<InteractionPoint> points, Vector3 interactorPos)
        {
            InteractionPoint closestPoint = null;
            float shortestDistance = Mathf.Infinity;
            if (points != null)
            {
                foreach (var point in points)
                {
                    // Distance is checked against the actual computed world position so the
                    // closest point is accurate even when the ragdoll has slid or rotated.
                    float distance = Vector3.Distance(GetInteractionPosition(point), interactorPos);
                    if (distance < shortestDistance)
                    {
                        closestPoint = point;
                        shortestDistance = distance;
                    }
                }
            }
            if (closestPoint == null)
                return (GetPivotPosition(), transform.rotation);
            return (GetInteractionPosition(closestPoint), GetInteractionRotation(closestPoint));
        }


        /// <summary>
        /// Converts a world-space position back into the positionOffset space used by
        /// InteractionPoint — the exact inverse of <see cref="GetInteractionPositionRaw"/>, so
        /// dragging a point handle round-trips without drift.
        /// </summary>
        public Vector3 WorldToOffsetSpace(Vector3 worldPos)
        {
            if (hipBone != null)
                return Quaternion.Inverse(GetBodyHorizontalRotation()) * (worldPos - hipBone.position);

            Vector3 local = Quaternion.Inverse(GetHorizontalRotation()) * (worldPos - transform.position);
            Vector3 scale = transform.lossyScale;
            return new Vector3(
                Mathf.Approximately(scale.x, 0f) ? local.x : local.x / scale.x,
                Mathf.Approximately(scale.y, 0f) ? local.y : local.y / scale.y,
                Mathf.Approximately(scale.z, 0f) ? local.z : local.z / scale.z);
        }

        /// <summary>
        /// Converts a world-space rotation back into the facingDirection (degrees) used by InteractionPoint.
        /// </summary>
        public float WorldToFacingDirection(Quaternion worldRot)
        {
            if (hipBone != null)
                return (Quaternion.Inverse(GetBodyHorizontalRotation()) * worldRot).eulerAngles.y;
            return (Quaternion.Inverse(GetHorizontalRotation()) * worldRot).eulerAngles.y;
        }

    }


    [System.Serializable]
    public class InteractionPoint
    {
        public Vector3 positionOffset = new Vector3(0, 0, 1);
        public float facingDirection = 180;

        public InteractionPoint(Vector3 po,  float fd = 180)
        {
            positionOffset = po;
            facingDirection = fd;
        }
    }


#if UNITY_EDITOR

    [CustomEditor(typeof(InteractionHotspot), true)]
    public class InteractionHotspotEditor : Editor
    {
        // Draw through duringSceneGui (not the gated OnSceneGUI) so the interaction-point gizmos —
        // spheres, arrows and the Move/Rotate handles — stay visible even when the Scene view's
        // Gizmos toggle is OFF, matching the concealment / occupiable spots. Registered only while a
        // hotspot is selected (editor OnEnable/OnDisable). OccupiableHotspot types use their own
        // more-derived editor, so this never double-draws for them.
        protected virtual void OnEnable()  { SceneView.duringSceneGui += DuringSceneGui; }
        protected virtual void OnDisable() { SceneView.duringSceneGui -= DuringSceneGui; }

        static readonly Color[] pointColors = { Color.cyan, Color.yellow, Color.green, new Color(1f, 0.5f, 0f) };

        protected virtual void DuringSceneGui(SceneView sceneView)
        {
            var comp = target as InteractionHotspot;
            if (comp == null) return;

            bool editable = !Application.isPlaying;
            if (!editable) sceneView.Repaint();   // track live pivots in play mode

            if (comp.useStoppingDistance)
            {
                InteractionPointGizmo.DrawStoppingDisc(comp.GetPivotPosition(), comp.stoppingDistance, Color.cyan, "");
                return;
            }

            var points = comp.interactionPoints;
            if (points != null)
                for (int i = 0; i < points.Count; i++)
                    DrawPoint(comp, points[i], i);

            // Snap the moved point to the nearest surface once the drag is released.
            InteractionPointGizmo.ProcessDeferredSnap(sceneView);
        }

        void DrawPoint(InteractionHotspot comp, InteractionPoint point, int index)
        {
            bool editable = !Application.isPlaying;

            // Same frame while editing and while playing — the authored gizmo IS where the
            // interactor ends up. Play mode additionally shows the ground-snapped result.
            Vector3 worldPos = editable
                ? comp.GetInteractionPositionRaw(point)
                : comp.GetInteractionPosition(point);
            Quaternion worldRot = comp.GetInteractionRotation(point);

            InteractionPointGizmo.DrawPoint(
                comp, comp.transform,
                worldPos, worldRot,
                w => point.positionOffset = comp.WorldToOffsetSpace(w),
                r => point.facingDirection = comp.WorldToFacingDirection(r),
                pointColors[index % pointColors.Length], $"[{index}]", editable, snapToSurface: true);
        }
    }
#endif
}
