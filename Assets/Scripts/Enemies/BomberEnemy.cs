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
        Vector3 targetScale = originalScale * 1.5f;
        float elapsed = 0f;
        
        // 2. White Shockwave Circle (Procedural OR Prefab)
        GameObject shockwave = null;
        SpriteRenderer sr = null;
        Animator shockwaveAnim = null;
        bool isProceduralShockwave = false;
        
        if (showShockwave)
        {
            if (explosionEffectPrefab != null)
            {
                // Instantiate Custom Prefab
                shockwave = Instantiate(explosionEffectPrefab, transform.position, Quaternion.identity);
                sr = shockwave.GetComponent<SpriteRenderer>(); 
                shockwaveAnim = shockwave.GetComponent<Animator>();
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
        
        // Calculate scale to match explosion radius
        // Sprite is 64x64. At 100 PPU, world size is 0.64.
        // We want world size = explosionRadius * 2 (diameter).
        // scale * 0.64 = explosionRadius * 2  =>  scale = (explosionRadius * 2) / 0.64
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
                // Only scale/fade the procedural shockwave
                // If it's a custom prefab, we let its internal animation (if any) handle things, 
                // OR we could scale it if desired, but user asked for "transparency 100%" specifically.
                // Let's assume custom prefab wants to handle its own Transform animation (FlashBomb does).
                // But FlashBomb DOES execute scaling ("Swell") on the custom instance.
                // If the user wants it to look like an explosion, maybe we SHOULD scale it?
                // But definitely DON'T touch alpha.
                
                if (isProceduralShockwave && sr != null)
                {
                    // Expand and Fade
                    float waveScale = Mathf.Lerp(0.5f, maxShockwaveScale, t);
                    shockwave.transform.localScale = Vector3.one * waveScale;
                    
                    float alpha = Mathf.Lerp(0.8f, 0f, t);
                    sr.color = new Color(1f, 1f, 1f, alpha);
                }
                else
                {
                     // For custom prefab: 
                     // If it's meant to be the "explosion", it should probably expand?
                     // Let's expand it to match the explosion radius too, but WITHOUT fade.
                     // UNLESS the prefab already handles its own size (like ParticleSystem).
                     // FlashBomb scales custom prefab. Let's replicate that behavior.
                     
                     // Check if we should scale it? Assuming yes for consistency with "Explosion Radius".
                     float waveScale = Mathf.Lerp(0.5f, maxShockwaveScale, t);
                     shockwave.transform.localScale = Vector3.one * waveScale;
                     
                     // DO NOT TOUCH COLOR/ALPHA
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
        
        // Create explosion effect that IGNORES other bombers to prevent chain reactions
        // UPDATE: User requested to damage other entities, so we enable friendly fire for chain reactions!
        // We pass 'gameObject' as the source to ignore so we don't kill OURSELVES instantly.
        ExplosionEffect.CreateExplosion(transform.position, explosionRadius, playerDamage, enemyDamage, false, gameObject);
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
