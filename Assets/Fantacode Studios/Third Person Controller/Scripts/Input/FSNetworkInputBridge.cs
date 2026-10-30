using FS_LocomotionSystem;
using FS_ParkourSystem;
using UnityEngine;

namespace FantacodeStudios.ThirdPersonController
{
    /// <summary>
    /// Bridges the raw local input captured by <see cref="FSInputAdapter"/> into the
    /// FS input managers consumed by the Fantacode controllers (Locomotion/Parkour).
    /// This component must only be enabled on the local player (Input Authority).
    /// </summary>
    [DefaultExecutionOrder(-50)]
    public class FSNetworkInputBridge : MonoBehaviour
    {
        [SerializeField] private FSInputAdapter inputAdapter;
        [SerializeField] private LocomotionInputManager locomotionInput;
        [SerializeField] private ParkourInputManager parkourInput;

        private void Awake()
        {
            if (inputAdapter == null) inputAdapter = GetComponent<FSInputAdapter>();
            if (locomotionInput == null) locomotionInput = GetComponent<LocomotionInputManager>();
            if (parkourInput == null) parkourInput = GetComponent<ParkourInputManager>();
        }

        private void OnEnable()
        {
            if (locomotionInput != null)
                locomotionInput.useExternalInput = true;
        }

        private void OnDisable()
        {
            if (locomotionInput != null)
                locomotionInput.useExternalInput = false;
        }

        private void Update()
        {
            if (inputAdapter == null || locomotionInput == null)
                return;

            // Locomotion input
            locomotionInput.DirectionInput = inputAdapter.move;
            locomotionInput.CameraInput = inputAdapter.look;
            locomotionInput.JumpKeyDown = inputAdapter.jump;
            locomotionInput.JumpHold = inputAdapter.jumpHold;
            locomotionInput.SprintKey = inputAdapter.sprint;

            // Parkour input
            if (parkourInput != null)
                parkourInput.JumpFromHang = inputAdapter.jump;
        }
    }
}
