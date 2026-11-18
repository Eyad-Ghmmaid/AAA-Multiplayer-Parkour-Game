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
    [Networked] private NetworkBool RemoteHangIKStable { get; set; }
    [Networked] private Vector3 RemoteRightHandTarget { get; set; }
    [Networked] private Vector3 RemoteLeftHandTarget { get; set; }
    [Networked] private float RemoteRightHandIKWeight { get; set; }
    [Networked] private float RemoteLeftHandIKWeight { get; set; }
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
    [Networked] private int NetworkOwnerCurrentStateHash { get; set; }
    [Networked] private float NetworkOwnerCurrentStateNormalizedTime { get; set; }
    [Networked] private int NetworkOwnerNextStateHash { get; set; }
    [Networked] private float NetworkOwnerNextStateNormalizedTime { get; set; }
    [Networked] private NetworkBool NetworkOwnerIsInTransition { get; set; }
    [Networked] private float NetworkOwnerTransitionDuration { get; set; }
    [Networked] private float NetworkOwnerTransitionNormalizedTime { get; set; }
    [Networked] private int NetworkOwnerTransitionDurationUnit { get; set; }

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
    private int lastCapturedAuthorityCurrentStateHash;
    private int lastCapturedAuthorityNextStateHash;
    private bool lastCapturedAuthorityWasInTransition;
    private bool hasCapturedAuthorityBaseState;
    private int ownerMecanimCueSequence = -1;
    private string ownerMecanimCueName;
    private float ownerMecanimCueTransitionIn;
    private bool ownerMecanimCueUsesFixedTransition;
    private bool ownerMecanimCueWaitingForAnimatorSnapshot;
    private int ownerMecanimCueBaselineBaseStateSequence = int.MinValue;
    private int lastObservedOwnerBaseStateSequence = -1;
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
    private bool proxyGraphReleasePending;
    private bool proxyGraphReleaseInProgress;
    private int proxyGraphReleaseSequence = -1;
    private Coroutine proxyGraphReleaseCoroutine;
    private bool proxySlideActive;
    private float proxyRightHandIKWeight;
    private float proxyLeftHandIKWeight;
    private Vector3 proxyRightHandTarget;
    private Vector3 proxyLeftHandTarget;
    private bool proxyHangTargetsInitialized;
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

        // NetworkMecanimAnimator does not render an Input Authority object. Capture
        // the complete Base Layer transition instead of reducing it to only the
        // destination state. Without source, progress, duration and duration unit,
        // the owner had to invent a crossfade and could never match the Host.
        AnimatorStateInfo currentState = animator.GetCurrentAnimatorStateInfo(0);
        bool isInTransition = animator.IsInTransition(0);
        AnimatorStateInfo nextState = isInTransition
            ? animator.GetNextAnimatorStateInfo(0)
            : default;
        AnimatorTransitionInfo transitionInfo = isInTransition
            ? animator.GetAnimatorTransitionInfo(0)
            : default;

        int nextStateHash = isInTransition ? nextState.fullPathHash : 0;
        AnimatorStateInfo presentationState = nextStateHash != 0 ? nextState : currentState;

        NetworkOwnerCurrentStateHash = currentState.fullPathHash;
        NetworkOwnerCurrentStateNormalizedTime = currentState.normalizedTime;
        NetworkOwnerNextStateHash = nextStateHash;
        NetworkOwnerNextStateNormalizedTime = nextStateHash != 0 ? nextState.normalizedTime : 0f;
        NetworkOwnerIsInTransition = isInTransition && nextStateHash != 0;
        NetworkOwnerTransitionDuration = isInTransition ? transitionInfo.duration : 0f;
        NetworkOwnerTransitionNormalizedTime = isInTransition ? transitionInfo.normalizedTime : 0f;
        NetworkOwnerTransitionDurationUnit = isInTransition ? (int)transitionInfo.durationUnit : 0;

        // A transition start and its completion are separate presentation events.
        // Increment only when that signature changes, never when loop time wraps.
        bool stateChanged = !hasCapturedAuthorityBaseState
            || currentState.fullPathHash != lastCapturedAuthorityCurrentStateHash
            || nextStateHash != lastCapturedAuthorityNextStateHash
            || isInTransition != lastCapturedAuthorityWasInTransition;

        if (stateChanged)
            NetworkOwnerBaseStateSequence++;

        NetworkOwnerBaseStateHash = presentationState.fullPathHash;
        NetworkOwnerBaseStateNormalizedTime = presentationState.normalizedTime;
        lastCapturedAuthorityCurrentStateHash = currentState.fullPathHash;
        lastCapturedAuthorityNextStateHash = nextStateHash;
        lastCapturedAuthorityWasInTransition = isInTransition;
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
        if (!proxyGraphActionPlaying || proxyGraphReleasePending || proxyGraphReleaseInProgress)
            ApplyOwnerBaseState(interpolator);

        // Keep observing the render timeline even while an AnimGraph clip owns the
        // final pose. A following Mecanim cue uses this value to distinguish its
        // first real Animator snapshot from the pre-action snapshot captured in the
        // same simulation tick as ActionSequence.
        lastObservedOwnerBaseStateSequence = interpolator.Int(
            nameof(NetworkOwnerBaseStateSequence));
    }

    private void ApplyOwnerBaseState(NetworkBehaviourBufferInterpolator interpolator)
    {
        int authorityStateHash = interpolator.Int(nameof(NetworkOwnerBaseStateHash));
        if (authorityStateHash == 0 || animator == null)
            return;

        int authorityStateSequence = interpolator.Int(nameof(NetworkOwnerBaseStateSequence));
        float normalizedTime = Mathf.Repeat(
            interpolator.Float(nameof(NetworkOwnerBaseStateNormalizedTime)), 1f);
        bool authorityIsInTransition = interpolator.NetworkBool(
            nameof(NetworkOwnerIsInTransition));
        int authorityCurrentStateHash = interpolator.Int(
            nameof(NetworkOwnerCurrentStateHash));
        float authorityCurrentNormalizedTime = interpolator.Float(
            nameof(NetworkOwnerCurrentStateNormalizedTime));
        int authorityNextStateHash = interpolator.Int(
            nameof(NetworkOwnerNextStateHash));
        float authorityNextNormalizedTime = interpolator.Float(
            nameof(NetworkOwnerNextStateNormalizedTime));
        float authorityTransitionDuration = interpolator.Float(
            nameof(NetworkOwnerTransitionDuration));
        float authorityTransitionNormalizedTime = interpolator.Float(
            nameof(NetworkOwnerTransitionNormalizedTime));
        DurationUnit authorityTransitionDurationUnit = (DurationUnit)interpolator.Int(
            nameof(NetworkOwnerTransitionDurationUnit));

        // Reliable RPCs arrive ahead of Fusion's interpolated render timeline.
        // Treat the Mecanim cue only as a sequence fence: starting the animation
        // from time zero in the RPC made the owner's pose lead its NetworkTransform
        // by several ticks. Hang, swing and ladder chains then appeared delayed or
        // snapped when the matching snapshot finally arrived.
        bool forceApplyBaseState = false;
        string queuedMecanimStateName = null;
        float queuedMecanimTransitionIn = 0f;
        bool queuedMecanimUsesFixedTransition = false;
        if (ownerMecanimCueSequence >= 0)
        {
            int renderedActionSequence = interpolator.Int(nameof(ActionSequence));
            if (renderedActionSequence < ownerMecanimCueSequence)
                return;

            bool startsMecanimAction = !string.IsNullOrWhiteSpace(ownerMecanimCueName);
            forceApplyBaseState = forceOwnerBaseStateAfterCue || startsMecanimAction;
            if (startsMecanimAction)
            {
                // ActionExecutor raises OnActionStarted immediately before calling
                // Animator.CrossFade. If FixedUpdateNetwork captures that frame, the
                // ActionSequence already changed while the Animator snapshot still
                // contains the previous hang state. Wait for the first changed
                // Animator signature instead of applying the old state's phase to
                // the new action name (the crouched/embedded poses in the client).
                if (ownerMecanimCueBaselineBaseStateSequence == int.MinValue)
                    ownerMecanimCueBaselineBaseStateSequence = lastObservedOwnerBaseStateSequence;

                bool animatorSnapshotStillPreAction = !authorityIsInTransition
                    && ownerMecanimCueBaselineBaseStateSequence >= 0
                    && authorityStateSequence == ownerMecanimCueBaselineBaseStateSequence;
                if (animatorSnapshotStillPreAction)
                {
                    if (!ownerMecanimCueWaitingForAnimatorSnapshot)
                    {
                        LogDiagnostic($"HOLD owner Mecanim state '{ownerMecanimCueName}' seq={ownerMecanimCueSequence}; render snapshot still has pre-action Animator state");
                        ownerMecanimCueWaitingForAnimatorSnapshot = true;
                    }
                    return;
                }

                queuedMecanimStateName = ownerMecanimCueName;
                queuedMecanimTransitionIn = ownerMecanimCueTransitionIn;
                queuedMecanimUsesFixedTransition = ownerMecanimCueUsesFixedTransition;
            }

            // The first snapshot carrying the cue supplies both the authoritative
            // state and its already elapsed phase. Apply that state once below. This
            // keeps presentation and root position on one timeline instead of
            // starting at normalized time zero when the RPC happens to arrive.
            if (startsMecanimAction && ownerMecanimActionActive)
                ownerMecanimActionStateHash = authorityStateHash;

            ownerMecanimCueSequence = -1;
            ownerMecanimCueName = null;
            ownerMecanimCueTransitionIn = 0f;
            ownerMecanimCueUsesFixedTransition = false;
            ownerMecanimCueWaitingForAnimatorSnapshot = false;
            ownerMecanimCueBaselineBaseStateSequence = int.MinValue;
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

        // Rebuild the Host's actual transition once per transition signature. The
        // old implementation discarded this data and replaced every controller
        // transition with 0.12/0.18 seconds, so improving one action necessarily
        // changed another action whose native transition used different timing.
        if (authorityIsInTransition
            && authorityCurrentStateHash != 0
            && authorityNextStateHash == authorityStateHash)
        {
            if (!forceApplyBaseState && animator.IsInTransition(0)
                && animator.GetNextAnimatorStateInfo(0).fullPathHash == authorityNextStateHash)
            {
                lastAppliedOwnerBaseStateHash = authorityStateHash;
                lastAppliedOwnerBaseStateSequence = authorityStateSequence;
                return;
            }

            if (!forceApplyBaseState
                && authorityStateSequence == lastAppliedOwnerBaseStateSequence)
                return;

            bool appliedTransition = animGraph != null
                && animGraph.PlayNetworkAnimatorTransition(
                    authorityCurrentStateHash,
                    Mathf.Repeat(authorityCurrentNormalizedTime, 1f),
                    authorityNextStateHash,
                    Mathf.Repeat(authorityNextNormalizedTime, 1f),
                    authorityTransitionDuration, authorityTransitionNormalizedTime,
                    authorityTransitionDurationUnit);

            if (!appliedTransition)
            {
                animator.Play(authorityCurrentStateHash, 0,
                    Mathf.Repeat(authorityCurrentNormalizedTime, 1f));
                if (authorityTransitionDurationUnit == DurationUnit.Fixed)
                {
                    animator.CrossFadeInFixedTime(authorityNextStateHash,
                        Mathf.Max(0f, authorityTransitionDuration), 0,
                        Mathf.Repeat(authorityNextNormalizedTime, 1f),
                        Mathf.Clamp01(authorityTransitionNormalizedTime));
                }
                else
                {
                    animator.CrossFade(authorityNextStateHash,
                        Mathf.Max(0f, authorityTransitionDuration), 0,
                        Mathf.Repeat(authorityNextNormalizedTime, 1f),
                        Mathf.Clamp01(authorityTransitionNormalizedTime));
                }
            }

            LogDiagnostic($"OWNER TRANSITION apply seq={authorityStateSequence} from={authorityCurrentStateHash} to={authorityNextStateHash} progress={authorityTransitionNormalizedTime:0.###} duration={authorityTransitionDuration:0.###} unit={authorityTransitionDurationUnit}");
            lastAppliedOwnerBaseStateHash = authorityStateHash;
            lastAppliedOwnerBaseStateSequence = authorityStateSequence;
            return;
        }

        // ClimbPoint actions frequently chain through controller transitions such
        // as Predictive -> HangIdles -> BracedHangClimb. The local controller has
        // the same transition and may already be blending to the authoritative
        // state. Starting another CrossFade here restarts that blend and produces
        // the short stop/pop seen only by the owning client. Adopt the transition
        // instead; once it completes, the normal same-state branch below resumes
        // authoritative time sampling.
        if (!forceApplyBaseState && animator.IsInTransition(0))
        {
            AnimatorStateInfo localNextState = animator.GetNextAnimatorStateInfo(0);
            if (localNextState.fullPathHash == authorityStateHash)
            {
                lastAppliedOwnerBaseStateHash = authorityStateHash;
                lastAppliedOwnerBaseStateSequence = authorityStateSequence;
                return;
            }
        }

        // Once the authoritative state is active, let the local Animator advance it
        // continuously. Replaying the same normalized snapshot with Animator.Play
        // every Render frame freezes locomotion whenever the render buffer pauses or
        // a Blend Tree reports an unreliable loop flag. State changes and reliable
        // action cues still perform explicit synchronization above and below.
        if (!forceApplyBaseState && authorityStateHash == lastAppliedOwnerBaseStateHash)
        {
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
        // Match the visible softness of the FS controller. 60 ms was shorter than
        // a few rendered frames at common client frame rates and made Balance,
        // HangIdles and Climb Up look like hard pose switches. Action exits get a
        // little more time because their root-motion pose is farther from idle.
        bool hasQueuedMecanimState = !string.IsNullOrWhiteSpace(queuedMecanimStateName);
        float transitionDuration = hasQueuedMecanimState
            ? Mathf.Max(0f, queuedMecanimTransitionIn)
            : (lastAppliedOwnerBaseStateHash == 0
                ? 0f
                : (forceApplyBaseState ? 0.18f : 0.12f));
        bool appliedToPlayable = animGraph != null
            && (hasQueuedMecanimState
                ? animGraph.PlayNetworkAnimatorState(queuedMecanimStateName,
                    normalizedTime, transitionDuration, queuedMecanimUsesFixedTransition)
                : animGraph.PlayNetworkAnimatorState(authorityStateHash,
                    normalizedTime, transitionDuration));

        // AnimGraph is required by Spawned(), but retain a defensive fallback so a
        // partially restored prefab does not silently lose every owner animation.
        if (!appliedToPlayable)
        {
            if (transitionDuration <= Mathf.Epsilon)
            {
                if (hasQueuedMecanimState)
                    animator.Play(queuedMecanimStateName, 0, normalizedTime);
                else
                    animator.Play(authorityStateHash, 0, normalizedTime);
            }
            else if (hasQueuedMecanimState && queuedMecanimUsesFixedTransition)
            {
                animator.CrossFadeInFixedTime(queuedMecanimStateName,
                    transitionDuration, 0, normalizedTime);
            }
            else if (hasQueuedMecanimState)
            {
                animator.CrossFade(queuedMecanimStateName,
                    transitionDuration, 0, normalizedTime);
            }
            else
            {
                animator.CrossFade(authorityStateHash, transitionDuration, 0, normalizedTime);
            }
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
        bool hasHangTargets = parkourController != null
            && parkourController.IsHanging
            && climbController != null
            && climbController.currentPoint != null
            && climbCore != null
            && climbCore.HasAppliedHandIK;

        RemoteHangIKActive = hasHangTargets;
        RemoteHangIKStable = hasHangTargets && !parkourController.InAction;
        if (!hasHangTargets)
        {
            RemoteRightHandIKWeight = 0f;
            RemoteLeftHandIKWeight = 0f;
            return;
        }

        Vector3 rightTarget = climbCore.AppliedRightHandIKPosition;
        Vector3 leftTarget = climbCore.AppliedLeftHandIKPosition;

        // The FS hang setup normally initializes these from the active ClimbPoint.
        // Rebuild them if the network tick arrives just before that initialization.
        if (rightTarget == Vector3.zero)
            rightTarget = climbController.GetHandPos(climbController.currentPoint.transform, AvatarTarget.RightHand);
        if (leftTarget == Vector3.zero)
            leftTarget = climbController.GetHandPos(climbController.currentPoint.transform, AvatarTarget.LeftHand);

        // Send targets in player-local space. Reconstructing them from the owning
        // client's interpolated NetworkTransform keeps the hands and the rendered
        // body on exactly the same timeline during catches and root-motion climbs.
        RemoteRightHandTarget = transform.InverseTransformPoint(rightTarget);
        RemoteLeftHandTarget = transform.InverseTransformPoint(leftTarget);
        RemoteRightHandIKWeight = climbCore.AppliedRightHandIKWeight;
        RemoteLeftHandIKWeight = climbCore.AppliedLeftHandIKWeight;
    }

    /// <summary>
    /// Applies the State Authority's ledge hand targets to a remote Animator IK pass.
    /// The FS controller's current/previous ClimbPoint references are local runtime
    /// objects and are not present on proxies, so their default IK cannot align hands.
    /// </summary>
    public bool ApplyProxyHangIK(Animator targetAnimator)
    {
        if (HasStateAuthority || targetAnimator == null)
            return false;

        const float targetFollowSharpness = 24f;

        var interpolator = new NetworkBehaviourBufferInterpolator(this);
        bool remoteHangIKActive = interpolator.NetworkBool(nameof(RemoteHangIKActive));
        Vector3 rightHandLocalTarget = interpolator.Vector3(nameof(RemoteRightHandTarget));
        Vector3 leftHandLocalTarget = interpolator.Vector3(nameof(RemoteLeftHandTarget));
        float renderedRightHandWeight = interpolator.Float(nameof(RemoteRightHandIKWeight));
        float renderedLeftHandWeight = interpolator.Float(nameof(RemoteLeftHandIKWeight));
        Vector3 renderedRightHandTarget = transform.TransformPoint(rightHandLocalTarget);
        Vector3 renderedLeftHandTarget = transform.TransformPoint(leftHandLocalTarget);

        if (remoteHangIKActive)
        {
            if (!proxyHangTargetsInitialized)
            {
                proxyRightHandTarget = renderedRightHandTarget;
                proxyLeftHandTarget = renderedLeftHandTarget;
                proxyHangTargetsInitialized = true;
            }
            else
            {
                float follow = 1f - Mathf.Exp(-targetFollowSharpness * Time.unscaledDeltaTime);
                proxyRightHandTarget = Vector3.Lerp(proxyRightHandTarget,
                    renderedRightHandTarget, follow);
                proxyLeftHandTarget = Vector3.Lerp(proxyLeftHandTarget,
                    renderedLeftHandTarget, follow);
            }
        }

        float rightTargetWeight = 0f;
        float leftTargetWeight = 0f;
        if (remoteHangIKActive && proxyHangTargetsInitialized)
        {
            // Match the values actually submitted by the State Authority. Deriving
            // them again from the owner's interpolated hand distance made Swing
            // alternate between animation pose and bar contact across adjacent
            // frames even though the Host maintained one continuous grip.
            rightTargetWeight = Mathf.Clamp01(renderedRightHandWeight);
            leftTargetWeight = Mathf.Clamp01(renderedLeftHandWeight);
        }

        // The NetworkBehaviour interpolator already provides the smooth render-time
        // weight. A second local MoveTowards introduced an avoidable phase lag.
        proxyRightHandIKWeight = rightTargetWeight;
        proxyLeftHandIKWeight = leftTargetWeight;

        if ((proxyRightHandIKWeight <= 0.0001f && proxyLeftHandIKWeight <= 0.0001f)
            || !proxyHangTargetsInitialized)
        {
            if (!remoteHangIKActive)
                proxyHangTargetsInitialized = false;
            return false;
        }

        targetAnimator.SetIKPosition(AvatarIKGoal.RightHand, proxyRightHandTarget);
        targetAnimator.SetIKPosition(AvatarIKGoal.LeftHand, proxyLeftHandTarget);
        targetAnimator.SetIKPositionWeight(AvatarIKGoal.RightHand, proxyRightHandIKWeight);
        targetAnimator.SetIKPositionWeight(AvatarIKGoal.LeftHand, proxyLeftHandIKWeight);
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

        if (!HasStateAuthority && proxyGraphActionPlaying
            && !proxyGraphReleaseInProgress && animGraph != null)
        {
            var interpolator = new NetworkBehaviourBufferInterpolator(this);
            int renderedActionSequence = interpolator.Int(nameof(ActionSequence));

            if (proxyGraphReleasePending
                && renderedActionSequence >= proxyGraphReleaseSequence)
            {
                // Do not sample normalized time zero from the clear snapshot into
                // the finished clip. Preserve its last pose and blend that pose out.
                StartProxyGraphBlendOut();
                return;
            }

            // A reliable start RPC can arrive before the matching interpolated
            // snapshot. Sampling the previous action's normalized time into the new
            // clip produces the one-frame pose jumps visible on Vault/Climb. Hold
            // the new action at zero until both channels identify the same cue.
            renderedGraphNormalizedTime = renderedActionSequence == proxyGraphActionSequence
                ? interpolator.Float(nameof(ActiveGraphNormalizedTime))
                : 0f;
            hasRenderedGraphTime = animGraph.SetNetworkPresentationNormalizedTime(
                renderedGraphNormalizedTime, proxyGraphTransitionIn,
                proxyGraphTransitionOut, transitionBack: proxyGraphReleasePending);
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
        if (HasStateAuthority || !proxyGraphActionPlaying || proxyGraphReleaseInProgress
            || !hasRenderedGraphTime || animGraph == null)
            return;

        // AnimGraph's coroutine calls ApplyAnimationSpeed during Update and can
        // re-enable the local Playable speed after Fusion's Render callback. Sample
        // once more in LateUpdate so the final rendered pose always uses the Host's
        // interpolated action time rather than the owning client's frame clock.
        // Preserve the same transition-out decision made in Render(). Previously
        // this LateUpdate sample used transitionBack=false (the default), which
        // restored the action mixer to full weight after Render had already begun
        // blending it out. The owning client therefore held the final parkour pose
        // and then snapped into Locomotion/Idle, while the Host stayed smooth.
        animGraph.SetNetworkPresentationNormalizedTime(renderedGraphNormalizedTime,
            proxyGraphTransitionIn, proxyGraphTransitionOut,
            transitionBack: proxyGraphReleasePending);
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
                ownerMecanimCueTransitionIn = 0f;
                ownerMecanimCueUsesFixedTransition = false;
                ownerMecanimCueWaitingForAnimatorSnapshot = false;
                ownerMecanimCueBaselineBaseStateSequence = int.MinValue;
                forceOwnerBaseStateAfterCue = graphWasPlaying;
                ownerAwaitingMecanimPostClearState = mecAnimWasPlaying
                    && ownerMecanimActionStateHash != 0;
                ownerMecanimActionActive = false;
                ownerMecanimActionName = null;
            }
            // The State Authority is the source of truth for an action's lifetime.
            // Previously this clear was ignored, so an AnimGraph coroutine which
            // failed to finish locally could leave the owning client permanently
            // frozen in a Step Up/Vault/Landing pose while every other peer already
            // saw Locomotion. Request the graph's normal transition-out and keep a
            // bounded recovery in case the third-party coroutine does not finish.
            BeginProxyGraphRelease(sequence);
            return;
        }

        // NetworkMecanimAnimator owns true proxies, but Fusion does not classify the
        // owning Input Authority object as a proxy. The reliable RPC is deliberately
        // only a sequence fence for that owner: its interpolated snapshot starts the
        // state at the correct phase together with the rendered NetworkTransform.
        if (!usesAnimGraph)
        {
            bool graphWasPlaying = proxyGraphActionPlaying;

            if (!HasInputAuthority)
            {
                if (graphWasPlaying)
                    ResetProxyGraphPresentation("true proxy Animator handoff");
                LogDiagnostic($"SKIP Animator state '{actionName}'; NetworkMecanimAnimator owns true proxy");
                return;
            }

            ownerMecanimCueSequence = sequence;
            ownerMecanimCueName = actionName;
            ownerMecanimCueTransitionIn = Mathf.Max(0f, transitionIn);
            ownerMecanimCueUsesFixedTransition = usesFixedTransition;
            ownerMecanimCueWaitingForAnimatorSnapshot = false;
            ownerMecanimCueBaselineBaseStateSequence = int.MinValue;
            forceOwnerBaseStateAfterCue = false;
            ownerMecanimActionActive = true;
            ownerMecanimActionName = actionName;
            ownerMecanimActionStateHash = 0;
            ownerAwaitingMecanimPostClearState = false;
            LogDiagnostic($"QUEUE owner Mecanim state '{actionName}' seq={sequence} until matching render snapshot");

            // Keep the previous AnimGraph pose connected until the same sequence is
            // visible in the render buffer. ApplyOwnerBaseState then starts the
            // controller underneath it before Render begins the smooth graph release.
            if (graphWasPlaying)
                BeginProxyGraphRelease(sequence);
            return;
        }

        if (!IsSupportedParkourAction(actionName, usesAnimGraph))
        {
            LogDiagnostic($"SKIP unsupported action '{actionName}' graph={usesAnimGraph}");
            return;
        }

        CancelProxyGraphRecovery();
        CancelProxyGraphRelease();
        PrepareProxyGraphReplacement();

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

    private void BeginProxyGraphRelease(int clearSequence)
    {
        if (!proxyGraphActionPlaying || animGraph == null)
            return;

        // Reliable RPCs arrive before Fusion's interpolated render buffer. Keep the
        // final action pose connected until the matching clear snapshot is visible;
        // then the underlying Animator Controller already contains the authoritative
        // Locomotion/Idle state and can receive a real crossfade instead of a cut.
        proxyGraphReleasePending = true;
        proxyGraphReleaseSequence = clearSequence;
    }

    private void StartProxyGraphBlendOut()
    {
        if (!proxyGraphActionPlaying || proxyGraphReleaseInProgress || animGraph == null)
            return;

        proxyGraphReleasePending = false;
        proxyGraphReleaseInProgress = true;
        hasRenderedGraphTime = false;

        float duration = proxyGraphTransitionOut > 0.001f
            ? proxyGraphTransitionOut
            : 0.12f;
        duration = Mathf.Clamp(duration, 0.1f, 0.22f);

        int generation = proxyGraphGeneration;
        proxyGraphReleaseCoroutine = StartCoroutine(
            BlendOutProxyGraph(generation, duration));
    }

    private IEnumerator BlendOutProxyGraph(int generation, float duration)
    {
        float startWeight = 1f;
        if (!animGraph.TryGetNetworkPresentationWeight(out startWeight))
        {
            proxyGraphReleaseCoroutine = null;
            CompleteProxyGraphRelease("release mixer unavailable");
            yield break;
        }

        float elapsed = 0f;
        while (generation == proxyGraphGeneration && proxyGraphActionPlaying
            && elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / duration));
            if (!animGraph.SetNetworkPresentationWeight(Mathf.Lerp(startWeight, 0f, t)))
                break;
            yield return null;
        }

        if (generation != proxyGraphGeneration)
            yield break;

        animGraph.SetNetworkPresentationWeight(0f);
        proxyGraphReleaseCoroutine = null;
        CompleteProxyGraphRelease("smooth authoritative release");
    }

    private void PrepareProxyGraphReplacement()
    {
        if (!proxyGraphActionPlaying)
            return;

        // Stop only the coroutine which advances the old clip. Its mixer stays in
        // the graph and becomes input zero of the next action mixer, giving the new
        // action a real source pose to crossfade from.
        proxyGraphGeneration++;
        if (proxyGraphCoroutine != null)
        {
            StopCoroutine(proxyGraphCoroutine);
            proxyGraphCoroutine = null;
        }

        proxyGraphActionPlaying = false;
        proxyGraphActionSequence = -1;
        proxyGraphReleasePending = false;
        proxyGraphReleaseInProgress = false;
        proxyGraphReleaseSequence = -1;
        hasRenderedGraphTime = false;
        LogDiagnostic($"PRESERVE AnimGraph pose for replacement generation={proxyGraphGeneration}");
    }

    private void CompleteProxyGraphRelease(string reason)
    {
        CancelProxyGraphRecovery();
        proxyGraphGeneration++;

        if (proxyGraphCoroutine != null)
        {
            StopCoroutine(proxyGraphCoroutine);
            proxyGraphCoroutine = null;
        }

        proxyGraphActionPlaying = false;
        proxyGraphActionSequence = -1;
        proxyGraphReleasePending = false;
        proxyGraphReleaseInProgress = false;
        proxyGraphReleaseSequence = -1;
        hasRenderedGraphTime = false;
        LogDiagnostic($"RELEASE AnimGraph reason='{reason}' generation={proxyGraphGeneration}");

        // Keep AnimatorControllerPlayable alive. It may already be blending into
        // BracedHangClimb/Climb Up beneath the action clip. Recreating it here was
        // the remaining client-only hard transition.
        if (animGraph == null || !animGraph.ReleaseNetworkPresentationToAnimatorBase())
        {
            lastAppliedOwnerBaseStateHash = 0;
            lastAppliedOwnerBaseStateSequence = -1;
            if (animGraph != null)
                animGraph.ResetAnimationGraph();
        }
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

    private void ResetProxyGraphPresentation(string reason)
    {
        CancelProxyGraphRecovery();
        CancelProxyGraphRelease();
        proxyGraphGeneration++;
        LogDiagnostic($"RESET AnimGraph reason='{reason}' generation={proxyGraphGeneration}");

        if (proxyGraphCoroutine != null)
        {
            StopCoroutine(proxyGraphCoroutine);
            proxyGraphCoroutine = null;
        }

        proxyGraphActionPlaying = false;
        proxyGraphActionSequence = -1;
        proxyGraphReleasePending = false;
        proxyGraphReleaseInProgress = false;
        proxyGraphReleaseSequence = -1;
        hasRenderedGraphTime = false;
        lastAppliedOwnerBaseStateHash = 0;
        lastAppliedOwnerBaseStateSequence = -1;

        if (animGraph != null)
            animGraph.ResetAnimationGraph();
    }

    private void CancelProxyGraphRelease()
    {
        if (proxyGraphReleaseCoroutine != null)
        {
            StopCoroutine(proxyGraphReleaseCoroutine);
            proxyGraphReleaseCoroutine = null;
        }

        proxyGraphReleasePending = false;
        proxyGraphReleaseInProgress = false;
        proxyGraphReleaseSequence = -1;
    }

    private void OnDestroy()
    {
        CancelProxyGraphRecovery();
        CancelProxyGraphRelease();

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
