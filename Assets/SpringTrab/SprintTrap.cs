using UnityEngine;

namespace FS_ThirdPerson
{
    /// <summary>
    /// Sprint-Falle: Wirft den Spieler nach hinten/oben wenn er drüberläuft.
    /// Spielt die Blender-Animation ab (intakt → zertrümmert).
    /// </summary>
    [RequireComponent(typeof(Animator))]
    public class SprintTrap : MonoBehaviour
    {
        [Header("Wurf-Kraft")]
        [Tooltip("Kraft nach hinten (relativ zur Falle)")]
        public float throwBackForce  = 12f;
        [Tooltip("Kraft nach oben")]
        public float throwUpForce    = 8f;

        [Header("Cooldown")]
        [Tooltip("Sekunden bis die Falle wieder auslösen kann (0 = einmalig)")]
        public float resetDelay = 0f;

        [Header("Effekte")]
        public AudioClip trapSound;
        public ParticleSystem dustParticles;

        // ── Interner State ──────────────────────────────────────────────
        private Animator    _animator;
        private AudioSource _audio;
        private bool        _triggered = false;

        // Animator-Parameter (muss in Unity mit Blender-Clip übereinstimmen)
        private static readonly int TriggerHash = Animator.StringToHash("Trigger");

        // ───────────────────────────────────────────────────────────────
        void Awake()
        {
            _animator = GetComponent<Animator>();

            _audio = GetComponent<AudioSource>();
            if (_audio == null && trapSound != null)
                _audio = gameObject.AddComponent<AudioSource>();
        }

        // ── Trigger-Kollision ───────────────────────────────────────────
        void OnTriggerEnter(Collider other)
        {
            if (_triggered) return;
            if (!other.CompareTag("Player")) return;

            Rigidbody rb = other.attachedRigidbody;
            if (rb == null) return;

            ActivateTrap(rb);
        }

        // ── Kern-Logik ──────────────────────────────────────────────────
        void ActivateTrap(Rigidbody playerRb)
        {
            _triggered = true;

            // Spieler wegwerfen: nach hinten (–transform.forward) und oben
            Vector3 throwDir = (-transform.forward * throwBackForce)
                             + (Vector3.up          * throwUpForce);

            playerRb.linearVelocity = Vector3.zero;          // vorherige Bewegung reset
            playerRb.AddForce(throwDir, ForceMode.Impulse);

            // Blender-Animation abspielen (Frame 1→30)
            _animator.SetTrigger(TriggerHash);

            // Sound
            if (_audio != null && trapSound != null)
                _audio.PlayOneShot(trapSound);

            // Partikel
            if (dustParticles != null)
                dustParticles.Play();

            // Optional: nach resetDelay zurücksetzen
            if (resetDelay > 0f)
                Invoke(nameof(ResetTrap), resetDelay);
        }

        // ── Reset (optional) ────────────────────────────────────────────
        void ResetTrap()
        {
            _triggered = false;
            _animator.Play("TrapIdle", 0, 0f); // zurück zu Frame 1
        }

#if UNITY_EDITOR
        void OnDrawGizmosSelected()
        {
            // Wurfrichtung im Editor anzeigen
            Gizmos.color = Color.red;
            Gizmos.DrawRay(transform.position + Vector3.up * 0.1f,
                           -transform.forward * (throwBackForce * 0.15f));
            Gizmos.color = Color.yellow;
            Gizmos.DrawRay(transform.position + Vector3.up * 0.1f,
                           Vector3.up * (throwUpForce * 0.15f));
        }
#endif
    }
}
