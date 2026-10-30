using FS_Util;
using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace FS_Core
{
    [RequireComponent(typeof(Image))]
    public class UIImageInteraction : MonoBehaviour,
        IPointerEnterHandler,
        IPointerExitHandler,
        IPointerClickHandler,
        IPointerDownHandler,
        IPointerUpHandler
    {
        [SerializeField] Image image;
        [SerializeField] RectTransform rect;

        public bool IsHovered { get; private set; }
        public bool IsToggledOn { get; private set; }

        //public Action OnClicked;

        [Header("Scale")]
        public Vector2 normalScale = Vector2.one;
        public Vector2 hoveredScale = Vector2.one * 1.05f;
        public Vector2 pressedScale = Vector2.one * 0.95f;

        [Header("Colors")]
        [ShowIf("isToggleButton", true)]
        public Color toggleEnabledColor = Color.white;
        [ShowIf("isToggleButton", true)]
        public Color toggleDisabledColor = Color.gray;
        public Color pressedColor = Color.green;
        [ShowIf(LogicOperator.And, "isToggleButton", true, "blinkColorWhenToggledOn", true)]
        public Color blinkColor = Color.yellow;

        [Header("Toggle")]
        public bool isToggleButton = false;
        [ShowIf("isToggleButton", true)]
        public bool blinkColorWhenToggledOn = false;
        [ShowIf(LogicOperator.And, "isToggleButton", true, "blinkColorWhenToggledOn", true)]
        public float blinkSpeed = 4f;

        Coroutine blinkRoutine;

        public bool ButtonDown { get; private set; }
        public bool ButtonUp { get; private set; }
        public bool ButtonHolding { get; private set; }

        void Awake()
        {
            if(image == null)
            image = GetComponent<Image>();
            if(rect == null)
            rect = GetComponent<RectTransform>();

            rect.localScale = normalScale;
            ApplyBaseColor();
            SetToggledState(false);
        }

        void Update()
        {
            // Blink handled via coroutine, nothing needed here
        }

        // -------------------- Hover --------------------

        public void OnPointerEnter(PointerEventData eventData)
        {
            IsHovered = true;
            rect.localScale = hoveredScale;
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            IsHovered = false;
            rect.localScale = normalScale;
        }

        // -------------------- Press --------------------

        public async void OnPointerDown(PointerEventData eventData)
        {
            rect.localScale = pressedScale;
            if(isToggleButton)
                image.color = pressedColor;
            ButtonDown = true;
            ButtonHolding = true;
            await AsyncUtil.RunAfterFramesAsync(1, () => ButtonDown = false);
        }

        public async void OnPointerUp(PointerEventData eventData)
        {
            rect.localScale = IsHovered ? hoveredScale : normalScale;
            ApplyBaseColor();
            ButtonUp = true;
            ButtonHolding = false;
            SetToggledState(IsToggledOn);
            await AsyncUtil.RunAfterFramesAsync(1, () => ButtonUp = false );
        }

        // -------------------- Click --------------------

        public void OnPointerClick(PointerEventData eventData)
        {
            if (isToggleButton)
            {
                IsToggledOn = !IsToggledOn;
                HandleBlinkState();
            }

            ApplyBaseColor();
            //OnClicked?.Invoke();
        }

        public void SetToggledState(bool toggledOn)
        {
            if (!isToggleButton)
                return;
            IsToggledOn = toggledOn;
            HandleBlinkState();
            ApplyBaseColor();
        }

        // -------------------- Color Handling --------------------

        void ApplyBaseColor()
        {
            if (!isToggleButton)
                return;

            image.color = IsToggledOn ? toggleEnabledColor : toggleDisabledColor;
        }

        void HandleBlinkState()
        {
            if (!blinkColorWhenToggledOn)
                return;

            if (IsToggledOn)
            {
                if (blinkRoutine != null)
                    StopCoroutine(blinkRoutine);

                blinkRoutine = StartCoroutine(BlinkColor());
            }
            else
            {
                if (blinkRoutine != null)
                    StopCoroutine(blinkRoutine);

                blinkRoutine = null;
            }
        }

        IEnumerator BlinkColor()
        {
            while (IsToggledOn)
            {
                image.color = Color.Lerp(toggleEnabledColor, blinkColor,
                    Mathf.PingPong(Time.unscaledTime * blinkSpeed, 1f));
                yield return null;
            }
        }

        void OnDisable()
        {
            if (blinkRoutine != null)
                StopCoroutine(blinkRoutine);

            blinkRoutine = null;
        }
    }
}
