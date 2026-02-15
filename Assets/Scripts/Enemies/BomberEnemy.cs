using UnityEngine;
using System.Collections;

public class BomberEnemy : EnemyAI
{
    [Header("Bomber Settings")]
    public float explosionRadius = 3f;
    public float playerDamage = 30f;
    public float enemyDamage = 60f;
    public float explosionDelay = 0.5f; // Time to wait for animation before exploding
    
    private bool isExploding = false;

    protected override void OnEnemyStart()
    {
        maxHealth = 60f;
        currentHealth = maxHealth;
        // speed = 3.5f; // Removed hardcoded speed to allow Inspector control
        
        Debug.Log("BomberEnemy: Initialized - will explode on death!");
    }

    protected override void OnEnemyDeath()
    {
        // This is called by base.Die() -> DropLoot() -> OnEnemyDeath() -> Destroy(gameObject)
        // BUT, we want to intercept the death to play animation FIRST.
        // So we actually need to override Die() to control the flow, OR we can use OnEnemyDeath 
        // if we change how base.Die works. 
        // Looking at EnemyAI.cs, Die() calls OnEnemyDeath() then Destroy().
        // To delay destruction, we must override Die().
    }

    protected override void Die()
    {
        if (isExploding) return; // Already dying/exploding
        
        StartCoroutine(ExplodeSequence());
    }
    
    // Override TakeDamage to ignore damage while exploding
    public override void TakeDamage(float damage)
    {
        if (isExploding) return;
        base.TakeDamage(damage);
    }

    private IEnumerator ExplodeSequence()
    {
        isExploding = true;
        
        // Stop movement
        speed = 0f;
        if (rb != null) rb.linearVelocity = Vector2.zero;
        if (pathfinding != null) pathfinding = null; // Stop path updates
        
        // Trigger Animation
        if (animator != null)
        {
            animator.SetTrigger("Explode");
        }
        
        Debug.Log($"BomberEnemy: specific sequence started. Exploding in {explosionDelay}s...");
        
        // Wait for animation
        yield return new WaitForSeconds(explosionDelay);
        
        // BOOM
        Explode();
        
        // Now call base.Die() to handle loot and destruction
        // We set isExploding to false briefly if we want base.Die to work normally? 
        // No, we overrode Die(). We need to manually do what base.Die() does minus the recursion.
        // Or we can just call the cleanup parts.
        
        // Actually, since we overrode Die(), calling base.Die() would just call our Die() again if we aren't careful?
        // No, base.Die() calls EnemyAI.Die().
        // But our Die() calls ExplodeSequence().
        // We need to call the cleanup logic.
        
        // Let's look at base.Die() logic again:
        // DropLoot();
        // OnEnemyDeath();
        // parentRoom.EnemyDefeated(this);
        // Destroy(gameObject);
        
        // We should replicate that or modify EnemyAI to be friendlier. 
        // For now, let's replicate the important parts to be safe and avoid recursion issues if structure changes.
        
        DropLoot();
        if (parentRoom != null) parentRoom.EnemyDefeated(this);
        Destroy(gameObject);
    }

    void Explode()
    {
        Debug.Log("BomberEnemy: EXPLODING!");
        
        // Create explosion effect
        ExplosionEffect.CreateExplosion(transform.position, explosionRadius, playerDamage, enemyDamage);
    }

    protected override void OnEnemyUpdate()
    {
        if (isExploding) return;

        // Trigger explosion if VERY close
        if (target != null)
        {
            float distanceToPlayer = Vector2.Distance(transform.position, target.position);
            
            if (distanceToPlayer < explosionRadius * 0.8f) // e.g. < 2.4f
            {
                StartCoroutine(ExplodeSequence());
            }
        }
    }
}
