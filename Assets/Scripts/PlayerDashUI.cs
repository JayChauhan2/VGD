using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(PlayerMovement))]
public class PlayerDashUI : MonoBehaviour
{
    // Configuration similar to EnemyHealthBar but for Player Dash
    public Vector3 offset = new Vector3(0, 1.2f, 0); // Slightly higher than player center
    public Vector2 size = new Vector2(0.8f, 0.1f);   // Wider / thinner bar

    private PlayerMovement playerMovement;
    private GameObject canvasGO;
    private Image fillImage;
    private Canvas canvas;

    void Start()
    {
        playerMovement = GetComponent<PlayerMovement>();
        CreateDashUI();
    }

    void LateUpdate()
    {
        if (canvasGO != null)
        {
            // Force world position to be player position + offset (ignoring player rotation)
            // This ensures the bar is always "above" the player on the screen, not relative to player's head direction
            canvasGO.transform.position = transform.position + offset;

            // Billboard effect
            if (Camera.main != null)
                canvasGO.transform.rotation = Camera.main.transform.rotation;
        }

        UpdateDashBar();
    }

    void CreateDashUI()
    {
        // 1. Create Canvas GameObject
        canvasGO = new GameObject("DashBarCanvas");
        canvasGO.transform.SetParent(transform);
        canvasGO.transform.localPosition = offset;
        canvasGO.transform.localRotation = Quaternion.identity;

        canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        
        // Ensure it draws on top of sprites
        canvas.sortingOrder = 10; 

        CanvasScaler scaler = canvasGO.AddComponent<CanvasScaler>();
        scaler.dynamicPixelsPerUnit = 100;

        RectTransform canvasRect = canvasGO.GetComponent<RectTransform>();
        canvasRect.sizeDelta = size;

        // 2. Background (Gray/White-ish per user request "gray/white background")
        // User said: "dash bar should be white white like gray/white background u choose"
        // Let's go with a Dark Gray background to make the White bar pop.
        GameObject bgGO = new GameObject("Background");
        bgGO.transform.SetParent(canvasGO.transform, false);
        Image bgImage = bgGO.AddComponent<Image>();
        bgImage.color = new Color(0.2f, 0.2f, 0.2f, 0.8f); // Dark gray
        
        RectTransform bgRect = bgGO.GetComponent<RectTransform>();
        bgRect.anchorMin = Vector2.zero;
        bgRect.anchorMax = Vector2.one;
        bgRect.sizeDelta = Vector2.zero;

        // 3. Fill (White)
        GameObject fillGO = new GameObject("Fill");
        fillGO.transform.SetParent(canvasGO.transform, false);
        fillImage = fillGO.AddComponent<Image>();
        fillImage.color = Color.white;
        
        RectTransform fillRect = fillGO.GetComponent<RectTransform>();
        fillRect.anchorMin = Vector2.zero;
        fillRect.anchorMax = Vector2.one;
        fillRect.sizeDelta = Vector2.zero;
        
        // Pivot Left for scaling
        fillRect.pivot = new Vector2(0, 0.5f);
        
        // Initially hide
        canvasGO.SetActive(false);
    }

    void UpdateDashBar()
    {
        if (playerMovement == null || canvasGO == null) return;

        // "shows up only when the player has run out of dash power" meaning cooldown < max
        // If ready, hide it.
        if (playerMovement.IsDashReady)
        {
            if (canvasGO.activeSelf) 
                canvasGO.SetActive(false);
        }
        else
        {
            if (!canvasGO.activeSelf) 
                canvasGO.SetActive(true);

            // Update fill
            // DashCooldownProgress goes 0 -> 1
            float progress = playerMovement.DashCooldownProgress;
            fillImage.rectTransform.localScale = new Vector3(progress, 1, 1);
        }
    }
}
