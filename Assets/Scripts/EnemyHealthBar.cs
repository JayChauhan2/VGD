using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(EnemyAI))]
public class EnemyHealthBar : MonoBehaviour
{
    public Vector3 offset = new Vector3(0, 1f, 0);
    public Vector2 size = new Vector2(0.6f, 0.03f);

    private Image healthFillImage;
    private Canvas healthCanvas;
    private EnemyAI enemyAI;

    void Start()
    {
        enemyAI = GetComponent<EnemyAI>();
        SetupHealthBar();
        
        enemyAI.OnHealthChanged += UpdateHealthBar;
        // Initial update
        // We can't easily get current health without modifying EnemyAI to expose it publicly or just waiting for event
        // But EnemyAI invokes event in Start, so if we run after, we catch it? 
        // Or we might miss it if execution order differs.
        // Safer to not worry about initial sync if event is fired in Start, provided this component initializes fast enough.
        // Actually, best to ensure we catch it.
    }

    void OnDestroy()
    {
        if (enemyAI != null)
        {
            enemyAI.OnHealthChanged -= UpdateHealthBar;
        }
    }

    void LateUpdate()
    {
        if (healthCanvas != null)
        {
            // Billboard effect: Face the camera
            healthCanvas.transform.rotation = Camera.main.transform.rotation;
        }
    }

    void SetupHealthBar()
    {
        // 1. Create Canvas GameObject
        GameObject canvasGO = new GameObject("HealthBarCanvas");
        canvasGO.transform.SetParent(transform);
        canvasGO.transform.localPosition = offset;
        canvasGO.transform.localRotation = Quaternion.identity;

        healthCanvas = canvasGO.AddComponent<Canvas>();
        healthCanvas.renderMode = RenderMode.WorldSpace;
        
        CanvasScaler scaler = canvasGO.AddComponent<CanvasScaler>();
        scaler.dynamicPixelsPerUnit = 100;

        RectTransform canvasRect = canvasGO.GetComponent<RectTransform>();
        canvasRect.sizeDelta = new Vector2(size.x, size.y);

        // 2. Create Background Image (Gray)
        GameObject bgGO = new GameObject("Background");
        bgGO.transform.SetParent(canvasGO.transform, false);
        Image bgImage = bgGO.AddComponent<Image>();
        bgImage.color = Color.gray;
        
        RectTransform bgRect = bgGO.GetComponent<RectTransform>();
        bgRect.anchorMin = Vector2.zero;
        bgRect.anchorMax = Vector2.one;
        bgRect.sizeDelta = Vector2.zero;

        // 3. Create Fill Image (yellow)
        GameObject fillGO = new GameObject("Fill");
        fillGO.transform.SetParent(canvasGO.transform, false);
        healthFillImage = fillGO.AddComponent<Image>();
        healthFillImage.color = Color.yellow;
        // Don't use Type.Filled as it requires a Sprite to work reliably
        // healthFillImage.type = Image.Type.Filled; 
        
        RectTransform fillRect = fillGO.GetComponent<RectTransform>();
        fillRect.anchorMin = Vector2.zero;
        fillRect.anchorMax = Vector2.one; // Start full
        fillRect.sizeDelta = Vector2.zero;
        
        // Ensure Pivot is Left so it shrinks from right to left
        fillRect.pivot = new Vector2(0, 0.5f);
    }

    void UpdateHealthBar(float current, float max)
    {
        if (healthFillImage != null)
        {
            float pct = Mathf.Clamp01(current / max);
            // using localScale X to scale the bar
            healthFillImage.rectTransform.localScale = new Vector3(pct, 1, 1);
        }
    }
}
