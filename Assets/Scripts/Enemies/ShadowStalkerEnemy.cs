using UnityEngine;

public class ShadowStalkerEnemy : EnemyAI
{
    [Header("Shadow Stalker Settings")]
    public float normalAlpha = 0.2f;
    public float pulseAlpha = 1f;
    public float pulseDuration = 0.5f;
    
    private SpriteRenderer spriteRenderer;
    private float pulseTimer;
    private bool isPulsing;

    protected override void OnEnemyStart()
    {
        maxHealth = 80f;
        currentHealth = maxHealth;
        speed = 5.5f;
        
        spriteRenderer = GetComponent<SpriteRenderer>();
        
        // Start nearly invisible
        if (spriteRenderer != null)
        {
            Color color = spriteRenderer.color;
            color.a = normalAlpha;
            spriteRenderer.color = color;
        }
        
        // Subscribe to echolocation events if available
        SubscribeToEcholocation();
        
        Debug.Log("ShadowStalkerEnemy: Initialized in shadow form");
    }

    void SubscribeToEcholocation()
    {
        // Try to find and subscribe to echolocation controller
        EcholocationController echoController = FindFirstObjectByType<EcholocationController>();
        if (echoController != null)
        {
            // Note: We'll need to add an event to EcholocationController
            // For now, we'll use a timer-based approach
            Debug.Log("ShadowStalkerEnemy: Found echolocation controller");
        }
    }

    protected override void OnEnemyUpdate()
    {
        // Handle pulse visibility
        if (isPulsing)
        {
            pulseTimer -= Time.deltaTime;
            if (pulseTimer <= 0)
            {
                EndPulse();
            }
        }
        else
        {
            // Simulate echolocation pulse every 1-2 seconds
            // In a real implementation, this would be triggered by EcholocationController events
            if (Random.value < 0.01f) // Small chance each frame (~1-2 seconds average)
            {
                StartPulse();
            }
        }
    }

    void StartPulse()
    {
        isPulsing = true;
        pulseTimer = pulseDuration;
        
        if (spriteRenderer != null)
        {
            Color color = spriteRenderer.color;
            color.a = pulseAlpha;
            spriteRenderer.color = color;
        }
    }

    void EndPulse()
    {
        isPulsing = false;
        
        if (spriteRenderer != null)
        {
            Color color = spriteRenderer.color;
            color.a = normalAlpha;
            spriteRenderer.color = color;
        }
    }

    // Public method that can be called by EcholocationController
    public void OnEcholocationPulse()
    {
        StartPulse();
    }

    void OnDestroy()
    {
        // Unsubscribe from events if needed
    }
}
