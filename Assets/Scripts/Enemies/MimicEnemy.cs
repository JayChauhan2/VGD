using UnityEngine;

public class MimicEnemy : EnemyAI
{
    [Header("Mimic Settings")]
    public float revealDistance = 2.5f;
    public float attackSpeed = 7f;
    
    private bool isRevealed = false;
    private SpriteRenderer spriteRenderer;
    private EnemyHealthBar healthBar;

    protected override void OnEnemyStart()
    {
        maxHealth = 70f;
        currentHealth = maxHealth;
        speed = 0f; // Doesn't move until revealed
        
        spriteRenderer = GetComponent<SpriteRenderer>();
        healthBar = GetComponent<EnemyHealthBar>();
        
        // Hide health bar initially
        if (healthBar != null)
        {
            healthBar.SetVisibility(false);
        }
        
        // Setup initial disguise from coin prefab
        if (coinPrefab != null)
        {
            SpriteRenderer coinRenderer = coinPrefab.GetComponent<SpriteRenderer>();
            if (coinRenderer != null && spriteRenderer != null)
            {
                spriteRenderer.sprite = coinRenderer.sprite;
                spriteRenderer.color = coinRenderer.color;
            }
        }
        else
        {
            // Fallback if coinPrefab not assigned
             if (RoomManager.Instance != null && RoomManager.Instance.coinPrefab != null)
             {
                 SpriteRenderer coinRenderer = RoomManager.Instance.coinPrefab.GetComponent<SpriteRenderer>();
                 if (coinRenderer != null && spriteRenderer != null)
                 {
                     spriteRenderer.sprite = coinRenderer.sprite;
                     spriteRenderer.color = coinRenderer.color;
                 }
             }
        }
        
        Debug.Log("MimicEnemy: Initialized in disguise");
    }

    protected override void OnEnemyUpdate()
    {
        if (target == null) return;
        
        if (!isRevealed)
        {
            // Check distance to player
            float distanceToPlayer = Vector2.Distance(transform.position, target.position);
            
            if (distanceToPlayer <= revealDistance)
            {
                Reveal();
            }
        }
        else
        {
            // Chase player aggressively after reveal
            speed = attackSpeed;
        }
    }

    void Reveal()
    {
        isRevealed = true;
        
       if (animator != null)
       {
           animator.SetTrigger("Reveal");
       }

        if (spriteRenderer != null)
        {
            spriteRenderer.color = Color.white; // Reset color tint
        }
        
        // Show health bar
        if (healthBar != null)
        {
            healthBar.SetVisibility(true);
        }
        
        Debug.Log("MimicEnemy: REVEALED! Attacking player!");
    }

    public override void TakeDamage(float damage)
    {
        // Taking damage also reveals the mimic
        if (!isRevealed)
        {
            Reveal();
        }
        
        base.TakeDamage(damage);
    }
}
