using UnityEngine;

public class SplitterEnemy : EnemyAI
{
    [Header("Splitter Settings")]
    public int splitCount = 2;
    public GameObject wandererPrefab;
    public float spawnHealth = 25f;

    protected override void OnEnemyStart()
    {
        maxHealth = 90f;
        currentHealth = maxHealth;
        speed = 3f;
        
        Debug.Log("SplitterEnemy: Initialized");
    }

    protected override void OnEnemyDeath()
    {
        // Spawn smaller enemies on death
        SpawnSplits();
    }

    void SpawnSplits()
    {
        if (parentRoom == null)
        {
            Debug.LogWarning("SplitterEnemy: No parent room, cannot spawn splits");
            return;
        }
        
        for (int i = 0; i < splitCount; i++)
        {
            // Calculate spawn position around this enemy
            float angle = (360f / splitCount) * i;
            float radians = angle * Mathf.Deg2Rad;
            Vector3 offset = new Vector3(Mathf.Cos(radians), Mathf.Sin(radians), 0) * 0.5f;
            Vector3 spawnPosition = transform.position + offset;
            
            // Create wanderer enemy
            GameObject splitObj = new GameObject($"Split_{i}");
            splitObj.transform.position = spawnPosition;
            
            // Add sprite renderer (smaller, different color)
            SpriteRenderer sr = splitObj.AddComponent<SpriteRenderer>();
            sr.sprite = CreateCircleSprite();
            sr.color = new Color(1f, 0.5f, 0.5f); // Light red
            splitObj.transform.localScale = Vector3.one * 0.6f; // Smaller than normal
            
            // Add collider
            CircleCollider2D collider = splitObj.AddComponent<CircleCollider2D>();
            collider.radius = 0.5f;
            
            // Add rigidbody
            Rigidbody2D rb = splitObj.AddComponent<Rigidbody2D>();
            rb.gravityScale = 0;
            rb.constraints = RigidbodyConstraints2D.FreezeRotation;
            
            // Add WandererEnemy script
            WandererEnemy wanderer = splitObj.AddComponent<WandererEnemy>();
            wanderer.maxHealth = spawnHealth;
            wanderer.speed = 5f; // Faster than normal wanderer
            
            // Register with room
            parentRoom.RegisterEnemy(wanderer);
            
            // Activate if room is already active
            if (IsActive)
            {
                wanderer.SetActive(true);
            }
            
            Debug.Log($"SplitterEnemy: Spawned split {i}");
        }
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
}
