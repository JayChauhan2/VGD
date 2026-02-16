using UnityEngine;
using UnityEngine.UI;

public class LaserEnergyUI : MonoBehaviour
{
    [SerializeField] private Laser laserScript;
    [SerializeField] private Image energyBarFill; // Assign the fill image of a bar
    [SerializeField] private CanvasGroup canvasGroup; // Optional: to fade out when full?
    void Update()
    {
        if (laserScript != null && energyBarFill != null)
        {
            float fillAmount = laserScript.GetEnergyPercent();
            energyBarFill.fillAmount = fillAmount;

            // Optional color feedback
            if (fillAmount < 0.2f) energyBarFill.color = Color.red;
            else energyBarFill.color = Color.cyan;
        }
    }

    void Start()
    {
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
            Debug.Log("LaserEnergyUI: Auto-adjusted parent CanvasScaler for 16:9 aspect ratio.");
        }
    }
}
