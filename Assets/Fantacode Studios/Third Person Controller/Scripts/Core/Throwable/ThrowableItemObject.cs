using FS_Core;
using FS_Util;
using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Events;

namespace FS_Core
{
    /// <summary>
    /// Base MonoBehaviour placed on a throwable projectile prefab.
    /// Handles physics, collision detection, hit effects, and lifetime.
    /// No damage or explosion logic — extend via <c>ThrowableWeaponObject</c> in the Shooter module.
    /// </summary>
    public class ThrowableItemObject : EquippableItemObject
    {
        [Tooltip("How many throwables this object represents. Managed by ThrowableHandler.")]
        public int count = 1;

        [Space]
        [SerializeField] UnityEvent onThrowEvent;
        [SerializeField] UnityEvent onLandEvent;

        // ── Public state ──────────────────────────────────────────────────────

        public ThrowableItem ItemData => itemData as ThrowableItem;
        public Rigidbody Rb => Rigidbody;
        public bool IsThrown { get; private set; }
        public bool HasLanded { get; private set; }
        public GameObject Source { get; private set; }

        /// <summary>
        /// Fired when the projectile needs to be force-dropped from the hand
        /// (e.g. fuse expiry in a weapon subclass). ThrowableHandler subscribes to this.
        /// </summary>
        public event Action OnForceDropRequested;

        /// <summary>
        /// Fired by subclasses when a fuse-on-aim countdown starts. ThrowableHandler
        /// subscribes and re-raises on its own OnFuseStarted event so any UI can react.
        /// </summary>
        public event Action OnFuseStarted;

        /// <summary>Fired by subclasses when the fuse stops (cancel / expire / throw).</summary>
        public event Action OnFuseStopped;

        // ── Private ───────────────────────────────────────────────────────────

        bool _firstHitHandled;

        // ── Public API ────────────────────────────────────────────────────────

        public void Initialize(ThrowableItem item, GameObject source = null)
        {
            itemData = item;
            Source = source;
            _firstHitHandled = false;
            HasLanded = false;
            IsThrown = false;
        }

        public void Launch()
        {
            HandlePhysics(true);
            IsThrown = true;
            onThrowEvent?.Invoke();
            OnLaunched();

            // Temporarily ignore collision with the source character so the
            // projectile doesn't collide immediately when leaving the hand.
            if (Source != null)
                StartCoroutine(IgnoreSourceCollision(0.25f));

            // Safety net: if the projectile never hits anything, clean it up
            // after maxLifetime (skipped if it hits something or is recoverable).
            StartCoroutine(LifetimeTimeout());
        }

        /// <summary>Called by ThrowableHandler when aim starts. Override for fuse logic.</summary>
        public virtual void OnAimStarted() { }

        /// <summary>Called by ThrowableHandler when aim is cancelled. Override to stop fuse.</summary>
        public virtual void OnAimCancelled() { }

        // ── Protected API for subclasses ──────────────────────────────────────

        /// <summary>Requests ThrowableHandler to force-drop this item (e.g. fuse expiry).</summary>
        protected void RequestForceDrop() => OnForceDropRequested?.Invoke();

        /// <summary>Subclasses (e.g. ThrowableWeaponObject) call this when a fuse-on-aim countdown begins.</summary>
        protected void RaiseFuseStarted() => OnFuseStarted?.Invoke();

        /// <summary>Subclasses call this when the fuse stops (cancel / expire / throw).</summary>
        protected void RaiseFuseStopped() => OnFuseStopped?.Invoke();

        /// <summary>Called at the end of <see cref="Launch"/>. Override for launch-time side effects.</summary>
        protected virtual void OnLaunched() { }

        /// <summary>
        /// Called before hit effects play. Override to add pre-hit behavior.
        /// Return true to skip the default hit processing (effects + lifetime).
        /// </summary>
        protected virtual bool OnBeforeHitProcessed(Collision collision) => false;

        /// <summary>Called after hit effects play. Override to apply damage.</summary>
        protected virtual void OnAfterHitProcessed(Collision collision) { }

        // ── Collision ─────────────────────────────────────────────────────────

        void OnCollisionEnter(Collision collision)
        {
            if (!IsThrown || _firstHitHandled) return;

            _firstHitHandled = true;
            HandleFirstHit(collision);
        }

        protected virtual void HandleFirstHit(Collision collision)
        {
            if (ItemData == null) return;

            HasLanded = true;
            onLandEvent?.Invoke();

            if (OnBeforeHitProcessed(collision))
                return;

            PlayHitEffect(collision);
            OnAfterHitProcessed(collision);

            // Non-recoverable items that landed get a cleanup timer.
            // Recoverable items stay in the world indefinitely for pickup.
            if (!ItemData.isRecoverable)
                StartCoroutine(LifetimeTimeout());
        }

        // ── Hit effects ───────────────────────────────────────────────────────

        protected void PlayHitEffect(Collision collision)
        {
            if (ItemData.hitEffects == null || ItemData.hitEffects.Count == 0) return;

            int layer = collision.gameObject.layer;
            var fx = ItemData.GetHitEffect(layer);
            if (fx == null) return;

            Vector3 point = collision.contacts[0].point;
            Quaternion normal = Quaternion.LookRotation(collision.contacts[0].normal);

            if (fx.hitMarker != null)
                Instantiate(fx.hitMarker, point, normal);

            if (fx.hitEffect != null)
                Instantiate(fx.hitEffect, point, normal);

            if (fx.hitAudioClip != null)
            {
                GameSoundEvents.Emit(new GameSoundEvent
                {
                    position = point,
                    radius = fx.landingSoundRadius,
                    type = GameSoundType.ThrowImpact,
                    baseLoudness = 1f,
                    source = gameObject
                });

                FSAudioUtil.PlaySfxNew(fx.hitAudioClip, point);
            }
        }

        // ── Source collision ignore ──────────────────────────────────────────

        IEnumerator IgnoreSourceCollision(float duration)
        {
            var sourceColliders = Source.GetComponentsInChildren<Collider>();
            var myCollider = GetComponent<Collider>();

            if (myCollider == null || sourceColliders.Length == 0) yield break;

            foreach (var col in sourceColliders)
                if (col != null) Physics.IgnoreCollision(myCollider, col, true);

            yield return new WaitForSeconds(duration);

            // Re-enable only if both objects still exist.
            if (myCollider != null)
                foreach (var col in sourceColliders)
                    if (col != null) Physics.IgnoreCollision(myCollider, col, false);
        }

        // ── Lifetime ─────────────────────────────────────────────────────────

        protected IEnumerator LifetimeTimeout()
        {
            float lifetime = ItemData != null && ItemData.maxLifetime > 0f
                ? ItemData.maxLifetime
                : 10f;

            yield return new WaitForSeconds(lifetime);

            // Already hit something — the object is on the ground, let it stay.
            if (_firstHitHandled) yield break;

            // Recoverable items remain in the world for pickup.
            if (ItemData != null && ItemData.isRecoverable) yield break;

            Destroy(gameObject);
        }
    }
}
