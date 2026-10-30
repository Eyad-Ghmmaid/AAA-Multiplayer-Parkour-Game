#if CMPSETUP_COMPLETE
using AvocadoShark;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

public class PopUp : MonoBehaviour
{
    public CanvasGroup canvasGroup;
    [SerializeField] private  TextMeshProUGUI content;

    public void ShowPopup(string text)
    {
        content.text = text;
        gameObject.SetActive(true);
    }

    public async void DisablePopup()
    {
        var fusionManager = FindFirstObjectByType<FusionConnection>();
        var activeRunner = fusionManager != null ? fusionManager.Runner : null;
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
            if (fusionManager != null)
                Destroy(fusionManager);

            SceneManager.LoadScene("Menu");
        }
    }
}
#endif
