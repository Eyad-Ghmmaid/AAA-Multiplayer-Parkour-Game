using FS_Core;
using UnityEngine;

namespace FS_SwingSystem
{
    public class SwingUI : FSInputUIBase
    {
        [Header("UI Buttons")]
        [SerializeField] UIImageInteraction hookUIButton;
        [SerializeField] UIImageInteraction hookReleaseUIButton;
        [SerializeField] UIImageInteraction ropeClimbModifierUIButton;

        public UIImageInteraction HookUIButton => hookUIButton;
        public UIImageInteraction HookReleaseUIButton => hookReleaseUIButton;
        public UIImageInteraction RopeClimbModifierUIButton => ropeClimbModifierUIButton;

        public override void CacheButtons()
        {
            hookUIButton ??= FindButtonInContainer("Swing Mobile Controller", "Swing Hook");
            hookReleaseUIButton ??= FindButtonInContainer("Swing Mobile Controller", "Hook Release", "Swing Hook");
            ropeClimbModifierUIButton ??= FindButtonInContainer("Swing Mobile Controller", "Climb Up Modifier");
        }
    }
}