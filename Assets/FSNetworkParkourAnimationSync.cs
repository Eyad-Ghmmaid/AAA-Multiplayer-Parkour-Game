using System;
using System.Collections;
using System.Collections.Generic;
using Fusion;
using FS_Core;
using FS_ParkourSystem;
using FC_ParkourSystem;
using UnityEngine;

/// <summary>
/// Relays one-shot FS parkour actions which are played through AnimGraph.
/// NetworkMecanimAnimator owns normal Animator Controller states on true proxies;
/// this component additionally supplies the owning Input Authority client, which
/// Fusion does not classify as a proxy. PlayableGraph clips are replayed separately.
/// The State Authority remains responsible for movement and root motion.
/// </summary>
public class FSNetworkParkourAnimationSync : NetworkBehaviour
{
    private static readonly int ClimbXHash = Animator.StringToHash("x");
    private static readonly int ClimbYHash = Animator.StringToHash("y");

    [SerializeField] private bool logDiagnostics;

    [Networked] private NetworkString<_64> ActiveActionName { get; set; }
    [Networked] private NetworkBool ActiveActionUsesAnimGraph { get; set; }
    [Networked] private NetworkBool ActiveActionUsesFixedTransition { get; set; }
    [Networked] private float ActiveTransitionIn { get; set; }
    [Networked] private float ActiveTransitionOut { get; set; }
    [Networked] private float ActiveAnimationSpeed { get; set; }
    [Networked] private float ActiveAuthorityClipLength { get; set; }
    [Networked] private float ActiveGraphNormalizedTime { get; set; }
    [Networked] private int ActionSequence { get; set; }
    [Networked] private NetworkBool RemoteHangIKActive { get; set; }
    [Networked] private Vector3 RemoteRightHandTarget { get; set; }
    [Networked] private Vector3 RemoteLeftHandTarget { get; set; }
    [Networked] private float NetworkMoveAmount { get; set; }
    [Networked] private float NetworkStrafeAmount { get; set; }
    [Networked] private float NetworkRotation { get; set; }
    [Networked] private float NetworkFallAmount { get; set; }
    [Networked] private float NetworkLocomotionType { get; set; }
    [Networked] private float NetworkIdleType { get; set; }
    [Networked] private float NetworkCrouchType { get; set; }
    [Networked] private float NetworkRunToStopAmount { get; set; }
    [Networked] private float NetworkClimbX { get; set; }
    [Networked] private float NetworkClimbY { get; set; }
    [Networked] private float NetworkFreeHang { get; set; }
    [Networked] private float NetworkJumpBackDirection { get; set; }
    [Networked] private float NetworkBackJumpDirection { get; set; }
    [Networked] private NetworkBool NetworkIsGrounded { get; set; }
    [Networked] private NetworkBool NetworkTurnbackMirror { get; set; }
    [Networked] private NetworkBool NetworkMirrorAction { get; set; }
    [Networked] private NetworkBool NetworkIsFalling { get; set; }
    [Networked] private NetworkBool NetworkMirrorJump { get; set; }
    [Networked] private NetworkBool NetworkBackJumpMode { get; set; }
    [Networked] private NetworkBool NetworkSlideActive { get; set; }
    [Networked] private int NetworkOwnerBaseStateHash { get; set; }
    [Networked] private float NetworkOwnerBaseStateNormalizedTime { get; set; }
    [Networked] private int NetworkOwnerBaseStateSequence { get; set; }

    private ActionExecutor actionExecutor;
    private AnimGraph animGraph;
    private Animator animator;
    private ParkourController parkourController;
    private ClimbController climbController;
    private ClimbCore climbCore;
    private SlidingController slidingController;

    private string pendingActionName;
    private bool pendingUsesAnimGraph;
    private bool pendingUsesFixedTransition;
    private float pendingTransitionIn;
    private float pendingTransitionOut;
    private float pendingAnimationSpeed;
    private float pendingAuthorityClipLength;
    private bool hasPendingAction;
    private readonly Queue<ActionCue> actionCueQueue = new Queue<ActionCue>();

    private int lastAppliedRpcSequence = -1;
    private int initialStateSequence = -1;
    private bool initializedProxyAnimationState;
    private bool proxyGraphActionPlaying;
    private int proxyGraphGeneration;
    private Coroutine proxyGraphCoroutine;
    private Coroutine proxyGraphRecoveryCoroutine;
    private int lastAppliedOwnerBaseStateHash;
    private int lastAppliedOwnerBaseStateSequence = -1;
    private int lastCapturedAuthorityBaseStateHash;
    private bool hasCapturedAuthorityBaseState;
    private int ownerMecanimCueSequence = -1;
    private string ownerMecanimCueName;
    private bool forceOwnerBaseStateAfterCue;
    private bool ownerMecanimActionActive;
    private string ownerMecanimActionName;
    private int ownerMecanimActionStateHash;
    private bool ownerAwaitingMecanimPostClearState;
    private bool warnedAboutMissingClip;
    private float renderedGraphNormalizedTime;
    private bool hasRenderedGraphTime;
    private int proxyGraphActionSequence = -1;
    private float proxyGraphTransitionIn;
    private float proxyGraphTransitionOut;
    private bool proxySlideActive;
    private readonly Dictionary<string, AnimGraphClipInfo> clipInfoCache =
        new Dictionary<string, AnimGraphClipInfo>(StringComparer.Ordinal);

    // The authority can finish a short step/vault action while the owning client
    // is still blending its presentation clip. Give the normal transition-out a
    // moment to complete, then rebuild only the local presentation graph if it is
    // still stuck. This never changes gameplay state or the networked transform.
    private const float ProxyGraphRecoveryTimeout = 0.75f;

    private struct ActionCue
    {
        public string Name;
        public bool UsesAnimGraph;
        public bool UsesFixedTransition;
        public float TransitionIn;
        public float TransitionOut;
        public float AnimationSpeed;
        public float AuthorityClipLength;
    }

    public override void Spawned()
    {
        actionExecutor = GetComponent<ActionExecutor>();
        animGraph = GetComponent<AnimGraph>();
        animator = GetComponent<Animator>();
        parkourController = GetComponent<ParkourController>();
        climbController = GetComponent<ClimbController>();
        climbCore = GetComponent<ClimbCore>();
        slidingController = GetComponent<SlidingController>();

        if (actionExecutor == null || animGraph == null || animator == null)
        {
            Debug.LogError("FSNetworkParkourAnimationSync needs ActionExecutor, AnimGraph, and Animator on the same Player object.", this);
            enabled = false;
            return;
        }

        actionExecutor.OnActionStarted += OnActionStarted;
        actionExecutor.OnActionEnded += OnActionEnded;
        initializedProxyAnimationState = false;
        LogDiagnostic($"Spawned; stateAuthority={HasStateAuthority}, inputAuthority={HasInputAuthority}");
    }

    public override void FixedUpdateNetwork()
    {
        if (!HasStateAuthority)
            return;

        CaptureLocomotionParameters();
        CaptureAuthorityGraphProgress();
        UpdateRemoteHangIKState();
        NetworkSlideActive = slidingController != null
            && slidingController.IsSlidingAnimationActive;

        if (!hasPendingAction)
            return;

        // Networked properties preserve the current action for late joiners. The
        // reliable RPC queue preserves every start event between snapshots.
        int queuedCueCount = 0;
        ActionCue lastQueuedCue = default;
        while (actionCueQueue.Count > 0)
        {
            ActionCue cue = actionCueQueue.Dequeue();
            SendActionCue(cue);
            lastQueuedCue = cue;
            queuedCueCount++;
        }

        ActionCue currentCue = new ActionCue
        {
            Name = pendingActionName ?? string.Empty,
            UsesAnimGraph = pendingUsesAnimGraph,
            UsesFixedTransition = pendingUsesFixedTransition,
            TransitionIn = pendingTransitionIn,
            TransitionOut = pendingTransitionOut,
            AnimationSpeed = pendingAnimationSpeed,
            AuthorityClipLength = pendingAuthorityClipLength
        };

        // If the action ended or was replaced before this tick, publish that final
        // state too. Otherwise the last queued start already represents it.
        if (queuedCueCount == 0 || !SameCue(lastQueuedCue, currentCue))
            SendActionCue(currentCue);
        else
            SetNetworkAction(currentCue);

        hasPendingAction = false;
    }

    private void CaptureAuthorityGraphProgress()
    {
        if (actionExecutor?.CurrentAction == null
            || !actionExecutor.CurrentAction.isAnimGraphAction
            || animGraph?.currentClipStateInfo == null)
            return;

        ActiveGraphNormalizedTime = Mathf.Clamp01(
            animGraph.currentClipStateInfo.normalizedTime);
    }

    private void CaptureLocomotionParameters()
    {
        if (animator == null)
            return;

        NetworkMoveAmount = animator.GetFloat(AnimatorParameters.moveAmount);
        NetworkStrafeAmount = animator.GetFloat(AnimatorParameters.strafeAmount);
        NetworkRotation = animator.GetFloat(AnimatorParameters.rotation);
        NetworkFallAmount = animator.GetFloat(AnimatorParameters.fallAmount);
        NetworkLocomotionType = animator.GetFloat(AnimatorParameters.locomotionType);
        NetworkIdleType = animator.GetFloat(AnimatorParameters.idleType);
        NetworkCrouchType = animator.GetFloat(AnimatorParameters.crouchType);
        NetworkRunToStopAmount = animator.GetFloat(AnimatorParameters.runToStopAmount);
        NetworkClimbX = animator.GetFloat(ClimbXHash);
        NetworkClimbY = animator.GetFloat(ClimbYHash);
        NetworkFreeHang = animator.GetFloat(AnimatorParameters.freeHang);
        NetworkJumpBackDirection = animator.GetFloat(AnimatorParameters.jumpBackDirection);
        NetworkBackJumpDirection = animator.GetFloat(AnimatorParameters.BackJumpDir);
        NetworkIsGrounded = animator.GetBool(AnimatorParameters.IsGrounded);
        NetworkTurnbackMirror = animator.GetBool(AnimatorParameters.turnback_Mirror);
        NetworkMirrorAction = animator.GetBool(AnimatorParameters.mirrorAction);
        NetworkIsFalling = animator.GetBool(AnimatorParameters.isFalling);
        NetworkMirrorJump = animator.GetBool(AnimatorParameters.mirrorJump);
        NetworkBackJumpMode = animator.GetBool(AnimatorParameters.BackJumpMode);

        // Fusion's NetworkMecanimAnimator applies only to NetworkObject.IsProxy.
        // An owning client (Input Authority) is explicitly not a proxy, so capture
        // the authoritative Base Layer state for that client on our render timeline.
        AnimatorStateInfo baseState = animator.GetCurrentAnimatorStateInfo(0);
        if (animator.IsInTransition(0))
        {
            AnimatorStateInfo nextState = animator.GetNextAnimatorStateInfo(0);
            if (nextState.fullPathHash != 0)
                baseState = nextState;
        }
        // Keep the time unwrapped on the wire. Interpolating 0.99 -> 0.01 at a
        // looping-state boundary would otherwise sample the middle of the clip and
        // create a visible pose jump. Consumers repeat it only when applying it.
        float baseStateNormalizedTime = baseState.normalizedTime;

        // Do not treat normalized-time wrapping as a new occurrence. Some FS states
        // report loop=false but remain active long enough for normalizedTime to wrap;
        // the old heuristic restarted those states about once per second on the
        // owning client. Repeated entries into the same state are carried by the
        // reliable action cue below instead.
        bool stateChanged = !hasCapturedAuthorityBaseState
            || baseState.fullPathHash != lastCapturedAuthorityBaseStateHash;

        if (stateChanged)
            NetworkOwnerBaseStateSequence++;

        NetworkOwnerBaseStateHash = baseState.fullPathHash;
        NetworkOwnerBaseStateNormalizedTime = baseStateNormalizedTime;
        lastCapturedAuthorityBaseStateHash = baseState.fullPathHash;
        hasCapturedAuthorityBaseState = true;
    }

    private void ApplyOwnerLocomotionParameters()
    {
        if (!HasInputAuthority || HasStateAuthority || animator == null)
            return;

        // Use Fusion's render buffers instead of the latest tick value. This keeps
        // the owning client's Blend Trees on the exact same remote render timeline
        // as NetworkTransform, avoiding visible stepping between Host snapshots.
        var interpolator = new NetworkBehaviourBufferInterpolator(this);
        SetOwnerFloat(AnimatorParameters.moveAmount, interpolator.Float(nameof(NetworkMoveAmount)));
        SetOwnerFloat(AnimatorParameters.strafeAmount, interpolator.Float(nameof(NetworkStrafeAmount)));
        SetOwnerFloat(AnimatorParameters.rotation, interpolator.Float(nameof(NetworkRotation)));
        SetOwnerFloat(AnimatorParameters.fallAmount, interpolator.Float(nameof(NetworkFallAmount)));
        SetOwnerFloat(AnimatorParameters.locomotionType, interpolator.Float(nameof(NetworkLocomotionType)));
        SetOwnerFloat(AnimatorParameters.idleType, interpolator.Float(nameof(NetworkIdleType)));
        SetOwnerFloat(AnimatorParameters.crouchType, interpolator.Float(nameof(NetworkCrouchType)));
        SetOwnerFloat(AnimatorParameters.runToStopAmount, interpolator.Float(nameof(NetworkRunToStopAmount)));
        SetOwnerFloat(ClimbXHash, interpolator.Float(nameof(NetworkClimbX)));
        SetOwnerFloat(ClimbYHash, interpolator.Float(nameof(NetworkClimbY)));
        SetOwnerFloat(AnimatorParameters.freeHang, interpolator.Float(nameof(NetworkFreeHang)));
        SetOwnerFloat(AnimatorParameters.jumpBackDirection, interpolator.Float(nameof(NetworkJumpBackDirection)));
        SetOwnerFloat(AnimatorParameters.BackJumpDir, interpolator.Float(nameof(NetworkBackJumpDirection)));
        SetOwnerBool(AnimatorParameters.IsGrounded, interpolator.NetworkBool(nameof(NetworkIsGrounded)));
        SetOwnerBool(AnimatorParameters.turnback_Mirror, interpolator.NetworkBool(nameof(NetworkTurnbackMirror)));
        SetOwnerBool(AnimatorParameters.mirrorAction, interpolator.NetworkBool(nameof(NetworkMirrorAction)));
        SetOwnerBool(AnimatorParameters.isFalling, interpolator.NetworkBool(nameof(NetworkIsFalling)));
        SetOwnerBool(AnimatorParameters.mirrorJump, interpolator.NetworkBool(nameof(NetworkMirrorJump)));
        SetOwnerBool(AnimatorParameters.BackJumpMode, interpolator.NetworkBool(nameof(NetworkBackJumpMode)));

        // AnimGraph owns the final pose while a vault/step/climb clip is active.
        // Crossfading the underlying Animator Controller at the same time creates
        // a second presentation writer and can make the clip appear to skip.
        if (!proxyGraphActionPlaying)
            ApplyOwnerBaseState(interpolator);
    }

    private void ApplyOwnerBaseState(NetworkBehaviourBufferInterpolator interpolator)
    {
        int authorityStateHash = interpolator.Int(nameof(NetworkOwnerBaseStateHash));
        if (authorityStateHash == 0 || animator == null)
            return;

        int authorityStateSequence = interpolator.Int(nameof(NetworkOwnerBaseStateSequence));
        float normalizedTime = Mathf.Repeat(
            interpolator.Float(nameof(NetworkOwnerBaseStateNormalizedTime)), 1f);

        // A reliable Mecanim cue reaches the owning client before the matching
        // interpolated snapshot. Until that snapshot catches up, applying the old
        // base state here would immediately undo the correctly timed cue.
        bool forceApplyBaseState = false;
        if (ownerMecanimCueSequence >= 0)
        {
            int renderedActionSequence = interpolator.Int(nameof(ActionSequence));
            if (renderedActionSequence < ownerMecanimCueSequence)
                return;

            forceApplyBaseState = forceOwnerBaseStateAfterCue;

            // The snapshot is now authoritative again. When it identifies the same
            // action already started by the cue, adopt its sequence without a
            // second CrossFade that would visibly restart the animation.
            AnimatorStateInfo currentState = animator.GetCurrentAnimatorStateInfo(0);
            bool localStateMatchesCue = !string.IsNullOrWhiteSpace(ownerMecanimCueName)
                && currentState.IsName(ownerMecanimCueName);
            if (!localStateMatchesCue && animator.IsInTransition(0))
            {
                AnimatorStateInfo nextState = animator.GetNextAnimatorStateInfo(0);
                localStateMatchesCue = nextState.IsName(ownerMecanimCueName);
            }

            if (localStateMatchesCue)
            {
                // Remember the full-path hash of the one-shot state. The reliable
                // clear can arrive one render snapshot before the authority's
                // Locomotion state. Without this marker that delayed action snapshot
                // is sampled once more, which makes FallingToRoll/Landing visibly
                // bend the character a second time after it already stood up.
                if (ownerMecanimActionActive && ownerMecanimActionStateHash == 0)
                    ownerMecanimActionStateHash = authorityStateHash;

                lastAppliedOwnerBaseStateHash = authorityStateHash;
                lastAppliedOwnerBaseStateSequence = authorityStateSequence;
                ownerMecanimCueSequence = -1;
                ownerMecanimCueName = null;
                forceOwnerBaseStateAfterCue = false;
                return;
            }

            ownerMecanimCueSequence = -1;
            ownerMecanimCueName = null;
            forceOwnerBaseStateAfterCue = false;
        }

        // A clear RPC is reliable and therefore normally arrives ahead of the
        // interpolated Animator snapshot. If that matching snapshot still contains
        // the just-finished one-shot, keep the locally completed pose instead of
        // replaying its final crouched/landing frames. The first different state is
        // the authority's real recovery state and is applied immediately below.
        if (ownerAwaitingMecanimPostClearState)
        {
            if (authorityStateHash == ownerMecanimActionStateHash)
            {
                lastAppliedOwnerBaseStateSequence = authorityStateSequence;
                return;
            }

            ownerAwaitingMecanimPostClearState = false;
            ownerMecanimActionStateHash = 0;
            forceApplyBaseState = true;
        }

        // The authority increments the snapshot sequence whenever it briefly sees a
        // transition. Interpolation can skip that transient hash and expose the same
        // final state with a newer sequence. Re-crossfading that identical hash makes
        // the owning client restart locomotion, hang and climb states every few ticks.
        // Reliable action cues above already cover intentional re-entry into a state.
        if (!forceApplyBaseState && authorityStateHash == lastAppliedOwnerBaseStateHash)
        {
            if (animGraph != null)
                animGraph.SampleNetworkAnimatorState(authorityStateHash, normalizedTime);
            lastAppliedOwnerBaseStateSequence = authorityStateSequence;
            return;
        }

        if (forceApplyBaseState)
        {
            lastAppliedOwnerBaseStateHash = 0;
            lastAppliedOwnerBaseStateSequence = -1;
        }

        // Apply each authoritative hash change exactly once. Explicit reliable
        // cues above cover repeated entries into the same state.
        if (authorityStateSequence == lastAppliedOwnerBaseStateSequence)
            return;

        AnimatorStateInfo current = animator.GetCurrentAnimatorStateInfo(0);
        float transitionDuration = lastAppliedOwnerBaseStateHash == 0 ? 0f : 0.06f;
        bool appliedToPlayable = animGraph != null
            && animGraph.PlayNetworkAnimatorState(authorityStateHash, normalizedTime, transitionDuration);

        // AnimGraph is required by Spawned(), but retain a defensive fallback so a
        // partially restored prefab does not silently lose every owner animation.
        if (!appliedToPlayable)
        {
            if (transitionDuration <= Mathf.Epsilon)
                animator.Play(authorityStateHash, 0, normalizedTime);
            else
                animator.CrossFade(authorityStateHash, transitionDuration, 0, normalizedTime);
        }

        LogDiagnostic($"OWNER STATE apply seq={authorityStateSequence} hash={authorityStateHash} normalized={normalizedTime:0.###} local={current.fullPathHash}");
        lastAppliedOwnerBaseStateHash = authorityStateHash;
        lastAppliedOwnerBaseStateSequence = authorityStateSequence;
    }

    private void SetOwnerFloat(int parameterHash, float value)
    {
        if (animGraph != null)
            animGraph.SetNetworkAnimatorFloat(parameterHash, value);
        else if (animator != null)
            animator.SetFloat(parameterHash, value);
    }

    private void SetOwnerBool(int parameterHash, bool value)
    {
        if (animGraph != null)
            animGraph.SetNetworkAnimatorBool(parameterHash, value);
        else if (animator != null)
            animator.SetBool(parameterHash, value);
    }

    private void UpdateRemoteHangIKState()
    {
        bool isStableHang = parkourController != null
            && parkourController.IsHanging
            && !parkourController.InAction
            && climbController != null
            && climbController.currentPoint != null
            && climbCore != null;

        RemoteHangIKActive = isStableHang;
        if (!isStableHang)
            return;

        Vector3 rightTarget = climbCore.rightHand.currentIkVecPoint;
        Vector3 leftTarget = climbCore.leftHand.currentIkVecPoint;

        // The FS hang setup normally initializes these from the active ClimbPoint.
        // Rebuild them if the network tick arrives just before that initialization.
        if (rightTarget == Vector3.zero)
            rightTarget = climbController.GetHandPos(climbController.currentPoint.transform, AvatarTarget.RightHand);
        if (leftTarget == Vector3.zero)
            leftTarget = climbController.GetHandPos(climbController.currentPoint.transform, AvatarTarget.LeftHand);

        RemoteRightHandTarget = rightTarget;
        RemoteLeftHandTarget = leftTarget;
    }

    /// <summary>
    /// Applies the State Authority's ledge hand targets to a remote Animator IK pass.
    /// The FS controller's current/previous ClimbPoint references are local runtime
    /// objects and are not present on proxies, so their default IK cannot align hands.
    /// </summary>
    public bool ApplyProxyHangIK(Animator targetAnimator)
    {
        if (HasStateAuthority || !RemoteHangIKActive || targetAnimator == null)
            return false;

        targetAnimator.SetIKPosition(AvatarIKGoal.RightHand, RemoteRightHandTarget);
        targetAnimator.SetIKPosition(AvatarIKGoal.LeftHand, RemoteLeftHandTarget);
        targetAnimator.SetIKPositionWeight(AvatarIKGoal.RightHand, 1f);
        targetAnimator.SetIKPositionWeight(AvatarIKGoal.LeftHand, 1f);
        return true;
    }

    private void OnAnimatorIK(int layerIndex)
    {
        // The owning client's ClimbController is disabled so it cannot run a
        // second gameplay simulation. Disabled MonoBehaviours do not receive
        // OnAnimatorIK, therefore this active network presentation component must
        // apply the State Authority's hand targets itself.
        if (!HasStateAuthority)
            ApplyProxyHangIK(animator);
    }

    private void SendActionCue(ActionCue cue)
    {
        ActionSequence++;
        SetNetworkAction(cue);
        LogDiagnostic($"SEND seq={ActionSequence} action='{cue.Name}' graph={cue.UsesAnimGraph} fixed={cue.UsesFixedTransition} speed={cue.AnimationSpeed:0.###} authorityLength={cue.AuthorityClipLength:0.###}");
        RPC_PlayParkourAction(cue.Name ?? string.Empty, cue.UsesAnimGraph,
            cue.UsesFixedTransition, cue.TransitionIn, cue.TransitionOut,
            cue.AnimationSpeed, cue.AuthorityClipLength, ActionSequence);
    }

    private static bool SameCue(ActionCue left, ActionCue right)
    {
        return string.Equals(left.Name, right.Name, StringComparison.Ordinal)
            && left.UsesAnimGraph == right.UsesAnimGraph
            && left.UsesFixedTransition == right.UsesFixedTransition
            && Mathf.Approximately(left.TransitionIn, right.TransitionIn)
            && Mathf.Approximately(left.TransitionOut, right.TransitionOut)
            && Mathf.Approximately(left.AnimationSpeed, right.AnimationSpeed)
            && Mathf.Approximately(left.AuthorityClipLength, right.AuthorityClipLength);
    }

    private void SetNetworkAction(ActionCue cue)
    {
        ActiveActionName = cue.Name ?? string.Empty;
        ActiveActionUsesAnimGraph = cue.UsesAnimGraph;
        ActiveActionUsesFixedTransition = cue.UsesFixedTransition;
        ActiveTransitionIn = cue.TransitionIn;
        ActiveTransitionOut = cue.TransitionOut;
        ActiveAnimationSpeed = cue.AnimationSpeed;
        ActiveAuthorityClipLength = cue.AuthorityClipLength;
    }

    public override void Render()
    {
        ApplyOwnerLocomotionParameters();
        ApplyProxySlidePresentation();

        if (!HasStateAuthority && proxyGraphActionPlaying && animGraph != null)
        {
            var interpolator = new NetworkBehaviourBufferInterpolator(this);
            int renderedActionSequence = interpolator.Int(nameof(ActionSequence));

            // A reliable start RPC can arrive before the matching interpolated
            // snapshot. Sampling the previous action's normalized time into the new
            // clip produces the one-frame pose jumps visible on Vault/Climb. Hold
            // the new action at zero until both channels identify the same cue.
            renderedGraphNormalizedTime = renderedActionSequence == proxyGraphActionSequence
                ? interpolator.Float(nameof(ActiveGraphNormalizedTime))
                : 0f;
            hasRenderedGraphTime = animGraph.SetNetworkPresentationNormalizedTime(
                renderedGraphNormalizedTime, proxyGraphTransitionIn, proxyGraphTransitionOut);
        }

        if (HasStateAuthority || initializedProxyAnimationState)
            return;

        // Networked state is the fallback for a proxy that joins while an action
        // is already in progress. Ongoing transitions are delivered as reliable RPCs.
        initializedProxyAnimationState = true;
        initialStateSequence = ActionSequence;
        LogDiagnostic($"INITIAL STATE seq={initialStateSequence} action='{ActiveActionName}' graph={ActiveActionUsesAnimGraph}");
        if (!string.IsNullOrWhiteSpace(ActiveActionName.ToString()))
        {
            PlayProxyAction(ActiveActionName.ToString(), ActiveActionUsesAnimGraph,
                ActiveActionUsesFixedTransition, ActiveTransitionIn, ActiveTransitionOut,
                ActiveAnimationSpeed, ActiveAuthorityClipLength, ActionSequence);
        }
    }

    private void ApplyProxySlidePresentation()
    {
        if (HasStateAuthority || animGraph == null || slidingController == null)
            return;

        bool shouldSlide = NetworkSlideActive;
        if (shouldSlide == proxySlideActive)
            return;

        proxySlideActive = shouldSlide;
        if (shouldSlide && slidingController.SlidingClip != null)
        {
            animGraph.PlayLoopingAnimation(slidingController.SlidingClip,
                transitionIn: 0.05f, transitionOut: 0.05f);
        }
        else
        {
            animGraph.StopLoopingAnimations(true);
        }
    }

    private void LateUpdate()
    {
        if (HasStateAuthority || !proxyGraphActionPlaying || !hasRenderedGraphTime || animGraph == null)
            return;

        // AnimGraph's coroutine calls ApplyAnimationSpeed during Update and can
        // re-enable the local Playable speed after Fusion's Render callback. Sample
        // once more in LateUpdate so the final rendered pose always uses the Host's
        // interpolated action time rather than the owning client's frame clock.
        animGraph.SetNetworkPresentationNormalizedTime(renderedGraphNormalizedTime,
            proxyGraphTransitionIn, proxyGraphTransitionOut);
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All, Channel = RpcChannel.Reliable)]
    private void RPC_PlayParkourAction(string actionName, bool usesAnimGraph,
        bool usesFixedTransition, float transitionIn, float transitionOut,
        float animationSpeed, float authorityClipLength, int sequence)
    {
        if (HasStateAuthority)
            return;

        initializedProxyAnimationState = true;
        LogDiagnostic($"RECEIVE seq={sequence} action='{actionName}' graph={usesAnimGraph}; initialSeq={initialStateSequence}, lastSeq={lastAppliedRpcSequence}");
        if (sequence == initialStateSequence || sequence <= lastAppliedRpcSequence)
        {
            LogDiagnostic($"SKIP seq={sequence}: duplicate/older than initialized or applied sequence");
            return;
        }

        lastAppliedRpcSequence = sequence;
        PlayProxyAction(actionName, usesAnimGraph, usesFixedTransition,
            transitionIn, transitionOut, animationSpeed, authorityClipLength, sequence);
    }

    private void PlayProxyAction(string actionName, bool usesAnimGraph,
        bool usesFixedTransition, float transitionIn, float transitionOut,
        float animationSpeed, float authorityClipLength, int sequence)
    {
        if (string.IsNullOrWhiteSpace(actionName))
        {
            LogDiagnostic("PLAY clear action");
            bool graphWasPlaying = proxyGraphActionPlaying;
            if (HasInputAuthority)
            {
                bool mecAnimWasPlaying = ownerMecanimActionActive;
                if (mecAnimWasPlaying && ownerMecanimActionStateHash == 0
                    && animator != null && !string.IsNullOrWhiteSpace(ownerMecanimActionName))
                {
                    AnimatorStateInfo localState = animator.GetCurrentAnimatorStateInfo(0);
                    if (localState.IsName(ownerMecanimActionName))
                    {
                        ownerMecanimActionStateHash = localState.fullPathHash;
                    }
                    else if (animator.IsInTransition(0))
                    {
                        AnimatorStateInfo nextState = animator.GetNextAnimatorStateInfo(0);
                        if (nextState.IsName(ownerMecanimActionName))
                            ownerMecanimActionStateHash = nextState.fullPathHash;
                    }
                }

                // The reliable clear arrives ahead of the render buffer. Keep the
                // current pose until the matching interpolated ActionSequence is
                // visible; otherwise an old FallingToRoll/parkour snapshot is applied
                // once more and the owner visibly bends or starts the action twice.
                ownerMecanimCueSequence = sequence;
                ownerMecanimCueName = null;
                forceOwnerBaseStateAfterCue = graphWasPlaying;
                ownerAwaitingMecanimPostClearState = mecAnimWasPlaying
                    && ownerMecanimActionStateHash != 0;
                ownerMecanimActionActive = false;
                ownerMecanimActionName = null;
            }
            proxyGraphActionSequence = -1;
            // The State Authority is the source of truth for an action's lifetime.
            // Previously this clear was ignored, so an AnimGraph coroutine which
            // failed to finish locally could leave the owning client permanently
            // frozen in a Step Up/Vault/Landing pose while every other peer already
            // saw Locomotion. Request the graph's normal transition-out and keep a
            // bounded recovery in case the third-party coroutine does not finish.
            BeginProxyGraphRelease();
            return;
        }

        // NetworkMecanimAnimator owns true proxies, but Fusion does not classify the
        // owning Input Authority object as a proxy. Start the state immediately on
        // that one presentation only; the render-buffer snapshot takes over once it
        // reaches the same reliable cue sequence.
        if (!usesAnimGraph)
        {
            proxyGraphActionSequence = -1;
            BeginProxyGraphRelease();

            if (!HasInputAuthority)
            {
                LogDiagnostic($"SKIP Animator state '{actionName}'; NetworkMecanimAnimator owns true proxy");
                return;
            }

            bool applied = animGraph != null
                && animGraph.PlayNetworkAnimatorState(actionName, 0f,
                    Mathf.Max(0f, transitionIn), usesFixedTransition);
            if (!applied && animator != null)
            {
                if (transitionIn <= Mathf.Epsilon)
                    animator.Play(actionName, 0, 0f);
                else if (usesFixedTransition)
                    animator.CrossFadeInFixedTime(actionName, transitionIn, 0, 0f);
                else
                    animator.CrossFade(actionName, transitionIn, 0, 0f);
            }

            ownerMecanimCueSequence = sequence;
            ownerMecanimCueName = actionName;
            forceOwnerBaseStateAfterCue = false;
            ownerMecanimActionActive = true;
            ownerMecanimActionName = actionName;
            ownerMecanimActionStateHash = 0;
            ownerAwaitingMecanimPostClearState = false;
            LogDiagnostic($"PLAY owner Mecanim state '{actionName}' seq={sequence} transition={transitionIn:0.###}");
            return;
        }

        if (!IsSupportedParkourAction(actionName, usesAnimGraph))
        {
            LogDiagnostic($"SKIP unsupported action '{actionName}' graph={usesAnimGraph}");
            return;
        }

        CancelProxyGraphRecovery();
        StopProxyGraphAction();

        if (usesAnimGraph)
        {
            AnimGraphClipInfo clipInfo = FindClipInfo(actionName);
            if (clipInfo == null)
            {
                LogDiagnostic($"CLIP NOT FOUND '{actionName}' in ParkourController/Resources");
                if (!warnedAboutMissingClip)
                {
                    Debug.LogWarning($"No ParkourAction clip named '{actionName}' was found on this FS Network Player; its remote animation will be skipped.", this);
                    warnedAboutMissingClip = true;
                }
                return;
            }

            // Match the State Authority's real action duration. The client can
            // otherwise resolve a same-named imported clip with a different local
            // duration and finish Climb Up/Vault roughly twice as early.
            float durationScale = authorityClipLength > 0.001f
                ? clipInfo.length / authorityClipLength
                : 1f;
            float synchronizedSpeed = Mathf.Max(0.01f, animationSpeed * durationScale);

            var parameters = new AnimParameters
            {
                transitionIn = Mathf.Max(0f, transitionIn),
                transitionOut = Mathf.Max(0f, transitionOut),
                animationSpeed = synchronizedSpeed,
                // Keep the action mixer connected after the local coroutine reaches
                // its end. The State Authority's clear cue releases the graph; until
                // then Render() samples the Host's normalized action time.
                transitionBack = false,
                ignoreActionReplacement = true,
                onAnimationUpdate = null,
                OnComplete = null
            };
            int generation = ++proxyGraphGeneration;
            proxyGraphActionPlaying = true;
            proxyGraphActionSequence = sequence;
            proxyGraphTransitionIn = parameters.transitionIn;
            proxyGraphTransitionOut = parameters.transitionOut;
            LogDiagnostic($"PLAY AnimGraph clip='{clipInfo.clip.name}' generation={generation} transitionIn={parameters.transitionIn:0.###} speed={parameters.animationSpeed:0.###} localLength={clipInfo.length:0.###} authorityLength={authorityClipLength:0.###}");
            proxyGraphCoroutine = StartCoroutine(PlayProxyGraphAction(clipInfo, parameters, generation));

            // Do not let the local coroutine advance before the render buffer has
            // reached this exact action sequence.
            renderedGraphNormalizedTime = 0f;
            hasRenderedGraphTime = animGraph.SetNetworkPresentationNormalizedTime(
                0f, proxyGraphTransitionIn, proxyGraphTransitionOut);
        }
    }

    private void OnActionStarted(CharacterAction action)
    {
        if (!HasStateAuthority || action == null)
            return;

        string actionName = action.isAnimGraphAction
            ? action.clipInfo?.clip?.name
            : action.animName;

        LogDiagnostic($"ACTION START name='{actionName}' graph={action.isAnimGraphAction} current='{actionExecutor.CurrentAction?.clipInfo?.clip?.name ?? actionExecutor.CurrentAction?.animName}'");

        // Normal Animator Controller actions still need a reliable start cue for
        // the owning Input Authority object. True proxies ignore this cue and remain
        // exclusively driven by NetworkMecanimAnimator.
        if (!action.isAnimGraphAction)
        {
            if (string.IsNullOrWhiteSpace(actionName))
                return;

            pendingActionName = actionName;
            pendingUsesAnimGraph = false;
            pendingUsesFixedTransition = action.useFixedTransition;
            pendingTransitionIn = action.animationParameters?.transitionIn ?? 0.2f;
            pendingTransitionOut = action.animationParameters?.transitionOut ?? 0.2f;
            pendingAnimationSpeed = action.animationParameters?.animationSpeed ?? 1f;
            pendingAuthorityClipLength = 0f;
            hasPendingAction = true;
            actionCueQueue.Enqueue(new ActionCue
            {
                Name = pendingActionName,
                UsesAnimGraph = false,
                UsesFixedTransition = pendingUsesFixedTransition,
                TransitionIn = pendingTransitionIn,
                TransitionOut = pendingTransitionOut,
                AnimationSpeed = pendingAnimationSpeed,
                AuthorityClipLength = 0f
            });

            LogDiagnostic($"MECANIM action '{actionName}' queued for owner; true proxies use NetworkMecanimAnimator");
            return;
        }

        if (!IsSupportedParkourAction(actionName, action.isAnimGraphAction))
        {
            bool shouldClear = !string.IsNullOrEmpty(pendingActionName)
                || actionCueQueue.Count > 0
                || !string.IsNullOrEmpty(ActiveActionName.ToString());
            if (shouldClear)
                QueueClearAction();
            return;
        }

        pendingActionName = actionName;
        pendingUsesAnimGraph = action.isAnimGraphAction;
        pendingUsesFixedTransition = action.useFixedTransition;
        pendingTransitionIn = action.animationParameters?.transitionIn ?? 0.2f;
        pendingTransitionOut = action.animationParameters?.transitionOut ?? 0.2f;
        pendingAnimationSpeed = action.animationParameters?.animationSpeed ?? 1f;
        pendingAuthorityClipLength = Mathf.Max(0f, action.clipInfo?.length ?? 0f);
        ActiveGraphNormalizedTime = 0f;
        hasPendingAction = true;

        if (!string.IsNullOrEmpty(pendingActionName))
        {
            actionCueQueue.Enqueue(new ActionCue
            {
                Name = pendingActionName,
                UsesAnimGraph = pendingUsesAnimGraph,
                UsesFixedTransition = pendingUsesFixedTransition,
                TransitionIn = pendingTransitionIn,
                TransitionOut = pendingTransitionOut,
                AnimationSpeed = pendingAnimationSpeed,
                AuthorityClipLength = pendingAuthorityClipLength
            });
        }
    }

    private void OnActionEnded(CharacterAction action)
    {
        if (!HasStateAuthority || actionExecutor == null)
            return;

        LogDiagnostic($"ACTION END name='{action?.clipInfo?.clip?.name ?? action?.animName}' current='{actionExecutor.CurrentAction?.clipInfo?.clip?.name ?? actionExecutor.CurrentAction?.animName ?? "<none>"}'");

        if (actionExecutor.CurrentAction != null)
            return;

        if (string.IsNullOrEmpty(pendingActionName)
            && actionCueQueue.Count == 0
            && string.IsNullOrEmpty(ActiveActionName.ToString()))
            return;

        QueueClearAction();
    }

    private void QueueClearAction()
    {
        LogDiagnostic("QUEUE CLEAR action");
        pendingActionName = string.Empty;
        pendingUsesAnimGraph = false;
        pendingUsesFixedTransition = false;
        pendingTransitionIn = 0f;
        pendingTransitionOut = 0f;
        pendingAnimationSpeed = 1f;
        pendingAuthorityClipLength = 0f;
        hasPendingAction = true;
    }

    private bool IsSupportedParkourAction(string actionName, bool usesAnimGraph)
    {
        if (string.IsNullOrWhiteSpace(actionName))
            return false;

        // Normal Mecanim states are intentionally excluded. This relay has one
        // responsibility only: clips owned by FS AnimGraph/PlayableGraph.
        return usesAnimGraph && FindClipInfo(actionName) != null;
    }

    private AnimGraphClipInfo FindClipInfo(string clipName)
    {
        if (clipInfoCache.TryGetValue(clipName, out AnimGraphClipInfo cached))
            return cached;

        if (parkourController != null && parkourController.parkourActions != null)
        {
            foreach (ParkourAction action in parkourController.parkourActions)
            {
                if (action?.ClipInfo?.clip != null && action.ClipInfo.clip.name == clipName)
                    return CacheClipInfo(clipName, action.ClipInfo);
            }

            ParkourAction wallRunAction = parkourController.wallRunAction;
            if (wallRunAction?.ClipInfo?.clip != null && wallRunAction.ClipInfo.clip.name == clipName)
                return CacheClipInfo(clipName, wallRunAction.ClipInfo);
        }

        if (slidingController?.SlideJumpClipInfo?.clip != null
            && slidingController.SlideJumpClipInfo.clip.name == clipName)
        {
            return CacheClipInfo(clipName, slidingController.SlideJumpClipInfo);
        }

        // Some FS prefabs keep an action in Resources without including it in the
        // ParkourController list. Resolving the catalog locally keeps IDs stable.
        ParkourAction[] resourceActions = Resources.LoadAll<ParkourAction>("Parkour Actions");
        foreach (ParkourAction action in resourceActions)
        {
            if (action?.ClipInfo?.clip != null && action.ClipInfo.clip.name == clipName)
                return CacheClipInfo(clipName, action.ClipInfo);
        }

        return null;
    }

    private AnimGraphClipInfo CacheClipInfo(string clipName, AnimGraphClipInfo clipInfo)
    {
        if (!string.IsNullOrWhiteSpace(clipName) && clipInfo != null)
            clipInfoCache[clipName] = clipInfo;
        return clipInfo;
    }

    private IEnumerator PlayProxyGraphAction(AnimGraphClipInfo clipInfo, AnimParameters parameters, int generation)
    {
        yield return animGraph.CrossfadeAsync(clipInfo, parameters);
        LogDiagnostic($"AnimGraph coroutine completed clip='{clipInfo?.clip?.name}' generation={generation} currentGeneration={proxyGraphGeneration}");
        if (generation == proxyGraphGeneration)
        {
            proxyGraphCoroutine = null;
            LogDiagnostic($"HOLD AnimGraph clip='{clipInfo?.clip?.name}' until authoritative clear");
        }
    }

    private void BeginProxyGraphRelease()
    {
        if (!proxyGraphActionPlaying || animGraph == null)
            return;

        ResetProxyGraphPresentation("authoritative clear");
    }

    private IEnumerator RecoverProxyGraphIfStuck(int generation)
    {
        float deadline = Time.unscaledTime + ProxyGraphRecoveryTimeout;
        while (generation == proxyGraphGeneration
            && proxyGraphActionPlaying
            && Time.unscaledTime < deadline)
        {
            yield return null;
        }

        proxyGraphRecoveryCoroutine = null;
        if (generation != proxyGraphGeneration || !proxyGraphActionPlaying || animGraph == null)
            yield break;

        LogDiagnostic($"RECOVER stuck AnimGraph generation={generation}; rebuilding local presentation graph");

        // Stop our wrapper before destroying its Playables. The rebuilt graph starts
        // from the Animator Controller, which NetworkMecanimAnimator and the
        // interpolated parameters already keep on the authority's render timeline.
        if (proxyGraphCoroutine != null)
        {
            StopCoroutine(proxyGraphCoroutine);
            proxyGraphCoroutine = null;
        }

        proxyGraphGeneration++;
        proxyGraphActionPlaying = false;
        proxyGraphActionSequence = -1;
        hasRenderedGraphTime = false;
        proxySlideActive = false;
        lastAppliedOwnerBaseStateHash = 0;
        lastAppliedOwnerBaseStateSequence = -1;
        animGraph.ResetAnimationGraph();
    }

    private void CancelProxyGraphRecovery()
    {
        if (proxyGraphRecoveryCoroutine == null)
            return;

        StopCoroutine(proxyGraphRecoveryCoroutine);
        proxyGraphRecoveryCoroutine = null;
    }

    private void StopProxyGraphAction()
    {
        if (!proxyGraphActionPlaying)
            return;

        ResetProxyGraphPresentation("replacement action");
    }

    private void ResetProxyGraphPresentation(string reason)
    {
        CancelProxyGraphRecovery();
        proxyGraphGeneration++;
        LogDiagnostic($"RESET AnimGraph reason='{reason}' generation={proxyGraphGeneration}");

        if (proxyGraphCoroutine != null)
        {
            StopCoroutine(proxyGraphCoroutine);
            proxyGraphCoroutine = null;
        }

        proxyGraphActionPlaying = false;
        proxyGraphActionSequence = -1;
        hasRenderedGraphTime = false;

        if (animGraph != null)
            animGraph.ResetAnimationGraph();
    }

    private void OnDestroy()
    {
        CancelProxyGraphRecovery();

        if (actionExecutor != null)
        {
            actionExecutor.OnActionStarted -= OnActionStarted;
            actionExecutor.OnActionEnded -= OnActionEnded;
        }
    }

    private void LogDiagnostic(string message)
    {
        if (!logDiagnostics)
            return;

        string role = HasStateAuthority ? "STATE" : HasInputAuthority ? "INPUT" : "PROXY";
        string tick = Runner != null ? Runner.Tick.ToString() : "no-runner";
        string objectId = Object != null ? Object.Id.ToString() : "no-object";
        Debug.Log($"[FS Parkour Sync][{role}][object={objectId}][tick={tick}][frame={Time.frameCount}] {message}", this);
    }
}
