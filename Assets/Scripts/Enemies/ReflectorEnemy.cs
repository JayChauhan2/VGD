using UnityEngine;

public class ReflectorEnemy : EnemyAI
{
    [Header("Reflector Settings")]
    public float shieldedDuration = 3f;
    public float vulnerableDuration = 2f;
    public float reflectDamageMultiplier = 0.5f;
    
    private enum ShieldState { Shielded, Vulnerable }
    private ShieldState currentState = ShieldState.Shielded;
    
    private float stateTimer;
    private SpriteRenderer spriteRenderer;
    private GameObject shieldVisual;

    protected override void OnEnemyStart()
    {
        maxHealth = 135f;
        currentHealth = maxHealth;
        // speed = 2.5f; // Removed to allow Inspector value
        
        spriteRenderer = GetComponent<SpriteRenderer>();
        stateTimer = shieldedDuration;
        
        CreateShieldVisual();
        UpdateVisuals();
        
        Debug.Log("ReflectorEnemy: Initialized in shielded state");
    }

    void CreateShieldVisual()
    {
        // Create shield visual as child object
        shieldVisual = new GameObject("ShieldVisual");
        shieldVisual.transform.SetParent(transform);
        shieldVisual.transform.localPosition = Vector3.zero;
        shieldVisual.transform.localScale = Vector3.one * 1.5f;
        
        SpriteRenderer shieldSR = shieldVisual.AddComponent<SpriteRenderer>();
        shieldSR.sprite = CreateCircleSprite();
        shieldSR.color = new Color(0f, 1f, 1f, 0.4f); // Cyan, semi-transparent
        shieldSR.sortingOrder = -1; // Behind enemy
    }

    Sprite CreateCircleSprite()
    {
        int size = 32;
        Texture2D texture = new Texture2D(size, size);
        Color[] pixels = new Color[size * size];
        
        Vector2 center = new Vector2(size / 2f, size / 2f);
        float outerRadius = size / 2f;
        float innerRadius = size / 2f - 4f;
        
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float distance = Vector2.Distance(new Vector2(x, y), center);
                // Create ring shape
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

    protected override void OnEnemyUpdate()
    {
        // Update state timer
        stateTimer -= Time.deltaTime;
        
        if (stateTimer <= 0)
        {
            // Switch states
            if (currentState == ShieldState.Shielded)
            {
                currentState = ShieldState.Vulnerable;
                stateTimer = vulnerableDuration;
            }
            else
            {
                currentState = ShieldState.Shielded;
                stateTimer = shieldedDuration;
            }
            
            UpdateVisuals();
        }
    }

    void UpdateVisuals()
    {
        if (shieldVisual != null)
        {
            shieldVisual.SetActive(currentState == ShieldState.Shielded);
        }
        
        if (spriteRenderer != null)
        {
            // Change color based on state
            if (currentState == ShieldState.Shielded)
            {
                spriteRenderer.color = new Color(0.5f, 0.8f, 1f); // Light blue
            }
            else
            {
                spriteRenderer.color = new Color(1f, 0.6f, 0.3f); // Orange (vulnerable)
            }
        }
        
        Debug.Log($"ReflectorEnemy: State changed to {currentState}");
    }

    public override void TakeDamage(float damage)
    {
        if (currentState == ShieldState.Shielded)
        {
            Debug.Log("ReflectorEnemy: Damage blocked by shield!");
            
            // Reflect damage back to player (if damage source is laser)
            // This is a simplified implementation
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            if (player != null)
            {
                PlayerHealth playerHealth = player.GetComponent<PlayerHealth>();
                if (playerHealth != null)
                {
                    Vector2 knockbackDir = (player.transform.position - transform.position).normalized;
                    playerHealth.TakeDamage(damage * reflectDamageMultiplier, knockbackDir);
                    Debug.Log("ReflectorEnemy: Reflected damage to player!");
                }
            }
            
            // Visual feedback
            if (shieldVisual != null)
            {
                SpriteRenderer shieldSR = shieldVisual.GetComponent<SpriteRenderer>();
                if (shieldSR != null)
                {
                    shieldSR.color = Color.white;
                    Invoke(nameof(ResetShieldColor), 0.1f);
                }
            }
            
            return; // Don't take damage
        }
        
        // Vulnerable state - take full damage
        base.TakeDamage(damage);
    }

    void ResetShieldColor()
    {
        if (shieldVisual != null)
        {
            SpriteRenderer shieldSR = shieldVisual.GetComponent<SpriteRenderer>();
            if (shieldSR != null)
            {
                shieldSR.color = new Color(0f, 1f, 1f, 0.4f);
            }
        }
    }
}
