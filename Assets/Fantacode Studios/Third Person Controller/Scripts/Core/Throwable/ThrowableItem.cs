using FS_Core;
using FS_Util;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace FS_Core
{
    /// <summary>
    /// ScriptableObject data asset for a throwable item.
    /// Contains flight physics, animations, hit effects, and recovery settings.
    /// No damage or combat logic — extend via <c>ThrowableWeapon</c> in the Shooter module for that.
    /// </summary>
    [CreateAssetMenu(menuName = "Item/Throwable Item")]
    public class ThrowableItem : EquippableItem
    {
        // ── Flight physics ────────────────────────────────────────────────────

        [Tooltip("Maximum horizontal throw distance (world units). Aim points beyond this are clamped.")]
        public float maxThrowDistance = 10;

        [Tooltip("Maximum range for the aim raycast from the camera center.")]
        public float maxAimRange = 10;

        [Tooltip("The projectile is destroyed after this many seconds even if it never hits anything.")]
        public float maxLifetime = 100f;

        // ── Recovery ─────────────────────────────────────────────────────────

        [Tooltip("If true, the projectile can be picked up and thrown again after landing. If false, it is destroyed on impact.")]
        public bool isRecoverable = true;

        // ── Hit effects ───────────────────────────────────────────────────────

        [Tooltip("Surface-specific VFX and audio played on first contact. Matched by the collider's layer.")]
        public List<HitEffectData> hitEffects = new();

        // ── Aim Camera ───────────────────────────────────────────────────────

        [Tooltip("When true, the camera switches to custom settings while aiming.")]
        public bool useAimCamera;

        [ShowIf("useAimCamera", true)]
        [Tooltip("Camera settings applied while the player is aiming this throwable.")]
        public CameraSettings aimCameraSettings = new()
        {
            distance = 1.6f,
            framingOffset = new Vector3(0.4f, 1.5f, 0),
            followSmoothTime = 0f,
            sensitivity = 0.3f
        };

        // ── High / Low Toss ──────────────────────────────────────────────────

        [Header("Toss Mode")]

        [Tooltip("Horizontal distance threshold. Below this → Low Toss (flat, fast). " +
                 "Above this → High Toss (lobbed arc). Similar to PUBG grenade throw modes.")]
        public float tossDistanceThreshold = 5;

        [Tooltip("When enabled, the throwable supports a separate Low Toss mode with its own animations, arc, and timing. " +
                 "When disabled, only High Toss is used regardless of distance.")]
        public bool useLowToss;

        [Header("High Toss (overhand, long distance)")]
        public TossData highToss = new()
        {
            arcHeight = .6f,
            dropStartTime = 0
        };

        [Header("Low Toss (underhand, short distance)")]
        public TossData lowToss = new()
        {
            arcHeight = 0.1f,
            dropStartTime = 0
        };

        /// <summary>Returns High Toss if distance > threshold, Low Toss otherwise.</summary>
        public TossData GetTossForDistance(float horizontalDistance)
        {
            if (!useLowToss || lowToss == null)
                return highToss;

            return horizontalDistance >= tossDistanceThreshold ? highToss : lowToss;
        }

        // ── Fuse ─────────────────────────────────────────────────────────────

        /// <summary>
        /// Seconds the fuse-on-aim countdown runs for. Drives the fuse HUD readout.
        /// Subclasses that own a real fuse override this (e.g. <c>ThrowableWeapon.explosionDelay</c>).
        /// </summary>
        public virtual float FuseDuration => 3f;

        // ── Public API ────────────────────────────────────────────────────────

        /// <summary>Returns the first hit-effect entry whose layer mask contains <paramref name="hitLayer"/>.</summary>
        public HitEffectData GetHitEffect(int hitLayer)
        {
            foreach (var e in hitEffects)
                if ((e.layer.value & (1 << hitLayer)) != 0) return e;
            return null;
        }

        public override void SetCategory() =>
            category = Resources.Load<ItemCategory>("Category/Throwable");
    }

    /// <summary>
    /// Data for a single toss mode (High or Low).
    /// Contains aim animation, throw animation, arc height, and release timing.
    /// </summary>
    [Serializable]
    public class TossData
    {
        [Tooltip("Looping aim animation for this toss mode.")]
        public AnimGraphClipInfo aimAnimation;

        [Tooltip("One-shot throw animation for this toss mode.")]
        public AnimGraphClipInfo throwAnimation;

        [Tooltip("Normalised time within the throw animation at which the projectile is released.")]
        [Range(0f, 1f)]
        public float dropStartTime = 0.3f;

        [Tooltip("Arc height. High Toss uses a large value (lobbed); Low Toss uses a small value (flat).")]
        public float arcHeight = 0.8f;
    }

    [Serializable]
    public class HitEffectData
    {
        [Tooltip("Layer(s) this hit effect applies to.")]
        public LayerMask layer;

        [Tooltip("Visual marker displayed at the point of impact.")]
        public GameObject hitMarker;

        [Tooltip("Particle effect or visual effect triggered upon impact.")]
        public GameObject hitEffect;

        [Tooltip("Sound played when the projectile hits the specified layer.")]
        public AudioClip hitAudioClip;

        public float landingSoundRadius = 15;
    }
}
