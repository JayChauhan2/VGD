using UnityEngine;
using UnityEditor;
using UnityEngine.UI;

public class MainMenuSetup : EditorWindow
{
    [MenuItem("Tools/Setup Main Menu")]
    public static void Setup()
    {
        // 1. Find or Create MainMenuController
        MainMenuController controller = Object.FindFirstObjectByType<MainMenuController>();
        if (controller == null)
        {
            GameObject controllerGO = new GameObject("MainMenuController");
            controller = controllerGO.AddComponent<MainMenuController>();
            Debug.Log("Created MainMenuController object.");
        }

        // 2. Find the Start Game Button
        Button startButton = null;
        Button[] buttons = Object.FindObjectsByType<Button>(FindObjectsSortMode.None);
        foreach (var b in buttons)
        {
            Text t = b.GetComponentInChildren<Text>();
            if (t != null && t.text.ToLower().Contains("start"))
            {
                startButton = b;
                break;
            }
            if (b.name.ToLower().Contains("start"))
            {
                startButton = b;
                break;
            }
        }

        if (startButton != null)
        {
            // Clear existing and add new
            UnityEditor.Events.UnityEventTools.RemovePersistentListener(startButton.onClick, 0); // Clear at least one if exists
            UnityEditor.Events.UnityEventTools.AddPersistentListener(startButton.onClick, controller.StartGame);
            
            EditorUtility.SetDirty(startButton);
            Debug.Log($"Successfully hooked up Start Game button ({startButton.name}) to MainMenuController.");
        }
        else
        {
            Debug.LogWarning("Could not find a button that looks like a 'Start' button.");
        }

        // 3. Find or Create SceneTransitionManager (optional, it auto-creates, but good to have)
        SceneTransitionManager transitionManager = Object.FindFirstObjectByType<SceneTransitionManager>();
        if (transitionManager == null)
        {
            GameObject tmGO = new GameObject("SceneTransitionManager");
            tmGO.AddComponent<SceneTransitionManager>();
            Debug.Log("Created SceneTransitionManager object.");
        }

        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
        Debug.Log("Main Menu Setup Complete! Please save your scene.");
    }
}
