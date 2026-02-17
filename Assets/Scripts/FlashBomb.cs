using UnityEngine;
using System.Collections;

public class FlashBomb : MonoBehaviour
{
    [Header("Explosion Settings")]
    public float explosionDelay = 1.5f;

    public float explosionRadius = 2f; // Matched to visual (Radius 2)
    public float damagePercent = 0.5f; // 50% of enemy max health
    
    [Header("Visual Settings")]
    public float flashDuration = 0.3f;
    public float maxFlashScale = 6.25f; // Scale 6.25 * 0.64(unit) = 4.0 diameter = 2.0 radius
    public GameObject explosionEffectPrefab; // Optional custom visual
    
    [Header("Camera Shake")]
    public float shakeIntensity = 1.0f;
    public float shakeDuration = 0.4f;
    
    private SpriteRenderer spriteRenderer;
    private bool hasExploded = false;

    void Start()
    {
        // Try to get existing visual representation (for custom prefabs)
        spriteRenderer = GetComponent<SpriteRenderer>();
        
        // Only if none exists, create procedural one
        if (spriteRenderer == null)
        {
            spriteRenderer = gameObject.AddComponent<SpriteRenderer>();
            spriteRenderer.sprite = CreateCircleSprite();
            spriteRenderer.color = new Color(1f, 1f, 1f, 0.3f); // White, semi-transparent
            spriteRenderer.sortingLayerName = "Object"; // Set sorting layer as requested
            spriteRenderer.sortingOrder = 10; // Ensure it draws on TOP of other obstacles
            
            // Only apply scale if we created the visual (assume prefab is scaled correctly)
            transform.localScale = Vector3.one * 0.5f;
        }
        
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
        
        // Visual Effect
        if (explosionEffectPrefab != null)
        {
            // Instantiate custom effect
            GameObject explosionInstance = Instantiate(explosionEffectPrefab, transform.position, Quaternion.identity);
            
            float duration = flashDuration; // Default fallback
            
            // Try to get duration from Animator
            Animator anim = explosionInstance.GetComponent<Animator>();
            if (anim != null)
            {
                // Force an update to ensure state info is ready
                anim.Update(0f);
                AnimatorStateInfo stateInfo = anim.GetCurrentAnimatorStateInfo(0);
                if (stateInfo.length > 0)
                {
                    duration = stateInfo.length;
                }
            }
            // If no animator (or invalid length), try ParticleSystem
            else 
            {
                ParticleSystem ps = explosionInstance.GetComponent<ParticleSystem>();
                if (ps != null)
                {
                    duration = ps.main.duration;
                    if (!ps.main.loop) duration += ps.main.startLifetime.constant;
                }
            }
            
            // Destroy the effect after the detected duration
            Destroy(explosionInstance, duration);
            
            // Hide the bomb itself immediately
            if (spriteRenderer != null) spriteRenderer.enabled = false;
        }
        else
        {
            // Fallback to default white flash effect
            StartCoroutine(FlashEffect());
        }
        
        // Camera shake
        if (CameraController.Instance != null)
        {
            StartCoroutine(CameraShake());
        }
        
        // Deal damage and knockback to all enemies AND player in radius
        // Use logic radius matching visual
        Collider2D[] hits = Physics2D.OverlapCircleAll(transform.position, explosionRadius);
        Debug.Log($"FlashBomb: Found {hits.Length} objects in blast radius");
        
        foreach (Collider2D hit in hits)
        {
            // Calculate knockback direction and distance-based force
            Vector2 diff = hit.transform.position - transform.position;
            Vector2 direction = diff.normalized;
            
            // Handle case where bomb creates exactly on top of entity (prevent 0 direction)
            if (direction == Vector2.zero)
            {
                 direction = Random.insideUnitCircle.normalized;
                 if (direction == Vector2.zero) direction = Vector2.right;
            }
            
            float distance = diff.magnitude;
            float distancePercent = 1f - Mathf.Clamp01(distance / explosionRadius);
            
            // Check for enemy
            // Check for enemy
            EnemyAI enemy = hit.GetComponent<EnemyAI>();
            if (enemy == null) enemy = hit.GetComponentInParent<EnemyAI>();
            
            if (enemy != null)
            {
                Debug.Log($"FlashBomb: Hit enemy {enemy.name}");
                
                // Deal damage (50% of max health)
                float damage = enemy.maxHealth * damagePercent;
                enemy.TakeDamage(damage);
                
                // Apply knockback (stronger when closer)
                // Increased base force from 10 to 30
                // Since we switched to Direct Velocity in EnemyAI, 30 is a speed of 30 units/s.
                // That's very fast. Let's keep it but maybe dampen distance factor less?
                float knockbackForce = 20f * distancePercent; // Tuned for velocity mode
                
                // Use the new method to pause AI and apply force
                enemy.ApplyKnockback(direction * knockbackForce, 0.4f);
            }
            
            // Check for player
            PlayerHealth player = hit.GetComponent<PlayerHealth>();
            if (player != null)
            {
                // Player takes exactly 1 heart (2 health units) of damage
                // We pass direction for the damage visual
                player.TakeDamage(2f, direction);
                
                // Explicitly apply strong bomb knockback to player
                PlayerMovement pm = player.GetComponent<PlayerMovement>();
                if (pm != null)
                {
                    // Reduced from 20f to 8f per user request ("way too strong")
                    float playerKnockbackForce = 8f * distancePercent; 
                    pm.ApplyKnockback(direction * playerKnockbackForce, 0.3f); // Reduced duration slightly too
                }
            }
        }
        
        // Destroy after adequate time for shakes/effects to finish
        // If using custom prefab, we can destroy self sooner (after shake)
        // If using default flash, we need to wait for flashDuration
        float destroyDelay = (explosionEffectPrefab != null) ? shakeDuration : Mathf.Max(flashDuration, shakeDuration);
        
        Destroy(gameObject, destroyDelay);
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
