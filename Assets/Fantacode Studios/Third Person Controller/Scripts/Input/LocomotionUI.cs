using FS_Core;
using UnityEngine;

namespace FS_LocomotionSystem
{
    public class LocomotionUI : FSInputUIBase
    {
        [Header("UI Joystick")]
        [SerializeField] UIJoystick moveJoystick;
        public UIJoystick MoveJoystick => moveJoystick;

        [Header("UI Buttons")]
        [SerializeField] UIImageInteraction jumpUIButton;
        [SerializeField] UIImageInteraction dropUIButton;
        [SerializeField] UIImageInteraction moveTypeUIButton;
        [SerializeField] UIImageInteraction sprintUIButton;
        [SerializeField] UIImageInteraction quickSwitchItemUIButton;
        [SerializeField] UIImageInteraction crouchUIButton;
        [SerializeField] UIImageInteraction cycleToPreviousItemUIButton;
        [SerializeField] UIImageInteraction cycleToNextItemUIButton;
        [SerializeField] UIImageInteraction itemThrowUIButton;
        [SerializeField] UIImageInteraction itemThrowCancelUIButton;

        public UIImageInteraction JumpUIButton => jumpUIButton;
        public UIImageInteraction DropUIButton => dropUIButton;
        public UIImageInteraction MoveTypeUIButton => moveTypeUIButton;
        public UIImageInteraction SprintUIButton => sprintUIButton;
        public UIImageInteraction QuickSwitchItemUIButton => quickSwitchItemUIButton;
        public UIImageInteraction CrouchUIButton => crouchUIButton;
        public UIImageInteraction CycleToPreviousItemUIButton => cycleToPreviousItemUIButton;
        public UIImageInteraction CycleToNextItemUIButton => cycleToNextItemUIButton;
        public UIImageInteraction ItemThrowUIButton => itemThrowUIButton;
        public UIImageInteraction ItemThrowCancelUIButton => itemThrowCancelUIButton;

        public override void CacheButtons()
        {
            if (moveJoystick == null)
                moveJoystick = FindAnyObjectByType<UIJoystick>(FindObjectsInactive.Include);

            jumpUIButton ??= FindButtonInContainer("Game UI", "Jump");
            dropUIButton ??= FindButtonInContainer("Game UI", "Drop");
            moveTypeUIButton ??= FindButtonInContainer("Game UI", "Move Type");
            sprintUIButton ??= FindButtonInContainer("Game UI", "Sprint");
            quickSwitchItemUIButton ??= FindButtonInContainer("Game UI", "Quick Switch");
            crouchUIButton ??= FindButtonInContainer("Game UI", "Crouch");
            cycleToPreviousItemUIButton ??= FindButtonInContainer("Game UI", "Previous Item", "Previous", "Prev");
            cycleToNextItemUIButton ??= FindButtonInContainer("Game UI", "Next Item", "Next", "NextPage");
            itemThrowUIButton ??= FindButtonInContainer("Game UI", "Throw");
            itemThrowCancelUIButton ??= FindButtonInContainer("Game UI", "Throw Cancel", "Cancel Throw", "Cancel");
        }
    }
}