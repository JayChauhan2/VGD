using UnityEngine;

public class WandererEnemy : EnemyAI
{
    // Wanderer uses the base EnemyAI behavior (simple chase)
    // Just configure different stats
    
    protected override void OnEnemyStart()
    {
        // Set Wanderer-specific stats
        maxHealth = 40f;
        currentHealth = maxHealth;
        // speed = 4.5f; // Removed to allow Inspector value
        
        Debug.Log("WandererEnemy: Initialized with low health, moderate speed");
    }
}
