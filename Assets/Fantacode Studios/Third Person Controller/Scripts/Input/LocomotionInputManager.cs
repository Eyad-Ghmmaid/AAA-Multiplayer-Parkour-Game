using FS_Core;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace FS_LocomotionSystem
{
    public class LocomotionInputManager : FSInputManagerBase
    {
        // Set by FSNetworkInputBridge when the network player receives input from
        // an external source. The bridge writes the public input properties; this
        // manager must then avoid polling hardware and overwriting those values.
        public bool useExternalInput;

        [SerializeField] LocomotionUI locomotionUI;
        [SerializeField] UIJoystick moveJoystick;
        [SerializeField] MobileCameraInputController cameraInputArea;
        [SerializeField] FSInput jumpInput = new FSInput(KeyCode.Space);
        [SerializeField] FSInput dropInput = new FSInput(KeyCode.R);
        [SerializeField] FSInput moveTypeInput = new FSInput(KeyCode.Tab);
        [SerializeField] FSInput sprintInput = new FSInput(KeyCode.LeftShift);
        [SerializeField] FSInput quickSwitchItemInput = new FSInput(KeyCode.CapsLock);
        [SerializeField] FSInput crouchInput = new FSInput(KeyCode.C);
        [SerializeField] FSInput cycleToPreviousItemInput = new FSInput(KeyCode.Alpha3);
        [SerializeField] FSInput cycleToNextItemInput = new FSInput(KeyCode.Alpha4);
        [SerializeField] FSInput itemThrowInput = new FSInput(KeyCode.Mouse0);
        [SerializeField] FSInput itemThrowCancelInput = new FSInput(KeyCode.X);


        public bool JumpHold { get; set; }
        public bool JumpKeyDown { get; set; }
        public bool Drop { get; set; }
        public Vector2 DirectionInput { get; set; }
        public Vector2 CameraInput { get; set; }
        public bool ToggleRun { get; set; }
        public bool SprintKey { get; set; }
        public bool QuickSwitchItemDown { get; set; }
        public bool QuickSwitchItemUp { get; set; }
        public bool Crouch { get; set; }
        public bool CycleToPreviousItem { get; set; }
        public bool CycleToNextItem { get; set; }
        public Vector2 MousePosition { get; set; }
        public bool ItemThrowDown { get; set; }
        public bool ItemThrowHold { get; set; }
        public bool ItemThrowUp { get; set; }
        public bool ItemThrowCancelDown { get; set; }

        public event Action OnInteractionPressed;
        public event Action<float> OnInteractionReleased;

        public float InteractionButtonHoldTime { get; set; } = 0f;

        public string InteractionKeyName { get; private set; }

        public void SetLocomotionUI(LocomotionUI ui)
        {
            locomotionUI = ui;
            CacheUIInputs();
        }

        protected override void CacheUIInputs()
        {
            if (locomotionUI == null) return;
                 
            locomotionUI.CacheButtons();

            jumpInput.SetUIButton(locomotionUI.JumpUIButton);
            dropInput.SetUIButton(locomotionUI.DropUIButton);
            moveTypeInput.SetUIButton(locomotionUI.MoveTypeUIButton);
            sprintInput.SetUIButton(locomotionUI.SprintUIButton);
            quickSwitchItemInput.SetUIButton(locomotionUI.QuickSwitchItemUIButton);
            crouchInput.SetUIButton(locomotionUI.CrouchUIButton);
            cycleToPreviousItemInput.SetUIButton(locomotionUI.CycleToPreviousItemUIButton);
            cycleToNextItemInput.SetUIButton(locomotionUI.CycleToNextItemUIButton);
            itemThrowInput.SetUIButton(locomotionUI.ItemThrowUIButton);
            itemThrowCancelInput.SetUIButton(locomotionUI.ItemThrowCancelUIButton);
        }

        private void Update()
        {
            if (useExternalInput)
                return;

            HandleQuickSwitchUIInput();

            if (Time.deltaTime == 0) return;

            HandleItemCycling();
            HandleDirectionalInput();
            HandlecameraInput();
            HandleJump();
            HandleDrop();
            HandleToggleRun();
            HandleSprint();
            HandleCrouch();
            HandleThrow();
        }

        public void HandleDirectionalInput()
        {
            Vector2 dir;
#if inputsystem
            dir = input.Locomotion.MoveInput.ReadValue<Vector2>();
#else
            float h = Input.GetAxisRaw("Horizontal");
            float v = Input.GetAxisRaw("Vertical");
            dir = new Vector2(h, v);
#endif

            // Mobile on-screen joystick — overrides keyboard/gamepad when active.
            // Gate on value magnitude rather than IsPressed: another component
            // (e.g., UIImageInteraction) on the joystick GameObject can swallow
            // OnPointerDown, leaving IsPressed=false even while the joystick is
            // actively dragged.
            var js = ResolveMoveJoystick();
            if (js != null && js.Value.sqrMagnitude > 0.0001f)
                dir = js.Value;

            DirectionInput = Vector2.ClampMagnitude(dir, 1f);
        }

        UIJoystick ResolveMoveJoystick()
        {
            if (moveJoystick != null) return moveJoystick;
            if (locomotionUI != null && locomotionUI.MoveJoystick != null)
            {
                moveJoystick = locomotionUI.MoveJoystick;
                return moveJoystick;
            }
            // Last-resort scene search (cached after first hit).
            moveJoystick = FindAnyObjectByType<UIJoystick>(FindObjectsInactive.Include);
            return moveJoystick;
        }

        void HandlecameraInput()
        {
#if !UNITY_ANDROID && !UNITY_IOS
#if inputsystem
            
            CameraInput = input.Locomotion.CameraInput.ReadValue<Vector2>();
            
#else
            float x = Input.GetAxis("Mouse X");
            float y = Input.GetAxis("Mouse Y");
            CameraInput = new Vector2(x, y) * 10;
#endif
#endif
        }

        void HandleJump()
        {
#if inputsystem
            JumpKeyDown = ReadButtonDown(jumpInput, () => input.Locomotion.Jump.WasPressedThisFrame());
            JumpHold = ReadButton(jumpInput, () => input.Locomotion.Jump.inProgress);
#else
            JumpKeyDown = ReadButtonDown(jumpInput);
            JumpHold = ReadButton(jumpInput);
#endif
        }

        void HandleDrop()
        {
#if inputsystem
            Drop = ReadButton(dropInput, () => input.Locomotion.Drop.inProgress);
#else
            Drop = ReadButton(dropInput);
#endif
        }

        void HandleToggleRun()
        {
#if inputsystem
            ToggleRun = ReadButtonDown(moveTypeInput, () => input.Locomotion.MoveType.WasPressedThisFrame());
#else
            ToggleRun = ReadButtonDown(moveTypeInput);
#endif
        }

        void HandleSprint()
        {
#if inputsystem
            SprintKey = ReadButton(sprintInput, () => input.Locomotion.SprintKey.inProgress);
#else
            SprintKey = ReadButton(sprintInput);
#endif
        }

        void HandleQuickSwitchUIInput()
        {
#if inputsystem
            QuickSwitchItemDown = ReadButtonDown(quickSwitchItemInput, () => input.Locomotion.QuickSwitchItem.WasPressedThisFrame());
            QuickSwitchItemUp = ReadButtonUp(quickSwitchItemInput, () => input.Locomotion.QuickSwitchItem.WasReleasedThisFrame());
            // Use Pointer (base of Mouse/Pen/Touchscreen) so this resolves to the
            // touchscreen on mobile. Mouse.current is null on mobile and would throw.
            var pointer = UnityEngine.InputSystem.Pointer.current;
            MousePosition = pointer != null ? pointer.position.ReadValue() : Vector2.zero;
#else
            QuickSwitchItemDown = ReadButtonDown(quickSwitchItemInput);
            QuickSwitchItemUp = ReadButtonUp(quickSwitchItemInput);
            MousePosition = Input.mousePosition;
#endif
        }

        private void HandleCrouch()
        {
#if inputsystem
            Crouch = ReadButtonDown(crouchInput, () => input.Locomotion.Crouch.WasPressedThisFrame());
#else
            Crouch = ReadButtonDown(crouchInput);
#endif
        }

        void HandleThrow()
        {
#if inputsystem
            ItemThrowDown = ReadButtonDown(itemThrowInput, () => input.Locomotion.Throw.WasPressedThisFrame());
            ItemThrowUp = ReadButtonUp(itemThrowInput, () => input.Locomotion.Throw.WasReleasedThisFrame());
            ItemThrowHold = ReadButton(itemThrowInput, () => input.Locomotion.Throw.inProgress);
            ItemThrowCancelDown = ReadButtonDown(itemThrowCancelInput, () => input.Locomotion.ThrowCancel.WasPressedThisFrame());
#else
            ItemThrowDown = ReadButtonDown(itemThrowInput);
            ItemThrowUp = ReadButtonUp(itemThrowInput);
            ItemThrowHold = ReadButton(itemThrowInput);
            ItemThrowCancelDown = ReadButtonDown(itemThrowCancelInput);
#endif
        }

        private void HandleItemCycling()
        {
#if inputsystem
            CycleToPreviousItem = ReadButtonDown(cycleToPreviousItemInput, () => input.Locomotion.CyclePreviousItem.WasPressedThisFrame());
            CycleToNextItem = ReadButtonDown(cycleToNextItemInput, () => input.Locomotion.CycleNextItem.WasPressedThisFrame());
#else
            CycleToPreviousItem = ReadButtonDown(cycleToPreviousItemInput);
            CycleToNextItem = ReadButtonDown(cycleToNextItemInput);
#endif
        }
    }
}
