using UnityEngine;

public class ShadowStalkerEnemy : EnemyAI
{
    [Header("Shadow Stalker Settings")]
    public float normalAlpha = 0.0f; // Invisible by default
    public float revealedAlpha = 1f; // Fully visible when close
    public float revealSmoothTime = 0.2f;
    
    private SpriteRenderer spriteRenderer;
    private EcholocationController echoController;
    private float currentAlpha;
    private float alphaVelocity; // For smooth damping

    private EnemyHealthBar healthBar;

    protected override void OnEnemyStart()
    {
        // Force normalAlpha to 0 to override any stale Prefab/Inspector values
        normalAlpha = 0f;
        
        maxHealth = 80f;
        currentHealth = maxHealth;
        
        spriteRenderer = GetComponent<SpriteRenderer>();
        healthBar = GetComponent<EnemyHealthBar>();
        
        // Start invisible
        if (spriteRenderer != null)
        {
            currentAlpha = normalAlpha;
            Color color = spriteRenderer.color;
            color.a = currentAlpha;
            spriteRenderer.color = color;
        }
        
        if (healthBar != null)
        {
            healthBar.SetVisibility(false);
        }
        
        echoController = FindFirstObjectByType<EcholocationController>();
        if (echoController == null)
        {
            Debug.LogWarning("ShadowStalkerEnemy: EcholocationController not found.");
        }
        
        Debug.Log("ShadowStalkerEnemy: Initialized in true shadow form");
    }

    protected override void OnEnemyUpdate()
    {
        UpdateVisibility();
    }

    void UpdateVisibility()
    {
        if (spriteRenderer == null) return;

        float targetAlpha = normalAlpha;

        // Check distance to player
        if (target != null) // target is inherited from EnemyAI and assigned to Player
        {
            float dist = Vector2.Distance(transform.position, target.position);
            
            // Get visibility radius from EcholocationController if available, otherwise default
            float visibilityRadius = (echoController != null) ? echoController.playerRadius : 2.5f;

            // If inside the inner circle, become visible
            if (dist <= visibilityRadius)
            {
                targetAlpha = revealedAlpha;
            }
        }

        // Smoothly transition alpha
        currentAlpha = Mathf.SmoothDamp(currentAlpha, targetAlpha, ref alphaVelocity, revealSmoothTime);
        
        // Snap to clean 0 if very small to correct "dim" ghosting
        if (Mathf.Abs(currentAlpha - normalAlpha) < 0.01f && targetAlpha == normalAlpha)
        {
            currentAlpha = normalAlpha;
        }
        
        Color color = spriteRenderer.color;
        color.a = currentAlpha;
        spriteRenderer.color = color;
        
        // COMPLETELY DISABLE RENDERER when invisible to prevent any "dim" ghosting or shader artifacts
        if (currentAlpha <= 0.01f)
        {
            if (spriteRenderer.enabled) spriteRenderer.enabled = false;
        }
        else
        {
            if (!spriteRenderer.enabled) spriteRenderer.enabled = true;
        }
        
        // Toggle Health Bar visibility
        if (healthBar != null)
        {
            // Show health bar only if enemy is significantly visible
            bool shouldShow = currentAlpha > 0.1f;
            healthBar.SetVisibility(shouldShow);
        }
    }

    // Override to prevent UI marker when hit by echolocation wave
    // We only want them to be revealed by proximity, not "detected" by the wave
    public override void MarkAsDetected()
    {
        // Only show the marker if we are already visible (close range)
        if (currentAlpha > 0.5f)
        {
            base.MarkAsDetected();
        }
    }

    // Public method called by EcholocationController - intentionally empty
    // Shadow Stalkers ignore global pulses
    public void OnEcholocationPulse()
    {
        // Ignore pulse
    }

    void OnDestroy()
    {
        // Cleanup if needed
    }
}
