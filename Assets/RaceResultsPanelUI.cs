using AvocadoShark;
using TMPro;
using UnityEngine;

/// <summary>Shows shared race results. Attach this to a persistent active object such as GameCanvas.</summary>
[DisallowMultipleComponent]
public sealed class RaceResultsPanelUI : MonoBehaviour
{
    [SerializeField] private GameObject resultsPanel;
    [SerializeField] private TMP_Text resultsText;

    private InGame_Manager _raceManager;

    private void Update()
    {
        if (_raceManager == null)
            _raceManager = FindAnyObjectByType<InGame_Manager>();

        if (_raceManager == null)
        {
            if (resultsPanel != null && resultsPanel.activeSelf)
                resultsPanel.SetActive(false);
            return;
        }

        var showResults = _raceManager.ShowRaceResults;
        if (resultsPanel != null && resultsPanel.activeSelf != showResults)
            resultsPanel.SetActive(showResults);

        if (resultsText != null)
            resultsText.text = _raceManager.RaceResultsLabel;
    }
}
