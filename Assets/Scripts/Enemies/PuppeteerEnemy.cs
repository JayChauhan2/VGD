using UnityEngine;
using System.Collections.Generic;

public class PuppeteerEnemy : EnemyAI
{
    [Header("Puppeteer Settings")]
    public int puppetCount = 3;
    public float controlRadius = 8f;
    public float keepDistanceFromPlayer = 6f;
    
    private List<WandererEnemy> puppets = new List<WandererEnemy>();
    private LineRenderer[] tethers;

    protected override void OnEnemyStart()
    {
        maxHealth = 165f;
        currentHealth = maxHealth;
        speed = 2f;
        
        // Spawn puppets
        SpawnPuppets();
        
        Debug.Log("PuppeteerEnemy: Initialized with puppets");
    }

    void SpawnPuppets()
    {
        if (parentRoom == null)
        {
            Debug.LogWarning("PuppeteerEnemy: No parent room, cannot spawn puppets");
            return;
        }
        
        tethers = new LineRenderer[puppetCount];
        
        for (int i = 0; i < puppetCount; i++)
        {
            // Calculate spawn position around puppeteer
            float angle = (360f / puppetCount) * i;
            float radians = angle * Mathf.Deg2Rad;
            Vector3 offset = new Vector3(Mathf.Cos(radians), Mathf.Sin(radians), 0) * 2f;
            Vector3 spawnPosition = transform.position + offset;
            
            // Create puppet
            GameObject puppetObj = new GameObject($"Puppet_{i}");
            puppetObj.transform.position = spawnPosition;
            
            // Add sprite renderer
            SpriteRenderer sr = puppetObj.AddComponent<SpriteRenderer>();
            sr.sprite = CreateCircleSprite();
            sr.color = new Color(0.6f, 0.4f, 0.8f); // Purple-ish
            puppetObj.transform.localScale = Vector3.one * 0.8f;
            
            // Add collider
            CircleCollider2D collider = puppetObj.AddComponent<CircleCollider2D>();
            collider.radius = 0.5f;
            
            // Add rigidbody
            Rigidbody2D rb = puppetObj.AddComponent<Rigidbody2D>();
            rb.gravityScale = 0;
            rb.constraints = RigidbodyConstraints2D.FreezeRotation;
            
            // Add WandererEnemy script
            WandererEnemy puppet = puppetObj.AddComponent<WandererEnemy>();
            puppet.maxHealth = 40f;
            puppet.speed = 4.5f;
            
            puppets.Add(puppet);
            
            // Register with room
            parentRoom.RegisterEnemy(puppet);
            
            // Create tether visual
            CreateTether(i, puppetObj);
            
            Debug.Log($"PuppeteerEnemy: Spawned puppet {i}");
        }
    }

    void CreateTether(int index, GameObject puppet)
    {
        // Create line renderer for visual tether
        GameObject tetherObj = new GameObject($"Tether_{index}");
        tetherObj.transform.SetParent(transform);
        
        LineRenderer lr = tetherObj.AddComponent<LineRenderer>();
        lr.startWidth = 0.05f;
        lr.endWidth = 0.05f;
        lr.positionCount = 2;
        lr.startColor = new Color(0.6f, 0.4f, 0.8f, 0.5f);
        lr.endColor = new Color(0.6f, 0.4f, 0.8f, 0.2f);
        lr.sortingOrder = -1;
        
        tethers[index] = lr;
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
        // Clean up destroyed puppets
        puppets.RemoveAll(p => p == null);
        
        // Keep distance from player (flee behavior)
        if (target != null)
        {
            float distanceToPlayer = Vector2.Distance(transform.position, target.position);
            
            if (distanceToPlayer < keepDistanceFromPlayer)
            {
                // Flee from player using safe movement
                path = null;
                Vector2 fleeDirection = (transform.position - target.position).normalized;
                MoveSafely(fleeDirection, speed * Time.deltaTime);
            }
        }
        
        // Update tether visuals
        UpdateTethers();
        
        // Activate puppets if this enemy is active
        if (IsActive)
        {
            foreach (var puppet in puppets)
            {
                if (puppet != null && !puppet.IsActive)
                {
                    puppet.SetActive(true);
                }
            }
        }
    }

    void UpdateTethers()
    {
        for (int i = 0; i < puppets.Count && i < tethers.Length; i++)
        {
            if (puppets[i] != null && tethers[i] != null)
            {
                tethers[i].SetPosition(0, transform.position);
                tethers[i].SetPosition(1, puppets[i].transform.position);
            }
        }
    }

    protected override void OnEnemyDeath()
    {
        // When puppeteer dies, weaken/confuse puppets
        foreach (var puppet in puppets)
        {
            if (puppet != null)
            {
                puppet.speed *= 0.5f; // Slow them down
                
                // Change color to indicate confusion
                SpriteRenderer sr = puppet.GetComponent<SpriteRenderer>();
                if (sr != null)
                {
                    sr.color = new Color(0.5f, 0.5f, 0.5f); // Gray
                }
            }
        }
        
        Debug.Log("PuppeteerEnemy: Died, puppets are now confused!");
    }
}
