using UnityEngine;
using UnityEngine.UI;

public class LaserEnergyUI : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Laser laserScript;
    [SerializeField] private Animator animator;
    [SerializeField] private Image laserBarImage; // The Image component that displays the sprite
    
    [Header("Positioning")]
    [SerializeField] private Vector2 anchorPosition = new Vector2(20f, 20f); // Bottom-left offset
    
    private static readonly string ANIMATION_NAME = "LaserCharge";
    private RectTransform rectTransform;

    void Start()
    {
        rectTransform = GetComponent<RectTransform>();
        
        // Position in bottom-left corner
        if (rectTransform != null)
        {
            rectTransform.anchorMin = new Vector2(0, 0); // Bottom-left anchor
            rectTransform.anchorMax = new Vector2(0, 0);
            rectTransform.pivot = new Vector2(0, 0);
            rectTransform.anchoredPosition = anchorPosition;
        }
        
        // Auto-fix for Aspect Ratio issues
        Canvas canvas = GetComponentInParent<Canvas>();
        if (canvas != null)
        {
            CanvasScaler scaler = canvas.gameObject.GetComponent<CanvasScaler>();
            if (scaler == null) scaler = canvas.gameObject.AddComponent<CanvasScaler>();
            
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;
        }
        
        // Validate references
        if (animator == null)
        {
            Debug.LogError("LaserEnergyUI: Animator not assigned! Please assign the Animator component.");
        }
        if (laserBarImage == null)
        {
            Debug.LogError("LaserEnergyUI: LaserBarImage not assigned! Please assign the Image component.");
        }
    }

    void Update()
    {
        if (laserScript != null && animator != null)
        {
            // Get energy percentage (0.0 to 1.0)
            float energyPercent = laserScript.GetEnergyPercent();
            
            // Control animation playback using normalized time
            // This smoothly transitions between all 15 sprite frames
            animator.Play(ANIMATION_NAME, 0, energyPercent);
            animator.speed = 0; // Ensure animation doesn't auto-play
        }
    }
}
