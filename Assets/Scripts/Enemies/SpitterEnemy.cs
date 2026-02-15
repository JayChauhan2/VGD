using UnityEngine;

public class SpitterEnemy : EnemyAI
{
    [Header("Spitter Settings")]
    public float shootRange = 5f; // Shortened range
    public float shootInterval = 2f;
    public float projectileSpeed = 6f;
    public float projectileDamage = 8f;
    public float contactDamage = 5f; // Damage on touch

    [Header("Projectile Prefab")]
    public GameObject projectilePrefab;
    
    private float shootTimer;

    [Header("Aiming Settings")]
    public float aimDuration = 1.1f; // Slightly longer than animation (0.85s) to ensure it finishes
    
    private bool isAiming = false;
    private float aimTimer;
    private float baseSpeed; // Store the movement speed

    protected override void OnEnemyStart()
    {
        maxHealth = 40f; // Default if not set
        currentHealth = maxHealth;
        
        // Capture the speed set in Inspector
        baseSpeed = speed;
        
        Debug.Log($"SpitterEnemy: Initialized with speed {speed}");
    }

    protected override void OnEnemyUpdate()
    {
        if (target == null) return;
        
        float distanceToPlayer = Vector2.Distance(transform.position, target.position);
        
        if (isAiming)
        {
            // --- Aiming Behavior ---
            
            // 1. Ensure we stay stopped
            speed = 0f; 
            
            // 2. Face the player explicitly while standing still
            Vector2 direction = (target.position - transform.position).normalized;
            if (spriteRenderer != null)
            {
                if (direction.x < -0.01f) spriteRenderer.flipX = true;
                else if (direction.x > 0.01f) spriteRenderer.flipX = false;
            }

            // 3. Count down aim timer
            aimTimer -= Time.deltaTime;
            
            if (aimTimer <= 0)
            {
                ShootProjectile();
                
                // Return to moving
                isAiming = false;
                speed = baseSpeed; // Restore speed
                shootTimer = shootInterval;
                Debug.Log("SpitterEnemy: Fired and Returning to Moving State");
            }
        }
        else
        {
            // --- Moving Behavior ---
            
            // Check Line of Sight (LOS)
            bool hasLineOfSight = CheckLineOfSight();

            // Check if we are within stopping distance AND have LOS
            // We want to stop a bit closer than max range so we don't jitter at the edge
            float stopDistance = shootRange - 1f; 

            if (hasLineOfSight && distanceToPlayer <= stopDistance)
            {
               // Stop moving if close enough AND we can see the player
               speed = 0f;
            }
            else
            {
               // Move if too far OR if we can't see the player (need to reposition)
               speed = baseSpeed; 
            }

            // Check if ready to aim/shoot
            // Only aim if we have LOS
            if (hasLineOfSight && distanceToPlayer <= shootRange)
            {
                shootTimer -= Time.deltaTime;
                
                if (shootTimer <= 0)
                {
                    Debug.Log("SpitterEnemy: Shoot Timer Reached 0. Starting Aim.");
                    StartAiming();
                }
            }
        }
    }    
    
    // Check if there are obstacles between us and the player
    bool CheckLineOfSight()
    {
        if (target == null) return false;

        Vector2 direction = (target.position - transform.position).normalized;
        float distance = Vector2.Distance(transform.position, target.position);

        // Mask for Obstacles (usually "Default" or "Obstacle" layer)
        // Adjust layer mask as needed. Here we check Default and Obstacle.
        int layerMask = LayerMask.GetMask("Default", "Obstacle"); 
        
        // RaycastAll to ignore ourselves
        RaycastHit2D[] hits = Physics2D.RaycastAll(transform.position, direction, distance, layerMask);

        foreach (var hit in hits)
        {
            if (hit.collider.transform == transform) continue; // Ignore our own collider
            if (hit.collider.isTrigger) continue; // Ignore triggers

            if (hit.collider.transform == target) return true; // We see the player!
            
            // If we hit something else (that isn't us, isn't a trigger, and isn't the player)
            // It must be a wall or obstacle.
            return false; 
        }

        // If we found nothing passing the filters, assume clear (or player is out of range/layer)
        // Check distance again to be sure? Actually if we didn't hit player in the loop, we probably didn't see them.
        // But RaycastAll order is not guaranteed to be sorted by distance!
        // We MUST sort by distance or check closest.
        
        // Revised Approach: RaycastNonAlloc or just simple Raycast with offset.
        // Offset is easiest and cheapest.
        
        // Start ray slightly outside our own collider (approx radius 0.5)
        Vector2 startPos = (Vector2)transform.position + (direction * 0.6f);
        float remainingDist = distance - 0.6f;
        
        if (remainingDist <= 0) return true; // Already there?

        RaycastHit2D offsetHit = Physics2D.Raycast(startPos, direction, remainingDist, layerMask);
        
        if (offsetHit.collider != null)
        {
             if (offsetHit.collider.transform == target) return true;
             return false;
        }
        
        // If we hit nothing starting from outside our body, and the player is within 'distance',
        // it means the path is clear.
        return true;

        // If we hit nothing, it means clear path (or player is on a layer we didn't check, assuming clear)
        return true;
    }

    void OnCollisionEnter2D(Collision2D collision)
    {
        // Deal contact damage to player
        if (collision.gameObject.CompareTag("Player"))
        {
            PlayerHealth playerHealth = collision.gameObject.GetComponent<PlayerHealth>();
            if (playerHealth != null)
            {
                Vector2 knockbackDir = (collision.transform.position - transform.position).normalized;
                playerHealth.TakeDamage(contactDamage, knockbackDir);
            }
        }
    }

    // ... (StartAiming and ShootProjectile remain)    
    
    void StartAiming()
    {
        isAiming = true;
        aimTimer = aimDuration;
        
        // Capture current speed if it's valid, otherwise keep existing baseSpeed
        // baseSpeed is now set in OnEnemyStart, so we don't need to capture it here every time 
        // unless we want to support dynamic speed changes (e.g. slows) persisting. 
        // For now, let's trust OnEnemyStart or previous state.
        if (speed > 0.1f) baseSpeed = speed;
        
        speed = 0f; // Stop moving
        
        // Trigger Animation
        if (animator != null)
        {
            animator.SetTrigger("Shoot");
        }
        
        Debug.Log("SpitterEnemy: Stopping to aim...");
    }

    void ShootProjectile()
    {
        if (projectilePrefab == null) return;
        
        // Calculate direction to player
        Vector2 direction = (target.position - transform.position).normalized;
        
        // Spawn projectile from hidden template
        // Force Z to 0 to ensure it's on the 2D plane
        Vector3 spawnPos = new Vector3(transform.position.x, transform.position.y, 0f);
        GameObject projectileObj = Instantiate(projectilePrefab, spawnPos, Quaternion.identity);
        projectileObj.SetActive(true); // Activate the instance
        
        // Ensure it's on the right sorting layer (redundant but safe)
        SpriteRenderer sr = projectileObj.GetComponent<SpriteRenderer>();
        if (sr != null)
        {
            sr.sortingLayerName = "Object";
            sr.sortingOrder = 10;
        }

        EnemyProjectile projectile = projectileObj.GetComponent<EnemyProjectile>();
        
        // Ignore collision with self
        Collider2D myCollider = GetComponent<Collider2D>();
        Collider2D projCollider = projectileObj.GetComponent<Collider2D>();
        if (myCollider != null && projCollider != null)
        {
            Physics2D.IgnoreCollision(myCollider, projCollider);
        }

        if (projectile != null)
        {
            projectile.Initialize(direction, projectileSpeed, projectileDamage);
        }
        
        Debug.Log($"SpitterEnemy: Fired! Pos: {projectileObj.transform.position}, Dir: {direction}");
        Debug.DrawRay(transform.position, direction * 5f, Color.red, 1f);
    }
}
