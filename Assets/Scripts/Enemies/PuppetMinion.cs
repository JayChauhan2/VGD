using UnityEngine;

public class PuppetMinion : EnemyAI
{
    // Confusion state for Puppeteer minions
    private bool isConfused = false;
    private Vector3? confusionTarget;
    private float confusionTimer;

    // Lower health bar for small minion
    public override Vector3? HealthBarOffsetOverride => new Vector3(0, 0.25f, 0);

    protected override void OnEnemyStart()
    {
        // Set Puppet-specific stats (similar to Wanderer but owned by Puppeteer)
        maxHealth = 40f;
        currentHealth = maxHealth;
        // speed is handled by EnemyAI or can be overriden here

        // Start in spawning state
        isSpawning = true;
        if (animator != null)
        {
            animator.Play("MiniPuppetSpawn");
        }

        // Fix Shadow Size:
        // This enemy is scale 2.0, but normal enemies are ~0.45.
        // We want the shadow to look "normal" (size of a 0.45 enemy).
        // Scale Factor = 0.45 / 2.0 = 0.225.
        // We apply this reduction to the SimpleShadow component's settings.
        var shadow = GetComponent<SimpleShadow>();
        if (shadow != null)
        {
            shadow.scale *= 0.225f; // Reduce to ~22.5% of default size
        }
    }

    private bool isSpawning = false;

    public void SetConfusion(bool state)
    {
        isConfused = state;
        if (isConfused)
        {
            // Pick initial random target
            PickRandomConfusionTarget();
            
            // Visual feedback (optional color change)
            if (spriteRenderer != null) spriteRenderer.color = Color.gray;
            
            // Stop chasing player
            target = null;
            path = null;
            stopWhenNoPath = false; // Allow movement without path
        }
        else
        {
            // Reset
            confusionTarget = null;
            if (spriteRenderer != null) spriteRenderer.color = Color.white;
            stopWhenNoPath = true; // Restore default behavior
            
            // Resume chasing
            FindPlayer();
        }
    }

    protected override void FindPlayer()
    {
        if (isConfused) return; // Don't look for player if confused
        base.FindPlayer();
    }

    protected override void OnEnemyUpdate()
    {
        // PRIORITY 1: Always check spawn completion first (even if confused)
        // This prevents getting stuck in spawn animation when Puppeteer is interrupted
        if (isSpawning)
        {
            // Check if spawn animation is finished
            if (animator != null)
            {
                AnimatorStateInfo stateInfo = animator.GetCurrentAnimatorStateInfo(0);
                // "MiniPuppetSpawn" is the name of the state
                if (stateInfo.IsName("MiniPuppetSpawn") && stateInfo.normalizedTime >= 1.0f)
                {
                    isSpawning = false;
                    animator.Play("MinionWalk");
                    
                    // If we were set to confused during spawning, apply the visual now
                    if (isConfused && spriteRenderer != null)
                    {
                        spriteRenderer.color = Color.gray;
                    }
                }
            }
            else
            {
                // Fallback if no animator
                isSpawning = false;
            }
            
            // Don't do anything else while spawning (stay still)
            return;
        }
        
        // PRIORITY 2: Handle confusion behavior (only after spawning is done)
        if (isConfused)
        {
            // Override base behavior: Ignore player, move randomly
            HandleConfusionBehavior();
        }
        // If not confused and not spawning, base EnemyAI.Update will handle standard pathfinding to target (Player)
    }

    private void HandleConfusionBehavior()
    {
        // If we have a confusion target, move towards it
        if (confusionTarget.HasValue)
        {
            // Simple movement towards target
            Vector2 direction = (confusionTarget.Value - transform.position).normalized;
            
            // Use MoveSafely from base if possible, or just RB velocity
            if (rb != null)
            {
                rb.linearVelocity = direction * speed;
            }
            else
            {
                transform.position += (Vector3)direction * speed * Time.deltaTime;
            }

            // Check if reached target or stuck
            if (Vector3.Distance(transform.position, confusionTarget.Value) < 0.5f)
            {
                PickRandomConfusionTarget();
            }
        }

        // Periodically change target to look erratic
        confusionTimer -= Time.deltaTime;
        if (confusionTimer <= 0)
        {
            PickRandomConfusionTarget();
        }
    }

    private void PickRandomConfusionTarget()
    {
        // Pick a random point within a small radius
        Vector2 randomOffset = Random.insideUnitCircle * 5f;
        Vector3 potentialTarget = transform.position + (Vector3)randomOffset;

        // Ensure it's valid (inside room)
        if (IsPositionValid(potentialTarget))
        {
            confusionTarget = potentialTarget;
        }
        else
        {
            // Try towards room center if invalid
            if (parentRoom != null) 
                confusionTarget = parentRoom.transform.position;
            else 
                confusionTarget = transform.position - (Vector3)randomOffset; // Move back
        }
        
        confusionTimer = Random.Range(1f, 3f);
    }
    // Override UpdateAnimation to ensure we use actual velocity/movement
    // The base EnemyAI.Update passes calculated path velocity, which is zero when confused (no path).
    protected override void UpdateAnimation(Vector2 ignoredVelocity)
    {
        if (isSpawning) return; // Don't update animation while spawning

        Vector2 actualVelocity = Vector2.zero;

        if (rb != null)
        {
            actualVelocity = rb.linearVelocity;
        }
        else
        {
            // Fallback if no RB (though EnemyAI requires one)
            // We could track lastPosition here or just use what we have
        }

        // If we are confused, we ignored the passed velocity anyway.
        // If we are NOT confused, base EnemyAI passes correct velocity, but RB velocity should also be correct.
        // Let's rely on RB velocity for animation as it's the source of truth for physics movement.
        
        base.UpdateAnimation(actualVelocity);
    }

    protected override void Die()
    {
        // Puppet minions have a large prefab scale (2.0) to make the small sprite visible
        // User requesting 4.444x larger DEATH SPRITE (which means using original scale)
        // BUT keeping the explosion small (so we pass an override for explosion)
        float spriteScale = transform.localScale.x;
        float smallExplosionScale = spriteScale / 4.444f;
        
        EnemyDeathEffect.PlayDeathEffect(transform.position, spriteRenderer?.sprite, spriteScale, parentRoom, smallExplosionScale);
        
        DropLoot();
        OnEnemyDeath();
        
        if (parentRoom != null)
        {
            Debug.Log($"PuppetMinion: Dying, telling Room {parentRoom.name} I am dead.");
            parentRoom.EnemyDefeated(this);
        }
        else
        {
            Debug.LogError("PuppetMinion: Dying but I have NO PARENT ROOM! I cannot unlock doors.");
        }
        Destroy(gameObject);
    }
    public override void DropLoot()
    {
        // Recursion Prevention:
        // Do NOT drop loot if the coinPrefab is actually a Puppeteer or Minion prefab
        // This stops infinite loops if someone accidentally assigns an enemy as loot
        if (coinPrefab != null)
        {
            if (coinPrefab.GetComponent<PuppetMinion>() != null || coinPrefab.GetComponent<PuppeteerEnemy>() != null)
            {
                Debug.LogError($"PuppetMinion: SAFETY BLOCK! Loot prefab {coinPrefab.name} contains an enemy script. Dropping nothing to prevent crash.");
                return;
            }
        }
        
        base.DropLoot();
    }
}
