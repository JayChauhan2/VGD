using UnityEngine;
using System.Collections;

public class BomberEnemy : EnemyAI
{
    [Header("Bomber Settings")]
    public float explosionRadius = 1.3f; // Reduced from 3f to match visual (approx 1.28f)
    public float playerDamage = 30f;
    public float enemyDamage = 30f;
    public float explosionDelay = 0.5f; // Time to wait for animation before exploding
    [Range(0.1f, 1.0f)]
    public float explosionTimingNormalized = 0.7f; // When in the animation to explode (0.0 to 1.0)
    public float explosionFreezeDuration = 0.5f; // How long to freeze animation at explosion point
    public bool showShockwave = true; // Toggle for the white explosion effect
    public GameObject explosionEffectPrefab; // Optional custom visual to replace procedural shockwave
    public float shockwaveSwellAmount = 1.5f; // Multiplier for custom shockwave prefab scale
    public float bodySwellAmount = 1.5f; // Multiplier for the enemy body swell
    
    [Header("Camera Shake")]
    public float shakeIntensity = 1.0f;
    public float shakeDuration = 0.4f;
    
    private bool isExploding = false;
    private bool isFrozen = false; // Flag to pause visual effects
    private float explosionDuration = 0f;

    protected override void OnEnemyStart()
    {
        maxHealth = 60f;
        currentHealth = maxHealth;
        // speed = 3.5f; // Removed hardcoded speed to allow Inspector control
        
        Debug.Log("BomberEnemy: Initialized - will explode on death!");
        
        // Cache animation length
        if (animator != null)
        {
            foreach(AnimationClip clip in animator.runtimeAnimatorController.animationClips)
            {
                if (clip.name.Contains("Explode"))
                {
                    explosionDuration = clip.length;
                    break;
                }
            }
        }
    }

    protected override void OnEnemyDeath()
    {
        // This is called by base.Die() -> DropLoot() -> OnEnemyDeath() -> Destroy(gameObject)
        // BUT, we want to intercept the death to play animation FIRST.
        // So we actually need to override Die() to control the flow, OR we can use OnEnemyDeath 
        // if we change how base.Die works. 
        // Looking at EnemyAI.cs, Die() calls OnEnemyDeath() then Destroy().
        // To delay destruction, we must override Die().
    }

    // Removed Die() override: We want default behavior (Destroy on death)
    
    public override void TakeDamage(float damage)
    {
        if (isExploding) return; // Invincible during explosion animation
        base.TakeDamage(damage);
    }
    
    public override void ApplyKnockback(Vector2 force, float duration)
    {
        if (isExploding) return; // Immovable during explosion animation
        base.ApplyKnockback(force, duration);
    }

    private IEnumerator ExplodeSequence()
    {
        isExploding = true;
        SetActive(false); // Disable AI (Movement, Touch Damage, etc.) completely
        
        // Stop movement (Redundant but safe)
        speed = 0f;
        if (rb != null) 
        {
            rb.linearVelocity = Vector2.zero;
            rb.bodyType = RigidbodyType2D.Kinematic; // Make immovable
        }
        if (pathfinding != null) pathfinding = null; // Stop path updates
        
        float waitTime = explosionDelay; // Default fallback

        // Trigger Animation
        // Use cached duration for reliability
        if (explosionDuration > 0)
        {
            waitTime = explosionDuration;
        }
        else
        {
             Debug.LogWarning("BomberEnemy: 'Explode' animation clip not found! Using default delay: " + explosionDelay);
        }

        if (animator != null)
        {
            animator.SetTrigger("Explode");
        }
        
        Debug.Log($"BomberEnemy Sequence: Duration={waitTime}s, Normalized={explosionTimingNormalized}, ExplosionTime={waitTime * explosionTimingNormalized}s");
        
        // Calculate timing: Start visual effect BEFORE explosion so it peaks AT explosion
        float visualDuration = 0.3f;
        float explosionWait = waitTime * explosionTimingNormalized;
        float timeToExplosion = explosionWait;
        
        // Start effect early (halfway through its duration so peak hits at explosion time)
        // Ensure we don't try to wait negative time
        float timeToStartVisuals = Mathf.Max(0f, timeToExplosion - (visualDuration * 0.5f));
        
        // Wait until visual start
        yield return new WaitForSeconds(timeToStartVisuals);
        
        // --- Visual Effects Trigger ---
        StartCoroutine(PlayVisualEffects(visualDuration)); 

        // Wait remaining time until actual explosion
        yield return new WaitForSeconds(timeToExplosion - timeToStartVisuals);

        // BOOM (Damage happens here now)
        Explode();
        
        // --- Hit Stop / Freeze Frame Effect ---
        if (explosionFreezeDuration > 0f)
        {
            if (animator != null) animator.speed = 0f;
            isFrozen = true; // Pause visual effects
            yield return new WaitForSeconds(explosionFreezeDuration);
            isFrozen = false; // Resume visual effects
            if (animator != null) animator.speed = 1f;
        }

        // Wait for remainder of animation (so visual can finish playing)
        yield return new WaitForSeconds(waitTime - explosionWait);
        
        DropLoot();
        if (parentRoom != null) parentRoom.EnemyDefeated(this);
        Destroy(gameObject);
    }
    
    private IEnumerator PlayVisualEffects(float duration)
    {
        // 1. Swell Effect (Scale Up and Down)
        Vector3 originalScale = transform.localScale;
        Vector3 targetScale = originalScale * bodySwellAmount;
        float elapsed = 0f;
        
        // 2. White Shockwave Circle (Procedural OR Prefab)
        GameObject shockwave = null;
        SpriteRenderer sr = null;
        Animator shockwaveAnim = null;
        bool isProceduralShockwave = false;
        
        Vector3 originalShockwaveScale = Vector3.one; // For custom prefab scaling base
        
        if (showShockwave)
        {
            if (explosionEffectPrefab != null)
            {
                // Instantiate Custom Prefab
                shockwave = Instantiate(explosionEffectPrefab, transform.position, Quaternion.identity);
                sr = shockwave.GetComponent<SpriteRenderer>(); 
                shockwaveAnim = shockwave.GetComponent<Animator>();
                originalShockwaveScale = shockwave.transform.localScale;
            }
            else
            {
                // Create Procedural Sprite
                shockwave = new GameObject("Shockwave");
                shockwave.transform.position = transform.position;
                sr = shockwave.AddComponent<SpriteRenderer>();
                sr.sprite = CreateCircleSprite();
                sr.color = new Color(1f, 1f, 1f, 0.8f);
                sr.sortingLayerName = "Object";
                sr.sortingOrder = 10;
                shockwave.transform.localScale = Vector3.one * 0.5f;
                // Mark procedural: We control its alpha/scale manually
                isProceduralShockwave = true;
            }
            
            // Safety: Ensure shockwave is destroyed even if this coroutine steps
            Destroy(shockwave, duration + 0.5f);
        }
        
        // Calculate procedural scale (radius based)
        float maxShockwaveScale = (explosionRadius * 2f) / 0.64f; 

        while (elapsed < duration)
        {
            // Pause while frozen
            if (isFrozen)
            {
                if (shockwaveAnim != null) shockwaveAnim.speed = 0f;
                yield return null;
                continue;
            }
            else
            {
                if (shockwaveAnim != null) shockwaveAnim.speed = 1f;
            }

            elapsed += Time.deltaTime;
            float t = elapsed / duration;
            
            // Swell: Up then Down (Sin curve 0->1->0)
            // sin(0) = 0, sin(pi) = 0. We want 0 to PI.
            float swell = Mathf.Sin(t * Mathf.PI); 
            transform.localScale = Vector3.Lerp(originalScale, targetScale, swell);
            
            // Shockwave Logic
            if (shockwave != null)
            {
                if (isProceduralShockwave && sr != null)
                {
                    // Expand and Fade (Standard White Circle)
                    float waveScale = Mathf.Lerp(0.5f, maxShockwaveScale, t);
                    shockwave.transform.localScale = Vector3.one * waveScale;
                    
                    float alpha = Mathf.Lerp(0.8f, 0f, t);
                    sr.color = new Color(1f, 1f, 1f, alpha);
                }
                else
                {
                     // CUSTOM PREFAB Scaling
                     // Use shockwaveSwellAmount as multiplier (like FlashBomb)
                     // Target Scale = Original * SwellAmount
                     // Lerp from Original to Target
                     Vector3 targetSwell = originalShockwaveScale * shockwaveSwellAmount;
                     shockwave.transform.localScale = Vector3.Lerp(originalShockwaveScale, targetSwell, t);
                     
                     // No alpha change
                }
            }
            
            yield return null;
        }
        
        transform.localScale = originalScale;
        if (shockwave != null) Destroy(shockwave);
    }
    
    private Sprite CreateCircleSprite()
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

    void Explode()
    {
        Debug.Log("BomberEnemy: EXPLODING!");
        
        // 1. Camera Shake (Exact same as FlashBomb)
        if (CameraController.Instance != null)
        {
            StartCoroutine(CameraShake());
        }

        // 2. Custom Explosion Logic (Exact same knockback as FlashBomb)
        ApplyBomberExplosionImpact();
    }
    
    // Adapted from FlashBomb.ApplyExplosionImpact
    void ApplyBomberExplosionImpact()
    {
        // Use the Bomber's radius
        float currentExplosionRadius = explosionRadius; 

        Collider2D[] hits = Physics2D.OverlapCircleAll(transform.position, currentExplosionRadius);
        Debug.Log($"BomberEnemy: Found {hits.Length} objects in blast radius ({currentExplosionRadius})");
        
        foreach (Collider2D hit in hits)
        {
            // Ignore ourselves
            if (hit.gameObject == gameObject) continue;
            
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
            
            // Check for enemy (Friendly Fire!)
            EnemyAI enemy = hit.GetComponent<EnemyAI>();
            if (enemy == null) enemy = hit.GetComponentInParent<EnemyAI>();
            
            if (enemy != null && enemy != this) // Don't damage self (we die anyway)
            {
                // Deal damage (Using Bomber's enemyDamage setting)
                enemy.TakeDamage(enemyDamage);
                
                // Apply knockback (FlashBomb: 20f * distancePercent)
                float knockbackForce = 20f * distancePercent; 
                enemy.ApplyKnockback(direction * knockbackForce, 0.4f);
            }
            
            // Check for player
            PlayerHealth player = hit.GetComponent<PlayerHealth>();
            if (player != null)
            {
                // Player takes damage (Using Bomber's playerDamage setting)
                // We pass direction for the damage visual
                player.TakeDamage(playerDamage, direction);
                
                // Explicitly apply strong bomb knockback to player (FlashBomb: 8f * distancePercent)
                PlayerMovement pm = player.GetComponent<PlayerMovement>();
                if (pm != null)
                {
                    float playerKnockbackForce = 8f * distancePercent; 
                    pm.ApplyKnockback(direction * playerKnockbackForce, 0.3f); 
                }
            }
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
    
    // ... OnEnemyUpdate logic remains ...
    protected override void OnEnemyUpdate()
    {
        if (isExploding) return;

        // Trigger explosion if VERY close
        if (target != null)
        {
            float distanceToPlayer = Vector2.Distance(transform.position, target.position);
            
            // Trigger explosion if VERY close (reduced from radius-based to fixed close range)
            if (distanceToPlayer < 1.2f) 
            {
                StartCoroutine(ExplodeSequence());
            }
        }
    }
}
