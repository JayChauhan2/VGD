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

    protected override void OnEnemyStart()
    {
        maxHealth = 35f;
        currentHealth = maxHealth;
        speed = 6f;
        shootTimer = shootInterval;
        
        // Use a smaller size for the Spitter
        transform.localScale = Vector3.one * 0.7f;
        
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
        sr.sortingOrder = 10; // Ensure it renders on top
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
        
        // Hide the template
        prefab.SetActive(false);
        
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
        
        // Maintain standard AI movement (chase behavior from base class) happens in base.Update() which calls this.
        // We just need to handle shooting here.
        
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
        
        Debug.Log("SpitterEnemy: Fired projectile!");
    }
}
