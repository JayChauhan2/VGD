using UnityEngine;

/// <summary>
/// Type4_Echolocation familiar — permanently expands the player's always-visible
/// radius in the EcholocationController by radiusBoost world units.
/// Effect is reversed if the familiar is ever destroyed.
/// </summary>
public class FamiliarEcholocation : MonoBehaviour
{
    [Tooltip("Multiplier applied to EcholocationController.pingInterval. (e.g. 0.5 = 2x frequency)")]
    public float intervalMultiplier = 0.5f;

    void Start()
    {
        EcholocationController.pingIntervalMultiplier = intervalMultiplier;
        Debug.Log($"[FamiliarEcholocation] Pulsing {1f/intervalMultiplier}x faster (Multiplier: {intervalMultiplier}).");
    }

    void OnDestroy()
    {
        // Reset to default
        EcholocationController.pingIntervalMultiplier = 1.0f;
        Debug.Log($"[FamiliarEcholocation] Reset ping frequency to normal.");
    }
}
