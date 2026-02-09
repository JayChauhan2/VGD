using UnityEngine;

public class GenericEnemy : EnemyAI
{
    [Header("Generic Enemy Settings")]
    public float attackRange = 1.0f;
    public float attackDamage = 10f;
    public float attackCooldown = 1.5f;
    
    // Optional: override start values
    protected override void OnEnemyStart()
    {
        // Set default stats if not set in inspector
        if (maxHealth <= 0) maxHealth = 50f;
        currentHealth = maxHealth;
        
        // Ensure standard collider sizes if needed
        if (GetComponent<Collider2D>() == null)
        {
            CircleCollider2D col = gameObject.AddComponent<CircleCollider2D>();
            col.radius = 0.4f;
        }

        Debug.Log($"GenericEnemy {gameObject.name} Initialized.");
    }

    protected override void OnEnemyUpdate()
    {
        if (target == null) return;

        float dist = Vector2.Distance(transform.position, target.position);
        
        // Simple melee logic: If close enough, deal damage (handled via collision mostly, but could add explicit attack here)
        // The base EnemyAI already handles collision damage via OnCollisionStay2D.
        // So this script is mainly just a concrete implementation to make the abstract class work.
    }
}
