using UnityEngine;
using System.Collections;

public class BomberEnemy : EnemyAI
{
    [Header("Bomber Settings")]
    public float explosionRadius = 3f;
    public float playerDamage = 30f;
    public float enemyDamage = 60f;
    public float explosionDelay = 0.5f; // Time to wait for animation before exploding
    
    private bool isExploding = false;

    protected override void OnEnemyStart()
    {
        maxHealth = 60f;
        currentHealth = maxHealth;
        // speed = 3.5f; // Removed hardcoded speed to allow Inspector control
        
        Debug.Log("BomberEnemy: Initialized - will explode on death!");
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

    protected override void Die()
    {
        if (isExploding) return; // Already dying/exploding
        
        StartCoroutine(ExplodeSequence());
    }
    
    // Override TakeDamage to ignore damage while exploding
    public override void TakeDamage(float damage)
    {
        if (isExploding) return;
        base.TakeDamage(damage);
    }

    private IEnumerator ExplodeSequence()
    {
        isExploding = true;
        
        // Stop movement
        speed = 0f;
        if (rb != null) rb.linearVelocity = Vector2.zero;
        if (pathfinding != null) pathfinding = null; // Stop path updates
        
        float waitTime = explosionDelay; // Default fallback

        // Trigger Animation
        if (animator != null)
        {
            animator.SetTrigger("Explode");
            
            // Wait a frame for the Animator to process the trigger and start transition
            yield return null; 
            
            // Get the duration of the new state
            AnimatorStateInfo info = animator.GetCurrentAnimatorStateInfo(0);
            if (animator.IsInTransition(0))
            {
                info = animator.GetNextAnimatorStateInfo(0);
            }
            
            if (info.length > 0)
            {
                waitTime = info.length;
            }
        }
        
        Debug.Log($"BomberEnemy: specific sequence started. Exploding in {waitTime}s (Animation Sync)...");
        
        // Wait for first half of animation
        float halfDuration = waitTime * 0.5f;
        yield return new WaitForSeconds(halfDuration);
        
        // --- Visual Effects Trigger ---
        StartCoroutine(PlayVisualEffects(0.3f)); // Duration for effects

        // Wait for remainder
        yield return new WaitForSeconds(waitTime - halfDuration);
        
        // BOOM
        Explode();
        
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
        
        // 2. White Shockwave Circle
        GameObject shockwave = new GameObject("Shockwave");
        shockwave.transform.position = transform.position;
        SpriteRenderer sr = shockwave.AddComponent<SpriteRenderer>();
        sr.sprite = CreateCircleSprite();
        sr.color = new Color(1f, 1f, 1f, 0.8f);
        sr.sortingLayerName = "Object";
        sr.sortingOrder = 10;
        shockwave.transform.localScale = Vector3.one * 0.5f;
        
        float maxShockwaveScale = 4.0f; // Similar to FlashBomb
        
        // Safety: Ensure shockwave is destroyed even if this coroutine stops (e.g. enemy dies)
        Destroy(shockwave, duration + 0.1f);

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;
            
            // Swell: Up then Down (Sin curve 0->1->0)
            // sin(0) = 0, sin(pi) = 0. We want 0 to PI.
            float swell = Mathf.Sin(t * Mathf.PI); 
            transform.localScale = Vector3.Lerp(originalScale, targetScale, swell);
            
            // Shockwave: Expand and Fade
            float waveScale = Mathf.Lerp(0.5f, maxShockwaveScale, t);
            shockwave.transform.localScale = Vector3.one * waveScale;
            
            float alpha = Mathf.Lerp(0.8f, 0f, t);
            sr.color = new Color(1f, 1f, 1f, alpha);
            
            yield return null;
        }
        
        transform.localScale = originalScale;
        Destroy(shockwave);
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
        ExplosionEffect.CreateExplosion(transform.position, explosionRadius, playerDamage, enemyDamage, true);
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
