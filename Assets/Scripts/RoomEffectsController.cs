using UnityEngine;
// using UnityEngine.Rendering.Universal; // Assuming URP, but might be Built-in. User didn't specify.

public class RoomEffectsController : MonoBehaviour
{
    [Header("Visuals")]
    public Light roomLight; // Assign in inspector
    public SpriteRenderer overlay; // Optional red overlay?


    private float baseIntensity = 1f;
    
    // Simple flicker logic
    private float flickerTimer;

    public void Initialize()
    {
        // Try to find a global light in the room if not assigned
        if (roomLight == null)
        {
            roomLight = GetComponentInChildren<Light>();
        }
        if (roomLight != null)
        {
            baseIntensity = roomLight.intensity;
        }
    }

    public void UpdateEffects(float pressurePercent)
    {
        // pressurePercent is 0 to 1
        
        // Example: Dim lights as pressure goes up (or flicker)
        // User said: "Flickering lights", "Vignette strength"
        
        // We will simulate flicker based on pressure
        // Higher pressure = more frequent/intense flicker
        
        DoFlicker(pressurePercent);
    }

    void DoFlicker(float pressure)
    {
        if (roomLight == null) return;

        if (pressure > 0.5f) // Mid Pressure starts flickering
        {
            flickerTimer -= Time.deltaTime;
            if (flickerTimer <= 0)
            {
                // Random intensity
                float flickerStrength = (pressure - 0.5f) * 2f; // 0 to 1 scaling for the upper half
                roomLight.intensity = baseIntensity * Random.Range(1f - flickerStrength, 1f + (flickerStrength * 0.2f));
                flickerTimer = Random.Range(0.05f, 0.2f);
            }
        }
        else
        {
            // Reset to normal smoothly
            roomLight.intensity = Mathf.Lerp(roomLight.intensity, baseIntensity, Time.deltaTime * 5f);
        }
    }
}
