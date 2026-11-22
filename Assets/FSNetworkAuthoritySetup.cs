using Fusion;
using UnityEngine;
using FS_Core;
using FS_LocomotionSystem;
using FS_ParkourSystem;
using FC_ParkourSystem;
using AvocadoShark;

public class FSNetworkAuthoritySetup : NetworkBehaviour
{
    [SerializeField] private bool logDiagnostics;
    [SerializeField] private PlayerController playerController;
    [SerializeField] private LocomotionInputManager inputManager;
    [SerializeField] private ParkourInputManager parkourInputManager;
    [SerializeField] private GameObject localCameraRig;
    [SerializeField] private Vector3 playerLocalPosition = new Vector3(-0.07309055f, -0.39661f, 0.062330246f);
    [SerializeField] private Quaternion playerLocalRotation = Quaternion.identity;
    private InGame_Manager raceManager;
    private float nextAuthorityDiagnosticTime;
    private float nextLocalDiagnosticTime;
    private Vector2 lastAuthorityMove;
    private Vector2 lastLocalMove;
    private bool hasAuthorityMoveSample;
    private bool hasLocalMoveSample;
    private LocomotionCore locomotionCore;
    private Animator playerAnimator;
    private Transform networkedPlayerTransform;
    private Vector3 networkedPlayerLocalPosition;
    private Quaternion networkedPlayerLocalRotation;
    private int authoritativeTeleportSettleFrames;
    private NetworkMecanimAnimator networkAnimator;
    private bool isLocalInputProxy;
    private GrapplingHookController grapplingHook;

    [Networked] private GrapplePresentation NetworkGrapple { get; set; }

    public override void Spawned()
    {
        raceManager = FindAnyObjectByType<InGame_Manager>();
        grapplingHook = GetComponentInChildren<GrapplingHookController>(true);
        bool hasStateAuthority = Object.HasStateAuthority;
        bool hasInputAuthority = Object.HasInputAuthority;

        if (playerController != null)
        {
            // The NetworkObject root is the one authoritative transform for this
            // avatar. The FS controller stays on its child, which must keep its
            // authored local offset beneath the networked root.
            networkedPlayerTransform = playerController.transform;
            var nestedCharacterController = networkedPlayerTransform.GetComponent<CharacterController>();
            bool wasCharacterControllerEnabled = nestedCharacterController != null && nestedCharacterController.enabled;
            if (nestedCharacterController != null)
                nestedCharacterController.enabled = false;

            if (networkedPlayerTransform.parent != transform)
                networkedPlayerTransform.SetParent(transform, false);

            networkedPlayerTransform.localPosition = playerLocalPosition;
            networkedPlayerTransform.localRotation = playerLocalRotation;
            networkedPlayerLocalPosition = playerLocalPosition;
            networkedPlayerLocalRotation = playerLocalRotation;

            if (nestedCharacterController != null)
                nestedCharacterController.enabled = wasCharacterControllerEnabled;
            Physics.SyncTransforms();
        }

        // Keep the tested Shared Mode setup identical: the local State Authority
        // owns the controller, input manager, and camera. No external input source
        // is used in this mode.
        if (Runner.GameMode == GameMode.Shared || Runner.GameMode == GameMode.Single)
        {
            if (playerController != null)
                playerController.enabled = hasStateAuthority;

            if (inputManager != null)
            {
                inputManager.enabled = hasStateAuthority;
                inputManager.useExternalInput = false;
            }

            if (localCameraRig != null)
                localCameraRig.SetActive(hasStateAuthority);

            return;
        }

        bool isHostOrServer = Runner.GameMode == GameMode.Host || Runner.GameMode == GameMode.Server;
        // The Host's own player reads its camera and controls locally. Only remote
        // player objects receive values through Fusion's input stream.
        bool receivesAuthorityInput = isHostOrServer && hasStateAuthority && !hasInputAuthority;

        // This controller is simulated only by State Authority. Enable Fusion's
        // transform forecast on remote presentations so render frames between Host
        // snapshots extrapolate velocity instead of visibly stepping at tick rate.
        var networkTransform = GetComponent<NetworkTransform>();
        if (networkTransform != null && !hasStateAuthority)
            networkTransform.PhysicsSettings.ForecastEnabled = true;

        // In Host/Server mode only State Authority simulates FS locomotion. The
        // owning client receives its presentation parameters from
        // FSNetworkParkourAnimationSync instead of running a second controller.
        isLocalInputProxy = hasInputAuthority && !hasStateAuthority;

        networkAnimator = GetComponentInChildren<NetworkMecanimAnimator>(true);
        if (networkAnimator != null)
        {
            // Keep Fusion's Mecanim synchronization active for genuine proxies.
            // NetworkMecanimAnimator.Render() deliberately skips Input Authority,
            // so FSNetworkParkourAnimationSync supplies that owning client with the
            // authoritative Base Layer state. It also handles PlayableGraph actions.
            networkAnimator.enabled = true;
        }

        if (playerController != null)
            playerController.enabled = hasStateAuthority;

        if (inputManager != null)
        {
            // State Authority consumes input delivered by Fusion, including for the
            // Host's own player. Input Authority retains local device polling.
            inputManager.enabled = hasStateAuthority || hasInputAuthority;
            inputManager.useExternalInput = receivesAuthorityInput;
        }

        if (parkourInputManager != null)
        {
            parkourInputManager.enabled = hasStateAuthority || hasInputAuthority;
            parkourInputManager.useExternalInput = receivesAuthorityInput;
        }

        // In Host/Server mode the same obstacle scan and random action selection
        // must not run independently on the owning client and the Host. The Host
        // selects and moves the parkour action; the client receives its animation
        // cue and authoritative root transform. This prevents divergent vault,
        // climb, and step choices from competing with the replicated result.
        if (isLocalInputProxy)
        {
            // Presentation-only owner: these components have their own Update()
            // loops and otherwise keep writing local locomotion/AnimGraph state
            // while the Host is already supplying the authoritative result.
            var locomotionController = GetComponentInChildren<LocomotionController>(true);
            if (locomotionController != null)
                locomotionController.enabled = false;

            var parkourController = GetComponentInChildren<ParkourController>(true);
            if (parkourController != null)
                parkourController.enabled = false;

            var climbController = GetComponentInChildren<ClimbController>(true);
            if (climbController != null)
                climbController.enabled = false;

            var slidingController = GetComponentInChildren<SlidingController>(true);
            if (slidingController != null)
                slidingController.enabled = false;
        }

        // Pose modifiers must follow the same authority boundary as gameplay.
        // FootIK reads PlayerController.FocusedSystemState, but that controller is
        // intentionally disabled on a presentation-only client. It would therefore
        // remain in Locomotion and bend hips/feet during Vault, Hang and Climb.
        // ClimbCore also owns an OnAnimatorIK callback which competes with the
        // replicated hand targets. Only State Authority may run either native IK
        // writer; proxies receive their required hang IK from the network sync.
        var footIK = GetComponentInChildren<FootIK>(true);
        if (footIK != null)
            footIK.enabled = hasStateAuthority;

        var nativeClimbCore = GetComponentInChildren<ClimbCore>(true);
        if (nativeClimbCore != null)
            nativeClimbCore.enabled = hasStateAuthority;

        if (localCameraRig != null)
            localCameraRig.SetActive(hasInputAuthority);

        var characterController = GetComponentInChildren<CharacterController>(true);
        var characterPosition = characterController != null ? characterController.bounds.center : transform.position;
        locomotionCore = GetComponentInChildren<LocomotionCore>(true);
        playerAnimator = GetComponentInChildren<Animator>(true);
        if (hasStateAuthority && locomotionCore != null)
        {
            // Initialize the Animator's grounded parameter before the first network
            // snapshot. A newly joined input-authority proxy has no local controller
            // to calculate this until the State Authority's Animator value arrives.
            Physics.SyncTransforms();
            locomotionCore.GroundCheck();
        }
        if (logDiagnostics)
            Debug.Log($"[FS Authority Setup] mode={Runner.GameMode} object={Object.Id} stateAuthority={hasStateAuthority} inputAuthority={hasInputAuthority} localInputProxy={isLocalInputProxy} root={transform.position} playerParent={(networkedPlayerTransform != null ? networkedPlayerTransform.parent?.name : "null")} playerLocal={(networkedPlayerTransform != null ? networkedPlayerTransform.localPosition : Vector3.zero)} playerWorld={(networkedPlayerTransform != null ? networkedPlayerTransform.position : Vector3.zero)} characterCenter={characterPosition} animatorGrounded={(playerAnimator != null && playerAnimator.GetBool(Animator.StringToHash("IsGrounded")))} playerController={(playerController != null && playerController.enabled)} networkAnimator={(networkAnimator != null && networkAnimator.enabled)} inputEnabled={(inputManager != null && inputManager.enabled)} externalInput={(inputManager != null && inputManager.useExternalInput)} raceManager={(raceManager != null)}");
    }

    public override void FixedUpdateNetwork()
    {
        if (!Object.HasStateAuthority)
            return;

        if (grapplingHook != null && grapplingHook.isActiveAndEnabled)
            NetworkGrapple = grapplingHook.CapturePresentation();
        else if (NetworkGrapple.Phase != GrapplePhase.Idle)
            NetworkGrapple = default;

        // Shared Mode remains on the existing local-authority path. In Host/Server
        // mode, the Host feeds remote players from Fusion input while retaining the
        // FS controller as the only writer of their movement.
        if (Runner.GameMode != GameMode.Host && Runner.GameMode != GameMode.Server)
            return;

        // Clear the previous tick's values if this tick has no input. Keeping stale
        // values here can leave movement or one-shot parkour actions stuck active.
        bool hasInput = GetInput<FSPlayerNetworkInput>(out var data);
        if (!hasInput)
            data = default;

        if (raceManager == null)
            raceManager = FindAnyObjectByType<InGame_Manager>();
        // Fail closed in Host/Server mode if the race authority is missing; this
        // component must never silently accept remote movement without validation.
        bool raceAllowsMovement = raceManager != null && raceManager.CanPlayerMoveOnAuthority(Object.InputAuthority);
        if (grapplingHook != null && grapplingHook.isActiveAndEnabled)
            grapplingHook.ReadAuthorityInput(data, hasInput,
                raceAllowsMovement && (inputManager == null || !inputManager.RaceInputLocked));

        if (logDiagnostics && Time.unscaledTime >= nextAuthorityDiagnosticTime)
        {
            nextAuthorityDiagnosticTime = Time.unscaledTime + 2f;
            var characterController = GetComponentInChildren<CharacterController>(true);
            var capsuleBottom = characterController != null ? characterController.bounds.min.y : float.NaN;
            var locomotionCore = this.locomotionCore != null ? this.locomotionCore : GetComponentInChildren<LocomotionCore>(true);
            var grounded = locomotionCore != null && locomotionCore.CheckIsGrounded();
            var velocity = locomotionCore != null ? locomotionCore.CurrentVelocity : Vector3.zero;
            var locomotionController = GetComponentInChildren<LocomotionController>(true);
            Debug.Log($"[FS Host Authority] object={Object.Id} player={Object.InputAuthority} hasInput={hasInput} move={data.Move} raceAllowsMovement={raceAllowsMovement} grounded={grounded} velocity={velocity} locomotionBlocked={(locomotionCore != null && locomotionCore.IsLocomotionBlocked)} preventAllSystems={(locomotionController != null && locomotionController.PreventAllSystems)} inputLocked={(inputManager != null && inputManager.RaceInputLocked)} inputManagerExternal={(inputManager != null && inputManager.useExternalInput)} controllerEnabled={(playerController != null && playerController.enabled)} root={transform.position} player={playerController?.transform.position} capsuleBottomY={capsuleBottom}");
        }

        if (logDiagnostics && (!hasAuthorityMoveSample || data.Move != lastAuthorityMove))
        {
            hasAuthorityMoveSample = true;
            lastAuthorityMove = data.Move;
            if (data.Move.sqrMagnitude > 0.001f)
            {
                var velocity = locomotionCore != null ? locomotionCore.CurrentVelocity : Vector3.zero;
                Debug.Log($"[FS Host Move Input] player={Object.InputAuthority} move={data.Move} raceAllowsMovement={raceAllowsMovement} locked={(inputManager != null && inputManager.RaceInputLocked)} velocity={velocity} playerPosition={playerController?.transform.position}");
            }
        }

        if (inputManager != null && inputManager.useExternalInput)
        {
            bool inputLocked = inputManager.RaceInputLocked || !raceAllowsMovement;
            inputManager.DirectionInput = inputLocked ? Vector2.zero : Vector2.ClampMagnitude(data.Move, 1f);
            inputManager.JumpKeyDown = !inputLocked && data.JumpPressed;
            inputManager.JumpHold = !inputLocked && data.JumpHeld;
            inputManager.Drop = !inputLocked && data.Drop;
            inputManager.ToggleRun = !inputLocked && data.ToggleRun;
            inputManager.SprintKey = !inputLocked && data.Sprint;
            inputManager.Crouch = !inputLocked && data.Crouch;
            inputManager.QuickSwitchItemDown = !inputLocked && data.QuickSwitchDown;
            inputManager.QuickSwitchItemUp = !inputLocked && data.QuickSwitchUp;
            inputManager.CycleToPreviousItem = !inputLocked && data.PreviousItem;
            inputManager.CycleToNextItem = !inputLocked && data.NextItem;
            inputManager.ItemThrowDown = !inputLocked && data.ThrowDown;
            inputManager.ItemThrowHold = !inputLocked && data.ThrowHeld;
            inputManager.ItemThrowUp = !inputLocked && data.ThrowUp;
            inputManager.ItemThrowCancelDown = !inputLocked && data.ThrowCancel;
        }

        if (parkourInputManager != null && parkourInputManager.useExternalInput)
            parkourInputManager.JumpFromHang = !parkourInputManager.RaceInputLocked && raceAllowsMovement && data.JumpFromHang;

        if (!Object.HasInputAuthority && playerController != null && playerController.cameraGameObject != null && data.CameraForward.sqrMagnitude > 0.0001f)
            playerController.cameraGameObject.transform.rotation = Quaternion.LookRotation(data.CameraForward.normalized, Vector3.up);
    }

    public override void Render()
    {
        if (HasStateAuthority || grapplingHook == null)
            return;

        var visible = NetworkGrapple;
        if (TryGetSnapshotsBuffers(out var from, out var to, out float alpha))
        {
            var (earlier, later) = GetPropertyReader<GrapplePresentation>(nameof(NetworkGrapple)).Read(from, to);
            visible = alpha >= 1f ? later : earlier;
            visible.Stamina = Mathf.Lerp(earlier.Stamina, later.Stamina, alpha);
            if (earlier.Shot == later.Shot && earlier.Phase != GrapplePhase.Idle && later.Phase != GrapplePhase.Idle)
            {
                visible.Extension = Mathf.Lerp(earlier.Extension, later.Extension, alpha);
                if (earlier.Phase != GrapplePhase.Casting && later.Phase != GrapplePhase.Casting)
                    visible.RopeLength = Mathf.Lerp(earlier.RopeLength, later.RopeLength, alpha);
                visible.Anchor = Vector3.Lerp(earlier.Anchor, later.Anchor, alpha);
                if (earlier.AnchorObject == later.AnchorObject)
                    visible.AnchorLocalPoint = Vector3.Lerp(earlier.AnchorLocalPoint, later.AnchorLocalPoint, alpha);
            }
        }
        grapplingHook.SetRemotePresentation(visible);
    }

    private void Update()
    {
        if (!logDiagnostics || Runner == null || Object == null || !Object.HasInputAuthority ||
            Time.unscaledTime < nextLocalDiagnosticTime)
            return;

        nextLocalDiagnosticTime = Time.unscaledTime + 2f;
        var characterController = GetComponentInChildren<CharacterController>(true);
        var capsuleBottom = characterController != null ? characterController.bounds.min.y : float.NaN;
        var locomotionCore = GetComponentInChildren<LocomotionCore>(true);
        var grounded = locomotionCore != null && locomotionCore.CheckIsGrounded();
        var animator = GetComponentInChildren<Animator>(true);
        var animatorGrounded = animator != null && animator.GetBool(Animator.StringToHash("IsGrounded"));
        Debug.Log($"[FS Local Player View] mode={Runner.GameMode} object={Object.Id} stateAuthority={Object.HasStateAuthority} input={inputManager?.DirectionInput ?? Vector2.zero} physicsGrounded={grounded} animatorGrounded={animatorGrounded} inputLocked={(inputManager != null && inputManager.RaceInputLocked)} controllerEnabled={(playerController != null && playerController.enabled)} root={transform.position} player={playerController?.transform.position} capsuleBottomY={capsuleBottom}");
    }

    private void LateUpdate()
    {
        if (Runner == null || Object == null)
            return;

        // FS locomotion moves the nested Player object. Fold that pose into the
        // NetworkObject root before Fusion captures/renders the networked pose,
        // then restore the child's authored local offset. This gives Fusion a
        // single transform that actually travels through the level.
        if (networkedPlayerTransform != null && Object.HasStateAuthority)
        {
            if (authoritativeTeleportSettleFrames > 0)
            {
                authoritativeTeleportSettleFrames--;
                networkedPlayerTransform.localPosition = networkedPlayerLocalPosition;
                networkedPlayerTransform.localRotation = networkedPlayerLocalRotation;
            }
            else
            {
                var playerWorldPosition = networkedPlayerTransform.position;
                var playerWorldRotation = networkedPlayerTransform.rotation;
                var rootRotation = playerWorldRotation * Quaternion.Inverse(networkedPlayerLocalRotation);
                transform.SetPositionAndRotation(
                    playerWorldPosition - rootRotation * networkedPlayerLocalPosition,
                    rootRotation);
                networkedPlayerTransform.localPosition = networkedPlayerLocalPosition;
                networkedPlayerTransform.localRotation = networkedPlayerLocalRotation;
            }
        }

        // State Authority is the sole source of grounded and airborne animation
        // state. A proxy-side GroundCheck here would run after Fusion Render and
        // overwrite the interpolated authoritative IsGrounded value. On ledges or
        // during landing that local sphere check can disagree permanently and trap
        // the proxy in FallTree/FallingToRoll.
        if (Object.HasStateAuthority || locomotionCore == null)
            return;

        if (logDiagnostics && Object.HasInputAuthority && inputManager != null)
        {
            var move = inputManager.DirectionInput;
            if (!hasLocalMoveSample || move != lastLocalMove)
            {
                hasLocalMoveSample = true;
                lastLocalMove = move;
                if (move.sqrMagnitude > 0.001f)
                {
                    var velocity = locomotionCore != null ? locomotionCore.CurrentVelocity : Vector3.zero;
                    var locomotionController = GetComponentInChildren<LocomotionController>(true);
                    Debug.Log($"[FS Client Move Input] move={move} inputLocked={inputManager.RaceInputLocked} controllerEnabled={(playerController != null && playerController.enabled)} velocity={velocity} preventAllSystems={(locomotionController != null && locomotionController.PreventAllSystems)} playerPosition={playerController?.transform.position}");
                }
            }
        }
    }

    public void ApplyAuthoritativeTeleport(Vector3 rootPosition, Quaternion rootRotation)
    {
        if (Object == null || !Object.HasStateAuthority || networkedPlayerTransform == null)
            return;

        if (grapplingHook != null)
        {
            grapplingHook.CancelForTeleport();
            NetworkGrapple = grapplingHook.CapturePresentation();
        }
        var characterController = networkedPlayerTransform.GetComponent<CharacterController>();
        var rigidbody = networkedPlayerTransform.GetComponent<Rigidbody>();
        var wasControllerEnabled = characterController != null && characterController.enabled;

        if (characterController != null)
            characterController.enabled = false;

        transform.SetPositionAndRotation(rootPosition, rootRotation);
        networkedPlayerTransform.localPosition = networkedPlayerLocalPosition;
        networkedPlayerTransform.localRotation = networkedPlayerLocalRotation;

        if (rigidbody != null)
        {
            rigidbody.position = networkedPlayerTransform.position;
            rigidbody.rotation = networkedPlayerTransform.rotation;
            rigidbody.linearVelocity = Vector3.zero;
            rigidbody.angularVelocity = Vector3.zero;
        }

        Physics.SyncTransforms();
        GetComponent<NetworkTransform>()?.Teleport(rootPosition, rootRotation);
        transform.SetPositionAndRotation(rootPosition, rootRotation);
        networkedPlayerTransform.localPosition = networkedPlayerLocalPosition;
        networkedPlayerTransform.localRotation = networkedPlayerLocalRotation;
        Physics.SyncTransforms();

        if (characterController != null)
            characterController.enabled = wasControllerEnabled;

        authoritativeTeleportSettleFrames = 2;
    }
}
