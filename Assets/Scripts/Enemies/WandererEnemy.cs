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
    // Note: UpdateAnimation uses the base EnemyAI logic:
    //   velocity.x < 0  (moving left)  → flipX = true  (sprite faces left)
    //   velocity.x > 0  (moving right) → flipX = false (sprite faces right)
    // The previous override that inverted these directions has been removed.
}
