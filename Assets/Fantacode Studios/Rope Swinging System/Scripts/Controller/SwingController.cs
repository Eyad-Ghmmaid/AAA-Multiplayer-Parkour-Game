using FS_Core;
using FS_LocomotionSystem;
using FS_Util;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Events;
using Image = UnityEngine.UI.Image;


namespace FS_Core
{
    public static partial class AnimatorParameters
    {
        public static int SwingLeftHandIk = Animator.StringToHash("SwingLeftHandIk");
        public static int SwingRightHandIk = Animator.StringToHash("SwingRightHandIk");
        public static int SwingIkPriority = Animator.StringToHash("SwingIkPriority");
    }

    public static partial class AnimationNames
    {
        public static string SwingActions = "Swing Actions";
        public static string SwingLand = "Swing Land";
        public static string SwingClimbUp = "Swing Climb Up";
        public static string SwingClimbDown = "Swing Climb Down";
    }
}

namespace FS_SwingSystem
{
    [DefaultExecutionOrder(2)]
    [RequireComponent(typeof(SwingInputManager))]
    public class SwingController : EquippableSystemBase
    {
        float swingReadyTime;
        int swingRequestFrame;
        bool holdingSwingControls;
        bool previousRotationLock;
        bool previousItemSwitchLock;
        bool previousItemUnequipLock;
        bool changedLedgeLimit;
        const float GroundLaunchRise = 1.2f;
        const float GroundSwingClearance = 0.45f;
        const float GroundReelSpeed = 7f;
        const float GroundReelAcceleration = 30f;
        const float RopeCorrectionSpeed = 8f;
        const float ClimbBlendDuration = 0.055f;
        const float FootProbeHeight = 0.45f;
        static readonly int SwingX = Animator.StringToHash("x");
        static readonly int SwingY = Animator.StringToHash("y");
        Vector2 swingLean;
        float groundLaunchStartHeight;
        float groundMountRopeLength;
        float groundReelSpeed;
        bool mountingFromGround;
        bool waitingForRopeTension;
        bool supportedByFloor;
        bool simulatesSwing = true;
        bool receivesNetworkInput;
        bool showsLocalAim = true;
        bool networkInputLocked;
        int shotSequence;
        int presentedShot;
        Vector3 remoteAimOrigin;
        Vector3 remoteAimDirection;
        RopeSwingPresentation remoteSwing;
        int handledClimbSequence;
        int localClimbSequence;
        int anticipatedClimbSequence;
        int anticipatedClimbShot;
        float anticipatedClimbUntil;
        RopeClimbStep anticipatedClimbStep;
        int presentedPoseShot;
        RopeClimbStep presentedClimbStep;
        bool presentingRopePose;
        [SerializeField] SwingHookItem replicatedHook;

        [Tooltip("If true, the character will be able to swing event without equipping the rope")]
        public bool swingWithoutEquip = false;

        [Tooltip("If true, the rope will be unequipped after the swing action is completed.")]
        public bool unEquipAfterSwing = false;

        [Tooltip("The minimum distance required for the swing")]
        public float minDistance = 2f;
        [Tooltip("Angle from vertical used to choose the swing release animation.")]
        public float maxAngleForClimb = 15;

        [Tooltip("The animation clip used when holding the rope.")]
        public AnimationClip hookHoldingClip;



        // Rope settings
        [Tooltip("Offset from the right hand (in the hand's local space) used as the rope hold point.")]
        public Vector3 ropeHoldOffsetRight;

        [Tooltip("Offset from the left hand (in the hand's local space) used as the rope hold point.")]
        public Vector3 ropeHoldOffsetLeft;

        [Tooltip("Transform for attaching the rope to the body.")]
        public Transform ropeAttachTransformInBody;

        // Runtime anchors parented to the hand bones at the configured offsets.
        Transform ropeHoldTransformRight;
        Transform ropeHoldTransformLeft;
        Transform leftFoot, rightFoot, leftToes, rightToes;

        //[Tooltip("Hook Object")]
        //public HookItem hookObject;


        [Tooltip("The radius of the rope.")]
        public float ropeRadius = .025f;

        [Tooltip("Material used for the rope's appearance.")]
        public Material ropeMaterial;

        [Tooltip("Resolution of the rope's segments.")]
        public int ropeResolution = 50;

        [Tooltip("Maximum length of the rope.")]
        public float ropeLength = 10f;

        [Tooltip("Width of the rope.")]
        public float ropeWidth = 0.1f;

        [Tooltip("Amount of dampening applied to the rope's movement.")]
        public float dampening = 0.5f;

        [Tooltip("Height of the rope throw.")]
        public float throwHeight = 1f;

        [Tooltip("Duration of the rope throw.")]
        public float throwDuration = 0.5f;

        // Swing settings
        [Tooltip("The force applied to the player during swinging.")]
        public float swingForce = 4f;

        [Tooltip("The speed of rotation while swinging.")]
        public float swingRotationSpeed = 100f;

        [Tooltip("Travel speed along the rope in metres per second while climbing.")]
        public float climbSpeed = 3;

        [Tooltip("Maximum distance travelled along the rope for a short climb input.")]
        [SerializeField, Min(0.05f)] private float climbStepDistance = 0.75f;

        [Tooltip("Multiplier for the forward force applied upon landing from a swing.")]
        public float forwardLandForceMultiplier = 1f;

        [Tooltip("Multiplier for the upward force applied upon landing from a swing.")]
        public float upwardLandForceMultiplier = 2f;

        [Tooltip("Amount of damping applied while swinging.")]
        public float damping = 0.5f;

        [Tooltip("The gravity force applied during swinging.")]
        public float gravity = -20f;


        [Tooltip("Friction applied during collisions while swinging.")]
        public float collisionFriction = .2f;

        // Crosshair settings
        [Tooltip("Prefab used for the crosshair when aiming the swing hook.")]
        public GameObject crosshairPrefab;

        [Tooltip("Size of the crosshair.")]
        public float crosshairSize = .3f;

        // Camera shake settings
        [Tooltip("Amount of camera shake when the swing start")]
        public float cameraShakeAmount = .2f;

        [Tooltip("Duration of camera shake when the swing start")]
        public float cameraShakeDuration = .3f;

        float totalRopeLength = 10f;

        // This variable will store the reference of the swing hook item, even when it's not equipped
        EquippableItem availableSwingHook;

        // Events
        public UnityEvent RopeReleased;
        public UnityEvent RopeHooked;
        public UnityEvent SwingStarted;
        public UnityEvent ExitedFromSwing;

        public bool debug;

        Animator animator;
        ICharacter playerCharacter;
        LocomotionCore locomotion;
        EnvironmentScanner environmentScanner;
        PlayerController playerController;
        ItemEquipper itemEquipper;
        ItemAttacher itemAttacher;
        SwingInputManager swingInputManager;
        SwingRope rope;
        SwingData SwingData;
        AnimGraph animGraph;
        GameObject crosshairObj;
        Crosshair crosshair;
        Canvas aimCanvas;
        Image aimImage;
        Camera aimCamera;
        Transform aimView;
        SwingData aimTarget;

        Vector3 pivotPoint;
        float rotateSpeed = 15;


        Vector3 velocity;
        Vector3 prevVelocity;
        Vector3 maxHeightPoint;
        Vector3 swingDirection;


        float maxSwingAngle = 60f;

        private Vector3 ropeVector;
        private CharacterController characterController;


        public bool InSwing { get; private set; }
        internal bool ControlsHandIK => simulatesSwing
            ? InSwing && !mountingFromGround && !waitingForRopeTension
            : remoteSwing.HasCast && remoteSwing.Phase is RopeState.Swinging or RopeState.Climbing;
        public bool InAction { get; private set; }
        public bool IsFalling { get; private set; }

        public ActionExecutor ActionExecutor { get; private set; }

        //bool isHoldingClipPlaying;

        public override SystemState State { get; } = SystemState.Swing;
        public override List<Type> EquippableItems => new List<Type>() { typeof(SwingHookItem) };

        public override bool IsUnreachableByGround => true;

        //public override List<SystemState> ExecutionStates => new List<SystemState>() { SystemState.Locomotion, SystemState.Swing, SystemState.Parkour }; 
        RopeClimbStep activeClimbStep;
        int activeClimbAnimationHash;
        int climbStartFrame;
        float climbTargetLength;
        bool IsClimbing => activeClimbStep != RopeClimbStep.None;


        public SwingHookItem currentHookItem
        {
            get
            {
                return (itemEquipper?.EquippedItem is SwingHookItem shooterWeaponData)
                    ? shooterWeaponData
                    : null;
            }
        }
        public SwingHookObject CurrentHookRight => itemEquipper?.EquippedItemRight as SwingHookObject;
        public SwingHookObject CurrentHookLeft => itemEquipper?.EquippedItemLeft as SwingHookObject;
        public SwingHookObject CurrentHookItem => itemEquipper?.EquippedItemObject as SwingHookObject;



        internal void ConfigureNetwork(bool simulate, bool localInput, bool remoteInput)
        {
            simulatesSwing = simulate;
            receivesNetworkInput = remoteInput;
            showsLocalAim = localInput;
            GetComponent<SwingInputManager>().ConfigureNetwork(localInput, remoteInput);
            if (!localInput)
                HideCrosshair();
        }

        internal void WriteNetworkInput(ref FSPlayerNetworkInput input)
        {
            if (swingInputManager == null)
                return;
            swingInputManager.WriteNetworkInput(ref input);
            if (playerController.cameraGameObject != null)
                input.SwingAimOrigin = playerController.cameraGameObject.transform.position;
        }

        internal void ReadNetworkInput(FSPlayerNetworkInput input, bool hasInput, bool allowInput)
        {
            networkInputLocked = !allowInput;
            if (hasInput)
            {
                remoteAimOrigin = input.SwingAimOrigin;
                remoteAimDirection = input.CameraForward;
            }
            swingInputManager?.ReadNetworkInput(input, hasInput, allowInput);
        }

        internal bool ConsumeRemotePickup() => swingInputManager != null && swingInputManager.ConsumePickup();

        internal RopeSwingPresentation CapturePresentation() => new RopeSwingPresentation
        {
            Equipped = currentHookItem != null,
            Stowed = GetBodyHook()?.itemModel?.activeSelf == true,
            InAction = InAction,
            HasCast = rope != null && rope.HasCast,
            Phase = rope != null ? rope.ropeState : RopeState.Normal,
            ClimbDirection = (byte)activeClimbStep,
            ClimbInputSequence = handledClimbSequence,
            RopeLength = ropeLength,
            Shot = shotSequence,
            HookPoint = rope != null ? rope.HookPoint : Vector3.zero
        };

        internal void SetRemotePresentation(RopeSwingPresentation presentation) => remoteSwing = presentation;

        internal bool PresentsOwnerRopePose => !simulatesSwing && showsLocalAim && ControlsHandIK;

        private void UpdateOwnerRopePose()
        {
            if (!PresentsOwnerRopePose)
            {
                presentingRopePose = false;
                anticipatedClimbStep = RopeClimbStep.None;
                return;
            }

            if (anticipatedClimbStep != RopeClimbStep.None &&
                (anticipatedClimbShot != remoteSwing.Shot || Time.unscaledTime >= anticipatedClimbUntil ||
                    unchecked(remoteSwing.ClimbInputSequence - anticipatedClimbSequence) >= 0))
                anticipatedClimbStep = RopeClimbStep.None;

            RopeClimbStep step = anticipatedClimbStep != RopeClimbStep.None
                ? anticipatedClimbStep
                : (remoteSwing.Phase == RopeState.Climbing ? (RopeClimbStep)remoteSwing.ClimbDirection : RopeClimbStep.None);
            if (presentingRopePose && presentedPoseShot == remoteSwing.Shot && presentedClimbStep == step)
                return;

            presentingRopePose = true;
            presentedPoseShot = remoteSwing.Shot;
            presentedClimbStep = step;
            PlayRopePose(step == RopeClimbStep.None ? AnimationNames.SwingActions : GetClimbAnimation(step), ClimbBlendDuration);
        }

        private void AnticipateLocalClimb()
        {
            int sequence = swingInputManager.ClimbPressSequence;
            if (sequence == localClimbSequence)
                return;

            localClimbSequence = sequence;
            if (!ControlsHandIK)
                return;

            RopeClimbStep step = swingInputManager.LatestClimbPress;
            bool canClimb = step == RopeClimbStep.Up
                ? remoteSwing.RopeLength > minDistance + 0.01f
                : remoteSwing.RopeLength < totalRopeLength - 0.01f;
            if (!canClimb || (anticipatedClimbStep == RopeClimbStep.None && remoteSwing.ClimbDirection == (byte)step))
                return;

            anticipatedClimbStep = step;
            anticipatedClimbSequence = sequence;
            anticipatedClimbShot = remoteSwing.Shot;
            anticipatedClimbUntil = Time.unscaledTime + 0.35f;
        }

        private void Start()
        {
            totalRopeLength = Mathf.Max(0.1f, ropeLength);
            locomotion = GetComponent<LocomotionCore>();
            playerCharacter = GetComponent<ICharacter>();
            animator = playerCharacter?.Animator;
            environmentScanner = GetComponent<EnvironmentScanner>();
            swingInputManager = GetComponent<SwingInputManager>();
            if (swingInputManager == null)
                swingInputManager = gameObject.AddComponent<SwingInputManager>();
            characterController = GetComponent<CharacterController>();
            itemEquipper = GetComponent<ItemEquipper>();
            itemAttacher = GetComponent<ItemAttacher>();
            animGraph = GetComponent<AnimGraph>();
            ActionExecutor = GetComponent<ActionExecutor>();
            playerController = GetComponent<PlayerController>();
            if (animator == null || !animator.isHuman || locomotion == null || environmentScanner == null ||
                characterController == null || itemEquipper == null || animGraph == null ||
                ActionExecutor == null || playerController == null)
            {
                Debug.LogError("Rope Swinging requires a humanoid FS player with a CharacterController.", this);
                enabled = false;
                return;
            }

            var rightHand = animator.GetBoneTransform(HumanBodyBones.RightHand);
            var leftHand = animator.GetBoneTransform(HumanBodyBones.LeftHand);
            if (rightHand == null || leftHand == null)
            {
                Debug.LogError("Assign both hand bones on the Rope Swinging player's humanoid avatar.", this);
                enabled = false;
                return;
            }
            ropeHoldTransformRight = CreateHoldAnchor(rightHand, ropeHoldOffsetRight, "RopeHoldAnchor_R");
            ropeHoldTransformLeft = CreateHoldAnchor(leftHand, ropeHoldOffsetLeft, "RopeHoldAnchor_L");
            leftFoot = animator.GetBoneTransform(HumanBodyBones.LeftFoot);
            rightFoot = animator.GetBoneTransform(HumanBodyBones.RightFoot);
            leftToes = animator.GetBoneTransform(HumanBodyBones.LeftToes);
            rightToes = animator.GetBoneTransform(HumanBodyBones.RightToes);

            if (ropeAttachTransformInBody == null)
                ropeAttachTransformInBody = animator.GetBoneTransform(HumanBodyBones.Hips) ?? transform;

            itemEquipper.OnEquip += EquipItem;
            itemEquipper.OnUnEquip += UnEquipItem;

            if (showsLocalAim)
                SetCrosshair();

            if (currentHookItem != null)
                EquipItem(currentHookItem);
        }

        void RefreshAvailableSwingHook()
        {
            availableSwingHook = itemEquipper.equippableItems.FirstOrDefault(i => i is SwingHookItem);
        }


        private void LateUpdate()
        {
            if (!simulatesSwing)
                PresentRemoteSwing();
            rope?.RopeUpdate(simulatesSwing);
            UpdateWorldAimMarker();
        }

        private void PresentRemoteSwing()
        {
            if (itemEquipper == null || ropeHoldTransformRight == null)
                return;

            PresentRemoteHookEquipment();

            handIk = remoteSwing.HasCast && remoteSwing.Phase == RopeState.Swinging;
            if (rope == null)
                return;
            if (remoteSwing.HasCast)
            {
                SwingData.hookPosition = remoteSwing.HookPoint;
                rope.PresentHook(remoteSwing.HookPoint, remoteSwing.Phase, presentedShot != remoteSwing.Shot);
                presentedShot = remoteSwing.Shot;
            }
            else if (rope.HasCast)
                rope.ResetRope();
        }

        private AttachedItem GetBodyHook()
        {
            if (itemAttacher != null && replicatedHook != null &&
                itemAttacher.AttachedItems.TryGetValue(replicatedHook.EquipmentSlotIndex, out var attached) &&
                attached.item is SwingHookItem)
                return attached;
            return null;
        }

        private void PresentRemoteHookEquipment()
        {
            if (itemEquipper.IsChangingItem || replicatedHook == null || itemAttacher == null)
                return;

            if (remoteSwing.Equipped)
            {
                if (currentHookItem == null)
                    itemEquipper.EquipItem(replicatedHook, false);
                return;
            }

            if (currentHookItem != null)
            {
                itemEquipper.UnEquipItem(false);
                return;
            }

            var bodyHook = GetBodyHook();
            if (remoteSwing.Stowed)
            {
                if (bodyHook == null)
                    itemAttacher.AttachItem(replicatedHook);
                else if (bodyHook.isHidden)
                    itemAttacher.ShowBodyAttachement(bodyHook.item);
            }
            else if (bodyHook != null)
                itemAttacher.DetachItem(bodyHook.item, autoEquipDefault: false);
        }

        public override void HandleUpdate()
        {
            float dt = Time.deltaTime;
            if (!simulatesSwing || !InSwing || networkInputLocked || dt <= 0f)
                return;

            if (mountingFromGround)
            {
                UpdateGroundMount(dt);
                return;
            }

            if (waitingForRopeTension)
            {
                if (MoveAlongAirArc(dt, ropeLength))
                    TakeSwingGrip();
                return;
            }

            UpdateClimbInput();
            if (IsClimbing || RopeClimbModifierHolding)
                swingDirection = Vector3.zero;
            else
                ControlVelecity();

            UpdateSwingPhysics(dt, GetNextRopeLength(dt));
            HandleCollisions();
            UpdateSwingPose(dt);
        }

        private void Update()
        {
            if (!simulatesSwing)
            {
                if (showsLocalAim)
                {
                    AnticipateLocalClimb();
                    UpdateOwnerRopePose();
                }
                var localView = playerController.cameraGameObject;
                if (showsLocalAim && remoteSwing.Equipped && !remoteSwing.InAction && localView != null)
                    ShowAim(localView.transform, environmentScanner.GetSwingLedgeData(totalRopeLength, minDistance,
                        ropeHoldTransformRight, debug));
                else
                    HideCrosshair();
                return;
            }

            bool fire = swingInputManager.ConsumeFire();
            if (networkInputLocked)
            {
                if (InAction)
                    ExitFromSwing();
                HideCrosshair();
                return;
            }
            if (InAction)
            {
                if (!InSwing && SwingReleasePressed)
                    ExitFromSwing();
                return;
            }
            if (receivesNetworkInput)
            {
                swingInputManager.ConsumeRelease();
                swingInputManager.ConsumeJump();
                swingInputManager.ConsumeClimbStep();
                if (!fire)
                    return;
            }

            var focused = playerController.FocusedScript;
            var view = playerController.cameraGameObject;
            if (Time.timeScale == 0f || Time.time < swingReadyTime || playerCharacter.PreventAllSystems ||
                (!receivesNetworkInput && view == null) ||
                (focused != null && !focused.PermitsWhileFocused(State)) ||
                (playerController.CurrentSystemState != SystemState.Locomotion && !playerController.IsInAir))
            {
                HideCrosshair();
                return;
            }

            if (swingWithoutEquip && (availableSwingHook == null || !itemEquipper.equippableItems.Contains(availableSwingHook)))
                RefreshAvailableSwingHook();

            bool canUseHook = currentHookItem != null ? rope != null : swingWithoutEquip && availableSwingHook != null;
            if (canUseHook)
                SwingHandler(view != null ? view.transform : null, fire);
            else
                HideCrosshair();
        }

        void SwingHandler(Transform view, bool fire)
        {
            if (receivesNetworkInput)
            {
                float directionLength = remoteAimDirection.sqrMagnitude;
                if (!(directionLength > 0.01f && directionLength < 2f &&
                    (remoteAimOrigin - transform.position).sqrMagnitude < 144f))
                    return;
                SwingData = environmentScanner.GetSwingLedgeData(totalRopeLength, minDistance, ropeHoldTransformRight,
                    remoteAimOrigin, remoteAimDirection.normalized, false);
            }
            else
            {
                SwingData = environmentScanner.GetSwingLedgeData(totalRopeLength, minDistance, ropeHoldTransformRight, debug);
                ShowAim(view, SwingData);
            }
            if (fire && SwingData.hasLedge)
                StartCoroutine(ControlSwingAction());
        }

        private void ShowAim(Transform view, SwingData target)
        {
            if (!target.hasLedge || view == null)
            {
                HideCrosshair();
                return;
            }

            if (aimImage != null && (view != aimView || aimCamera == null))
                aimCamera = view.GetComponentInChildren<Camera>(true) ?? Camera.main;
            aimView = view;
            aimTarget = target;
        }

        private Vector3 AimMarkerPosition => aimTarget.ledge != null
            ? aimTarget.ledge.transform.TransformPoint(aimTarget.ledge.hookPoint)
            : aimTarget.hookPosition;

        private void RenderScreenAimMarker()
        {
            if (aimImage == null || aimView == null || aimCamera == null)
                return;

            Vector3 screen = aimCamera.WorldToScreenPoint(AimMarkerPosition);
            aimImage.enabled = screen.z > aimCamera.nearClipPlane && aimCamera.pixelRect.Contains(screen);
            if (!aimImage.enabled)
                return;

            aimCanvas.targetDisplay = aimCamera.targetDisplay;
            float size = aimCamera.pixelHeight * crosshairSize /
                (10f * Mathf.Tan(aimCamera.fieldOfView * Mathf.Deg2Rad * 0.5f));
            aimImage.rectTransform.sizeDelta = Vector2.one * Mathf.Max(1f, size);
            aimImage.rectTransform.position = new Vector3(screen.x, screen.y, 0f);
        }

        private void UpdateWorldAimMarker()
        {
            if (crosshair == null || aimView == null)
                return;

            crosshair.View = aimView;
            crosshair.size = crosshairSize;
            crosshairObj.transform.SetPositionAndRotation(AimMarkerPosition, aimView.rotation);
            if (!crosshairObj.activeSelf)
                crosshairObj.SetActive(true);
        }

        IEnumerator ControlSwingAction()
        {
            // Claim the action before the (optional) auto-equip await below. The equip
            // animation spans several frames, and FixedUpdate keeps re-triggering this
            // coroutine while the swing button is held. If InAction were only set after
            // the await, a re-entrant call would slip in, see the rope already equipped
            // (equippedForAction == false), and run the action to completion in place of
            // this one — so the unequip-after-swing step would never fire.
            if (InAction) yield break;
            InAction = true;
            swingRequestFrame = Time.frameCount;
            Vector3 castHandOffset = ropeHoldTransformRight.position - transform.position;

            bool equippedForAction = false;

            HideCrosshair();
            if (currentHookItem == null)
            {
                if (availableSwingHook == null)
                {
                    InAction = false;
                    yield break;
                }

                itemEquipper.EquipItem(availableSwingHook);
                yield return new WaitUntil(() => !itemEquipper.IsChangingItem);

                equippedForAction = true;
            }

            if (currentHookItem == null || rope == null)
            {
                InAction = false;
                yield break;
            }

            var currentFocusedScript = playerController.FocusedScript;
            LockSwingControls();
            yield return SetRotation(SwingData.forwardDirection, rotateSpeed);
            if (ShouldAbortThrow(currentFocusedScript))
            {
                FinishSwing();
                yield break;
            }
            RopeReleased?.Invoke();
            if (!InAction)
                yield break;

            // Play rope throwing animation
            animGraph.Crossfade(currentHookItem.ropeThrowingClip, currentHookItem.ropeThrowingClip, mask: Mask.Arm);
            yield return new WaitForSeconds(.3f);
            if (ShouldAbortThrow(currentFocusedScript))
            {
                FinishSwing();
                yield break;
            }

            pivotPoint = SwingData.hookPosition;

            // Start rope throwing
            shotSequence++;
            rope.StartThrow(ropeHoldTransformRight, pivotPoint);

            // Wait for the rope to reach the hook point.
            while (!rope.HasReachedTarget())
            {
                if (ShouldAbortThrow(currentFocusedScript))
                {
                    FinishSwing();
                    if (unEquipAfterSwing && equippedForAction)
                        itemEquipper.UnEquipItem();
                    yield break;
                }
                yield return null;
            }
            rope.SetRopeState(RopeState.Normal);

            // Rope hooked
            RopeHooked?.Invoke();
            if (!InAction)
                yield break;

            animGraph.StopLoopingAnimations(false);


            locomotion.PreventRotation = false;
            locomotion.SetPreventFallingFromLedge(false);
            changedLedgeLimit = true;

            bool mountFromGround;
            while (true)
            {
                // Once the hook has landed, measure actual player travel. Throw and
                // equip poses can move the wrist across either end of the aim range.
                float castDistance = Vector3.Distance(transform.position + castHandOffset, pivotPoint);
                if (ShouldAbortThrow(currentFocusedScript) || castDistance > totalRopeLength + characterController.radius)
                {
                    FinishSwing();
                    if (unEquipAfterSwing && equippedForAction)
                        itemEquipper.UnEquipItem();
                    yield break;
                }
                bool alreadyJumping = Mathf.Max(locomotion.CurrentVelocity.y, characterController.velocity.y) > 0.1f;
                bool grounded = !alreadyJumping && CheckGround();
                float length = Vector3.Distance(transform.position, pivotPoint);
                mountFromGround = grounded && pivotPoint.y > characterController.bounds.center.y + 0.5f &&
                    Mathf.Min(length, totalRopeLength) > minDistance + 0.2f;
                if (!grounded || mountFromGround)
                    break;

                yield return null;
            }

            var prevPos = transform.position;
            velocity = locomotion.CurrentVelocity;
            if (!mountFromGround && Mathf.Abs(velocity.y) <= 0.1f)
                velocity.y = characterController.velocity.y;
            locomotion.ResetPreventFallingFromLedge();
            changedLedgeLimit = false;
            ropeLength = Mathf.Clamp(Vector3.Distance(transform.position, pivotPoint),
                Mathf.Min(minDistance, totalRopeLength), totalRopeLength);
            supportedByFloor = mountFromGround;
            waitingForRopeTension = !mountFromGround;
            if (mountFromGround)
                BeginGroundMount();


            // Pause all systems and focus on the swing.
            playerCharacter.FocusSystem(this);
            if (!IsInFocus)
            {
                FinishSwing();
                if (unEquipAfterSwing && equippedForAction)
                    itemEquipper.UnEquipItem();
                yield break;
            }
            playerController.IsInAir = false;

            // Applying camera shake
            playerController.OnStartCameraShake?.Invoke(cameraShakeAmount, cameraShakeDuration);

            if (mountingFromGround)
            {
                animGraph.SetNetworkAnimatorBool(AnimatorParameters.IsGrounded, false);
                animGraph.SetNetworkAnimatorFloat(AnimatorParameters.fallAmount, 0f);
                PlayRopePose(AnimationNames.FallTree, 0.08f);
            }
            else if (!waitingForRopeTension)
                TakeSwingGrip();

            ropeVector = transform.position - pivotPoint;
            prevVelocity = velocity;
            maxHeightPoint = transform.position;
            InSwing = true;
            int mountedFrame = Time.frameCount;
            if (receivesNetworkInput)
                swingInputManager.ConsumeJump();

            SwingStarted?.Invoke();

            bool isSwingingAtLowVelocity = false;
            while (IsInFocus && !playerCharacter.PreventAllSystems &&
                (Time.frameCount == mountedFrame || !JumpPressed) && !SwingReleasePressed)
            {
                var curVelocityAngle = Vector3.Angle(Vector3.down, (maxHeightPoint - pivotPoint).normalized);
                var curAngle = Vector3.Angle(Vector3.down, ropeVector);
                IsFalling = prevPos.y > transform.position.y;
                isSwingingAtLowVelocity = curAngle < maxAngleForClimb && curVelocityAngle < maxAngleForClimb;
                prevPos = transform.position;
                yield return null;
            }
            bool canPlayExit = IsInFocus && !playerCharacter.PreventAllSystems;
            bool playExitAnimation = !isSwingingAtLowVelocity && !IsFalling;
            FinishSwing();
            if (!canPlayExit)
                yield break;

            // Applying camera shake
            playerController.OnStartCameraShake?.Invoke(cameraShakeAmount, cameraShakeDuration);

            // If the player is grounded, reuse LocomotionCore's soft landing.
            // Otherwise, play the airborne rope-release exit pose; LocomotionCore auto-lands once gravity catches up.
            if (CheckGround())
            {
                yield return locomotion.PerformLanding(false);
            }
            else
            {
                HandleLandingMomentum(playExitAnimation);
            }
            
            yield return new WaitForSeconds(.2f);
            if (unEquipAfterSwing && equippedForAction && currentHookItem != null)
                itemEquipper.UnEquipItem();
        }

        bool CheckGround()
        {
            return locomotion.CheckIsGrounded();
        }

        public void ExitFromSwing()
        {
            StopAllCoroutines();
            FinishSwing();
        }

        public override void ExitSystem()
        {
            if (!InAction)
                return;

            StopAllCoroutines();
            ClearSwing();
        }

        private void FinishSwing()
        {
            bool wasSwinging = InSwing;
            ClearSwing();
            if (IsInFocus)
            {
                if (gameObject.activeInHierarchy)
                    playerCharacter.UnFocusSystem(this);
                else
                {
                    UnFocusScript();
                    playerController.ResetState();
                }
            }
            if (wasSwinging)
                ExitedFromSwing?.Invoke();
        }

        private void ClearSwing()
        {
            activeClimbStep = RopeClimbStep.None;
            handIk = false;
            InSwing = false;
            IsFalling = false;
            InAction = false;
            mountingFromGround = false;
            waitingForRopeTension = false;
            groundMountRopeLength = 0f;
            groundReelSpeed = 0f;
            supportedByFloor = false;
            swingReadyTime = Time.time + 0.5f;

            rope?.ResetRope();
            HideCrosshair();
            if (changedLedgeLimit)
            {
                locomotion.ResetPreventFallingFromLedge();
                changedLedgeLimit = false;
            }
            if (holdingSwingControls)
            {
                locomotion.PreventRotation = previousRotationLock;
                itemEquipper.PreventItemSwitching = previousItemSwitchLock;
                itemEquipper.PreventItemUnEquip = previousItemUnequipLock;
                holdingSwingControls = false;
            }
            if (simulatesSwing && hookHoldingClip != null && currentHookItem != null && animGraph != null && isActiveAndEnabled)
                animGraph.PlayLoopingAnimation(hookHoldingClip, mask: Mask.RightHand);
        }

        private void LockSwingControls()
        {
            previousRotationLock = locomotion.PreventRotation;
            previousItemSwitchLock = itemEquipper.PreventItemSwitching;
            previousItemUnequipLock = itemEquipper.PreventItemUnEquip;
            locomotion.PreventRotation = true;
            itemEquipper.PreventItemSwitching = true;
            itemEquipper.PreventItemUnEquip = true;
            holdingSwingControls = true;
        }

        private bool ShouldAbortThrow(SystemBase previousFocus)
        {
            var currentFocus = playerController.FocusedScript;
            return SwingReleasePressed || playerCharacter.PreventAllSystems ||
                (currentFocus != null && currentFocus != previousFocus && currentFocus != this);
        }

        IEnumerator SetRotation(Vector3 lookDir, float rotateSpeed)
        {
            if (lookDir.sqrMagnitude <= 0.0001f)
                yield break;
            var dir = Quaternion.LookRotation(lookDir);
            while (Quaternion.Angle(transform.rotation, dir) > 0.1f)
            {
                transform.rotation = Quaternion.RotateTowards(transform.rotation, dir, rotateSpeed * Time.deltaTime * 50);
                yield return null;
            }
            transform.rotation = dir;
        }

        void SetCrosshair()
        {
            if (crosshairPrefab == null)
                return;
            var spriteRenderer = crosshairPrefab.GetComponentInChildren<SpriteRenderer>(true);
            if (spriteRenderer != null && spriteRenderer.sprite != null)
            {
                crosshairObj = new GameObject(crosshairPrefab.name, typeof(RectTransform), typeof(Canvas));
                aimCanvas = crosshairObj.GetComponent<Canvas>();
                aimCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
                var icon = new GameObject("Hook Aim Icon", typeof(RectTransform), typeof(Image));
                icon.transform.SetParent(crosshairObj.transform, false);
                aimImage = icon.GetComponent<Image>();
                aimImage.sprite = spriteRenderer.sprite;
                aimImage.color = spriteRenderer.color;
                aimImage.preserveAspect = true;
                aimImage.raycastTarget = false;
                aimImage.enabled = false;
                Canvas.willRenderCanvases += RenderScreenAimMarker;
                return;
            }

            crosshairObj = Instantiate(crosshairPrefab);
            crosshairObj.name = crosshairPrefab.name;
            crosshair = crosshairObj.GetComponent<Crosshair>() ?? crosshairObj.AddComponent<Crosshair>();
            crosshairObj.transform.localScale = Vector3.zero;
            crosshairObj.SetActive(false);
        }

        private void HideCrosshair()
        {
            aimView = null;
            aimTarget = default;
            if (aimImage != null)
                aimImage.enabled = false;
            else if (crosshairObj != null && crosshairObj.activeSelf)
                crosshairObj.SetActive(false);
        }

        Transform CreateHoldAnchor(Transform parent, Vector3 localOffset, string name)
        {
            var anchor = new GameObject(name).transform;
            anchor.SetParent(parent, false);
            anchor.localPosition = localOffset;
            anchor.localRotation = Quaternion.identity;
            return anchor;
        }

        #region Landing

        void HandleLandingMomentum(bool playLandAnimation = true)
        {
            float releaseScale = playLandAnimation ? 1f : 0.5f;
            Vector3 horizontal = Vector3.ProjectOnPlane(velocity, Vector3.up);
            StartCoroutine(SetRotation(horizontal, 5));
            StartCoroutine(DoExitAction(playLandAnimation ? AnimationNames.SwingLand : AnimationNames.FallTree));

            Vector3 exitVelocity = horizontal * forwardLandForceMultiplier;
            exitVelocity.y = velocity.y * upwardLandForceMultiplier;
            locomotion.ApplyExternalAirMomentum(exitVelocity * releaseScale);
        }


        IEnumerator DoExitAction(string animation)
        {
            var action = new CharacterAction
            {
                animName = animation,
                useFixedTransition = true,
                blocksLocomotion = false,
                usesRootMotion = false,
                resetMoveAmount = false,
                animationParameters = new AnimParameters { transitionIn = 0.2f }
            };
            yield return ActionExecutor.ExecuteAnimatorAction(action);
        }

        #endregion

        #region Swinging Controller

        void ControlVelecity()
        {
            float inputX = swingInputManager.DirectionInput.x;
            float inputY = swingInputManager.DirectionInput.y;


            var tarRot = playerController.cameraGameObject.transform.forward;
            tarRot.y = 0;
            if (!IsFalling && tarRot.sqrMagnitude > 0.0001f)
                transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(tarRot), Time.deltaTime * swingRotationSpeed);
            Vector3 cameraForward = Vector3.ProjectOnPlane(playerController.cameraGameObject.transform.forward, Vector3.up).normalized;
            Vector3 cameraRight = Vector3.ProjectOnPlane(playerController.cameraGameObject.transform.right, Vector3.up).normalized;
            swingDirection = Vector3.MoveTowards(swingDirection, (cameraRight * inputX + cameraForward * inputY).normalized, Time.deltaTime);

            Vector3 horizontalVelocity = Vector3.ProjectOnPlane(velocity, Vector3.up);
            float dotProduct = Vector3.Dot(swingDirection, horizontalVelocity.normalized);

            float currentAngle = Vector3.Angle(Vector3.down, ropeVector);
            if (currentAngle < maxSwingAngle && dotProduct >= 0 || horizontalVelocity.magnitude < 0.1f)
            {
                velocity += swingDirection * swingForce * Time.deltaTime;
            }

        }

        void UpdateSwingPhysics(float dt, float nextLength)
        {
            Vector3 oldPosition = transform.position;
            Vector3 radial = oldPosition - pivotPoint;
            bool lengthChanging = !Mathf.Approximately(nextLength, ropeLength);
            Vector3 swingVelocity = IsClimbing || lengthChanging ? Vector3.ProjectOnPlane(velocity, radial) : velocity;
            Vector3 proposed = radial + (swingVelocity + Vector3.up * (gravity * dt)) * dt;
            float radiusLimit = GetRopeRadiusLimit(radial, nextLength, dt);
            Vector3 projected = IsClimbing ? proposed.normalized * nextLength : Vector3.ClampMagnitude(proposed, radiusLimit);
            Vector3 movement = projected - radial;

            float currentAngle = Vector3.Angle(Vector3.down, projected);
            if ((movement.y < 0 && swingDirection == Vector3.zero) || currentAngle > maxSwingAngle * 0.7f)
                movement *= Mathf.Exp(-dt * Mathf.Max(0f, damping));

            Vector3 damped = radial + movement;
            movement = (IsClimbing ? damped.normalized * nextLength : Vector3.ClampMagnitude(damped, radiusLimit)) - radial;

            CollisionFlags contacts = characterController.Move(movement);
            supportedByFloor = (contacts & CollisionFlags.Below) != 0 ||
                (supportedByFloor && movement.y <= 0.001f && characterController.isGrounded);
            ropeVector = transform.position - pivotPoint;
            if (lengthChanging)
            {
                // A blocked capsule should not advance the rope beyond the distance it travelled.
                ropeLength = Mathf.Clamp(ropeVector.magnitude, Mathf.Min(ropeLength, nextLength),
                    Mathf.Max(ropeLength, nextLength));
            }

            velocity = (transform.position - oldPosition) / dt;
            if (prevVelocity.y > 0 && velocity.y < 0)
            {
                maxHeightPoint = transform.position;
            }


            prevVelocity = velocity;

        }

        private void BeginGroundMount()
        {
            groundLaunchStartHeight = transform.position.y;
            float minLength = Mathf.Min(minDistance, totalRopeLength);
            float horizontalDistanceSquared = Vector3.ProjectOnPlane(transform.position - pivotPoint, Vector3.up).sqrMagnitude;
            float anchorClearance = Mathf.Sqrt(Mathf.Max(0f, minLength * minLength - horizontalDistanceSquared));
            float rise = Mathf.Min(GroundLaunchRise, pivotPoint.y - anchorClearance - groundLaunchStartHeight);
            groundMountRopeLength = Mathf.Clamp(pivotPoint.y - groundLaunchStartHeight - GroundSwingClearance, minLength, ropeLength);
            mountingFromGround = rise > 0.01f && gravity < 0f;
            if (mountingFromGround)
                velocity.y = Mathf.Sqrt(-2f * gravity * rise);
            else
                groundMountRopeLength = 0f;
        }

        private void UpdateGroundMount(float dt)
        {
            float ascentTime = Mathf.Min(dt, Mathf.Max(0f, velocity.y / -gravity));
            if (ascentTime > 0.0001f)
                MoveAlongAirArc(ascentTime, totalRopeLength);
            if (velocity.y > 0.01f && ascentTime >= dt)
                return;

            ropeLength = Mathf.Clamp((transform.position - pivotPoint).magnitude, Mathf.Min(minDistance, totalRopeLength), totalRopeLength);
            if (transform.position.y < groundLaunchStartHeight + GroundSwingClearance)
                groundMountRopeLength = 0f;
            TakeSwingGrip();
        }

        private float GetRopeRadiusLimit(Vector3 radial, float length, float dt) =>
            Mathf.Max(length, radial.magnitude - RopeCorrectionSpeed * dt);

        private bool MoveAlongAirArc(float dt, float maxLength)
        {
            Vector3 oldPosition = transform.position;
            Vector3 radial = oldPosition - pivotPoint;
            Vector3 acceleration = Vector3.up * gravity;
            Vector3 proposed = radial + (velocity + acceleration * (0.5f * dt)) * dt;
            float radiusLimit = GetRopeRadiusLimit(radial, maxLength, dt);
            bool taut = proposed.sqrMagnitude > radiusLimit * radiusLimit;
            Vector3 movement = Vector3.ClampMagnitude(proposed, radiusLimit) - radial;
            CollisionFlags contacts = characterController.Move(movement);
            supportedByFloor = (contacts & CollisionFlags.Below) != 0;
            ropeVector = transform.position - pivotPoint;
            velocity = taut || contacts != CollisionFlags.None
                ? (transform.position - oldPosition) / dt + acceleration * (0.5f * dt)
                : velocity + acceleration * dt;
            if ((contacts & CollisionFlags.Above) != 0 && velocity.y > 0f)
                velocity.y = 0f;
            if (supportedByFloor && velocity.y < 0f)
                velocity.y = 0f;
            return taut || supportedByFloor;
        }

        private void TakeSwingGrip()
        {
            mountingFromGround = false;
            waitingForRopeTension = false;
            rope.SetRopeState(RopeState.Swinging);
            handIk = true;
            swingLean = Vector2.zero;
            animGraph.SetNetworkAnimatorFloat(SwingX, 0f);
            animGraph.SetNetworkAnimatorFloat(SwingY, 0f);
            PlayRopePose(AnimationNames.SwingActions, 0.10f);
            prevVelocity = velocity;
        }

        private void UpdateSwingPose(float dt)
        {
            Vector3 horizontalVelocity = Vector3.ProjectOnPlane(velocity, Vector3.up);
            float motionWeight = mountingFromGround || supportedByFloor ? 0f :
                Mathf.InverseLerp(0.12f, 1.6f, horizontalVelocity.magnitude);
            Vector3 radialDirection = Vector3.ProjectOnPlane(ropeVector.normalized, Vector3.up);
            Vector3 lean = transform.InverseTransformDirection(radialDirection) * (0.5f * motionWeight);
            swingLean = Vector2.Lerp(swingLean, new Vector2(lean.x, lean.z), 1f - Mathf.Exp(-dt / 0.075f));
            animGraph.SetNetworkAnimatorFloat(SwingX, swingLean.x);
            animGraph.SetNetworkAnimatorFloat(SwingY, swingLean.y);
        }

        void HandleCollisions()
        {
            if (velocity.sqrMagnitude <= 0.0001f)
                return;

            if (Physics.SphereCast(characterController.bounds.center, characterController.radius, velocity.normalized,
                out RaycastHit hit, characterController.radius, environmentScanner.ObstacleLayer, QueryTriggerInteraction.Ignore))
            {
                Vector3 reflection = Vector3.Reflect(velocity.normalized, hit.normal);
                reflection += Vector3.up * 0.5f;
                velocity = reflection.normalized * velocity.magnitude * collisionFriction;
            }

            //GizmosExtend.drawSphereCast(animator.GetBoneTransform(HumanBodyBones.Hips).position, characterController.radius, velocity.normalized, characterController.radius, Color.red);
        }

        private void UpdateClimbInput()
        {
            RopeClimbStep pressed = swingInputManager.ConsumeClimbStep();
            RopeClimbStep held = swingInputManager.HeldClimbStep;
            RopeClimbStep requested = pressed != RopeClimbStep.None ? pressed : held;
            if (requested != RopeClimbStep.None && (activeClimbStep != requested || pressed == requested))
            {
                float target = GetClimbTarget(requested, false);
                if (CanClimb(requested, target))
                {
                    climbTargetLength = target;
                    if (requested != activeClimbStep)
                        StartClimb(requested);
                }
                else if (requested != activeClimbStep && IsClimbing)
                    StopClimb();
            }

            if (IsClimbing && !CanClimb(activeClimbStep, GetActiveClimbTarget()))
                StopClimb();
            handledClimbSequence = swingInputManager.ReceivedClimbSequence;
        }

        private float GetClimbTarget(RopeClimbStep step, bool held)
        {
            float minLength = Mathf.Min(minDistance, totalRopeLength);
            if (held)
                return step == RopeClimbStep.Down ? totalRopeLength : minLength;
            float stepDistance = Mathf.Max(0f, climbStepDistance);
            return Mathf.Clamp(ropeLength + (step == RopeClimbStep.Down ? stepDistance : -stepDistance),
                minLength, totalRopeLength);
        }

        private bool CanClimb(RopeClimbStep step, float target) => climbSpeed > 0f &&
            (step == RopeClimbStep.Down
                ? !supportedByFloor && !mountingFromGround && target - ropeLength > 0.001f
                : ropeLength - target > 0.001f);

        private float GetActiveClimbTarget() => swingInputManager.HeldClimbStep == activeClimbStep
            ? GetClimbTarget(activeClimbStep, true) : climbTargetLength;

        private float GetNextRopeLength(float dt)
        {
            if (!IsClimbing)
            {
                if (groundMountRopeLength <= 0f || ropeLength <= groundMountRopeLength + 0.001f)
                    return ropeLength;

                // Take up the excess over the first swing while gravity and
                // angular velocity still determine the player's path.
                groundReelSpeed = Mathf.MoveTowards(groundReelSpeed, GroundReelSpeed, GroundReelAcceleration * dt);
                return Mathf.MoveTowards(ropeLength, groundMountRopeLength, groundReelSpeed * dt);
            }

            return Mathf.MoveTowards(ropeLength, GetActiveClimbTarget(), climbSpeed * GetClimbBlend() * dt);
        }

        private float GetClimbBlend()
        {
            // The Animator evaluates after gameplay Update. Keep the existing radius
            // until it has sampled the new state, then ramp with its transition.
            if (Time.frameCount == climbStartFrame)
                return 0f;

            if (animator.IsInTransition(0))
            {
                if (animator.GetNextAnimatorStateInfo(0).shortNameHash != activeClimbAnimationHash)
                    return 0f;
                return Mathf.Clamp01(animator.GetAnimatorTransitionInfo(0).normalizedTime);
            }

            return animator.GetCurrentAnimatorStateInfo(0).shortNameHash == activeClimbAnimationHash ? 1f : 0f;
        }

        private void StartClimb(RopeClimbStep step)
        {
            groundMountRopeLength = 0f;
            groundReelSpeed = 0f;
            activeClimbStep = step;
            rope.SetRopeState(RopeState.Climbing);
            handIk = false;
            string animation = GetClimbAnimation(step);
            activeClimbAnimationHash = Animator.StringToHash(animation);
            climbStartFrame = Time.frameCount;
            PlayRopePose(animation, ClimbBlendDuration);
        }

        private void StopClimb()
        {
            activeClimbStep = RopeClimbStep.None;
            handIk = true;
            rope.SetRopeState(RopeState.Swinging);
            PlayRopePose(AnimationNames.SwingActions, ClimbBlendDuration);
        }

        private void PlayRopePose(string state, float transition) =>
            animGraph.PlayNetworkAnimatorState(state, 0f, 0f, transition, true);

        private static string GetClimbAnimation(RopeClimbStep step) =>
            step == RopeClimbStep.Down ? AnimationNames.SwingClimbDown : AnimationNames.SwingClimbUp;

        #endregion

        #region IK

        bool handIk;

        private void OnAnimatorIK(int layerIndex)
        {
            if (layerIndex != 0)
                return;
            if (ControlsHandIK && FSSettings.i != null)
            {
                KeepFootAboveFloor(AvatarIKGoal.LeftFoot, leftFoot, leftToes);
                KeepFootAboveFloor(AvatarIKGoal.RightFoot, rightFoot, rightToes);
            }
            bool climbPose = IsClimbing || (!simulatesSwing && remoteSwing.HasCast &&
                (remoteSwing.Phase == RopeState.Climbing || anticipatedClimbStep != RopeClimbStep.None));
            if (handIk && !climbPose)
            {
                var dirToHookFromLefthand = (SwingData.hookPosition - ropeHoldTransformLeft.position).normalized;
                var offset = (ropeHoldTransformRight.position - animator.GetBoneTransform(HumanBodyBones.RightHand).position);
                animator.SetIKPosition(AvatarIKGoal.RightHand, ropeHoldTransformLeft.transform.position + dirToHookFromLefthand * .25f - offset);
                animator.SetIKPositionWeight(AvatarIKGoal.RightHand, 1);
            }

            if (rope != null && climbPose)
            {
                var rightHandIkWeight = animator.GetFloat(AnimatorParameters.SwingRightHandIk);
                var leftHandIkWeight = animator.GetFloat(AnimatorParameters.SwingLeftHandIk);
                var swingIkPriority = animator.GetFloat(AnimatorParameters.SwingIkPriority);


                // If the priority is zero, the left hand is currently holding the rope, meaning the other hand is attempting to reach for a higher point on the rope.
                rope.currentRopeHoldPointTransform = swingIkPriority < 0.5f ? ropeHoldTransformLeft : ropeHoldTransformRight;


                var dirToHookFromLefthand = (SwingData.hookPosition - ropeHoldTransformLeft.position).normalized;
                var offset = (ropeHoldTransformRight.position - animator.GetBoneTransform(HumanBodyBones.RightHand).position);
                animator.SetIKPosition(AvatarIKGoal.RightHand, ropeHoldTransformLeft.transform.position + dirToHookFromLefthand * .25f - offset);
                animator.SetIKPositionWeight(AvatarIKGoal.RightHand, rightHandIkWeight);

                var dirToHookFromRighthand = (SwingData.hookPosition - ropeHoldTransformRight.position).normalized;
                offset = (ropeHoldTransformLeft.position - animator.GetBoneTransform(HumanBodyBones.LeftHand).position);
                animator.SetIKPosition(AvatarIKGoal.LeftHand, ropeHoldTransformRight.transform.position + dirToHookFromRighthand * .25f - offset);
                animator.SetIKPositionWeight(AvatarIKGoal.LeftHand, leftHandIkWeight);
            }
        }

        private void KeepFootAboveFloor(AvatarIKGoal goal, Transform foot, Transform toes)
        {
            if (foot == null)
                return;

            float lift = GetFootLift(foot.position, 0.09f);
            if (toes != null)
                lift = Mathf.Max(lift, GetFootLift(toes.position, 0.035f));
            if (lift <= 0f)
                return;

            animator.SetIKPosition(goal, animator.GetIKPosition(goal) + Vector3.up * lift);
            animator.SetIKPositionWeight(goal, 1f);
        }

        private float GetFootLift(Vector3 position, float soleOffset)
        {
            float distance = FootProbeHeight + soleOffset;
            if (Physics.Raycast(position + Vector3.up * FootProbeHeight, Vector3.down, out var floor,
                    distance, FSSettings.i.GroundLayer, QueryTriggerInteraction.Ignore) &&
                floor.normal.y >= 0.5f && !floor.transform.IsChildOf(transform))
                return distance - floor.distance;
            return 0f;
        }

        #endregion

        bool JumpPressed => swingInputManager.ConsumeJump();
        bool SwingReleasePressed => Time.frameCount > swingRequestFrame && swingInputManager.ConsumeRelease();
        bool RopeClimbModifierHolding => swingInputManager != null && swingInputManager.RopeClimbModifierHolding;

        #region Equip And UnEquip

        public void EquipItem(EquippableItem equippableItem)
        {
            if (!isActiveAndEnabled || equippableItem is not SwingHookItem || ropeHoldTransformRight == null)
                return;

            var hook = CurrentHookItem;
            if (hook == null || hook.ropeHookPoint == null)
            {
                Debug.LogWarning("The equipped Swing Hook needs a SwingHookObject with its Rope Hook Point assigned.", this);
                return;
            }

            rope?.DeleteRope();
            rope = new SwingRope(ropeRadius, ropeWidth, ropeMaterial, throwDuration, totalRopeLength, dampening,
                ropeResolution, throwHeight, transform, ropeAttachTransformInBody, ropeHoldTransformLeft,
                ropeHoldTransformRight, hook.ropeHookPoint, hook.transform);
            availableSwingHook = equippableItem;
        }

        public void UnEquipItem()
        {
            if (rope != null)
            {
                if (InAction)
                    ExitFromSwing();
                rope?.DeleteRope();
                rope = null;
                RefreshAvailableSwingHook();
                HideCrosshair();
            }
        }

        private void OnEnable()
        {
            if (ropeHoldTransformRight != null && currentHookItem != null)
                EquipItem(currentHookItem);
        }

        private void OnDisable()
        {
            if (InAction)
                ExitFromSwing();
            else
                StopAllCoroutines();
            rope?.DeleteRope();
            rope = null;
            HideCrosshair();
        }

        private void OnDestroy()
        {
            Canvas.willRenderCanvases -= RenderScreenAimMarker;
            if (itemEquipper != null)
            {
                itemEquipper.OnEquip -= EquipItem;
                itemEquipper.OnUnEquip -= UnEquipItem;
            }
            if (crosshairObj != null)
                Destroy(crosshairObj);
            if (ropeHoldTransformRight != null)
                Destroy(ropeHoldTransformRight.gameObject);
            if (ropeHoldTransformLeft != null)
                Destroy(ropeHoldTransformLeft.gameObject);
        }

        #endregion
    }
}
