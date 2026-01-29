using UnityEngine;
using System.Collections.Generic;

public class SpawnerEnemy : EnemyAI
{
    [Header("Spawner Settings")]
    public float spawnInterval = 6f;
    public int maxSpawns = 3;
    public float spawnRadius = 1.5f;
    
    private float spawnTimer;
    private List<WandererEnemy> spawnedEnemies = new List<WandererEnemy>();

    protected override void OnEnemyStart()
    {
        maxHealth = 180f;
        currentHealth = maxHealth;
        speed = 0.5f; // Nearly stationary
        
        spawnTimer = spawnInterval;
        
        Debug.Log("SpawnerEnemy: Initialized");
    }

    protected override void OnEnemyUpdate()
    {
        // Clean up destroyed spawns from list
        spawnedEnemies.RemoveAll(enemy => enemy == null);
        
        // Spawn new enemies if under limit
        if (spawnedEnemies.Count < maxSpawns)
        {
            spawnTimer -= Time.deltaTime;
            if (spawnTimer <= 0)
            {
                SpawnEnemy();
                spawnTimer = spawnInterval;
            }
        }
    }

    void SpawnEnemy()
    {
        if (parentRoom == null)
        {
            Debug.LogWarning("SpawnerEnemy: No parent room, cannot spawn");
            return;
        }
        
        // Random position around spawner
        float angle = Random.Range(0f, 360f) * Mathf.Deg2Rad;
        Vector3 offset = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0) * spawnRadius;
        Vector3 spawnPosition = transform.position + offset;
        
        // Create wanderer enemy
        GameObject spawnObj = new GameObject("SpawnedWanderer");
        spawnObj.transform.position = spawnPosition;
        
        // Add sprite renderer
        SpriteRenderer sr = spawnObj.AddComponent<SpriteRenderer>();
        sr.sprite = CreateCircleSprite();
        sr.color = new Color(0.8f, 0.8f, 0.3f); // Yellowish
        spawnObj.transform.localScale = Vector3.one * 0.7f;
        
        // Add collider
        CircleCollider2D collider = spawnObj.AddComponent<CircleCollider2D>();
        collider.radius = 0.5f;
        
        // Add rigidbody
        Rigidbody2D rb = spawnObj.AddComponent<Rigidbody2D>();
        rb.gravityScale = 0;
        rb.constraints = RigidbodyConstraints2D.FreezeRotation;
        
        // Add WandererEnemy script
        WandererEnemy wanderer = spawnObj.AddComponent<WandererEnemy>();
        wanderer.maxHealth = 30f;
        wanderer.speed = 4f;
        
        // Track spawned enemy
        spawnedEnemies.Add(wanderer);
        
        // Register with room
        parentRoom.RegisterEnemy(wanderer);
        
        // Activate if room is already active
        if (IsActive)
        {
            wanderer.SetActive(true);
        }
        
        Debug.Log($"SpawnerEnemy: Spawned enemy ({spawnedEnemies.Count}/{maxSpawns})");
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

    protected override void OnEnemyDeath()
    {
        // Clean up any remaining spawned enemies
        foreach (var enemy in spawnedEnemies)
        {
            if (enemy != null)
            {
                // Optionally: make them weaker or destroy them
                // For now, they continue to exist
            }
        }
    }
}
