#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

public class MainMenuBuilder
{
    [MenuItem("Tools/Generate Main Menu UI")]
    public static void GenerateUI()
    {
        // 1. Create Canvas
        GameObject canvasGO = new GameObject("Canvas");
        Canvas canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        CanvasScaler scaler = canvasGO.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        canvasGO.AddComponent<GraphicRaycaster>();
        Undo.RegisterCreatedObjectUndo(canvasGO, "Create Canvas");

        // 2. EventSystem
        if (Object.FindObjectOfType<EventSystem>() == null)
        {
            GameObject eventSystem = new GameObject("EventSystem");
            eventSystem.AddComponent<EventSystem>();
            eventSystem.AddComponent<StandaloneInputModule>();
            Undo.RegisterCreatedObjectUndo(eventSystem, "Create EventSystem");
        }

        // Get standard UI sprites
        Sprite bgSprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Background.psd");
        Sprite checkmarkSprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Checkmark.psd");
        Sprite knobSprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/Knob.psd");

        DefaultControls.Resources uiResources = new DefaultControls.Resources
        {
            standard = bgSprite,
            background = bgSprite,
            checkmark = checkmarkSprite,
            knob = knobSprite
        };

        // 3. Title Text
        GameObject titleGO = DefaultControls.CreateText(uiResources);
        titleGO.name = "TitleText";
        titleGO.transform.SetParent(canvasGO.transform, false);
        Text titleText = titleGO.GetComponent<Text>();
        titleText.text = "GAME TITLE";
        titleText.fontSize = 100;
        titleText.alignment = TextAnchor.MiddleCenter;
        titleText.horizontalOverflow = HorizontalWrapMode.Overflow;
        titleText.verticalOverflow = VerticalWrapMode.Overflow;
        titleText.color = Color.white;
        RectTransform titleRect = titleGO.GetComponent<RectTransform>();
        titleRect.anchorMin = new Vector2(0.5f, 0.8f);
        titleRect.anchorMax = new Vector2(0.5f, 0.8f);
        titleRect.anchoredPosition = Vector2.zero;
        titleRect.sizeDelta = new Vector2(800, 150);

        // 4. Start Button
        GameObject startBtnGO = DefaultControls.CreateButton(uiResources);
        startBtnGO.name = "StartGameButton";
        startBtnGO.transform.SetParent(canvasGO.transform, false);
        RectTransform startRect = startBtnGO.GetComponent<RectTransform>();
        startRect.anchorMin = new Vector2(0.5f, 0.5f);
        startRect.anchorMax = new Vector2(0.5f, 0.5f);
        startRect.anchoredPosition = Vector2.zero;
        startRect.sizeDelta = new Vector2(300, 80);
        
        Button startButton = startBtnGO.GetComponent<Button>();
        ColorBlock cb = startButton.colors;
        cb.normalColor = Color.white;
        cb.highlightedColor = Color.yellow; // Changes to yellow on hover!
        startButton.colors = cb;
        
        Text startText = startBtnGO.GetComponentInChildren<Text>();
        startText.text = "Start Game";
        startText.fontSize = 36;
        startText.color = Color.black;

        // 5. Tutorial Toggle (Bottom Right)
        GameObject toggleGO = DefaultControls.CreateToggle(uiResources);
        toggleGO.name = "TutorialToggle";
        toggleGO.transform.SetParent(canvasGO.transform, false);
        RectTransform toggleRect = toggleGO.GetComponent<RectTransform>();
        toggleRect.anchorMin = new Vector2(1, 0); // Bottom Right
        toggleRect.anchorMax = new Vector2(1, 0);
        toggleRect.pivot = new Vector2(1, 0);
        toggleRect.anchoredPosition = new Vector2(-50, 50); // Offset padding
        toggleRect.sizeDelta = new Vector2(180, 40); // Make it slightly larger
        
        Text toggleText = toggleGO.GetComponentInChildren<Text>();
        toggleText.text = "Tutorial";
        toggleText.fontSize = 28;
        toggleText.color = Color.white;

        // 6. Audio Slider (Left Side)
        GameObject sliderGO = DefaultControls.CreateSlider(uiResources);
        sliderGO.name = "AudioSlider";
        sliderGO.transform.SetParent(canvasGO.transform, false);
        RectTransform sliderRect = sliderGO.GetComponent<RectTransform>();
        sliderRect.anchorMin = new Vector2(0, 0.5f); // Middle Left
        sliderRect.anchorMax = new Vector2(0, 0.5f);
        sliderRect.pivot = new Vector2(0, 0.5f);
        sliderRect.anchoredPosition = new Vector2(50, 0); // Offset from left edge
        sliderRect.sizeDelta = new Vector2(200, 20);

        // Add a helpful label above the slider
        GameObject audioLabelGO = DefaultControls.CreateText(uiResources);
        audioLabelGO.name = "AudioLabel";
        audioLabelGO.transform.SetParent(sliderGO.transform, false);
        Text audioLabel = audioLabelGO.GetComponent<Text>();
        audioLabel.text = "Audio Volume";
        audioLabel.fontSize = 24;
        audioLabel.alignment = TextAnchor.MiddleCenter;
        audioLabel.color = Color.white;
        audioLabel.horizontalOverflow = HorizontalWrapMode.Overflow;
        audioLabel.verticalOverflow = VerticalWrapMode.Overflow;
        RectTransform audioLabelRect = audioLabelGO.GetComponent<RectTransform>();
        audioLabelRect.anchorMin = new Vector2(0.5f, 1f);
        audioLabelRect.anchorMax = new Vector2(0.5f, 1f);
        audioLabelRect.pivot = new Vector2(0.5f, 0f);
        audioLabelRect.anchoredPosition = new Vector2(0, 15);
        audioLabelRect.sizeDelta = new Vector2(200, 40);

        Debug.Log("Main Menu UI Generated! You can press Ctrl+Z to undo if you want.");
    }
}
#endif
