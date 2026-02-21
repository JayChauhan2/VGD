using UnityEngine;
using UnityEngine.SceneManagement;

public class LevelLoader : MonoBehaviour
{
    [Header("Settings")]
    public string sceneToLoad;

    public void LoadLevel()
    {
        Debug.Log($"LevelLoader: Button clicked on '{gameObject.name}'!");

        // Remove ALL spaces (internal and external) to match FirstLevel even if user types First Level
        string cleanSceneName = sceneToLoad.Replace(" ", "").Trim();

        if (string.IsNullOrEmpty(cleanSceneName))
        {
            Debug.LogError("LevelLoader: No scene name assigned to load!");
            return;
        }

        Debug.Log($"LevelLoader: Attempting to load scene '{cleanSceneName}'...");

        if (SceneTransitionManager.Instance != null)
        {
            SceneTransitionManager.Instance.FadeToLevel(cleanSceneName);
        }
        else
        {
            Debug.LogWarning("LevelLoader: SceneTransitionManager not found in scene. Loading directly (no fade).");
            SceneManager.LoadScene(cleanSceneName);
        }
    }
}
