//using FS_InteractionSystem;
//using System.Collections;
//using UnityEngine;

//namespace FS_Core
//{
//    /// <summary>
//    /// Interaction hotspot placed on a landed throwable projectile.
//    /// Triggered when the player walks into range and interacts, recovering the item.
//    /// </summary>
//    public class ThrowableRecoveryHotspot : InteractionHotspot
//    {
//        // ── Events ────────────────────────────────────────────────────────────

//        /// <summary>Fired when the interaction completes and the item is recovered.</summary>
//        public System.Action OnRecovered;

//        // ── InteractionHotspot overrides ──────────────────────────────────────

//        public override IEnumerator Interact(InteractionExecutor interactionExecutor)
//        {
//            yield return null;
//            OnRecovered?.Invoke();
//        }
//    }
//}
