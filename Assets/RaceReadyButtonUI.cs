using AvocadoShark;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Connects a user-created uGUI Ready button to the networked race manager.</summary>
[DisallowMultipleComponent]
public sealed class RaceReadyButtonUI : MonoBehaviour
{
    [SerializeField] private UnityEngine.UI.Button readyButton;
    [SerializeField] private TMP_Text buttonLabel;
    [SerializeField] private TMP_Text raceStatusLabel;

    private InGame_Manager _raceManager;

    private void Awake()
    {
        if (readyButton == null)
            readyButton = GetComponent<UnityEngine.UI.Button>();
        if (buttonLabel == null && readyButton != null)
            buttonLabel = readyButton.GetComponentInChildren<TMP_Text>(true);
    }

    private void Update()
    {
        if (_raceManager == null)
        {
            _raceManager = FindAnyObjectByType<InGame_Manager>();
            if (_raceManager != null && readyButton != null)
                readyButton.onClick.AddListener(_raceManager.ToggleReady);
        }

        if (_raceManager == null)
            return;

        if (readyButton != null)
            readyButton.interactable = _raceManager.CanLocalPlayerToggleReady;
        if (buttonLabel != null)
            buttonLabel.text = _raceManager.ReadyButtonLabel;
        if (raceStatusLabel != null)
            raceStatusLabel.text = _raceManager.RaceStatusLabel;
    }

    private void OnDestroy()
    {
        if (_raceManager != null && readyButton != null)
            readyButton.onClick.RemoveListener(_raceManager.ToggleReady);
    }
}
