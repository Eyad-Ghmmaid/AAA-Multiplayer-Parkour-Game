using UnityEngine;

namespace AvocadoShark
{
    public class GameUIManager : MonoBehaviour
    {
        public static GameUIManager Instance { get; private set; }

        [Header("Scene UI references")]
        public GameObject gameUIRoot;
        public GameObject quickSwitchCanvas;

        private PlayerLocalActivator localPlayer;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            // default: hide scene UI until a local player registers
            if (gameUIRoot != null) gameUIRoot.SetActive(false);
            if (quickSwitchCanvas != null) quickSwitchCanvas.SetActive(false);
        }

        public void SetLocalPlayer(PlayerLocalActivator player)
        {
            localPlayer = player;
            if (gameUIRoot != null) gameUIRoot.SetActive(true);
            if (quickSwitchCanvas != null) quickSwitchCanvas.SetActive(true);
            // TODO: bind HP bar / abilities here using player reference
        }

        public void ClearLocalPlayer()
        {
            localPlayer = null;
            if (gameUIRoot != null) gameUIRoot.SetActive(false);
            if (quickSwitchCanvas != null) quickSwitchCanvas.SetActive(false);
        }
    }
}
