using FS_Util;
using System;
using UnityEngine;

namespace FS_InteractionSystem
{
    public class EnterExitInteractionHandler : MonoBehaviour
    {
        [Tooltip("Live state, filled in by the spot — not a setting. True once this character is " +
                 "settled inside a spot, false again the moment it starts leaving or is pulled out.")]
        [SerializeField, ReadOnly] bool isInsideSpot;

        public bool IsInsideSpot => isInsideSpot && CurrentSpot != null && CurrentSpot.IsOccupied;
        public EnterExitHotspot CurrentSpot { get; private set; }
        public Action<EnterExitHotspot> OnEnteredSpot;
        public Action<EnterExitHotspot> OnLeftSpot;

        // Item switching is NOT touched here. Blocking it while folded into a dumpster is one case
        // of a general rule — hands busy with an interaction cannot change items — and that rule
        // lives in one place, InteractionExecutor, which already sees this stay through the
        // interaction lock the spot takes on entering. Reaching for the ItemEquipper from here as
        // well only gave two owners to a single shared flag.
        public void SetInsideSpot(EnterExitHotspot spot)
        {
            if (spot == null) return;

            CurrentSpot = spot;
            isInsideSpot = true;
            OnEnteredSpot?.Invoke(spot);
        }

        public void ClearInsideSpot(EnterExitHotspot spot)
        {
            if (spot != null && CurrentSpot != null && !ReferenceEquals(CurrentSpot, spot)) return;

            var left = CurrentSpot;
            bool wasInside = isInsideSpot;

            CurrentSpot = null;
            isInsideSpot = false;

            if (!wasInside) return;

            OnLeftSpot?.Invoke(left);
        }
    }
}
