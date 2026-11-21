#if CMPSETUP_COMPLETE
using UnityEngine;
using Fusion;
using Fusion.Sockets;
using System.Collections.Generic;
using System;
using System.Threading.Tasks;
using UnityEngine.UI;
using TMPro;
using System.Collections;
using UnityEngine.SceneManagement;
using Photon.Voice.Fusion;
using Photon.Voice.Unity;
using StarterAssets;
using Fusion.Photon.Realtime;
using UnityEngine.Serialization;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif
using FS_Core;
using FS_LocomotionSystem;
using FS_ParkourSystem;

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
        private static bool _returningToMenu;
        private static readonly TimeSpan RoomShutdownTimeout = TimeSpan.FromSeconds(5);

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
        [SerializeField] private bool logInputDiagnostics;
        private int _lastInputDiagnosticFrame = -1000;
        private FSPlayerNetworkInput _lastInputSample;
        private bool _cloudConnectionLostCallbackRegistered;

        [Header("Loading Screen")] public LoadingScreen loadingScreenScript;

        [Header("Loading Screen")] public PopUp popup;

        [Header("UI")] [SerializeField] private MenuCanvas menuCanvas;

        private bool initialRoomListPopulated = false;
        private List<SessionInfo> _sessionList = new List<SessionInfo>();
        private List<RoomEntry> _roomEntryList = new List<RoomEntry>();
        private bool _initialEnvironmentLoadStarted;
        private bool _clientGameSceneLoadStarted;

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
            var localPlayer = Runner != null && Runner.IsRunning
                ? Runner.GetPlayerObject(Runner.LocalPlayer)
                : null;
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
            // If the old Photon room has already disappeared, QuickRejoin alone can
            // only return GameNotFound. Let the Host create Fusion's invisible backup
            // room so directly connected players keep the running match and the room
            // can become joinable again after the transfer.
            NetworkRunner.CloudConnectionLostCurrentMode =
                NetworkRunner.CloudConnectionLostMode.MoveSessionToNewRoom;
            if (!_cloudConnectionLostCallbackRegistered)
            {
                NetworkRunner.CloudConnectionLost += OnCloudConnectionLost;
                _cloudConnectionLostCallbackRegistered = true;
            }

            // Host/Client gameplay input is submitted through OnInput. Without this
            // flag Fusion starts the session but never collects that input, leaving
            // State Authority players stationary while local input can still animate.
            Runner.ProvideInput = true;
            Runner.AddCallbacks(this);
            return true;
        }

        private void OnCloudConnectionLost(NetworkRunner affectedRunner,
            ShutdownReason shutdownReason, bool reconnecting)
        {
            if (affectedRunner != Runner)
                return;

            if (reconnecting)
            {
                Debug.LogWarning($"Fusion cloud connection lost ({shutdownReason}). Reconnection is in progress.", this);
                return;
            }

            isConnected = affectedRunner != null && affectedRunner.IsRunning;
            // The direct Host/Client transport can still be alive here. This must not
            // be an error log in a Development Build: Unity opens its blocking console
            // for errors and makes an otherwise running match look frozen.
            Debug.LogWarning($"Fusion could not restore the Photon Cloud connection ({shutdownReason}). " +
                             "Existing directly connected players can continue the current match, " +
                             "but new players cannot join until the next room.", this);
        }

        private void OnDestroy()
        {
            if (_cloudConnectionLostCallbackRegistered)
            {
                NetworkRunner.CloudConnectionLost -= OnCloudConnectionLost;
                _cloudConnectionLostCallbackRegistered = false;
            }

            if (Instance == this)
                Instance = null;
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
                GameMode = GameMode.Client,
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
            if (this != null && !_returningToMenu)
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
                GameMode = GameMode.Host,
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

        private async Task ShutdownFailedRunner(NetworkRunner failedRunner)
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

        internal static async void ReturnToMenu(NetworkRunner runner)
        {
            if (_returningToMenu)
                return;

            _returningToMenu = true;
            try
            {
                // Fusion may invoke this from inside its own disconnect or shutdown callback.
                await Task.Yield();

                var shutdown = ShutdownRoomRunner(runner);
                if (!shutdown.IsCompleted && await Task.WhenAny(shutdown, Task.Delay(RoomShutdownTimeout)) != shutdown)
                {
                    Debug.LogWarning("Fusion room shutdown timed out. Returning to the menu.");
                    if (runner != null)
                        Destroy(runner.gameObject);
                }

                Time.timeScale = 1f;
                await SceneManager.LoadSceneAsync("Menu", LoadSceneMode.Single);
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }
            finally
            {
                _returningToMenu = false;
            }
        }

        private static async Task ShutdownRoomRunner(NetworkRunner runner)
        {
            if (runner == null || runner.IsShutdown)
                return;

            try
            {
                await runner.Shutdown();
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"Fusion room shutdown failed: {exception.Message}");
                if (runner != null)
                    Destroy(runner.gameObject);
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
            // Client/Server sessions must spawn every participant from the Host.
            // Shared Mode keeps its existing local-owner spawn path below.
            if (runner.GameMode != GameMode.Shared)
            {
                if (!runner.IsServer)
                    return;

                int spawnIndex = 0;
                foreach (PlayerRef player in runner.ActivePlayers)
                    SpawnHostPlayerCharacter(runner, player, spawnIndex++);
                return;
            }

            if (runner.GetPlayerObject(runner.LocalPlayer) != null)
                return;

            if (fsNetworkPlayerPrefab == null)
            {
                Debug.LogError("FS Network Player Prefab is not assigned.");
                return;
            }

            var location = GetPlayerSpawnLocation(0);

            var playerObject = runner.Spawn(
                fsNetworkPlayerPrefab,
                location,
                Quaternion.identity);

            runner.SetPlayerObject(runner.LocalPlayer, playerObject);
        }

        private void SpawnHostPlayerCharacter(NetworkRunner runner, PlayerRef player, int spawnIndex)
        {
            if (runner.GetPlayerObject(player) != null)
                return;

            if (fsNetworkPlayerPrefab == null)
            {
                Debug.LogError("FS Network Player Prefab is not assigned.");
                return;
            }

            var playerObject = runner.Spawn(
                fsNetworkPlayerPrefab,
                GetPlayerSpawnLocation(spawnIndex),
                Quaternion.identity,
                player);

            if (playerObject != null)
                runner.SetPlayerObject(player, playerObject);
        }

        private Vector3 GetPlayerSpawnLocation(int spawnIndex)
        {
            var location = !UseCustomLocation
                ? new Vector3(UnityEngine.Random.Range(-7.6f, 14.2f), 0,
                    UnityEngine.Random.Range(-41.22f, -31.48f))
                : CustomLocation;

            // Keep Host-spawned participants apart before the race formation is set.
            if (spawnIndex > 0)
                location += new Vector3((spawnIndex % 2) * 1.5f, 0, (spawnIndex / 2) * 1.5f);

            // Treat location.y as floor height and lift the network root to place the
            // child CharacterController capsule on that floor.
            var characterController = fsNetworkPlayerPrefab != null
                ? fsNetworkPlayerPrefab.GetComponentInChildren<CharacterController>()
                : null;
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

            return location;
        }

        #region INetworkCallbacks

        public void OnConnectedToServer(NetworkRunner runner)
        {
            Debug.Log("OnConnectedToServer");
            isConnected = true;
        }

        public void OnDisconnectedFromServer(NetworkRunner runner, NetDisconnectReason reason)
        {
            if (runner != Runner)
                return;

            isConnected = false;
            if (_clientGameSceneLoadStarted)
                ReturnToMenu(runner);
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
            if (runner == Runner && _clientGameSceneLoadStarted)
                ReturnToMenu(runner);
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
            if (runner.GameMode == GameMode.Shared)
                return;

            FSPlayerNetworkInput data = default;
            NetworkObject playerObject = runner.GetPlayerObject(runner.LocalPlayer);
            if (playerObject != null)
            {
                var locomotionInput = playerObject.GetComponentInChildren<LocomotionInputManager>(true);
                var parkourInput = playerObject.GetComponentInChildren<ParkourInputManager>(true);
                var playerController = playerObject.GetComponentInChildren<PlayerController>(true);

                if (locomotionInput != null)
                {
                    var move = locomotionInput.DirectionInput;
                    data.Move = Vector2.ClampMagnitude(move, 1f);
                    data.JumpHeld = locomotionInput.JumpHold;
                    data.Drop = locomotionInput.Drop;
                    data.ToggleRun = locomotionInput.ToggleRun;
                    data.Sprint = locomotionInput.SprintKey;
                    data.Crouch = locomotionInput.Crouch;
                    data.QuickSwitchDown = locomotionInput.QuickSwitchItemDown;
                    data.QuickSwitchUp = locomotionInput.QuickSwitchItemUp;
                    data.PreviousItem = locomotionInput.CycleToPreviousItem;
                    data.NextItem = locomotionInput.CycleToNextItem;
                    data.ThrowDown = locomotionInput.ItemThrowDown;
                    data.ThrowHeld = locomotionInput.ItemThrowHold;
                    data.ThrowUp = locomotionInput.ItemThrowUp;
                    data.ThrowCancel = locomotionInput.ItemThrowCancelDown;
                }

                // Convert the held input to a single edge. Fusion may request input
                // several times during one rendered frame; WasPressedThisFrame can
                // otherwise be true for every simulation tick in that frame.
                bool jumpHeldNow = data.JumpHeld;
#if ENABLE_INPUT_SYSTEM
                if (Keyboard.current != null)
                    jumpHeldNow |= Keyboard.current.spaceKey.isPressed;
#endif
                data.JumpPressed = jumpHeldNow && !_lastInputSample.JumpHeld;
                data.JumpHeld = jumpHeldNow;

                if (parkourInput != null)
                    data.JumpFromHang = parkourInput.JumpFromHang;

                var currentInputSample = data;
                // InputAction WasPressedThisFrame can be observed by multiple Fusion
                // ticks in one rendered frame. Turn all one-shot controls into edges
                // so one physical press cannot retrigger during resimulation.
                data.Drop = data.Drop && !_lastInputSample.Drop;
                data.ToggleRun = data.ToggleRun && !_lastInputSample.ToggleRun;
                data.Crouch = data.Crouch && !_lastInputSample.Crouch;
                data.QuickSwitchDown = data.QuickSwitchDown && !_lastInputSample.QuickSwitchDown;
                data.QuickSwitchUp = data.QuickSwitchUp && !_lastInputSample.QuickSwitchUp;
                data.PreviousItem = data.PreviousItem && !_lastInputSample.PreviousItem;
                data.NextItem = data.NextItem && !_lastInputSample.NextItem;
                data.ThrowDown = data.ThrowDown && !_lastInputSample.ThrowDown;
                data.ThrowUp = data.ThrowUp && !_lastInputSample.ThrowUp;
                data.ThrowCancel = data.ThrowCancel && !_lastInputSample.ThrowCancel;
                data.JumpFromHang = data.JumpFromHang && !_lastInputSample.JumpFromHang;
                _lastInputSample = currentInputSample;

                if (playerController != null && playerController.cameraGameObject != null)
                    data.CameraForward = playerController.cameraGameObject.transform.forward;
            }

            if (logInputDiagnostics && Time.frameCount - _lastInputDiagnosticFrame >= 120)
            {
                _lastInputDiagnosticFrame = Time.frameCount;
                Debug.Log($"[FS Host Input] mode={runner.GameMode} provideInput={runner.ProvideInput} localPlayer={runner.LocalPlayer} playerObject={(playerObject != null ? playerObject.Id.ToString() : "NULL")} move={data.Move} jump={data.JumpPressed} camera={data.CameraForward}");
            }

            input.Set(data);
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

            if (runner.GameMode != GameMode.Shared && runner.IsServer && hasEnteredGameScene)
                SpawnHostPlayerCharacter(runner, player, playersInSession.IndexOf(player));
        }

        public void OnPlayerLeft(NetworkRunner runner, PlayerRef player)
        {
            playersInSession.Remove(player);

            if (runner.GameMode != GameMode.Shared && runner.IsServer)
            {
                NetworkObject playerObject = runner.GetPlayerObject(player);
                if (playerObject != null)
                    runner.Despawn(playerObject);
            }
        }
        public void OnSceneLoadDone(NetworkRunner runner)
        {
            if (runner != Runner || _returningToMenu)
                return;

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
                        if (this == null || runner != Runner || _returningToMenu)
                            return;

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
            if (runner == Runner && runner.GameMode == GameMode.Client)
                _clientGameSceneLoadStarted = true;
        }

        public void OnShutdown(NetworkRunner runner, ShutdownReason shutdownReason)
        {
            if (runner != Runner)
                return;

            bool clientWasInRoom = _clientGameSceneLoadStarted;
            Runner = null;
            isConnected = false;
            hasEnteredGameScene = false;
            _clientGameSceneLoadStarted = false;
            _initialEnvironmentLoadStarted = false;
            playersInSession.Clear();
            _lastInputSample = default;

            if (clientWasInRoom && shutdownReason != ShutdownReason.Ok)
                ReturnToMenu(runner);
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
