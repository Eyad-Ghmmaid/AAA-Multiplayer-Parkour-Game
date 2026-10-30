using UnityEngine;

namespace FS_Core
{
    public class UIInteractionInputManager : FSInputManagerBase
    {
        [SerializeField] FSInput selectInput = new FSInput(KeyCode.E);
        [SerializeField] FSInput backInput = new FSInput(KeyCode.Escape);

        public Vector2 NavigationInput { get; set; }
        public bool Select { get; set; }
        public bool Back { get; set; }

        protected override void CacheUIInputs()
        {
            //BindUIButton(selectInput, "Mobile Controller", "Select", "Use");
            //BindUIButton(backInput, "Mobile Controller", "Back", "BackButton", "Back Button");
        }

        private void Update()
        {
            HandleNavigationInput();
            HandleSelect();
            HandleBack();
        }

        void HandleNavigationInput()
        {
#if inputsystem
            NavigationInput = input.Selection.Navigation.ReadValue<Vector2>();
#else
            float h = Input.GetAxisRaw("Horizontal");
            float v = Input.GetAxisRaw("Vertical");
            NavigationInput = new Vector2(h, v);
#endif
        }

        void HandleSelect()
        {
#if inputsystem
            Select = ReadButtonDown(selectInput, () => input.Selection.Select.WasPressedThisFrame());
#else
            Select = ReadButtonDown(selectInput);
#endif
        }

        void HandleBack()
        {
#if inputsystem
            Back = ReadButtonDown(backInput, () => input.Selection.Back.WasPressedThisFrame());
#else
            Back = ReadButtonDown(backInput);
#endif
        }
    }
}
