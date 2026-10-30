using FS_Core;
using FS_Util;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace FS_Core
{
    /// <summary>
    /// Core throwable mechanics — no input, no UI, no damage dependencies.
    /// Manages the in-hand projectile, throw count, and spawning of the next projectile.
    /// Reusable by both the player (<see cref="ThrowableController"/>) and AI states.
    /// </summary>
    public class ThrowableHandler : MonoBehaviour
    {

        // ── State ─────────────────────────────────────────────────────────────

        public bool IsAiming { get; private set; }
        public bool IsThrowing { get; private set; }
        public bool HasThrowable => CurrentEquippedItem != null;
        public Vector3 AimTarget { get; private set; }
        /// <summary>True while a fuse-on-aim throwable's countdown is running.</summary>
        public bool IsFuseActive { get; private set; }
        public ThrowableItem CurrentEquippedItem
        {
            get
            {
                return itemEquipper?.EquippedItem is ThrowableItem throwableItem ? throwableItem : null;
            }
        }

        // ── Events ────────────────────────────────────────────────────────────

        public event Action OnThrowStarted;
        public event Action OnThrowComplete;
        /// <summary>Fired when a fuse-on-aim throwable starts cooking (aim entered).</summary>
        public event Action<ThrowableItem> OnFuseStarted;
        /// <summary>Fired when the fuse stops (aim cancelled, throw, or fuse expired).</summary>
        public event Action OnFuseStopped;

        // ── Private ───────────────────────────────────────────────────────────

        ThrowableItemObject projectile;

        // The throwable the in-flight throw belongs to. What is EQUIPPED can change underneath a
        // running throw — the player switching to a firearm during the wind-up — so every step of the
        // throw reads the item from here rather than from whatever happens to be in hand at that
        // instant. Null whenever no throw is running.
        ThrowableItem throwingItem;

        AnimGraph animGraph;
        ActionExecutor actionExecuter;
        ItemEquipper itemEquipper;
        Coroutine throwCoroutine;

        /// <summary>Currently active toss mode (High or Low) based on aim distance.</summary>
        public TossData ActiveToss { get; private set; }

        /// <summary>
        /// The item the current throw belongs to — the in-flight one while a throw is running,
        /// otherwise whatever is in hand. Everything that reads throw settings (animations, drop
        /// time, arc height, distance clamp) goes through this so a weapon switch mid-throw can't
        /// leave the throw reading settings off the wrong item, or off none at all.
        /// </summary>
        ThrowableItem ActiveItem => throwingItem != null ? throwingItem : CurrentEquippedItem;

        private void Awake()
        {
            animGraph = GetComponent<AnimGraph>();
            actionExecuter = GetComponent<ActionExecutor>();
            itemEquipper = GetComponent<ItemEquipper>();
        }

        public void EquipItem(EquippableItem item)
        {
            if (item is not ThrowableItem) return;
            IsAiming = false;
            IsThrowing = false;
            if (IsFuseActive)
            {
                IsFuseActive = false;
                OnFuseStopped?.Invoke();
            }
            projectile = itemEquipper.EquippedItemObject as ThrowableItemObject;

            if (projectile != null)
            {
                projectile.HandlePhysics(false);
                projectile.OnForceDropRequested += HandleForceDropRequested;
                projectile.OnFuseStarted += HandleFuseStarted;
                projectile.OnFuseStopped += HandleFuseStopped;
            }
        }

        void HandleFuseStarted()
        {
            IsFuseActive = true;
            OnFuseStarted?.Invoke(CurrentEquippedItem);
        }

        void HandleFuseStopped()
        {
            if (!IsFuseActive) return;
            IsFuseActive = false;
            OnFuseStopped?.Invoke();
        }

        // ── Public API ────────────────────────────────────────────────────────

        public void StartAim()
        {
            if (IsAiming || IsThrowing || !HasThrowable) return;

            IsAiming = true;
            ActiveToss = null;

            // Default to high toss — UpdateAimDistance will swap to low toss if target is close.
            SetActiveToss(CurrentEquippedItem.highToss);

            projectile?.OnAimStarted();
        }

        /// <summary>
        /// Called each frame while aiming. Switches between High Toss and Low Toss
        /// based on horizontal distance to the aim point.
        /// </summary>
        public void UpdateAimDistance(Vector3 aimPoint)
        {
            if (!IsAiming || CurrentEquippedItem == null) return;

            Vector3 origin = transform.position;
            float horizDist = new Vector2(aimPoint.x - origin.x, aimPoint.z - origin.z).magnitude;

            var newToss = CurrentEquippedItem.GetTossForDistance(horizDist);

            if (newToss != ActiveToss)
                SetActiveToss(newToss);
        }

        void SetActiveToss(TossData toss)
        {
            ActiveToss = toss;

            var clip = GetActiveAimAnimation();
            if (clip != null && clip.NotNull())
                animGraph?.PlayLoopingAnimation(clip.clip, clip, mask: Mask.UpperBodyWithRoot);
        }

        /// <summary>Returns the aim animation for the active toss, respecting the separate-animation toggle.</summary>
        AnimGraphClipInfo GetActiveAimAnimation()
        {
            var item = ActiveItem;
            if (item == null) return null;

            if (item.useLowToss && ActiveToss?.aimAnimation != null && ActiveToss.aimAnimation.NotNull())
                return ActiveToss.aimAnimation;

            return item.highToss?.aimAnimation;
        }

        /// <summary>Returns the throw animation for the active toss, respecting the separate-animation toggle.</summary>
        AnimGraphClipInfo GetActiveThrowAnimation()
        {
            var item = ActiveItem;
            if (item == null) return null;

            if (item.useLowToss && ActiveToss?.throwAnimation != null && ActiveToss.throwAnimation.NotNull())
                return ActiveToss.throwAnimation;

            return item.highToss?.throwAnimation;
        }

        /// <summary>Returns the drop start time for the active toss, respecting the separate-animation toggle.</summary>
        float GetActiveDropStartTime()
        {
            var item = ActiveItem;
            if (item == null) return 0.3f;

            if (item.useLowToss && ActiveToss != null)
                return ActiveToss.dropStartTime;

            return item.highToss?.dropStartTime ?? 0.3f;
        }

        public void CancelAim()
        {
            if (!IsAiming) return;

            IsAiming = false;
            ActiveToss = null;

            // Stop the looping aim animation AND cancel any AnimGraph action to fully reset.
            animGraph?.StopLoopingAnimations(true);
            animGraph?.CancelCurrentAction();

            // Don't unsubscribe from the projectile here — the same projectile stays
            // in hand after a cancelled aim and will be re-aimed. Unsubscribing now
            // would orphan the handler from a fresh fuse cycle the next time the
            // player aims, leaving the UI / IsFuseActive state stuck.
            // The subscriptions are torn down in EquipItem (on a new projectile) and
            // in HandleForceDropRequested when the projectile leaves the hand.
            if (projectile != null)
                projectile.OnAimCancelled();
        }

        public void Throw(Vector3 aimPoint)
        {
            if (!IsAiming || IsThrowing || !HasThrowable) return;

            AimTarget = aimPoint;
            throwCoroutine = StartCoroutine(ExecuteThrow(playAnimation: true));
        }

        // ── Throw execution ───────────────────────────────────────────────────

        IEnumerator ExecuteThrow(bool playAnimation)
        {
            if (projectile == null || CurrentEquippedItem == null) yield break;

            IsAiming = false;
            IsThrowing = true;
            OnThrowStarted?.Invoke();

            // The throw runs off its own item and projectile from here on. Both are captured now
            // because the throw spans several frames, and what is in hand can change during them.
            var cachedItem = CurrentEquippedItem;
            var thrownProjectile = projectile;
            throwingItem = cachedItem;

            int projectileCount = thrownProjectile.count;
            thrownProjectile.count = 1;

            // Hold item switching for the release-critical stretch of the throw. A switch landing
            // during the wind-up re-points EquippedItem at the incoming weapon, and the drop below
            // acts on whatever is equipped — so the firearm would be thrown on the floor while the
            // grenade stayed stuck in hand. Holding it here also keeps the multi-count re-equip from
            // fighting a switch, which is what the old narrower hold was already for.
            //
            // The previous value is restored rather than forced false: other systems (dodge, carry,
            // vehicles) hold this flag too, and a throw finishing must not hand switching back to a
            // player who is still mid-dodge.
            bool switchingWasHeld = itemEquipper.PreventItemSwitching;
            itemEquipper.PreventItemSwitching = true;

            try
            {
                var throwAnim = GetActiveThrowAnimation();
                float dropTime = GetActiveDropStartTime();

                if (playAnimation && throwAnim != null && throwAnim.NotNull())
                {
                    // Do NOT stop the aim loop before starting the throw clip.
                    // The throw crossfade will blend out the aim loop naturally.
                    actionExecuter.Execute(new CharacterAction(throwAnim) { usesRootMotion = false, animationParameters = new AnimParameters { mask = Mask.UpperBodyWithRoot } });

                    StartCoroutine(AsyncUtil.RunAfterDelay(.2f, () => 
                        animGraph?.StopLoopingAnimations(false)
                    ));
                    yield return new WaitForSeconds(dropTime);

                }

                if (thrownProjectile != null)
                {
                    // Drop the throwable BY NAME. Left to its default this drops whatever is equipped,
                    // which is only the same thing while nothing else has touched the hand.
                    itemEquipper.DropItem(cachedItem, destroyItem: false, canEquip: cachedItem.isRecoverable);

                    if (playAnimation)
                        ReleaseProjectile(thrownProjectile, cachedItem);
                }

                if (projectileCount > 1)
                {
                    yield return new WaitForSeconds(.5f);

                    // The next projectile goes in through the normal equip path, which refuses to run
                    // while switching is held — release the hold first. The thrown one is already gone
                    // by now, so there is nothing left for a switch to corrupt.
                    itemEquipper.PreventItemSwitching = switchingWasHeld;
                    itemEquipper.EquipItem(cachedItem);
                    yield return new WaitUntil(() => itemEquipper.EquippedItemObject != null);

                    var newProjectile = itemEquipper.EquippedItemObject as ThrowableItemObject;
                    if (newProjectile != null)
                    {
                        projectile = newProjectile;
                        projectile.count = projectileCount - 1;
                        projectile.OnForceDropRequested += HandleForceDropRequested;
                    }
                }
                else
                {
                    projectile = null;
                }
            }
            finally
            {
                // Runs however the throw ends — including the coroutine being stopped mid-flight —
                // so the switching hold can never be left on and lock the player out of their weapons.
                itemEquipper.PreventItemSwitching = switchingWasHeld;
                throwingItem = null;
                throwCoroutine = null;
                IsThrowing = false;
                OnThrowComplete?.Invoke();
            }
        }

        // ── Trajectory ──────────────────────────────────────────────────────

        public Vector3 GetThrowOrigin()
        {
            if (projectile != null)
                return projectile.transform.position;
            return transform.position + Vector3.up * 1.6f + transform.forward * 0.5f;
        }

        public (float drag, float mass) GetProjectilePhysics()
        {
            if (projectile != null && projectile.Rb != null)
                return (projectile.Rb.linearDamping, projectile.Rb.mass);
            return (0f, 1f);
        }

        /// <summary>
        /// Launches the projectile this throw started with, using that throw's own item data. Both are
        /// passed in rather than read from the fields: by the time the release lands, the equipped item
        /// may be a firearm the player switched to, and reading it here would initialise the grenade
        /// with the wrong data (or none).
        /// </summary>
        void ReleaseProjectile(ThrowableItemObject thrown, ThrowableItem item)
        {
            if (thrown == null || item == null) return;

            Vector3 origin = thrown.transform.position;
            Vector3 velocity = CalculateThrowVelocity(origin, AimTarget);

            thrown.OnForceDropRequested -= HandleForceDropRequested;
            thrown.OnFuseStarted -= HandleFuseStarted;
            thrown.OnFuseStopped -= HandleFuseStopped;
            thrown.Initialize(item, gameObject);
            thrown.Rb.linearVelocity = velocity;
            thrown.Launch();
        }

        // ── Force-drop (generic, used by weapon fuse expiry etc.) ────────────

        void HandleForceDropRequested()
        {
            if (projectile != null)
            {
                projectile.OnForceDropRequested -= HandleForceDropRequested;
                projectile.OnFuseStarted -= HandleFuseStarted;
                projectile.OnFuseStopped -= HandleFuseStopped;
            }

            if (IsThrowing) return;

            IsAiming = false;
            animGraph?.StopLoopingAnimations(false);
            throwCoroutine = StartCoroutine(ExecuteThrow(playAnimation: false));
        }

        // ── Velocity calculation ──────────────────────────────────────────────

        public Vector3 CalculateThrowVelocity(Vector3 from, Vector3 to)
        {
            var item = ActiveItem;
            if (item == null) return Vector3.zero;

            Vector3 delta = to - from;
            float horizDist = new Vector2(delta.x, delta.z).magnitude;

            if (horizDist > item.maxThrowDistance)
            {
                float scale = item.maxThrowDistance / horizDist;
                delta = new Vector3(delta.x * scale, delta.y, delta.z * scale);
                horizDist = item.maxThrowDistance;
            }

            float arcHeight = GetActiveArcHeight();

            float g = Mathf.Abs(Physics.gravity.y);

            float peakY    = Mathf.Max(delta.y, 0f) + arcHeight;
            float timeUp   = Mathf.Sqrt(2f * peakY / Mathf.Max(g, 0.001f));
            float timeDown = Mathf.Sqrt(2f * Mathf.Max(peakY - delta.y, 0f) / Mathf.Max(g, 0.001f));
            float totalTime = Mathf.Max(timeUp + timeDown, 0.01f);

            Vector3 v;
            v.x = delta.x / totalTime;
            v.z = delta.z / totalTime;
            v.y = (delta.y - 0.5f * Physics.gravity.y * totalTime * totalTime) / totalTime;

            return v;
        }

        float GetActiveArcHeight()
        {
            var item = ActiveItem;
            if (item == null)
                return 0.8f;

            if (item.useLowToss && ActiveToss != null)
                return ActiveToss.arcHeight;

            return item.highToss?.arcHeight ?? 0.8f;
        }

        // ── Trajectory simulation ─────────────────────────────────────────────

        public List<Vector3> ComputeTrajectoryPoints(
            Vector3 origin, Vector3 velocity,
            int segments, float timeStep,
            LayerMask collisionMask,
            float drag = 0f, float mass = 1f)
        {
            var points = new List<Vector3> { origin };
            Vector3 pos = origin;
            Vector3 vel = velocity;

            for (int i = 0; i < segments; i++)
            {
                Vector3 dragForce = -drag * vel.magnitude * vel.normalized;
                Vector3 accel = Physics.gravity + dragForce / Mathf.Max(mass, 0.001f);

                Vector3 prevPos = pos;
                vel += accel * timeStep;
                pos += vel * timeStep;
                points.Add(pos);

                Vector3 seg = pos - prevPos;
                float segDist = seg.magnitude;

                if (segDist > 0.001f && Physics.Raycast(
                        prevPos, seg.normalized, out RaycastHit hit,
                        segDist, collisionMask, QueryTriggerInteraction.Ignore))
                {
                    points[points.Count - 1] = hit.point;
                    break;
                }
            }

            return points;
        }
    }
}
