using UnityEngine;
using System;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace FantacodeStudios.ThirdPersonController
{
    /// <summary>
    /// CMP Kompatibilität-Wrapper für FS Input System.
    /// Bietet StarterAssetsInputs-kompatible API für CMP-Komponenten.
    /// </summary>
    public class FSInputAdapter : MonoBehaviour
    {
        [Header("Voice Input")]
        public bool pushToTalk { get; set; }

        [Header("UI State")]
        public bool cursorLocked { get; set; } = true;
        public bool cursorInputForLook { get; set; } = true;

        public Vector2 move { get; private set; }
        public Vector2 look { get; private set; }
        public bool jump { get; private set; }
        public bool jumpHold { get; private set; }
        public bool sprint { get; private set; }

        public event Action<bool> OnPushToTalkChanged;
        public event Action OnToggleCursor;
        public event Action OnOpenPlayerStats;
        public event Action OnOpenChat;

        private bool _lastPushToTalkState = false;
        private bool _lastJumpState = false;

        private void Update()
        {
            var currentMoveInput = ReadMoveInput();
            move = Vector2.ClampMagnitude(currentMoveInput, 1f);

            look = cursorInputForLook ? ReadLookInput() : Vector2.zero;

            bool currentJumpInput = ReadKeyHeld(KeyCode.Space);
            jump = currentJumpInput && !_lastJumpState;
            jumpHold = currentJumpInput;
            _lastJumpState = currentJumpInput;

            sprint = ReadKeyHeld(KeyCode.LeftShift) || ReadKeyHeld(KeyCode.RightShift);

            HandlePushToTalk();
            HandleKeyboardShortcuts();
        }

        private Vector2 ReadMoveInput()
        {
#if ENABLE_INPUT_SYSTEM
            Vector2 keyboardMove = Vector2.zero;
            var keyboard = Keyboard.current;
            if (keyboard != null)
            {
                if (keyboard.aKey.isPressed) keyboardMove.x -= 1f;
                if (keyboard.dKey.isPressed) keyboardMove.x += 1f;
                if (keyboard.sKey.isPressed) keyboardMove.y -= 1f;
                if (keyboard.wKey.isPressed) keyboardMove.y += 1f;
            }

            var gamepad = Gamepad.current;
            if (gamepad != null)
            {
                var stick = gamepad.leftStick.ReadValue();
                if (stick.sqrMagnitude > keyboardMove.sqrMagnitude)
                    return stick;
            }

            return keyboardMove;
#else
            float horizontal = Input.GetAxis("Horizontal");
            float vertical = Input.GetAxis("Vertical");
            return new Vector2(horizontal, vertical);
#endif
        }

        private Vector2 ReadLookInput()
        {
#if ENABLE_INPUT_SYSTEM
            Vector2 lookValue = Vector2.zero;
            var mouse = Mouse.current;
            if (mouse != null)
                lookValue += mouse.delta.ReadValue();

            var gamepad = Gamepad.current;
            if (gamepad != null)
                lookValue += gamepad.rightStick.ReadValue();

            return lookValue;
#else
            float mouseX = Input.GetAxis("Mouse X");
            float mouseY = Input.GetAxis("Mouse Y");
            return new Vector2(mouseX, mouseY);
#endif
        }

        private bool ReadKeyHeld(KeyCode key)
        {
#if ENABLE_INPUT_SYSTEM
            var keyboard = Keyboard.current;
            if (keyboard == null)
                return false;

            return key switch
            {
                KeyCode.Space => keyboard.spaceKey.isPressed,
                KeyCode.LeftShift => keyboard.leftShiftKey.isPressed,
                KeyCode.RightShift => keyboard.rightShiftKey.isPressed,
                KeyCode.V => keyboard.vKey.isPressed,
                _ => false
            };
#else
            return Input.GetKey(key);
#endif
        }

        private bool ReadKeyDown(KeyCode key)
        {
#if ENABLE_INPUT_SYSTEM
            var keyboard = Keyboard.current;
            if (keyboard == null)
                return false;

            return key switch
            {
                KeyCode.Escape => keyboard.escapeKey.wasPressedThisFrame,
                KeyCode.P => keyboard.pKey.wasPressedThisFrame,
                KeyCode.T => keyboard.tKey.wasPressedThisFrame,
                _ => false
            };
#else
            return Input.GetKeyDown(key);
#endif
        }

        private void HandlePushToTalk()
        {
            bool isPressed = ReadKeyHeld(KeyCode.V);

            if (isPressed && !_lastPushToTalkState)
            {
                _lastPushToTalkState = true;
                pushToTalk = true;
                OnPushToTalkChanged?.Invoke(true);
                Debug.Log("[Voice] Push-to-Talk started");
            }
            else if (!isPressed && _lastPushToTalkState)
            {
                _lastPushToTalkState = false;
                pushToTalk = false;
                OnPushToTalkChanged?.Invoke(false);
                Debug.Log("[Voice] Push-to-Talk stopped");
            }
        }

        private void HandleKeyboardShortcuts()
        {
            if (ReadKeyDown(KeyCode.Escape))
            {
                cursorLocked = !cursorLocked;
                Cursor.lockState = cursorLocked ? CursorLockMode.Locked : CursorLockMode.None;
                Cursor.visible = !cursorLocked;
                OnToggleCursor?.Invoke();
                Debug.Log($"[Input] Cursor: {(cursorLocked ? "LOCKED" : "FREE")}");
            }

            if (ReadKeyDown(KeyCode.P))
            {
                OnOpenPlayerStats?.Invoke();
                Debug.Log("[Input] Open Player Stats");
            }

            if (ReadKeyDown(KeyCode.T))
            {
                OnOpenChat?.Invoke();
                Debug.Log("[Input] Open Chat");
            }
        }

        public void MoveInput(Vector2 input)
        {
            move = input;
        }

        public void LookInput(Vector2 input)
        {
            if (cursorInputForLook)
                look = input;
        }

        public void JumpInput(bool input)
        {
            jump = input;
        }

        public void SprintInput(bool input)
        {
            sprint = input;
        }

        public void EnablePlayerInput()
        {
            enabled = true;
        }

        public void DisablePlayerInput()
        {
            enabled = false;
            move = Vector2.zero;
            look = Vector2.zero;
            jump = false;
            sprint = false;
            pushToTalk = false;
            _lastJumpState = false;
            _lastPushToTalkState = false;
        }
    }
}

