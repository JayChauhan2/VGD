using UnityEngine;
using System.Collections;

/// <summary>
/// Handles the death animation effect for enemies.
/// Plays an explosion and spawns a ghost sprite that floats upward in a sine wave while fading out.
/// Total duration: ~0.5 seconds for a quick, snappy effect.
/// </summary>
public class EnemyDeathEffect : MonoBehaviour
{
    [Header("Explosion Settings")]
    [Tooltip("Optional prefab with explosion animation. If null, uses procedural effect.")]
    public GameObject explosionPrefab;
    public float explosionDuration = 0.25f;
    public float explosionScale = 1.0f;
    
    [Header("Ghost Settings")]
    [Tooltip("Optional ghost sprite. If null, uses the enemy's sprite.")]
    public Sprite ghostSprite;
    public float ghostRiseSpeed = 2.5f;        // Units per second upward
    public float ghostDuration = 0.5f;         // Total float time
    public float sineWaveAmplitude = 0.3f;     // How far left/right (smaller for quick effect)
    public float sineWaveFrequency = 4.5f;     // Cycles per second (faster wobble)
    public Color ghostTint = new Color(0.8f, 0.8f, 1f, 1f); // Slight blue tint
    
    [Header("Sorting")]
    public string sortingLayer = "Object";
    public int explosionSortingOrder = 15;
    public int ghostSortingOrder = 20;
    
    /// <summary>
    /// Static method to easily play death effect from anywhere.
    /// </summary>
    public static void PlayDeathEffect(Vector3 position, Sprite enemySprite = null)
    {
        // Create temporary GameObject to run the effect
        GameObject effectObj = new GameObject("EnemyDeathEffect");
        effectObj.transform.position = position;
        
        EnemyDeathEffect effect = effectObj.AddComponent<EnemyDeathEffect>();
        effect.ghostSprite = enemySprite; // Use enemy's sprite if provided
        effect.StartEffect();
    }
    
    private void StartEffect()
    {
        StartCoroutine(PlayEffectSequence());
    }
    
    private IEnumerator PlayEffectSequence()
    {
        // 1. Spawn Explosion
        GameObject explosion = SpawnExplosion();
        
        // 2. Spawn Ghost (starts immediately, overlaps with explosion)
        GameObject ghost = SpawnGhost();
        
        // 3. Animate Ghost
        if (ghost != null)
        {
            yield return StartCoroutine(AnimateGhost(ghost));
        }
        
        // 4. Cleanup
        if (explosion != null) Destroy(explosion);
        Destroy(gameObject); // Destroy the effect controller
    }
    
    private GameObject SpawnExplosion()
    {
        GameObject explosion;
        
        if (explosionPrefab != null)
        {
            // Use provided prefab
            explosion = Instantiate(explosionPrefab, transform.position, Quaternion.identity);
            explosion.transform.localScale = Vector3.one * explosionScale;
            
            // Auto-destroy after duration
            Destroy(explosion, explosionDuration);
        }
        else
        {
            // Create procedural explosion effect
            explosion = CreateProceduralExplosion();
        }
        
        // Ensure proper sorting
        SpriteRenderer sr = explosion.GetComponent<SpriteRenderer>();
        if (sr != null)
        {
            sr.sortingLayerName = sortingLayer;
            sr.sortingOrder = explosionSortingOrder;
        }
        
        return explosion;
    }
    
    private GameObject CreateProceduralExplosion()
    {
        // Simple expanding circle effect as fallback
        GameObject explosion = new GameObject("ProceduralExplosion");
        explosion.transform.position = transform.position;
        
        SpriteRenderer sr = explosion.AddComponent<SpriteRenderer>();
        sr.sprite = CreateCircleSprite();
        sr.color = new Color(1f, 0.6f, 0.2f, 0.8f); // Orange
        sr.sortingLayerName = sortingLayer;
        sr.sortingOrder = explosionSortingOrder;
        
        // Animate scale and fade
        StartCoroutine(AnimateProceduralExplosion(explosion, sr));
        
        return explosion;
    }
    
    private IEnumerator AnimateProceduralExplosion(GameObject explosion, SpriteRenderer sr)
    {
        float elapsed = 0f;
        Vector3 startScale = Vector3.one * 0.3f;
        Vector3 endScale = Vector3.one * 1.5f;
        Color startColor = sr.color;
        Color endColor = new Color(startColor.r, startColor.g, startColor.b, 0f);
        
        while (elapsed < explosionDuration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / explosionDuration;
            
            explosion.transform.localScale = Vector3.Lerp(startScale, endScale, t);
            sr.color = Color.Lerp(startColor, endColor, t);
            
            yield return null;
        }
        
        Destroy(explosion);
    }
    
    private GameObject SpawnGhost()
    {
        GameObject ghost = new GameObject("Ghost");
        ghost.transform.position = transform.position;
        
        SpriteRenderer sr = ghost.AddComponent<SpriteRenderer>();
        sr.sprite = ghostSprite; // Will be enemy sprite or custom ghost sprite
        
        if (sr.sprite == null)
        {
            // Fallback: create a simple ghost sprite
            sr.sprite = CreateGhostSprite();
        }
        
        sr.color = ghostTint;
        sr.sortingLayerName = sortingLayer;
        sr.sortingOrder = ghostSortingOrder;
        
        // Slightly smaller than original
        ghost.transform.localScale = Vector3.one * 0.8f;
        
        return ghost;
    }
    
    private IEnumerator AnimateGhost(GameObject ghost)
    {
        SpriteRenderer sr = ghost.GetComponent<SpriteRenderer>();
        Vector3 startPos = ghost.transform.position;
        Color startColor = sr.color;
        Color endColor = new Color(startColor.r, startColor.g, startColor.b, 0f); // Fade to transparent
        
        float elapsed = 0f;
        
        while (elapsed < ghostDuration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / ghostDuration;
            
            // Upward movement
            float yOffset = ghostRiseSpeed * elapsed;
            
            // Sine wave horizontal movement
            float xOffset = Mathf.Sin(elapsed * sineWaveFrequency * Mathf.PI * 2f) * sineWaveAmplitude;
            
            ghost.transform.position = startPos + new Vector3(xOffset, yOffset, 0f);
            
            // Fade out
            sr.color = Color.Lerp(startColor, endColor, t);
            
            yield return null;
        }
        
        Destroy(ghost);
    }
    
    // Helper: Create a simple circle sprite
    private Sprite CreateCircleSprite()
    {
        int size = 32;
        Texture2D texture = new Texture2D(size, size);
        Color[] pixels = new Color[size * size];
        Vector2 center = new Vector2(size / 2f, size / 2f);
        float radius = size / 2f - 2;
        
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
    
    // Helper: Create a simple ghost sprite (wavy shape)
    private Sprite CreateGhostSprite()
    {
        int size = 32;
        Texture2D texture = new Texture2D(size, size);
        Color[] pixels = new Color[size * size];
        
        // Simple ghost shape: rounded top, wavy bottom
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float normalizedX = (x - size / 2f) / (size / 2f);
                float normalizedY = (y - size / 2f) / (size / 2f);
                
                // Circle for head
                bool inHead = (normalizedX * normalizedX + normalizedY * normalizedY) < 0.8f && normalizedY > -0.2f;
                
                // Wavy bottom
                float wave = Mathf.Sin(normalizedX * Mathf.PI * 3f) * 0.15f;
                bool inBody = Mathf.Abs(normalizedX) < 0.6f && normalizedY < 0.2f && normalizedY > -0.8f + wave;
                
                pixels[y * size + x] = (inHead || inBody) ? Color.white : Color.clear;
            }
        }
        
        texture.SetPixels(pixels);
        texture.Apply();
        return Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
    }
}
