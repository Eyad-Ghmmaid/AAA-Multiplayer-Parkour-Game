using FS_Core;
using System;
using System.Collections;
using UnityEngine;

namespace FS_ParkourSystem
{
    /// <summary>
    /// Shared climbing engine containing methods that are duplicated between
    /// ClimbController (player) and ClimbAI. Both sides delegate to this
    /// engine, providing their specific behavior via IParkourBridge + IClimbBridge.
    /// Lives on the same GameObject as the controller; owns shared climb state directly.
    /// </summary>
    public class ClimbCore : MonoBehaviour
    {
        // --- Serialized config ---
        [Tooltip("Offset between the foot bone and position on the wall where the foot is placed.")]
        [SerializeField] float footPlacementOffset = 0.15f;

        [Tooltip("Length of the raycast used for finding the foot IK position on the wall during braced climb.")]
        [SerializeField] float footIkRayLength = 0.5f;

        [SerializeField] float hipRayLength = 0.3f;

        [SerializeField] Vector3 wallRayOffset = new Vector3(-0.15f, -0.9f, 0.14f);

        [Tooltip("Offset between the hand and the climbpoint while bracedhanging.")]
        public Vector3 handOffsets = new Vector3(0f, -0.08f, 0.05f);

        public float handSpacing;

        // --- Shared state (owned by engine) ---
        private ClimbPoint _currentPoint;

        public ClimbPoint currentPoint
        {
            get { return _currentPoint; }
            set
            {
                if (_currentPoint != null)
                    _currentPoint.hasOwner = false;
                if (value != null)
                    value.hasOwner = true;

                _currentPoint = value;
            }
        }

        public ClimbPoint previousPoint { get; set; }

        public IKweights rightHand { get; set; } = new IKweights();
        public IKweights leftHand { get; set; } = new IKweights();
        public IKweights rightFoot { get; set; } = new IKweights();
        public IKweights leftFoot { get; set; } = new IKweights();
        public IKweights head { get; set; } = new IKweights();

        // --- IK state ---
        AnimatorHelper animatorHelper = new AnimatorHelper();
        Vector3 lookAtPosition;
        float lookAtWeight;
        bool ikEnabled;

        // --- Cached references ---
        ParkourCore parkourCore;
        Animator animator;
        ActionExecutor actionExecutor;
        public IParkourBridge ParkourBridge { get; set; }
        public IClimbBridge ClimbBridge { get; set; }


        void Awake()
        {
            parkourCore = GetComponent<ParkourCore>();
            animator = GetComponentInChildren<Animator>();
            actionExecutor = GetComponent<ActionExecutor>();
        }

        /// <summary>
        /// Finds foot IK position by raycasting against the wall surface.
        /// </summary>
        public Vector3 GetFootIK(Vector3 rayStartPos, Vector3 rayDir)
        {
            rayStartPos -= rayDir * 0.2f;
            bool isWall = Physics.Raycast(rayStartPos, rayDir, out RaycastHit hitInfo, footIkRayLength, FSSettings.i.ObstacleLayer);

            var point = Vector3.zero;
            if (isWall)
            {
                point = hitInfo.point + hitInfo.normal * footPlacementOffset;
            }
            return point;
        }

        /// <summary>
        /// Checks whether there is a wall behind the given climb point by sphere-casting
        /// from the right and left foot positions.
        /// </summary>
        public WallInfo? CheckWall(ClimbPoint point)
        {
            if (point == null) return null;

            WallInfo wallinfo = new WallInfo();

            var rightFootPoint = point.transform.position + wallRayOffset.x * point.transform.right + wallRayOffset.y * point.transform.up + wallRayOffset.z * point.transform.forward;
            var leftFootPoint = point.transform.position - wallRayOffset.x * point.transform.right + wallRayOffset.y * point.transform.up + wallRayOffset.z * point.transform.forward;

            wallinfo.isWall = Physics.SphereCast(rightFootPoint, 0.1f, -point.transform.forward, out wallinfo.rightFootInfo, hipRayLength, FSSettings.i.ObstacleLayer);
            if (wallinfo.isWall)
            {
                wallinfo.isWall = Physics.SphereCast(leftFootPoint, 0.1f, -point.transform.forward, out wallinfo.leftFootInfo, hipRayLength, FSSettings.i.ObstacleLayer);
            }

            return wallinfo;
        }

        /// <summary>
        /// Gets the hand position on a ledge for the specified hand target.
        /// </summary>
        public Vector3 GetHandPos(Transform ledge, AvatarTarget hand, Vector3? handOffset = null)
        {
            rightHand.currentIkVecPoint = animatorHelper.pointTransformWithVectorDown(ledge.transform, rightHand.endBodyPartOffset);
            leftHand.currentIkVecPoint = animatorHelper.pointTransformWithVectorDown(ledge.transform, leftHand.endBodyPartOffset);
            var point = (hand == AvatarTarget.RightHand) ? rightHand.currentIkVecPoint : leftHand.currentIkVecPoint;
            return point;
        }

        /// <summary>
        /// Performs IK shimmy interpolation during a climbing animation.
        /// </summary>
        public void DoShimmy(float normalTime, bool mirror = false, bool HangTypeChange = false)
        {
            float[] lerpValues = new float[4];

            lerpValues[0] = (normalTime - 0.1f) / 0.3f; //rightFoot
            lerpValues[1] = (normalTime - 0) / 0.3f; //rightHand
            lerpValues[2] = (normalTime - 0.5f) / 0.5f; //leftHand
            lerpValues[3] = (normalTime - 0.6f) / 0.4f; //leftFoot

            if (mirror) Array.Reverse(lerpValues);

            rightFoot.lerpValue = lerpValues[0];
            rightHand.lerpValue = lerpValues[1];
            leftHand.lerpValue = lerpValues[2];
            leftFoot.lerpValue = lerpValues[3];

            rightHand.IKPoint = Vector3.Slerp(rightHand.previousIkVecPoint, rightHand.currentIkVecPoint, rightHand.lerpValue);
            leftHand.IKPoint = Vector3.Slerp(leftHand.previousIkVecPoint, leftHand.currentIkVecPoint, leftHand.lerpValue);

            rightHand.weight = rightFoot.weight = 1.3f;
            leftHand.weight = leftFoot.weight = 1.3f;

            rightHand.weight = Mathf.Abs(0.5f - rightHand.lerpValue) * 0.5f + 1f;
            rightFoot.weight = Mathf.Abs(0.5f - rightFoot.lerpValue) * 0.5f + 1f;
            leftHand.weight = Mathf.Abs(0.5f - leftHand.lerpValue) * 0.5f + 1f;
            leftFoot.weight = Mathf.Abs(0.5f - leftFoot.lerpValue) * 0.5f + 1f;

            var freeHanglerp = (0.5f - animator.GetFloat(AnimatorParameters.freeHang));
            rightFoot.IKPoint = Vector3.Slerp(rightFoot.previousIkVecPoint, rightFoot.currentIkVecPoint, rightFoot.lerpValue);
            leftFoot.IKPoint = Vector3.Slerp(leftFoot.previousIkVecPoint, leftFoot.currentIkVecPoint, leftFoot.lerpValue);
            rightFoot.weight *= freeHanglerp;
            leftFoot.weight *= freeHanglerp;
        }

        /// <summary>
        /// Handles the IK pass for climbing. Shared between player and AI OnAnimatorIK.
        /// </summary>
        public void OnAnimatorIK(int layerIndex)
        {
            rightHand.boneTransform = animator.GetBoneTransform(HumanBodyBones.RightHand).position;
            leftHand.boneTransform = animator.GetBoneTransform(HumanBodyBones.LeftHand).position;
            rightFoot.boneTransform = animator.GetBoneTransform(HumanBodyBones.RightFoot).position;
            leftFoot.boneTransform = animator.GetBoneTransform(HumanBodyBones.LeftFoot).position;

            if (animator && parkourCore.IsHanging)
            {
                if (currentPoint)
                {
                    if (lookAtPosition == Vector3.zero) lookAtPosition = currentPoint.transform.position;

                    lookAtPosition = Vector3.MoveTowards(lookAtPosition, currentPoint.transform.position, 2f * Time.deltaTime);
                    animator.SetLookAtPosition(lookAtPosition);
                    lookAtWeight = Mathf.Clamp01(lookAtWeight + 2f * Time.deltaTime) * 0.8f;
                    animator.SetLookAtWeight(lookAtWeight);
                }
                else
                {
                    lookAtWeight = Mathf.Clamp01(lookAtWeight - 1f * Time.deltaTime) * 0.5f;
                    animator.SetLookAtWeight(lookAtWeight);
                }

                bool isBracedHang = animator.GetFloat(AnimatorParameters.freeHang) < 0.5f;

                if (ikEnabled)
                {
                    animator.SetIKPosition(AvatarIKGoal.RightHand, rightHand.IKPoint);
                    animator.SetIKPosition(AvatarIKGoal.LeftHand, leftHand.IKPoint);

                    if (rightFoot.previousIkVecPoint != Vector3.zero && leftFoot.previousIkVecPoint != Vector3.zero && rightFoot.currentIkVecPoint != Vector3.zero && leftFoot.currentIkVecPoint != Vector3.zero && isBracedHang)
                    {
                        animator.SetIKPosition(AvatarIKGoal.RightFoot, rightFoot.IKPoint);
                        animator.SetIKPosition(AvatarIKGoal.LeftFoot, leftFoot.IKPoint);
                        animator.SetIKPositionWeight(AvatarIKGoal.RightFoot, rightFoot.weight);
                        animator.SetIKPositionWeight(AvatarIKGoal.LeftFoot, leftFoot.weight);
                    }
                    animator.SetIKPositionWeight(AvatarIKGoal.RightHand, rightHand.weight);
                    animator.SetIKPositionWeight(AvatarIKGoal.LeftHand, leftHand.weight);
                }
                else
                {
                    void setIK(IKweights bodyPart, HumanBodyBones bone, AvatarIKGoal ikGoal)
                    {
                        Vector3 previous = bodyPart.previousVecPoint;
                        Vector3 current = bodyPart.currentVecPoint;

                        var curDis = (animator.GetBoneTransform(bone).transform.position - current).magnitude;
                        var preDis = (animator.GetBoneTransform(bone).transform.position - previous).magnitude;

                        if (currentPoint == previousPoint || previousPoint == null) preDis = 1000f;

                        if (curDis < preDis)
                        {
                            bodyPart.previousIkVecPoint = bodyPart.currentIkVecPoint;
                        }
                        animator.SetIKPosition(ikGoal, curDis < preDis ? bodyPart.currentIkVecPoint : bodyPart.previousIkVecPoint);
                        animator.SetIKPositionWeight(ikGoal, 0.75f - ((curDis < preDis ? curDis : preDis) - 0.1f) / 0.3f);
                    }

                    setIK(rightHand, HumanBodyBones.RightHand, AvatarIKGoal.RightHand);
                    setIK(leftHand, HumanBodyBones.LeftHand, AvatarIKGoal.LeftHand);

                    if (isBracedHang)
                    {
                        setIK(rightFoot, HumanBodyBones.RightFoot, AvatarIKGoal.RightFoot);
                        setIK(leftFoot, HumanBodyBones.LeftFoot, AvatarIKGoal.LeftFoot);
                    }
                }
            }
        }

        /// <summary>
        /// Initializes both start and end body part positions from animation sampling.
        /// </summary>
        public void InitializeBodyParts(TargetMatchParams matchParams = null, AnimatorHelper _animatorHelper = null,
            float climbTransition = 0, bool mirror = false)
        {
            InitializeStartBodyParts(matchParams, _animatorHelper, climbTransition, mirror);
            InitializeEndBodyParts(matchParams, _animatorHelper, climbTransition, mirror);
        }

        /// <summary>
        /// Initializes start body part offsets and positions from the previous point.
        /// </summary>
        public void InitializeStartBodyParts(TargetMatchParams matchParams = null, AnimatorHelper _animatorHelper = null,
            float climbTransition = 0, bool mirror = false)
        {
            Vector3 curHandOffsets = handOffsets;

            if (_animatorHelper == null) _animatorHelper = animatorHelper;
            if (previousPoint)
            {
                var startCenter = (_animatorHelper.getTransformPos(HumanBodyBones.LeftHand, 0) + _animatorHelper.getTransformPos(HumanBodyBones.RightHand, 0)) / 2;

                rightFoot.startBodyPartOffset = (_animatorHelper.getTransformPos(HumanBodyBones.RightFoot, 0) - startCenter);
                leftFoot.startBodyPartOffset = (_animatorHelper.getTransformPos(HumanBodyBones.LeftFoot, 0) - startCenter);
                rightHand.startBodyPartOffset = (_animatorHelper.getTransformPos(HumanBodyBones.RightHand, 0) - startCenter) + curHandOffsets;
                leftHand.startBodyPartOffset = (_animatorHelper.getTransformPos(HumanBodyBones.LeftHand, 0) - startCenter) + curHandOffsets;

                rightFoot.previousVecPoint = _animatorHelper.pointTransformWithVectorDown(previousPoint.transform, rightFoot.startBodyPartOffset);
                rightFoot.previousIkVecPoint = GetFootIK(rightFoot.previousVecPoint, -previousPoint.transform.forward);
                if (rightFoot.previousIkVecPoint == Vector3.zero) rightFoot.previousIkVecPoint = rightFoot.previousVecPoint;

                leftFoot.previousVecPoint = _animatorHelper.pointTransformWithVectorDown(previousPoint.transform, leftFoot.startBodyPartOffset);
                leftFoot.previousIkVecPoint = GetFootIK(leftFoot.previousVecPoint, -previousPoint.transform.forward);
                if (leftFoot.previousIkVecPoint == Vector3.zero) leftFoot.previousIkVecPoint = leftFoot.previousVecPoint;

                var _handSpacing = ClimbBridge.GetHandSpacing(previousPoint, 0f);
                rightHand.previousVecPoint = _animatorHelper.pointTransformWithVectorDown(previousPoint.transform, rightHand.startBodyPartOffset);
                rightHand.previousIkVecPoint = _animatorHelper.pointTransformWithVectorDown(previousPoint.transform, rightHand.startBodyPartOffset - _handSpacing * Vector3.right);

                leftHand.previousVecPoint = _animatorHelper.pointTransformWithVectorDown(previousPoint.transform, leftHand.startBodyPartOffset);
                leftHand.previousIkVecPoint = _animatorHelper.pointTransformWithVectorDown(previousPoint.transform, leftHand.startBodyPartOffset + _handSpacing * Vector3.right);
            }
        }

        /// <summary>
        /// Initializes end body part offsets and positions from the current point.
        /// </summary>
        public void InitializeEndBodyParts(TargetMatchParams matchParams = null, AnimatorHelper _animatorHelper = null,
            float climbTransition = 0, bool mirror = false)
        {
            Vector3 curHandOffsets = handOffsets;

            if (_animatorHelper == null) _animatorHelper = animatorHelper;
            if (currentPoint)
            {
                var endCenter = (_animatorHelper.getTransformPos(HumanBodyBones.LeftHand, 1) + _animatorHelper.getTransformPos(HumanBodyBones.RightHand, 1)) / 2;

                rightFoot.endBodyPartOffset = (_animatorHelper.getTransformPos(HumanBodyBones.RightFoot, 1) - endCenter);
                leftFoot.endBodyPartOffset = (_animatorHelper.getTransformPos(HumanBodyBones.LeftFoot, 1) - endCenter);
                rightHand.endBodyPartOffset = (_animatorHelper.getTransformPos(HumanBodyBones.RightHand, 1) - endCenter) + curHandOffsets;
                leftHand.endBodyPartOffset = (_animatorHelper.getTransformPos(HumanBodyBones.LeftHand, 1) - endCenter) + curHandOffsets;

                rightFoot.currentVecPoint = _animatorHelper.pointTransformWithVectorDown(currentPoint.transform, rightFoot.endBodyPartOffset);
                rightFoot.currentIkVecPoint = GetFootIK(rightFoot.currentVecPoint, -currentPoint.transform.forward);
                if (rightFoot.currentIkVecPoint == Vector3.zero) rightFoot.currentIkVecPoint = rightFoot.currentVecPoint;

                leftFoot.currentVecPoint = _animatorHelper.pointTransformWithVectorDown(currentPoint.transform, leftFoot.endBodyPartOffset);
                leftFoot.currentIkVecPoint = GetFootIK(leftFoot.currentVecPoint, -currentPoint.transform.forward);
                if (leftFoot.currentIkVecPoint == Vector3.zero) leftFoot.currentIkVecPoint = leftFoot.currentVecPoint;

                var _handSpacing = ClimbBridge.GetHandSpacing(currentPoint, 0f);
                rightHand.currentVecPoint = _animatorHelper.pointTransformWithVectorDown(currentPoint.transform, rightHand.endBodyPartOffset);
                rightHand.currentIkVecPoint = _animatorHelper.pointTransformWithVectorDown(currentPoint.transform, rightHand.endBodyPartOffset - _handSpacing * Vector3.right);

                leftHand.currentVecPoint = _animatorHelper.pointTransformWithVectorDown(currentPoint.transform, leftHand.endBodyPartOffset);
                leftHand.currentIkVecPoint = _animatorHelper.pointTransformWithVectorDown(currentPoint.transform, leftHand.endBodyPartOffset + _handSpacing * Vector3.right);
            }

            if (matchParams == null) return;
            if (matchParams.target == AvatarTarget.RightHand)
            {
                matchParams.pos = rightHand.currentVecPoint;
            }
            else if (matchParams.target == AvatarTarget.LeftHand)
            {
                matchParams.pos = leftHand.currentVecPoint;
            }
        }

        /// <summary>
        /// Sets up shimmy action parameters and starts JumpToLedge coroutine.
        /// </summary>
        public void DoShimmyAction(ClimbPoint newPoint)
        {
            previousPoint = currentPoint;
            currentPoint = newPoint;

            var match = new Vector3();

            var angle = Vector3.Angle(transform.right, currentPoint.transform.position - previousPoint.transform.position);
            var angleSigned = Vector3.SignedAngle(transform.right, currentPoint.transform.position - previousPoint.transform.position, previousPoint.transform.forward);

            var mirror = false;

            var isWall = CheckWall(currentPoint).Value.isWall;

            var climbTransition = 0;
            if (animator.GetFloat(AnimatorParameters.freeHang) > 0.7f && isWall)
                climbTransition = -1;
            else if (animator.GetFloat(AnimatorParameters.freeHang) < 0.3f && !isWall)
                climbTransition = 1;

            var x = 0.5f * Mathf.Cos(angleSigned * Mathf.Deg2Rad);
            var y = -0.5f * Mathf.Sin(angleSigned * Mathf.Deg2Rad);

            animator.SetFloat("x", Mathf.Abs(x));
            animator.SetFloat("y", y);

            if (angle > 85 && angle < 95) mirror = !animator.GetBool(AnimatorParameters.mirrorAction);
            else if (x < 0)
            {
                mirror = true;
            }

            StartCoroutine(JumpToLedge(currentPoint.transform, "ClimbTree", match.x, match.y, matchHand: match.z == 0 ? AvatarTarget.RightHand : AvatarTarget.LeftHand, climbTransition: climbTransition, ikAssist: true, mirror: mirror));
        }

        /// <summary>
        /// Sets up climb jump action parameters and starts JumpToLedge coroutine.
        /// </summary>
        public void DoClimbJumpAction(ClimbPoint newPoint, float angleDiff = 0)
        {
            previousPoint = currentPoint;
            currentPoint = newPoint;
            var angle = Vector3.SignedAngle(-transform.right, currentPoint.transform.position - previousPoint.transform.position, previousPoint.transform.forward);

            if (angleDiff < 30f && Mathf.Abs(angle) > 60 && Mathf.Abs(angle) < 120)
            {
                animator.SetFloat("x", 0f);
                if (angle > 0 && angle < 180)
                    animator.SetFloat("y", 1f);
                else
                    animator.SetFloat("y", -1f);
            }
            else
            {
                animator.SetFloat("y", 0f);
                if (Mathf.Abs(angle) > 90)
                    animator.SetFloat("x", 1f);
                else
                    animator.SetFloat("x", -1f);
            }

            var match = new Vector3();

            var isWall = CheckWall(currentPoint).Value.isWall;

            var climbTransition = 0;
            if (animator.GetFloat(AnimatorParameters.freeHang) > 0.7f && isWall)
                climbTransition = -1;
            else if (animator.GetFloat(AnimatorParameters.freeHang) < 0.3f && !isWall)
                climbTransition = 1;

            StartCoroutine(JumpToLedge(currentPoint.transform, "ClimbTree", match.x, match.y, matchHand: match.z == 0 ? AvatarTarget.RightHand : AvatarTarget.LeftHand, climbTransition: climbTransition));
        }

        /// <summary>
        /// Prepares match parameters and delegates to DoClimbingAction for a ledge jump.
        /// </summary>
        public IEnumerator JumpToLedge(Transform ledge, string anim, float matchStartTime, float matchEndTime,
            Vector3? offset = null,
            AvatarTarget matchHand = AvatarTarget.RightHand,
            AvatarTarget matchStart = AvatarTarget.RightHand,
            bool rotateToLedge = true,
            Action onComplete = null, int climbTransition = 0, bool ikAssist = false, bool mirror = false, bool autoMatch = true)
        {
            if (mirror) matchHand = matchHand == AvatarTarget.RightHand ? AvatarTarget.LeftHand : AvatarTarget.RightHand;
            if (mirror)
            {
                if (matchStart == AvatarTarget.RightHand)
                    matchStart = AvatarTarget.LeftHand;
                else if (matchStart == AvatarTarget.LeftHand)
                    matchStart = AvatarTarget.RightHand;
            }

            var grabPos = GetHandPos(ledge, matchHand, offset);

            var targetRot = Quaternion.LookRotation(-ledge.forward);

            var matchParams = new TargetMatchParams()
            {
                pos = grabPos,
                startTime = matchStartTime,
                endTime = matchEndTime,
                target = matchHand,
                startTarget = matchStart,
                posWeight = Vector3.one
            };

            parkourCore.IsHanging = true;

            yield return DoClimbingAction(anim, rotateToLedge, targetRot, matchParams, onComplete: onComplete, climbTransition: climbTransition, ikAssist: ikAssist, mirror: mirror, autoMatch: autoMatch);
            ikEnabled = false;
        }

        /// <summary>
        /// Core climbing action: samples animation, computes match parameters, then executes
        /// the animation with MatchTarget, rotation, climb transitions, and IK shimmy.
        /// </summary>
        public IEnumerator DoClimbingAction(string anim, bool rotate = false,
              Quaternion targetRot = new Quaternion(), TargetMatchParams matchParams = null, bool mirror = false,
              Action onComplete = null, int climbTransition = 0, bool ikAssist = false, bool autoMatch = true)
        {
            parkourCore.InAction = true;

            if (ParkourBridge.WaitToStart)
                yield return new WaitUntil(() => ParkourBridge.WaitToStart == false);

            animator.SetBool(AnimatorParameters.mirrorAction, mirror);

            if (matchParams != null)
            {
                var _animator = animatorHelper.sampleAnimation(anim, animator);
                matchParams.startTime = animatorHelper.findStartTime(matchParams.startTarget);

                InitializeStartBodyParts(matchParams, climbTransition: climbTransition, mirror: mirror);
                matchParams.startPos = transform.position;

                var maxDelta = 0.005f;

                if (climbTransition != 0)
                {
                    _animator.SetFloat(AnimatorParameters.freeHang, climbTransition == 1 ? 1 : -1);
                    maxDelta = 0.001f;
                }
                if (ikAssist)
                    maxDelta = 0.001f;

                matchParams.endTime = animatorHelper.findEndTime(matchParams.target, maxDelta);
                InitializeEndBodyParts(matchParams, climbTransition: climbTransition, mirror: mirror);

                if (matchParams.startTime > matchParams.endTime) (matchParams.startTime, matchParams.endTime) = (matchParams.endTime, matchParams.startTime);

                if (matchParams.target == AvatarTarget.RightHand)
                {
                    matchParams.endPos = AnimatorHelper._animator.transform.position - animatorHelper.getTransformPos(HumanBodyBones.RightHand, matchParams.endTime);
                }
                else if (matchParams.target == AvatarTarget.LeftHand)
                {
                    matchParams.endPos = AnimatorHelper._animator.transform.position - animatorHelper.getTransformPos(HumanBodyBones.LeftHand, matchParams.endTime);
                }
                else
                    matchParams.endPos = animatorHelper.getRotatedPos(AvatarTarget.Root, matchParams.endTime) - animatorHelper.getRotatedPos(matchParams.target, matchParams.endTime);

                matchParams.endPos = animatorHelper.pointTransformWithVectorDown(currentPoint.transform, matchParams.endPos, matchParams.pos);
                var changedPos = animatorHelper.getRotatedPos(AvatarTarget.Root, matchParams.endTime) - animatorHelper.getRotatedPos(matchParams.target, matchParams.endTime);
                changedPos = animatorHelper.pointTransformWithVectorDown(currentPoint.transform, changedPos, matchParams.pos);

                matchParams.pos += (matchParams.endPos - changedPos);

                animatorHelper.closeSampler(_animator);
            }

            var action = new CharacterAction()
            {
                animName = anim,
                useFixedTransition = true,
                requiresLedgeBlocking = false,
                ignoreCollision = true,
                canLeaveNavMesh = true,
                usesRootMotion = true,
                animationParameters = new AnimParameters
                {
                    transitionIn = 0.2f,
                    onAnimationUpdate = (normalizedTime, timer) =>
                    {
                        if (!ClimbBridge.ShouldContinueAction)
                            actionExecutor.CancelCurrent();

                        if (matchParams != null)
                        {
                            ParkourCore.MatchTarget(animator, matchParams.pos, matchParams.rot, matchParams.target,
                                new MatchTargetWeightMask(matchParams.posWeight, 0),
                                matchParams.startTime, matchParams.endTime);

                            if (rotate && normalizedTime >= matchParams.startTime)
                                transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRot,
                                     parkourCore.RotationSpeed * Time.deltaTime);
                        }

                        if (climbTransition != 0)
                        {
                            var lerpMiddleTime = (matchParams.startTime + matchParams.endTime) / 2;
                            var lerpingTime = (matchParams.endTime - matchParams.startTime);
                            var lerpValue = ikAssist ? (normalizedTime - lerpMiddleTime) / (lerpingTime / 2) : (normalizedTime - matchParams.startTime) / (lerpingTime);
                            animator.Update(-Time.deltaTime);
                            animator.SetFloat(AnimatorParameters.freeHang, Mathf.Lerp((climbTransition - Math.Abs(climbTransition)) / -2, (climbTransition + Math.Abs(climbTransition)) / 2, lerpValue));
                            animator.Update(Time.deltaTime);
                        }

                        if (ikAssist)
                        {
                            ikEnabled = true;
                            DoShimmy(normalizedTime, mirror, climbTransition != 0);
                        }
                    }
                }
            };

            yield return actionExecutor.ExecuteAnimatorAction(action);

            previousPoint = currentPoint;
            parkourCore.InAction = false;

            onComplete?.Invoke();
        }

        /// <summary>
        /// Handles climbing when the character is falling and grabs a ledge.
        /// </summary>
        public IEnumerator ClimbWhenFalling()
        {
            parkourCore.InAction = true;
            parkourCore.IsHanging = true;

            ClimbBridge.OnClimbActionStart(true);

            if (ParkourBridge.WaitToStart)
                yield return new WaitUntil(() => ParkourBridge.WaitToStart == false);

            string animationName;
            if (CheckWall(currentPoint).Value.isWall)
            {
                animator.SetFloat(AnimatorParameters.freeHang, 0);
                animationName = "PredictiveToBracedhang";
            }
            else
            {
                animationName = "PredictiveToFreehangOneHanded";
                animator.SetFloat(AnimatorParameters.freeHang, 1);
            }

            var _animator = animatorHelper.sampleAnimation("HangIdles", animator);

            InitializeBodyParts();
            var right = GetHandPos(currentPoint.transform, AvatarTarget.RightHand);

            animatorHelper.closeSampler(_animator);

            ParkourBridge.OnLand(0.01f, 1f);

            var action = new CharacterAction()
            {
                animName = animationName,
                useFixedTransition = true,
                usesRootMotion = false,
                canLeaveNavMesh = true,
                animationParameters = new AnimParameters
                {
                    transitionIn = 0.2f,
                    onAnimationUpdate = (normalizedTime, timer) =>
                    {
                        var handPos = rightHand.boneTransform;
                        if (rightHand.boneTransform == Vector3.zero)
                            handPos = animator.GetBoneTransform(HumanBodyBones.RightHand).position;

                        var offset = right - handPos;
                        transform.position = Vector3.MoveTowards(transform.position, transform.position + offset, 5f * Time.deltaTime);

                        transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(-currentPoint.transform.forward),
                             parkourCore.RotationSpeed * Time.deltaTime);
                    }
                }
            };

            yield return actionExecutor.ExecuteAnimatorAction(action);

            parkourCore.InAction = false;
        }
    }
}
