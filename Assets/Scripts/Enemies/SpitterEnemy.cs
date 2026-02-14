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
    public float aimDuration = 0.85f; // Matches SpitterShoot animation length
    
    // ... rest of variables ...
    private bool isAiming = false;
    private float aimTimer;
    private float baseSpeed; // Store the movement speed

    // ... OnEnemyStart ...

    // ... CreateProjectilePrefab ...

    // ... CreateCircleSprite ...

    // ... OnEnemyUpdate ...
    
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
        GameObject projectileObj = Instantiate(projectilePrefab, transform.position, Quaternion.identity);
        projectileObj.SetActive(true); // Activate the instance
        
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
        
        Debug.Log("SpitterEnemy: Fired!");
    }
}
