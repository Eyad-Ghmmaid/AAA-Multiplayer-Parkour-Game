using FS_Core;
using UnityEngine;

namespace FS_InteractionSystem
{
    public class InteractionUI : FSInputUIBase
    {
        [Header("UI Buttons")]
        [SerializeField] UIImageInteraction interactUIButton;
        [SerializeField] UIImageInteraction throwUIButton;
        [SerializeField] UIImageInteraction dropUIButton;

        public UIImageInteraction InteractionUIButton => interactUIButton;
        public UIImageInteraction ThrowUIButton => throwUIButton;
        public UIImageInteraction DropUIButton => dropUIButton;

        public override void CacheButtons()
        {
            interactUIButton ??= FindButtonInContainer("Game UI", "Interaction", "Interact");
            throwUIButton ??= FindButtonInContainer("Game UI", "Throw");
            dropUIButton ??= FindButtonInContainer("Game UI", "Drop");
        }
    }
}