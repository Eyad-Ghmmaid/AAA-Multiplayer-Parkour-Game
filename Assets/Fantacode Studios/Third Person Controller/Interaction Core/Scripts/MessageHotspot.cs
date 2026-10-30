using UnityEngine;

namespace FS_InteractionSystem
{
    public class MessageHotspot : Hotspot
    {
        [SerializeField] string message;

        public override void ShowIndicator(bool state)
        {
            base.ShowIndicator(state);

            if (interactText != null)
                interactText.text = message;
        }

        public override void Interact(HotspotController detector)
        {
            // Do nothing, this is just a message hotspot
        }
    }
}
