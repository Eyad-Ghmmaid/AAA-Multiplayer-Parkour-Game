using System;
using System.Collections;
using System.Collections.Generic;
using Fusion;
using FS_Core;
using FS_ParkourSystem;
using UnityEngine;

/// <summary>
/// Relays one-shot FS parkour actions which are played through AnimGraph or
/// Animator.CrossFade. NetworkMecanimAnimator does not replicate the PlayableGraph
/// clips used by AnimGraph, so proxies replay those clips for presentation only.
/// The State Authority remains responsible for movement and root motion.
/// </summary>
public class FSNetworkParkourAnimationSync : NetworkBehaviour
{
    [SerializeField] private bool logDiagnostics = true;

    [Networked] private NetworkString<_64> ActiveActionName { get; set; }
    [Networked] private NetworkBool ActiveActionUsesAnimGraph { get; set; }
    [Networked] private NetworkBool ActiveActionUsesFixedTransition { get; set; }
    [Networked] private float ActiveTransitionIn { get; set; }
    [Networked] private float ActiveTransitionOut { get; set; }
    [Networked] private float ActiveAnimationSpeed { get; set; }
    [Networked] private int ActionSequence { get; set; }

    private ActionExecutor actionExecutor;
    private AnimGraph animGraph;
    private Animator animator;
    private ParkourController parkourController;

    private string pendingActionName;
    private bool pendingUsesAnimGraph;
    private bool pendingUsesFixedTransition;
    private float pendingTransitionIn;
    private float pendingTransitionOut;
    private float pendingAnimationSpeed;
    private bool hasPendingAction;
    private readonly Queue<ActionCue> actionCueQueue = new Queue<ActionCue>();

    private int lastAppliedRpcSequence = -1;
    private int initialStateSequence = -1;
    private bool initializedProxyAnimationState;
    private bool proxyGraphActionPlaying;
    private int proxyGraphGeneration;
    private bool warnedAboutMissingClip;

    private struct ActionCue
    {
        public string Name;
        public bool UsesAnimGraph;
        public bool UsesFixedTransition;
        public float TransitionIn;
        public float TransitionOut;
        public float AnimationSpeed;
    }

    public override void Spawned()
    {
        actionExecutor = GetComponent<ActionExecutor>();
        animGraph = GetComponent<AnimGraph>();
        animator = GetComponent<Animator>();
        parkourController = GetComponent<ParkourController>();

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
        if (!HasStateAuthority || !hasPendingAction)
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
            AnimationSpeed = pendingAnimationSpeed
        };

        // If the action ended or was replaced before this tick, publish that final
        // state too. Otherwise the last queued start already represents it.
        if (queuedCueCount == 0 || !SameCue(lastQueuedCue, currentCue))
            SendActionCue(currentCue);
        else
            SetNetworkAction(currentCue);

        hasPendingAction = false;
    }

    private void SendActionCue(ActionCue cue)
    {
        ActionSequence++;
        SetNetworkAction(cue);
        LogDiagnostic($"SEND seq={ActionSequence} action='{cue.Name}' graph={cue.UsesAnimGraph} fixed={cue.UsesFixedTransition} speed={cue.AnimationSpeed:0.###}");
        RPC_PlayParkourAction(cue.Name ?? string.Empty, cue.UsesAnimGraph,
            cue.UsesFixedTransition, cue.TransitionIn, cue.TransitionOut,
            cue.AnimationSpeed, ActionSequence);
    }

    private static bool SameCue(ActionCue left, ActionCue right)
    {
        return string.Equals(left.Name, right.Name, StringComparison.Ordinal)
            && left.UsesAnimGraph == right.UsesAnimGraph
            && left.UsesFixedTransition == right.UsesFixedTransition
            && Mathf.Approximately(left.TransitionIn, right.TransitionIn)
            && Mathf.Approximately(left.TransitionOut, right.TransitionOut)
            && Mathf.Approximately(left.AnimationSpeed, right.AnimationSpeed);
    }

    private void SetNetworkAction(ActionCue cue)
    {
        ActiveActionName = cue.Name ?? string.Empty;
        ActiveActionUsesAnimGraph = cue.UsesAnimGraph;
        ActiveActionUsesFixedTransition = cue.UsesFixedTransition;
        ActiveTransitionIn = cue.TransitionIn;
        ActiveTransitionOut = cue.TransitionOut;
        ActiveAnimationSpeed = cue.AnimationSpeed;
    }

    public override void Render()
    {
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
                ActiveAnimationSpeed);
        }
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All, Channel = RpcChannel.Reliable)]
    private void RPC_PlayParkourAction(string actionName, bool usesAnimGraph,
        bool usesFixedTransition, float transitionIn, float transitionOut,
        float animationSpeed, int sequence)
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
            transitionIn, transitionOut, animationSpeed);
    }

    private void PlayProxyAction(string actionName, bool usesAnimGraph,
        bool usesFixedTransition, float transitionIn, float transitionOut,
        float animationSpeed)
    {
        if (string.IsNullOrWhiteSpace(actionName))
        {
            LogDiagnostic("PLAY clear action");
            // A clear means State Authority has finished the gameplay action. It
            // must not cut off the proxy's one-shot presentation clip: the action
            // can finish a few frames after the authority reports completion.
            // A subsequent non-empty action still cancels/replaces this graph clip.
            return;
        }

        if (!IsSupportedParkourAction(actionName, usesAnimGraph))
        {
            LogDiagnostic($"SKIP unsupported action '{actionName}' graph={usesAnimGraph}");
            return;
        }

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

            var parameters = new AnimParameters
            {
                transitionIn = Mathf.Max(0f, transitionIn),
                transitionOut = Mathf.Max(0f, transitionOut),
                animationSpeed = Mathf.Max(0.01f, animationSpeed),
                transitionBack = true,
                onAnimationUpdate = null,
                OnComplete = null
            };
            int generation = ++proxyGraphGeneration;
            proxyGraphActionPlaying = true;
            LogDiagnostic($"PLAY AnimGraph clip='{clipInfo.clip.name}' generation={generation} transitionIn={parameters.transitionIn:0.###} speed={parameters.animationSpeed:0.###}");
            StartCoroutine(PlayProxyGraphAction(clipInfo, parameters, generation));
        }
        else
        {
            // This replays only the Animator state. The FS ActionExecutor has no
            // CurrentAction on a proxy, so its OnAnimatorMove cannot move the root.
            if (usesFixedTransition)
                animator.CrossFadeInFixedTime(actionName, Mathf.Max(0f, transitionIn));
            else
                animator.CrossFade(actionName, Mathf.Max(0f, transitionIn));
            LogDiagnostic($"PLAY Animator state='{actionName}' fixed={usesFixedTransition} transitionIn={transitionIn:0.###}");
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
                AnimationSpeed = pendingAnimationSpeed
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
        hasPendingAction = true;
    }

    private bool IsSupportedParkourAction(string actionName, bool usesAnimGraph)
    {
        if (string.IsNullOrWhiteSpace(actionName))
            return false;

        // AnimGraph-Aktionen werden über das konfigurierte ParkourAction-Set
        // erkannt, statt über starre Namens-Keywords.
        if (usesAnimGraph)
            return FindClipInfo(actionName) != null;

        string normalized = actionName.Replace(" ", string.Empty)
            .Replace("_", string.Empty)
            .Replace("-", string.Empty)
            .ToLowerInvariant();

        return normalized.Contains("climb")
            || normalized.Contains("stepup")
            || normalized.Contains("vault")
            || normalized.Contains("wallrun")
            || normalized.Contains("jump")
            || normalized.Contains("fall")
            || normalized.Contains("roll")
            || normalized.Contains("landing")
            || normalized.Contains("hang")
            || normalized.Contains("ledge")
            || normalized.Contains("shimmy");
    }

    private AnimGraphClipInfo FindClipInfo(string clipName)
    {
        if (parkourController != null && parkourController.parkourActions != null)
        {
            foreach (ParkourAction action in parkourController.parkourActions)
            {
                if (action?.ClipInfo?.clip != null && action.ClipInfo.clip.name == clipName)
                    return action.ClipInfo;
            }

            ParkourAction wallRunAction = parkourController.wallRunAction;
            if (wallRunAction?.ClipInfo?.clip != null && wallRunAction.ClipInfo.clip.name == clipName)
                return wallRunAction.ClipInfo;
        }

        // Some FS prefabs keep an action in Resources without including it in the
        // ParkourController list. Resolving the catalog locally keeps IDs stable.
        ParkourAction[] resourceActions = Resources.LoadAll<ParkourAction>("Parkour Actions");
        foreach (ParkourAction action in resourceActions)
        {
            if (action?.ClipInfo?.clip != null && action.ClipInfo.clip.name == clipName)
                return action.ClipInfo;
        }

        // Fallback: search all AnimationClips in Resources by name. Some projects
        // store clips directly without a ParkourAction asset; create a transient
        // AnimGraphClipInfo so AnimGraph can play the clip on proxies.
        AnimationClip[] resourceClips = Resources.LoadAll<AnimationClip>("");
        foreach (AnimationClip clip in resourceClips)
        {
            if (clip != null && clip.name == clipName)
                return new AnimGraphClipInfo { clip = clip };
        }

        return null;
    }

    private IEnumerator PlayProxyGraphAction(AnimGraphClipInfo clipInfo, AnimParameters parameters, int generation)
    {
        yield return animGraph.CrossfadeAsync(clipInfo, parameters);
        LogDiagnostic($"AnimGraph coroutine completed clip='{clipInfo?.clip?.name}' generation={generation} currentGeneration={proxyGraphGeneration}");
        if (generation == proxyGraphGeneration)
            proxyGraphActionPlaying = false;
    }

    private void StopProxyGraphAction()
    {
        if (!proxyGraphActionPlaying)
            return;

        proxyGraphGeneration++;
        LogDiagnostic($"CANCEL AnimGraph action; generation={proxyGraphGeneration}");
        animGraph.CancelCurrentAction();
        proxyGraphActionPlaying = false;
    }

    private void OnDestroy()
    {
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
