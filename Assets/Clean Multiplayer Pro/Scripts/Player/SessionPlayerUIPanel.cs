#if CMPSETUP_COMPLETE
using System;
using System.Collections.Generic;
using AvocadoShark;
using UnityEngine;

public class SessionPlayerUIPanel : MonoBehaviour {

    public GameObject playerPortraitPrefab;

    public SessionPlayers sessionPlayersRef;

    public Action<bool> OnToggleButtonAfterPlayersChange;

    public Dictionary<PlayerStats ,PlayerUIEntry> playerUIEntryList = new Dictionary<PlayerStats, PlayerUIEntry>();  

    private void Awake() {
        sessionPlayersRef.OnPlayerAddedToSession += AddNewPlayerPortrait;
        sessionPlayersRef.OnPlayerRemovedFromSession += RemovePlayerPortrait;
        sessionPlayersRef.OnPlayerAddedToSession += ToggleVoteKickButton;
        sessionPlayersRef.OnPlayerRemovedFromSession += ToggleVoteKickButton;
    }

    public void AddNewPlayerPortrait(PlayerStats playerStats) {
        GameObject portraitInstance = Instantiate(playerPortraitPrefab, transform);
        PlayerUIEntry playerUIEntry = portraitInstance.GetComponent<PlayerUIEntry>();
        playerUIEntryList.Add(playerStats, playerUIEntry);
        var isLocal = playerStats.Object.StateAuthority == playerStats.Runner.LocalPlayer;
        var safeName = AvocadoShark.RichTextSafety.Escape(playerStats.PlayerName.ToString());
        playerUIEntry.playerName = isLocal ? $"<color=orange>{safeName}</color>" : safeName;
        playerUIEntry.playerStats = playerStats;
        playerUIEntry.OnVoteKick += InitiateVoteKick;
        if (!playerStats.HasStateAuthority) { 
            OnToggleButtonAfterPlayersChange += playerUIEntry.ToggleVoteKickButton;
        }
    }

    public void ToggleVoteKickButton(PlayerStats playerStats) {
        if (SessionPlayers.instance.activePlayers.Count <= 2) {
            OnToggleButtonAfterPlayersChange?.Invoke(false);
        } else {
            OnToggleButtonAfterPlayersChange?.Invoke(true);
        }
    }

    public void RemovePlayerPortrait(PlayerStats playerStats) {
        // A portrait only exists for remote players (see AddPlayerPortrait callers) - a
        // despawning local player, or one that failed mid-spawn, has no entry to remove.
        if (!playerUIEntryList.TryGetValue(playerStats, out var playerUIEntry))
            return;
        OnToggleButtonAfterPlayersChange -= playerUIEntry.ToggleVoteKickButton;
        Destroy(playerUIEntry.gameObject);
        playerUIEntryList.Remove(playerStats);
    }

    public void InitiateVoteKick(PlayerStats playerStats) {
        playerStats.InitializeVoteKick();
    }


}
#endif