using FS_Core;
using FS_Util;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace FS_InteractionSystem
{
    [RequireComponent(typeof(InteractionExecutor))]
    public class HotspotController : SystemBase
    {
        [SerializeField] float detectionRadius = 1f;

        [SerializeField] bool overrideHotspotLayer = false;

        [ShowIf("overrideHotspotLayer", true)]
        [SerializeField] LayerMask hotspotLayer = 1;

        private List<Hotspot> nearbyHotspots = new List<Hotspot>();
        private Hotspot closestHotspot;
        private Hotspot previousClosest;

        InteractionInputManager inputManager;
        PlayerController playerController;
        ICharacter player;
        InteractionExecutor interactionExecutor;

        Damagable damagable;

        public Action OnStartInteraction;
        public Action OnEndInteraction;

        // External hook: optional filter applied to every candidate hotspot. Return true to keep.
        public Func<Hotspot, bool> HotspotFilter { get; set; }

        public Hotspot ClosestHotspot => closestHotspot;

        public override SystemState State => SystemState.Hotspot;

        private void Awake()
        {
            inputManager = GetComponent<InteractionInputManager>();
            playerController = GetComponent<PlayerController>();
            player = GetComponent<ICharacter>();
            interactionExecutor = GetComponent<InteractionExecutor>();

            // The player puts their own weapon back, through the quick-switch wheel or a hotkey, and
            // would resent one reappearing uninvited. Declared from HERE, the player's own detector,
            // rather than sniffed out inside the executor — that component serves AI just as much and
            // has no business asking which kind of character it is on.
            if (interactionExecutor != null)
                interactionExecutor.RestoreItemAfterInteraction = false;
        }

        // Detection and focus are THIS component's concerns, so it subscribes to the executor rather
        // than the executor reaching in here. That keeps InteractionExecutor free of any detector and
        // usable by an AI, which has none.
        private void OnEnable()
        {
            if (interactionExecutor == null) return;

            interactionExecutor.OnInteractionStart += StartInteraction;
            interactionExecutor.OnInteractionEnd += HandleInteractionEnd;
        }

        private void OnDisable()
        {
            if (interactionExecutor == null) return;

            interactionExecutor.OnInteractionStart -= StartInteraction;
            interactionExecutor.OnInteractionEnd -= HandleInteractionEnd;
        }

        /// <summary>
        /// An interaction finishing normally hands control back — unless it is holding itself open
        /// (a character still sitting in the spot it entered), in which case the focus stays put until
        /// the exit releases it.
        /// </summary>
        void HandleInteractionEnd()
        {
            if (interactionExecutor != null && interactionExecutor.IsInteractionLocked) return;

            EndInteraction();
        }

        /// <summary>
        /// With something in the character's hands, only hotspots that say they still work stay
        /// visible — a bin you can stash the body into, the spot you are already hiding in.
        /// </summary>
        bool PassesCarryFilter(Hotspot hs)
        {
            if (interactionExecutor == null || !interactionExecutor.IsCarrying) return true;

            return hs is InteractionHotspot ihs && ihs.CanInteractWhileCarrying(interactionExecutor);
        }

        private void Start()
        {
            if (!overrideHotspotLayer)
                hotspotLayer = 1 << LayerMask.NameToLayer("Hotspot");

            damagable = GetComponent<Damagable>();
        }

        public void StartInteraction()
        {
            player.FocusSystem(this);
            OnStartInteraction?.Invoke();
        }

        public void EndInteraction()
        {
            player.UnFocusSystem(this);
            OnEndInteraction?.Invoke();
        }

        private void LateUpdate()
        {
            if (playerController.FocusedScript != null && playerController.FocusedSystemState == SystemState.Locomotion)
                HandleUpdate();
        }

        public override void HandleUpdate()
        {
            if (damagable?.CurrentHealth <= 0)
                return; // Skip hotspot detection if the character is dead

            DetectHotspots();

            if (inputManager != null && inputManager.InteractionButtonDown() && closestHotspot != null && Time.timeScale > 0)
            {
                closestHotspot.Interact(this);
            }
        }

        void DetectHotspots()
        {
            Collider[] hits = Physics.OverlapSphere(transform.position, detectionRadius, hotspotLayer, queryTriggerInteraction: QueryTriggerInteraction.Collide);

            nearbyHotspots.Clear();
            if(!interactionExecutor.IsBusy)
            {
                foreach (Collider col in hits)
                {
                    var hotspots = col.GetComponents<Hotspot>().ToList();


                    foreach (var hs in hotspots)
                    {
                        if (hs == null || !hs.enabled || !hs.CanInteract() || !PassesCarryFilter(hs)) continue;

                        if (HotspotFilter != null && !HotspotFilter(hs)) continue;

                        nearbyHotspots.Add(hs);
                    }
                }
            }
            var newClosest = GetClosestHotspot();

            // Toggle indicators + focus notifications
            if (previousClosest != newClosest)
            {
                if (previousClosest != null)
                {
                    previousClosest.OnLoseFocus(this);
                    previousClosest.ShowIndicator(false);
                }

                if (newClosest != null)
                {
                    newClosest.ShowIndicator(true);
                    newClosest.OnGainFocus(this);
                }

                previousClosest = newClosest;
            }

            closestHotspot = newClosest;
        }

        Hotspot GetClosestHotspot()
        {
            Hotspot closest = null;
            float minDist = float.MaxValue;

            foreach (var hs in nearbyHotspots)
            {
                float dist = Vector3.Distance(transform.position, hs.transform.position);
                if (dist < minDist)
                {
                    minDist = dist;
                    closest = hs;
                }
            }

            return closest;
        }
    }
}
