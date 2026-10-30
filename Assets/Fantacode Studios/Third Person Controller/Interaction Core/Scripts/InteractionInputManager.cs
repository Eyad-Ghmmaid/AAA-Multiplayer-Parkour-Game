using FS_Core;
using UnityEngine;

namespace FS_InteractionSystem
{
    public class InteractionInputManager : FSInputManagerBase
    {
        [SerializeField] InteractionUI interactionUI;
        [SerializeField] FSInput interactInput = new FSInput(KeyCode.E);
        [SerializeField] FSInput throwInput = new FSInput(KeyCode.R);
        [SerializeField] FSInput dropInput = new FSInput(KeyCode.Y);

        public void SetInteractionUI(InteractionUI ui)
        {
            interactionUI = ui;
            CacheUIInputs();
        }

        protected override void CacheUIInputs()
        {
            if (interactionUI == null) return;

            interactionUI.CacheButtons();

            interactInput.SetUIButton(interactionUI.InteractionUIButton);
            throwInput.SetUIButton(interactionUI.ThrowUIButton);
            dropInput.SetUIButton(interactionUI.DropUIButton);
        }

        public bool InteractionButtonDown()
        {
#if inputsystem
            return ReadButtonDown(interactInput, () => input.Interaction.Interaction.WasPressedThisFrame());
#else
            return ReadButtonDown(interactInput);
#endif
        }

        public bool DropButtonDown()
        {
#if inputsystem
            return ReadButtonDown(dropInput, () => input.Interaction.Drop.WasPressedThisFrame());
#else
            return ReadButtonDown(dropInput);
#endif
        }

        public bool ThrowButtonDown()
        {
#if inputsystem
            return ReadButtonDown(throwInput, () => input.Interaction.Throw.WasPressedThisFrame());
#else
            return ReadButtonDown(throwInput);
#endif
        }
    }
}