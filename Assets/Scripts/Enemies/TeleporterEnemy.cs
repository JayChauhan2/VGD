using UnityEngine;
using System.Collections;

public class TeleporterEnemy : EnemyAI
{
    [Header("Teleporter Settings")]
    public float teleportDistance = 4f;
    public float teleportCooldown = 3f;
    public float shootInterval = 2.5f;
    public float projectileSpeed = 7f;
    public float projectileDamage = 10f;
    public float invulnerabilityDuration = 0.3f;
    
    [Header("Projectile Prefab")]
    public GameObject projectilePrefab;
    
    private float teleportTimer;
    private float shootTimer;
    private bool isInvulnerable;
    private SpriteRenderer spriteRenderer;

    protected override void OnEnemyStart()
    {
        maxHealth = 80f;
        currentHealth = maxHealth;
        speed = 0f; // Doesn't move, only teleports
        
        teleportTimer = teleportCooldown;
        shootTimer = shootInterval;
        
        spriteRenderer = GetComponent<SpriteRenderer>();
        
        // Create projectile prefab if not assigned
        if (projectilePrefab == null)
        {
            projectilePrefab = CreateProjectilePrefab();
        }
        
        Debug.Log("TeleporterEnemy: Initialized");
    }

    GameObject CreateProjectilePrefab()
    {
        GameObject prefab = new GameObject("TeleporterProjectile");
        
        SpriteRenderer sr = prefab.AddComponent<SpriteRenderer>();
        sr.sprite = CreateCircleSprite();
        sr.color = new Color(0.5f, 0f, 1f); // Purple
        prefab.transform.localScale = Vector3.one * 0.35f;
        
        CircleCollider2D collider = prefab.AddComponent<CircleCollider2D>();
        collider.isTrigger = true;
        collider.radius = 0.5f;
        
        Rigidbody2D rb = prefab.AddComponent<Rigidbody2D>();
        rb.gravityScale = 0;
        rb.isKinematic = true;
        
        prefab.AddComponent<EnemyProjectile>();
        
        return prefab;
    }

    Sprite CreateCircleSprite()
    {
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
        
        // Disable pathfinding (teleporter doesn't walk)
        path = null;
        
        float distanceToPlayer = Vector2.Distance(transform.position, target.position);
        
        // Teleport if player gets too close
        if (distanceToPlayer < teleportDistance)
        {
            teleportTimer -= Time.deltaTime;
            if (teleportTimer <= 0)
            {
                Teleport();
                teleportTimer = teleportCooldown;
            }
        }
        
        // Shoot projectiles
        shootTimer -= Time.deltaTime;
        if (shootTimer <= 0 && !isInvulnerable)
        {
            ShootProjectile();
            shootTimer = shootInterval;
        }
    }

    void Teleport()
    {
        Bounds roomBounds = GetRoomBounds();
        
        // Find random position in room
        Vector3 newPosition = Vector3.zero;
        int attempts = 0;
        bool validPosition = false;
        
        while (!validPosition && attempts < 10)
        {
            float randomX = Random.Range(roomBounds.min.x + 1f, roomBounds.max.x - 1f);
            float randomY = Random.Range(roomBounds.min.y + 1f, roomBounds.max.y - 1f);
            newPosition = new Vector3(randomX, randomY, transform.position.z);
            
            // Check if far enough from player AND valid position (not in wall)
            if (Vector2.Distance(newPosition, target.position) > teleportDistance && IsPositionValid(newPosition))
            {
                validPosition = true;
            }
            
            attempts++;
        }
        
        if (validPosition)
        {
            transform.position = newPosition;
            StartCoroutine(InvulnerabilityPeriod());
            Debug.Log("TeleporterEnemy: Teleported to new position!");
        }
        else
        {
            Debug.LogWarning("TeleporterEnemy: Failed to find valid teleport position.");
        }
    }

    IEnumerator InvulnerabilityPeriod()
    {
        isInvulnerable = true;
        
        // Visual feedback - flash sprite
        if (spriteRenderer != null)
        {
            Color originalColor = spriteRenderer.color;
            spriteRenderer.color = new Color(originalColor.r, originalColor.g, originalColor.b, 0.5f);
            
            yield return new WaitForSeconds(invulnerabilityDuration);
            
            spriteRenderer.color = originalColor;
        }
        else
        {
            yield return new WaitForSeconds(invulnerabilityDuration);
        }
        
        isInvulnerable = false;
    }

    public override void TakeDamage(float damage)
    {
        if (isInvulnerable) return;
        base.TakeDamage(damage);
    }

    void ShootProjectile()
    {
        if (projectilePrefab == null || target == null) return;
        
        Vector2 direction = (target.position - transform.position).normalized;
        
        GameObject projectileObj = Instantiate(projectilePrefab, transform.position, Quaternion.identity);
        EnemyProjectile projectile = projectileObj.GetComponent<EnemyProjectile>();
        
        if (projectile != null)
        {
            projectile.Initialize(direction, projectileSpeed, projectileDamage);
        }
    }
}
