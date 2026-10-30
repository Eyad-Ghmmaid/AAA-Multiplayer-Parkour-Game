using System;
using TMPro;
using UnityEngine;
using UnityEngine.Events;

namespace FS_InteractionSystem
{
    public class Hotspot : MonoBehaviour
    {
        [SerializeField] GameObject interactUIPrefab;

        [Tooltip("Where the interaction prompt sits relative to this hotspot. Y is applied straight " +
                 "up in world space, so the prompt stays level however the object is tilted; X and Z " +
                 "follow the hotspot's heading, so a prompt pushed out to the side of a door stays " +
                 "beside that door whichever way it faces.")]
        [SerializeField] Vector3 interactUIPositionOffset = new Vector3(0f, 1f, 0f);

        // ── Legacy height-only offset ────────────────────────────────────────────────
        // Hidden, and read exactly once. It exists only so the height hand-tuned on every existing
        // hotspot survives becoming a Vector3: Unity cannot convert a serialized float into one, so
        // without this each of them would deserialize to (0,0,0) and silently drop its prompt to the
        // hotspot's own origin. MigrateUIOffset lifts the old value across, then sets the flag and
        // neither is touched again.
        [SerializeField, HideInInspector] float interactUIOffset = 1f;
        [SerializeField, HideInInspector] bool interactUIOffsetMigrated;

        [Tooltip("Text shown on the interaction indicator when the player is in range.")]
        [SerializeField] protected string interactionLabel = "Interact";

        protected GameObject interactUI;
        protected TMP_Text interactText;

        public Action OnStart;
        public Action OnComplete;

        public UnityEvent OnStartInteractionEvent;
        public UnityEvent OnEndInteractionEvent;



        /// <summary>
        /// World position of the interaction prompt. The Y offset is applied straight up so the
        /// prompt never tips over with the object, while X and Z are rotated into the hotspot's
        /// heading so a lateral offset follows it around. A pure height offset — which is what every
        /// hotspot authored before this was a Vector3 has — resolves identically either way.
        /// </summary>
        public Vector3 InteractUIPosition =>
            transform.position + Quaternion.Euler(0f, transform.eulerAngles.y, 0f) * interactUIPositionOffset;

        /// <summary>
        /// Lifts the old height-only offset into the Vector3, once. Runs on load in the editor so the
        /// inspector always shows the real value, and in Awake so a build made before the scene was
        /// ever re-saved still places its prompts correctly.
        /// </summary>
        void MigrateUIOffset()
        {
            if (interactUIOffsetMigrated) return;

            interactUIPositionOffset = new Vector3(0f, interactUIOffset, 0f);
            interactUIOffsetMigrated = true;
        }

#if UNITY_EDITOR
        protected virtual void OnValidate() => MigrateUIOffset();
#endif

        protected virtual void Awake()
        {
            MigrateUIOffset();

            if(interactUIPrefab == null)
            {
                interactUIPrefab = (GameObject)Resources.Load("UI/Interact UI");
            }
        }

        protected virtual void Start() { }

        protected virtual void OnEnable()
        {
            gameObject.layer = LayerMask.NameToLayer("Hotspot");
            if (transform.Find("FS_InteractUI") != null)
            {
                interactUI = transform.Find("FS_InteractUI").gameObject;
            }
            if (interactUIPrefab != null && interactUI == null)
            {
                interactUI = Instantiate(interactUIPrefab, InteractUIPosition, Quaternion.identity);
                var interactUIScale = interactUI.transform.lossyScale;
                interactUI.transform.SetParent(transform);
                SetGlobalScale(interactUI.gameObject.transform, interactUIScale);
                interactUI.name = "FS_InteractUI"; 
            }
            if (interactUI != null)
            {
                interactText = interactUI.GetComponentInChildren<TMP_Text>();
                interactUI.gameObject.SetActive(false);
            }
        }

        protected virtual void Update()
        {
            
        }

        protected virtual void LateUpdate()
        {
            if (interactUI != null)
            {
                // Driven from the hotspot every frame rather than parented in place, so the prompt
                // keeps its authored offset while the object it sits on moves, turns or tilts.
                interactUI.transform.position = InteractUIPosition;
            }
        }

        // We can use this to prevent the player from interacting with this hotspot in certain conditions
        // Example: Player should not be able to interact with a door while it's already playing open or close animation
        public virtual bool CanInteract()
        {
            return true;
        }
        public virtual void Interact(HotspotController detector)
        {

        }

        /// <summary>Called when this hotspot becomes the controller's closest candidate.</summary>
        public virtual void OnGainFocus(HotspotController detector) { }

        /// <summary>Called when this hotspot is no longer the controller's closest candidate.</summary>
        public virtual void OnLoseFocus(HotspotController detector) { }

        public virtual void ShowIndicator(bool state)
        {
            if (interactUI != null)
                interactUI.SetActive(state);

            if (state && interactText != null)
                interactText.text = interactionLabel;
        }


        void SetGlobalScale(Transform target, Vector3 desiredGlobalScale)
        {
            Vector3 currentGlobalScale = target.lossyScale;

            if (currentGlobalScale == Vector3.zero)
            {
                Debug.LogWarning("Cannot set global scale when current global scale is zero.");
                return;
            }

            Vector3 scaleRatio = new Vector3(
                desiredGlobalScale.x / currentGlobalScale.x,
                desiredGlobalScale.y / currentGlobalScale.y,
                desiredGlobalScale.z / currentGlobalScale.z
            );

            target.localScale = new Vector3(
                target.localScale.x * scaleRatio.x,
                target.localScale.y * scaleRatio.y,
                target.localScale.z * scaleRatio.z
            );
        }
    }
}
