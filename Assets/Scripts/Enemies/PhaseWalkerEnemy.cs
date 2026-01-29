using UnityEngine;

public class PhaseWalkerEnemy : EnemyAI
{
    [Header("Phase Walker Settings")]
    public float solidDuration = 4f;
    public float intangibleDuration = 3f;
    
    private enum PhaseState { Solid, Intangible }
    private PhaseState currentPhase = PhaseState.Solid;
    
    private float phaseTimer;
    private SpriteRenderer spriteRenderer;
    private Collider2D enemyCollider;

    protected override void OnEnemyStart()
    {
        maxHealth = 115f;
        currentHealth = maxHealth;
        speed = 5.5f;
        
        spriteRenderer = GetComponent<SpriteRenderer>();
        enemyCollider = GetComponent<Collider2D>();
        
        phaseTimer = solidDuration;
        UpdatePhaseVisuals();
        
        Debug.Log("PhaseWalkerEnemy: Initialized in solid phase");
    }

    protected override void OnEnemyUpdate()
    {
        // Update phase timer
        phaseTimer -= Time.deltaTime;
        
        if (phaseTimer <= 0)
        {
            // Switch phases
            if (currentPhase == PhaseState.Solid)
            {
                EnterIntangiblePhase();
            }
            else
            {
                EnterSolidPhase();
            }
        }
        
        // When intangible, can move through walls
        if (currentPhase == PhaseState.Intangible)
        {
            // Disable collision with walls (simplified - just ignore pathfinding)
            path = null;
            
            // Move directly toward player
            if (target != null)
            {
                Vector2 direction = (target.position - transform.position).normalized;
                transform.position += (Vector3)direction * speed * Time.deltaTime;
            }
        }
    }

    void EnterSolidPhase()
    {
        currentPhase = PhaseState.Solid;
        phaseTimer = solidDuration;
        
        // Enable collision
        if (enemyCollider != null)
        {
            enemyCollider.enabled = true;
        }
        
        UpdatePhaseVisuals();
        Debug.Log("PhaseWalkerEnemy: Entered SOLID phase");
    }

    void EnterIntangiblePhase()
    {
        currentPhase = PhaseState.Intangible;
        phaseTimer = intangibleDuration;
        
        // Disable collision (except with player for damage)
        if (enemyCollider != null)
        {
            // Keep collision enabled but we'll handle damage blocking separately
            enemyCollider.enabled = true;
        }
        
        UpdatePhaseVisuals();
        Debug.Log("PhaseWalkerEnemy: Entered INTANGIBLE phase");
    }

    void UpdatePhaseVisuals()
    {
        if (spriteRenderer != null)
        {
            if (currentPhase == PhaseState.Solid)
            {
                // Fully opaque, normal color
                Color color = spriteRenderer.color;
                color.a = 1f;
                spriteRenderer.color = color;
            }
            else
            {
                // Semi-transparent, ghostly
                Color color = spriteRenderer.color;
                color.a = 0.3f;
                spriteRenderer.color = color;
            }
        }
    }

    public override void TakeDamage(float damage)
    {
        // Only take damage when solid
        if (currentPhase == PhaseState.Solid)
        {
            base.TakeDamage(damage);
        }
        else
        {
            Debug.Log("PhaseWalkerEnemy: Intangible - damage ignored!");
        }
    }

    void OnCollisionEnter2D(Collision2D collision)
    {
        // When intangible, don't collide with walls
        if (currentPhase == PhaseState.Intangible)
        {
            if (collision.gameObject.CompareTag("Obstacle") || 
                collision.gameObject.layer == LayerMask.NameToLayer("Obstacle"))
            {
                // Ignore wall collision
                Physics2D.IgnoreCollision(enemyCollider, collision.collider, true);
            }
        }
    }

    void OnCollisionExit2D(Collision2D collision)
    {
        // Re-enable collision when solid
        if (currentPhase == PhaseState.Solid)
        {
            if (collision.gameObject.CompareTag("Obstacle") || 
                collision.gameObject.layer == LayerMask.NameToLayer("Obstacle"))
            {
                Physics2D.IgnoreCollision(enemyCollider, collision.collider, false);
            }
        }
    }
}
