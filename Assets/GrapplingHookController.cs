using FS_Core;
using FS_LocomotionSystem;
using Fusion;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using UnityEngine.UI;

public enum GrapplePhase
{
    Idle,
    Casting,
    Swinging,
    Reeling,
    Rappelling
}

public struct GrapplePresentation : INetworkStruct
{
    public GrapplePhase Phase;
    public int Shot;
    public float Extension;
    public float Stamina;
    public Vector3 Anchor;
    public NetworkId AnchorObject;
    public Vector3 AnchorLocalPoint;
}

/// <summary>Camera-aimed grapple for the FS CharacterController and its Fusion authority bridge.</summary>
[DefaultExecutionOrder(2)] // Evaluate the reticle after CameraController updates the view.
[DisallowMultipleComponent]
[RequireComponent(typeof(PlayerController), typeof(CharacterController))]
public sealed class GrapplingHookController : SystemBase
{
    // All request data originates with Input Authority. Movement and collision are
    // resolved only by State Authority (or locally when used on an offline player).
    public const int ReelButton = 0;
    public const int RappelButton = 1;
    private const string Grappleable = "Grappleable";
    private const float ContactTolerance = 0.06f;
    private const int AimHitCapacity = 32;

    [Header("Targeting")]
    [SerializeField, Min(0.5f)] private float range = 20f;
    [SerializeField, Min(0.01f)] private float aimRadius = 0.22f;
    [SerializeField, Min(0.5f)] private float maxCameraOffset = 6f;
    [SerializeField] private LayerMask worldMask = Physics.DefaultRaycastLayers;
    [SerializeField] private LayerMask grappleableLayers;

    [Header("Controls")]
    [SerializeField] private Key grappleKey = Key.G;
    [SerializeField] private Key reelKey = Key.R;
    [SerializeField] private Key rappelKey = Key.V;

    [Header("Rope and momentum")]
    [SerializeField, Min(1f)] private float projectileSpeed = 55f;
    [SerializeField, Min(0.05f)] private float minimumCastTime = 0.07f;
    [SerializeField, Min(0.3f)] private float minimumRopeLength = 1.35f;
    [SerializeField, Min(0f)] private float swingAcceleration = 6f;
    [SerializeField, Min(1f)] private float maximumSpeed = 22f;
    [SerializeField, Min(0f)] private float drag = 0.1f;
    [SerializeField, Min(0.1f)] private float reelSpeed = 8f;
    [SerializeField, Min(0.1f)] private float reelAcceleration = 25f;
    [SerializeField, Range(0f, 1f)] private float reelGravityScale = 0.25f;
    [SerializeField, Min(0.1f)] private float rappelSpeed = 4f;
    [SerializeField, Min(0.1f)] private float rappelFallSpeed = 5f;
    [SerializeField, Min(0.1f)] private float rappelDamping = 12f;
    [SerializeField, Min(0.1f)] private float maximumAnchorStep = 1.25f;

    [Header("Ledge assist")]
    [SerializeField, Min(0f)] private float ledgeAssistRange = 2.6f;
    [SerializeField, Min(0.1f)] private float ledgeAssistHeight = 1.15f;
    [SerializeField, Min(0f)] private float ledgeUpSpeed = 5.6f;
    [SerializeField, Min(0f)] private float ledgeForwardSpeed = 2.4f;

    [Header("Stamina")]
    [SerializeField, Min(1f)] private float maxStamina = 100f;
    [SerializeField, Min(0.1f)] private float minimumStaminaToFire = 12f;
    [SerializeField, Min(0.1f)] private float swingStaminaPerSecond = 13f;
    [SerializeField, Min(0.1f)] private float reelStaminaPerSecond = 23f;
    [SerializeField, Min(0.1f)] private float rappelStaminaPerSecond = 10f;
    [SerializeField, Min(0.1f)] private float staminaRecoveryPerSecond = 24f;
    [SerializeField, Min(0f)] private float staminaRecoveryDelay = 0.8f;

    [Header("Presentation")]
    [SerializeField] private Transform ropeOrigin;
    [SerializeField] private LineRenderer ropeRenderer;
    [SerializeField] private Material ropeMaterial;
    [SerializeField, Min(0.005f)] private float ropeWidth = 0.026f;
    [SerializeField] private bool showReticle = true;
    [SerializeField] private Graphic reticleOverride;
    [SerializeField] private Color readyColor = new Color(0.75f, 1f, 0.54f, 1f);
    [SerializeField] private Color idleColor = new Color(1f, 1f, 1f, 0.54f);

    private readonly RaycastHit[] aimHits = new RaycastHit[AimHitCapacity];
    private PlayerController playerController;
    private CharacterController capsule;
    private ICharacter player;
    private LocomotionCore locomotion;
    private LocomotionInputManager locomotionInput;
    private ActionExecutor actionExecutor;
    private FSNetworkAuthoritySetup networkAuthority;
    private Collider anchorCollider;
    private Transform anchorTransform;
    private NetworkObject networkAnchor;
    private Vector3 anchorLocalPoint;
    private Vector3 lastAnchor;
    private Vector3 velocity;
    private float ropeLength;
    private float castTime;
    private float castElapsed;
    private float stamina;
    private float recoveryDelay;
    private int shot;
    private GrapplePhase phase;

    private int localPress;
    private Vector3 localPressOffset;
    private Vector3 localPressDirection;
    private int consumedPress;
    private bool pendingToggle;
    private bool movementAllowed;
    private bool suspendedRootMotion;
    private bool previousRootMotion;
    private bool ropeConfigured;
    private int ignoredLayers;
    private int namedGrappleableLayer;
    private FSPlayerNetworkInput authorityInput;
    private GrapplePresentation remotePresentation;
    private Canvas reticleCanvas;
    private Image[] reticleBars;
    private Image staminaFill;
    private RectTransform staminaTrack;
    private Material fallbackRopeMaterial;

    public override SystemState State => SystemState.GrapplingHook;
    public override bool IsUnreachableByGround => IsInFocus;
    public GrapplePhase Phase => SimulatesLocally ? phase : remotePresentation.Phase;
    public float Stamina01 => SimulatesLocally ? Mathf.Clamp01(stamina / maxStamina) : remotePresentation.Stamina;

    private bool SimulatesLocally => networkAuthority == null ||
        (networkAuthority.Object != null && networkAuthority.Object.HasStateAuthority);

    private bool OwnsInput => networkAuthority == null ||
        (networkAuthority.Object != null && networkAuthority.Object.HasInputAuthority);

    private Transform View => playerController.cameraGameObject != null
        ? playerController.cameraGameObject.transform
        : null;

    private Vector3 Harness => transform.TransformPoint(capsule.center);
    private int CollisionMask => worldMask.value & ~ignoredLayers;

    private void Awake()
    {
        playerController = GetComponent<PlayerController>();
        capsule = GetComponent<CharacterController>();
        player = GetComponent<ICharacter>();
        locomotion = GetComponent<LocomotionCore>();
        locomotionInput = GetComponent<LocomotionInputManager>();
        actionExecutor = GetComponent<ActionExecutor>();
        networkAuthority = GetComponentInParent<FSNetworkAuthoritySetup>();
        ignoredLayers = LayerMask.GetMask("Player", "Enemy", "NPC", "PlayerAlly");
        namedGrappleableLayer = LayerMask.GetMask(Grappleable);
        stamina = maxStamina;
        if (player == null || locomotion == null)
        {
            Debug.LogError("GrapplingHookController requires the FS LocomotionController and LocomotionCore.", this);
            enabled = false;
        }
    }

    private void Start()
    {
        if (!playerController.CoreSystems.Contains(this))
        {
            playerController.Register(this);
            playerController.CoreSystems.Add(this);
        }

        if (ropeOrigin == null && player.Animator != null && player.Animator.isHuman)
            ropeOrigin = player.Animator.GetBoneTransform(HumanBodyBones.RightHand);
    }

    private void Update()
    {
        if (!OwnsInput || !AcceptsLocalInput() || View == null)
            return;

        var keyboard = Keyboard.current;
        if (keyboard != null && grappleKey != Key.None && keyboard[grappleKey].wasPressedThisFrame)
        {
            localPressOffset = View.position - transform.position;
            localPressDirection = View.forward;
            localPress++;
        }
    }

    public void WriteNetworkInput(ref FSPlayerNetworkInput input)
    {
        input.GrapplePress = localPress;
        input.GrappleAimOffset = localPressOffset;
        input.GrappleAimDirection = localPressDirection;

        var keyboard = Keyboard.current;
        if (keyboard == null || !AcceptsLocalInput())
            return;

        input.GrappleButtons.Set(ReelButton, reelKey != Key.None && keyboard[reelKey].isPressed);
        input.GrappleButtons.Set(RappelButton, rappelKey != Key.None && keyboard[rappelKey].isPressed);
    }

    public void ReadAuthorityInput(FSPlayerNetworkInput input, bool hasInput, bool canMove)
    {
        movementAllowed = canMove && !player.PreventAllSystems && capsule.enabled;
        if (hasInput)
        {
            pendingToggle |= movementAllowed && input.GrapplePress != consumedPress;
            consumedPress = input.GrapplePress;
        }
        authorityInput = hasInput ? input : default;

        if (!movementAllowed)
        {
            pendingToggle = false;
            Release(false);
        }
    }

    public override void HandleFixedUpdate()
    {
        if (!isActiveAndEnabled || !SimulatesLocally || !capsule.enabled)
            return;

        if (networkAuthority == null || networkAuthority.Runner.GameMode is GameMode.Shared or GameMode.Single)
        {
            FSPlayerNetworkInput input = default;
            WriteNetworkInput(ref input);
            input.Move = locomotionInput != null ? locomotionInput.DirectionInput : Vector2.zero;
            input.CameraForward = View != null ? View.forward : transform.forward;
            ReadAuthorityInput(input, true, locomotionInput == null || !locomotionInput.RaceInputLocked);
        }

        float dt = Time.fixedDeltaTime;
        bool toggle = pendingToggle;
        pendingToggle = false;
        if (phase == GrapplePhase.Idle)
        {
            recoveryDelay = Mathf.Max(0f, recoveryDelay - dt);
            if (recoveryDelay <= 0f)
                stamina = Mathf.Min(maxStamina, stamina + staminaRecoveryPerSecond * dt);

            if (movementAllowed && toggle && CanStartCast() &&
                TryGetValidatedAim(out var origin, out var direction) && TryFindAnchor(origin, direction, out var hit))
                StartCast(hit);
            return;
        }

        if (!movementAllowed || toggle || !TryGetAnchor(out Vector3 anchor) || IsRopeBlocked(anchor))
        {
            Release(movementAllowed);
            return;
        }

        if (phase == GrapplePhase.Casting)
        {
            if (!SpendStamina(swingStaminaPerSecond, dt))
                return;

            castElapsed += dt;
            if (castElapsed >= castTime)
                Attach(anchor);
            return;
        }

        if (!IsInFocus)
        {
            Release(false);
            return;
        }

        bool reel = authorityInput.GrappleButtons.IsSet(ReelButton);
        bool rappel = !reel && authorityInput.GrappleButtons.IsSet(RappelButton);
        phase = reel ? GrapplePhase.Reeling : rappel ? GrapplePhase.Rappelling : GrapplePhase.Swinging;
        if (!SpendStamina(reel ? reelStaminaPerSecond : rappel ? rappelStaminaPerSecond : swingStaminaPerSecond, dt))
            return;

        MoveAlongRope(anchor, reel, rappel, dt);
    }

    private bool CanStartCast()
    {
        return stamina >= Mathf.Min(minimumStaminaToFire, maxStamina) && CanTakeMovementFocus();
    }

    private bool CanTakeMovementFocus()
    {
        return capsule.enabled && playerController.FocusedScript == null &&
            !player.PreventAllSystems && (actionExecutor == null || actionExecutor.CurrentAction == null);
    }

    private bool TryGetValidatedAim(out Vector3 origin, out Vector3 direction)
    {
        origin = transform.position + authorityInput.GrappleAimOffset;
        direction = authorityInput.GrappleAimDirection;
        if (!IsFinite(origin) || !IsFinite(direction) ||
            authorityInput.GrappleAimOffset.sqrMagnitude > maxCameraOffset * maxCameraOffset ||
            direction.sqrMagnitude < 0.25f || direction.sqrMagnitude > 4f)
            return false;

        direction.Normalize();
        // A remote view origin must still be reachable from its authoritative avatar.
        return !Physics.Linecast(Harness, origin, CollisionMask, QueryTriggerInteraction.Ignore);
    }

    private bool TryFindAnchor(Vector3 origin, Vector3 direction, out RaycastHit result)
    {
        result = default;
        int count = Physics.SphereCastNonAlloc(origin, aimRadius, direction, aimHits, range,
            CollisionMask, QueryTriggerInteraction.Ignore);
        if (count == AimHitCapacity)
            return false;

        float nearest = float.PositiveInfinity;
        for (int i = 0; i < count; i++)
        {
            var hit = aimHits[i];
            if (hit.collider == null || hit.distance >= nearest || hit.collider.transform.IsChildOf(transform))
                continue;

            nearest = hit.distance;
            result = hit;
        }
        return result.collider != null && IsGrappleable(result.collider.transform) &&
            (result.point - origin).sqrMagnitude <= range * range &&
            Vector3.Distance(result.point, Harness) <= range && !IsRopeBlocked(result.point);
    }

    private bool IsGrappleable(Transform target)
    {
        int mask = grappleableLayers.value | namedGrappleableLayer;
        for (var current = target; current != null; current = current.parent)
            if ((mask & (1 << current.gameObject.layer)) != 0 || current.CompareTag(Grappleable))
                return true;
        return false;
    }

    private void StartCast(RaycastHit hit)
    {
        anchorCollider = hit.collider;
        anchorTransform = hit.collider.transform;
        anchorLocalPoint = anchorTransform.InverseTransformPoint(hit.point);
        networkAnchor = anchorTransform.GetComponentInParent<NetworkObject>();
        lastAnchor = hit.point;
        castTime = Mathf.Max(minimumCastTime, Vector3.Distance(Harness, hit.point) / projectileSpeed);
        castElapsed = 0f;
        shot++;
        phase = GrapplePhase.Casting;
    }

    private void Attach(Vector3 anchor)
    {
        float distance = Vector3.Distance(Harness, anchor);
        if (!CanTakeMovementFocus() || distance > range || distance < 0.15f)
        {
            Release(false);
            return;
        }

        velocity = Vector3.ClampMagnitude(capsule.velocity, maximumSpeed);
        player.FocusSystem(this);
        if (!IsInFocus)
        {
            Release(false);
            return;
        }

        ropeLength = Mathf.Clamp(distance, Mathf.Min(minimumRopeLength, range), range);
        previousRootMotion = locomotion.UseLocomotionRootMotion;
        locomotion.UseLocomotionRootMotion = false;
        suspendedRootMotion = true;
        lastAnchor = anchor;
        phase = GrapplePhase.Swinging;
    }

    private void MoveAlongRope(Vector3 anchor, bool reel, bool rappel, float dt)
    {
        Vector3 position = Harness;
        Vector3 toAnchor = anchor - position;
        Vector3 inward = toAnchor.normalized;
        Vector3 anchorStep = anchor - lastAnchor;
        if (anchorStep.sqrMagnitude > maximumAnchorStep * maximumAnchorStep)
        {
            Release(true);
            return;
        }
        Vector3 anchorVelocity = anchorStep / dt;
        lastAnchor = anchor;

        if (reel)
        {
            ropeLength = Mathf.MoveTowards(ropeLength, Mathf.Min(minimumRopeLength, range), reelSpeed * dt);
            float speedAlongRope = Vector3.Dot(velocity - anchorVelocity, inward);
            velocity += inward * Mathf.Clamp(reelSpeed - speedAlongRope, 0f, reelAcceleration * dt);
        }
        else if (rappel)
        {
            ropeLength = Mathf.Min(range, ropeLength + rappelSpeed * dt);
        }

        if (rappel && velocity.y < 0f)
            velocity.y = Mathf.Lerp(velocity.y, -rappelFallSpeed, 1f - Mathf.Exp(-rappelDamping * dt));
        else
            velocity.y += player.Gravity * (reel ? reelGravityScale : 1f) * dt;
        Vector3 forward = Vector3.ProjectOnPlane(authorityInput.CameraForward, Vector3.up).normalized;
        if (!IsFinite(forward) || forward.sqrMagnitude < 0.01f)
            forward = transform.forward;
        Vector2 move = authorityInput.Move;
        if (float.IsFinite(move.x) && float.IsFinite(move.y))
        {
            move = Vector2.ClampMagnitude(move, 1f);
            var steer = forward * move.y + Vector3.Cross(Vector3.up, forward) * move.x;
            if (toAnchor.magnitude >= ropeLength - ContactTolerance)
                steer = Vector3.ProjectOnPlane(steer, inward);
            velocity += steer * (swingAcceleration * dt);
        }

        velocity *= Mathf.Exp(-drag * dt);
        velocity = Vector3.ClampMagnitude(velocity, maximumSpeed);
        if (reel && toAnchor.sqrMagnitude <= ledgeAssistRange * ledgeAssistRange && TryAssistLedge(inward))
            return;

        // A unilateral constraint: the rope only pulls when taut and never pushes at shorter distances.
        Vector3 radial = position + velocity * dt - anchor;
        Vector3 requested = anchor + Vector3.ClampMagnitude(radial, ropeLength) - position;
        CollisionFlags collision = capsule.Move(requested);
        Vector3 finalRadial = Harness - anchor;
        if (finalRadial.magnitude > ropeLength + ContactTolerance)
        {
            // Contact sliding can move the capsule outside the rope sphere.
            // Correct through the capsule too, so a blocked pull cannot tunnel.
            collision |= capsule.Move(Vector3.ClampMagnitude(finalRadial, ropeLength) - finalRadial);
            finalRadial = Harness - anchor;
        }

        velocity = Vector3.ClampMagnitude((Harness - position) / dt, maximumSpeed);
        if (finalRadial.sqrMagnitude >= (ropeLength - ContactTolerance) * (ropeLength - ContactTolerance))
        {
            Vector3 outward = finalRadial.normalized;
            velocity -= outward * Mathf.Max(0f, Vector3.Dot(velocity - anchorVelocity, outward));
        }

        bool grounded = (collision & CollisionFlags.Below) != 0;
        player.Animator.SetBool(AnimatorParameters.IsGrounded, grounded);
        player.Animator.SetFloat(AnimatorParameters.fallAmount, Mathf.Clamp01(Mathf.Abs(velocity.y) * 0.05f));
        if (finalRadial.magnitude > ropeLength + ContactTolerance || IsRopeBlocked(anchor))
            Release(true);
    }

    private bool TryAssistLedge(Vector3 towardsAnchor)
    {
        Vector3 forward = Vector3.ProjectOnPlane(towardsAnchor, Vector3.up).normalized;
        if (forward.sqrMagnitude < 0.01f)
            return false;

        Vector3 feet = capsule.bounds.center - Vector3.up * capsule.bounds.extents.y;
        float radius = capsule.radius * Mathf.Max(transform.lossyScale.x, transform.lossyScale.z);
        float height = capsule.bounds.size.y;
        Vector3 wallOrigin = feet + Vector3.up * 0.25f;
        if (!Physics.Raycast(wallOrigin, forward, out var wall, radius + 0.5f,
            CollisionMask, QueryTriggerInteraction.Ignore) || Mathf.Abs(wall.normal.y) > 0.45f)
            return false;

        Vector3 aboveLedge = feet + forward * (wall.distance + radius + ContactTolerance) +
            Vector3.up * (ledgeAssistHeight + ContactTolerance);
        if (!Physics.Raycast(aboveLedge, Vector3.down, out var top, ledgeAssistHeight,
            CollisionMask, QueryTriggerInteraction.Ignore) ||
            Vector3.Angle(top.normal, Vector3.up) > capsule.slopeLimit)
            return false;

        Vector3 capsuleBottom = top.point + Vector3.up * (radius + ContactTolerance);
        Vector3 capsuleTop = capsuleBottom + Vector3.up * Mathf.Max(0f, height - 2f * radius);
        if (Physics.CheckCapsule(capsuleBottom, capsuleTop, radius, CollisionMask, QueryTriggerInteraction.Ignore))
            return false;

        // Scale the kick to clear the measured lip under the controller's gravity.
        float rise = top.point.y - feet.y + 0.12f;
        float lift = Mathf.Sqrt(2f * Mathf.Abs(player.Gravity) * rise);
        velocity += forward * Mathf.Max(0f, ledgeForwardSpeed - Vector3.Dot(velocity, forward));
        velocity.y = Mathf.Max(velocity.y, Mathf.Max(ledgeUpSpeed, lift));
        Release(true);
        return true;
    }

    private bool TryGetAnchor(out Vector3 anchor)
    {
        anchor = default;
        if (anchorCollider == null || !anchorCollider.enabled || !anchorCollider.gameObject.activeInHierarchy)
            return false;

        anchor = anchorTransform.TransformPoint(anchorLocalPoint);
        return IsFinite(anchor);
    }

    private bool IsRopeBlocked(Vector3 anchor)
    {
        Vector3 start = Harness;
        Vector3 rope = anchor - start;
        if (rope.sqrMagnitude <= ContactTolerance * ContactTolerance)
            return false;

        return Physics.Linecast(start, anchor - rope.normalized * ContactTolerance,
            CollisionMask, QueryTriggerInteraction.Ignore);
    }

    private bool SpendStamina(float cost, float dt)
    {
        stamina = Mathf.Max(0f, stamina - cost * dt);
        if (stamina > 0f)
            return true;
        Release(true);
        return false;
    }

    public void CancelForTeleport()
    {
        pendingToggle = false;
        Release(false);
    }

    private void Release(bool preserveMomentum)
    {
        if (phase == GrapplePhase.Idle)
            return;

        Vector3 carry = velocity;
        ClearRope();
        if (!IsInFocus || playerController == null)
            return;

        player.UnFocusSystem(this);
        if (preserveMomentum)
            locomotion.ApplyExternalAirMomentum(carry);
    }

    public override void ExitSystem() => ClearRope();

    private void ClearRope()
    {
        if (suspendedRootMotion && locomotion != null)
        {
            locomotion.UseLocomotionRootMotion = previousRootMotion;
            suspendedRootMotion = false;
        }
        phase = GrapplePhase.Idle;
        anchorCollider = null;
        anchorTransform = null;
        networkAnchor = null;
        velocity = Vector3.zero;
        recoveryDelay = staminaRecoveryDelay;
    }

    public GrapplePresentation CapturePresentation()
    {
        var state = new GrapplePresentation { Phase = phase, Shot = shot, Stamina = Mathf.Clamp01(stamina / maxStamina) };
        if (phase == GrapplePhase.Idle || !TryGetAnchor(out state.Anchor))
        {
            state.Phase = GrapplePhase.Idle;
            return state;
        }

        state.Extension = phase == GrapplePhase.Casting ? Mathf.Clamp01(castElapsed / castTime) : 1f;
        if (networkAnchor != null && networkAnchor.IsValid)
        {
            state.AnchorObject = networkAnchor.Id;
            state.AnchorLocalPoint = networkAnchor.transform.InverseTransformPoint(state.Anchor);
        }
        return state;
    }

    public void SetRemotePresentation(GrapplePresentation state) => remotePresentation = state;

    private void LateUpdate()
    {
        bool authority = SimulatesLocally;
        var visible = authority ? CapturePresentation() : remotePresentation;
        DrawRope(visible, authority);
        if (OwnsInput && showReticle)
            DrawReticle(visible);
    }

    private void DrawRope(GrapplePresentation visible, bool authority)
    {
        if (visible.Phase == GrapplePhase.Idle)
        {
            if (ropeRenderer != null)
                ropeRenderer.enabled = false;
            return;
        }

        EnsureRopeRenderer();
        Vector3 anchor = visible.Anchor;
        if (!authority && visible.AnchorObject.IsValid && networkAuthority.Runner != null &&
            networkAuthority.Runner.TryFindObject(visible.AnchorObject, out var target))
            anchor = target.transform.TransformPoint(visible.AnchorLocalPoint);

        Vector3 origin = ropeOrigin != null ? ropeOrigin.position : Harness;
        ropeRenderer.SetPosition(0, origin);
        ropeRenderer.SetPosition(1, Vector3.Lerp(origin, anchor, visible.Extension));
        ropeRenderer.enabled = true;
    }

    private void EnsureRopeRenderer()
    {
        if (ropeConfigured && ropeRenderer != null)
            return;

        if (ropeRenderer == null)
        {
            var line = new GameObject("Grapple Rope");
            line.transform.SetParent(transform, false);
            ropeRenderer = line.AddComponent<LineRenderer>();
        }
        ropeRenderer.useWorldSpace = true;
        ropeRenderer.positionCount = 2;
        ropeRenderer.widthMultiplier = ropeWidth;
        ropeRenderer.shadowCastingMode = ShadowCastingMode.Off;
        ropeRenderer.receiveShadows = false;
        if (ropeMaterial != null)
            ropeRenderer.sharedMaterial = ropeMaterial;
        else if (ropeRenderer.sharedMaterial == null)
        {
            var shader = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Sprites/Default");
            if (shader != null)
            {
                fallbackRopeMaterial = new Material(shader) { color = new Color(0.98f, 0.77f, 0.42f) };
                ropeRenderer.sharedMaterial = fallbackRopeMaterial;
            }
        }
        ropeConfigured = true;
    }

    private void DrawReticle(GrapplePresentation visible)
    {
        bool canAim = AcceptsLocalInput() && View != null;
        bool valid = canAim && visible.Phase == GrapplePhase.Idle &&
            visible.Stamina * maxStamina >= Mathf.Min(minimumStaminaToFire, maxStamina) &&
            TryFindAnchor(View.position, View.forward, out _);
        Color color = valid || visible.Phase != GrapplePhase.Idle ? readyColor : idleColor;
        if (reticleOverride != null)
        {
            reticleOverride.enabled = canAim;
            reticleOverride.color = color;
            return;
        }

        if (reticleCanvas == null)
            CreateReticle();
        reticleCanvas.enabled = canAim;
        foreach (var bar in reticleBars)
            bar.color = color;

        staminaTrack.gameObject.SetActive(visible.Stamina < 0.999f);
        staminaFill.rectTransform.anchorMax = new Vector2(Mathf.Clamp01(visible.Stamina), 1f);
        staminaFill.color = Color.Lerp(new Color(1f, 0.34f, 0.22f), readyColor, visible.Stamina);
    }

    private void CreateReticle()
    {
        var canvasObject = new GameObject("Grapple Reticle", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
        reticleCanvas = canvasObject.GetComponent<Canvas>();
        reticleCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        reticleCanvas.sortingOrder = 15;
        var scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        reticleBars = new[]
        {
            CreateMark("Left", canvasObject.transform, new Vector2(-9f, 0f), new Vector2(6f, 2f)),
            CreateMark("Right", canvasObject.transform, new Vector2(9f, 0f), new Vector2(6f, 2f)),
            CreateMark("Top", canvasObject.transform, new Vector2(0f, 9f), new Vector2(2f, 6f)),
            CreateMark("Bottom", canvasObject.transform, new Vector2(0f, -9f), new Vector2(2f, 6f))
        };
        var track = CreateMark("Stamina", canvasObject.transform, new Vector2(0f, -24f), new Vector2(66f, 4f));
        track.color = new Color(0.05f, 0.05f, 0.05f, 0.72f);
        staminaTrack = track.rectTransform;
        staminaFill = CreateMark("Fill", staminaTrack, Vector2.zero, Vector2.zero);
        staminaFill.rectTransform.anchorMin = Vector2.zero;
        staminaFill.rectTransform.offsetMin = Vector2.zero;
        staminaFill.rectTransform.offsetMax = Vector2.zero;
    }

    private static Image CreateMark(string name, Transform parent, Vector2 position, Vector2 size)
    {
        var mark = new GameObject(name, typeof(RectTransform), typeof(Image)).GetComponent<Image>();
        mark.transform.SetParent(parent, false);
        mark.rectTransform.anchorMin = mark.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
        mark.rectTransform.anchoredPosition = position;
        mark.rectTransform.sizeDelta = size;
        mark.raycastTarget = false;
        return mark;
    }

    private bool AcceptsLocalInput()
    {
        if (locomotionInput != null && locomotionInput.RaceInputLocked)
            return false;
        var selected = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
        return selected == null ||
            (!(selected.TryGetComponent(out TMP_InputField tmpField) && tmpField.isFocused) &&
             !(selected.TryGetComponent(out InputField field) && field.isFocused));
    }

    private static bool IsFinite(Vector3 vector) =>
        float.IsFinite(vector.x) && float.IsFinite(vector.y) && float.IsFinite(vector.z);

    private void OnDisable()
    {
        pendingToggle = false;
        if (SimulatesLocally)
            Release(false);
        if (ropeRenderer != null)
            ropeRenderer.enabled = false;
        if (reticleCanvas != null)
            reticleCanvas.enabled = false;
        if (reticleOverride != null)
            reticleOverride.enabled = false;
    }

    private void OnDestroy()
    {
        if (playerController != null)
        {
            playerController.CoreSystems.Remove(this);
            playerController.Unregister(this);
        }
        if (fallbackRopeMaterial != null)
            Destroy(fallbackRopeMaterial);
        if (reticleCanvas != null)
            Destroy(reticleCanvas.gameObject);
    }
}
