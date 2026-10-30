#if CMPSETUP_COMPLETE
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Fusion;
using AvocadoShark;
using StarterAssets;

public class ChatPlayer : NetworkBehaviour
{
    public string playerName;

    [SerializeField] private StarterAssetsInputs _input;
    private PlayerWorldUIManager _playerWorldUIManager;


    private void Awake()
    {
        GetComponent<PlayerStats>().OnPlayerStatsReady += PlayerStatsReady;
        _playerWorldUIManager = GetComponent<PlayerWorldUIManager>();
    }

    public void PlayerStatsReady(string playerName)
    {
        if (Object.HasStateAuthority)
        {
            this.playerName = playerName;
            ChatSystem.Instance.SetChatPlayer(this);
        }
    }

    public void SendChat(Chat chat)
    {
        BroadcastChatRpc(chat.Sender.ToString(), chat.Message.ToString());
    }

    // Chat is an event, not state: delivering it via a [Networked] property drops
    // messages that leave the state buffer unchanged (identical consecutive texts)
    // or that are overwritten within the same tick. An RPC delivers every message,
    // and two plain strings are serialized by actual length instead of the fixed
    // ~1 KB footprint of two NetworkString<_128> fields.
    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void BroadcastChatRpc(string sender, string message)
    {
        var chat = new Chat(sender, message);
        ChatSystem.Instance.AddChatEntry(!HasStateAuthority, chat);
        if (!Object.HasStateAuthority)
            _playerWorldUIManager.QueueChat(chat);
    }

    public void SetPlayerIsWriting(bool value)
    {
        if (value)
        {
            _input.DisablePlayerInput();
        }
        else
        {
            _input.EnablePlayerInput();
        }
    }
}

public struct Chat : INetworkStruct
{
    public NetworkString<_128> Sender, Message;

    public Chat(NetworkString<_128> sender, NetworkString<_128> message)
    {
        Sender = sender;
        Message = message;
    }
}
#endif