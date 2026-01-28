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
}
