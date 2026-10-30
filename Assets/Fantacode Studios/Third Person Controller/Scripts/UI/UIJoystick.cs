using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace FS_Core
{
    /// <summary>
    /// On-screen virtual joystick. Exposes X / Y / Value in [-1, 1] like a hardware input axis.
    /// Drop it on a UI Image (the background ring) and assign a child knob RectTransform.
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public class UIJoystick : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler, IEndDragHandler
    {
        [Header("References")]
        [SerializeField] RectTransform background;
        [SerializeField] RectTransform knob;

        [Header("Behavior")]
        [Tooltip("Knob movement radius as a fraction of the background's half-size. 1 = knob reaches the edge.")]
        [Range(0.1f, 1f)] [SerializeField] float handleRange = 1f;

        [Tooltip("Inputs below this magnitude are clamped to 0 (analog deadzone).")]
        [Range(0f, 0.9f)] [SerializeField] float deadZone = 0.05f;

        [Tooltip("If true, the joystick repositions to the touch point on press and returns on release.")]
        [SerializeField] bool dynamicOrigin = false;

        Vector2 input;
        Vector2 defaultAnchoredPos;
        Canvas canvas;
        Camera uiCamera;

        public Vector2 Value => input;
        public float X => input.x;
        public float Y => input.y;
        public bool IsPressed { get; private set; }

        void Awake()
        {
            if (background == null) background = GetComponent<RectTransform>();
            canvas = GetComponentInParent<Canvas>();
            uiCamera = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay
                ? canvas.worldCamera
                : null;

            defaultAnchoredPos = background.anchoredPosition;
            ResetKnob();
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            IsPressed = true;

            if (dynamicOrigin)
            {
                // Move the background to the touch point so the knob originates there.
                if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
                        (RectTransform)background.parent, eventData.position, eventData.pressEventCamera, out var local))
                {
                    background.anchoredPosition = local;
                }
            }

            OnDrag(eventData);
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    background, eventData.position, eventData.pressEventCamera, out var local))
            {
                return;
            }

            Vector2 halfSize = background.rect.size * 0.5f;
            // Local point ranges from -halfSize to +halfSize at the background's edges.
            Vector2 normalized = new Vector2(local.x / halfSize.x, local.y / halfSize.y);

            // Clamp to unit circle so diagonals don't exceed magnitude 1.
            if (normalized.sqrMagnitude > 1f)
                normalized.Normalize();

            // Apply deadzone.
            input = normalized.magnitude < deadZone ? Vector2.zero : normalized;

            // Position knob inside background bounds.
            knob.anchoredPosition = new Vector2(normalized.x * halfSize.x, normalized.y * halfSize.y) * handleRange;
        }

        public void OnPointerUp(PointerEventData eventData) => Release();
        public void OnEndDrag(PointerEventData eventData) => Release();

        void Release()
        {
            IsPressed = false;
            input = Vector2.zero;
            ResetKnob();

            if (dynamicOrigin)
                background.anchoredPosition = defaultAnchoredPos;
        }

        void ResetKnob()
        {
            if (knob != null)
                knob.anchoredPosition = Vector2.zero;
        }

        void OnDisable()
        {
            IsPressed = false;
            input = Vector2.zero;
            ResetKnob();
        }
    }
}
