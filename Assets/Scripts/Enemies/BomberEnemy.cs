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
        
        float waitTime = explosionDelay; // Default fallback

        // Trigger Animation
        if (animator != null)
        {
            animator.SetTrigger("Explode");
            
            // Wait a frame for the Animator to process the trigger and start transition
            yield return null; 
            
            // Get the duration of the new state
            // If transitioning, we want the next state's length. If not (instant), current state.
            AnimatorStateInfo info = animator.GetCurrentAnimatorStateInfo(0);
            if (animator.IsInTransition(0))
            {
                info = animator.GetNextAnimatorStateInfo(0);
            }
            
            // Use animation length if valid, otherwise stick to default
            if (info.length > 0)
            {
                waitTime = info.length;
            }
        }
        
        Debug.Log($"BomberEnemy: specific sequence started. Exploding in {waitTime}s (Animation Sync)...");
        
        // Wait for animation
        yield return new WaitForSeconds(waitTime);
        
        // BOOM
        Explode();
        
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
            
            // Trigger explosion if VERY close (reduced from radius-based to fixed close range)
            if (distanceToPlayer < 1.2f) 
            {
                StartCoroutine(ExplodeSequence());
            }
        }
    }
}
