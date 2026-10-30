using System.Collections;
using System.Collections.Generic;
using UnityEngine;
#if inputsystem
using UnityEngine.InputSystem;
#endif
using UnityEngine.UI;

namespace FC_ParkourSystem
{
    public class ControlPanel : MonoBehaviour
    {
        public GameObject image;
        public Text text;

        string controlPanelKey = "controlPanelKey";
        int isActive;

#if inputsystem
        private InputAction enterAction;
#endif
        private void Awake()
        {
            isActive = PlayerPrefs.GetInt(controlPanelKey);
            ControlPanelController();


#if inputsystem
            enterAction = new InputAction(type: InputActionType.Button, binding: "<Keyboard>/enter");
            enterAction.performed += ctx => OnEnterPressed();
            enterAction.Enable();
#endif
        }
#if inputsystem
        private void OnDestroy()
        {
            if (enterAction != null)
            {
                enterAction.Disable();
                enterAction.Dispose();
            }
        }
#endif
        void Update()
        {
#if inputsystem
// New input system
#else
            // Old input system
            if (Input.GetKeyDown(KeyCode.Return))
            {
                ToggleControlPanel();
            }
#endif
        }

        // Called by new input system
        private void OnEnterPressed()
        {
            ToggleControlPanel();
        }

        private void ToggleControlPanel()
        {
            isActive = image.activeSelf ? 0 : 1;
            PlayerPrefs.SetInt(controlPanelKey, isActive);
            ControlPanelController();
        }

        void ControlPanelController()
        {
            image.SetActive(isActive == 0 ? false : true);
            var t = image.activeSelf ? "disable" : "enable";
            text.text = "Click Enter to " + t + " control panel";
        }
    }
}
