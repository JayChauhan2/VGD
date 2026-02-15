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
        rb = GetComponent<Rigidbody2D>(); // Ensure RB reference is cached
        
        // Hide health bar initially
        if (healthBar != null)
        {
            healthBar.SetVisibility(false);
        }
        
        // Make Mimic immovable and pass-through while disguised
        if (rb != null)
        {
            rb.bodyType = RigidbodyType2D.Kinematic;
            rb.linearVelocity = Vector2.zero;
        }
        Collider2D col = GetComponent<Collider2D>();
        if (col != null)
        {
            col.isTrigger = true;
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
        
        // Re-enable physics (Dynamic + Solid)
        if (rb != null)
        {
            rb.bodyType = RigidbodyType2D.Dynamic;
        }
        Collider2D col = GetComponent<Collider2D>();
        if (col != null)
        {
            col.isTrigger = false;
        }
        
        // Show health bar
        if (healthBar != null)
        {
            healthBar.SetVisibility(true);
        }
        
        // Animate Scale: 0.3 -> 0.5
        StartCoroutine(AnimateScale(new Vector3(0.5f, 0.5f, 0.5f), 0.5f));
        
        Debug.Log("MimicEnemy: REVEALED! Attacking player!");
    }

    System.Collections.IEnumerator AnimateScale(Vector3 targetScale, float duration)
    {
        Vector3 startScale = transform.localScale;
        float elapsed = 0f;
        
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;
            // Ease out elastic or simple smoothstep
            t = Mathf.SmoothStep(0, 1, t); 
            
            transform.localScale = Vector3.Lerp(startScale, targetScale, t);
            yield return null;
        }
        transform.localScale = targetScale;
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
    protected override void UpdateAnimation(Vector2 velocity)
    {
        if (animator != null)
        {
            animator.SetFloat("Speed", velocity.magnitude);
        }

        if (spriteRenderer != null)
        {
             // Dynamic Depth Sorting
             // Multiply Y by -100 to give lower objects higher order (draw on top)
             spriteRenderer.sortingOrder = Mathf.RoundToInt(transform.position.y * -100);
             
             // Intentionally Removed flipX logic so it doesn't face movement direction
        }
    }
}
