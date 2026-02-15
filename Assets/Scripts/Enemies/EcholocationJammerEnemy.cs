using UnityEngine;

public class EcholocationJammerEnemy : EnemyAI
{
    [Header("Jammer Settings")]
    public float jammerRadius = 10f;
    public float interferenceStrength = 0.5f;
    
    private bool isJamming = false;
    private GameObject jammerVisual;

    protected override void OnEnemyStart()
    {
        maxHealth = 90f;
        currentHealth = maxHealth;
        // speed = 3f; // Removed to allow Inspector value
        
        CreateJammerVisual();
        RegisterWithEcholocation();
        
        Debug.Log("EcholocationJammerEnemy: Initialized - jamming echolocation");
    }

    void CreateJammerVisual()
    {
        // Create pulsing visual effect
        jammerVisual = new GameObject("JammerEffect");
        jammerVisual.transform.SetParent(transform);
        jammerVisual.transform.localPosition = Vector3.zero;
        jammerVisual.transform.localScale = Vector3.one * 2f;
        
        SpriteRenderer sr = jammerVisual.AddComponent<SpriteRenderer>();
        sr.sprite = CreateRingSprite();
        sr.color = new Color(1f, 0f, 0f, 0.3f); // Red, semi-transparent
        sr.sortingOrder = -1;
    }

    Sprite CreateRingSprite()
    {
        int size = 64;
        Texture2D texture = new Texture2D(size, size);
        Color[] pixels = new Color[size * size];
        
        Vector2 center = new Vector2(size / 2f, size / 2f);
        float outerRadius = size / 2f;
        float innerRadius = size / 2f - 8f;
        
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float distance = Vector2.Distance(new Vector2(x, y), center);
                if (distance <= outerRadius && distance >= innerRadius)
                {
                    pixels[y * size + x] = Color.white;
                }
                else
                {
                    pixels[y * size + x] = Color.clear;
                }
            }
        }
        
        texture.SetPixels(pixels);
        texture.Apply();
        
        return Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
    }

    void RegisterWithEcholocation()
    {
        // Register with echolocation controller
        EcholocationController.RegisterJammer(this);
        isJamming = true;
        Debug.Log("EcholocationJammerEnemy: Registered with echolocation controller");
    }

    protected override void OnEnemyUpdate()
    {
        // Pulse the jammer visual
        if (jammerVisual != null)
        {
            float pulse = Mathf.PingPong(Time.time * 2f, 1f);
            float scale = 2f + pulse * 0.5f;
            jammerVisual.transform.localScale = Vector3.one * scale;
            
            SpriteRenderer sr = jammerVisual.GetComponent<SpriteRenderer>();
            if (sr != null)
            {
                Color color = sr.color;
                color.a = 0.2f + pulse * 0.2f;
                sr.color = color;
            }
        }
    }

    protected override void OnEnemyDeath()
    {
        // Unregister from echolocation
        UnregisterFromEcholocation();
    }

    void UnregisterFromEcholocation()
    {
        EcholocationController.UnregisterJammer(this);
        Debug.Log("EcholocationJammerEnemy: Unregistered from echolocation");
    }

    void OnDestroy()
    {
        UnregisterFromEcholocation();
    }

    // Public method that EcholocationController can check
    public bool IsJamming()
    {
        return isJamming && IsActive;
    }

    public float GetJammerRadius()
    {
        return jammerRadius;
    }

    public float GetInterferenceStrength()
    {
        return interferenceStrength;
    }
}
