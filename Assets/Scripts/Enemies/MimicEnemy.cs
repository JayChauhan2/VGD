using UnityEngine;

public class MimicEnemy : EnemyAI
{
    [Header("Mimic Settings")]
    public float revealDistance = 2.5f;
    public float attackSpeed = 7f;
    
    private bool isRevealed = false;
    private SpriteRenderer spriteRenderer;
    private Sprite coinSprite;
    private Sprite enemySprite;

    protected override void OnEnemyStart()
    {
        maxHealth = 70f;
        currentHealth = maxHealth;
        speed = 0f; // Doesn't move until revealed
        
        spriteRenderer = GetComponent<SpriteRenderer>();
        
        // Create coin disguise sprite
        coinSprite = CreateCoinSprite();
        enemySprite = CreateEnemySprite();
        
        if (spriteRenderer != null)
        {
            spriteRenderer.sprite = coinSprite;
            spriteRenderer.color = new Color(1f, 0.84f, 0f); // Gold color
        }
        
        Debug.Log("MimicEnemy: Initialized in disguise");
    }

    Sprite CreateCoinSprite()
    {
        // Create simple circle for coin
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

    Sprite CreateEnemySprite()
    {
        // Create different sprite for revealed form (red circle)
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
        
        if (!isRevealed)
        {
            // Check distance to player
            float distanceToPlayer = Vector2.Distance(transform.position, target.position);
            
            if (distanceToPlayer <= revealDistance)
            {
                Reveal();
            }
        }
        else
        {
            // Chase player aggressively after reveal
            speed = attackSpeed;
        }
    }

    void Reveal()
    {
        isRevealed = true;
        
        // Change appearance
        if (spriteRenderer != null)
        {
            spriteRenderer.sprite = enemySprite;
            spriteRenderer.color = new Color(1f, 0.2f, 0.2f); // Red
            transform.localScale = Vector3.one * 1.2f; // Slightly larger
        }
        
        Debug.Log("MimicEnemy: REVEALED! Attacking player!");
    }

    public override void TakeDamage(float damage)
    {
        // Taking damage also reveals the mimic
        if (!isRevealed)
        {
            Reveal();
        }
        
        base.TakeDamage(damage);
    }
}
