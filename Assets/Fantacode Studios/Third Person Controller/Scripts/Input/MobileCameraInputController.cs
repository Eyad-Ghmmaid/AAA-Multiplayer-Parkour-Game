using FS_LocomotionSystem;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;

#if inputsystem
using UnityEngine.InputSystem.EnhancedTouch;
// Alias to avoid clashing with UnityEngine.Touch / UnityEngine.TouchPhase used in the legacy path.
using ETouch = UnityEngine.InputSystem.EnhancedTouch.Touch;
using ETouchPhase = UnityEngine.InputSystem.TouchPhase;
#endif

namespace FS_Core
{
    public class MobileCameraInputController : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
    {

        public LocomotionInputManager locomotionInput;
        private int currentTouchId = -1;

        void Awake()
        {
            if(locomotionInput == null)
                locomotionInput = FindAnyObjectByType<LocomotionInputManager>();
        }

#if inputsystem
        // EnhancedTouch.Touch.activeTouches is empty unless support is enabled.
        // Enable/Disable are reference-counted, so per-instance calls are safe.
        private void OnEnable()
        {
            EnhancedTouchSupport.Enable();
        }

        private void OnDisable()
        {
            EnhancedTouchSupport.Disable();
        }
#endif

        private void Update()
        {
#if UNITY_ANDROID || UNITY_IOS
            locomotionInput.CameraInput = Vector2.zero;
#if inputsystem
            if (currentTouchId >= 0 && ETouch.activeTouches.Count > 0)
            {
                bool touchFound = false;
                foreach (var touch in ETouch.activeTouches)
                {
                    if (touch.touchId == currentTouchId)
                    {
                        touchFound = true;

                        if (touch.phase == ETouchPhase.Moved)
                        {
                            locomotionInput.CameraInput = touch.delta;
                        }
                        else if (touch.phase == ETouchPhase.Ended || touch.phase == ETouchPhase.Canceled)
                        {
                            ResetTouch();
                        }
                        
                        break;
                    }

                    
                }
                if (!touchFound)
                {
                    ResetTouch();
                }
            }
            
#else
            if (currentTouchId >= 0 && Input.touchCount > 0)
            {
                bool touchFound = false;
                for (int i = 0; i < Input.touchCount; i++)
                {
                    Touch touch = Input.GetTouch(i);
                    if (touch.fingerId == currentTouchId)
                    {
                        touchFound = true;

                        if (touch.phase == TouchPhase.Moved)
                        {
                            locomotionInput.CameraInput = touch.deltaPosition;
                        }
                        else if (touch.phase == TouchPhase.Ended || touch.phase == TouchPhase.Canceled)
                        {
                            ResetTouch();
                        }
                        break;
                    }
                }

                if (!touchFound)
                {
                    ResetTouch();
                }
            }
#endif
#endif
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            if (currentTouchId != -1) return;

#if inputsystem
            foreach (var touch in ETouch.activeTouches)
            {
                if (touch.phase == ETouchPhase.Began)
                {
                    currentTouchId = touch.touchId;
                    break;
                }
            }
#else
            if (Input.touchCount > 0)
            {
                for (int i = 0; i < Input.touchCount; i++)
                {
                    Touch touch = Input.GetTouch(i);
                    if (touch.phase == TouchPhase.Began)
                    {
                        currentTouchId = touch.fingerId;
                        break;
                    }
                }
            }
#endif
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            ResetTouch();
        }

        private void ResetTouch()
        {
            currentTouchId = -1;
        }
    }
}
