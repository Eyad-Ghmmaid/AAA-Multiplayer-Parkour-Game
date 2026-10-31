#if CMPSETUP_COMPLETE
using Fusion;
using UnityEngine.SceneManagement;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using System;
using System.Collections.Generic;
using StarterAssets;
using UnityEngine.InputSystem;
using FS_LocomotionSystem;
using FS_ParkourSystem;

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

        private const int RaceWaiting = 0;
        private const int RaceCountdown = 1;
        private const int RaceRunning = 2;
        private const int RaceFinished = 3;
        private const float RaceCountdownSeconds = 3f;

        [Networked] private int RacePhase { get; set; }
        [Networked] private int RaceRoundId { get; set; }
        [Networked] private double RaceCountdownEndTime { get; set; }
        [Networked] private double RaceStartTime { get; set; }
        [Networked, Capacity(10)] private NetworkDictionary<PlayerRef, byte> ReadyPlayers => default;
        [Networked, Capacity(10)] private NetworkDictionary<PlayerRef, byte> FinishedPlayers => default;
        [Networked, Capacity(10)] private NetworkDictionary<PlayerRef, int> CheckpointProgress => default;

        private int _observedRoundId = -1;
        private bool _localRaceFinished;
        private bool _localRaceCountdownLock;
        private double _localFinishTime;
        private double _localTeleportInputLockUntil;

        public bool CanLocalPlayerToggleReady => Object != null && Runner != null && !Runner.IsShutdown &&
            (RacePhase == RaceWaiting || RacePhase == RaceFinished);
        public bool IsLocalPlayerReady => Object != null && Runner != null && ReadyPlayers.ContainsKey(Runner.LocalPlayer);
        public int ReadyPlayerCount => Object != null ? ReadyPlayers.Count : 0;
        public int ConnectedPlayerCount => Object != null && Runner != null ? GetActivePlayerCount() : 0;
        public string ReadyButtonLabel => Object == null
            ? "Bereit"
            : RacePhase == RaceFinished
                ? "Für nächste Runde bereit"
                : IsLocalPlayerReady ? "Bereit ✓ — zurückziehen" : "Bereit";

        public string RaceStatusLabel
        {
            get
            {
                if (Object == null || Runner == null || Runner.IsShutdown)
                    return "Warte auf Netzwerkverbindung";

                if (RacePhase == RaceCountdown)
                {
                    var remaining = RaceCountdownEndTime - Runner.SimulationTime;
                    return remaining <= 0d ? "GO!" : Math.Ceiling(remaining).ToString();
                }

                if (RacePhase == RaceRunning)
                {
                    if (_localRaceFinished)
                        return $"Im Ziel: {FormatRaceTime(_localFinishTime)} — andere laufen noch";

                    var elapsed = Math.Max(0d, Runner.SimulationTime - RaceStartTime);
                    return elapsed < 0.5d ? $"GO!  {FormatRaceTime(elapsed)}" : FormatRaceTime(elapsed);
                }

                if (RacePhase == RaceFinished)
                    return _localRaceFinished
                        ? $"Runde beendet — deine Zeit: {FormatRaceTime(_localFinishTime)}"
                        : "Runde beendet — bereit für die nächste?";

                return $"Bereit: {ReadyPlayerCount}/{ConnectedPlayerCount}";
            }
        }

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

        public override void FixedUpdateNetwork()
        {
            if (!HasStateAuthority || Runner == null)
                return;

            RemoveDisconnectedPlayers();

            if (RacePhase == RaceCountdown && Runner.SimulationTime >= RaceCountdownEndTime)
            {
                RaceStartTime = RaceCountdownEndTime;
                RacePhase = RaceRunning;
            }
            else if (RacePhase == RaceRunning && FinishedPlayers.Count >= GetActivePlayerCount())
            {
                RacePhase = RaceFinished;
            }
        }

        private void Update()
        {
            if (Object == null || Runner == null || Runner.IsShutdown)
                return;

            if (_observedRoundId != RaceRoundId)
            {
                _observedRoundId = RaceRoundId;
                _localRaceFinished = false;
                _localFinishTime = 0d;
            }

            if (RacePhase == RaceRunning || RacePhase == RaceFinished)
                _localRaceCountdownLock = false;

            SetLocalRaceInputLock(RacePhase == RaceCountdown || _localRaceCountdownLock ||
                Runner.SimulationTime < _localTeleportInputLockUntil);
        }

        private void SetLocalRaceInputLock(bool locked)
        {
            var localPlayer = Runner.GetPlayerObject(Runner.LocalPlayer);
            if (localPlayer == null)
                return;

            var locomotionInput = localPlayer.GetComponentInChildren<LocomotionInputManager>(true);
            if (locomotionInput != null)
                locomotionInput.RaceInputLocked = locked;

            var parkourInput = localPlayer.GetComponentInChildren<ParkourInputManager>(true);
            if (parkourInput != null)
                parkourInput.RaceInputLocked = locked;
        }

        /// <summary>Wire the user's Ready button to this method.</summary>
        public void ToggleReady()
        {
            if (Object == null || Runner == null || Runner.IsShutdown || !CanLocalPlayerToggleReady)
                return;
            ToggleReadyRpc();
        }

        [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
        private void ToggleReadyRpc(RpcInfo info = default)
        {
            if (RacePhase != RaceWaiting && RacePhase != RaceFinished)
                return;

            var source = info.Source;
            if (source == PlayerRef.None || !IsPlayerActive(source))
                return;

            if (RacePhase == RaceFinished)
            {
                ReadyPlayers.Clear();
                FinishedPlayers.Clear();
                RacePhase = RaceWaiting;
            }

            if (ReadyPlayers.ContainsKey(source))
                ReadyPlayers.Remove(source);
            else if (ReadyPlayers.Count < ReadyPlayers.Capacity)
                ReadyPlayers.Add(source, 1);

            TryStartRaceWhenAllReady();
        }

        private void TryStartRaceWhenAllReady()
        {
            var players = new List<PlayerRef>();
            foreach (var player in Runner.ActivePlayers)
            {
                if (Runner.GetPlayerObject(player) == null)
                    return;
                players.Add(player);
            }

            if (players.Count == 0 || ReadyPlayers.Count != players.Count)
                return;

            foreach (var player in players)
                if (!ReadyPlayers.ContainsKey(player))
                    return;

            var startPoint = FindAnyObjectByType<RaceStartPoint>();
            if (startPoint == null)
            {
                Debug.LogError("All players are ready, but this map has no RaceStartPoint. Add one to the active environment scene.", this);
                return;
            }

            players.Sort((left, right) => left.PlayerId.CompareTo(right.PlayerId));
            var slots = startPoint.CalculatePlayerSlots(players.Count);

            RaceRoundId++;
            ReadyPlayers.Clear();
            FinishedPlayers.Clear();
            CheckpointProgress.Clear();
            foreach (var player in players)
                CheckpointProgress.Add(player, 0);
            RaceCountdownEndTime = Runner.SimulationTime + RaceCountdownSeconds;
            RaceStartTime = 0d;
            RacePhase = RaceCountdown;

            for (var i = 0; i < players.Count; i++)
                TeleportPlayerRpc(players[i], slots[i], startPoint.transform.rotation);
        }

        [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
        private void TeleportPlayerRpc(PlayerRef target, Vector3 floorPosition, Quaternion rotation)
        {
            if (Runner.LocalPlayer != target)
                return;

            var networkPlayer = Runner.GetPlayerObject(target);
            if (networkPlayer == null || !networkPlayer.HasStateAuthority)
                return;

            var rootNetworkTransform = networkPlayer.GetComponent<NetworkTransform>();
            var characterController = networkPlayer.GetComponentInChildren<CharacterController>(true);
            var playerTransform = characterController != null ? characterController.transform : null;
            var playerNetworkTransform = playerTransform != null ? playerTransform.GetComponent<NetworkTransform>() : null;

            if (rootNetworkTransform == null || playerNetworkTransform == null || characterController == null)
            {
                Debug.LogError("Cannot line up the player: the FS Network Player needs root and Player NetworkTransforms plus a CharacterController.", networkPlayer);
                return;
            }

            var locomotionInput = networkPlayer.GetComponentInChildren<LocomotionInputManager>(true);
            if (locomotionInput != null)
                locomotionInput.RaceInputLocked = true;

            var parkourInput = networkPlayer.GetComponentInChildren<ParkourInputManager>(true);
            if (parkourInput != null)
                parkourInput.RaceInputLocked = true;

            _localRaceCountdownLock = true;
            _localTeleportInputLockUntil = Runner.SimulationTime + 0.2d;

            var locomotionCore = networkPlayer.GetComponentInChildren<LocomotionCore>(true);
            if (locomotionCore != null)
            {
                locomotionCore.ResetCurrentVelocity();
                locomotionCore.ResetTargetRotation();
            }

            var rootTransform = networkPlayer.transform;
            var capsuleBottom = characterController.transform.TransformPoint(
                characterController.center + Vector3.down * (characterController.height * 0.5f));
            var capsuleBottomLocal = rootTransform.InverseTransformPoint(capsuleBottom);
            var rootPosition = floorPosition - rotation * capsuleBottomLocal + rotation * Vector3.up * characterController.skinWidth;

            var wasCharacterControllerEnabled = characterController.enabled;
            characterController.enabled = false;
            rootNetworkTransform.Teleport(rootPosition, rotation);
            playerNetworkTransform.Teleport(playerTransform.position, playerTransform.rotation);
            characterController.enabled = wasCharacterControllerEnabled;
        }

        /// <summary>Called by a locally owned player after entering a map checkpoint trigger.</summary>
        public void ReportCheckpoint(int checkpointIndex)
        {
            if (Object == null || Runner == null || Runner.IsShutdown || RacePhase != RaceRunning || checkpointIndex < 1)
                return;

            ReportCheckpointRpc(checkpointIndex);
        }

        [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
        private void ReportCheckpointRpc(int checkpointIndex, RpcInfo info = default)
        {
            if (RacePhase != RaceRunning || info.Source == PlayerRef.None || !IsPlayerActive(info.Source))
                return;

            var checkpointExists = false;
            foreach (var checkpoint in FindObjectsByType<RaceCheckpoint>(FindObjectsSortMode.None))
            {
                if (checkpoint.CheckpointIndex == checkpointIndex)
                {
                    checkpointExists = true;
                    break;
                }
            }

            if (!checkpointExists)
                return;

            var previousIndex = CheckpointProgress.TryGet(info.Source, out var savedIndex) ? savedIndex : 0;
            if (checkpointIndex > previousIndex)
                CheckpointProgress.Set(info.Source, checkpointIndex);
        }

        /// <summary>Requests a respawn at the player's latest checkpoint, or at the race start.</summary>
        public void RequestLocalRespawn()
        {
            if (Object == null || Runner == null || Runner.IsShutdown || RacePhase != RaceRunning)
                return;

            RequestLocalRespawnRpc();
        }

        [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
        private void RequestLocalRespawnRpc(RpcInfo info = default)
        {
            if (RacePhase != RaceRunning || info.Source == PlayerRef.None || !IsPlayerActive(info.Source))
                return;

            var savedIndex = CheckpointProgress.TryGet(info.Source, out var value) ? value : 0;
            var startPoint = FindAnyObjectByType<RaceStartPoint>();
            Vector3 floorPosition;
            Quaternion rotation;

            if (savedIndex > 0)
            {
                RaceCheckpoint savedCheckpoint = null;
                foreach (var checkpoint in FindObjectsByType<RaceCheckpoint>(FindObjectsSortMode.None))
                {
                    if (checkpoint.CheckpointIndex == savedIndex)
                    {
                        savedCheckpoint = checkpoint;
                        break;
                    }
                }

                if (savedCheckpoint != null)
                {
                    floorPosition = savedCheckpoint.RespawnPosition;
                    rotation = savedCheckpoint.transform.rotation;
                }
                else if (startPoint != null)
                {
                    floorPosition = startPoint.transform.position;
                    rotation = startPoint.transform.rotation;
                }
                else
                {
                    Debug.LogError("Respawn requested, but the map has no valid checkpoint or RaceStartPoint.", this);
                    return;
                }
            }
            else if (startPoint != null)
            {
                floorPosition = startPoint.transform.position;
                rotation = startPoint.transform.rotation;
            }
            else
            {
                Debug.LogError("Respawn requested, but the map has no RaceStartPoint.", this);
                return;
            }

            TeleportPlayerRpc(info.Source, floorPosition, rotation);
        }

        /// <summary>Stops this client's timer when its owner enters the map's finish trigger.</summary>
        public void FinishLocalRace()
        {
            if (RacePhase != RaceRunning || _localRaceFinished || Runner == null)
                return;

            _localFinishTime = Math.Max(0d, Runner.SimulationTime - RaceStartTime);
            _localRaceFinished = true;
            ReportPlayerFinishedRpc();
        }

        [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
        private void ReportPlayerFinishedRpc(RpcInfo info = default)
        {
            if (RacePhase != RaceRunning || info.Source == PlayerRef.None || !IsPlayerActive(info.Source))
                return;

            if (!FinishedPlayers.ContainsKey(info.Source))
                FinishedPlayers.Add(info.Source, 1);

            if (FinishedPlayers.Count >= GetActivePlayerCount())
                RacePhase = RaceFinished;
        }

        private int GetActivePlayerCount()
        {
            var count = 0;
            foreach (var _ in Runner.ActivePlayers)
                count++;
            return count;
        }

        private bool IsPlayerActive(PlayerRef player)
        {
            foreach (var activePlayer in Runner.ActivePlayers)
                if (activePlayer == player)
                    return true;
            return false;
        }

        private void RemoveDisconnectedPlayers()
        {
            var activePlayers = new List<PlayerRef>();
            foreach (var player in Runner.ActivePlayers)
                activePlayers.Add(player);

            var staleReady = new List<PlayerRef>();
            foreach (var ready in ReadyPlayers)
                if (!activePlayers.Contains(ready.Key))
                    staleReady.Add(ready.Key);
            foreach (var player in staleReady)
                ReadyPlayers.Remove(player);

            var staleFinished = new List<PlayerRef>();
            foreach (var finished in FinishedPlayers)
                if (!activePlayers.Contains(finished.Key))
                    staleFinished.Add(finished.Key);
            foreach (var player in staleFinished)
                FinishedPlayers.Remove(player);

            var staleCheckpoints = new List<PlayerRef>();
            foreach (var checkpoint in CheckpointProgress)
                if (!activePlayers.Contains(checkpoint.Key))
                    staleCheckpoints.Add(checkpoint.Key);
            foreach (var player in staleCheckpoints)
                CheckpointProgress.Remove(player);
        }

        private static string FormatRaceTime(double seconds)
        {
            var time = TimeSpan.FromSeconds(Math.Max(0d, seconds));
            return $"{(int)time.TotalMinutes:00}:{time.Seconds:00}.{time.Milliseconds / 10:00}";
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
