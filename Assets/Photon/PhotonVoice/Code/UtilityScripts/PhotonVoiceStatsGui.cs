// --------------------------------------------------------------------------------------------------------------------
// <copyright file="PhotonVoiceStatsGui.cs" company="Exit Games GmbH">
//   Part of: Photon Voice Utilities for Unity - Copyright (C) 2018 Exit Games GmbH
// </copyright>
// <summary>
// This MonoBehaviour is a basic GUI for the Photon Voice client's network statistics.
// </summary>
// <author>developer@exitgames.com</author>
// --------------------------------------------------------------------------------------------------------------------

using UnityEngine;

namespace Photon.Voice.Unity.UtilityScripts
{
    /// <summary>
    /// Basic GUI to show traffic and health statistics of the connection to Photon,
    /// toggled by shift+tab.
    /// </summary>
    /// <remarks>
    /// The shown health values can help identify problems with connection losses or performance.
    /// Example:
    /// If the time delta between two consecutive SendOutgoingCommands calls is a second or more,
    /// chances rise for a disconnect being caused by this (because acknowledgments to the server
    /// need to be sent in due time).
    /// </remarks>
    public class PhotonVoiceStatsGui : MonoBehaviour
    {
        /// <summary>Shows or hides GUI (does not affect if stats are collected).</summary>
        private bool statsWindowOn = true;

        /// <summary>Shows additional "health" values of connection.</summary>
        private bool healthStatsVisible = true;

        /// <summary>Shows additional "lower level" traffic stats.</summary>
        private bool trafficStatsOn = true;

        /// <summary>Show buttons to control stats and reset them.</summary>
        private bool buttonsOn = true;

        private bool voiceStatsOn = true;

        /// <summary>Positioning rect for window.</summary>
        private Rect statsRect = new Rect(0, 100, 300, 50);

        /// <summary>Unity GUI Window ID (must be unique or will cause issues).</summary>
        private int windowId = 200;

        /// <summary>The peer currently in use (to set the network simulation).</summary>
        private Client.PhotonPeer peer;

        private VoiceConnection voiceConnection;

        private VoiceClient voiceClient;

        private void Start()
        {
            VoiceConnection[] voiceConnections = this.GetComponents<VoiceConnection>();
            if (voiceConnections == null || voiceConnections.Length == 0)
            {
                Debug.LogError("No VoiceConnection component found, PhotonVoiceStatsGui disabled", this);
                this.enabled = false;
                return;
            }
            if (voiceConnections.Length > 1)
            {
                Debug.LogWarningFormat(this, "Multiple VoiceConnection components found, using first occurrence attached to GameObject {0}", voiceConnections[0].name);
            }
            this.voiceConnection = voiceConnections[0];
            this.voiceClient = this.voiceConnection.VoiceClient;
            this.peer = this.voiceConnection.Client.RealtimePeer;
            this.statsSnapshot = this.peer.Stats.ToSnapshot();
            this.peer.Stats.ResetMaximumCounters();
            if (this.statsRect.x <= 0)
            {
                this.statsRect.x = Screen.width - this.statsRect.width;
            }
        }

        /// <summary>Checks for shift+tab input combination (to toggle statsOn).</summary>
        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.Tab) && Input.GetKey(KeyCode.LeftShift))
            {
                this.statsWindowOn = !this.statsWindowOn;
            }
        }

        private void OnGUI()
        {
            if (!this.statsWindowOn)
            {
                return;
            }

            this.statsRect = GUILayout.Window(this.windowId, this.statsRect, this.TrafficStatsWindow, "Voice Client Messages (shift+tab)");
        }

        Client.TrafficStatsSnapshot statsSnapshot;

        private void TrafficStatsWindow(int windowId)
        {
            bool statsToLog = false;
            var delta = new Client.TrafficStatsDelta(statsSnapshot, this.peer.Stats.ToSnapshot());

            GUILayout.BeginHorizontal();
            this.buttonsOn = GUILayout.Toggle(this.buttonsOn, "buttons");
            this.healthStatsVisible = GUILayout.Toggle(this.healthStatsVisible, "health");
            this.trafficStatsOn = GUILayout.Toggle(this.trafficStatsOn, "traffic");
            this.voiceStatsOn = GUILayout.Toggle(this.voiceStatsOn, "voice stats");
            GUILayout.EndHorizontal();

            string total = string.Format("Out {0,4} | In {1,4} | Sum {2,4}", delta.PackagesOut, delta.PackagesIn, delta.PackagesOut + delta.PackagesIn);
            string elapsedTime = string.Format("{0} sec average:", delta.DeltaTime / 1000);
            string average = delta.DeltaTime > 0 ? string.Format("Out {0,4} | In {1,4} | Sum {2,4}", delta.PackagesOut * 1000 / delta.DeltaTime, delta.PackagesIn * 1000 / delta.DeltaTime, (delta.PackagesOut + delta.PackagesIn) * 1000 / delta.DeltaTime) : "";
            GUILayout.Label(total);
            GUILayout.Label(elapsedTime);
            GUILayout.Label(average);

            if (this.buttonsOn)
            {
                GUILayout.BeginHorizontal();
                if (GUILayout.Button("Reset"))
                {
                    this.statsSnapshot = this.peer.Stats.ToSnapshot();
                    this.peer.Stats.ResetMaximumCounters();
                }
                statsToLog = GUILayout.Button("To Log");
                GUILayout.EndHorizontal();
            }

            if (this.trafficStatsOn)
            {
                GUILayout.Label(delta.ToString(true, true, true));
            }

            string healthStats = string.Empty;
            if (this.healthStatsVisible)
            {
                GUILayout.Box("Voice Client Health Stats");
                healthStats = string.Format(
                    "ping: {0}|{1}[+/-{2}|{3}]ms resent:{4} \n\nmax ms between\nsend: {5,4} \ndispatch: {6,4}",
                    this.peer.Stats.RoundtripTime,
                    this.voiceClient.RoundTripTime,
                    this.peer.Stats.RoundtripTimeVariance,
                    this.voiceClient.RoundTripTimeVariance,
                    delta.UdpReliableCommandsResent,
                    this.peer.Stats.LongestDeltaBetweenSendOutgoingCalls,
                    this.peer.Stats.LongestDeltaBetweenDispatchCalls);
                GUILayout.Label(healthStats);
            }

            string voiceStats = string.Empty;
            if (this.voiceStatsOn)
            {
                GUILayout.Box("Voice Frames Stats");
                voiceStats = string.Format("received: {0}, {1:F2}/s\nlost: {2}, {3:F2}/s ({4:F2}%) \nfec: {5}, frag part: {6}\nsent: {7} ({8} bytes)",
                    this.voiceClient.FramesReceived,
                    this.voiceConnection.FramesReceivedPerSecond,
                    this.voiceClient.FramesLost,
                    this.voiceConnection.FramesLostPerSecond,
                    this.voiceConnection.FramesLostPercent,
                    this.voiceClient.FramesRecovered,
                    this.voiceClient.FramesFragPart,
                    this.voiceClient.FramesSent,
                    this.voiceClient.FramesSentBytes);
                GUILayout.Label(voiceStats);
            }

            if (statsToLog)
            {
                string complete = string.Format("{0}\n{1}\n{2}\n{3}\n{4}", total, elapsedTime, average, this.peer.Stats.ToString(), healthStats);
                Debug.Log(complete);
            }

            // if anything was clicked, the height of this window is likely changed. reduce it to be layouted again next frame
            if (GUI.changed)
            {
                this.statsRect.height = 100;
            }

            GUI.DragWindow();
        }
    }
}


