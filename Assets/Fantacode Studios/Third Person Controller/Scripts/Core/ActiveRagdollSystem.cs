using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace FS_Core
{
    [Serializable]
    public class BoneBlendOverride
    {
        public HumanBodyBones bone;
        [Range(0f, 1f)] public float blend = 1f;
    }

    [Serializable]
    public class BoneAnchor
    {
        public HumanBodyBones bone;
        public Transform target;
    }

    /// <summary>
    /// Active ragdoll system that blends skeleton pose between physics simulation and animation.
    ///
    /// Blend = 1 → bones follow the animator.
    /// Blend = 0 → bones follow physics (gravity, collisions, applied forces).
    ///
    /// Individual bones can be overridden with their own blend value (e.g. lower arms/legs
    /// always follow animation while the rest is physical), or pinned to a world transform
    /// (e.g. hand anchored to a wall while the rest of the body hangs).
    ///
    /// The blend is applied visually in LateUpdate — physics always simulates independently,
    /// so the feel of the fall/hang is always physically accurate. The blend only controls
    /// how much the rendered pose follows the animator vs the physics result.
    /// </summary>
    public class ActiveRagdollSystem : MonoBehaviour
    {
        [Header("Blend")]
        [SerializeField, Range(0f, 1f)] float blend = 1f;

        [Header("Per-Bone Blend Overrides")]
        [Tooltip("Override blend for individual bones. Useful for keeping limbs animated while the rest falls.")]
        [SerializeField] List<BoneBlendOverride> boneBlendOverrides = new();

        [Header("Bone Anchors")]
        [Tooltip("Pins a bone to a target transform each physics step. Use this to attach a hand or foot to the world.")]
        [SerializeField] List<BoneAnchor> boneAnchors = new();

        /// <summary>
        /// Global blend between physics (0) and animation (1).
        /// Per-bone overrides set via SetBoneBlend() or the inspector list take priority.
        /// </summary>
        public float Blend { get => blend; set => blend = Mathf.Clamp01(value); }

        public bool IsActive { get; private set; }

        /// <summary>
        /// True when at least one humanoid bone has a Rigidbody component. Use this to skip
        /// ragdoll-dependent logic (blending, force application, etc.) on characters that
        /// haven't had their ragdoll rigidbodies set up.
        /// </summary>
        public bool HasRagdollBones => bones.Count > 0;

        /// <summary>
        /// When true and ragdoll is active, the root transform continuously follows
        /// the hips position and derives rotation from the spine direction.
        /// Automatically enabled by <see cref="BlendToRagdoll"/> and disabled by <see cref="Deactivate"/>.
        /// </summary>
        public bool SyncRootToHips { get => syncRootToHips; set => syncRootToHips = value; }

        /// <summary>Fired the moment the ragdoll becomes active (physics takes over the bones).</summary>
        public Action OnRagdollStart;

        /// <summary>Fired once the ragdoll bones have settled (velocities below threshold). See <see cref="StartSettlingMonitor"/>.</summary>
        public Action OnSettled;

        /// <summary>Fired when the blend back to the animator (get-up) completes and the ragdoll is fully deactivated.</summary>
        public Action OnRecovered;

        [Header("Collision Filtering")]
        [Tooltip("While the ragdoll simulates, ignore contacts with other characters' ragdoll bones so two ragdolls never tangle and collapse into each other. " +
                 "All bones share the RagdollBone layer, so this also disables self-collision between this ragdoll's own bones — turn it off to get both back.")]
        [SerializeField] bool ignoreOtherRagdollsWhileActive = true;

        [Header("Settling Detection")]
        [SerializeField] float settledVelocityThreshold = 0.2f;
        [SerializeField] float minActiveTime = 2f;
        bool monitorSettling;
        float activeTimer;
        bool syncRootToHips;

        Animator animator;
        ActionExecutor actionExecutor;
        int ragdollBoneMask;
        Coroutine blendRoutine;
        Transform hips;
        Transform head;

        class BoneData
        {
            public HumanBodyBones humanBone;
            public Transform transform;
            public Rigidbody rigidbody;
            public Quaternion animWorldRot;
            public Vector3 animWorldPos;
            // Frozen ragdoll pose — captured when bones go kinematic for blend-back.
            // Used instead of rigidbody.position/rotation during blend because
            // kinematic rigidbodies sync to the animator and lose the ragdoll pose.
            public Vector3 frozenPos;
            public Quaternion frozenRot;

            public Collider collider;
        }

        bool usesFrozenPose;

        // Smooth root-rotation easing — opt-in via BlendToAnimatorAsync(smoothRootRotation: true), used
        // only by the carry. Eases a pickup clip's root-rotation OFFSET in over the blend instead of
        // letting OnAnimatorMove snap the root on the first frame (the "hips snap, then move to target"
        // carry glitch). No other blend sets the flag, so get-ups etc. are unaffected.
        bool easingRootRotation;
        bool easeRotTargetCaptured;
        float easeRotElapsed;
        float easeRotDuration;
        Quaternion easeRotFrom;
        Quaternion easeRotTarget;

        List<BoneData> bones = new();
        Dictionary<HumanBodyBones, float> runtimeBlendOverrides = new();
        Dictionary<HumanBodyBones, Transform> runtimeAnchors = new();

        void Awake()
        {
            animator = GetComponent<Animator>();
            actionExecutor = GetComponent<ActionExecutor>();
            int boneLayer = LayerMask.NameToLayer("RagdollBone");
            ragdollBoneMask = boneLayer >= 0 ? 1 << boneLayer : 0;
            BuildBoneList();
            hips = animator.GetBoneTransform(HumanBodyBones.Hips);
            head = animator.GetBoneTransform(HumanBodyBones.Head);
        }

        void Start()
        {
            // All ragdoll bodies start kinematic so the animator drives the character normally.
            SetAllKinematic(true);
        }

        void BuildBoneList()
        {
            bones.Clear();
            foreach (HumanBodyBones bone in Enum.GetValues(typeof(HumanBodyBones)))
            {
                if (bone == HumanBodyBones.LastBone) continue;
                var t = animator.GetBoneTransform(bone);
                if (t == null) continue;
                var rb = t.GetComponent<Rigidbody>();
                if (rb == null) continue;

                // Bone colliders must be solid (non-trigger) and enabled at all times —
                // see ApplyContactFilter. Normalise here so prefabs authored incorrectly
                // or saved by earlier revisions still register hits.
                var col = t.GetComponent<Collider>();
                if (col != null)
                {
                    col.isTrigger = false;
                    col.enabled = true;
                }

                bones.Add(new BoneData {collider = col, humanBone = bone, transform = t, rigidbody = rb });
            }
        }

        // ── Activation ───────────────────────────────────────────────────────────────

        /// <summary>
        /// Enable the active ragdoll. Physics begins simulating all bones.
        /// Call ApplyForce() immediately after to give the body an initial impulse direction.
        /// </summary>
        [ContextMenu("Activate Ragdoll")]
        public void Activate()
        {
            usesFrozenPose = false;
            if (IsActive)
            {
                SetAllKinematic(false);
                return;
            }
            IsActive = true;
            SetAllKinematic(false);
            OnRagdollStart?.Invoke();
        }

        /// <summary>
        /// Disable the active ragdoll. All bones return to kinematic and the animator drives the character.
        /// </summary>
        [ContextMenu("Deactivate Ragdoll")]
        public void Deactivate()
        {
            if (!IsActive) return;
            IsActive = false;
            monitorSettling = false;
            syncRootToHips = false;
            usesFrozenPose = false;
            easingRootRotation = false;
            runtimeAnchors.Clear();
            SetAllKinematic(true);
        }

        public void SetAllKinematic(bool kinematic)
        {
            foreach (var bd in bones)
            {
                if (bd.rigidbody == null) continue;
                bd.rigidbody.isKinematic = kinematic;
                ApplyContactFilter(bd, kinematic);
            }
        }

        /// <summary>
        /// Keeps bone colliders ENABLED in every state and suppresses unwanted physical
        /// interaction through <see cref="Collider.excludeLayers"/> instead of by disabling them.
        ///
        /// Disabling the colliders while kinematic made a living character unshootable: AI body
        /// capsules are flipped to isTrigger by CombatAIController and every bullet cast runs with
        /// QueryTriggerInteraction.Ignore, so the ragdoll bones are the only thing a hit-scan can
        /// land on. excludeLayers filters CONTACT generation only — scene queries (Raycast /
        /// SphereCast / Overlap) still hit an excluded collider — so hit detection works while the
        /// physics behaviour stays exactly as it was.
        ///
        /// Kinematic (animator-driven) → exclude every layer. The collider becomes a pure hit
        /// target: it can't shove props, and it can't act as an immovable moving wall that kicks a
        /// nearby character's simulating ragdoll apart.
        /// Simulating → exclude the RagdollBone layer (see <see cref="ignoreOtherRagdollsWhileActive"/>)
        /// so the body still collides with the world but never tangles with another ragdoll.
        /// </summary>
        void ApplyContactFilter(BoneData bd, bool kinematic)
        {
            if (bd.collider == null) return;

            bd.collider.enabled = true;
            bd.collider.isTrigger = false;
            bd.collider.excludeLayers = kinematic
                ? ~0
                : (ignoreOtherRagdollsWhileActive ? ragdollBoneMask : 0);
        }

        // ── FixedUpdate: anchor bones ─────────────────────────────────────────────────

        void FixedUpdate()
        {
            if (!IsActive) return;

            foreach (var bd in bones)
            {
                var anchorTarget = GetAnchorTarget(bd.humanBone);
                bool shouldBeKinematic = anchorTarget != null;

                // Keep kinematic state in sync with anchor presence. The collider stays
                // enabled and solid throughout — raycasts (bullets) must always hit ragdoll
                // bones regardless of kinematic state; only contact filtering changes.
                if (bd.rigidbody.isKinematic != shouldBeKinematic)
                {
                    bd.rigidbody.isKinematic = shouldBeKinematic;
                    ApplyContactFilter(bd, shouldBeKinematic);
                }

                if (shouldBeKinematic)
                {
                    bd.rigidbody.MovePosition(anchorTarget.position);
                    bd.rigidbody.MoveRotation(anchorTarget.rotation);
                }
            }

            if (monitorSettling)
            {
                activeTimer += Time.fixedDeltaTime;
                if (activeTimer < minActiveTime) return;

                float sqrThreshold = settledVelocityThreshold * settledVelocityThreshold;
                foreach (var bd in bones)
                {
                    if (bd.rigidbody == null || bd.rigidbody.isKinematic) continue;
                    if (bd.rigidbody.linearVelocity.sqrMagnitude > sqrThreshold) return;
                }

                monitorSettling = false;
                OnSettled?.Invoke();
            }
        }

        // ── LateUpdate: blend visual output ──────────────────────────────────────────

        void LateUpdate()
        {
            // Runs AFTER OnAnimatorMove, so transform.rotation here already includes the pickup clip's
            // root-rotation offset. Ease it in from the pre-carry rotation instead of letting it snap.
            if (easingRootRotation) EaseRootRotation();

            if (!IsActive) return;

            // Cache each bone's current pose (set by the animator if enabled,
            // or the last blended value if animator is off) before overwriting.
            foreach (var bd in bones)
            {
                bd.animWorldRot = bd.transform.rotation;
                bd.animWorldPos = bd.transform.position;
            }

            foreach (var bd in bones)
            {
                if (GetAnchorTarget(bd.humanBone) != null) continue;

                float effectiveBlend = GetEffectiveBlend(bd.humanBone);

                // During blend-back, use the frozen ragdoll pose (captured before
                // bones went kinematic) instead of rigidbody positions, which now
                // track the animator and would make the lerp a no-op.
                Vector3 physPos = usesFrozenPose ? bd.frozenPos : bd.rigidbody.position;
                Quaternion physRot = usesFrozenPose ? bd.frozenRot : bd.rigidbody.rotation;

                bd.transform.rotation = Quaternion.Slerp(physRot, bd.animWorldRot, effectiveBlend);
                bd.transform.position = Vector3.Lerp(physPos, bd.animWorldPos, effectiveBlend);
            }

            // Continuously sync root transform to follow hips during ragdoll.
            if (syncRootToHips && hips != null)
                SyncRootToHipsPosition();
        }

        /// <summary>
        /// Eases the root rotation from the pre-carry rotation (<see cref="easeRotFrom"/>) to the pickup
        /// clip's offset rotation over <see cref="easeRotDuration"/>. Runs in LateUpdate, after
        /// OnAnimatorMove has applied the clip's root rotation, so it can capture the offset target and
        /// then slerp toward it instead of letting it snap on the first frame. It holds the start rotation
        /// until the offset actually appears (jump &gt; 0.5°), and times out so a clip WITHOUT an offset
        /// is never held.
        /// </summary>
        void EaseRootRotation()
        {
            easeRotElapsed += Time.deltaTime;

            if (!easeRotTargetCaptured)
            {
                if (Quaternion.Angle(transform.rotation, easeRotFrom) > 0.5f)
                {
                    easeRotTarget = transform.rotation;   // full offset (clip has transitionIn = 0)
                    easeRotTargetCaptured = true;
                    easeRotElapsed = 0f;
                }
                else
                {
                    transform.rotation = easeRotFrom;                 // hold until the offset appears
                    if (easeRotElapsed > 0.5f) easingRootRotation = false;   // no offset → stop holding
                    return;
                }
            }

            float t = easeRotDuration > 0f ? Mathf.Clamp01(easeRotElapsed / easeRotDuration) : 1f;
            transform.rotation = Quaternion.Slerp(easeRotFrom, easeRotTarget, t);
            if (t >= 1f) easingRootRotation = false;
        }

        /// <summary>
        /// Moves the root transform to the hips position and derives rotation from
        /// the hip→head spine direction. Saves and restores bone world poses so
        /// moving the parent doesn't drag the physics-driven children.
        /// </summary>
        void SyncRootToHipsPosition()
        {
            SaveBonePoses();

            // Follow hips position every frame. No rotation change.
            transform.position = hips.position;

            RestoreBonePoses();
        }

        // ── Public API ────────────────────────────────────────────────────────────────

        /// <summary>
        /// Override the blend for a single bone. Useful for keeping specific limbs animated
        /// (e.g. lower arms/legs at blend=1) while the rest of the body is physics-driven.
        /// Pass a value less than 0 to clear the override and fall back to the global blend.
        /// </summary>
        public void SetBoneBlend(HumanBodyBones bone, float value)
        {
            if (value < 0f)
                runtimeBlendOverrides.Remove(bone);
            else
                runtimeBlendOverrides[bone] = Mathf.Clamp01(value);
        }

        public void ClearBoneBlend(HumanBodyBones bone) => runtimeBlendOverrides.Remove(bone);

        /// <summary>
        /// Pins a bone to a target transform. The bone becomes kinematic and follows the target
        /// each physics step. Useful for anchoring a hand/foot to a wall while the body hangs.
        /// </summary>
        public void SetAnchor(HumanBodyBones bone, Transform target) => runtimeAnchors[bone] = target;

        public void ClearAnchor(HumanBodyBones bone) => runtimeAnchors.Remove(bone);

        /// <summary>
        /// Apply a force or impulse to a specific bone's rigidbody.
        /// Call this right after Activate() to launch the body in a direction.
        /// </summary>
        public void ApplyForce(HumanBodyBones bone, Vector3 force, ForceMode mode = ForceMode.Impulse)
        {
            var rb = GetRigidbody(bone);
            if (rb != null && !rb.isKinematic)
                rb.AddForce(force, mode);
        }

        /// <summary>
        /// Zeros velocity and angular velocity on all active ragdoll bones.
        /// Call before <see cref="ApplyForce"/> to cancel any physics forces
        /// that were applied by collision responses (e.g. vehicle impact).
        /// </summary>
        public void ZeroAllVelocities()
        {
            foreach (var bd in bones)
            {
                if (bd.rigidbody == null || bd.rigidbody.isKinematic) continue;
                bd.rigidbody.linearVelocity = Vector3.zero;
                bd.rigidbody.angularVelocity = Vector3.zero;
            }
        }

        /// <summary>
        /// Begins monitoring all ragdoll bones for settling. Once all bones' velocities
        /// fall below the threshold (after minActiveTime), OnSettled is invoked.
        /// </summary>
        public void StartSettlingMonitor()
        {
            monitorSettling = true;
            activeTimer = 0f;
        }

        /// <summary>
        /// Captures world position and rotation of every ragdoll bone.
        /// Call before moving the root transform, then call RestoreBonePoses() after.
        /// </summary>
        public void SaveBonePoses()
        {
            if (savedPoses == null || savedPoses.Length != bones.Count)
                savedPoses = new (Vector3 pos, Quaternion rot)[bones.Count];

            for (int i = 0; i < bones.Count; i++)
                savedPoses[i] = (bones[i].transform.position, bones[i].transform.rotation);
        }

        /// <summary>
        /// Restores bone world positions and rotations saved by SaveBonePoses().
        /// </summary>
        public void RestoreBonePoses()
        {
            if (savedPoses == null) return;
            for (int i = 0; i < bones.Count && i < savedPoses.Length; i++)
            {
                bones[i].transform.position = savedPoses[i].pos;
                bones[i].transform.rotation = savedPoses[i].rot;
            }
        }

        (Vector3 pos, Quaternion rot)[] savedPoses;

        /// <summary>Returns the Rigidbody for a given humanoid bone, or null if not found.</summary>
        public Rigidbody GetRigidbody(HumanBodyBones bone)
        {
            foreach (var bd in bones)
                if (bd.humanBone == bone) return bd.rigidbody;
            return null;
        }

        // ── Helpers ───────────────────────────────────────────────────────────────────

        float GetEffectiveBlend(HumanBodyBones bone)
        {
            if (runtimeBlendOverrides.TryGetValue(bone, out float rt)) return rt;
            foreach (var ov in boneBlendOverrides)
                if (ov.bone == bone) return ov.blend;
            return blend;
        }

        Transform GetAnchorTarget(HumanBodyBones bone)
        {
            if (runtimeAnchors.TryGetValue(bone, out var t) && t != null) return t;
            foreach (var a in boneAnchors)
                if (a.bone == bone && a.target != null) return a.target;
            return null;
        }


        public Coroutine BlendToRagdoll(bool syncRoot = true)
        {
            if (!HasRagdollBones) return null;

            return StartBlend(true, .1f, null, syncRoot);
        }

        public Coroutine BlendToAnimator(CharacterAction anim = null)
        {
            return StartBlend(false, .4f, anim);
        }

        public IEnumerator BlendToRagdollAsync(bool syncRoot = true)
        {
            if (!HasRagdollBones) yield break;

            if (IsActive)
            {
                blend = 0f;
                if (animator != null) animator.enabled = false;
                syncRootToHips = syncRoot;
                yield break;
            }
            yield return BlendRoutine(true, .1f, null, null, syncRoot);
        }

        public IEnumerator BlendToAnimatorAsync(CharacterAction anim = null, Action onAligned = null, bool smoothRootRotation = false)
        {
            yield return BlendRoutine(false, .4f, anim, onAligned, smoothRootRotation: smoothRootRotation);
        }

        Coroutine StartBlend(bool toRagdoll, float duration = .3f, CharacterAction anim = null, bool syncRoot = true)
        {
            if (toRagdoll && IsActive)
            {
                blend = 0f;
                if (animator != null) animator.enabled = false;
                syncRootToHips = syncRoot;
                return null;
            }

            if (blendRoutine != null)
                StopCoroutine(blendRoutine);

            blendRoutine = StartCoroutine(BlendRoutine(toRagdoll, duration, anim, null, syncRoot));
            return blendRoutine;
        }

        IEnumerator BlendRoutine(bool toRagdoll, float duration = .3f, CharacterAction anim = null, Action onAligned = null, bool syncRoot = true, bool smoothRootRotation = false)
        {
            yield return new WaitForFixedUpdate();
            bool animCompleted = true;
            if (anim != null)
            {
                animCompleted = false;
                anim.animationParameters.OnComplete = () => { animCompleted = true; };
                actionExecutor.Execute(anim);
            }

            // Cache the aligned root pose so we can restore it after the animator
            // enables — the animator's first evaluation may shift the root via root motion.
            Vector3 alignedPos = transform.position;
            Quaternion alignedRot = transform.rotation;

            if (!toRagdoll)
            {
                AlignAnimatorToRagdoll();
                alignedPos = transform.position;
                alignedRot = transform.rotation;
            }

            // Start easing the root rotation from the POST-align pose. The clip was just started but its
            // root-rotation offset lands via OnAnimatorMove on the next frame; EaseRootRotation captures
            // it then and slerps to it, so only the clip's offset is smoothed — never the align above.
            if (smoothRootRotation)
            {
                easingRootRotation = true;
                easeRotTargetCaptured = false;
                easeRotElapsed = 0f;
                easeRotDuration = duration;
                easeRotFrom = transform.rotation;
            }

            yield return BlendPosition(toRagdoll, duration, onAligned, syncRoot, alignedPos, alignedRot);
            blendRoutine = null;

            yield return new WaitUntil(() => animCompleted);
        }


        void AlignAnimatorToRagdoll()
        {
            // ── Rotation ──────────────────────────────────────────────────────────
            // Read from RIGIDBODIES — transforms may be stale from SaveBonePoses/RestoreBonePoses.
            var hipsRb = GetRigidbody(HumanBodyBones.Hips);
            var headRb = GetRigidbody(HumanBodyBones.Head);

            if (hipsRb != null && headRb != null)
            {
                Vector3 spineDir = Vector3.ProjectOnPlane(headRb.position - hipsRb.position, Vector3.up);
                if (spineDir.sqrMagnitude > 0.001f)
                {
                    bool faceDown = Vector3.Dot(hipsRb.rotation * Vector3.forward, Vector3.up) < 0f;
                    Vector3 forward = spineDir.normalized;
                    transform.rotation = faceDown
                        ? Quaternion.LookRotation(forward, Vector3.up)
                        : Quaternion.LookRotation(-forward, Vector3.up);
                }
            }

            // ── Position ──────────────────────────────────────────────────────────
            Vector3 hipsPos = hipsRb != null ? hipsRb.position : hips.position;
            Vector3 rayOrigin = hipsPos + Vector3.up * 0.5f;
            if (Physics.Raycast(rayOrigin, Vector3.down, out RaycastHit hit, 5.5f,
                    FSSettings.i.GroundLayer, QueryTriggerInteraction.Ignore))
            {
                transform.position = new Vector3(hipsPos.x, hit.point.y, hipsPos.z);
            }
            else
            {
                transform.position = hipsPos;
            }
        }
        IEnumerator BlendPosition(bool toRagdoll, float duration = .3f, Action onAligned = null, bool syncRoot = true,
            Vector3 lockedPos = default, Quaternion lockedRot = default)
        {
            float startBlend = blend;
            float targetBlend = toRagdoll ? 0f : 1f;
            float elapsed = 0f;


            if (toRagdoll)
            {
                Activate();
            }
            else
            {
                syncRootToHips = false;

                // Capture the ragdoll pose BEFORE freezing bones.
                // Once kinematic, rigidbodies sync to the animator and lose the ragdoll pose.
                foreach (var bd in bones)
                {
                    bd.frozenPos = bd.rigidbody.position;
                    bd.frozenRot = bd.rigidbody.rotation;
                }
                usesFrozenPose = true;

                SetAllKinematic(true);
                animator.enabled = true;
                transform.SetPositionAndRotation(lockedPos, lockedRot);

                elapsed += Time.deltaTime;
                blend = Mathf.Lerp(startBlend, targetBlend, elapsed / duration);
                yield return null;

                onAligned?.Invoke();
            }

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                blend = Mathf.Lerp(startBlend, targetBlend, elapsed / duration);

                // Keep the root locked during blend-back so root motion
                // from the get-up animation doesn't shift it mid-blend.
                if (!toRagdoll)
                    transform.SetPositionAndRotation(lockedPos, lockedRot);

                yield return null;
            }

            blend = targetBlend;

            if (toRagdoll)
            {
                animator.enabled = false;
                syncRootToHips = syncRoot;
            }
            else
            {
                Deactivate();
                // Ragdoll get-up finished — the animator is fully back in control.
                OnRecovered?.Invoke();
            }
        }
    }
}
