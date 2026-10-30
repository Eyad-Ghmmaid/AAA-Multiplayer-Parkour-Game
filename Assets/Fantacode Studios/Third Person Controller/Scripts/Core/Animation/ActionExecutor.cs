using FS_Util;
using System;
using System.Collections;
using UnityEngine;

namespace FS_Core
{
    [RequireComponent(typeof(AnimGraph))]
    public class ActionExecutor : MonoBehaviour
    {
        CharacterAction currentAction;
        public CharacterAction CurrentAction => currentAction;

        // Output
        public bool IsBusy => currentAction != null;
        public bool BlockLocomotion => currentAction != null && currentAction.blocksLocomotion;
        public bool IgnoreCollision => currentAction != null && currentAction.ignoreCollision;
        public bool UsesRootMotion => currentAction != null && currentAction.usesRootMotion;
        public bool UsesRootMovement => currentAction != null && currentAction.usesRootMovement;
        public bool UsesRootRotation => currentAction != null && currentAction.usesRootRotation;
        public bool UsesGravity => currentAction != null && (currentAction.usesGravity 
            ||  (currentAction.isAnimGraphAction && currentAction.clipInfo != null && currentAction.clipInfo.useGravity));
        public bool RequiresLedgeBlocking => currentAction != null && currentAction.requiresLedgeBlocking;
        public bool IsInSyncedAnimation => currentAction != null && currentAction.isSyncedAnimation;
        public bool CanLeaveNavMesh => currentAction != null && currentAction.canLeaveNavMesh;

        public AnimGraph AnimGraph => animGraph;
        public string CurrentClipName => GetCurrentClipName();
        public bool IsMotionWarping { get; private set; }
        public Vector3 MotionWarpingDeltaPos { get; private set; }

        // Fired whenever currentAction transitions from null/other → newAction (Started)
        // or from oldAction → null/other (Ended). Subscribers (e.g. NavmeshHybridDriver)
        // use this to block/unblock the NavMeshAgent for the lifetime of any action.
        public event Action<CharacterAction> OnActionStarted;
        public event Action<CharacterAction> OnActionEnded;

        AnimGraph animGraph;
        Animator animator;
        private void Awake()
        {
            animGraph = GetComponent<AnimGraph>();
            animator = GetComponent<Animator>();
        }

        public void Execute(CharacterAction action)
        {
            if (IsInSyncedAnimation && !action.isSyncedAnimation) return;
            StartCoroutine(ExecuteAsync(action));
        }

        public IEnumerator ExecuteAsync(CharacterAction action)
        {
            if (IsInSyncedAnimation && !action.isSyncedAnimation)
                yield break;

            SetCurrentAction(action);

            if (action.resetMoveAmount)
                ResetMoveAmount();

            // The clip timer below is shared graph state: only the crossfade that owns it advances it,
            // and a crossfade that bows out early — something else took the action mixer over — stops
            // writing it without ever pushing it past the clip length. Waiting on the timer alone
            // then never ends, and the character is stuck busy: locomotion blocked, nothing else able
            // to run. So the crossfade's own completion is tracked as well, and either one releases.
            bool crossfadeFinished = false;
            StartCoroutine(RunCrossfade(action, () => crossfadeFinished = true));

            float clipLength = action.clipInfo.length;

            if (currentAction.motionWarp)
                SetupMotionWarp(currentAction.motionWarpParams, clipLength);

            float transitionInSeconds = action.animationParameters.transitionIn;

            while (!crossfadeFinished && animGraph.currentClipStateInfo.timer <= clipLength)
            {
                if (currentAction != action)
                {
                    // Another Execute() call has taken over; exit without clearing currentAction
                    IsMotionWarping = false;
                    yield break;
                }

                float timer = animGraph.currentClipStateInfo.timer;
                float normalizedTime = timer / action.clipInfo.length;

                if (currentAction.motionWarp)
                    DoMotionWarp(currentAction.motionWarpParams, normalizedTime, clipLength, transitionInSeconds);

                yield return null;
            }

            if (action == currentAction)
            {
                IsMotionWarping = false;
                SetCurrentAction(null);
            }
        }

        IEnumerator RunCrossfade(CharacterAction action, Action onFinished)
        {
            yield return animGraph.CrossfadeAsync(action.clipInfo, action.animationParameters);
            onFinished?.Invoke();
        }

        /// <summary>Cancels whatever action is currently executing, if any.</summary>
        public void CancelCurrent()
        {
            if (currentAction == null) return;
            if (IsInSyncedAnimation) return;
            IsMotionWarping = false;
            bool wasAnimGraph = currentAction.isAnimGraphAction;
            SetCurrentAction(null);
            if (wasAnimGraph) animGraph.CancelCurrentAction();
        }

        public void Cancel(CharacterAction action)
        {
            if (currentAction == null || action == null) return;
            if (IsInSyncedAnimation) return;

            if (currentAction == action)
            {
                IsMotionWarping = false;
                bool wasAnimGraph = currentAction.isAnimGraphAction;
                SetCurrentAction(null);
                if (wasAnimGraph) animGraph.CancelCurrentAction();
            }
        }

        public void Cancel(AnimGraphClipInfo clip)
        {
            if (currentAction == null) return;
            if (IsInSyncedAnimation) return;

            if (currentAction.clipInfo == clip)
            {
                IsMotionWarping = false;
                bool wasAnimGraph = currentAction.isAnimGraphAction;
                SetCurrentAction(null);
                if (wasAnimGraph) animGraph.CancelCurrentAction();
            }
        }

        public IEnumerator ExecuteAnimatorAction(CharacterAction action)
        {
            if (IsInSyncedAnimation && !action.isSyncedAnimation)
                yield break;

            action.isAnimGraphAction = false;
            SetCurrentAction(action);

            if (action.resetMoveAmount)
                ResetMoveAmount();

            string anim = action.animName;

            if (action.useFixedTransition)
                animator.CrossFadeInFixedTime(anim, action.animationParameters.transitionIn);
            else
                animator.CrossFade(anim, action.animationParameters.transitionIn);

            yield return null;

            var animState = animator.GetNextAnimatorStateInfo(0);
            // if (!animState.IsName(anim))
            //     Debug.LogError("Wrong animation detected for " + anim);

            float clipLength = animState.length;
            if (action.motionWarp)
            {
                // animState.length is speed-adjusted (raw / speed), for Motion Warping we also need the raw length
                var nextClips = animator.GetNextAnimatorClipInfo(0);
                action.motionWarpParams.rawClipLength = nextClips.Length > 0? nextClips[0].clip.length : clipLength;
                SetupMotionWarp(action.motionWarpParams, clipLength);
            }

            // Cache the animator's pre-action speed so we can scale animation
            // speed inside the warp window and restore it on exit. Only used
            // when motionWarp + scaleAnimationSpeedWithDistance are both set.
            float originalAnimatorSpeed = animator.speed;
            bool warpSpeedActive = false;
            // CrossFade transitionIn is normalized to destination clip; CrossFadeInFixedTime is seconds.
            float transitionInSeconds = action.useFixedTransition
                ? action.animationParameters.transitionIn
                : action.animationParameters.transitionIn * clipLength;

            float timer = 0f;
            while (timer <= animState.length)
            {
                float normalizedTime = timer / animState.length;
                action.animationParameters.onAnimationUpdate?.Invoke(normalizedTime, timer);

                if (action.useTwoPhaseMatchTarget && action.twoPhaseMatchTargetParams != null)
                {
                    DoTwoPhaseMatchTarget(action.twoPhaseMatchTargetParams, normalizedTime);
                    UpdateMatchTargetAnimationSpeed(action.twoPhaseMatchTargetParams, normalizedTime,
                                                    originalAnimatorSpeed, ref warpSpeedActive);
                }
                else if (action.motionWarp)
                {
                    DoMotionWarp(action.motionWarpParams, normalizedTime, clipLength, transitionInSeconds);
                    UpdateWarpAnimationSpeed(action.motionWarpParams, normalizedTime,
                                             originalAnimatorSpeed, ref warpSpeedActive);
                }

                if (currentAction != action || (!animator.GetCurrentAnimatorStateInfo(0).IsName(anim) && !animState.IsName(anim)))
                    break;

                timer += Time.deltaTime * animator.speed;
                yield return null;
            }

            // Unconditional restore — covers normal completion, mid-window
            // cancel, and the case where the in-loop else-if already reset.
            bool warpSpeedScalingUsed =
                (action.motionWarp && action.motionWarpParams != null
                 && action.motionWarpParams.scaleAnimationSpeedWithDistance) ||
                (action.useTwoPhaseMatchTarget && action.twoPhaseMatchTargetParams != null
                 && action.twoPhaseMatchTargetParams.scaleAnimationSpeedWithDistance);
            if (warpSpeedScalingUsed)
                animator.speed = originalAnimatorSpeed;

            if (action == currentAction) {
                IsMotionWarping = false;
                SetCurrentAction(null);
            }
        }

        /// <summary>
        /// Two-phase bone matching using Unity's animator.MatchTarget.
        ///   Phase 1: matchTargetBone → matchPos by matchEndTime.
        ///   Phase 2 (optional, when finalMatchEndTime > matchEndTime):
        ///            AvatarTarget.Root → matchPos by finalMatchEndTime.
        /// </summary>
        void DoTwoPhaseMatchTarget(TwoPhaseTargetMatchParams p, float normalizedTime)
        {
            if (p == null || animator == null) return;

            AvatarTarget target;
            float startTime;
            float endTime;

            if (p.finalMatchEndTime > p.matchEndTime &&
                normalizedTime > p.matchEndTime)
            {
                // Phase 2: root catches up to matchPos.
                target = AvatarTarget.Root;
                startTime = p.matchEndTime;
                endTime   = p.finalMatchEndTime;
            }
            else
            {
                // Phase 1: the configured bone reaches matchPos.
                target = p.matchTargetBone;
                startTime = p.startTime;
                endTime   = p.matchEndTime;
            }

            if (!animator.isMatchingTarget && !animator.IsInTransition(0))
            {
                animator.MatchTarget(
                    p.matchPos,
                    p.matchRotation,
                    target,
                    new MatchTargetWeightMask(p.positionWeight, 0f),
                    startTime,
                    endTime);
            }
        }

        /// <summary>
        /// One-shot animator.speed scaling for the two-phase match path.
        /// Speed is set on entry to the speed-scale window using the distance
        /// at that frame, held constant, then restored on exit.
        /// </summary>
        void UpdateMatchTargetAnimationSpeed(
            TwoPhaseTargetMatchParams p, float normalizedTime,
            float originalSpeed, ref bool warpSpeedActive)
        {
            if (p == null || !p.scaleAnimationSpeedWithDistance) return;

            float scaleStart = p.speedScaleStartTime >= 0f ? p.speedScaleStartTime : p.startTime;
            float scaleEnd   = p.speedScaleEndTime   >= 0f ? p.speedScaleEndTime   : p.matchEndTime;

            if (normalizedTime > scaleEnd)
            {
                if (!Mathf.Approximately(animator.speed, originalSpeed))
                    animator.speed = originalSpeed;
                warpSpeedActive = false;
                return;
            }

            if (normalizedTime >= scaleStart && !warpSpeedActive)
            {
                float distance = Vector3.Distance(transform.position, p.matchPos);
                if (distance < 0.0001f) distance = 0.0001f;

                float speed = Mathf.Clamp(
                    p.warpMoveSpeed / distance,
                    p.warpSpeedMultiplierRange.x,
                    p.warpSpeedMultiplierRange.y);

                animator.speed = speed;
                warpSpeedActive = true;
            }
        }

        /// <summary>
        /// One-shot animator.speed scaling at match-start time:
        /// <c>speed = clamp(warpMoveSpeed / distanceAtStart, range)</c>.
        /// The speed is set the first frame normalizedTime crosses
        /// <c>speedScaleStartTime</c> using the distance at that moment, and
        /// held constant until normalizedTime crosses <c>speedScaleEndTime</c>,
        /// at which point it's restored to the cached pre-action speed.
        /// Distance is NOT recomputed during the window.
        /// </summary>
        void UpdateWarpAnimationSpeed(
            MotionWarpParams warp, float normalizedTime,
            float originalSpeed, ref bool warpSpeedActive)
        {
            if (warp == null || !warp.scaleAnimationSpeedWithDistance) return;

            // Resolve the speed-scale window (falls back to the warp's own
            // startTime/endTime if not explicitly set).
            float scaleStart = warp.speedScaleStartTime >= 0f ? warp.speedScaleStartTime : warp.startTime;
            float scaleEnd   = warp.speedScaleEndTime   >= 0f ? warp.speedScaleEndTime   : warp.endTime;

            // Past the window — always force the original speed back. Don't
            // gate on warpSpeedActive, so a stale flag never strands the
            // animator at a scaled speed.
            if (normalizedTime > scaleEnd)
            {
                if (!Mathf.Approximately(animator.speed, originalSpeed))
                    animator.speed = originalSpeed;
                warpSpeedActive = false;
                return;
            }

            // Inside the window for the first time — sample distance once and
            // lock the speed for the rest of the window.
            if (normalizedTime >= scaleStart && !warpSpeedActive)
            {
                float distance = Vector3.Distance(transform.position, warp.targetPos);
                if (distance < 0.0001f) distance = 0.0001f;

                float speed = Mathf.Clamp(
                    warp.warpMoveSpeed / distance,
                    warp.warpSpeedMultiplierRange.x,
                    warp.warpSpeedMultiplierRange.y);

                animator.speed = speed;
                warpSpeedActive = true;
            }
            // Inside the window with warpSpeedActive already true, or before
            // the window starts: do nothing — speed stays at whatever the
            // animator already has.
        }

        // Sets a character action without executing it. Useful when you want to execute the action separately.
        public void SetAction(CharacterAction action)
        {
            SetCurrentAction(action);

            if (action.resetMoveAmount)
                ResetMoveAmount();
        }

        // Resets current action without canceling without affecting the execution
        public void ResetAction(CharacterAction action)
        {
            if (currentAction == action)
                SetCurrentAction(null);
        }

        // Single choke point for currentAction mutations. Fires lifecycle events so
        // subscribers (e.g. NavmeshHybridDriver) can block/unblock the NavMeshAgent
        // for the lifetime of any action. Started fires before Ended on takeover so
        // refcounted subscribers go 1→2→1 instead of 1→0→1.
        void SetCurrentAction(CharacterAction newAction)
        {
            if (currentAction == newAction) return;
            var prev = currentAction;
            currentAction = newAction;
            if (newAction != null)
                OnActionStarted?.Invoke(newAction);
            if (prev != null)
                OnActionEnded?.Invoke(prev);
        }

        void ResetMoveAmount()
        {
            float currentMove = animator.GetFloat(AnimatorParameters.moveAmount);
            if (Mathf.Abs(currentMove) > 0.01f)
                StartCoroutine(TweenUtil.TweenVal(currentMove, 0, 0.15f, val => animator.SetFloat(AnimatorParameters.moveAmount, val)));

            float currentStrafe = animator.GetFloat(AnimatorParameters.strafeAmount);
            if (Mathf.Abs(currentStrafe) > 0.01f)
                StartCoroutine(TweenUtil.TweenVal(currentStrafe, 0, 0.15f, val => animator.SetFloat(AnimatorParameters.strafeAmount, val)));
        }

        string GetCurrentClipName()
        {
            if (currentAction == null) return null;

            if (currentAction.isAnimGraphAction)
                return currentAction.clipInfo?.clip?.name;
            else
                return currentAction.animName;
        }

        void SetupMotionWarp(MotionWarpParams warpParams, float clipLength)
        {
            if (warpParams.boneRootOffset != Vector3.zero)
            {
                var toTarget = warpParams.matchPos - transform.position;
                toTarget.y = 0;
                var facingRot = toTarget.sqrMagnitude > 0.01f
                    ? Quaternion.LookRotation(toTarget) : transform.rotation;
                warpParams.targetPos = warpParams.matchPos + facingRot * warpParams.boneRootOffset;
            }
            else
            {
                warpParams.targetPos = warpParams.matchPos;
            }

            warpParams.startPos = transform.position;
            warpParams.warpStarted = false;

            PersistentGizmos.DrawSphere(warpParams.targetPos, 0.1f, Color.red);
        }

        // Called once, on the first frame normalizedTime enters [startTime, endTime].
        void ComputeScaleFactor(MotionWarpParams warpParams, float clipLength)
        {
            // Use rawClipLength (1x speed) for evaluating root curves
            float rawLength = warpParams.rawClipLength > 0f ? warpParams.rawClipLength : clipLength;
            Vector3 animLocalDisp;
            if (warpParams.rootCurves != null)
            {
                animLocalDisp = warpParams.rootCurves.GetPositionAtTime(warpParams.endTime * rawLength)
                              - warpParams.rootCurves.GetPositionAtTime(warpParams.effectiveStartTime * rawLength);
            }
            else
            {
                Debug.LogError("Root curves are empty. Check if they are generated correctly");
                animLocalDisp = Vector3.zero;
            }

            // warpParams.warpRotation was set at warp entry; project destDisp into that frame.
            var destDisp = warpParams.targetPos - transform.position;
            var localDestDisp = Quaternion.Inverse(warpParams.warpRotation) * destDisp;

            // Per-axis local-space scale factor.
            // weightMask = 1  →  scale to reach localDestDisp[axis] exactly.
            // weightMask = 0  →  natural root motion passes through unchanged (scale = 1).
            float ScaleAxis(float dest, float anim, float mask) =>
                mask > 0f && Mathf.Abs(anim) > 0f ? dest / anim : 1f;

            warpParams.rootMotionScaleFactor = new Vector3(
                ScaleAxis(localDestDisp.x, animLocalDisp.x, warpParams.weightMask.x),
                ScaleAxis(localDestDisp.y, animLocalDisp.y, warpParams.weightMask.y),
                ScaleAxis(localDestDisp.z, animLocalDisp.z, warpParams.weightMask.z));
        }

        void DoMotionWarp(MotionWarpParams warpParams, float normalizedTime, float clipLength, float transitionInSeconds)
        {
            if (Time.frameCount - animGraph.LastRebakeFrame <= 1)
            {
                IsMotionWarping = false;
                return;
            }

            float timer = normalizedTime * clipLength;
            
            // Transition blending only corrupts scaleRootMotion 
            bool transitionComplete = !warpParams.scaleRootMotion || timer >= transitionInSeconds;
            bool inWarpWindow = normalizedTime >= warpParams.startTime && normalizedTime <= warpParams.endTime;

            if (inWarpWindow && transitionComplete)
            {
                //Time.timeScale = 0.1f;

                IsMotionWarping = true;

                // First frame inside the warp window: capture true startPos, the effective start time,
                // and compute scale factors from the character's actual position at this moment.
                if (!warpParams.warpStarted)
                {
                    warpParams.warpStarted = true;
                    warpParams.startPos = transform.position;
                    warpParams.effectiveStartTime = normalizedTime;
                    var flat = warpParams.targetPos - transform.position;
                    flat.y = 0;
                    warpParams.warpRotation = flat.sqrMagnitude > 0.001f
                        ? Quaternion.LookRotation(flat)
                        : transform.rotation;
                    if (warpParams.scaleRootMotion)
                        ComputeScaleFactor(warpParams, clipLength);
                }

                if (warpParams.scaleRootMotion)
                {
                    var localDelta = Quaternion.Inverse(warpParams.warpRotation) * animator.deltaPosition;
                    MotionWarpingDeltaPos = warpParams.warpRotation * Vector3.Scale(localDelta, warpParams.rootMotionScaleFactor);
                }
                else
                {
                    // Linear: interpolate toward target. Since the weightMask is provided in local space, we have to convert
                    // the displacement vector to local space before multiplying with the mask
                    Vector3 linearWorld = (warpParams.targetPos - warpParams.startPos) * Time.deltaTime / ((warpParams.endTime - warpParams.effectiveStartTime) * clipLength);
                    Vector3 linearLocal = Quaternion.Inverse(warpParams.warpRotation) * linearWorld;
                    var delta = warpParams.warpRotation * Vector3.Scale(linearLocal, warpParams.weightMask);

                    // Clamp to the remaining distance on the weighted axes — with a short warp window, 
                    // a single long frame can otherwise carry the character past the target
                    var remainingLocal = Quaternion.Inverse(warpParams.warpRotation) * (warpParams.targetPos - transform.position);
                    var remaining = warpParams.warpRotation * Vector3.Scale(remainingLocal, warpParams.weightMask);
                    if (delta.sqrMagnitude > remaining.sqrMagnitude)
                        delta = remaining;

                    MotionWarpingDeltaPos = delta;
                }

                // Debug.Log($"original delta {animator.deltaPosition} warpedDelta={MotionWarpingDeltaPos}");
            }
            else
            {
                // To test if reached the desired post at the end of the warp.
                // if (IsMotionWarping) Debug.Break();

                IsMotionWarping = false;
            }
        }

        public bool IsLinearMotionWarpActive()
        {
            return IsMotionWarping && CurrentAction?.motionWarpParams != null 
                && !CurrentAction.motionWarpParams.scaleRootMotion;
        }
    }

    public class CharacterAction
    {
        public AnimGraphClipInfo clipInfo;
        public AnimParameters animationParameters = new AnimParameters();
        public bool isAnimGraphAction = true;

        public string animName;         // use if you want to execute an animation from animator by it's name
        public bool useFixedTransition = false; // use CrossFadeInFixedTime (seconds) instead of CrossFade (normalized)

        public bool blocksLocomotion = true;
        public bool ignoreCollision = false;
        public bool canLeaveNavMesh = false;

        public bool usesRootMotion = true;
        public bool usesRootMovement = true;    // Can be set to false if you want to ignore the movement from rootmotion
        public bool usesRootRotation = true;     // Can be set to false if you want to ignore the rotation from rootmotion
        
        public bool usesGravity = false;

        public bool requiresLedgeBlocking = true; // For actions where the player should not fall off a ledge during root motion
        public bool isSyncedAnimation = false;   // Prevents other actions from overriding this animation
        public bool resetMoveAmount = true;      // Smoothly reset moveAmount to 0 when action starts

        public bool motionWarp = false;
        public MotionWarpParams motionWarpParams;

        public TargetMatchParams targetMatchParams;

        /// <summary>
        /// Opt into two-phase animator.MatchTarget warping (bone match → root
        /// catchup). Distinct from <see cref="motionWarp"/> which is a simple
        /// linear root warp; this is for animations like grappling where a
        /// specific bone needs to land on the target precisely.
        /// </summary>
        public bool useTwoPhaseMatchTarget = false;
        public TwoPhaseTargetMatchParams twoPhaseMatchTargetParams;

        public CharacterAction() { }

        public CharacterAction(AnimGraphClipInfo clipInfo)
        {
            this.clipInfo = clipInfo;
            animationParameters.transitionIn = clipInfo.TranistionInAndOut.x;
            animationParameters.transitionOut = clipInfo.TranistionInAndOut.y;
        }
    }

    public class MotionWarpParams
    {
        public Vector3 targetPos;
        public float startTime;
        public float endTime;

        // Alternative to setting targetPos directly: provide the raw match position and
        // animation-local bone-root offset. SetupMotionWarp resolves these into targetPos.
        public Vector3 matchPos;
        public Vector3 boneRootOffset;

        public bool scaleRootMotion;
        public Vector3 weightMask;
        public PositionCurves rootCurves;

        // Raw clip duration in seconds at 1× speed, for evaluating RootT curves.
        public float rawClipLength;

        // ── Animation-speed scaling (ExecuteAnimatorAction only) ─────────
        // During the warp window the animation plays at
        //   speed = clamp(warpMoveSpeed / currentDistanceToTarget, range)
        // so far targets get more time to cover the distance and very near
        // targets don't crawl. Distance is recomputed each frame from the
        // character's current position to targetPos.

        /// <summary>Enable animation-speed scaling during the speed-scale window.</summary>
        public bool scaleAnimationSpeedWithDistance;

        /// <summary>
        /// Reference movement speed (m/s) used in the speed formula.
        /// At distance = warpMoveSpeed, the multiplier is exactly 1×.
        /// </summary>
        public float warpMoveSpeed = 5f;

        /// <summary>Min/max clamp on the speed multiplier.</summary>
        public Vector2 warpSpeedMultiplierRange = new Vector2(0f, 2f);

        /// <summary>
        /// Optional separate window for the speed scaling (kept distinct from
        /// startTime/endTime, which the warp itself uses). When -1 the
        /// scaling falls back to startTime/endTime.
        /// </summary>
        public float speedScaleStartTime = -1f;
        public float speedScaleEndTime = -1f;

        // Computed at runtime — do not set manually
        public Vector3 rootMotionScaleFactor;   // per-axis scale in character-local space
        public bool warpStarted;               // true once the warp window has been entered
        public Quaternion warpRotation;         // reference frame for scale factor decomposition
        public float effectiveStartTime;        // normalizedTime captured on the first frame the warp actually runs

        public Vector3 startPos;
    }

    /// <summary>
    /// Two-phase animator.MatchTarget warping. Distinct from MotionWarpParams
    /// (which does linear root warping). Use for animations where a specific
    /// bone must reach a target precisely — e.g. grappling hook, finishers,
    /// vault hand-on-ledge, etc.
    ///
    /// Phase 1: matchTargetBone → matchPos by matchEndTime.
    /// Phase 2 (optional): AvatarTarget.Root → matchPos by finalMatchEndTime.
    /// </summary>
    public class TwoPhaseTargetMatchParams
    {
        /// <summary>World-space target the bone (and root) match to.</summary>
        public Vector3 matchPos;

        /// <summary>Rotation passed to animator.MatchTarget (Y is typically the only used axis).</summary>
        public Quaternion matchRotation = Quaternion.identity;

        /// <summary>Bone that should land on matchPos by matchEndTime (e.g. RightHand, RightFoot).</summary>
        public AvatarTarget matchTargetBone = AvatarTarget.RightHand;

        /// <summary>Normalized time when phase 1 (bone match) starts.</summary>
        public float startTime = 0f;

        /// <summary>Normalized time when phase 1 ends — bone is at matchPos here.</summary>
        public float matchEndTime = 0.64f;

        /// <summary>Normalized time when phase 2 ends — root is at matchPos. Set 0 / ≤ matchEndTime to skip phase 2.</summary>
        public float finalMatchEndTime = 0.98f;

        /// <summary>Per-axis weight passed to the MatchTargetWeightMask.</summary>
        public Vector3 positionWeight = Vector3.one;

        // ── Optional animator.speed scaling, mirrors MotionWarpParams ────
        // Speed is set ONCE on entry to the speed-scale window using the
        // distance at that frame, held until the window closes, then
        // restored. Speed scaling normally maps to the bone-match phase.

        /// <summary>Enable distance-based animator.speed scaling on entry to the speed-scale window.</summary>
        public bool scaleAnimationSpeedWithDistance = false;

        /// <summary>Reference movement speed (m/s) — animator.speed = clamp(warpMoveSpeed / distance, range).</summary>
        public float warpMoveSpeed = 5f;

        /// <summary>Min/max clamp on the resulting animator.speed.</summary>
        public Vector2 warpSpeedMultiplierRange = new Vector2(0f, 2f);

        /// <summary>Speed-scale window start (normalized). -1 falls back to startTime.</summary>
        public float speedScaleStartTime = -1f;

        /// <summary>Speed-scale window end (normalized). -1 falls back to matchEndTime.</summary>
        public float speedScaleEndTime = -1f;
    }

    [Serializable]
    public class PositionCurves
    {
        public AnimationCurve CurveX;
        public AnimationCurve CurveY;
        public AnimationCurve CurveZ;

        public Vector3 GetPositionAtTime(float normalTime)
        {
            return new Vector3(CurveX.Evaluate(normalTime),
                CurveY.Evaluate(normalTime),
                CurveZ.Evaluate(normalTime));
        }
    }
}
