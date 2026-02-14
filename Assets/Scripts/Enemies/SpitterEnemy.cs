using UnityEngine;

public class SpitterEnemy : EnemyAI
{
    [Header("Spitter Settings")]
    public float shootRange = 8f;
    public float shootInterval = 2f;
    public float projectileSpeed = 6f;
    public float projectileDamage = 8f;
    
    [Header("Projectile Prefab")]
    public GameObject projectilePrefab;
    
    private float shootTimer;

    [Header("Aiming Settings")]
    public float aimDuration = 1.1f; // Slightly longer than animation (0.85s) to ensure it finishes
    
    // ... rest of variables ...
    private bool isAiming = false;
    private float aimTimer;
    private float baseSpeed; // Store the movement speed

    // ... OnEnemyStart ...

    // ... CreateProjectilePrefab ...

    // ... CreateCircleSprite ...

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
            
            // Check if we are within stopping distance
            // We want to stop a bit closer than max range so we don't jitter at the edge
            float stopDistance = shootRange - 2f; 

            if (distanceToPlayer <= stopDistance)
            {
               // Stop moving if close enough
               speed = 0f;
            }
            else
            {
               // Move if too far
               speed = baseSpeed > 0 ? baseSpeed : 4f; // Fallback to 4 if baseSpeed is lost
            }

            // Check if ready to aim/shoot
            if (distanceToPlayer <= shootRange)
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
    
    void StartAiming()
    {
        isAiming = true;
        aimTimer = aimDuration;
        
        // Capture current speed if it's valid, otherwise keep existing baseSpeed
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
