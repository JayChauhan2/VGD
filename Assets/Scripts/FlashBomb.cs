using UnityEngine;
using System.Collections;

public class FlashBomb : MonoBehaviour
{
    [Header("Explosion Settings")]
    public float explosionDelay = 1.5f;

    public float explosionRadius = 2f; // Matched to visual (Radius 2)
    public float damagePercent = 0.5f; // 50% of enemy max health
    
    [Header("Explosion Animation Settings")]
    public bool useNormalizedTiming = true; // Toggle: True = use 0.0-1.0, False = use seconds
    [Range(0.1f, 1.0f)]
    public float explosionTimingNormalized = 0.7f; // % of animation
    public float explosionTimingSeconds = 0.5f;   // Specific time in seconds
    public float explosionFreezeDuration = 0.5f; // Duration of the freeze
    public float swellAmount = 1.5f; // How much it scales up during animation
    
    [Header("Throw Animation Settings")]
    public float throwScaleAmount = 1.5f; // Peak scale multiplier during throw
    public float throwDuration = 0.4f; // Total time for throw
    public float spinSpeed = 1080f; // Degrees per second (3 spins = 1080)
    public float bounceScaleAmount = 1.2f; // Peak scale multiplier during first bounce
    public float bounceDuration = 0.2f; // Bounce time
    public int bounceCount = 2; // Number of bounces

    [Header("Visual Settings")]
    public float flashDuration = 0.3f;
    public float maxFlashScale = 6.25f; // Scale 6.25 * 0.64(unit) = 4.0 diameter = 2.0 radius
    public GameObject explosionEffectPrefab; // Optional custom visual
    
    [Header("Camera Shake")]
    public float shakeIntensity = 1.0f;
    public float shakeDuration = 0.4f;
    
    private SpriteRenderer spriteRenderer;
    private bool hasExploded = false;
    private Vector3 targetPosition;

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

        // Store the intended position (where it was instantiated)
        targetPosition = transform.position;

        // Start throw sequence immediately
        StartCoroutine(ThrowAndLandSequence());
    }

    IEnumerator ThrowAndLandSequence()
    {
        // 1. Determine Start Position
        // We want it to look like it comes from the player if possible
        Vector3 startPosition = targetPosition;
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player != null)
        {
            // Start at player position (no upward offset)
            startPosition = player.transform.position;
        }

        // Store original scale to return to
        Vector3 baseScale = transform.localScale;

        // 2. Perform The Throw Arc (Scale-based "Z-axis" arc)
        float elapsed = 0f;
        
        while (elapsed < throwDuration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / throwDuration;
            
            // Linear movement to target
            transform.position = Vector3.Lerp(startPosition, targetPosition, t);
            
            // Scale Arc: Parabola 0 -> 1 -> 0
            float arcFactor = 4f * (t - (t * t)); 
            
            // Calculate scale: Base -> Peak -> Base
            Vector3 peakScale = baseScale * throwScaleAmount;
            transform.localScale = Vector3.Lerp(baseScale, peakScale, arcFactor);
            
            // Spin
            float rotation = t * spinSpeed; // e.g. 0 to 1080
            transform.rotation = Quaternion.Euler(0, 0, rotation);

            yield return null;
        }

        // Ensure we land exactly at target and reset
        transform.position = targetPosition;
        transform.rotation = Quaternion.identity;
        transform.localScale = baseScale;

        // 3. Bounce Effect (Scale Bounce)
        // Bounce in scale (a little jump towards camera)
        float currentBounceScale = bounceScaleAmount; 
        float currentBounceDuration = bounceDuration;
        
        for (int i = 0; i < bounceCount; i++)
        {
            elapsed = 0f;
            while (elapsed < currentBounceDuration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / currentBounceDuration;
                
                // Bounce Parabola
                float arcFactor = 4f * (t - (t * t));
                
                // Scale Bounce
                Vector3 bouncePeakScale = baseScale * currentBounceScale;
                transform.localScale = Vector3.Lerp(baseScale, bouncePeakScale, arcFactor);
                
                yield return null;
            }
            
            // Prepare for next smaller bounce
            float extraScale = currentBounceScale - 1.0f;
            extraScale /= 2f;
            currentBounceScale = 1.0f + extraScale;
            
            currentBounceDuration /= 1.5f;
        }
        
        // Ensure final scale
        transform.localScale = baseScale;

        // 4. Start Explosion Countdown (Existing Logic)
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
            // Existing pulse logic
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
        
        float effectDuration = 0f;

        // Visual Effect
        if (explosionEffectPrefab != null)
        {
            // Instantiate custom effect
            GameObject explosionInstance = Instantiate(explosionEffectPrefab, transform.position, Quaternion.identity);
            
            // Calculate base duration
            float baseDuration = flashDuration; // Default fallback
            
            // Try to get duration from Animator
            Animator anim = explosionInstance.GetComponent<Animator>();
            if (anim != null)
            {
                anim.Update(0f);
                AnimatorStateInfo stateInfo = anim.GetCurrentAnimatorStateInfo(0);
                if (stateInfo.length > 0) baseDuration = stateInfo.length;
            }
            else 
            {
                ParticleSystem ps = explosionInstance.GetComponent<ParticleSystem>();
                if (ps != null)
                {
                    baseDuration = ps.main.duration;
                    if (!ps.main.loop) baseDuration += ps.main.startLifetime.constant;
                }
            }

            // Start custom animation sequence (Swell + Freeze) on the instance
            StartCoroutine(AnimateExplosionEffect(explosionInstance, anim, baseDuration));
            
            // Total time needed before destruction
            effectDuration = baseDuration + explosionFreezeDuration;
            
            // EXPLICITLY schedule destruction of the visual effect to prevent looping if the coroutine dies
            Destroy(explosionInstance, effectDuration);
            
            // Hide the bomb itself immediately
            if (spriteRenderer != null) spriteRenderer.enabled = false;
        }
        else
        {
            // Fallback to default white flash effect
            StartCoroutine(FlashEffect());
            effectDuration = flashDuration;
            
            // Standard damage for fallback (no visual instance bounds)
            ApplyExplosionImpact(null);
        }
        
        // Camera shake
        if (CameraController.Instance != null)
        {
            StartCoroutine(CameraShake());
        }
        
        // Destroy after effects complete
        // Use meaningful max of all durations PLUS A BUFFER to ensure coroutines finish
        float destroyDelay = Mathf.Max(effectDuration, shakeDuration) + 0.1f;
        
        Destroy(gameObject, destroyDelay);
    }

    // Helper to keep Explode() clean
    void ApplyExplosionImpact(GameObject visualInstance = null)
    {
        float currentExplosionRadius = explosionRadius; // Default fallback

        // If we have a visual instance, use its actual rendered bounds for perfect accuracy
        if (visualInstance != null)
        {
            Renderer r = visualInstance.GetComponent<Renderer>();
            if (r != null)
            {
                // Use the largest extent to cover the visual area (safe for non-circular sprites)
                currentExplosionRadius = Mathf.Max(r.bounds.extents.x, r.bounds.extents.y);
            }
        }

        Collider2D[] hits = Physics2D.OverlapCircleAll(transform.position, currentExplosionRadius);
        Debug.Log($"FlashBomb: Found {hits.Length} objects in blast radius ({currentExplosionRadius})");
        
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
            float distancePercent = 1f - Mathf.Clamp01(distance / currentExplosionRadius);
            
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
    }

    IEnumerator AnimateExplosionEffect(GameObject instance, Animator anim, float duration)
    {
        float elapsed = 0f;
        Vector3 originalScale = instance.transform.localScale;
        Vector3 targetScale = originalScale * swellAmount;
        
        // 1. Play until Freeze Point (Swell Up)
        float freezeTime = useNormalizedTiming ? (duration * explosionTimingNormalized) : explosionTimingSeconds;
        
        // Safety clamp
        if (freezeTime > duration) freezeTime = duration;
        if (freezeTime < 0) freezeTime = 0;
        
        while (elapsed < freezeTime)
        {
            if (instance == null) yield break;
            
            elapsed += Time.deltaTime;
            float t = elapsed / freezeTime;
            
            // Swell: Up (Sin curve 0->1) but effectively just Lerp up for the first part?
            // Bomber logic: Sin(t * PI) for full Up/Down.
            // Let's do a simple swell up then down over the full duration? 
            // Or swell UP to the freeze point? 
            // "size scale-up and then the explosion freezes" -> Implies Scale UP -> Freeze.
            
            // Let's scale UP to swellAmount
            instance.transform.localScale = Vector3.Lerp(originalScale, targetScale, t);
            
            yield return null;
        }
        
        // APPLY DAMAGE HERE (After delay, with scaling swell)
        ApplyExplosionImpact(instance);
        
        // 2. Freeze
        if (anim != null) anim.speed = 0f;
        yield return new WaitForSeconds(explosionFreezeDuration);
        if (anim != null && instance != null) anim.speed = 1f;
        
        // 3. Finish Animation (Scale Down?)
        // If we want it to "swell", maybe we should scale down after?
        // Let's scale back down to original over the remaining time.
        float remainingTime = duration - freezeTime;
        elapsed = 0f;
        
        while (elapsed < remainingTime)
        {
            if (instance == null) yield break;
            
            elapsed += Time.deltaTime;
            float t = elapsed / remainingTime;
            
            instance.transform.localScale = Vector3.Lerp(targetScale, originalScale, t);
            yield return null;
        }
        
        // Ensure instance is destroyed
        if (instance != null) Destroy(instance);
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
