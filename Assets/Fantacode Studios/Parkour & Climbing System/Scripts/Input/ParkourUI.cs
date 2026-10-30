using FS_Core;
using UnityEngine;

namespace FS_ParkourSystem
{
    public class ParkourUI : FSInputUIBase
    {
        [Header("UI Buttons")]
        //[SerializeField] UIImageInteraction jumpUIButton;
        //[SerializeField] UIImageInteraction dropUIButton;
        [SerializeField] UIImageInteraction jumpFromHangUIButton;

        //public UIImageInteraction JumpUIButton => jumpUIButton;
        //public UIImageInteraction DropUIButton => dropUIButton;
        public UIImageInteraction JumpFromHangUIButton => jumpFromHangUIButton;

        public override void CacheButtons()
        {
            //jumpUIButton ??= FindButtonInContainer("Game UI", "Jump");
            //dropUIButton ??= FindButtonInContainer("Game UI", "Drop");
            jumpFromHangUIButton ??= FindButtonInContainer("Game UI", "Jump From Hang");
        }
    }
}