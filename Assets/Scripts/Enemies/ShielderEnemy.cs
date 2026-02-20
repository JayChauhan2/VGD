using UnityEngine;

public class ShielderEnemy : EnemyAI
{
    [Header("Shielder Settings")]
    public float shieldArc = 180f; // Degrees of protection in front
    public float maxShieldHealth = 100f; // Increased for continuous laser damage (approx 3-4 sec survival)
    public float shieldRotationSpeed = 100f; // Degrees per second. Lower = easier to flank.
    [SerializeField] protected float damageFlashCooldown = 0.1f;
    
    [Header("Visual References")]
    [SerializeField] protected Animator shieldAnimator; // Protected for child access
    public Transform shieldTransform; // Reference to the actual Shield GameObject to rotate

    // Shield States
    public enum ShieldState { Shielded, Vulnerable, Broken } // Public for child access
    protected ShieldState currentState = ShieldState.Shielded; // Protected
    protected float currentShieldHealth;
    protected float lastFlashTime;
    
    protected Vector3 initialScale;

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
            
        // Try to find Shield Transform if not assigned
        if (shieldTransform == null)
        {
            // First check if shieldAnimator is on a child, that might be it
            if (shieldAnimator != null && shieldAnimator.transform != transform)
            {
                shieldTransform = shieldAnimator.transform;
            }
            else
            {
                // Look for a child named "Shield"
                Transform shieldChild = transform.Find("Shield");
                if (shieldChild != null) shieldTransform = shieldChild;
            }
        }
            
        UpdateVisuals();
        
        Debug.Log($"{GetType().Name}: Initialized. Shield Transform: {(shieldTransform != null ? shieldTransform.name : "NULL")}");
    }

    protected override void OnEnemyUpdate()
    {
        RotateShieldToPlayer();
    }

    protected virtual void UpdateVisuals()
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
        
        // Also toggle the shieldTransform if it's different from animator
        if (shieldTransform != null && currentState == ShieldState.Broken)
        {
             shieldTransform.gameObject.SetActive(false);
        }
    }

    protected virtual void RotateShieldToPlayer()
    {
        if (target == null)
        {
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            if (player != null) target = player.transform;
            else return;
        }
        
        // 1. Handle Shield Rotation (Aiming)
        // We only rotate the Shield Transform, NOT the main body.
        
        if (shieldTransform != null && shieldTransform.gameObject.activeSelf)
        {
             Vector2 direction = (target.position - transform.position).normalized;
             float targetAngle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;

             // Smooth Rotation towards target
             float currentAngle = shieldTransform.rotation.eulerAngles.z;
             float nextAngle = Mathf.MoveTowardsAngle(currentAngle, targetAngle, shieldRotationSpeed * Time.deltaTime);
             
             shieldTransform.rotation = Quaternion.Euler(0, 0, nextAngle);
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
             Debug.Log($"{GetType().Name}: Shield Hit while Vulnerable! BREAKING SHIELD.");
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
        // Check angle to determine if hit is on Shield or Body
        Vector2 directionToPlayer = (target.position - transform.position).normalized;
        Vector2 shieldForward = Vector2.right; 
        
        if (shieldTransform != null) shieldForward = shieldTransform.right; 
        else if (shieldAnimator != null) shieldForward = shieldAnimator.transform.right;

        // Calculate angle between Shield Facing and Player Direction
        float angle = Vector2.Angle(shieldForward, directionToPlayer);

        // If the angle is OUTSIDE half the arc, the player has flanked the shield
        if (angle > shieldArc / 2f)
        {
            // Debug.Log($"{GetType().Name}: Flanked! Angle: {angle} > {shieldArc/2f}. Taking Body Damage.");
            base.TakeDamage(damage);
            return;
        }
        
        // Otherwise, it hits the shield (Frontal Attack)
        
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
            Debug.Log($"{GetType().Name}: Shield health depleted! Becoming Vulnerable.");
            currentState = ShieldState.Vulnerable;
            UpdateVisuals();
        }
    }
    
    protected System.Collections.IEnumerator FlashVulnerableState()
    {
        if (shieldAnimator != null)
        {
            shieldAnimator.SetBool("IsVulnerable", true);
            yield return new WaitForSeconds(0.05f); // Very short flash
            if (currentState == ShieldState.Shielded) // Only revert if still shielded
                shieldAnimator.SetBool("IsVulnerable", false);
        }
    }

    // Checking for Reflection Logic - Helper
    // Base Shielder DOES NOT reflect (return false), or we can return true but Laser ignores it.
    // To implement "Shielder doesn't reflect", we should return false here, 
    // AND let Reflector override it to return true.
    // BUT, user's previous code had IsReflecting return true here.
    // For proper OOP, Shielder shouldn't reflect. 
    // UPDATE: Now takes incoming direction to check angle (for Reflector)
    public virtual bool IsReflecting(Vector2 incomingDir)
    {
        return false; // Default: No reflection
    }

    public GameObject GetShieldGameObject()
    {
        return shieldTransform != null ? shieldTransform.gameObject : (shieldAnimator != null ? shieldAnimator.gameObject : null);
    }
}
