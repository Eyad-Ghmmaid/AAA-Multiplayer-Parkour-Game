using UnityEngine;

namespace FS_Core
{

    public class CharacterCapsule : MonoBehaviour
    {
        float controllerDefaultHeight = .87f;
        float controllerDefaultYOffset = 1.7f;

        CharacterController characterController;
        private void Awake()
        {
            characterController = GetComponent<CharacterController>();
            if (characterController == null) return;

            controllerDefaultHeight = characterController.height;
            controllerDefaultYOffset = characterController.center.y;
        }

        public void SetColliderSize(float height, float yOffset)
        {
            if (characterController == null) return;

            characterController.height = height;
            characterController.center = new Vector3(characterController.center.x, yOffset, characterController.center.z);
        }

        public void ScaleColliderSize(float scaleFactor)
        {
            if (characterController == null) return;

            characterController.height = controllerDefaultHeight * scaleFactor;
            characterController.center = new Vector3(characterController.center.x, controllerDefaultYOffset * scaleFactor, characterController.center.z);
        }

        public void ResetColliderSize()
        {
            if (characterController == null) return;

            characterController.height = controllerDefaultHeight;
            characterController.center = new Vector3(characterController.center.x, controllerDefaultYOffset, characterController.center.z);
        }
    }
}
