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

        private const int RaceWaiting = 0;
        private const int RaceCountdown = 1;
        private const int RaceRunning = 2;
        private const int RaceFinished = 3;
        private const int RaceRematchWaiting = 4;
        private const float RaceCountdownSeconds = 3f;

        [Networked] private int RacePhase { get; set; }
        [Networked] private int RaceRoundId { get; set; }
        [Networked] private double RaceCountdownEndTime { get; set; }
        [Networked] private double RaceStartTime { get; set; }
        [Networked, Capacity(10)] private NetworkDictionary<PlayerRef, byte> ReadyPlayers => default;
        [Networked, Capacity(10)] private NetworkDictionary<PlayerRef, byte> FinishedPlayers => default;
        [Networked, Capacity(10)] private NetworkDictionary<PlayerRef, byte> RaceParticipants => default;
        [Networked, Capacity(10)] private NetworkDictionary<PlayerRef, float> FinishTimes => default;
        [Networked, Capacity(10)] private NetworkDictionary<PlayerRef, int> CheckpointProgress => default;

        private int _observedRoundId = -1;
        private bool _localRaceFinished;
        private bool _localFinishReportPending;
        private double _localFinishReportSentAt;
        private bool _localRaceCountdownLock;
        private double _localTeleportInputLockUntil;
        private readonly List<PendingHostTeleport> _pendingHostTeleports = new List<PendingHostTeleport>();

        private struct PendingHostTeleport
        {
            public PlayerRef Target;
            public Vector3 FloorPosition;
            public Quaternion Rotation;
        }

        public bool CanLocalPlayerToggleReady => Object != null && Runner != null && !Runner.IsShutdown &&
            (RacePhase == RaceWaiting || RacePhase == RaceFinished || RacePhase == RaceRematchWaiting);
        public bool IsHostAuthoritativeSession => Runner != null &&
            (Runner.GameMode == GameMode.Host || Runner.GameMode == GameMode.Server);
        public bool IsLocalPlayerReady => Object != null && Runner != null && ReadyPlayers.ContainsKey(Runner.LocalPlayer);
        private bool IsLocalRaceParticipant => Object != null && Runner != null && RaceParticipants.ContainsKey(Runner.LocalPlayer);
        public int ReadyPlayerCount => Object != null ? ReadyPlayers.Count : 0;
        public int ConnectedPlayerCount => Object != null && Runner != null ? GetActivePlayerCount() : 0;
        public string ReadyButtonLabel
        {
            get
            {
                if (Object == null)
                    return "Bereit";
                var isRematch = RacePhase == RaceFinished || RacePhase == RaceRematchWaiting;
                if (IsLocalPlayerReady)
                    return "Bereit ✓ — zurückziehen";
                return isRematch ? "Für nächste Runde bereit" : "Bereit";
            }
        }

        public bool ShowRaceResults => Object != null &&
            (RacePhase == RaceFinished || RacePhase == RaceRematchWaiting);

        /// <summary>Host-side movement gate for countdowns, spectators, and finished racers.</summary>
        public bool CanPlayerMoveOnAuthority(PlayerRef player)
        {
            if (Object == null || Runner == null || !IsPlayerActive(player))
                return false;

            if (RacePhase == RaceCountdown)
                return false;

            if (RacePhase == RaceRunning)
                return IsRaceParticipant(player) && !FinishedPlayers.ContainsKey(player);

            return true;
        }

        public string RaceResultsLabel
        {
            get
            {
                if (Object == null || Runner == null)
                    return string.Empty;

                var results = new List<KeyValuePair<PlayerRef, float>>();
                foreach (var result in FinishTimes)
                    if (IsPlayerActive(result.Key))
                        results.Add(result);

                results.Sort((left, right) =>
                {
                    var timeOrder = left.Value.CompareTo(right.Value);
                    return timeOrder != 0 ? timeOrder : left.Key.PlayerId.CompareTo(right.Key.PlayerId);
                });

                var lines = new List<string> { "Ergebnisse" };
                for (var i = 0; i < results.Count; i++)
                {
                    var result = results[i];
                    lines.Add($"{i + 1}. {GetRacePlayerName(result.Key)} — {FormatRaceTime(result.Value)}");
                }

                lines.Add(ReadyPlayerCount >= ConnectedPlayerCount && ConnectedPlayerCount > 0
                    ? "Rematch startet …"
                    : $"Für Rematch bereit: {ReadyPlayerCount}/{ConnectedPlayerCount}");
                return string.Join("\n", lines);
            }
        }

        public string RaceStatusLabel
        {
            get
            {
                if (Object == null || Runner == null || Runner.IsShutdown)
                    return "Warte auf Netzwerkverbindung";

                if (RacePhase == RaceCountdown)
                {
                    if (!IsLocalRaceParticipant)
                        return "Runde startet — du bist in der nächsten Runde dabei";

                    var remaining = RaceCountdownEndTime - Runner.SimulationTime;
                    return remaining <= 0d ? "GO!" : Math.Ceiling(remaining).ToString();
                }

                if (RacePhase == RaceRunning)
                {
                    if (!IsLocalRaceParticipant)
                        return "Runde läuft — du kannst bei der nächsten Runde mitspielen";

                    if (_localRaceFinished)
                        return FinishTimes.TryGet(Runner.LocalPlayer, out var confirmedTime)
                            ? $"Im Ziel: {FormatRaceTime(confirmedTime)} — andere laufen noch"
                            : "Ziel erreicht — Ergebnis wird geprüft";

                    var elapsed = Math.Max(0d, Runner.SimulationTime - RaceStartTime);
                    return elapsed < 0.5d ? $"GO!  {FormatRaceTime(elapsed)}" : FormatRaceTime(elapsed);
                }

                if (RacePhase == RaceFinished || RacePhase == RaceRematchWaiting)
                {
                    var ownTime = _localRaceFinished && FinishTimes.TryGet(Runner.LocalPlayer, out var confirmedTime)
                        ? $" — deine Zeit: {FormatRaceTime(confirmedTime)}"
                        : string.Empty;
                    return $"Runde beendet{ownTime} · Rematch bereit: {ReadyPlayerCount}/{ConnectedPlayerCount}";
                }

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
        }

        public override void FixedUpdateNetwork()
        {
            if (!HasStateAuthority || Runner == null)
                return;

            // Apply teleports inside Fusion's simulation tick. A teleport issued
            // directly from the Ready button's Unity Update can be overwritten by
            // the next NetworkTransform snapshot with the old position.
            if (_pendingHostTeleports.Count > 0)
            {
                foreach (var pending in _pendingHostTeleports)
                {
                    if (Runner.GetPlayerObject(pending.Target) != null)
                        TeleportPlayerAtAuthority(pending.Target, pending.FloorPosition, pending.Rotation);
                }
                _pendingHostTeleports.Clear();
            }

            RemoveDisconnectedPlayers();

            if (RacePhase == RaceCountdown && Runner.SimulationTime >= RaceCountdownEndTime)
            {
                RaceStartTime = RaceCountdownEndTime;
                RacePhase = RaceRunning;
            }
            else if (RacePhase == RaceRunning && FinishedPlayers.Count >= GetActiveRaceParticipantCount())
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
                _localFinishReportPending = false;
            }

            if (_localFinishReportPending)
            {
                if (FinishTimes.ContainsKey(Runner.LocalPlayer))
                    _localFinishReportPending = false;
                else if (Runner.SimulationTime - _localFinishReportSentAt >= 0.75d)
                {
                    _localFinishReportPending = false;
                    _localRaceFinished = false;
                }
            }

            if (RacePhase == RaceRunning || RacePhase == RaceFinished)
                _localRaceCountdownLock = false;

            var isWaitingAsSpectator = RacePhase == RaceRunning && !IsLocalRaceParticipant;
            SetLocalRaceInputLock(RacePhase == RaceCountdown || isWaitingAsSpectator || _localRaceCountdownLock ||
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

            // A Host already owns this networked race object. Apply its own ready
            // action directly so it does not depend on RpcInfo.Source for a local RPC.
            if (HasStateAuthority)
            {
                ToggleReadyForPlayer(Runner.LocalPlayer);
                return;
            }

            ToggleReadyRpc();
        }

        [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
        private void ToggleReadyRpc(RpcInfo info = default)
        {
            ToggleReadyForPlayer(info.Source);
        }

        private void ToggleReadyForPlayer(PlayerRef source)
        {
            if (_sceneSwitchInProgress ||
                (RacePhase != RaceWaiting && RacePhase != RaceFinished && RacePhase != RaceRematchWaiting))
                return;

            if (source == PlayerRef.None || !IsPlayerActive(source))
                return;

            if (RacePhase == RaceFinished)
                RacePhase = RaceRematchWaiting;

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
            RaceParticipants.Clear();
            FinishTimes.Clear();
            CheckpointProgress.Clear();
            foreach (var player in players)
            {
                RaceParticipants.Add(player, 1);
                CheckpointProgress.Add(player, 0);
            }
            RaceCountdownEndTime = Runner.SimulationTime + RaceCountdownSeconds;
            RaceStartTime = 0d;
            RacePhase = RaceCountdown;

            Debug.Log($"[Race Start] round={RaceRoundId} players={players.Count} marker={startPoint.transform.position} floor={startPoint.FloorPosition} slots={string.Join("; ", slots)}");

            for (var i = 0; i < players.Count; i++)
                TeleportPlayerRpc(players[i], slots[i], startPoint.transform.rotation);
        }

        private void TeleportPlayerRpc(PlayerRef target, Vector3 floorPosition, Quaternion rotation)
        {
            if (IsHostAuthoritativeSession)
            {
                if (HasStateAuthority)
                    _pendingHostTeleports.Add(new PendingHostTeleport
                    {
                        Target = target,
                        FloorPosition = floorPosition,
                        Rotation = rotation
                    });
                return;
            }

            RPC_TeleportPlayer(target, floorPosition, rotation);
        }

        [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
        private void RPC_TeleportPlayer(PlayerRef target, Vector3 floorPosition, Quaternion rotation)
        {
            if (Runner.LocalPlayer != target)
                return;

            var networkPlayer = Runner.GetPlayerObject(target);
            if (networkPlayer == null || !networkPlayer.HasStateAuthority)
                return;

            TeleportPlayerAtAuthority(target, floorPosition, rotation);
        }

        private void TeleportPlayerAtAuthority(PlayerRef target, Vector3 floorPosition, Quaternion rotation)
        {
            var networkPlayer = Runner.GetPlayerObject(target);
            if (networkPlayer == null || !networkPlayer.HasStateAuthority)
                return;

            var rootNetworkTransform = networkPlayer.GetComponent<NetworkTransform>();
            var characterController = networkPlayer.GetComponentInChildren<CharacterController>(true);
            var playerTransform = characterController != null ? characterController.transform : null;

            if (rootNetworkTransform == null || characterController == null)
            {
                Debug.LogError("Cannot line up the player: the FS Network Player needs a root NetworkTransform and a CharacterController.", networkPlayer);
                return;
            }

            // The Host directly teleports remote players too. A local script lock
            // set on the Host's remote proxy never gets cleared by that player's
            // client, so use the networked race phase for remote countdown gating.
            if (target == Runner.LocalPlayer)
            {
                var locomotionInput = networkPlayer.GetComponentInChildren<LocomotionInputManager>(true);
                if (locomotionInput != null)
                    locomotionInput.RaceInputLocked = true;

                var parkourInput = networkPlayer.GetComponentInChildren<ParkourInputManager>(true);
                if (parkourInput != null)
                    parkourInput.RaceInputLocked = true;

                _localRaceCountdownLock = !IsHostAuthoritativeSession || RacePhase == RaceCountdown;
                _localTeleportInputLockUntil = Runner.SimulationTime + 0.2d;
            }

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

            var authoritySetup = networkPlayer.GetComponent<FSNetworkAuthoritySetup>();
            if (authoritySetup != null)
            {
                authoritySetup.ApplyAuthoritativeTeleport(rootPosition, rotation);
            }
            else
            {
                var wasCharacterControllerEnabled = characterController.enabled;
                characterController.enabled = false;
                rootNetworkTransform.Teleport(rootPosition, rotation);
                Physics.SyncTransforms();
                characterController.enabled = wasCharacterControllerEnabled;
            }

            var locomotionCoreAfterTeleport = networkPlayer.GetComponentInChildren<LocomotionCore>(true);
            if (locomotionCoreAfterTeleport != null)
                locomotionCoreAfterTeleport.GroundCheck();

            Debug.Log($"[Race Teleport] target={target} object={networkPlayer.Id} stateAuthority={networkPlayer.HasStateAuthority} floor={floorPosition} root={networkPlayer.transform.position} player={playerTransform.position} capsuleBottomY={characterController.bounds.min.y}", networkPlayer);
        }

        /// <summary>Reports a locally owned player's checkpoint in Shared Mode.</summary>
        public void ReportCheckpoint(int checkpointIndex)
        {
            if (Object == null || Runner == null || Runner.IsShutdown || Runner.GameMode != GameMode.Shared ||
                RacePhase != RaceRunning || !IsLocalRaceParticipant || checkpointIndex < 1)
                return;

            ReportCheckpointRpc(checkpointIndex);
        }

        [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
        private void ReportCheckpointRpc(int checkpointIndex, RpcInfo info = default)
        {
            var player = info.Source;
            if (Runner.GameMode != GameMode.Shared || !IsNextCheckpoint(player, checkpointIndex))
                return;

            RaceCheckpoint reachedCheckpoint = null;
            foreach (var checkpoint in FindObjectsByType<RaceCheckpoint>(FindObjectsSortMode.None))
            {
                if (checkpoint.CheckpointIndex == checkpointIndex)
                {
                    reachedCheckpoint = checkpoint;
                    break;
                }
            }

            var playerObject = Runner.GetPlayerObject(player);
            if (reachedCheckpoint == null || playerObject == null)
                return;

            var characterController = playerObject.GetComponentInChildren<CharacterController>(true);
            var position = characterController != null ? characterController.bounds.center : playerObject.transform.position;
            var checkpointTolerance = characterController != null
                ? characterController.radius * Mathf.Max(characterController.transform.lossyScale.x, characterController.transform.lossyScale.z) + 0.1f
                : 0.25f;
            if (!reachedCheckpoint.ContainsPosition(position, checkpointTolerance))
            {
                Debug.LogWarning($"Rejected checkpoint {checkpointIndex} report from {player}: authoritative player position was outside the checkpoint trigger.", this);
                return;
            }

            CheckpointProgress.Set(player, checkpointIndex);
        }

        /// <summary>Records a checkpoint overlap already confirmed by the Host's physics scene.</summary>
        public void ReportAuthoritativeCheckpoint(NetworkObject playerObject, int checkpointIndex)
        {
            if (Object == null || !Object.IsValid || !IsHostAuthoritativeSession || !HasStateAuthority ||
                playerObject == null || !playerObject.HasStateAuthority)
                return;

            var player = playerObject.InputAuthority;
            if (IsNextCheckpoint(player, checkpointIndex) && Runner.GetPlayerObject(player) == playerObject)
                CheckpointProgress.Set(player, checkpointIndex);
        }

        private bool IsNextCheckpoint(PlayerRef player, int checkpointIndex)
        {
            if (RacePhase != RaceRunning || player == PlayerRef.None || !IsRaceParticipant(player))
                return false;

            var previousIndex = CheckpointProgress.TryGet(player, out var savedIndex) ? savedIndex : 0;
            return checkpointIndex == previousIndex + 1;
        }

        /// <summary>Queues a respawn for the player observed in a fall volume by the Host.</summary>
        public void RequestAuthoritativeRespawn(NetworkObject playerObject)
        {
            if (Object == null || !Object.IsValid || !IsHostAuthoritativeSession || !HasStateAuthority ||
                playerObject == null || !playerObject.HasStateAuthority || _sceneSwitchInProgress || RacePhase == RaceCountdown)
                return;

            var target = playerObject.InputAuthority;
            if (target == PlayerRef.None || Runner.GetPlayerObject(target) != playerObject ||
                _pendingHostTeleports.Exists(pending => pending.Target == target))
                return;

            RespawnPlayer(target);
        }

        /// <summary>Requests a respawn for the locally owned player in Shared Mode.</summary>
        public void RequestLocalRespawn()
        {
            if (Object == null || Runner == null || Runner.GameMode != GameMode.Shared ||
                Runner.IsShutdown || RacePhase != RaceRunning ||
                !IsLocalRaceParticipant)
                return;

            RequestLocalRespawnRpc();
        }

        [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
        private void RequestLocalRespawnRpc(RpcInfo info = default)
        {
            if (Runner.GameMode != GameMode.Shared || RacePhase != RaceRunning ||
                info.Source == PlayerRef.None || !IsRaceParticipant(info.Source))
                return;

            RespawnPlayer(info.Source);
        }

        private void RespawnPlayer(PlayerRef target)
        {
            if (CheckpointProgress.TryGet(target, out var savedIndex) && savedIndex > 0)
            {
                foreach (var checkpoint in FindObjectsByType<RaceCheckpoint>(FindObjectsSortMode.None))
                {
                    if (checkpoint.CheckpointIndex == savedIndex)
                    {
                        TeleportPlayerRpc(target, checkpoint.RespawnPosition, checkpoint.transform.rotation);
                        return;
                    }
                }
            }

            var startPoint = FindAnyObjectByType<RaceStartPoint>();
            if (startPoint == null)
            {
                Debug.LogWarning("Respawn requested, but the map has no RaceStartPoint.", this);
                return;
            }

            TeleportPlayerRpc(target, startPoint.FloorPosition, startPoint.transform.rotation);
        }

        /// <summary>Stops this client's timer when its owner enters the map's finish trigger.</summary>
        public void FinishLocalRace()
        {
            if (RacePhase != RaceRunning || !IsLocalRaceParticipant || Runner == null ||
                FinishTimes.ContainsKey(Runner.LocalPlayer))
                return;

            if (_localFinishReportPending && Runner.SimulationTime - _localFinishReportSentAt < 0.75d)
                return;

            _localRaceFinished = true;
            _localFinishReportPending = true;
            _localFinishReportSentAt = Runner.SimulationTime;
            ReportPlayerFinishedRpc();
        }

        [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
        private void ReportPlayerFinishedRpc(RpcInfo info = default)
        {
            TryAcceptFinish(info.Source);
        }

        /// <summary>Accepts a finish observed by the Host's authoritative physics scene.</summary>
        public void ReportAuthoritativeFinish(NetworkObject playerObject)
        {
            if (!IsHostAuthoritativeSession || !HasStateAuthority || playerObject == null || !playerObject.HasStateAuthority)
                return;

            TryAcceptFinish(playerObject.InputAuthority);
        }

        private void TryAcceptFinish(PlayerRef player)
        {
            if (RacePhase != RaceRunning || player == PlayerRef.None || !IsRaceParticipant(player) ||
                FinishedPlayers.ContainsKey(player))
                return;

            var playerObject = Runner.GetPlayerObject(player);
            if (playerObject == null)
                return;

            var characterController = playerObject.GetComponentInChildren<CharacterController>(true);
            var finishPosition = characterController != null ? characterController.bounds.center : playerObject.transform.position;
            var finishTriggers = FindObjectsByType<RaceFinishTrigger>(FindObjectsSortMode.None);
            var reachedFinish = false;
            foreach (var finishTrigger in finishTriggers)
            {
                var finishTolerance = characterController != null
                    ? characterController.radius * Mathf.Max(characterController.transform.lossyScale.x, characterController.transform.lossyScale.z) + 0.1f
                    : 0.25f;
                if (finishTrigger != null && finishTrigger.ContainsPosition(finishPosition, finishTolerance))
                {
                    reachedFinish = true;
                    break;
                }
            }

            if (!reachedFinish)
            {
                Debug.LogWarning($"Rejected finish report from {player}: authoritative player position was outside every finish trigger.", this);
                return;
            }

            var finishTime = (float)Math.Max(0d, Runner.SimulationTime - RaceStartTime);
            FinishedPlayers.Add(player, 1);
            FinishTimes.Set(player, finishTime);

            if (FinishedPlayers.Count >= GetActiveRaceParticipantCount())
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

        private bool IsRaceParticipant(PlayerRef player) => RaceParticipants.ContainsKey(player) && IsPlayerActive(player);

        private int GetActiveRaceParticipantCount()
        {
            var count = 0;
            foreach (var participant in RaceParticipants)
                if (IsPlayerActive(participant.Key))
                    count++;
            return count;
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

            var staleParticipants = new List<PlayerRef>();
            foreach (var participant in RaceParticipants)
                if (!activePlayers.Contains(participant.Key))
                    staleParticipants.Add(participant.Key);
            foreach (var player in staleParticipants)
                RaceParticipants.Remove(player);

            var staleTimes = new List<PlayerRef>();
            foreach (var time in FinishTimes)
                if (!activePlayers.Contains(time.Key))
                    staleTimes.Add(time.Key);
            foreach (var player in staleTimes)
                FinishTimes.Remove(player);

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

        private string GetRacePlayerName(PlayerRef player)
        {
            var playerObject = Runner.GetPlayerObject(player);
            var stats = playerObject != null ? playerObject.GetComponentInChildren<PlayerStats>(true) : null;
            if (stats != null && !string.IsNullOrWhiteSpace(stats.PlayerName.ToString()))
                return RichTextSafety.Escape(stats.PlayerName.ToString());

            return $"Spieler {player.PlayerId + 1}";
        }
        public void LeaveRoom()
        {
            FusionConnection.ReturnToMenu(runner != null ? runner : Runner);
        }
        private float smoothedRTT = 0.0f;
        private void LateUpdate()
        { 
            if (Object == null || !Object.IsValid || runner == null || !runner.IsRunning)
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

            if (!Runner.IsSceneAuthority || _sceneSwitchInProgress || RacePhase == RaceCountdown ||
                RacePhase == RaceRunning || RaceMapCatalog.Paths.Count < 2 ||
                !RaceMapCatalog.TryGetLoadedMap(out var currentMap))
                return;

            var paths = RaceMapCatalog.Paths;
            int currentIndex = 0;
            while (currentIndex < paths.Count && paths[currentIndex] != currentMap.path)
                currentIndex++;

            string destinationPath = paths[(currentIndex + 1) % paths.Count];
            var sceneToLoad = SceneRef.FromIndex(SceneUtility.GetBuildIndexByScenePath(destinationPath));
            var activeRunner = Runner;
            _sceneSwitchInProgress = true;

            // Keep the current track available until the destination is loaded and usable.
            var load = activeRunner.LoadScene(sceneToLoad, LoadSceneMode.Additive);
            load.AddOnCompleted(operation =>
            {
                if (this == null || activeRunner == null || activeRunner.IsShutdown)
                    return;

                var destination = SceneManager.GetSceneByPath(destinationPath);
                if (operation.Error != null || !destination.isLoaded)
                {
                    Debug.LogWarning($"Fusion could not load {destinationPath}.", this);
                    _sceneSwitchInProgress = false;
                    return;
                }

                var start = RaceMapCatalog.FindStartPoint(destination);
                if (start == null)
                {
                    Debug.LogWarning($"The map {destinationPath} has no active RaceStartPoint.", this);
                    var discard = activeRunner.UnloadScene(sceneToLoad);
                    discard.AddOnCompleted(_ => _sceneSwitchInProgress = false);
                    return;
                }

                var unload = activeRunner.UnloadScene(SceneRef.FromIndex(currentMap.buildIndex));
                unload.AddOnCompleted(result =>
                {
                    if (this == null || activeRunner == null || activeRunner.IsShutdown)
                        return;

                    _sceneSwitchInProgress = false;
                    if (result.Error != null)
                    {
                        Debug.LogWarning($"Fusion could not unload {currentMap.path}.", this);
                        return;
                    }

                    ResetRaceForMap(start);
                    activeRunner.SessionInfo.UpdateCustomProperties(new Dictionary<string, SessionProperty>
                    {
                        [RaceMapCatalog.MapProperty] = destinationPath
                    });
                });
            });
        }

        private void ResetRaceForMap(RaceStartPoint start)
        {
            RacePhase = RaceWaiting;
            RaceRoundId++;
            RaceCountdownEndTime = 0d;
            RaceStartTime = 0d;
            ReadyPlayers.Clear();
            FinishedPlayers.Clear();
            RaceParticipants.Clear();
            FinishTimes.Clear();
            CheckpointProgress.Clear();
            _pendingHostTeleports.Clear();
            _localRaceCountdownLock = false;

            var players = new List<PlayerRef>(Runner.ActivePlayers);
            players.Sort((left, right) => left.PlayerId.CompareTo(right.PlayerId));
            var slots = start.CalculatePlayerSlots(players.Count);
            for (int i = 0; i < players.Count; i++)
                TeleportPlayerRpc(players[i], slots[i], start.transform.rotation);
        }

        [Rpc(RpcSources.Proxies,RpcTargets.StateAuthority)]
        private void SceneSwitchRpc()
        {
            SwitchScene();
        }

        public void PauseGame()
        {
            if (FusionConnection.Instance != null &&
                FusionConnection.Instance.TryGetLocalPlayerComponent(out StarterAssetsInputs starterAssetsInputs))
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
            if (FusionConnection.Instance != null &&
                FusionConnection.Instance.TryGetLocalPlayerComponent(out StarterAssetsInputs starterAssetsInputs))
            {
                starterAssetsInputs.EnablePlayerInput();
            }
        }
    }
}
#endif
