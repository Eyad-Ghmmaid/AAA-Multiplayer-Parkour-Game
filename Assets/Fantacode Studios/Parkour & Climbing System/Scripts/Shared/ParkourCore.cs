using FS_Core;
using FS_LocomotionSystem;
using FS_Util;
using System;
using System.Collections;
using UnityEngine;

namespace FS_ParkourSystem
{
    /// <summary>
    /// Shared parkour engine containing methods that are duplicated between
    /// ParkourController (player) and ParkourAI. Both sides delegate to this
    /// engine, providing their specific behavior via IParkourBridge.
    /// Lives on the same GameObject as the controller; owns shared state directly.
    /// </summary>
    [RequireComponent(typeof(LocomotionCore))]
    [RequireComponent(typeof(ActionExecutor))]
    public class ParkourCore : MonoBehaviour, IHitReactionSuppressor
    {
        [field: Tooltip("The speed at which the player will rotate while performing parkour or climbing actions")]
        [field: SerializeField] public float RotationSpeed { get; private set; } = 500f;

        public IParkourBridge Bridge { get; set; }

        // --- Shared state (owned by engine, accessed by controllers) ---
        public bool InAction { get; set; }
        private bool _isHanging;
        public bool IsHanging
        {
            get => _isHanging;
            set
            {
                if (_isHanging == value) return;
                _isHanging = value;
                if (value)
                {
                    locomotionCore.BlockLocomotion();
                    locomotionCore.SetRaycastHeightOverride(0.5f);
                }
                else
                {
                    locomotionCore.UnblockLocomotion();
                    locomotionCore.SetRaycastHeightOverride(null);
                }
            }
        }
        public bool MirrorJump { get; set; }
        public bool InPredictiveJump { get; set; }
        public bool InAirOfPredictiveJump { get; set; }

        /// <summary>A fullbody hit reaction would corrupt the hang; additive ones layer over it safely.</summary>
        public bool SuppressHitReaction(bool isAdditiveReaction) => IsHanging && !isAdditiveReaction;

        // --- Cached references ---
        Animator animator;
        ActionExecutor actionExecutor;
        LocomotionCore locomotionCore;

        void Awake()
        {
            animator = GetComponentInChildren<Animator>();
            actionExecutor = GetComponent<ActionExecutor>();
            locomotionCore = GetComponent<LocomotionCore>();
        }

        public Animator Animator => animator;
        public ActionExecutor ActionExecutor => actionExecutor;

        /// <summary>
        /// Calculates the velocity needed to jump from startPos to targetPos using a parabolic arc.
        /// Returns the initial velocity vector and the total time of flight.
        /// </summary>
        public (Vector3 velocity, float jumpTime) CalculateJumpVelocity(Vector3 startPos, Vector3 targetPos)
        {
            float gravity = Bridge.Gravity;
            float displacementY = targetPos.y - startPos.y;
            var displacementXZ = new Vector3(targetPos.x - startPos.x, 0, targetPos.z - startPos.z);

            var h = Bridge.GetJumpHeight(displacementY, displacementXZ);

            float ty = Mathf.Sqrt(-2 * h / gravity);
            float tx = Mathf.Sqrt(2 * (displacementY - h) / gravity);
            float t = tx + ty;

            var velocityY = Vector3.up * Mathf.Sqrt(-2 * gravity * h);
            var velocityXZ = displacementXZ / t;

            return (velocityY + velocityXZ, t);
        }

        /// <summary>
        /// Wrapper around Unity's Animator.MatchTarget that skips the call if already matching or in transition.
        /// </summary>
        public static void MatchTarget(Animator animator, Vector3 matchPos, Quaternion rotation,
            AvatarTarget target, MatchTargetWeightMask weightMask, float startTime, float endTime)
        {
            if (animator.isMatchingTarget || animator.IsInTransition(0)) return;
            animator.MatchTarget(matchPos, rotation, target, weightMask, startTime, endTime);
        }

        /// <summary>
        /// The back jump init-animation phase that plays root motion with hand tracking.
        /// This loop is identical between player and AI DoPredictiveBackJump.
        /// </summary>
        public IEnumerator PlayBackJumpInitPhase(AnimatorStateInfo animState, float stopPerc,
            float directionValue, bool combo, float height, float dirMagnitude,
            ClimbCore climbEngine, HangType hangType)
        {
            var prevHandPos = directionValue > 0 ? climbEngine.leftHand.boneTransform : climbEngine.rightHand.boneTransform;

            float startTimer = 0f;
            if (!combo)
            {
                animator.speed = Mathf.Clamp(dirMagnitude * 0.12f, 1f, 1.5f);
                while (startTimer <= animState.length * stopPerc)
                {
                    transform.position += animator.deltaPosition;
                    transform.rotation *= animator.deltaRotation;
                    if (hangType == HangType.freeHang)
                    {
                        var handPos = directionValue > 0 ? climbEngine.leftHand.boneTransform : climbEngine.rightHand.boneTransform;
                        var offset = prevHandPos - handPos;
                        transform.position = Vector3.MoveTowards(transform.position, transform.position + offset, 10f * Time.deltaTime);
                    }
                    startTimer += Time.deltaTime;
                    yield return null;
                }
                animator.speed = 1;
            }
            else if (hangType == HangType.freeHang)
            {
                while (startTimer <= animState.length * Mathf.Clamp(0, 0.6f, height))
                {
                    transform.position += animator.deltaPosition;
                    transform.rotation *= animator.deltaRotation;
                    if (hangType == HangType.freeHang)
                    {
                        var handPos = directionValue > 0 ? climbEngine.leftHand.boneTransform : climbEngine.rightHand.boneTransform;
                        var offset = prevHandPos - handPos;
                        transform.position = Vector3.MoveTowards(transform.position, transform.position + offset, 10f * Time.deltaTime);
                    }
                    startTimer += Time.deltaTime;
                    yield return null;
                }
            }
        }

        /// <summary>
        /// Prepares the predictive climb animation by sampling bone positions to compute
        /// the jump target position and IK body parts. Shared between player and AI.
        /// </summary>
        public (Vector3 rotatedPos, Vector3 right, float endTime) PreparePredictiveClimb(
            ClimbPoint point, int climbTransition, ClimbCore climbEngine)
        {
            var hangTypeName = climbTransition == 0 ? "PredictiveToBracedhang" : "PredictiveToFreehang";

            AnimatorHelper animatorHelper = new AnimatorHelper();
            var _animator = animatorHelper.sampleAnimation(hangTypeName, animator);

            float endTime = animatorHelper.findEndTime(AvatarTarget.RightHand);

            var pos = animatorHelper.getTransformRootPos(endTime) - animatorHelper.getTransformPos(HumanBodyBones.RightHand, endTime);

            var endCenter = (animatorHelper.getTransformPos(HumanBodyBones.LeftHand, 1) + animatorHelper.getTransformPos(HumanBodyBones.RightHand, 1)) / 2;
            var right = (animatorHelper.getTransformPos(HumanBodyBones.RightHand, 1) - endCenter) + climbEngine.handOffsets;
            right = animatorHelper.pointTransformWithVectorDown(point.transform, right);

            var rotatedPos = animatorHelper.pointTransformWithVectorDown(point.transform, pos, right);

            climbEngine.InitializeBodyParts(null, animatorHelper);

            animatorHelper.closeSampler(_animator);

            return (rotatedPos, right, endTime);
        }

        /// <summary>
        /// Unified predictive jump coroutine shared between player and AI.
        /// Init animation → rotation phase → airborne velocity loop → ground check → landing → cleanup.
        /// </summary>
        public IEnumerator DoPredictiveJump(JumpData jumpPoint, string animName = null, float crossFadeTime = 0.2f,
            Func<bool, IEnumerator> onLanding = null, float preLandingTime = 0f)
        {
            InAction = true;

            Bridge.OnActionStart();
            if (Bridge.WaitToStart) yield return new WaitUntil(() => Bridge.WaitToStart == false);

            InAirOfPredictiveJump = true;
            InPredictiveJump = true;

            bool isFalling = false;

            animator.SetBool(AnimatorParameters.IsGrounded, false);

            var dispVec = jumpPoint.rootPosition - transform.position;
            dispVec.y = 0f;
            var targetRot = Quaternion.LookRotation(dispVec);
            var anim = animName;

            Vector3 deltaRootPos = Vector3.zero;
            if (animName == null)
            {
                MirrorJump = !MirrorJump;
                animName = MirrorJump ? "Predictive JumpM" : "Predictive Jump";
                animator.SetBool(AnimatorParameters.mirrorJump, MirrorJump);
            }

            animator.CrossFadeInFixedTime(animName, crossFadeTime);
            yield return null;

            if (anim == null)
            {
                var jumpAction = new CharacterAction()
                {
                    animName = animName,
                    isAnimGraphAction = false,
                    requiresLedgeBlocking = false,
                    ignoreCollision = true,
                    usesRootMotion = true,
                    canLeaveNavMesh = true
                };

                actionExecutor.SetAction(jumpAction);

                var t = 0f;
                while (true)
                {
                    t += Time.deltaTime;
                    if (t > .15f)
                        break;
                    transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRot, RotationSpeed * 2 * Time.deltaTime);
                    yield return null;
                }

                actionExecutor.ResetAction(jumpAction);
            }

            var (jumpVel, jumpTime) = CalculateJumpVelocity(transform.position, jumpPoint.rootPosition + targetRot * deltaRootPos);

            Vector3 startPos = transform.position;
            float ySpeed = jumpVel.y;
            float timer = 0f;

            Bridge.SetIsInAir(true);

            while (Bridge.ShouldContinueAction)
            {
                timer += Time.deltaTime;

                Vector3 velocity = jumpVel;
                ySpeed += Bridge.Gravity * Time.deltaTime;
                velocity.y = ySpeed;

                transform.position += velocity * Time.deltaTime;

                if (!locomotionCore.PreventRotation)
                    transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRot, RotationSpeed * Time.deltaTime);

                // Pre-landing: fire callback before ground contact for smoother transitions
                if (onLanding != null && preLandingTime > 0f && timer >= jumpTime - preLandingTime)
                {
                    yield return onLanding(isFalling);
                    break;
                }

                if (Bridge.CheckGrounded(timer, jumpTime, jumpPoint.footPosition, velocity * Time.deltaTime, MirrorJump))
                {
                    if (onLanding != null)
                        yield return onLanding(isFalling);
                    else
                        yield return locomotionCore.PerformLanding(isFalling);
                    break;
                }

                if (!isFalling && timer > .6f && startPos.y - jumpPoint.rootPosition.y > 2.5f)
                {
                    isFalling = true;
                    animator.SetBool(AnimatorParameters.isFalling, isFalling);
                }

                yield return null;
            }

            Bridge.SetIsInAir(false);
            InAirOfPredictiveJump = false;
            IsHanging = false;

            if (Bridge.ShouldContinueAction)
                Bridge.OnActionEnd();
            else
            {
                animator.SetBool(AnimatorParameters.isFalling, true);
            }

            InAction = false;
            InPredictiveJump = false;
        }

        /// <summary>
        /// Unified predictive climb coroutine shared between player and AI.
        /// Init animation → parabolic arc → hang transition → IK settle → combo check → cleanup.
        /// </summary>
        public IEnumerator DoPredictiveClimb(ClimbPoint point, int climbTransition,
            ClimbCore climbCore, IClimbBridge climbBridge,
            string animName = null, float crossFadeTime = 0.2f)
        {
            InAction = true;

            climbBridge.OnClimbActionStart(true);
            if (Bridge.WaitToStart) yield return new WaitUntil(() => Bridge.WaitToStart == false);

            InAirOfPredictiveJump = true;
            InPredictiveJump = true;

            animator.SetBool(AnimatorParameters.IsGrounded, false);

            var (rotatedPos, right, endTime) = PreparePredictiveClimb(point, climbTransition, climbCore);

            var dispVec = point.transform.position - transform.position;
            dispVec.y = 0f;
            var targetRot = Quaternion.LookRotation(dispVec);
            var anim = animName;
            if (animName == null)
            {
                climbBridge.ComputeMirrorJump(this);
                animName = MirrorJump ? "Predictive JumpM" : "Predictive Jump";
                animator.SetBool(AnimatorParameters.mirrorJump, MirrorJump);
            }

            animator.CrossFadeInFixedTime(animName, crossFadeTime);
            yield return null;

            if (anim == null)
            {
                var jumpAction = new CharacterAction()
                {
                    animName = animName,
                    isAnimGraphAction = false,
                    requiresLedgeBlocking = false,
                    ignoreCollision = true,
                    usesRootMotion = true,
                    canLeaveNavMesh = true
                };

                actionExecutor.SetAction(jumpAction);

                var t = 0f;
                while (true)
                {
                    t += Time.deltaTime;
                    if (t > .15f)
                        break;
                    transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRot, RotationSpeed * 5 * Time.deltaTime);
                    yield return null;
                }

                actionExecutor.ResetAction(jumpAction);
            }

            var (jumpVel, jumpTime) = CalculateJumpVelocity(transform.position, rotatedPos);

            animator.SetFloat(AnimatorParameters.freeHang, climbTransition);

            Vector3 startPos = transform.position;
            float ySpeed = jumpVel.y;

            AnimatorStateInfo animState = animator.GetNextAnimatorStateInfo(0);

            var start = true;
            var idleStart = true;
            var camShake = true;
            float timer = 0f;

            var temp = jumpTime;
            var transitionTime = jumpTime * 0.3f;
            var idleTransitionTime = jumpTime * 0.7f;

            float comboTime = 0f;

            climbBridge.ResetPredictiveClimbState();

            while (timer <= temp && climbBridge.ShouldContinueAction)
            {
                if (timer <= jumpTime)
                {
                    Vector3 velocity = jumpVel;
                    velocity.y = ySpeed;
                    transform.position += velocity * Time.deltaTime;
                    ySpeed += Bridge.Gravity * Time.deltaTime;

                    if (timer >= jumpTime - transitionTime && start)
                    {
                        animator.CrossFade("PredictiveTo" + (climbTransition == 0 ? "Bracedhang" : "Freehang"), 0.3f);
                        animator.Update(0);
                        animState = animator.GetNextAnimatorStateInfo(0);
                        start = false;
                        targetRot = Quaternion.LookRotation(-point.transform.forward);
                        temp += animState.length;
                        animator.speed = endTime * (animState.length) / transitionTime;
                    }
                    else if (timer >= jumpTime - idleTransitionTime && idleStart)
                    {
                        IsHanging = false;
                        animator.CrossFade("FallIdle", 0.6f);
                        idleStart = false;
                    }
                }
                else
                {
                    IsHanging = true;
                    animator.speed = 1;

                    Bridge.SetIsInAir(false);

                    var handPos = climbCore.rightHand.boneTransform;

                    if (camShake)
                    {
                        Bridge.OnLand(Mathf.Clamp((startPos.y - transform.position.y) * 0.003f, 0f, 0.05f), 0.7f);
                        camShake = false;
                    }

                    if (climbCore.rightHand.boneTransform == Vector3.zero)
                        handPos = animator.GetBoneTransform(HumanBodyBones.RightHand).position;

                    var offset = right - handPos;
                    transform.position = Vector3.MoveTowards(transform.position, transform.position + offset, 10f * Time.deltaTime);

                    comboTime += Time.deltaTime;

                    if (climbBridge.HandlePredictiveClimbLanding(point, comboTime, (animState.length - transitionTime) * 0.3f))
                        yield break;
                }

                if (!locomotionCore.PreventRotation)
                    transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRot, RotationSpeed * Time.deltaTime);

                timer += Time.deltaTime;
                yield return null;
            }

            if (climbBridge.ShouldContinueAction)
                transform.rotation = targetRot;
            else
                IsHanging = false;

            InAirOfPredictiveJump = false;
            InPredictiveJump = false;
            InAction = false;
        }

        /// <summary>
        /// Unified predictive back jump coroutine shared between player and AI.
        /// Computes direction, plays init animation, then chains to DoPredictiveJump or DoPredictiveClimb.
        /// </summary>
        public IEnumerator DoPredictiveBackJump(JumpData jumpPoint = null, ClimbPoint climbPoint = null,
            int climbTransition = 0, bool combo = false,
            ClimbCore climbCore = null, IClimbBridge climbBridge = null)
        {
            Vector3 dir;
            if (jumpPoint != null)
                dir = transform.position - jumpPoint.rootPosition;
            else
                dir = transform.position - climbPoint.transform.position;

            var angleBWvec = Vector3.SignedAngle(transform.forward, new Vector3(dir.x, 0, dir.z), Vector3.down);
            var value = angleBWvec / 90;

            var hangType = animator.GetFloat(AnimatorParameters.freeHang) < 0.5f ? HangType.bracedHang : HangType.freeHang;

            var currentPoint = climbPoint != null ? climbCore.previousPoint : climbCore.currentPoint;
            if (hangType == HangType.freeHang && !currentPoint.transform.parent.CompareTag("SwingableLedge") && Mathf.Abs(value) > 1f)
            {
                if (climbCore.currentPoint && climbPoint != null)
                    climbCore.currentPoint = climbCore.previousPoint;
                InAirOfPredictiveJump = InPredictiveJump = InAction = false;
                IsHanging = true;
                yield break;
            }

            InAction = InAirOfPredictiveJump = InPredictiveJump = true;

            climbBridge.OnClimbActionStart(true);
            if (Bridge.WaitToStart) yield return new WaitUntil(() => Bridge.WaitToStart == false);

            animator.SetBool(AnimatorParameters.IsGrounded, false);

            var height = Bridge.GetJumpHeight(dir.z, new Vector3(dir.x, 0, dir.z));

            MirrorJump = !MirrorJump;
            var animName = MirrorJump ? "Jump Idle M" : "Jump Idle";
            float stopPerc = 0.5f;

            if (hangType == HangType.bracedHang)
            {
                combo = false;
                animator.SetFloat(AnimatorParameters.jumpBackDirection, Mathf.Sign(value) * Mathf.Clamp(Mathf.Abs(value), 0.1f, 1));
                animator.CrossFade("predictiveJumpBack", 0.2f);
            }
            else
            {
                animator.SetFloat(AnimatorParameters.jumpBackDirection, Mathf.Sign(value) * Mathf.Clamp(Mathf.Abs(value), 0.1f, 2));
                animator.CrossFadeInFixedTime("predictiveJumpBackFreeHang", 0.2f);
                if (combo)
                    animator.CrossFadeInFixedTime("Freehang Forward Jump", 0.2f);

                if (Mathf.Abs(value) > 1.2f)
                {
                    stopPerc = Mathf.Clamp(0, 0.7f, height);
                }
                else
                {
                    stopPerc = 0.6f;
                    if (combo)
                    {
                        if (Mathf.Abs(value) < 0.8f)
                        {
                            animator.CrossFade("predictiveJumpBackFreeHang", 0.2f, 0, 0.4f);
                            stopPerc = 0.1f;
                        }
                        else stopPerc = 0.2f;

                        combo = false;
                    }
                }
            }

            yield return null;

            var animState = animator.GetNextAnimatorStateInfo(0);

            yield return PlayBackJumpInitPhase(animState, stopPerc, value, combo, height, dir.magnitude,
                climbCore, hangType);

            bool noChain = jumpPoint == null && climbPoint == null;
            climbBridge.OnBackJumpComplete(noChain);

            if (jumpPoint != null)
                StartCoroutine(DoPredictiveJump(jumpPoint, animName, 0.3f));
            else if (climbPoint != null)
                StartCoroutine(DoPredictiveClimb(climbPoint, climbTransition, climbCore, climbBridge, animName, 0.8f));
            else
            {
                InAirOfPredictiveJump = InPredictiveJump = InAction = false;
            }
        }

        public IEnumerator DoParkourAction(ParkourAction action)
        {
            MotionWarpParams warpParams = null;

            if (action.EnableTargetMatching && action.RootMotionCurves != null)
            {
                PersistentGizmos.DrawSphere(action.MatchPos, 0.1f, Color.green);

                warpParams = new MotionWarpParams()
                {
                    matchPos = action.MatchPos,
                    boneRootOffset = action.BoneRootOffset,
                    startTime = action.MatchStartTime,
                    endTime = action.MatchTargetTime,
                    scaleRootMotion = action.WarpMatchType == MotionWarpType.ScaleRootMotion,
                    weightMask = action.MatchPosWeight,
                    rootCurves = action.RootMotionCurves,
                };
            }

            InAction = true;
            Bridge.OnActionStart(action.UseHands);
            if (Bridge.WaitToStart)
                yield return new WaitUntil(() => Bridge.WaitToStart == false);

            yield return DoAction(action.ClipInfo, action.RotateToObstacle, action.TargetRotation,
                warpParams, action.PostActionDelay, action.Mirror);

            Bridge.OnActionEnd();
            InAction = false;
        }

        public IEnumerator DoAction(AnimGraphClipInfo clipInfo, bool rotate = false,
            Quaternion targetRot = default, MotionWarpParams warpParams = null,
            float postDelay = 0f, bool mirror = false, Action onComplete = null,
            float crossFadeTime = .2f)
        {
            animator.SetBool(AnimatorParameters.mirrorAction, mirror);
            var action = new CharacterAction(clipInfo)
            {
                usesRootMotion = true,
                ignoreCollision = true,
                canLeaveNavMesh = true,
                requiresLedgeBlocking = false,
                motionWarp = warpParams != null,
                motionWarpParams = warpParams,
                animationParameters = new AnimParameters
                {
                    transitionIn = crossFadeTime,
                    onAnimationUpdate = !rotate ? null : (normalizedTime, timer) =>
                    {
                        if (warpParams == null || normalizedTime >= warpParams.startTime)
                            transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRot, RotationSpeed * Time.deltaTime);
                    }
                }
            };
            yield return ActionExecutor.ExecuteAsync(action);
            yield return new WaitForSeconds(postDelay);
            onComplete?.Invoke();
        }
    }
}
