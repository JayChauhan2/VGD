using UnityEngine;

/// <summary>
/// Type4_Echolocation familiar — permanently makes the echolocation ripple slower
/// and stay on screen longer.
/// Effect is reversed if the familiar is ever destroyed.
/// </summary>
public class FamiliarEcholocation : MonoBehaviour
{
    [Tooltip("Multiplier applied to EcholocationController ping interval and expand speed. (e.g. 2.0 = half speed, 2x duration)")]
    public float slowdownMultiplier = 2.0f;

    void Start()
    {
        EcholocationController.pingIntervalMultiplier = slowdownMultiplier;
        Debug.Log($"[FamiliarEcholocation] Echolocation is {slowdownMultiplier}x slower (Multiplier: {slowdownMultiplier}).");
    }

    void OnDestroy()
    {
        // Reset to default
        EcholocationController.pingIntervalMultiplier = 1.0f;
        Debug.Log($"[FamiliarEcholocation] Reset echolocation speed to normal.");
    }
}
