using UnityEngine;
using UnityEngine.SceneManagement;

public class MainMenuController : MonoBehaviour
{
    public string firstLevelName = "FirstLevel";

    public void StartGame()
    {
        Debug.Log("MainMenuController: Starting game...");
        if (SceneTransitionManager.Instance != null)
        {
            SceneTransitionManager.Instance.FadeToLevel(firstLevelName);
        }
        else
        {
            Debug.LogWarning("SceneTransitionManager not found, loading scene directly.");
            SceneManager.LoadScene(firstLevelName);
        }
    }

    public void QuitGame()
    {
        Debug.Log("MainMenuController: Quitting game...");
        Application.Quit();
    }
}
