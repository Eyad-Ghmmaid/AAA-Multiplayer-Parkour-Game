using FS_Core;
using FS_InteractionSystem;
using FS_LocomotionSystem;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace FS_SwingSystem
{
    internal enum RopeClimbStep { None, Up, Down }

    [DefaultExecutionOrder(-5)]
    [RequireComponent(typeof(LocomotionInputManager))]
    public class SwingInputManager : FSInputManagerBase
    {
        [SerializeField] SwingUI swingUI;
        [SerializeField] FSInput hookInput = new FSInput(KeyCode.F);
        [SerializeField] FSInput hookReleaseInput = new FSInput(KeyCode.F);
        [SerializeField] FSInput ropeClimbModifierInput = new FSInput(KeyCode.LeftShift);

        LocomotionInputManager locomotionInputManager;
        bool previousClimbModifier;
        bool previousClimbUp;
        bool previousClimbDown;
        bool usedClimbChord;
        RopeClimbStep climbStepPressed;
        InteractionInputManager interactionInputManager;
        bool receivesNetworkInput;
        bool pollsDevice = true;
        int fireSequence, releaseSequence, climbSequence, jumpSequence, pickupSequence;
        int lastFireSequence, lastReleaseSequence, lastClimbSequence, lastJumpSequence, lastPickupSequence;
        RopeClimbStep lastClimbPress;
        bool pendingFire, pendingRelease, pendingJump, pendingPickup;

        internal void ConfigureNetwork(bool localInput, bool remoteInput)
        {
            pollsDevice = localInput;
            receivesNetworkInput = remoteInput;
        }

        internal void WriteNetworkInput(ref FSPlayerNetworkInput input)
        {
            input.SwingFireSequence = fireSequence;
            input.SwingReleaseSequence = releaseSequence;
            input.SwingClimbSequence = climbSequence;
            input.SwingJumpSequence = jumpSequence;
            input.SwingPickupSequence = pickupSequence;
            input.SwingClimbPress = (byte)lastClimbPress;
            input.SwingClimbHeld = (byte)HeldClimbStep;
            input.SwingClimbModifier = RopeClimbModifierHolding;
        }

        internal void ReadNetworkInput(FSPlayerNetworkInput input, bool hasInput, bool allowInput)
        {
            if (!receivesNetworkInput)
                return;

            HeldClimbStep = hasInput && allowInput ? ReadClimbStep(input.SwingClimbHeld) : RopeClimbStep.None;
            RopeClimbModifierHolding = hasInput && allowInput && input.SwingClimbModifier;
            if (!allowInput)
            {
                pendingFire = pendingRelease = pendingJump = pendingPickup = false;
                climbStepPressed = RopeClimbStep.None;
            }
            if (!hasInput)
                return;

            pendingFire |= MarkPress(input.SwingFireSequence, ref lastFireSequence) && allowInput;
            pendingRelease |= MarkPress(input.SwingReleaseSequence, ref lastReleaseSequence) && allowInput;
            pendingJump |= MarkPress(input.SwingJumpSequence, ref lastJumpSequence) && allowInput;
            pendingPickup |= MarkPress(input.SwingPickupSequence, ref lastPickupSequence) && allowInput;
            if (MarkPress(input.SwingClimbSequence, ref lastClimbSequence) && allowInput)
                climbStepPressed = ReadClimbStep(input.SwingClimbPress);
        }

        private static bool MarkPress(int sequence, ref int previous)
        {
            if (unchecked(sequence - previous) <= 0)
                return false;
            previous = sequence;
            return true;
        }

        private static RopeClimbStep ReadClimbStep(byte value) =>
            value <= (byte)RopeClimbStep.Down ? (RopeClimbStep)value : RopeClimbStep.None;

        private static bool Consume(ref bool pending)
        {
            bool pressed = pending;
            pending = false;
            return pressed;
        }

        internal bool ConsumeFire() => receivesNetworkInput ? Consume(ref pendingFire) : HookInputHolding;
        internal bool ConsumeRelease() => receivesNetworkInput ? Consume(ref pendingRelease) : HookReleaseDown;
        internal bool ConsumeJump() => receivesNetworkInput ? Consume(ref pendingJump) : JumpKeyDown;
        internal bool ConsumePickup() => Consume(ref pendingPickup);

        public Vector2 DirectionInput => locomotionInputManager != null ? locomotionInputManager.DirectionInput : Vector2.zero;
        public bool JumpKeyDown => locomotionInputManager != null && locomotionInputManager.JumpKeyDown;
        public bool HookInputHolding { get; private set; }
        public bool HookReleaseDown { get; private set; }
        public bool RopeClimbModifierHolding { get; private set; }
        internal RopeClimbStep HeldClimbStep { get; private set; }
        internal RopeClimbStep LatestClimbPress => lastClimbPress;
        internal int ClimbPressSequence => climbSequence;
        internal int ReceivedClimbSequence => lastClimbSequence;

        internal RopeClimbStep ConsumeClimbStep()
        {
            RopeClimbStep step = climbStepPressed;
            climbStepPressed = RopeClimbStep.None;
            return step;
        }

        protected override void Awake()
        {
            base.Awake();
            locomotionInputManager = GetComponent<LocomotionInputManager>();
            interactionInputManager = GetComponent<InteractionInputManager>();
        }

        public void SetSwingUI(SwingUI ui)
        {
            swingUI = ui;
            CacheUIInputs();
        }

        protected override void CacheUIInputs()
        {
            if (swingUI == null) return;

            swingUI.CacheButtons();

            hookInput.SetUIButton(swingUI.HookUIButton);
            hookReleaseInput.SetUIButton(swingUI.HookReleaseUIButton);
            ropeClimbModifierInput.SetUIButton(swingUI.RopeClimbModifierUIButton);
        }

        void Update()
        {
            if (!pollsDevice)
                return;

            climbStepPressed = RopeClimbStep.None;
            if (locomotionInputManager != null &&
                (locomotionInputManager.RaceInputLocked || locomotionInputManager.useExternalInput))
            {
                HookInputHolding = HookReleaseDown = RopeClimbModifierHolding = false;
                previousClimbModifier = previousClimbUp = previousClimbDown = usedClimbChord = false;
                HeldClimbStep = RopeClimbStep.None;
                return;
            }

            bool wasHoldingHook = HookInputHolding;
#if inputsystem
            HookInputHolding = ReadButton(hookInput, () => input.Swing.Hook.inProgress);
            HookReleaseDown = ReadButtonDown(hookReleaseInput, () => input.Swing.HookRelease.WasPerformedThisFrame());
            RopeClimbModifierHolding = ReadButton(ropeClimbModifierInput, () => input.Swing.ClimbModifier.inProgress);
#else
            HookInputHolding = ReadButton(hookInput);
            HookReleaseDown = ReadButtonDown(hookReleaseInput);
            RopeClimbModifierHolding = ReadButton(ropeClimbModifierInput);
#endif

            bool up = DirectionInput.y > 0.1f;
            bool down = DirectionInput.y < -0.1f;
#if ENABLE_INPUT_SYSTEM
            var keyboard = Keyboard.current;
            if (keyboard != null)
            {
                up |= keyboard.eKey.isPressed || keyboard.eKey.wasPressedThisFrame;
                down |= keyboard.sKey.isPressed || keyboard.sKey.wasPressedThisFrame;
            }
#endif
#if ENABLE_LEGACY_INPUT_MANAGER
            up |= Input.GetKey(KeyCode.E) || Input.GetKeyDown(KeyCode.E);
            down |= Input.GetKey(KeyCode.S) || Input.GetKeyDown(KeyCode.S);
#endif
            SampleClimbStep(RopeClimbModifierHolding, up, down);
            if (HookInputHolding && !wasHoldingHook)
                fireSequence++;
            if (HookReleaseDown)
                releaseSequence++;
            if (JumpKeyDown)
                jumpSequence++;
            if (!RopeClimbModifierHolding && interactionInputManager != null && interactionInputManager.InteractionButtonDown())
                pickupSequence++;
            if (climbStepPressed != RopeClimbStep.None)
            {
                lastClimbPress = climbStepPressed;
                climbSequence++;
            }
        }

        private void SampleClimbStep(bool modifier, bool up, bool down)
        {
            bool upHeld = modifier && up;
            bool downHeld = modifier && down;
            HeldClimbStep = downHeld ? RopeClimbStep.Down : upHeld ? RopeClimbStep.Up : RopeClimbStep.None;
            if (modifier)
                usedClimbChord = (previousClimbModifier && usedClimbChord) || upHeld || downHeld;

            if (downHeld && !previousClimbDown)
                climbStepPressed = RopeClimbStep.Down;
            else if (upHeld && !previousClimbUp)
                climbStepPressed = RopeClimbStep.Up;
            else if (previousClimbModifier && !modifier && !usedClimbChord)
                climbStepPressed = RopeClimbStep.Up;

            previousClimbModifier = modifier;
            previousClimbUp = upHeld;
            previousClimbDown = downHeld;
        }
    }
}
