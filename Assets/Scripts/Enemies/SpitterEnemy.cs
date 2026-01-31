using UnityEngine;

public class SpitterEnemy : EnemyAI
{
    [Header("Spitter Settings")]
    public float shootRange = 8f;
    public float fleeDistance = 3f;
    public float shootInterval = 2f;
    public float projectileSpeed = 6f;
    public float projectileDamage = 8f;
    
    [Header("Projectile Prefab")]
    public GameObject projectilePrefab;
    
    private float shootTimer;

    protected override void OnEnemyStart()
    {
        maxHealth = 35f;
        currentHealth = maxHealth;
        speed = 3.5f;
        shootTimer = shootInterval;
        
        // Create projectile prefab if not assigned
        if (projectilePrefab == null)
        {
            projectilePrefab = CreateProjectilePrefab();
        }
        
        Debug.Log("SpitterEnemy: Initialized");
    }

    GameObject CreateProjectilePrefab()
    {
        // Create a simple projectile GameObject
        GameObject prefab = new GameObject("SpitterProjectile");
        
        // Add sprite renderer (red circle)
        SpriteRenderer sr = prefab.AddComponent<SpriteRenderer>();
        sr.sprite = CreateCircleSprite();
        sr.color = Color.red;
        prefab.transform.localScale = Vector3.one * 0.3f;
        
        // Add circle collider
        CircleCollider2D collider = prefab.AddComponent<CircleCollider2D>();
        collider.isTrigger = true;
        collider.radius = 0.5f;
        
        // Add rigidbody
        Rigidbody2D rb = prefab.AddComponent<Rigidbody2D>();
        rb.gravityScale = 0;
        rb.isKinematic = true;
        
        // Add projectile script
        prefab.AddComponent<EnemyProjectile>();
        
        return prefab;
    }

    Sprite CreateCircleSprite()
    {
        // Create a simple circle texture
        int size = 32;
        Texture2D texture = new Texture2D(size, size);
        Color[] pixels = new Color[size * size];
        
        Vector2 center = new Vector2(size / 2f, size / 2f);
        float radius = size / 2f;
        
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float distance = Vector2.Distance(new Vector2(x, y), center);
                pixels[y * size + x] = distance <= radius ? Color.white : Color.clear;
            }
        }
        
        texture.SetPixels(pixels);
        texture.Apply();
        
        return Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
    }

    protected override void OnEnemyUpdate()
    {
        if (target == null) return;
        
        float distanceToPlayer = Vector2.Distance(transform.position, target.position);
        
        // Flee if player is too close
        if (distanceToPlayer < fleeDistance)
        {
            FleeFromPlayer();
        }
        else
        {
            
            // Shoot if in range
            if (distanceToPlayer <= shootRange)
            {
                shootTimer -= Time.deltaTime;
                if (shootTimer <= 0)
                {
                    ShootProjectile();
                    shootTimer = shootInterval;
                }
            }
        }
    }

    void FleeFromPlayer()
    {
        // Disable pathfinding and move away from player
        path = null;
        
        Vector2 fleeDirection = (transform.position - target.position).normalized;
        
        // Use MoveSafely to prevent going through walls
        MoveSafely(fleeDirection, speed * Time.deltaTime);
    }

    void ShootProjectile()
    {
        if (projectilePrefab == null) return;
        
        // Calculate direction to player
        Vector2 direction = (target.position - transform.position).normalized;
        
        // Spawn projectile
        GameObject projectileObj = Instantiate(projectilePrefab, transform.position, Quaternion.identity);
        EnemyProjectile projectile = projectileObj.GetComponent<EnemyProjectile>();
        
        if (projectile != null)
        {
            projectile.Initialize(direction, projectileSpeed, projectileDamage);
        }
        
        Debug.Log("SpitterEnemy: Fired projectile!");
    }
}
