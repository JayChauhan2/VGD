using UnityEngine;

public class WandererEnemy : EnemyAI
{
    // Wanderer uses the base EnemyAI behavior (simple chase)
    // Just configure different stats
    
    // Confusion state for Puppeteer minions
    private bool isConfused = false;
    private Vector3? confusionTarget;
    private float confusionTimer;

    protected override void OnEnemyStart()
    {
        // Set Wanderer-specific stats
        maxHealth = 40f;
        currentHealth = maxHealth;
        // speed = 4.5f; // Removed to allow Inspector value
        
        Debug.Log("WandererEnemy: Initialized with low health, moderate speed");
    }

    public void SetConfusion(bool state)
    {
        isConfused = state;
        if (isConfused)
        {
            // Pick initial random target
            PickRandomConfusionTarget();
            
            // Visual feedback (optional color change)
            if (spriteRenderer != null) spriteRenderer.color = Color.gray;
        }
        else
        {
            // Reset
            confusionTarget = null;
            if (spriteRenderer != null) spriteRenderer.color = Color.white;
        }
    }

    protected override void OnEnemyUpdate()
    {
        if (isConfused)
        {
            // Override base behavior: Ignore player, move randomly
            HandleConfusionBehavior();
        }
        // If not confused, base EnemyAI.Update will handle standard pathfinding to target (Player)
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
}
