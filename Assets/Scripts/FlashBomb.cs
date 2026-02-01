using UnityEngine;
using System.Collections;

public class FlashBomb : MonoBehaviour
{
    [Header("Explosion Settings")]
    public float explosionDelay = 1.5f;
    public float explosionRadius = 5f;
    public float damagePercent = 0.5f; // 50% of enemy max health
    
    [Header("Visual Settings")]
    public float flashDuration = 0.3f;
    public float maxFlashScale = 10f;
    
    [Header("Camera Shake")]
    public float shakeIntensity = 1.0f;
    public float shakeDuration = 0.4f;
    
    private SpriteRenderer spriteRenderer;
    private bool hasExploded = false;

    void Start()
    {
        // Create visual representation
        spriteRenderer = gameObject.AddComponent<SpriteRenderer>();
        spriteRenderer.sprite = CreateCircleSprite();
        spriteRenderer.color = new Color(1f, 1f, 1f, 0.3f); // White, semi-transparent
        transform.localScale = Vector3.one * 0.5f;
        
        // Start explosion countdown
        StartCoroutine(ExplodeAfterDelay());
    }

    IEnumerator ExplodeAfterDelay()
    {
        // Pulse effect while waiting
        float elapsed = 0f;
        Vector3 initialScale = transform.localScale;
        
        while (elapsed < explosionDelay)
        {
            elapsed += Time.deltaTime;
            float pulse = 1f + Mathf.Sin(elapsed * 20f) * 0.2f;
            transform.localScale = initialScale * pulse;
            yield return null;
        }
        
        Explode();
    }

    void Explode()
    {
        if (hasExploded) return;
        hasExploded = true;
        
        // White flash effect
        StartCoroutine(FlashEffect());
        
        // Camera shake
        if (CameraController.Instance != null)
        {
            StartCoroutine(CameraShake());
        }
        
        // Deal damage and knockback to all enemies AND player in radius
        Collider2D[] hits = Physics2D.OverlapCircleAll(transform.position, explosionRadius);
        Debug.Log($"FlashBomb: Found {hits.Length} objects in blast radius");
        
        foreach (Collider2D hit in hits)
        {
            // Calculate knockback direction and distance-based force
            Vector2 direction = (hit.transform.position - transform.position).normalized;
            float distance = Vector2.Distance(transform.position, hit.transform.position);
            float distancePercent = 1f - Mathf.Clamp01(distance / explosionRadius);
            
            // Check for enemy
            EnemyAI enemy = hit.GetComponent<EnemyAI>();
            if (enemy != null)
            {
                // Deal damage (50% of max health)
                float damage = enemy.maxHealth * damagePercent;
                enemy.TakeDamage(damage);
                
                // Apply knockback (stronger when closer)
                Rigidbody2D enemyRb = enemy.GetComponent<Rigidbody2D>();
                if (enemyRb != null)
                {
                    // Reset velocity first to ensure knockback is applied cleanly
                    enemyRb.linearVelocity = Vector2.zero;
                    
                    float knockbackForce = 10f * distancePercent; // Max 10 force when at center
                    enemyRb.AddForce(direction * knockbackForce, ForceMode2D.Impulse);
                    
                    Debug.Log($"FlashBomb: Applied {knockbackForce} knockback force to {enemy.name} at distance {distance:F2}");
                }
                else
                {
                    Debug.LogWarning($"FlashBomb: Enemy {enemy.name} has no Rigidbody2D!");
                }
                
                Debug.Log($"FlashBomb: Dealt {damage} damage to {enemy.name} with {distancePercent:F2} knockback percent");
            }
            
            // Check for player
            PlayerHealth player = hit.GetComponent<PlayerHealth>();
            if (player != null)
            {
                // Player takes exactly 1 heart (2 health units) of damage
                // PlayerHealth.TakeDamage applies its own knockbackForce multiplier (10f)
                // So we just pass the direction, not a pre-scaled force
                player.TakeDamage(2f, direction);
                Debug.Log($"FlashBomb: Hit player at distance {distance:F2}");
            }
        }
        
        // Destroy after flash completes
        Destroy(gameObject, flashDuration);
    }

    IEnumerator FlashEffect()
    {
        float elapsed = 0f;
        
        while (elapsed < flashDuration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / flashDuration;
            
            // Expand quickly, then fade
            float scale = Mathf.Lerp(1f, maxFlashScale, t);
            transform.localScale = Vector3.one * scale;
            
            // Fade out
            float alpha = Mathf.Lerp(0.8f, 0f, t);
            spriteRenderer.color = new Color(1f, 1f, 1f, alpha);
            
            yield return null;
        }
    }

    IEnumerator CameraShake()
    {
        float elapsed = 0f;
        
        while (elapsed < shakeDuration)
        {
            elapsed += Time.deltaTime;
            
            // Random shake offset that decreases over time
            float intensity = shakeIntensity * (1f - elapsed / shakeDuration);
            Vector3 shakeOffset = new Vector3(
                Random.Range(-intensity, intensity),
                Random.Range(-intensity, intensity),
                0f
            );
            
            CameraController.Instance.SetRecoilOffset(shakeOffset);
            yield return null;
        }
        
        // Reset camera
        CameraController.Instance.SetRecoilOffset(Vector3.zero);
    }

    Sprite CreateCircleSprite()
    {
        int size = 64;
        Texture2D tex = new Texture2D(size, size);
        Color[] colors = new Color[size * size];
        Vector2 center = new Vector2(size / 2f, size / 2f);
        float radius = size / 2f;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dist = Vector2.Distance(new Vector2(x, y), center);
                if (dist <= radius)
                {
                    // Soft gradient from center
                    float alpha = 1f - (dist / radius);
                    colors[y * size + x] = new Color(1f, 1f, 1f, alpha);
                }
                else
                {
                    colors[y * size + x] = Color.clear;
                }
            }
        }
        
        tex.SetPixels(colors);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
    }

    void OnDrawGizmosSelected()
    {
        // Show explosion radius in editor
        Gizmos.color = Color.white;
        Gizmos.DrawWireSphere(transform.position, explosionRadius);
    }
}
