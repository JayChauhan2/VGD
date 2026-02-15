using UnityEngine;

public class ShielderEnemy : EnemyAI
{
    [Header("Shielder Settings")]
    public float shieldArc = 180f; // Degrees of protection in front
    public float maxShieldHealth = 100f; // Increased for continuous laser damage (approx 3-4 sec survival)
    [SerializeField] private float damageFlashCooldown = 0.1f;
    
    [Header("Visual References")]
    [SerializeField] private Animator shieldAnimator;
    
    // Shield States
    private enum ShieldState { Shielded, Vulnerable, Broken }
    private ShieldState currentState = ShieldState.Shielded;
    private float currentShieldHealth;
    private float lastFlashTime;
    
    private Vector3 initialScale;

    protected override void OnEnemyStart()
    {
        // REMOVED: maxHealth = 110f; // This prevented Inspector changes
        // REMOVED: currentHealth = maxHealth; // (Handled by base Start)
        
        currentShieldHealth = maxShieldHealth;
        
        // Capture initial scale just in case we need it, but we won't flip it anymore
        initialScale = transform.localScale;
        
        // Ensure we find the components if user forgets to link
        if (shieldAnimator == null)
            shieldAnimator = GetComponentInChildren<Animator>();
            
        UpdateVisuals();
        
        Debug.Log("ShielderEnemy: Initialized");
    }

    protected override void OnEnemyUpdate()
    {
        RotateShieldToPlayer();
    }

    void UpdateVisuals()
    {
        if (shieldAnimator != null)
        {
            // If Broken, disable shield GameObject entirely
            if (currentState == ShieldState.Broken)
            {
                shieldAnimator.gameObject.SetActive(false);
                return;
            }

            shieldAnimator.SetBool("IsVulnerable", currentState == ShieldState.Vulnerable);
        }
    }

    void RotateShieldToPlayer()
    {
        if (target == null)
        {
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            if (player != null) target = player.transform;
            else return;
        }
        
        // 1. Handle Shield Rotation (Aiming)
        Vector2 direction = (target.position - transform.position).normalized;
        float targetAngle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
        
        // The Shield should rotate to face the player fully
        if (shieldAnimator != null && shieldAnimator.gameObject.activeSelf)
        {
             // We set global rotation. 
             // Aligning shield rotation with target angle (0 degree offset).
             shieldAnimator.transform.rotation = Quaternion.Euler(0, 0, targetAngle);
        }
        
        // 2. Handle Body Facing (Discrete Left/Right) using SpriteRenderer.
        // We do NOT modify transform.localScale to avoid messing up the Shield child relative rotation.
        // Sprite is Left-Facing by default.
        
        if (spriteRenderer != null)
        {
            if (target.position.x < transform.position.x - 0.1f)
            {
                // Target is Left. Sprite is Left. No flip needed.
                spriteRenderer.flipX = false;
            }
            else if (target.position.x > transform.position.x + 0.1f)
            {
                // Target is Right. Sprite is Left. Flip needed.
                spriteRenderer.flipX = true;
            }
        }
    }

    // Override UpdateAnimation to prevent EnemyAI from overriding our Facing logic
    protected override void UpdateAnimation(Vector2 velocity)
    {
        if (animator != null)
        {
            animator.SetFloat("Speed", velocity.magnitude);
        }

        if (spriteRenderer != null)
        {
             // Dynamic Depth Sorting (Keep this)
             spriteRenderer.sortingOrder = Mathf.RoundToInt(transform.position.y * -100);
             
             // WE DO NOT FLIP HERE based on velocity. Facing is handled in RotateShieldToPlayer based on Target position.
        }
    }

    public override void TakeDamage(float damage)
    {
        // 1. If Shield Broken, take full body damage
        if (currentState == ShieldState.Broken)
        {
            base.TakeDamage(damage);
            return;
        }

        // 2. If Vulnerable, next hit breaks the shield
        if (currentState == ShieldState.Vulnerable)
        {
             Debug.Log("ShielderEnemy: Shield Hit while Vulnerable! BREAKING SHIELD.");
             currentState = ShieldState.Broken;
             UpdateVisuals();
             return;
        }

        // 3. If Shielded, check angle
        if (target == null)
        {
            base.TakeDamage(damage);
            return;
        }
        
        // Check angle (Optional, implemented for completeness)
        Vector2 directionToPlayer = (target.position - transform.position).normalized;
        Vector2 shieldForward = Vector2.right; // Logic handled by rotation + 180 fix if needed, but here simple checking
        if (shieldAnimator != null) shieldForward = shieldAnimator.transform.right;

        float dotProduct = Vector2.Dot(shieldForward, directionToPlayer);
        // dot > 0 means frontal roughly, but let's assume if it tracks player, it blocks.
        
        // Accumulate Damage
        currentShieldHealth -= damage;
        // Debug.Log($"ShielderEnemy: Shield HP: {currentShieldHealth}");
        
        // Visual Feedback: Flash Vulnerable sprite briefly (with cooldown)
        if (Time.time > lastFlashTime + damageFlashCooldown)
        {
            if (shieldAnimator != null)
            {
                StartCoroutine(FlashVulnerableState());
                lastFlashTime = Time.time;
            }
        }

        if (currentShieldHealth <= 0)
        {
            Debug.Log("ShielderEnemy: Shield health depleted! Becoming Vulnerable.");
            currentState = ShieldState.Vulnerable;
            UpdateVisuals();
        }
    }
    
    private System.Collections.IEnumerator FlashVulnerableState()
    {
        if (shieldAnimator != null)
        {
            shieldAnimator.SetBool("IsVulnerable", true);
            yield return new WaitForSeconds(0.05f); // Very short flash
            if (currentState == ShieldState.Shielded) // Only revert if still shielded
                shieldAnimator.SetBool("IsVulnerable", false);
        }
    }

    void ResetShieldColor()
    {
        // Deprecated by Animator flash
    }

    // Checking for Reflection Logic
    public bool IsReflecting()
    {
        // Only reflect if Shield is UP and not Vulnerable/Broken
        return currentState == ShieldState.Shielded && currentShieldHealth > 0;
    }
}
