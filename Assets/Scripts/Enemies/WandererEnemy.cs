using UnityEngine;

public class WandererEnemy : EnemyAI
{
    // Wanderer uses the base EnemyAI behavior (simple chase)
    // Just configure different stats
    
    protected override void OnEnemyStart()
    {
        // Set Wanderer-specific stats only if not already configured
        // This allows SplitterEnemy or Inspector to override these values
        if (maxHealth == 100f) // 100f is the default from EnemyAI
        {
            maxHealth = 40f;
        }
        currentHealth = maxHealth;
        // speed = 4.5f; // Removed to allow Inspector value
        
        Debug.Log($"WandererEnemy: Initialized with health={maxHealth}");
    }

    protected override void UpdateAnimation(Vector2 velocity)
    {
        // Call base for sorting order
        base.UpdateAnimation(velocity);

        // Override sprite flipping to face TOWARD player (opposite of default)
        if (spriteRenderer != null && velocity.sqrMagnitude > 0.01f)
        {
            // Invert the default logic: flip when moving RIGHT, don't flip when moving LEFT
            if (velocity.x < -0.01f) spriteRenderer.flipX = false;
            else if (velocity.x > 0.01f) spriteRenderer.flipX = true;
        }
    }
}
