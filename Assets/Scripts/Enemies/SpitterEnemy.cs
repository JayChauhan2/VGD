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
    public float aimDuration = 0.5f;
    private bool isAiming = false;
    private float aimTimer;
    private float baseSpeed; // Store the movement speed

    protected override void OnEnemyStart()
    {
        maxHealth = 35f;
        currentHealth = maxHealth;
        speed = 3f;
        baseSpeed = speed; // Initialize baseSpeed
        shootTimer = shootInterval;
        
        // Use a smaller size for the Spitter
        transform.localScale = Vector3.one * 0.5f;

        // Ensure CircleCollider for smooth movement
        BoxCollider2D box = GetComponent<BoxCollider2D>();
        if (box != null) Destroy(box);
        
        PolygonCollider2D poly = GetComponent<PolygonCollider2D>();
        if (poly != null) Destroy(poly);
        
        if (GetComponent<CircleCollider2D>() == null)
        {
            CircleCollider2D circle = gameObject.AddComponent<CircleCollider2D>();
            circle.radius = 0.3f; // Standard size
        }
        
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
        sr.sortingLayerName = "Object"; // Set sorting layer
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
                // Visual feedback? (Maybe flash or something, but movement stop is enough for now)
                ShootProjectile();
                
                // Return to moving
                isAiming = false;
                speed = baseSpeed; // Restore speed
                shootTimer = shootInterval;
            }
        }
        else
        {
            // --- Moving Behavior ---
            
            // Ensure speed is normal (in case we got stuck at 0 somehow)
            // But we don't want to override if something else (like a slow effect) changed it.
            // For now, if it's 0, restore base.
            if (speed == 0) speed = baseSpeed;

            // Check if ready to aim/shoot
            if (distanceToPlayer <= shootRange)
            {
                shootTimer -= Time.deltaTime;
                if (shootTimer <= 0)
                {
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
