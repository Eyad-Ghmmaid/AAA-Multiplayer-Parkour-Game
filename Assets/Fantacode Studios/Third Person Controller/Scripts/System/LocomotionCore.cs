using FS_Core;
using FS_Util;
using System;
using System.Collections;
using System.Linq;
using UnityEngine;
using UnityEngine.AI;

namespace FS_LocomotionSystem
{
    public enum MoveType { Walk, Run, Sprint }

    // Class that contains locomotion functionalities common to both Player and AI
    [RequireComponent(typeof(EnvironmentScanner))]
    [RequireComponent(typeof(ActionExecutor))]
    [RequireComponent(typeof(CharacterCapsule))]
    public class LocomotionCore : MonoBehaviour
    {
        #region InspectorParams

        [Header("Movement Parameters")]
        [SerializeField] float sprintSpeed = 6.5f;
        [SerializeField] float runSpeed = 4.5f;
        [SerializeField] float walkSpeed = 2f;
        [SerializeField] float rotationSpeed = 2.5f;

        [field: Space(2)]
        public float acceleration = 8f;
        public float deceleration = 6f;

        // Multi-Directional Movement
        [field: Space(2)]
        public bool useMultiDirectionalAnimation;

        [ShowIf("useMultiDirectionalAnimation", true)]
        [Tooltip("When set to 0 character faces moving Direction. When set to 1 character faces camera Direction")]
        [Range(0, 1)] public float characterDirectionBlend = 1f;

        [ShowIf("useMultiDirectionalAnimation", true)]
        [Tooltip("Use this multiplier if you want different speed for multi directional animations")]
        public float speedMultiplier = 0.8f;

        [ShowIf("useMultiDirectionalAnimation", true)]
        [Tooltip("Only sprints if character is facing forward by this percentage")]
        [Range(0, 1)] public float sprintDirectionThreshold = 0.9f;

        [ShowIf("useMultiDirectionalAnimation", true)]
        [Range(0, 1)] public float forwardHipRotationBlend = 0.5f;

        [ShowIf("useMultiDirectionalAnimation", true)]
        public bool rotateHipForBackwardAnimation = true;

        [ShowIf("useMultiDirectionalAnimation", true)]
        [Range(0, 1)] public float backwardHipRotationBlend = 0.8f;

        [ShowIf("useMultiDirectionalAnimation", true)]
        public bool snapToDesiredFacingDir = false;

        // Jump
        public float gravity = -20f;
        public bool canPerformVerticalJump;

        [ShowIf("verticalJump", true)]
        [Tooltip("Defines how long it takes for the character to reach the peak of the jump")]
        public float timeToJump = 0.4f;

        [ShowIf("verticalJump", true)]
        [Tooltip("Movement speed while the character is in the air")]
        public float jumpMoveSpeed = 4f;

        [ShowIf("verticalJump", true)]
        [Tooltip("Acceleration in air")]
        public float jumpMoveAcceleration = 4f;

        // Ledge Protection
        [field: Space(3)]
        [field: Tooltip("Automatically stopping movement on ledges")]
        [SerializeField] bool preventFallingFromLedge = true;

        [ShowIf("preventFallingFromLedge", true)]
        [field: Tooltip("Sliding movement threshold on ledges")]
        [Range(-1, 1)]
        [SerializeField] float slidingMovementThresholdFromLedge = -1f;

        [ShowIf("preventFallingFromLedge", true)]
        [field: Tooltip("Prevents character rotation at ledges when not able to move")]
        [SerializeField] bool preventLedgeRotation = false;

        [field: Tooltip("Prevents movement near walls during locomotion")]
        [SerializeField] bool preventWallSlide = false;

        // Balance Walking
        [field: Space(3)]
        [field: Tooltip("Enables balance walking on narrow beams")]
        public bool enableBalanceWalk = true;

        [ShowIf("enableBalanceWalk", true)]
        public BalanceWalkDetectionType balanceWalkDetectionType = BalanceWalkDetectionType.Dynamic;

        public enum BalanceWalkDetectionType { Dynamic, Tagged, Both }

        // Optional Animations
        [Header("Optional Animations")]
        [field: Tooltip("Plays turning animations")]
        public bool enableTurningAnim = true;

        [Tooltip("Plays idle turn animation")]
        public bool playIdleTurnAnimations = false;

        [Tooltip("The angle above which the idle rotation should be trigerred")]
        [ShowIf("playIdleTurnAnimations", true)]
        public float idleRotationTriggerAngle = 40;

        [ShowIf("playIdleTurnAnimations", true)]
        [SerializeField] IdleTurns idleTurns;

        bool useIdleTurnOverrides;
        IdleTurns overrideIdleTurns;


        [field: Tooltip("Plays quick turn animation")]
        public bool playQuickTurnAnimation = true;

        [ShowIf("playQuickTurnAnimation", true)]
        [field: Tooltip("Threshold for triggering quick turn")]
        public float QuickTurnThreshhold = -0.01f;

        [field: Tooltip("Plays quick stop animation")]
        public bool playQuickStopAnimation = false;

        [ShowIf("playQuickStopAnimation", true)]
        [field: Tooltip("Threshold for run to stop animation")]
        public float runToStopThreshhold = 0.4f;

        // Ground Check
        [Header("Ground Check Settings")]
        [Tooltip("Radius of ground detection sphere")]
        [SerializeField] float groundCheckRadius = 0.2f;

        [Tooltip("Offset between the character's root position and the ground detection sphere")]
        [SerializeField] Vector3 groundCheckOffset = new(0f, 0.15f, 0.07f);

        #endregion

        // Inputs
        public bool PreventRotation { get; set; }
        public bool PreventVerticalJump { get; set; }
        public bool PreventTurnBacks { get; set; }
        public float MaxSpeed { get; set; }
        public float SprintSpeed => sprintSpeed;
        public float RunSpeed => runSpeed;
        public float WalkSpeed => walkSpeed;
        public bool InterruptActions { get; set; }
        public bool IsLocomotionLockActive {get; private set;}

        int stopAllMovementCount;
        bool StopAllMovement => stopAllMovementCount > 0;

        /// <summary>
        /// Bocks all movmement (both locomotion and rootmotion movement)
        /// </summary>
        public void BlockAllMovement()
        {
            stopAllMovementCount++;
        }

        public void UnblockAllMovement()
        {
            stopAllMovementCount--;
            if (stopAllMovementCount < 0)
            {
                Debug.LogWarning("StopAllMovement count went below zero!");
                stopAllMovementCount = 0;
            }
        }

        public void ResetAllMovementBlock()
        {
            stopAllMovementCount = 0;
        }

        /// <summary>
        /// Blocks locomotion but does not block rootmotion
        /// </summary>
        public void BlockLocomotion()
        {
            locomotionBlockCount++;
        }

        public void UnblockLocomotion()
        {
            locomotionBlockCount--;

            if (locomotionBlockCount < 0)
            {
                // Debug.LogWarning("MovementBlockCount went below zero!");
                locomotionBlockCount = 0;
            }
        }

        public float LocomotionSpeedOverride { get; set; }  // Can be used to override the locomotion speed (eg. Combat Movement)
        public bool UseLocomotionRootMotion { get; set; }   // If true, OnAnimatorMove drives x/z movement; HandleLocomotion only applies gravity/y

        // Raycast target height
        float? raycastHeightOverride;

        public float RaycastTargetHeight =>
            raycastHeightOverride ?? (IsCrouching ? FSSettings.i.TargetCrouchDetectionOffset : FSSettings.i.TargetDetectionOffset);

        public void SetRaycastHeightOverride(float? height) => raycastHeightOverride = height;

        // States
        public bool IsLocomotionBlocked => locomotionBlockCount > 0;    // Only stops locomotion, not root motion

        public MoveType moveType { get; private set; }
        public bool IsCrouching { get; private set; }
        public bool IsWalkOnly { get; private set; }
        public bool CanCrouch { get; private set; } = true;

        bool _useMultiDirectionalAnimation;

        public bool UseMultiDirectionalAnimation => useMultiDirectionalAnimation;

        bool _preventFallingFromLedge;
        public bool PreventFallingFromLedge => _preventFallingFromLedge;

        bool _enableTurningAnim;
        bool _playIdleTurnAnimations;
        bool _playQuickTurnAnimation;
        bool _playQuickStopAnimation;

        int locomotionBlockCount = 0;
        bool isGrounded = false;


        public float MoveAmount => Animator.GetFloat(AnimatorParameters.moveAmount);

        // Events
        public event Action OnLocomotionActionStart;
        public event Action OnLocomotionActionEnd;

        public event Action OnLocomotionLockEnabled;
        public event Action OnLocomotionLockDisabled;

        public event Action<float, float> OnLanded;
        public event Action OnLandingActionStarted;    // For landing actions like rolling

        // Constants
        float minJumpHeightForHardland = 3f;
        float headHeightThreshold = .75f;

        // Constants for balance check
        float footOffset = .1f;
        float footRayHeight = .8f;

        // Private fields
        Vector3 desiredMoveDir;
        Vector3 moveDir;
        Vector3 desiredVelocity;
        Vector3 desiredFacingDir;   // only valid for multi-directional movement
        Quaternion targetRotation;
        float ySpeed = 0f;
        Vector3 prevAngle;
        float rotationValue = 0;
        float jumpMaxPosY;
        float prevBalanceAmount = 0;

        float crouchAmount = 0f;
        float balanceAmount = 0;
        bool balanceHasSpace = true;

        float crouchVal => Mathf.Max(crouchAmount, balanceAmount);


        Vector3 currentVelocity;

        public Vector3 CurrentVelocity => currentVelocity;

        float moveSpeed = 0;
        float airSpeed;
        bool lockAirVelocity;
        bool isTurningBack;
        bool isIdleTurning;

        public bool IsPlayingTurnAnimation => isIdleTurning || isTurningBack;

        IMotionDriver motionDriver;
        public Animator Animator { get; private set; }
        public ActionExecutor ActionExecutor { get; private set; }
        EnvironmentScanner environmentScanner;
        CharacterCapsule characterCapsule;

        private void Awake()
        {
            Animator = GetComponent<Animator>();
            environmentScanner = GetComponent<EnvironmentScanner>();
            motionDriver = GetComponent<IMotionDriver>();
            characterCapsule = GetComponent<CharacterCapsule>();
            ActionExecutor = GetComponent<ActionExecutor>();

            _useMultiDirectionalAnimation = useMultiDirectionalAnimation;
            _preventFallingFromLedge = preventFallingFromLedge;
            _enableTurningAnim = enableTurningAnim;
            _playIdleTurnAnimations = playIdleTurnAnimations;
            _playQuickTurnAnimation = playQuickTurnAnimation;
            _playQuickStopAnimation = playQuickStopAnimation;

            targetRotation = transform.rotation;

            originalLocomotionType = Animator.GetFloat(AnimatorParameters.locomotionType);
            prevPos = transform.position;

            
        }

        private void Start()
        {
            _walkSpeed = walkSpeed;
            _runSpeed = runSpeed;
            _sprintSpeed = sprintSpeed;
            MaxSpeed = sprintSpeed;
            jumpMaxPosY = transform.position.y;
            GroundCheck();
        }

        public void HandleLocomotion(Vector3 desiredMoveDir, float moveAmount, Vector3 desiredFacingDir,
            bool shouldJump, bool shouldDrop, bool forceIdleRotation = false)
        {

            this.desiredMoveDir = desiredMoveDir;
            moveDir = desiredMoveDir;
            this.desiredFacingDir = desiredFacingDir;

            //Debug.DrawRay(transform.position, desiredFacingDir, Color.green);
            //Debug.DrawRay(transform.position, transform.forward, Color.blue);

            if (IsLocomotionBlocked || ActionExecutor.BlockLocomotion || StopAllMovement)
            {
                ResetTargetRotation();
                return;
            }

            // locomotion type might change (for example, when swithching to First Person). So keep updating it.
            Animator.SetFloat(AnimatorParameters.locomotionType, useMultiDirectionalAnimation ? 1 : 0);

            bool wasGroundedPreviously = isGrounded;
            if (ySpeed < 0)
                GroundCheck();

            Animator.SetFloat(AnimatorParameters.fallAmount, Mathf.Clamp(Mathf.Abs(ySpeed) * 0.05f, 0f, 1));

            if (isGrounded && !wasGroundedPreviously)
            {
                lockAirVelocity = false;
                OnLanded?.Invoke(Mathf.Clamp(Mathf.Abs(ySpeed) * 0.0007f, 0.0f, 0.01f), 1f);
                float jumpHeightDiff = Mathf.Abs(jumpMaxPosY - transform.position.y);
                bool hardLand = jumpHeightDiff > minJumpHeightForHardland;

                StartCoroutine(PerformLanding(hardLand));
                return;
            }

            // Air Movement
            if (!isGrounded)
            {
                if (wasGroundedPreviously)
                {
                    ySpeed = -0.5f;
                    airSpeed = Mathf.Max(jumpMoveSpeed, new Vector3(currentVelocity.x, 0f, currentVelocity.z).magnitude);
                }

                ySpeed += gravity * Time.deltaTime;

                // Update fall velocity
                UpdateCurrentVelocityFromAir();

                motionDriver.Move(currentVelocity * Time.deltaTime);

                // To get max jump height
                if (jumpMaxPosY < transform.position.y)
                    jumpMaxPosY = transform.position.y;

                if (moveDir != Vector3.zero && !PreventRotation)
                    UpdateRotationFromAir();

                return;
            }

            ySpeed = gravity / 2;

            jumpMaxPosY = transform.position.y - 1;

            moveSpeed = moveType == MoveType.Walk ? walkSpeed : runSpeed;
            if (moveType == MoveType.Sprint)
                moveSpeed = CalculateSprintMoveSpeed();

            var currentRunSpeed = runSpeed;

            if (crouchVal > 0.2f)
                moveSpeed = Mathf.Clamp(moveSpeed, 0, walkSpeed);
            else if (useMultiDirectionalAnimation)
            {
                moveSpeed *= speedMultiplier;
                currentRunSpeed *= speedMultiplier;
            }

            if (LocomotionSpeedOverride > 0)
            {
                moveSpeed = LocomotionSpeedOverride;
                currentRunSpeed = LocomotionSpeedOverride;
            }

            desiredVelocity = desiredMoveDir * moveSpeed;

            if (shouldJump)
                TryVerticalJump();

            if (_enableTurningAnim)
                HandleTurningAnimations();

            if (IsOnLedge && shouldDrop)
                TryToJumpFromLedge();

            // Update Locomotion Animations
            float forwardSpeed, strafeSpeed;
            CalculateForwardAndStrafeSpeed(out forwardSpeed, out strafeSpeed);

            if (currentRunSpeed > 0)
            {
                Animator.SetFloat(AnimatorParameters.moveAmount, forwardSpeed / currentRunSpeed, 0.2f, Time.deltaTime);
                Animator.SetFloat(AnimatorParameters.strafeAmount, strafeSpeed / currentRunSpeed, 0.2f, Time.deltaTime);
            }

            UpdateCurrentVelocity();

            if (_playIdleTurnAnimations)
            {
                bool idleTurnSuccess = TryIdleTurn(forceIdleRotation);
                if (idleTurnSuccess) return;
            }

            if (_playQuickTurnAnimation)
                TryQuickTurn();

            if (_playQuickStopAnimation)
                TryQuickStop(forwardSpeed, strafeSpeed, currentRunSpeed);

            currentVelocity.y = 0;

            if (motionDriver.SupportsManualMovement)
            {
                if (!UseLocomotionRootMotion)
                {
                    if (preventFallingFromLedge)
                        UpdateVelocityAndDirFromLedge();
                    else
                        moveDir = currentVelocity.normalized;

                    desiredVelocity.y = ySpeed;
                    currentVelocity.y = ySpeed;

                    motionDriver.Move(currentVelocity * Time.deltaTime);

                    currentVelocity.y = 0;
                }
                else
                {
                    // Root motion drives x/z via OnAnimatorMove; only apply gravity here
                    currentVelocity.y = ySpeed;
                    motionDriver.Move(new Vector3(0, currentVelocity.y, 0) * Time.deltaTime);
                    currentVelocity.y = 0;
                }
            }

            // Prevent Wall Slide
            if (preventWallSlide && motionDriver.GetVelocity().magnitude > 0.05f)
            {
                moveDir = motionDriver.GetVelocity();
                moveDir.y = 0;
            }

            // Rotate
            if (!PreventRotation)
            {
                var preSet = targetRotation;
                SetTargetRotation(moveDir, ref targetRotation);

                float turnSpeed = Mathf.Lerp(rotationSpeed * 100f, 2 * rotationSpeed * 100f, moveSpeed / runSpeed);
                transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRotation, Time.deltaTime * turnSpeed);
            }
            else
                targetRotation = transform.rotation;
        }

        public void SetCurrentVelocity(float ySpeed, Vector3 currentVelocity, bool setMoveAmount = true)
        {
            SetCurrentVelocity(ySpeed, currentVelocity, setMoveAmount, false);
        }

        public void SetCurrentVelocity(float ySpeed, Vector3 currentVelocity, bool setMoveAmount, bool lockUntilGrounded)
        {
            // While the character is not grounded, apply gravity and move the character towards the landing position.
            this.ySpeed = ySpeed;
            this.currentVelocity = currentVelocity;
            currentVelocity.y = 0;
            jumpMaxPosY = transform.position.y - 1;
            airSpeed = Mathf.Max(jumpMoveSpeed, currentVelocity.magnitude);
            lockAirVelocity = lockUntilGrounded;

            if (setMoveAmount)
                Animator.SetFloat(AnimatorParameters.moveAmount, Mathf.Clamp01(currentVelocity.magnitude) * 0.5f);
        }

        void SetTargetRotation(Vector3 desiredDir, ref Quaternion targetRotation)
        {
            if (desiredDir.sqrMagnitude > 0)
            {
                if (!useMultiDirectionalAnimation)
                {
                    targetRotation = Quaternion.LookRotation(desiredDir);
                    return;
                }

                // Face the desired facing direction
                desiredFacingDir.y = 0;

                // When fully locked to a facing direction (e.g., combat strafe), skip
                // both hip-rotation adjustments — they were designed for blend < 1
                if (characterDirectionBlend >= 1f)
                {
                    targetRotation = Quaternion.LookRotation(desiredFacingDir);
                    return;
                }

                float dotProd = 1f;
                if (rotateHipForBackwardAnimation)
                    dotProd = Vector3.Dot(desiredFacingDir, desiredDir) + 0.35f;

                if (dotProd > 0 || Animator.GetFloat(AnimatorParameters.idleType) > 0.7f)
                    targetRotation = Quaternion.LookRotation(Vector3.Lerp(desiredDir, desiredFacingDir, characterDirectionBlend - Animator.GetFloat(AnimatorParameters.idleType)));
                else
                    targetRotation = Quaternion.LookRotation(Vector3.Lerp(-desiredDir, desiredFacingDir, characterDirectionBlend - backwardHipRotationBlend - Animator.GetFloat(AnimatorParameters.idleType)));

                var playerMoveDirDot = Vector3.Dot(targetRotation * Vector3.forward, desiredDir.normalized) - (1 - forwardHipRotationBlend);
                targetRotation = Quaternion.LookRotation(Vector3.Lerp(targetRotation * Vector3.forward, desiredDir, playerMoveDirDot - Animator.GetFloat(AnimatorParameters.idleType)));

            }
            else if (snapToDesiredFacingDir && useMultiDirectionalAnimation)
            {
                targetRotation = Quaternion.LookRotation(desiredFacingDir);
            }
        }

        // Voluntary crouch intent only. The shared animator/collider outputs are applied by the
        // single compositor (ApplyStanceToAnimatorAndCollider) after both producers have run.
        public void HandleCrouching()
        {
            crouchAmount = IsCrouching ? 0.5f : 0f;
        }

        public void HandleBalancing()
        {
            if (enableBalanceWalk && isGrounded)
            {
                if (balanceWalkDetectionType == BalanceWalkDetectionType.Dynamic)
                    HandleBalanceOnNarrowBeam();
                else if (balanceWalkDetectionType == BalanceWalkDetectionType.Tagged)
                    HandleBalanceOnNarrowBeamWithTag();
                else if (balanceWalkDetectionType == BalanceWalkDetectionType.Both)
                {
                    if (prevBalanceAmount < 0.5f)
                        HandleBalanceOnNarrowBeamWithTag();
                    if (balanceAmount < 0.5f || prevBalanceAmount > 0.5f)
                    {
                        HandleBalanceOnNarrowBeam();
                        prevBalanceAmount = balanceAmount;
                    }
                }
            }
        }

        private void CalculateForwardAndStrafeSpeed(out float forwardSpeed, out float strafeSpeed)
        {
            var characterVelocity = motionDriver.GetVelocity();
            characterVelocity.y = 0;

            forwardSpeed = Vector3.Dot(characterVelocity, transform.forward);
            strafeSpeed = Vector3.Dot(characterVelocity, transform.right);
        }

        private void UpdateRotationFromAir()
        {
            if (useMultiDirectionalAnimation)
                transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRotation, Time.deltaTime * 100 * rotationSpeed);
            else
                transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(moveDir), Time.deltaTime * 100 * rotationSpeed);
        }

        float CalculateSprintMoveSpeed()
        {
            var curSpeedDir = currentVelocity;
            curSpeedDir.y = 0;

            float sprintDirModifier = Vector3.Dot(curSpeedDir.normalized, transform.forward);
            sprintDirModifier = sprintDirModifier > sprintDirectionThreshold ? sprintDirModifier : 0;
            return Mathf.Lerp(moveSpeed, sprintSpeed, Mathf.Clamp01(sprintDirModifier));
        }

        void UpdateCurrentVelocity()
        {
            if (desiredVelocity.magnitude != 0)
                currentVelocity = Vector3.MoveTowards(currentVelocity, desiredVelocity, acceleration * Time.deltaTime);
            else
                currentVelocity = Vector3.MoveTowards(currentVelocity, Vector3.zero, deceleration * Time.deltaTime);

            if (currentVelocity.magnitude > MaxSpeed)
                currentVelocity = Vector3.MoveTowards(currentVelocity, currentVelocity.normalized * MaxSpeed, deceleration * Time.deltaTime);
        }

        void UpdateCurrentVelocityFromAir()
        {
            if (lockAirVelocity)
            {
                currentVelocity.y = ySpeed;
                return;
            }

            var targetSpeed = Mathf.Max(jumpMoveSpeed, airSpeed);
            var fallVelocity = new Vector3((moveDir * targetSpeed).x, motionDriver.GetVelocity().y, (moveDir * targetSpeed).z);
            currentVelocity = Vector3.MoveTowards(currentVelocity, fallVelocity, jumpMoveAcceleration * Time.deltaTime);
            currentVelocity.y = ySpeed;
        }

        void UpdateVelocityAndDirFromLedge()
        {
            var (ledgeMoveDir, ledgeCurrentVel) = LedgeMovement(currentVelocity.normalized, currentVelocity);

            moveDir = ledgeMoveDir;
            currentVelocity = ledgeCurrentVel;

            if (Vector3.Dot(currentVelocity.normalized, ledgeMoveDir.normalized) < slidingMovementThresholdFromLedge - 0.02f)
                currentVelocity = Vector3.zero;
        }

        void TryVerticalJump()
        {
            if (!canPerformVerticalJump || !isGrounded || PreventVerticalJump) return;

            // Check if there is space above the head to execute the jump
            var headHit = Physics.SphereCast(Animator.GetBoneTransform(HumanBodyBones.Head).position, .15f, Vector3.up,
                out RaycastHit headHitData, headHeightThreshold, environmentScanner.ObstacleLayer);
            if (!headHit)
                StartCoroutine(HandleVerticalJump());
        }

        public void HandleTurningAnimations()
        {
            var rotDiff = transform.eulerAngles - prevAngle;
            var threshold = moveSpeed >= runSpeed ? 0.025 : 0.1;
            if (rotDiff.sqrMagnitude < threshold)
            {
                rotationValue = 0;
            }
            else
            {
                rotationValue = Mathf.Sign(rotDiff.y) * .5f;
            }

            Animator.SetFloat(AnimatorParameters.rotation, rotationValue, 0.35f, Time.deltaTime);

            prevAngle = transform.eulerAngles;
        }

        public IEnumerator HandleVerticalJump()
        {
            // We're waiting since other actions like predictive jump/climb might interupt the vertical jump
            yield return new WaitForFixedUpdate();
            yield return null;

            if (InterruptActions) yield break;

            BlockLocomotion();

            var velocity = Vector3.zero;

            //Calculates the initial vertical velocity required for jumping
            var velocityY = Mathf.Sqrt(2 * Mathf.Abs(gravity) * 1.5f);
            Animator.SetBool(AnimatorParameters.IsGrounded, false);

            //animator.SetFloat(AnimatorParameters.moveAmount, 0);
            isGrounded = false;
            airSpeed = Mathf.Max(jumpMoveSpeed, new Vector3(currentVelocity.x, 0f, currentVelocity.z).magnitude);
            Animator.CrossFadeInFixedTime("FallTree", .12f);

            var characterVelocity = motionDriver.GetVelocity();

            //yield return new WaitForSeconds(0.1f);
            var time = 0f;
            while ((time += Time.deltaTime) <= 0.1f)
            {
                motionDriver.Move(currentVelocity * Time.deltaTime);
                yield return null;

                if (InterruptActions)
                {
                    UnblockLocomotion();
                    yield break;
                }
            }

            UnblockLocomotion();
            ySpeed = velocityY;
        }

        public IEnumerator DoLocomotionAction(string anim, bool useRootMovement = true,
            bool useRootRotation = true, bool useRotationCorrection = false, float rotationCorrectionFactor = 1f,
            Action onComplete = null, float crossFadeTime = 0.2f, Quaternion? targetRotation = null,
            bool setMoveAmount = false, bool requiresLedgeBlocking = true, bool usesGravity = false)
        {

            OnLocomotionActionStart?.Invoke();

            // motionDriver block is driven by ActionExecutor's OnActionStarted event
            // (subscribed by NavmeshHybridDriver/NavmeshDriver); no manual call needed.

            var action = new CharacterAction()
            {
                animName = anim,
                usesRootMotion = true,
                usesRootMovement = useRootMovement,
                usesRootRotation = useRootRotation,
                usesGravity = usesGravity,
                resetMoveAmount = !setMoveAmount,
                animationParameters = new AnimParameters()
                {
                    transitionIn = crossFadeTime,
                    onAnimationUpdate = (float normalizedTime, float timer) =>
                    {
                        if (InterruptActions)
                            ActionExecutor.CancelCurrent();

                        if (setMoveAmount)
                        {
                            float animLength = (timer / normalizedTime);
                            bool isRunning = moveType != MoveType.Walk;
                            Animator.SetFloat(AnimatorParameters.moveAmount, MoveAmount * (isRunning ? 1f : 0.5f),
                                (animLength) * 1.3f, 1f * Time.deltaTime);
                        }

                        if (targetRotation.HasValue && !PreventRotation)
                            transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRotation.Value, 500f * Time.deltaTime);

                        if (useRotationCorrection)
                        {
                            var deltaRot = Animator.deltaRotation;

                            // same as multiplying the scale factor but this prevents gimbal lock 
                            deltaRot = Quaternion.SlerpUnclamped(Quaternion.identity, deltaRot,
                                rotationCorrectionFactor);

                            transform.rotation *= deltaRot;
                        }
                    }
                },
                requiresLedgeBlocking = requiresLedgeBlocking
            };


            yield return ActionExecutor.ExecuteAnimatorAction(action);

            onComplete?.Invoke();

            OnLocomotionActionEnd?.Invoke();
        }

        public IEnumerator DoLocomotionAction(AnimGraphClipInfo clipInfo, bool useRootMovement = false,
            bool useRootRotation = true, bool useRotationCorrection = false, float rotationCorrectionFactor = 1f,
            Action onComplete = null, float animationAngle = 0, float turnDirection = 0)
        {
            if (clipInfo == null || clipInfo.clip == null)
                yield break;

            OnLocomotionActionStart?.Invoke();
            // motionDriver block driven by ActionExecutor lifecycle events; see overload above.

            float accumulatedRawRotation = 0f;
            float prevNormalizedTime = 0f;

            var action = new CharacterAction(clipInfo)
            {
                usesRootMotion = true,
                usesRootMovement = useRootMovement,
                usesRootRotation = useRootRotation,
                animationParameters = new AnimParameters()
                {
                    onAnimationUpdate = (float normalizedTime, float timer) =>
                    {
                        if (useRotationCorrection && !PreventRotation)
                            ApplyRotationCorrection(normalizedTime, animationAngle, turnDirection,
                                rotationCorrectionFactor, ref accumulatedRawRotation, ref prevNormalizedTime);
                    }
                }
            };

            yield return ActionExecutor.ExecuteAsync(action);

            onComplete?.Invoke();
            OnLocomotionActionEnd?.Invoke();
        }

        /// <summary>
        /// Rotates the character towards desiredFacingDir while a turn animation is playing.
        /// Scales the clip's root rotation so the character covers the requested angle even when
        /// it differs from the animation's baked angle, and applies a time-based completion
        /// guarantee so the turn always reaches the desired direction by the end of the clip.
        /// </summary>
        void ApplyRotationCorrection(float normalizedTime, float animationAngle, float turnDirection,
            float rotationCorrectionFactor, ref float accumulatedRawRotation, ref float prevNormalizedTime)
        {
            var deltaRot = Animator.deltaRotation;

            if (animationAngle > 0 && desiredFacingDir.sqrMagnitude > 0.01f)
            {
                // To get the angle from a Quaterion, we could use Quaternion.Angle function
                // but the problem with it is, it floors anything below ~0.162° (kEpsilon) to zero
                // So at high frame rates, it silently discard per-frame rotation. So here we are using
                // the a Asin based calculation to find the angle from Quaternion since it's more accurate
                float deltaAngle = 2f * Mathf.Asin(Mathf.Clamp01(Mathf.Sqrt(
                    deltaRot.x * deltaRot.x + deltaRot.y * deltaRot.y + deltaRot.z * deltaRot.z))) * Mathf.Rad2Deg;
                accumulatedRawRotation += deltaAngle;

                Quaternion targetRot = Quaternion.LookRotation(desiredFacingDir, Vector3.up);
                float signedRemaining = Vector3.SignedAngle(transform.forward, targetRot * Vector3.forward, Vector3.up);

                // Only rotate if the target is still in the original turn direction.
                float remainingInDir = signedRemaining * turnDirection;
                if (remainingInDir > 0)
                {
                    // SCALE THE DELTA ANGLE. For example, if we want to rotate by 100 degrees,
                    // but if we only have a 90 degree turn animation, then we'll scale the rotation
                    // to make the character rotate by 100 degrees
                    float remainingRawRotation = Mathf.Max(animationAngle - accumulatedRawRotation, 0.1f);
                    float scaledDeltaAngle = deltaAngle * (remainingInDir / remainingRawRotation);

                    // Time-based completion guarantee: scaled animation angle will not perfectly
                    // turn the character to the desired direction since the deltaRotation can
                    // be incorrect during transitions. So we find the exact angle required to reach
                    // the desired direction, and we use it instead of deltaAngle if it's value
                    // is greater than deltaAngle
                    float dNorm = Mathf.Max(normalizedTime - prevNormalizedTime, 0f);
                    float remainingNorm = Mathf.Max(1f - normalizedTime, 1e-4f);
                    float exactRequiredAngle = remainingInDir * Mathf.Clamp01(dNorm / remainingNorm);

                    float maxDegrees = Mathf.Max(scaledDeltaAngle, exactRequiredAngle);
                    transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRot, maxDegrees);
                }

                prevNormalizedTime = normalizedTime;
            }
            else
            {
                // Static fallback (original behavior)
                deltaRot = Quaternion.SlerpUnclamped(Quaternion.identity, deltaRot, rotationCorrectionFactor);
                transform.rotation *= deltaRot;
            }
        }

        bool TryIdleTurn(bool forceRotation = false)
        {
            if (desiredMoveDir.sqrMagnitude < 0.05f && desiredFacingDir.sqrMagnitude > 0.01f)
            {
                Quaternion idleRotation = default(Quaternion);
                SetTargetRotation(desiredFacingDir, ref idleRotation);
                float angle = Vector3.SignedAngle(transform.forward, idleRotation * Vector3.forward, Vector3.up);

                float sign = Mathf.Sign(angle);
                angle = Mathf.Abs(angle);

                if (!forceRotation && angle < idleRotationTriggerAngle) return false;

                float animationAngle = 0f;

                if (angle <= 77f)
                    animationAngle = 45f;
                else if (angle > 77f && angle <= 112f)
                    animationAngle = 90f;
                else if (angle > 112f && angle <= 157f)
                    animationAngle = 135f;
                else if (angle > 157f && angle <= 180f)
                    animationAngle = 180f;

                bool mirrored = sign < 0;
                var clipInfo = GetIdleTurnClip(animationAngle, mirrored);

                if (clipInfo != null && clipInfo.clip != null)
                {
                    isIdleTurning = true;
                    StartCoroutine(DoLocomotionAction(clipInfo, useRootRotation: false, useRotationCorrection: true,
                        rotationCorrectionFactor: angle / animationAngle, animationAngle: animationAngle, turnDirection: sign,
                        onComplete: () =>
                        {
                            ResetTargetRotation();
                            isIdleTurning = false;
                        }));

                    // Sync targetRotation now so the gap frame before BlockLocomotion
                    // becomes true doesn't snap the AI back to the pre-turn direction
                    targetRotation = transform.rotation;

                    return true;
                }
            }

            return false;
        }

        AnimGraphClipInfo GetIdleTurnClip(float animationAngle, bool mirrored)
        {
            var activeTurns = useIdleTurnOverrides && overrideIdleTurns != null ? overrideIdleTurns : idleTurns;
            return activeTurns != null ? activeTurns.GetClip(animationAngle, mirrored) : null;
        }

        public void OverrideTurns(IdleTurns idleTurns)
        {
            if (idleTurns != null)
            {
                useIdleTurnOverrides = true;
                overrideIdleTurns = idleTurns;
            }
            else
            {
                useIdleTurnOverrides = false;
                overrideIdleTurns = null;
            }
        }

        Quaternion velocityRotation;
        bool TryQuickTurn()
        {
            if (desiredMoveDir == Vector3.zero) return false;

            SetTargetRotation(desiredMoveDir, ref velocityRotation);
            var angle = Vector3.SignedAngle(transform.forward, velocityRotation * Vector3.forward, Vector3.up);

            if (!PreventTurnBacks && Mathf.Abs(angle) > 130 && MoveAmount > QuickTurnThreshhold
                && Animator.GetFloat(AnimatorParameters.idleType) < 0.2f
                && Physics.Raycast(transform.position + Vector3.up * 0.1f + transform.forward * 0.3f + transform.forward * MoveAmount / 1.5f, Vector3.down, 0.3f)
                && !Physics.Raycast(transform.position + Vector3.up * 0.1f, transform.forward, 0.6f))
            {
                isTurningBack = true;
                Animator.SetBool(AnimatorParameters.turnback_Mirror, angle <= 0);
                bool isInLocomotionBlendTree = Animator.GetCurrentAnimatorStateInfo(0).IsName("Locomotion");

                StartCoroutine(DoLocomotionAction("Running Turn 180",
                    crossFadeTime: isInLocomotionBlendTree ? 0.2f : 0.2f, setMoveAmount: true,
                    onComplete: () =>
                    {
                        currentVelocity = runSpeed * transform.forward * (MoveAmount);
                        motionDriver.Move(currentVelocity * Time.deltaTime);

                        targetRotation = transform.rotation;
                        isTurningBack = false;
                    }));
                return true;
            }
            return false;
        }

        // Locomotion animator state used for turning, and the base state to settle back into
        // once a turn lands.
        const string TurnState = "Running Turn 180";
        const string LocomotionState = "Locomotion";
        const float SettleTransition = 0.12f;
        const float SettleTimeout = 0.3f;

        // How close (deg) to the target facing counts as aligned.
        const float AlignedAngle = 1.5f;

        /// <summary>
        /// Rotates <paramref name="actor"/> to face <paramref name="faceDir"/> (horizontal) by
        /// playing the character's "Running Turn 180" state and letting the animation's root
        /// motion drive the rotation (<c>usesRootRotation</c> → applied in
        /// <see cref="OnAnimatorMove"/>).
        ///
        /// The turn is played with <c>ActionExecutor.ExecuteAnimatorAction</c> directly
        /// (not <c>DoLocomotionAction</c>, which self-cancels while the actor is focused in an
        /// interaction). The clip is authored for ~180°, so the turn is cancelled the moment the
        /// actor reaches the target facing — that way it lands exactly on smaller angles instead
        /// of over-rotating.
        ///
        /// Falls back to a direct rotation only when the actor has no locomotion core.
        /// </summary>
        public static IEnumerator RotateToFace(LocomotionCore loco, Transform actor,
                                               Vector3 faceDir, float doneAngle = 5f)
        {
            if (actor == null) yield break;

            faceDir.y = 0f;
            if (faceDir.sqrMagnitude < 1e-4f) yield break;
            faceDir.Normalize();

            Vector3 fwd = actor.forward; fwd.y = 0f;
            if (fwd.sqrMagnitude < 1e-6f) yield break;

            float signedAngle = Vector3.SignedAngle(fwd, faceDir, Vector3.up);
            if (Mathf.Abs(signedAngle) <= doneAngle) yield break;

            var exec = loco != null ? loco.ActionExecutor : null;
            if (exec != null && loco.Animator != null)
            {
                // Mirror the turn for its direction (matches TryQuickTurn).
                loco.Animator.SetBool(AnimatorParameters.turnback_Mirror, signedAngle <= 0f);

                float startSign = Mathf.Sign(signedAngle);
                var turn = new CharacterAction
                {
                    animName = TurnState,
                    usesRootMotion = true,
                    usesRootMovement = false,   // turn in place (no forward drift)
                    usesRootRotation = true,    // rotation comes from the animation's root motion
                    usesGravity = false,
                    resetMoveAmount = true,
                    requiresLedgeBlocking = false,
                    animationParameters = new AnimParameters
                    {
                        transitionIn = 0.2f,
                        // Stop the root-motion turn once the actor has reached (or just passed)
                        // the target facing, so a full-180° clip lands exactly on any angle.
                        onAnimationUpdate = (normalizedTime, timer) =>
                        {
                            Vector3 curFwd = actor.forward; curFwd.y = 0f;
                            float remaining = Vector3.SignedAngle(curFwd, faceDir, Vector3.up);
                            if (Mathf.Abs(remaining) <= AlignedAngle || Mathf.Sign(remaining) != startSign)
                                exec.CancelCurrent();
                        }
                    },
                };

                yield return exec.ExecuteAnimatorAction(turn);

                // Clean up the sub-degree remainder for an exact finish.
                actor.rotation = Quaternion.LookRotation(faceDir, Vector3.up);

                // The turn was cancelled mid-play, so the animator is still in the turn state.
                // Settle it back into Locomotion (and wait until it has actually left the turn)
                // so a following turn/animation starts from a clean state instead of re-entering
                // a still-playing turn — which snaps (or mirror-flips) the pose.
                loco.Animator.CrossFade(LocomotionState, SettleTransition);
                float settle = 0f;
                while (settle < SettleTimeout &&
                       loco.Animator.GetCurrentAnimatorStateInfo(0).IsName(TurnState))
                {
                    settle += Time.deltaTime;
                    yield return null;
                }
                yield break;
            }

            // No locomotion core — rotate directly so it still faces the target.
            Quaternion target = Quaternion.LookRotation(faceDir, Vector3.up);
            while (Quaternion.Angle(actor.rotation, target) > 1f)
            {
                actor.rotation = Quaternion.RotateTowards(actor.rotation, target, 360f * Time.deltaTime);
                yield return null;
            }
            actor.rotation = target;
        }

        void TryQuickStop(float forwardSpeed, float strafeSpeed, float currentRunSpeed)
        {
            if ((MoveAmount > runToStopThreshhold && (desiredVelocity.magnitude == 0 || forwardSpeed / currentRunSpeed < 0.1f))
                && Animator.GetFloat(AnimatorParameters.idleType) < 0.2f)
            {
                currentVelocity = Vector3.zero;
                Animator.SetBool(AnimatorParameters.turnback_Mirror, strafeSpeed > 0.03f);
                Animator.SetFloat(AnimatorParameters.runToStopAmount, MoveAmount);
                StartCoroutine(DoLocomotionAction("Run To Stop", crossFadeTime: 0.3f,
                    onComplete: () =>
                    {
                        Animator.SetFloat(AnimatorParameters.moveAmount, 0);
                    }));
            }
        }

        void TryToJumpFromLedge()
        {
            if (desiredMoveDir == Vector3.zero || IsLocomotionBlocked || !isGrounded)
                return;

            var hitData = environmentScanner.ObstacleCheck(performHeightCheck: false);
            if (!hitData.forwardHitFound && !Physics.Raycast(transform.position + Vector3.up * 0.1f, transform.forward, 0.5f, environmentScanner.ObstacleLayer))
            {
                StartCoroutine(DoLocomotionAction("Jump Down", targetRotation: Quaternion.LookRotation(desiredMoveDir),
                    requiresLedgeBlocking: false));
                isOnLedge = false;
                Animator.SetBool(AnimatorParameters.IsGrounded, isGrounded = false);
                return;
            }
        }

        public IEnumerator PerformLanding(bool hardland = false)
        {
            if (hardland)
            {
                BlockLocomotion();

                motionDriver.Move(Vector3.down);
                var halfExtends = new Vector3(.3f, .9f, 0.01f);
                var hasSpaceForRoll = Physics.BoxCast(transform.position + Vector3.up, halfExtends, transform.forward, Quaternion.LookRotation(transform.forward), 2.5f, environmentScanner.ObstacleLayer);

                halfExtends = new Vector3(.1f, .9f, 0.01f);
                var heightHiting = true;
                for (int i = 0; i < 6 && heightHiting; i++)
                    heightHiting = Physics.BoxCast(transform.position + Vector3.up * 1.8f + transform.forward * (i * .5f + .5f), halfExtends, Vector3.down, Quaternion.LookRotation(Vector3.down), 2.2f + i * .1f, environmentScanner.ObstacleLayer);

                OnLandingActionStarted?.Invoke();

                if (!hasSpaceForRoll && heightHiting)
                {
                    // itemEquipper.PreventItemSwitching = true;
                    yield return DoLocomotionAction("FallingToRoll",
                        onComplete: () =>
                        {
                            currentVelocity = runSpeed * transform.forward * (MoveAmount);
                            motionDriver.Move(currentVelocity * Time.deltaTime);

                            targetRotation = transform.rotation;
                            // itemEquipper.PreventItemSwitching = false;
                        }
                        , crossFadeTime: .1f, setMoveAmount: true, usesGravity: true);
                }
                else
                    yield return DoLocomotionAction("Landing", useRootMovement: false, crossFadeTime: .1f);

                UnblockLocomotion();
            }
            else
            {
                Animator.CrossFadeInFixedTime("LandAndStepForward", 0.13f);
                currentVelocity *= Mathf.Clamp01(Vector3.Dot(transform.forward, currentVelocity));
                currentVelocity = transform.forward * currentVelocity.magnitude;
            }
        }

        public Vector3 globalDeltaPos;
        void OnAnimatorMove()
        {
            if (Time.frameCount - ActionExecutor.AnimGraph.LastRebakeFrame <= 1) return;

            globalDeltaPos = Vector3.zero;

            var currAction = ActionExecutor.CurrentAction;

            // Linear motion warp is self-sufficient, so it must apply even when usesRootMotion is false.
            if (ActionExecutor.IsLinearMotionWarpActive())
            {
                if (ActionExecutor.UsesRootRotation)
                    transform.rotation *= Animator.deltaRotation;

                var deltaPos = ActionExecutor.MotionWarpingDeltaPos;

                if (ActionExecutor.RequiresLedgeBlocking && IsLedgeBlocked(deltaPos.normalized, deltaPos))
                    return;

                globalDeltaPos = deltaPos;
                motionDriver.Move(deltaPos, true, ActionExecutor.IgnoreCollision);
                return;
            }

            if (StopAllMovement) return;

            if (ActionExecutor.UsesRootMotion)
            {
                if (ActionExecutor.UsesRootRotation)
                    transform.rotation *= Animator.deltaRotation;

                if (ActionExecutor.UsesRootMovement)
                {
                    var deltaPos = Animator.deltaPosition;

                    if (ActionExecutor.IsMotionWarping)
                    {
                        deltaPos = ActionExecutor.MotionWarpingDeltaPos;
                    }

                    if (ActionExecutor.RequiresLedgeBlocking && IsLedgeBlocked(deltaPos.normalized, deltaPos))
                        return;

                    globalDeltaPos = deltaPos;

                    if (ActionExecutor.UsesGravity && !ActionExecutor.IgnoreCollision)
                    {
                        var isGrounded = CheckIsGrounded();
                        if (isGrounded) 
                            ySpeed = -0.5f;
                        else
                            ySpeed += Physics.gravity.y * Time.deltaTime;

                        float gravityModifier =  currAction.isAnimGraphAction? currAction.clipInfo.gravityModifier.GetValue(ActionExecutor.AnimGraph.currentClipStateInfo.normalizedTime) : 1;

                        deltaPos.y = ySpeed * gravityModifier * Time.deltaTime;
                    }

                    motionDriver.Move(deltaPos, true, ActionExecutor.IgnoreCollision);
                }
            }
            else if (UseLocomotionRootMotion)
            {
                transform.rotation *= Animator.deltaRotation;
                motionDriver.Move(Animator.deltaPosition, true, false);
            }
        }

        public void GroundCheck()
        {
            isGrounded = CheckIsGrounded();
            Animator.SetBool(AnimatorParameters.IsGrounded, isGrounded);
        }

        public bool CheckIsGrounded()
        {
            if (FSSettings.i == null) return false;
            return Physics.CheckSphere(transform.TransformPoint(groundCheckOffset), groundCheckRadius, FSSettings.i.GroundLayer) || motionDriver.IsGrounded();
        }

        public void SetCrouching(bool value)
        {
            if (!CanCrouch) value = false;
            IsCrouching = value;
        }

        public void EnableCrouching()
        {
            CanCrouch = true;
        }

        public void DisableCrouching()
        {
            CanCrouch = false;
            IsCrouching = false;
        }

        public void SetMoveType(MoveType moveType)
        {
            if (IsWalkOnly && moveType != MoveType.Walk)
                moveType = MoveType.Walk;
            this.moveType = moveType;
        }

        public void EnableWalkOnly()
        {
            IsWalkOnly = true;
            if (moveType != MoveType.Walk)
                moveType = MoveType.Walk;
        }

        public void DisableWalkOnly()
        {
            IsWalkOnly = false;
        }

        public void WarpToPosition(Vector3 pos)
        {
            motionDriver.WarpToPosition(pos);
        }

        public void Move(Vector3 deltaPos)
        {
            motionDriver.Move(deltaPos);
        }

        public float GetBlendTreeSpeed()
        {
            if (moveType == MoveType.Sprint)
                return 1.5f;
            else if (moveType == MoveType.Run)
                return 1f;
            else
                return 0.2f;
        }

        public void SetTargetRotation(Quaternion targetRotation)
        {
            this.targetRotation = targetRotation;
        }

        public void ResetTargetRotation()
        {
            targetRotation = transform.rotation;
        }

        public void ResetCurrentVelocity()
        {
            currentVelocity *= 0;
        }

        /// <summary>
        /// Eases the locomotion blend back to a standing idle. Call it when something stops feeding
        /// this character movement every frame — the animator holds the last values written to it, so
        /// a character left mid-blend keeps playing that walk on the spot until someone writes over
        /// it.
        ///
        /// strafeAmount settles with the rest: a character that was strafing (multi-directional
        /// locomotion) blends on a second axis, and zeroing only the forward one leaves it side-
        /// stepping in place.
        /// </summary>
        public void ResetAnimationMotionParams()
        {
            StartCoroutine(TweenUtil.TweenVal(Animator.GetFloat(AnimatorParameters.moveAmount), 0, 0.15f, (lerpVal) => Animator.SetFloat(AnimatorParameters.moveAmount, lerpVal)));
            StartCoroutine(TweenUtil.TweenVal(Animator.GetFloat(AnimatorParameters.strafeAmount), 0, 0.15f, (lerpVal) => Animator.SetFloat(AnimatorParameters.strafeAmount, lerpVal)));
            StartCoroutine(TweenUtil.TweenVal(Animator.GetFloat(AnimatorParameters.rotation), 0, 0.15f, (lerpVal) => Animator.SetFloat(AnimatorParameters.rotation, lerpVal)));
            StartCoroutine(TweenUtil.TweenVal(Animator.GetFloat(AnimatorParameters.idleType), 0, 0.15f, (lerpVal) => Animator.SetFloat(AnimatorParameters.idleType, lerpVal)));
        }

        public void UseMultiDirectionalAnimations(bool value)
        {
            useMultiDirectionalAnimation = value;

            SetLocomotionTypeSmoothly(useMultiDirectionalAnimation ? 1 : 0);
            //Animator.SetFloat(AnimatorParameters.locomotionType, useMultiDirectionalAnimation ? 1 : 0);
        }

        public void ResetUseMultiDirectionalAnimations()
        {
            useMultiDirectionalAnimation = _useMultiDirectionalAnimation;
            SetLocomotionTypeSmoothly(useMultiDirectionalAnimation ? 1 : 0);
            //Animator.SetFloat(AnimatorParameters.locomotionType, useMultiDirectionalAnimation ? 1 : 0);
        }

        public void SetPreventFallingFromLedge(bool value)
        {
            _preventFallingFromLedge = value;
        }

        public void ResetPreventFallingFromLedge()
        {
            _preventFallingFromLedge = preventFallingFromLedge;
        }

        void HandleBalanceOnNarrowBeam()
        {
            bool leftFootHit, rightFootHit;
            int hitCount = 0;

            Vector3 right = transform.right * 0.3f, forward = transform.forward * 0.3f, up = Vector3.up * 0.2f;

            hitCount += Physics.CheckCapsule(transform.position - right + up, transform.position - right - up, 0.1f, FSSettings.i.GroundLayer) ? 1 : 0;
            hitCount += Physics.CheckCapsule(transform.position + right + up, transform.position + right - up, 0.1f, FSSettings.i.GroundLayer) ? 1 : 0;
            hitCount += (rightFootHit = Physics.CheckCapsule(transform.position + forward + up, transform.position + forward - up, 0.1f, FSSettings.i.GroundLayer)) ? 1 : 0;
            hitCount += (leftFootHit = Physics.CheckCapsule(transform.position - forward + up, transform.position - forward - up, 0.1f, FSSettings.i.GroundLayer)) ? 1 : 0;


            if ((rightFootHit || leftFootHit) && !Physics.Linecast(transform.position + up, transform.position - up, FSSettings.i.GroundLayer)) // for predictive jump cases
                hitCount -= 1;

            balanceAmount = hitCount > 2 ? 0f : 1f;
            balanceHasSpace = leftFootHit && rightFootHit;
        }

        void HandleBalanceOnNarrowBeamWithTag()
        {
            var hitObjects = Physics.OverlapSphere(transform.TransformPoint(new Vector3(0f, 0.15f, 0.07f)), .2f).ToList().Where(g => g.gameObject.tag == "NarrowBeam" || g.gameObject.tag == "SwingableLedge").ToArray();
            balanceAmount = hitObjects.Length > 0 ? 1f : 0f;
            if (balanceAmount > 0f)
            {
                var leftFootHit = Physics.SphereCast(transform.position - transform.forward * 0.3f + Vector3.up * footRayHeight / 2, 0.1f, Vector3.down, out RaycastHit leftHit, footRayHeight + footOffset, FSSettings.i.GroundLayer);
                var rightFootHit = Physics.SphereCast(transform.position + transform.forward * 0.3f + Vector3.up * footRayHeight / 2, 0.1f, Vector3.down, out RaycastHit rightHit, footRayHeight + footOffset, FSSettings.i.GroundLayer);
                balanceHasSpace = leftFootHit && rightFootHit;
            }
            else
            {
                balanceHasSpace = true;
            }
        }

        public void ApplyStanceToAnimatorAndCollider()
        {
            Animator.SetFloat(AnimatorParameters.idleType, crouchVal, 0.2f, Time.deltaTime);

            if (Animator.GetFloat(AnimatorParameters.idleType) > .2f)
                Animator.SetFloat(AnimatorParameters.crouchType, balanceHasSpace ? 0 : 1, 0.2f, Time.deltaTime);

            if (crouchVal > 0.3f)
                characterCapsule?.ScaleColliderSize(0.7f);
            else
                characterCapsule?.ResetColliderSize();
        }

        float _walkSpeed;
        float _runSpeed;
        float _sprintSpeed;

        public void ChangeMoveSpeed(float walk, float run, float sprint)
        {
            walkSpeed = walk;
            runSpeed = run;
            sprintSpeed = sprint;
        }
        public void ResetMoveSpeed()
        {
            walkSpeed = _walkSpeed;
            runSpeed = _runSpeed;
            sprintSpeed = _sprintSpeed;
        }

        public void DisableTurningAnimations()
        {
            _enableTurningAnim = false;
            _playQuickTurnAnimation = false;
            _playQuickStopAnimation = false;
            Animator.SetFloat(AnimatorParameters.rotation, 0f);
        }

        public void ResetTurningAnimations()
        {
            _enableTurningAnim = enableTurningAnim;
            _playQuickTurnAnimation = playQuickTurnAnimation;
            _playQuickStopAnimation = playQuickStopAnimation;
        }

        public (Vector3, Vector3) LedgeMovement(Vector3 currMoveDir, Vector3 currVelocity)
        {
            if (currMoveDir == Vector3.zero) return (currMoveDir, currVelocity);

            float yOffset = 0.5f;
            float xOffset = 0.4f;
            float forwardOffset = xOffset / 2f; // can control moveAngle here


            var radius = xOffset / 2; // can control moveAngle here


            if (Animator.GetFloat(AnimatorParameters.idleType) > 0.5f)
            {
                xOffset = 0.2f;
                radius = xOffset / 2f;       // crouch angle
                forwardOffset = xOffset / 2f; // can control moveAngle here
            }

            float maxAngle = 60f;
            float velocityMag = currVelocity.magnitude;
            var moveDir = currMoveDir;
            RaycastHit rightHit, leftHit, newHit;
            var positionOffset = transform.position + currMoveDir * xOffset;
            var rigthVec = Vector3.Cross(Vector3.up, currMoveDir);
            var rightLeg = transform.position + currMoveDir * forwardOffset + rigthVec * xOffset / 2;
            var leftLeg = transform.position + currMoveDir * forwardOffset - rigthVec * xOffset / 2;

            var rightFound = (Physics.Raycast(rightLeg + Vector3.up * yOffset, Vector3.down, out rightHit, yOffset + environmentScanner.ledgeHeightThreshold, environmentScanner.ObstacleLayer) && (rightHit.distance - yOffset) < environmentScanner.ledgeHeightThreshold && Vector3.Angle(Vector3.up, rightHit.normal) < maxAngle);
            var leftFound = (Physics.Raycast(leftLeg + Vector3.up * yOffset, Vector3.down, out leftHit, yOffset + environmentScanner.ledgeHeightThreshold, environmentScanner.ObstacleLayer) && (leftHit.distance - yOffset) < environmentScanner.ledgeHeightThreshold && Vector3.Angle(Vector3.up, leftHit.normal) < maxAngle);

            if (!rightFound) positionOffset += rigthVec * xOffset / 2;
            if (!leftFound) positionOffset -= rigthVec * xOffset / 2;

            isOnLedge = false;

            if (!(Physics.SphereCast(positionOffset + Vector3.up * yOffset /* + Vector3.up * radius */, radius, Vector3.down, out newHit, yOffset + environmentScanner.ledgeHeightThreshold, environmentScanner.ObstacleLayer)) || ((newHit.distance - yOffset) > environmentScanner.ledgeHeightThreshold && Vector3.Angle(Vector3.up, newHit.normal) > maxAngle))
            {
                isOnLedge = true;

                if (!rightFound || !leftFound)
                {
                    if (!(!rightFound && !leftFound) && preventLedgeRotation) //to restrict rot
                        currMoveDir = Vector3.zero;
                    currVelocity = Vector3.zero;
                    if (!(Physics.SphereCast(transform.position + currMoveDir * 2* xOffset + Vector3.up * yOffset /* + Vector3.up * radius */, radius, Vector3.down, out newHit, yOffset + environmentScanner.ledgeHeightThreshold, environmentScanner.ObstacleLayer)) || ((newHit.distance - yOffset) > environmentScanner.ledgeHeightThreshold && Vector3.Angle(Vector3.up, newHit.normal) > maxAngle))
                    {
                        if (!(!rightFound && !leftFound) && preventLedgeRotation) //to restrict rot
                            currMoveDir = Vector3.zero;
                        currVelocity = Vector3.zero;
                    }
                }
            }
            else if ((!rightFound || !leftFound))
            {
                if (rightFound)
                {
                    if (Physics.SphereCast(leftLeg + Vector3.up * yOffset, 0.1f, Vector3.down, out leftHit, yOffset + environmentScanner.ledgeHeightThreshold, environmentScanner.ObstacleLayer))
                        currVelocity = (newHit.point - leftHit.point).normalized * velocityMag;
                    else
                        currVelocity = (newHit.point - leftLeg).normalized * velocityMag;
                }
                else if (leftFound)
                {
                    if (Physics.SphereCast(rightLeg + Vector3.up * yOffset, 0.1f, Vector3.down, out rightHit, yOffset + environmentScanner.ledgeHeightThreshold, environmentScanner.ObstacleLayer))
                        currVelocity = (newHit.point - rightHit.point).normalized * velocityMag;
                    else
                        currVelocity = (newHit.point - rightLeg).normalized * velocityMag;
                }
                else if ((rightHit.transform != null && Vector3.Angle(Vector3.up, rightHit.normal) > maxAngle) || (leftHit.transform != null && Vector3.Angle(Vector3.up, leftHit.normal) > maxAngle))
                    currVelocity = Vector3.zero;
            }

            if (currVelocity == Vector3.zero)
                return (currMoveDir, currVelocity);
            return (new Vector3(currVelocity.x, 0, currVelocity.z), currVelocity);
        }

        public bool IsLedgeBlocked(Vector3 currMoveDir, Vector3 currVelocity)
        {
            if (currMoveDir == Vector3.zero) return false;

            float yOffset = 1f;
            float xOffset = 0.7f;

            var radius = 0.2f; // can control moveAngle here
            float maxAngle = 60f;

            RaycastHit newHit;
            var positionOffset = transform.position + currMoveDir * xOffset + Vector3.up * yOffset;

            var ledgeHeightThreshold = 1.4f;

            if (!(Physics.SphereCast(positionOffset, radius, Vector3.down, out newHit, yOffset + ledgeHeightThreshold, FSSettings.i.ObstructionLayer))
                || ((newHit.distance - yOffset) > ledgeHeightThreshold && Vector3.Angle(Vector3.up, newHit.normal) > maxAngle))
            {
                return true;
            }

            // More readable?
            //if (Physics.SphereCast(positionOffset, radius, Vector3.down, out newHit, yOffset + ledgeHeightThreshold, obstacleMask))
            //{
            //    // Check if distance and angle threshold matches
            //    if ((newHit.distance - yOffset) > ledgeHeightThreshold && Vector3.Angle(Vector3.up, newHit.normal) > maxAngle)
            //        return true;
            //}

            return false;
        }

        public void SetMaxSpeed(float maxSpeed)
        {
            MaxSpeed = maxSpeed;
        }

        public void ResetMaxSpeed()
        {
            MaxSpeed = sprintSpeed;
        }

        public void ReachDestination(Vector3 dest, float speed = 3f)
        {
            var newVelocity = (dest - transform.position).normalized * speed;
            currentVelocity = Vector3.MoveTowards(currentVelocity, newVelocity, acceleration * Time.deltaTime);

            motionDriver.Move(newVelocity * Time.deltaTime);
            var characterVelocity = motionDriver.GetVelocity();
            characterVelocity.y = 0;

            if (runSpeed > 0)
            {
                float forwardSpeed = Vector3.Dot(characterVelocity, transform.forward);
                Animator.SetFloat(AnimatorParameters.moveAmount, forwardSpeed / runSpeed, 0.1f, Time.deltaTime);

                float strafeSpeed = Vector3.Dot(characterVelocity, transform.right);
                Animator.SetFloat(AnimatorParameters.strafeAmount, strafeSpeed / runSpeed, 0.1f, Time.deltaTime);
            }
        }



        // ── Target navigation (alignment to interaction points etc.) ───────────────
        // Drivers handle position movement (CharacterController / NavMeshAgent).
        // LocomotionCore owns rotation so it can apply the correct multi-directional
        // or single-directional (rotate-after-arrival + turn animation) logic.

        float _motorSpeed;
        Vector3 prevPos;
        float originalLocomotionType;

        Quaternion _alignRot;
        bool _alignMultiDirectional;

        // Tunables
        [SerializeField] float defaultPosThreshold = 0.2f;
        [SerializeField] float defaultRotThreshold = 1f;

        /// <summary>
        /// Moves the character smoothly to a target position and rotation.
        /// Use this when the locomotion HandleUpdate is not being called.
        /// </summary>
        public void SetTarget(Vector3 position, Quaternion rotation, bool multiDirectional = false, float speed = 2f)
        {
            _alignRot = rotation;
            _alignMultiDirectional = multiDirectional;
            _motorSpeed = Mathf.Max(0f, speed);
            SavePrevPos();

            originalLocomotionType = Animator.GetFloat(AnimatorParameters.locomotionType);
            SetLocomotionType(multiDirectional ? 1f : 0f);

            prevAngle = transform.eulerAngles;
            motionDriver.SetTarget(position, rotation, _motorSpeed, driverHandlesRotation: !_alignMultiDirectional);
        }

        public bool TargetReached(float posThreshold = -1f, float rotThreshold = -1f)
        {
            if (posThreshold < 0) posThreshold = defaultPosThreshold;
            if (rotThreshold < 0) rotThreshold = defaultRotThreshold;

            bool posOK = motionDriver.IsPositionReached(posThreshold);
            bool rotOK = Quaternion.Angle(transform.rotation, _alignRot) <= rotThreshold;

            if (posOK)
            {
                StartCoroutine(ResetParamsAsync(.2f));
            }

            if (posOK && rotOK)
            {
                motionDriver.SnapToTarget();
                transform.rotation = _alignRot;
                return true;
            }
            return false;
        }

        IEnumerator ResetParamsAsync(float duration)
        {
            float timer = 0f;
            while (timer <= duration)
            {
                Animator.SetFloat(AnimatorParameters.moveAmount, 0f, 0.2f, Time.deltaTime);
                Animator.SetFloat(AnimatorParameters.strafeAmount, 0f, 0.2f, Time.deltaTime);

                Animator.SetFloat(AnimatorParameters.locomotionType, originalLocomotionType, 0.2f, Time.deltaTime);

                timer += Time.deltaTime;

                yield return null;
            }
            Animator.SetFloat(AnimatorParameters.moveAmount, 0f);
            Animator.SetFloat(AnimatorParameters.strafeAmount, 0f);
        }

        /// <summary>
        /// Advances one frame toward the target. If <paramref name="newPosition"/> or
        /// <paramref name="newRotation"/> are provided, the stored target is updated before
        /// moving — use this when the destination changes every frame (e.g. a moving vehicle).
        /// </summary>
        public void TickMoveToTarget(Vector3? newPosition = null, Quaternion? newRotation = null, bool ignoreCollision = false)
        {
            if (newRotation.HasValue) _alignRot = newRotation.Value;

            float dt = Time.deltaTime;
            motionDriver.TickTarget(dt, newPosition, newRotation, ignoreCollision);

            bool posReached = motionDriver.IsPositionReached(defaultPosThreshold);

            // Multi-directional: rotate every frame while moving (strafe style).
            // Single-directional: only rotate once the position is reached, preferring
            // a turn animation for large angles (mirrors TryIdleTurn behaviour).
            if (_alignMultiDirectional || posReached)
            {
                transform.rotation = Quaternion.RotateTowards(transform.rotation, _alignRot, rotationSpeed * 100f * dt);
            }

            UpdateAnimatorMotionParams(dt);
        }
        public void SetLocomotionType(float locomotionType)
        {
            Animator.SetFloat(AnimatorParameters.locomotionType, locomotionType);
        }

        Coroutine _locomotionTypeCoroutine;

        public void SetLocomotionTypeSmoothly(float locomotionType, float duration = 0.25f)
        {
            if (_locomotionTypeCoroutine != null)
                StopCoroutine(_locomotionTypeCoroutine);
            _locomotionTypeCoroutine = StartCoroutine(SmoothLocomotionType(locomotionType, duration));
        }

        IEnumerator SmoothLocomotionType(float target, float duration)
        {
            float start = Animator.GetFloat(AnimatorParameters.locomotionType);
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                Animator.SetFloat(AnimatorParameters.locomotionType, Mathf.Lerp(start, target, t));
                characterDirectionBlend = Mathf.Lerp(start, target, t);
                yield return null;
            }
            Animator.SetFloat(AnimatorParameters.locomotionType, target);
            characterDirectionBlend = target;
            _locomotionTypeCoroutine = null;
        }

        public void SavePrevPos()
        {
            prevPos = transform.position;
        }

        void UpdateAnimatorMotionParams(float dt)
        {
            var deltaPos = transform.position - prevPos;
            var velocity = deltaPos / Mathf.Max(dt, 1e-6f);
            velocity.y = 0f;

            // Normalise against runSpeed so blend values match the normal locomotion
            // blend tree (same denominator used at line ~1293 in ReachDestination).
            float denom = Mathf.Max(runSpeed, 1e-4f);

            Animator.SetFloat(AnimatorParameters.moveAmount,
                Vector3.Dot(velocity, transform.forward) / denom, 0.2f, dt);
            Animator.SetFloat(AnimatorParameters.strafeAmount,
                Vector3.Dot(velocity, transform.right) / denom, 0.2f, dt);

            prevPos = transform.position;
        }



        bool isOnLedge;
        public bool IsOnLedge => isOnLedge;

        public Vector3 AbsoluteVelocity => motionDriver.GetVelocity();

        /// <summary>
        /// True when the underlying motion driver moves the body from an explicit velocity
        /// (player / CharacterController). False for path-following drivers (AI / NavMeshAgent),
        /// whose <c>Move</c> is a no-op — those must be driven via SetTarget / TickMoveToTarget.
        /// </summary>
        public bool SupportsManualMovement => motionDriver.SupportsManualMovement;

        void MatchTarget(Vector3 matchPos, Quaternion rotation, AvatarTarget target,
            MatchTargetWeightMask weightMask, float startTime, float endTime)
        {
            if (Animator.isMatchingTarget || Animator.IsInTransition(0)) return;
            Animator.MatchTarget(matchPos, rotation, target, weightMask, startTime, endTime);
        }

        public IEnumerator DoAction(string anim, bool rotate = false,
            Quaternion targetRot = default, TargetMatchParams matchParams = null,
            float postDelay = 0f, bool mirror = false, Action onComplete = null,
            bool fixedTransition = false, bool resetMoveAmount = false,
            bool canLeaveNavMesh = false)
        {
            Animator.SetBool(AnimatorParameters.mirrorAction, mirror);

            var crossFadeTime = 0.2f;
            if (matchParams != null)
                crossFadeTime = Mathf.Min(crossFadeTime, matchParams.startTime + 0.02f);

            var action = new CharacterAction()
            {
                animName = anim,
                useFixedTransition = fixedTransition,
                usesRootMotion = true,
                ignoreCollision = true,
                requiresLedgeBlocking = false,
                resetMoveAmount = resetMoveAmount,
                canLeaveNavMesh = canLeaveNavMesh,
                animationParameters = new AnimParameters
                {
                    transitionIn = crossFadeTime,
                    onAnimationUpdate = (normalizedTime, timer) =>
                    {
                        if (matchParams != null)
                            MatchTarget(matchParams.pos, matchParams.rot, matchParams.target,
                                new MatchTargetWeightMask(matchParams.posWeight, 0),
                                matchParams.startTime, matchParams.endTime);
                        if (rotate)
                            transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRot,
                                rotationSpeed * 100 * Time.deltaTime);
                    }
                }
            };

            yield return ActionExecutor.ExecuteAnimatorAction(action);
            yield return new WaitForSeconds(postDelay);
            onComplete?.Invoke();
        }

        /// <summary>
        /// Prevents the character from going out of locomotion to perform other actions
        /// </summary>
        public void EnableLocomotionLock()
        {
            IsLocomotionLockActive = true;
            OnLocomotionLockEnabled?.Invoke();
        }

        public void DisableLocomotionLock()
        {
            IsLocomotionLockActive = false;
            OnLocomotionLockDisabled?.Invoke();
        }

        public IEnumerator HandleFalling(string animName, Vector3 landPos,
            float? gravityOverride = null, Action onComplete = null)
        {
            var matchParams = new TargetMatchParams()
            {
                pos = landPos,
                startTime = 0.4f,
                endTime = 0.7f,
                target = AvatarTarget.Root,
                posWeight = new Vector3(0, 0, 1)
            };

            var dir = landPos - transform.position;
            dir.y = 0;
            Quaternion targetRot = dir != Vector3.zero
                ? Quaternion.LookRotation(dir)
                : Quaternion.LookRotation(transform.forward);

            Animator.SetBool(AnimatorParameters.IsGrounded, false);
            // Falling always leaves the current navmesh island — the landing is below the edge.
            yield return DoAction(animName, rotate: true, targetRot: targetRot, matchParams: matchParams, 
                onComplete: onComplete, canLeaveNavMesh: true);

            float g = gravityOverride ?? gravity;
            float fallSpeed = g / 4;
            while (transform.position.y > landPos.y)
            {
                fallSpeed += g * Time.deltaTime;
                transform.position += Vector3.up * fallSpeed * Time.deltaTime;
                if ((transform.position.y - landPos.y) < 0.2f)
                {
                    if (fallSpeed < g / 2)
                        Animator.SetFloat(AnimatorParameters.fallAmount,
                            Mathf.Clamp(Mathf.Abs(fallSpeed) * 0.06f, 0.6f, 1f));
                    else
                        Animator.SetFloat(AnimatorParameters.fallAmount, 0);
                    Animator.SetBool(AnimatorParameters.IsGrounded, true);
                }
                yield return null;
            }

            if (fallSpeed < g / 2)
                Animator.SetFloat(AnimatorParameters.fallAmount,
                    Mathf.Clamp(Mathf.Abs(fallSpeed) * 0.06f, 0.6f, 1f));
            else
                Animator.SetFloat(AnimatorParameters.fallAmount, 0);
            Animator.SetBool(AnimatorParameters.IsGrounded, true);
            transform.position = new Vector3(transform.position.x, landPos.y, transform.position.z);
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0, 1, 0, 0.5f);
            Gizmos.DrawSphere(transform.TransformPoint(groundCheckOffset), groundCheckRadius);
        }
    }

}
