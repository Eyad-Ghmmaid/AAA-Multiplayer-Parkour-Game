#if CMPSETUP_COMPLETE
using Fusion;
using UnityEngine.SceneManagement;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using StarterAssets;
using UnityEngine.InputSystem;

namespace AvocadoShark
{
    public class InGame_Manager : NetworkBehaviour
    {
        public int environment1, environment2;
        public TextMeshProUGUI room_name;
        public TextMeshProUGUI players;
        public Image lock_image;
        public Sprite locked_sprite;
        public TextMeshProUGUI lock_status_text;
        public TextMeshProUGUI InfoText;
        [HideInInspector] public float deltaTime;
        private float fps;
        public int ping;
        public NetworkRunner runner;
        
        [Header("Game Pause")]
        [SerializeField] private GameObject pauseMenu;

        [SerializeField] private InputActionAsset inputActionAsset;
        private bool _sceneSwitchInProgress;
        private bool _leavingRoom;
        private void Start()
        {
            if (PlayerPrefs.GetInt("has_pass") == 1)
            {
                lock_image.sprite = locked_sprite;
                lock_status_text.text = "private";
            }
            fps = 1.0f / Time.smoothDeltaTime;

            runner = NetworkRunner.GetRunnerForGameObject(gameObject);

            if (environment1 < 0 || environment1 >= SceneManager.sceneCountInBuildSettings ||
                environment2 < 0 || environment2 >= SceneManager.sceneCountInBuildSettings)
            {
                Debug.LogError($"Environment scene indexes are invalid (Environment 1: {environment1}, Environment 2: {environment2}). Check the Game scene's InGame_Manager settings against Build Settings.", this);
                return;
            }
        }

        public async void LeaveRoom()
        {
            if (_leavingRoom)
                return;

            _leavingRoom = true;
            var activeRunner = runner != null ? runner : Runner;
            try
            {
                if (activeRunner != null && !activeRunner.IsShutdown)
                    await activeRunner.Shutdown();
            }
            catch (System.Exception exception)
            {
                Debug.LogException(exception, this);
            }
            finally
            {
                SceneManager.LoadScene("Menu");
            }

        }
        private float smoothedRTT = 0.0f;
        private void LateUpdate()
        { 
            if(!Object)
                return;
            room_name.text = Runner.SessionInfo.Name;
            players.text = Runner.SessionInfo.PlayerCount + "/" + Runner.SessionInfo.MaxPlayers;
            float newFPS = 1.0f / Time.smoothDeltaTime;
            fps = Mathf.Lerp(fps, newFPS, 0.005f);

            double rttInSeconds = runner.GetPlayerRtt(PlayerRef.None);
            int rttInMilliseconds = (int)(rttInSeconds * 1000);
            smoothedRTT = Mathf.Lerp(smoothedRTT, rttInMilliseconds, 0.005f);
            int ping = (int)smoothedRTT / 2;
            InfoText.text = "Ping: " + ping.ToString() + "\n" + "FPS: " + ((int)fps).ToString();
        }

        public void SwitchScene()
        {
            if (Runner == null || Runner.IsShutdown)
                return;

            if (!HasStateAuthority)
            {
                SceneSwitchRpc();
                return;
            }
            if (!Runner.IsSceneAuthority) 
                return;

            if (_sceneSwitchInProgress)
                return;

            var firstEnvironment = SceneManager.GetSceneByBuildIndex(environment1);
            var secondEnvironment = SceneManager.GetSceneByBuildIndex(environment2);
            int loadedEnvironmentIndex = firstEnvironment.IsValid() && firstEnvironment.isLoaded
                ? environment1
                : secondEnvironment.IsValid() && secondEnvironment.isLoaded
                    ? environment2
                    : -1;

            if (loadedEnvironmentIndex < 0)
            {
                Debug.LogError("Scene switch requested, but neither configured environment scene is loaded.", this);
                return;
            }

            int sceneToLoad = loadedEnvironmentIndex == environment1 ? environment2 : environment1;
            _sceneSwitchInProgress = true;

            // Keep the current level until Fusion has loaded the destination on all peers.
            var loadOperation = Runner.LoadScene(SceneRef.FromIndex(sceneToLoad), LoadSceneMode.Additive);
            loadOperation.AddOnCompleted(_ =>
            {
                if (Runner == null || Runner.IsShutdown)
                {
                    _sceneSwitchInProgress = false;
                    return;
                }

                var destination = SceneManager.GetSceneByBuildIndex(sceneToLoad);
                if (!destination.IsValid() || !destination.isLoaded)
                {
                    Debug.LogError($"Fusion failed to load environment scene index {sceneToLoad}. The current environment was kept.", this);
                    _sceneSwitchInProgress = false;
                    return;
                }

                var unloadOperation = Runner.UnloadScene(SceneRef.FromIndex(loadedEnvironmentIndex));
                unloadOperation.AddOnCompleted(__ => _sceneSwitchInProgress = false);
            });
        }

        [Rpc(RpcSources.Proxies,RpcTargets.StateAuthority)]
        private void SceneSwitchRpc()
        {
            SwitchScene();
        }

        public void PauseGame()
        {
            if (FusionConnection.Instance.TryGetLocalPlayerComponent(out StarterAssetsInputs starterAssetsInputs))
            {
                starterAssetsInputs.DisablePlayerInput();
            }

            pauseMenu.SetActive(true);
            //inputActionAsset.Disable();
        }
        public void ResumeGame()
        {
            pauseMenu.SetActive(false);
            //inputActionAsset.Enable();
            if (FusionConnection.Instance.TryGetLocalPlayerComponent(out StarterAssetsInputs starterAssetsInputs))
            {
                starterAssetsInputs.EnablePlayerInput();
            }
        }
    }
}
#endif
