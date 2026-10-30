#if CMPSETUP_COMPLETE
using UnityEngine;
using Fusion;
using Fusion.Sockets;
using System.Collections.Generic;
using System;
using UnityEngine.UI;
using TMPro;
using System.Collections;
using UnityEngine.SceneManagement;
using Photon.Voice.Fusion;
using Photon.Voice.Unity;
using StarterAssets;
using Fusion.Photon.Realtime;
using UnityEngine.Serialization;

namespace AvocadoShark
{
    public class FusionConnection : MonoBehaviour, INetworkRunnerCallbacks
    {
        #region ThingsAddedByMe

        public List<PlayerRef> playersInSession = new List<PlayerRef>();

        #endregion

        public RoomEntry CurrentEntryBeingEdited;
        public Action OnDestroyRoomEntries;

        public static FusionConnection Instance;

        [SerializeField] private NetworkRunner runnerPrefab;
        public NetworkRunner Runner { get; private set; }

        public bool hasEnteredGameScene = false;

        // Test Player Prefabs for spawning in the game scene.
        [Header("Network Player")]
        [SerializeField] private NetworkObject fsNetworkPlayerPrefab;

        [Header("Player 1")] [SerializeField] public GameObject playerPrefabYellow;
        [Header("Player 2")] [SerializeField] public GameObject playerPrefabRed;
        public CharacterSO characterScriptableObject;
        [Header("Name Entry")] public GameObject mainObject;
        public Button submitButton;
        public TMP_InputField nameField;
        public GameObject characterselectionobject;

        [Header("Room List")] public RoomEntry roomEntryPrefab;
        public GameObject roomListObject;
        public Transform content;
        public Button createRoomButton;
        public TextMeshProUGUI NoRoomsText;
        public TMP_InputField room_search;

        [Header("Room List Refresh (s)")] [SerializeField]
        private float refreshInterval = 2f;

        [Header("Player Spawn Location")] [SerializeField]
        public bool UseCustomLocation;

        [SerializeField] public Vector3 CustomLocation;

        private FusionVoiceClient _fvc;
        private Recorder _recorder;
        private VoiceManager _voiceManager;

        [Header("Loading Screen")] public LoadingScreen loadingScreenScript;

        [Header("Loading Screen")] public PopUp popup;

        [Header("UI")] [SerializeField] private MenuCanvas menuCanvas;

        private bool initialRoomListPopulated = false;
        private List<SessionInfo> _sessionList = new List<SessionInfo>();
        private List<RoomEntry> _roomEntryList = new List<RoomEntry>();
        private bool _initialEnvironmentLoadStarted;

        [HideInInspector] public bool isConnected = false;
        [HideInInspector] public string _playerName = null;
        [HideInInspector] public int nRooms = 0;
        [HideInInspector] public int nPPLOnline = 0;

        public TMP_Dropdown region_select;
        public Button backButton;

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
            }

            room_search.onValueChanged.AddListener(OnSearchTextValueChange);
#if UNITY_2022_3_OR_NEWER
        Application.targetFrameRate = (int)Screen.currentResolution.refreshRateRatio.value;
#else
            Application.targetFrameRate = Screen.currentResolution.refreshRate;
#endif

            int region_n = PlayerPrefs.GetInt("region");
            string region = null;
            if (region_n == 0)
            {
                region = "";
            }
            else if (region_n == 1)
            {
                region = "asia";
            }
            else if (region_n == 2)
            {
                region = "eu";
            }
            else if (region_n == 3)
            {
                region = "jp";
            }
            else if (region_n == 4)
            {
                region = "kr";
            }
            else if (region_n == 5)
            {
                region = "us";
            }
            region_select.value = region_n;
            PhotonAppSettings settings = Resources.Load<PhotonAppSettings>("PhotonAppSettings");
            settings.AppSettings.FixedRegion = region;
        }

        public void ChangeRegion(int regionNum)
        {
            string region = null;
            var settings = Resources.Load<PhotonAppSettings>("PhotonAppSettings");
            region = regionNum switch
            {
                0 => "",
                1 => "asia",
                2 => "eu",
                3 => "jp",
                4 => "kr",
                5 => "us",
                _ => null
            };
            settings.AppSettings.FixedRegion = region;
            PlayerPrefs.SetInt("region", regionNum);
        }
        public void RefreshRoomList()
        {
            InitialRoomListSetup();
        }
        
        private IEnumerator AutoRefreshRoomList()
        {
            while (true)
            {
                RefreshRoomList();
                yield return new WaitForSeconds(refreshInterval);
            }
        }

        public void OnSearchTextValueChange(string value)
        {
            _roomEntryList.RemoveAll(e => e == null);
            if (string.IsNullOrEmpty(value) || string.IsNullOrWhiteSpace(value))
            {
                foreach (var i in _roomEntryList)
                {
                    i.gameObject.SetActive(true);
                }
            }

            foreach (var i in _roomEntryList)
            {
                if (value != null && i.roomName.text.Contains(value))
                    i.gameObject.SetActive(true);
                else
                {
                    i.gameObject.SetActive(false);
                }
            }
        }

        public void CreateRoom()
        {
            PlayerPrefs.SetInt("has_pass", 0);
            loadingScreenScript.gameObject.SetActive(true);
            Invoke(nameof(ContinueCreateRoom), loadingScreenScript.lerpSpeed);
        }
        private void ContinueCreateRoom()
        {
            string sessionName = null;
            string sessionPassword = null;
            int maxPlayers = 2;
            if (IsRoomNameValid())
            {
                sessionName = menuCanvas.GetRoomName();
                sessionPassword = menuCanvas.GetPassword();
                maxPlayers = menuCanvas.GetMaxPlayers();
            }
            else
            {
                int randomInt = UnityEngine.Random.Range(1000, 9999);
                sessionPassword = menuCanvas.GetPassword();
                maxPlayers = menuCanvas.GetMaxPlayers();
                sessionName = "Room-" + randomInt;
            }

            Debug.Log($"Session name is {sessionName}");

            Debug.Log($"maxPlayers is {maxPlayers}");

            if (menuCanvas.IsPasswordEnabled)
            {
                PlayerPrefs.SetInt("has_pass", 1);
                JoinRoom(sessionName, maxPlayers, sessionPassword);
            }
            else
            {
                JoinRoom(sessionName, maxPlayers,string.Empty);
            }

            StopCoroutine(AutoRefreshRoomList());
        }

        private bool IsRoomNameValid()
        {
            return menuCanvas.GetRoomName().Length != 0;
        }

        public bool TryGetLocalPlayerComponent<T>(out T component)
        {
            var localPlayer = Runner.GetPlayerObject(Runner.LocalPlayer);
            if (localPlayer == null)
            {
                component = default;
                return false;
            }
            component = localPlayer.GetComponent<T>();
            return component != null;
        }

        public void OnSessionListUpdated(NetworkRunner runner, List<SessionInfo> sessionList)
        {
            if (initialRoomListPopulated == false)
            {
                //StartCoroutine(AutoRefreshRoomList());
                loadingScreenScript.FadeOutAndDisable();
            }

            _sessionList = sessionList;
            nRooms = sessionList.Count;

            nPPLOnline = 0;
            foreach (var session in sessionList)
            {
                nPPLOnline += session.PlayerCount;
            }

            RefreshRoomList();
        }

        private void InitialRoomListSetup()
        {
            if (roomListObject == null)
                return;
            initialRoomListPopulated = true;
            roomListObject.SetActive(true);

            OnDestroyRoomEntries?.Invoke();
            // Destroy() above is deferred to end of frame, so a null-filter alone would
            // not prune this refresh's entries - everything except the entry currently
            // being edited (which unsubscribes from OnDestroyRoomEntries) was destroyed.
            _roomEntryList.RemoveAll(e => e == null || e != CurrentEntryBeingEdited);

            //foreach (Transform child in content)
            //{
            //    Destroy(child.gameObject);
            //}

            foreach (SessionInfo session in _sessionList)
            {
                if (CurrentEntryBeingEdited != null)
                {
                    if (session.Name == CurrentEntryBeingEdited.SessionInfo.Name)
                    {
                        UpdateCurrentEntryBeingEdited(session);
                        continue;
                    }
                }

                RoomEntry entryScript = Instantiate(roomEntryPrefab, content);
                entryScript.Init(session,this);
                _roomEntryList.Add(entryScript);
            }

            if (_sessionList.Count == 0)
            {
                NoRoomsText.gameObject.SetActive(true);
            }
            else
            {
                NoRoomsText.gameObject.SetActive(false);
            }
        }

        //public void AddPlayerToBannedList(string name) {
        //runner.SessionInfo.UpdateCustomProperties();
        //}

        public void ConnectToRunner()
        {
            loadingScreenScript.gameObject.SetActive(true);
            Invoke(nameof(ContinueConnectToRunner), loadingScreenScript.lerpSpeed);
        }
        private void ContinueConnectToRunner()
        {
            _playerName = nameField.text;
            mainObject.SetActive(false);
            characterselectionobject.SetActive(false);
            if (!SetUpComponents())
                return;

            Runner.JoinSessionLobby(SessionLobby.Shared);
        }

        private bool SetUpComponents()
        {
            if (Runner != null && !Runner.IsShutdown)
                return true;

            if (runnerPrefab == null)
            {
                Debug.LogError("NetworkRunner prefab is not assigned.");
                popup?.ShowPopup("NetworkRunner prefab is not assigned.");
                return false;
            }

            Runner = Instantiate(runnerPrefab);
            if (Runner == null)
            {
                Debug.LogError("Failed to create the NetworkRunner from its prefab.");
                popup?.ShowPopup("Failed to create the network connection.");
                return false;
            }

            _fvc = Runner.GetComponent<FusionVoiceClient>();
            _recorder = Runner.GetComponentInChildren<Recorder>();
            _voiceManager = Runner.GetComponentInChildren<VoiceManager>();
            Runner.AddCallbacks(this);
            return true;
        }

        public async void JoinRoom(string sessionName)
        {
            int buildIndex = -1;

            for (int i = 0; i < SceneManager.sceneCountInBuildSettings; i++)
            {
                string scenePath = SceneUtility.GetScenePathByBuildIndex(i);
                string sceneName = System.IO.Path.GetFileNameWithoutExtension(scenePath);

                if (sceneName == "Game")
                {
                    buildIndex = i;
                    break;
                }
            }

            if (buildIndex < 0)
            {
                popup.ShowPopup("The Game scene is missing from Build Settings.");
                return;
            }

            StopCoroutine(AutoRefreshRoomList());
            if (!SetUpComponents())
                return;

            var result = await Runner.StartGame(new StartGameArgs()
            {
                GameMode = GameMode.Shared,
                SessionName = sessionName,
                Scene = SceneRef.FromIndex(buildIndex),
                // Join only - never create. Without this, a room that closed between the
                // last lobby refresh and this click would be silently re-created here
                // under the same name and WITHOUT its password properties, turning a
                // private room into a public one.
                EnableClientSessionCreation = false,
            });
            if (result.Ok)
                return;

            await ShutdownFailedRunner(Runner);
            popup.ShowPopup(result.ShutdownReason.ToString());
        }

        public async void JoinRoom(string sessionName, int maxPlayers, string password = null)
        {
            int buildIndex = -1;

            // Publishes a salt + PBKDF2 hash, never the password itself: Photon sends
            // session properties to every client browsing the lobby.
            var sessionProperties = new Dictionary<string, SessionProperty>();
            RoomPassword.Apply(sessionProperties, password);

            for (int i = 0; i < SceneManager.sceneCountInBuildSettings; i++)
            {
                string scenePath = SceneUtility.GetScenePathByBuildIndex(i);
                string sceneName = System.IO.Path.GetFileNameWithoutExtension(scenePath);

                if (sceneName == "Game")
                {
                    buildIndex = i;
                    break;
                }
            }

            if (buildIndex < 0)
            {
                popup.ShowPopup("The Game scene is missing from Build Settings.");
                return;
            }

            StopCoroutine(AutoRefreshRoomList());

            if (!SetUpComponents())
                return;

            var result = await Runner.StartGame(new StartGameArgs()
            {
                GameMode = GameMode.Shared,
                SessionName = sessionName,
                Scene = SceneRef.FromIndex(buildIndex),
                SessionProperties = sessionProperties,
                PlayerCount = maxPlayers
            });
            if (result.Ok)
            {
                return;
            }

            await ShutdownFailedRunner(Runner);
            popup.ShowPopup(result.ShutdownReason.ToString());
        }

        private async System.Threading.Tasks.Task ShutdownFailedRunner(NetworkRunner failedRunner)
        {
            try
            {
                if (failedRunner != null && !failedRunner.IsShutdown)
                    await failedRunner.Shutdown();
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
            }
            finally
            {
                if (Runner == failedRunner)
                    Runner = null;
            }
        }

        //private void SpawnPlayerCharacter(NetworkRunner runner)
        //{
        //    if (runner.GetPlayerObject(runner.LocalPlayer) != null)
        //        return;
        //    var playerPrefab = characterScriptableObject.GetSelectedCharacter().character;
        //    var location = !UseCustomLocation
        //        ? new Vector3(UnityEngine.Random.Range(-7.6f, 14.2f), 0,
        //            UnityEngine.Random.Range(-31.48f, -41.22f))
        //        : CustomLocation;
        //    var playerObject = runner.Spawn(playerPrefab,location);
        //    //playerObject.transform.position = location;


        //    _voiceManager.Init(playerObject.GetComponent<StarterAssetsInputs>(),
        //        playerObject.GetComponent<PlayerWorldUIManager>());


        //    runner.SetPlayerObject(runner.LocalPlayer, playerObject.Object);
        //}
        private void SpawnPlayerCharacter(NetworkRunner runner)
        {
            if (runner.GetPlayerObject(runner.LocalPlayer) != null)
                return;

            if (fsNetworkPlayerPrefab == null)
            {
                Debug.LogError("FS Network Player Prefab is not assigned.");
                return;
            }

            var location = !UseCustomLocation
                ? new Vector3(UnityEngine.Random.Range(-7.6f, 14.2f), 0,
                    UnityEngine.Random.Range(-31.48f, -41.22f))
                : CustomLocation;

            // The FS controller is a child of the network root and has a negative local Y offset.
            // Treat location.y as the floor height, then lift the network root so the capsule starts
            // on that floor instead of inheriting the prefab's child offset below it.
            var characterController = fsNetworkPlayerPrefab.GetComponentInChildren<CharacterController>();
            if (characterController != null)
            {
                var controllerBottom = characterController.transform.TransformPoint(
                    characterController.center + Vector3.down * (characterController.height * 0.5f));
                var bottomOffsetFromNetworkRoot = fsNetworkPlayerPrefab.transform
                    .InverseTransformPoint(controllerBottom).y;
                location.y += -bottomOffsetFromNetworkRoot + characterController.skinWidth;
            }
            else
            {
                Debug.LogWarning("FS Network Player has no CharacterController; spawning without floor-height correction.");
            }

            var playerObject = runner.Spawn(
                fsNetworkPlayerPrefab,
                location,
                Quaternion.identity);

            runner.SetPlayerObject(runner.LocalPlayer, playerObject);
        }

        #region INetworkCallbacks

        public void OnConnectedToServer(NetworkRunner runner)
        {
            Debug.Log("OnConnectedToServer");
            isConnected = true;
        }

        public void OnDisconnectedFromServer(NetworkRunner runner, NetDisconnectReason reason)
        {
            isConnected = false;
        }

        public void OnConnectFailed(NetworkRunner runner, NetAddress remoteAddress, NetConnectFailedReason reason)
        {
            popup.ShowPopup(reason.ToString());
        }

        public void OnConnectRequest(NetworkRunner runner, NetworkRunnerCallbackArgs.ConnectRequest request,
            byte[] token)
        {
        }

        public void OnCustomAuthenticationResponse(NetworkRunner runner, Dictionary<string, object> data)
        {
        }

        public void OnHostMigration(NetworkRunner runner, HostMigrationToken hostMigrationToken)
        {
        }

        // Fusion 2.0 signature. Kept alongside the 2.1 overload below so the same
        // script compiles no matter which Fusion 2.x the user has installed.
        public void OnReliableDataReceived(NetworkRunner runner, PlayerRef player, ReliableKey key,
            ArraySegment<byte> data)
        {
        }

        // Fusion 2.1 signature (reliable data now arrives as ReadOnlySpan<byte>).
        public void OnReliableDataReceived(NetworkRunner runner, PlayerRef player, ReliableKey key,
            ReadOnlySpan<byte> data)
        {
        }

        public void OnReliableDataProgress(NetworkRunner runner, PlayerRef player, ReliableKey key, float progress)
        {
        }

        public void OnInput(NetworkRunner runner, NetworkInput input)
        {
        }

        public void OnInputMissing(NetworkRunner runner, PlayerRef player, NetworkInput input)
        {
        }

        public void OnObjectExitAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player)
        {
        }

        public void OnObjectEnterAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player)
        {
        }

        public void OnPlayerJoined(NetworkRunner runner, PlayerRef player)
        {
            if (!playersInSession.Contains(player))
                playersInSession.Add(player);
        }

        public void OnPlayerLeft(NetworkRunner runner, PlayerRef player)
        {
            playersInSession.Remove(player);
        }
        public void OnSceneLoadDone(NetworkRunner runner)
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            
            Debug.Log("Scene Load Done.");
            if (hasEnteredGameScene)
                return;

            // Wait until the shared parkour environment is ready on this client.
            // Otherwise a joining player's controller can simulate gravity before
            // the floor exists locally and the proxy appears to start in the air.
            int environment1BuildIndex = FindBuildIndex("Environment 1");
            int environment2BuildIndex = FindBuildIndex("Environment 2");
            if (environment1BuildIndex < 0 || environment2BuildIndex < 0)
            {
                Debug.LogError("'Environment 1' and/or 'Environment 2' is missing from Build Settings. The player was not spawned.");
                return;
            }

            var environment1Scene = SceneManager.GetSceneByBuildIndex(environment1BuildIndex);
            var environment2Scene = SceneManager.GetSceneByBuildIndex(environment2BuildIndex);
            bool anEnvironmentIsLoaded =
                (environment1Scene.IsValid() && environment1Scene.isLoaded) ||
                (environment2Scene.IsValid() && environment2Scene.isLoaded);

            if (!anEnvironmentIsLoaded)
            {
                if (runner.IsSceneAuthority && !_initialEnvironmentLoadStarted)
                {
                    _initialEnvironmentLoadStarted = true;
                    var loadOperation = runner.LoadScene(SceneRef.FromIndex(environment1BuildIndex), LoadSceneMode.Additive);
                    loadOperation.AddOnCompleted(_ =>
                    {
                        var loadedEnvironment = SceneManager.GetSceneByBuildIndex(environment1BuildIndex);
                        if (!loadedEnvironment.IsValid() || !loadedEnvironment.isLoaded)
                        {
                            Debug.LogError("Fusion failed to load the initial Environment 1 scene.");
                            _initialEnvironmentLoadStarted = false;
                            return;
                        }

                        if (!hasEnteredGameScene)
                        {
                            hasEnteredGameScene = true;
                            SpawnPlayerCharacter(runner);
                        }
                    });
                }

                return;
            }

            hasEnteredGameScene = true;
            SpawnPlayerCharacter(runner);
        }

        public void OnSceneLoadStart(NetworkRunner runner)
        {
        }

        public void OnShutdown(NetworkRunner runner, ShutdownReason shutdownReason)
        {
            if (Runner == runner)
                Runner = null;

            isConnected = false;
            hasEnteredGameScene = false;
            _initialEnvironmentLoadStarted = false;
            playersInSession.Clear();
        }

        private static int FindBuildIndex(string sceneName)
        {
            for (int i = 0; i < SceneManager.sceneCountInBuildSettings; i++)
            {
                string path = SceneUtility.GetScenePathByBuildIndex(i);
                if (string.Equals(System.IO.Path.GetFileNameWithoutExtension(path), sceneName,
                        StringComparison.Ordinal))
                    return i;
            }

            return -1;
        }

        public void OnUserSimulationMessage(NetworkRunner runner, SimulationMessagePtr message)
        {
        }
        #endregion
        public void Checkforname()
        {
            if (nameField.text != "")
                submitButton.interactable = true;
            else
                submitButton.interactable = false;
        }

        public void UpdateCurrentEntryBeingEdited(SessionInfo session)
        {
            CurrentEntryBeingEdited.UpdateEntry(session,this);
        }
        public void SetCurrentEntryBeingEdited(RoomEntry roomEntry)
        {
            if (CurrentEntryBeingEdited != null)
            {
                // If we were previously editing an entry
                OnDestroyRoomEntries += CurrentEntryBeingEdited.DestroyEntry;
                CurrentEntryBeingEdited = roomEntry;
            }
            else
            {
                // If we were not editing any entry
                CurrentEntryBeingEdited = roomEntry;
            }
        }
        public void ResetCurrentEntryBeingEdited(RoomEntry roomEntry)
        {
            if (roomEntry == CurrentEntryBeingEdited)
            {
                OnDestroyRoomEntries += CurrentEntryBeingEdited.DestroyEntry;
                CurrentEntryBeingEdited = null;
            }
        }
    }
}
#endif
