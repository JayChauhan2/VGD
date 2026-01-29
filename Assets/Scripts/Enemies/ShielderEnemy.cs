using UnityEngine;

public class ShielderEnemy : EnemyAI
{
    [Header("Shielder Settings")]
    public float shieldArc = 180f; // Degrees of protection in front
    
    private SpriteRenderer shieldIndicator;

    protected override void OnEnemyStart()
    {
        maxHealth = 110f;
        currentHealth = maxHealth;
        speed = 2.5f;
        
        CreateShieldVisual();
        
        Debug.Log("ShielderEnemy: Initialized");
    }

    void CreateShieldVisual()
    {
        // Create a child object for shield visual
        GameObject shieldObj = new GameObject("ShieldIndicator");
        shieldObj.transform.SetParent(transform);
        shieldObj.transform.localPosition = new Vector3(0.6f, 0, 0); // In front of enemy
        shieldObj.transform.localScale = Vector3.one * 0.8f;
        
        // Add sprite renderer
        shieldIndicator = shieldObj.AddComponent<SpriteRenderer>();
        shieldIndicator.sprite = CreateShieldSprite();
        shieldIndicator.color = new Color(0.5f, 0.5f, 1f, 0.7f); // Semi-transparent blue
        shieldIndicator.sortingOrder = 1;
    }

    Sprite CreateShieldSprite()
    {
        // Create a simple rectangle for shield
        int width = 16;
        int height = 32;
        Texture2D texture = new Texture2D(width, height);
        Color[] pixels = new Color[width * height];
        
        for (int i = 0; i < pixels.Length; i++)
        {
            pixels[i] = Color.white;
        }
        
        texture.SetPixels(pixels);
        texture.Apply();
        
        return Sprite.Create(texture, new Rect(0, 0, width, height), new Vector2(0.5f, 0.5f));
    }

    [Header("Rotation Settings")]
    public float rotationSpeed = 200f; // Degrees per second

    protected override void OnEnemyUpdate()
    {
        if (target == null) return;
        
        // Calculate target angle
        Vector2 direction = (target.position - transform.position).normalized;
        float targetAngle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
        
        // Smoothly rotate towards target angle
        float currentAngle = transform.rotation.eulerAngles.z;
        float newAngle = Mathf.MoveTowardsAngle(currentAngle, targetAngle, rotationSpeed * Time.deltaTime);
        
        transform.rotation = Quaternion.Euler(0, 0, newAngle);
    }

    public override void TakeDamage(float damage)
    {
        if (target == null)
        {
            base.TakeDamage(damage);
            return;
        }
        
        // Calculate if damage is coming from the front (shielded side)
        Vector2 directionToPlayer = (target.position - transform.position).normalized;
        Vector2 forward = transform.right; // In 2D, right is forward when rotated
        
        float dotProduct = Vector2.Dot(forward, directionToPlayer);
        float angleToPlayer = Mathf.Acos(dotProduct) * Mathf.Rad2Deg;
        
        // If attack is from the front (within shield arc), block it
        if (angleToPlayer < shieldArc / 2f)
        {
            Debug.Log("ShielderEnemy: Blocked damage with shield!");
            
            // Visual feedback - flash shield
            if (shieldIndicator != null)
            {
                shieldIndicator.color = Color.white;
                Invoke(nameof(ResetShieldColor), 0.1f);
            }
            
            return; // Damage blocked
        }
        
        // Damage from side or back - take full damage
        Debug.Log("ShielderEnemy: Hit from side/back!");
        base.TakeDamage(damage);
    }

    void ResetShieldColor()
    {
        if (shieldIndicator != null)
        {
            shieldIndicator.color = new Color(0.5f, 0.5f, 1f, 0.7f);
        }
    }
}
