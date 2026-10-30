using FS_Core;
using UnityEngine;

namespace FS_ParkourSystem
{
    public partial class ParkourInputManager : FSInputManagerBase
    {
        [SerializeField] ParkourUI parkourUI;
        //[SerializeField] FSInput jumpInput = new FSInput(KeyCode.Space);
        //[SerializeField] FSInput dropInput = new FSInput(KeyCode.E);
        [SerializeField] FSInput jumpFromHangInput = new FSInput(KeyCode.Q);

        //public bool Jump { get; set; }
        //public bool JumpKeyDown { get; set; }
        //public bool Drop { get; set; }
        public bool JumpFromHang { get; set; }

        public void SetParkourUI(ParkourUI ui)
        {
            parkourUI = ui;
            CacheUIInputs();
        }

        protected override void CacheUIInputs()
        {
            if (parkourUI == null) return;

            parkourUI.CacheButtons();

            //jumpInput.SetUIButton(parkourUI.JumpUIButton);
            //dropInput.SetUIButton(parkourUI.DropUIButton);
            jumpFromHangInput.SetUIButton(parkourUI.JumpFromHangUIButton);
        }

        private void Update()
        {
            if (Time.deltaTime == 0) return;

            //HandleJump();
            //HandleJumpKeyDown();
            //HandleDrop();
            HandleJumpFromHang();
        }

//        void HandleJump()
//        {
//#if inputsystem
//            Jump = ReadButton(jumpInput, () => input.Parkour.Jump.inProgress);
//#else
//            Jump = ReadButton(jumpInput);
//#endif
//        }

//        void HandleJumpKeyDown()
//        {
//#if inputsystem
//            JumpKeyDown = ReadButtonDown(jumpInput, () => input.Parkour.Jump.WasPressedThisFrame());
//#else
//            JumpKeyDown = ReadButtonDown(jumpInput);
//#endif
//        }

//        void HandleDrop()
//        {
//#if inputsystem
//            Drop = ReadButton(dropInput, () => input.Parkour.Drop.inProgress);
//#else
//            Drop = ReadButton(dropInput);
//#endif
//        }

        void HandleJumpFromHang()
        {
#if inputsystem
            JumpFromHang = ReadButton(jumpFromHangInput, () => input.Parkour.JumpFromHang.inProgress);
#else
            JumpFromHang = ReadButton(jumpFromHangInput);
#endif
        }
    }
}