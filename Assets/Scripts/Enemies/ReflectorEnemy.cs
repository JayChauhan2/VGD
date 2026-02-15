using UnityEngine;

public class ReflectorEnemy : EnemyAI
{
    [Header("Reflector Settings")]
    public float maxShieldHealth = 100f; // Shield HP
    [SerializeField] private float damageFlashCooldown = 0.1f;
    
    [Header("Visual References")]
    [SerializeField] private Animator shieldAnimator; // Assign the Shield child here
    
    // Shield States
    private enum ShieldState { Shielded, Vulnerable, Broken }
    private ShieldState currentState = ShieldState.Shielded;
    private float currentShieldHealth;
    private float lastFlashTime;
    
    private Vector3 initialScale;

    protected override void OnEnemyStart()
    {
        // Stats are now set in Inspector (MaxHealth from base, MaxShieldHealth here)
        currentShieldHealth = maxShieldHealth;
        
        // Capture initial scale
        initialScale = transform.localScale;
        
        // Ensure we find the components if user forgets to link
        if (shieldAnimator == null)
            shieldAnimator = GetComponentInChildren<Animator>();
            
        UpdateVisuals();
        
        Debug.Log("ReflectorEnemy: Initialized");
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
             // Aligning shield rotation with target angle (0 degree offset)
             shieldAnimator.transform.rotation = Quaternion.Euler(0, 0, targetAngle);
        }
        
        // 2. Handle Body Facing (Discrete Left/Right) using SpriteRenderer.
        if (spriteRenderer != null)
        {
            if (target.position.x < transform.position.x - 0.1f)
            {
                // Target is Left. Sprite is Left (default). No flip.
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
             spriteRenderer.sortingOrder = Mathf.RoundToInt(transform.position.y * -100);
             // Facing handled in RotateShieldToPlayer
        }
    }

    public bool IsReflecting()
    {
        // Reflector specifically reflects when Shielded
        return currentState == ShieldState.Shielded && currentShieldHealth > 0;
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
             Debug.Log("ReflectorEnemy: Shield Hit while Vulnerable! BREAKING SHIELD.");
             currentState = ShieldState.Broken;
             UpdateVisuals();
             return;
        }

        // 3. If Shielded
        if (target == null)
        {
            base.TakeDamage(damage);
            return;
        }
        
        // Accumulate Damage to Shield
        currentShieldHealth -= damage;
        
        // Visual Feedback
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
            Debug.Log("ReflectorEnemy: Shield health depleted! Becoming Vulnerable.");
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
            if (currentState == ShieldState.Shielded) 
                shieldAnimator.SetBool("IsVulnerable", false);
        }
    }
}
