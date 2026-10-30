using FS_Core;
using FS_LocomotionSystem;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace FS_InteractionSystem
{
    public class InteractionExecutor : MonoBehaviour
    {
        public event Action OnInteractionStart;
        public event Action OnInteractionEnd;
        public event Action OnInteractionCancelled;
        public Action LockInteraction;
        public Action UnLockInteraction;



        ActionExecutor actionExecutor;
        LocomotionCore locomotionCore;
        ItemEquipper itemEquipper;
        IInteractorRole interactor;
        Damagable damagable;
        bool lockOnInteraction;

        // See the item-switching region at the bottom of this file.
        bool handsWereBusy;
        EquippableItem itemToRestore;

        Action setLockHandler;
        Action clearLockHandler;

        /// <summary>
        /// True while an interaction is holding itself open past its own routine — a character sitting
        /// in a spot it has not left yet. Whoever owns the character's focus watches this to know that
        /// an interaction ending does NOT mean control should be handed back.
        /// </summary>
        public bool IsInteractionLocked => lockOnInteraction;

        public bool IsBusy => isInInteraction || actionExecutor.IsBusy;

        public ActionExecutor ActionExecutor => actionExecutor;

        InteractionHotspot activeHotspot;

        public InteractionHotspot ActiveHotspot => activeHotspot;

        private void Awake()
        {
            actionExecutor = GetComponent<ActionExecutor>();
            itemEquipper = GetComponent<ItemEquipper>();
            locomotionCore = GetComponent<LocomotionCore>();
            interactor = GetComponent<IInteractorRole>();
            damagable = GetComponent<Damagable>();
        }

        // Nothing here reaches for a HotspotController any more. Detecting hotspots and holding the
        // character's focus are the PLAYER's concerns, and wiring them from in here made this whole
        // component player-only — an AI has no detector, so the early return meant it never even got
        // its own lock handlers. The dependency now runs the other way: this component announces what
        // it is doing (OnInteractionStart / OnInteractionEnd / IsBusy / IsInteractionLocked /
        // IsCarrying) and HotspotController subscribes, so an AI can drive interactions through the
        // very same executor.
        private void OnEnable()
        {
            setLockHandler = () => lockOnInteraction = true;
            clearLockHandler = () => lockOnInteraction = false;

            LockInteraction += setLockHandler;
            UnLockInteraction += clearLockHandler;
        }

        private void OnDisable()
        {
            if (setLockHandler != null) LockInteraction -= setLockHandler;
            if (clearLockHandler != null) UnLockInteraction -= clearLockHandler;

            // Nothing runs LateUpdate on a disabled component, so unblock here rather than leaving
            // the flag set for good.
            if (handsWereBusy && itemEquipper != null)
                itemEquipper.PreventItemSwitching = false;
            handsWereBusy = false;
        }

        bool isInInteraction;

        public void TryExecute(InteractionHotspot hotspot)
        {
            if (IsBusy || hotspot == null)
                return;

            if (!hotspot.CanInteract())
                return;

            StartCoroutine(Execute(hotspot));
        }

        /// <summary>
        /// The interaction itself, as a coroutine the caller can wait on.
        ///
        /// Public because not every interaction is started by a button press. An AI walking into a
        /// vehicle seat has to KNOW when it is seated before its state can finish, and the only way
        /// to know is to yield on this. The alternative — calling <c>hotspot.Interact</c> straight —
        /// looks equivalent and is not: it skips the claim below, so the hotspot never learns who is
        /// interacting with it. Everything keyed off that then reads wrong. The character's own
        /// capsule is no longer excluded from the hotspot's space check, so it blocks the very point
        /// it is standing on and <c>CanInteract</c> goes false; a seat cannot tell a carjacker from a
        /// stranger, so it refuses to eject its occupant; the weapon is never put away; and
        /// <see cref="Cancel"/> has no interaction on record to stop.
        ///
        /// Deliberately does NOT re-check <c>CanInteract</c> — <see cref="TryExecute"/> owns that
        /// gate. An occupant leaving a seat would fail it (the seat reads as occupied and blocked by
        /// the very character trying to get out), and refusing to let it out would be worse.
        /// </summary>
        public IEnumerator Execute(InteractionHotspot hotspot)
        {
            if (hotspot == null) yield break;

            // Claim the interaction from the very first frame, BEFORE the (possibly long) weapon
            // unequip. Cancel() gates on this state — without the early claim, a hit landing during
            // the unequip window couldn't cancel anything, and the routine would carry on into the
            // hotspot after the hit reaction (visible as the character snapping into a hide spot
            // right after the flinch).
            activeHotspot = hotspot;
            isInInteraction = true;

            // Pin the interactor context on the hotspot for the whole routine. The claim above makes
            // IsBusy suppress hotspot detection immediately, which fires OnLoseFocus — and that would
            // clear CurrentInteractionExecutor unless InteractionRunning already guards it (it is
            // normally only set by hotspot.OnStart, AFTER the unequip). Losing the context breaks
            // interactor-aware logic: e.g. the occupant exiting a spot would no longer be recognised
            // and would wrongly walk to the enter interaction point before playing the exit.
            hotspot.CurrentInteractionExecutor = this;
            hotspot.InteractionRunning = true;

            // Every interaction starts with empty hands — a carry, an Enter/Exit spot, a plain
            // hotspot alike: the interaction's animation is authored unarmed, and a weapon left in
            // the hand clips through whatever the character is climbing into or picking up. Done
            // here, before the walk to the interaction point, so the holster plays during the
            // approach rather than as a pause on arrival.
            //
            // Null on a character that carries nothing — plenty of AI have no ItemEquipper at all,
            // and they reach this the moment an AI drives an interaction rather than a player.
            if (itemEquipper != null)
            {
                // Remembered before the unequip, and only for a character that has to re-arm itself.
                if (RestoreItemAfterInteraction)
                    itemToRestore = itemEquipper.EquippedItem;

                yield return itemEquipper.UnEquipItemAsync();
            }

            OnInteractionStart?.Invoke();
            hotspot.OnStart?.Invoke();
            hotspot.OnStartInteractionEvent?.Invoke();

            if (hotspot.AlignToHotspot)
                yield return AlignToHotspot(hotspot);

            yield return hotspot.Interact(this);

            EndInteraction(hotspot);
            activeHotspot = null;
        }



        void EndInteraction(InteractionHotspot hotspot)
        {
            isInInteraction = false;
            OnInteractionEnd?.Invoke();
            hotspot.OnComplete?.Invoke();
            hotspot.OnEndInteractionEvent?.Invoke();
        }

        /// <summary>
        /// Walks the character onto <paramref name="hotspot"/>'s best interaction point and turns it
        /// to the authored facing, re-querying as it goes so a moving hotspot stays tracked. Honours
        /// the hotspot's <c>snapToPosition</c> for spots that want it instant.
        ///
        /// Public so a system that has to re-align MID-interaction can reuse it rather than
        /// hand-rolling a second one. A vehicle seat needs exactly that: the alignment that runs
        /// before Interact aims at whichever point set matched THEN — the pull-out point, for an
        /// occupied seat — so once the occupant is out, the character has to be walked across to the
        /// enter point before the enter animation, whose root motion is authored from that mark.
        /// </summary>
        public IEnumerator AlignToHotspot(InteractionHotspot hotspot, List<InteractionPoint> points = null)
        {
            (Vector3 targetPos, Quaternion targetRot) = hotspot.GetBestInteractionPoint(transform.position, points);

            if (hotspot.snapToPosition)
            {
                transform.position = targetPos;
                transform.rotation = targetRot;
                yield break;
            }
            var isAIInteractor = interactor != null && interactor.IsAIInteractor;
            locomotionCore.SetTarget(targetPos, targetRot,isAIInteractor?false: true, 2);
            while (!locomotionCore.TargetReached())
            {
                (targetPos, targetRot) = hotspot.GetBestInteractionPoint(transform.position);
                locomotionCore.TickMoveToTarget(targetPos, targetRot,ignoreCollision: true);
                yield return null;
            }
        }

        /// <summary>
        /// Forcibly cancels any in-progress interaction, restores all locked/focused systems,
        /// and notifies listeners (e.g. CarryHandler) to clean up without requiring direct coupling.
        /// </summary>
        /// <param name="cancelCurrentAction">
        /// Whether to also cancel the action currently running on the ActionExecutor. True for
        /// hit-driven cancels, where the interaction's own half-played clip must die with it.
        /// False when the caller has ALREADY started the animation that takes over — a pull-out
        /// releases the occupant's interaction state while the pulled clip is playing on that same
        /// executor, and cancelling "the current action" there kills the very clip the pull-out
        /// just started.
        /// </param>
        public void Cancel(bool cancelCurrentAction = true)
        {
            // Also handle a held-focus interaction (e.g. hiding in a concealment spot) whose enter
            // routine already finished: isInInteraction is false and activeHotspot is null, but
            // lockOnInteraction keeps the hotspot focused. Skipping it here would leave such a
            // character un-cancellable (and frozen). UnLockInteraction + OnInteractionEnd below then
            // reach HandleInteractionEnd, which un-focuses the hotspot.
            //
            // And a settled carry, for the same reason: the pickup routine is long finished, so
            // every flag above reads clear while the character still walks around holding a body.
            // Without IsCarrying here a hit or a ragdoll cancelled nothing once the pickup was over,
            // OnInteractionCancelled never fired, and the object stayed welded to the hand through
            // the flinch, the fall, and the get-up. Everything below is a no-op when there is no
            // interaction to tear down — CancelCurrent ignores an idle executor, and UnFocusSystem
            // ignores a system that isn't focused — so the carry-only path costs nothing but the
            // OnInteractionCancelled that CarryHandler is waiting for.
            if (!isInInteraction && activeHotspot == null && !lockOnInteraction && !IsCarrying) return;

            StopAllCoroutines();

            // Cancel the interaction's own animation still running on the ActionExecutor (e.g. a
            // half-played enter clip). StopAllCoroutines only halts the interaction coroutine; the
            // clip runs on the ActionExecutor's own coroutine, so its currentAction and locomotion
            // block would otherwise linger and freeze the character. CancelCurrent no-ops when idle
            // and skips synced animations.
            if (cancelCurrentAction)
                actionExecutor.CancelCurrent();

            if (activeHotspot != null)
                activeHotspot.InteractionRunning = false;

            isInInteraction = false;
            activeHotspot = null;

            UnLockInteraction?.Invoke();
            OnInteractionCancelled?.Invoke();
            OnInteractionEnd?.Invoke();
        }

        public void Stop()
        {
            StopAllCoroutines();
            // The claim in Execute sets this at interaction start; without clearing it here a
            // stopped interaction would leave the hotspot flagged as running (CanInteract == false)
            // forever.
            if (activeHotspot != null)
                activeHotspot.InteractionRunning = false;
            isInInteraction = false;
            activeHotspot = null;
        }

        /// <summary>
        /// Whether the item an interaction takes out of the character's hands is put back when the
        /// interaction lets go of them. True by default — giving back what you took is the generic
        /// behaviour, and an actor with no input of its own has no other way to get its weapon back.
        ///
        /// Whoever drives the character turns it off if that character re-arms by some other means;
        /// HotspotController does exactly that for the player, who picks their own weapon back up.
        /// Nothing in here needs to know which kind of actor it is serving.
        /// </summary>
        public bool RestoreItemAfterInteraction { get; set; } = true;

        public bool IsCarrying { get; private set; }
        public void StartCarrying() => IsCarrying = true;
        public void EndCarrying() => IsCarrying = false;

        // ══ Item switching ═══════════════════════════════════════════════════════════

        /// <summary>
        /// Item switching is blocked for exactly as long as the character's hands are busy with an
        /// interaction — a routine is running, it is settled inside an Enter/Exit spot it has not
        /// left, or it is carrying something — and unblocked the moment all three are clear. One
        /// rule for the whole interaction system, so no participating system needs a line of its own.
        ///
        /// Written every frame while busy rather than once on the way in, because PreventItemSwitching
        /// is a shared flag that several systems clear unconditionally — LocomotionController's
        /// UnFocusSystem fires at the end of every pickup interaction, with the body still in the
        /// character's arms.
        /// </summary>
        void LateUpdate()
        {
            if (itemEquipper == null) return;

            bool busy = isInInteraction || lockOnInteraction || IsCarrying;

            if (busy)
            {
                itemEquipper.PreventItemSwitching = true;
            }
            else if (handsWereBusy)
            {
                // Cleared outright, never put back to some earlier value. Restoring a snapshot taken
                // on the way in is what left this stuck at random: anything that happened to be
                // blocking switching as the interaction began — a hit reaction, a dodge, an AI
                // freeing its hands — was re-applied long after it had finished with it.
                itemEquipper.PreventItemSwitching = false;
                RestoreItem();
            }

            handsWereBusy = busy;
        }

        /// <summary>
        /// Puts back what the interaction took out of the character's hands — see
        /// <see cref="RestoreItemAfterInteraction"/> for who gets this and who opts out. Never onto a
        /// corpse: death cancels interactions, and arrives here like any other cancel.
        /// </summary>
        void RestoreItem()
        {
            var item = itemToRestore;
            itemToRestore = null;

            if (item == null) return;
            if (damagable != null && damagable.IsDead) return;

            // No draw animation: the character is usually already moving again by now.
            itemEquipper.EquipItem(item, false);
        }
    }
}
