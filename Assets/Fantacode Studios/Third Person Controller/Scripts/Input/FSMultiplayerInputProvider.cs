using UnityEngine;

#if ENABLE_INPUT_SYSTEM
using Fusion;
using FantacodeStudios.ThirdPersonController;

namespace FantacodeStudios.ThirdPersonController
{
    /// <summary>
    /// Fusion-Integration für FSInputAdapter.
    /// Synchronisiert Input über Netzwerk mit StateAuthority-Gates.
    /// </summary>
    public class FSMultiplayerInputProvider : MonoBehaviour, INetworkInput
    {
        [SerializeField] private FSInputAdapter _inputHandler;

        private void OnEnable()
        {
            if (_inputHandler == null)
            {
                _inputHandler = GetComponent<FSInputAdapter>();
            }
        }

        public void OnInput(NetworkRunner runner, NetworkInput input)
        {
            if (_inputHandler == null)
                return;

            // Nur der InputAuthority (lokale Player) kann Input senden
            var data = new PlayerInputData();

            // === LOCOMOTION INPUT ===
            data.MovementInput = _inputHandler.move;
            data.LookDelta = _inputHandler.look;
            data.Jump = _inputHandler.jump;
            data.Sprint = _inputHandler.sprint;

            // === MULTIPLAYER INPUT ===
            data.PushToTalk = _inputHandler.pushToTalk;

            input.Set(data);
        }

        public struct PlayerInputData : INetworkInput
        {
            public Vector2 MovementInput;
            public Vector2 LookDelta;
            public bool Jump;
            public bool Sprint;
            public bool PushToTalk;
        }
    }
}
#endif
