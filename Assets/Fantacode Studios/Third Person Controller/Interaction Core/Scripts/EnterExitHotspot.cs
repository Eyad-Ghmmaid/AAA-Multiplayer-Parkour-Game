using FS_Core;
using FS_Util;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace FS_InteractionSystem
{
    public class EnterExitHotspot : InteractionHotspot
    {
        // ══ Inspector ════════════════════════════════════════════════════════════════

        [Tooltip("Animation played on the character as it enters and settles into this spot.")]
        public InteractionAnimation enterClip;

        [Tooltip("Looping animation held on the occupant for as long as it stays inside.")]
        public AnimGraphClipInfo idleClip;

        [Tooltip("Animation played on the occupant as it leaves this spot.")]
        public InteractionAnimation exitClip;

        [Tooltip("Indicator label shown to the character currently inside (who can leave).")]
        public string exitLabel = "Exit";

        [Header("Occupant Placement")]
        [Tooltip("Place the occupant at the authored pose below instead of leaving it wherever the " +
                 "enter animation's root motion landed.")]
        public bool useCustomOccupantPlacement;

        [Tooltip("Occupant position, local to this spot's placement frame.")]
        [ShowIf("useCustomOccupantPlacement", true)]
        public Vector3 occupantLocalPosition;

        [Tooltip("Occupant rotation in euler angles, local to this spot's placement frame.")]
        [ShowIf("useCustomOccupantPlacement", true)]
        public Vector3 occupantLocalRotation;

        [Header("Pull Out")]
        [Tooltip("Allow another character to forcibly pull this spot's occupant out.")]
        public bool pullOutEnabled = true;

        [Tooltip("Interacting with an occupied spot pulls the current occupant out first, then the " +
                 "interactor enters it. Off, an occupied spot can only be emptied.")]
        public bool pullOutWhenEnter = false;

        [Tooltip("Animation played on the character performing the pull-out.")]
        [ShowIf("pullOutEnabled", true)]
        public AnimGraphClipInfo pullerClip;

        [Tooltip("Animation played on the occupant as it is dragged out.")]
        [ShowIf("pullOutEnabled", true)]
        public AnimGraphClipInfo pulledClip;

        [Tooltip("Animate this spot opening as the occupant comes out — a bin lid, a locker or a door.")]
        [ShowIf("pullOutEnabled", true)]
        public bool playOpenAnimationOnPullOut;

        [Tooltip("The AnimGraph on this spot that plays the open animation.")]
        [ShowIf("playOpenAnimationOnPullOut", true)]
        public AnimGraph pullOutOpenTarget;

        [Tooltip("Open animation played on this spot when a pull-out occurs (e.g. the lid lifting).")]
        [ShowIf("playOpenAnimationOnPullOut", true)]
        public AnimGraphClipInfo pullOutOpenClip;

        [Tooltip("Where the pulling character stands to haul the occupant out. Falls back to the " +
                 "base Interaction Points when left empty.")]
        [ShowIf("pullOutEnabled", true)]
        public List<InteractionPoint> pullOutInteractionPoints =
            new List<InteractionPoint>() { new InteractionPoint(new Vector3(.5f, 0, 0), 0) };

        [Tooltip("Indicator label shown to a character who can pull the occupant out.")]
        [ShowIf("pullOutEnabled", true)]
        public string pullOutLabel = "Pull Out";

        // ══ Events ═══════════════════════════════════════════════════════════════════

        public event Action<Transform> OnEnterCompleted;
        public event Action<Transform> OnExitCompleted;
        public event Action<ActionExecutor, Transform> OnPullOutComplete;

        // ══ State ════════════════════════════════════════════════════════════════════

        /// <summary>Everything about the character currently inside, cached on entering.</summary>
        protected class Occupancy
        {
            public Transform transform;
            public Transform defaultParent;
            public ActionExecutor actionExecutor;
            public AnimGraph animGraph;
            public InteractionExecutor interactionExecutor;
            public Damagable damagable;
            public EnterExitInteractionHandler interactionHandler;
        }

        const float UnanimatedPullOutDuration = 1f;
        const float IdleLoopDelay = .2f;

        Occupancy occupancy;

        public virtual bool IsOccupied => occupancy != null;
        public virtual Transform Occupant => occupancy?.transform;
        public bool IsEnterRunning { get; private set; }
        public bool IsOccupantExiting { get; private set; }
        public bool IsPullOutRunning { get; private set; }
        public bool InAction => IsEnterRunning || IsOccupantExiting || IsPullOutRunning;
        public bool HasCustomOccupantPose => useCustomOccupantPlacement;
        public virtual Transform OccupantPlacementFrame => transform;

        // ══ Unity lifecycle ══════════════════════════════════════════════════════════

        protected override void Awake()
        {
            base.Awake();

            disableHotspotAfterCompletion = false;
            disableObjectAfterCompletion = false;
            alignToHotspot = false;
        }

        protected override void Update()
        {
            base.Update();
            SetInteractText();
        }

        // ══ Interaction routing ══════════════════════════════════════════════════════

        public override IEnumerator Interact(InteractionExecutor interactionExecutor)
        {
            yield return base.Interact(interactionExecutor);

            // ── Exit ── the occupant itself interacting → leave normally.
            if (IsTheOccupant(interactionExecutor))
            {
                yield return Exit();
                yield break;
            }

            // ── Enter ──
            yield return Enter(interactionExecutor);
        }

        /// <summary>
        /// The indicator names the action the looking character would actually perform: leaving when
        /// it is the occupant, pulling out when it can, entering otherwise.
        /// </summary>
        protected virtual void SetInteractText()
        {
            if (CurrentInteractionExecutor == null || interactText == null) return;

            if (IsTheOccupant(CurrentInteractionExecutor))
                interactText.text = exitLabel;
            else if (CanPullOut(CurrentInteractionExecutor))
                interactText.text = pullOutLabel;
            else
                interactText.text = interactionLabel;
        }

        /// <summary>True when <paramref name="exec"/> is the character currently inside this spot.</summary>
        protected bool IsTheOccupant(InteractionExecutor exec) =>
            occupancy != null && exec != null && exec.transform == occupancy.transform;

        // ══ Enter ════════════════════════════════════════════════════════════════════

        /// <summary>Puts <paramref name="exec"/>'s character inside this spot, playing the enter clip.</summary>
        protected virtual IEnumerator Enter(InteractionExecutor exec)
        {
            if (exec == null) yield break;

            if ((IsOccupied && !pullOutWhenEnter) || InAction) yield break;
            
            bool doPullout = CanPullOut(CurrentInteractionExecutor);

            if (doPullout && pullOutWhenEnter)
                yield return PullOut(exec);
            

            bool playAnimation = enterClip.clip.NotNull();

            var record = new Occupancy
            {
                transform = exec.transform,
                defaultParent = exec.transform.parent,
                actionExecutor = exec.GetComponent<ActionExecutor>(),
                animGraph = exec.GetComponent<AnimGraph>(),
                interactionExecutor = exec,
                damagable = exec.GetComponent<Damagable>(),
                interactionHandler = exec.GetComponent<EnterExitInteractionHandler>()
            };

            occupancy = record;
            IsEnterRunning = true;
            exec.LockInteraction?.Invoke();
            yield return exec.AlignToHotspot(this, interactionPoints);


            SetCanTakeHit(record, false);
            try
            {
                StartIdleLoop(record, delay: playAnimation ? IdleLoopDelay : 0f);

                if (playAnimation)
                    yield return PlayInteractionAnimation(record, enterClip);

                record.transform.SetParent(transform);

                // With an animation, its root motion decides where the character lands and snapping
                // over that would fight it — so only an authored pose overrides it. Without one,
                // nothing has moved the character at all, so it must always be placed.
                if (HasCustomOccupantPose)
                {
                    GetOccupantPose(out Vector3 position, out Quaternion rotation);
                    record.transform.SetPositionAndRotation(position, rotation);
                }

                // Marked inside only HERE — after the clip has played and the occupant has been
                // placed. Set any earlier and a character still climbing in would already read as
                // pullable, and the pull would fire on top of the enter animation still running.
                record.interactionHandler?.SetInsideSpot(this);

                OnEnterCompleted?.Invoke(record.transform);
            }
            finally
            {
                SetCanTakeHit(record, true);
                IsEnterRunning = false;
            }
        }

        // ══ Exit ═════════════════════════════════════════════════════════════════════

        /// <summary>The occupant leaves, playing the exit clip.</summary>
        protected virtual IEnumerator Exit()
        {
            var record = occupancy;
            if (record == null || InAction) yield break;

            IsOccupantExiting = true;

            bool playAnimation = exitClip.clip.NotNull();

            // Release the held focus first: the interaction routine can finish normally from here and
            // the character gets its own control back as the exit plays.
            record.interactionExecutor?.UnLockInteraction?.Invoke();

            SetCanTakeHit(record, false);
            try
            {
                StopIdleLoop(record, delay: playAnimation ? IdleLoopDelay : 0f);
                if (playAnimation)
                {
                    yield return PlayInteractionAnimation(record, exitClip);
                    ResetParent(record);
                }
                else
                {
                    ResetParent(record);
                }

                occupancy = null;
                OnExitCompleted?.Invoke(record.transform);
            }
            finally
            {
                SetCanTakeHit(record, true);
                IsOccupantExiting = false;

                // Cleared at the END, so the stay reads as ongoing for the whole climb-out and
                // whatever the character was doing in here keeps its state until it is genuinely
                // out. Anything that must react the moment leaving BEGINS watches
                // IsOccupantExiting / InAction instead — this is not that signal.
                record.interactionHandler?.ClearInsideSpot(this);
            }
        }

        /// <summary>
        /// Abandons an enter that was stopped part-way, for when the coroutine driving it was killed
        /// before it could finish — the graph transitioned an AI out mid-approach, say.
        ///
        /// Needed because Unity does not run a stopped coroutine's finally blocks, so nothing the
        /// enter claimed is ever given back on its own. The damaging one is
        /// <see cref="IsEnterRunning"/>: it feeds <see cref="InAction"/>, which gates entering,
        /// exiting AND pulling out, so a spot left stuck mid-enter silently refuses every character
        /// for the rest of the level while looking perfectly normal in the inspector.
        ///
        /// No-op unless an enter is actually in flight, so an occupant who genuinely made it in is
        /// never disturbed. This is for the character that never got in.
        /// </summary>
        public virtual void AbandonEnter()
        {
            if (!IsEnterRunning) return;

            IsEnterRunning = false;

            var record = occupancy;
            if (record == null) return;

            // Claimed the moment the enter began, so it is still this spot's to release even though
            // the character never arrived.
            occupancy = null;

            SetCanTakeHit(record, true);

            if (idleClip.NotNull() && record.animGraph != null)
                record.animGraph.StopLoopingAnimations(false);

            ResetParent(record);
            record.interactionHandler?.ClearInsideSpot(this);

            // Enter took the interaction lock the moment it began; an abandoned enter has to give it
            // back, exactly as Exit does. Left standing, it holds the character's focus on a spot it
            // never reached — and now blocks item switching for the rest of the level with it, since
            // that lock is one of the three states InteractionExecutor reads to decide the hands are
            // busy.
            record.interactionExecutor?.UnLockInteraction?.Invoke();
        }

        // ══ Pull out ═════════════════════════════════════════════════════════════════

        /// <summary>
        /// Whether <paramref name="interactor"/> may pull this spot's occupant out: the feature must
        /// be enabled, the spot occupied and settled (not already emptying), and the interactor must
        /// not be the occupant itself (that is an exit).
        /// </summary>
        public virtual bool CanPullOut(InteractionExecutor interactor) =>
            pullOutEnabled && IsOccupied && !InAction && !IsTheOccupant(interactor);

        /// <summary>
        /// Forcibly removes the occupant: tears its stay down, plays the pull-out animations for both
        /// characters, clears occupancy and raises <see cref="OnPullOutComplete"/>.
        ///
        /// Public because the puller is not always the character interacting with this spot: combat
        /// AI hauls a target out of whatever it is hiding in and drives that through here. Safe to
        /// call unconditionally — an empty spot, or one already mid-enter / mid-exit / mid-pull-out,
        /// returns immediately.
        /// </summary>
        public IEnumerator PullOut(InteractionExecutor exec)
        {
            var record = occupancy;
            if (record == null || InAction || exec == null) yield break;

            IsPullOutRunning = true;
            try
            {
                yield return PullOutSequence(exec, record);
            }
            finally
            {
                IsPullOutRunning = false;
            }
        }

        IEnumerator PullOutSequence(InteractionExecutor exec, Occupancy record)
        {
            yield return exec.AlignToHotspot(this, interactionPoints);
            var pullerExecutor = exec.ActionExecutor;

            bool pulledClipPlaying = pulledClip.NotNull() && record.actionExecutor != null;
            if (pulledClipPlaying)
                record.actionExecutor.Execute(new CharacterAction(pulledClip)
                {
                    animationParameters = new AnimParameters() { transitionIn = 0 },
                    ignoreCollision = true,
                    requiresLedgeBlocking = false
                });

            ReleaseForPullOut(record);

            // Neither character can take a hit for the rest of the choreography — a hit landing on
            // either half of the synced pair mid-pull-out would desync or cancel it partway. Set
            // AFTER the release, whose teardown restores the pulled character's hittability, and
            // restored in the finally so an aborted sequence leaves nobody invulnerable.
            var pullerDamagable = pullerExecutor != null ? pullerExecutor.GetComponent<Damagable>() : null;
            if (pullerDamagable != null) pullerDamagable.CanTakeHit = false;
            SetCanTakeHit(record, false);

            try
            {
                PlayOpening(playOpenAnimationOnPullOut, pullOutOpenTarget, pullOutOpenClip);

                if (pullerClip.NotNull() && pullerExecutor != null)
                    yield return pullerExecutor.ExecuteAsync(new CharacterAction(pullerClip)
                    {
                        ignoreCollision = true,
                        requiresLedgeBlocking = false
                    });
                else if (pulledClipPlaying)
                    yield return new WaitUntil(() => !record.actionExecutor.IsBusy);
                else
                    // No pull-out animation drove the timing — without this the whole pull-out (and
                    // whatever follows it) resolves in a single frame. Hold a fixed beat in place of
                    // the animation. Deliberately hardcoded, not an inspector value: when clips ARE
                    // assigned, their length is the timing.
                    yield return new WaitForSeconds(UnanimatedPullOutDuration);
            }
            finally
            {
                if (pullerDamagable != null) pullerDamagable.CanTakeHit = true;
                SetCanTakeHit(record, true);
            }

            OnPullOutComplete?.Invoke(pullerExecutor, record.transform);
        }

        // Removal for the pull-out path: the reaction clip has already taken the graph, so the idle
        // loop is BLENDED out rather than yanked — cutting it here would pop the character from the
        // occupied pose to the locomotion pose in the same frame, on top of the clip just started.
        void ReleaseForPullOut(Occupancy record)
        {
            if (idleClip.NotNull() && record.animGraph != null)
                StartCoroutine(AsyncUtil.RunAfterDelay(IdleLoopDelay,
                    () => record.animGraph.StopLoopingAnimations(false)));

            ResetParent(record);
            occupancy = null;
            record.interactionHandler?.ClearInsideSpot(this);

            record.interactionExecutor?.Cancel(cancelCurrentAction: false);
        }

        // ══ Shared internals ═════════════════════════════════════════════════════════

        /// <summary>
        /// Plays one authored beat — the character's clip, with this spot's own opening animation (a
        /// lid, a door) fired alongside it. Awaits the character's clip, which owns the timing.
        /// </summary>
        protected IEnumerator PlayInteractionAnimation(Occupancy record, InteractionAnimation anim)
        {
            if (anim == null || !anim.clip.NotNull() || record.actionExecutor == null) yield break;

            if (anim.HasOpenAnimation)
                anim.openTarget.Crossfade(anim.openClip.clip, anim.openClip);

            yield return record.actionExecutor.ExecuteAsync(new CharacterAction(anim.clip)
            {
                ignoreCollision = true,
                requiresLedgeBlocking = false
            });
        }

        /// <summary>
        /// Fires one of this spot's own "opening" animations (a lid, a boot, a door) through its
        /// AnimGraph. Never drives the interaction's timing — the character animations do.
        /// </summary>
        protected void PlayOpening(bool enabled, AnimGraph target, AnimGraphClipInfo clip)
        {
            if (!enabled || target == null || !clip.NotNull()) return;
            target.Crossfade(clip.clip, clip, transitionBack: false);
        }

        /// <summary>
        /// The authored occupant pose in world space, resolved against
        /// <see cref="OccupantPlacementFrame"/> — this spot itself unless a subclass parks the
        /// occupant on a larger moving object and points the frame at its root.
        /// </summary>
        public void GetOccupantPose(out Vector3 position, out Quaternion rotation)
        {
            var frame = OccupantPlacementFrame;
            position = frame.TransformPoint(occupantLocalPosition);
            rotation = frame.rotation * Quaternion.Euler(occupantLocalRotation);
        }

        /// <summary>
        /// Stores a world pose back into the authored local values — the exact inverse of
        /// <see cref="GetOccupantPose"/>, so a scene-view handle round-trips without drift. Also
        /// what the inspector's Copy From Occupant writes, so a pose tuned live in play mode is
        /// captured against the same frame the runtime reads it back from.
        /// </summary>
        public void SetOccupantPoseFromWorld(Vector3 position, Quaternion rotation)
        {
            var frame = OccupantPlacementFrame;
            occupantLocalPosition = frame.InverseTransformPoint(position);
            occupantLocalRotation = (Quaternion.Inverse(frame.rotation) * rotation).eulerAngles;
        }

        void ResetParent(Occupancy record)
        {
            if (record.transform != null && record.transform.parent != record.defaultParent)
                record.transform.SetParent(record.defaultParent);
        }

        /// <summary>
        /// Hits are ignored while an enter / exit / pull-out plays — cancelling one of these
        /// parent-changing animations midway is what leaves stuck half-states. Every routine restores
        /// it in a finally, so however it ends the character is never left invulnerable. Hits landing
        /// while SETTLED inside still register.
        /// </summary>
        void SetCanTakeHit(Occupancy record, bool value)
        {
            if (record.damagable != null) record.damagable.CanTakeHit = value;
        }

        // ══ Idle loop ════════════════════════════════════════════════════════════════

        void StartIdleLoop(Occupancy record, float delay)
        {
            StartCoroutine(AsyncUtil.RunAfterDelay(delay, () =>
            {
                record.animGraph.PlayLoopingAnimation(idleClip.clip, idleClip);
            }));
        }

        /// <summary>Fades the loop out a beat from now — for when a clip is taking the pose over.</summary>
        void StopIdleLoop(Occupancy record, float delay)
        {
            if (!idleClip.NotNull() || record.animGraph == null) return;

            StartCoroutine(AsyncUtil.RunAfterDelay(IdleLoopDelay,
                () => record.animGraph.StopLoopingAnimations(false)));
        }
    }
}
