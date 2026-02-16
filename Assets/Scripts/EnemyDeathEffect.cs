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
    public float explosionDuration = 0.5f;
    public float explosionScale = 1.0f;
    
    [Header("Ghost Settings")]
    [Tooltip("Optional ghost sprite. If null, uses the enemy's sprite.")]
    public Sprite ghostSprite;
    
    [Range(0.1f, 2.0f)]
    public float ghostScale = 0.4f;           // Scale of ghost sprite
    
    public float ghostRiseSpeed = 2.5f;        // Units per second upward
    [Tooltip("Note: This is overridden to match explosionDuration so they end together")]
    public float ghostDuration = 0.5f;         // Total float time
    public float sineWaveAmplitude = 0.15f;    // Reduced from 0.3 (less wobble width)
    public float sineWaveFrequency = 2.5f;     // Reduced from 4.5 (slower wobble)
    public Color ghostTint = new Color(0.8f, 0.8f, 1f, 1f); // Slight blue tint
    
    [Header("Sorting")]
    public string sortingLayer = "Object";
    public int explosionSortingOrder = 15;
    public int ghostSortingOrder = 20;
    
    /// <summary>
    /// Static method to easily play death effect from anywhere.
    /// </summary>
    public static void PlayDeathEffect(Vector3 position, Sprite enemySprite = null, float enemyScale = 1.0f, Room room = null)
    {
        // 25% Chance to become a ghost
        // If successful:
        // 1. Force use of Ghost Sprite in animation (ignore enemySprite)
        // 2. Register ghost with Room for future spawning
        bool isGhostSuccess = Random.value < 0.25f;
        
        if (isGhostSuccess)
        {
            // If it's a ghost death, we DON'T use the enemy sprite at all.
            // We rely on LoadSettingsFromAsset to pick up the default ghost sprite.
            enemySprite = null; 
            
            if (room != null)
            {
                room.AddGhost(position);
            }
        }

        // Create temporary GameObject to run the effect
        GameObject effectObj = new GameObject("EnemyDeathEffect");
        effectObj.transform.position = position;
        
        EnemyDeathEffect effect = effectObj.AddComponent<EnemyDeathEffect>();
        effect.baseScale = enemyScale; // Store enemy scale
        
        // If it was a ghost success, we want to ensure we use the ghost sprite.
        // If it was NOT a success, we pass the enemy sprite as fallback.
        // But LoadSettingsFromAsset prioritizes Settings.ghostSprite if it exists!
        // We need to tell the instance to IGNORE the settings ghost sprite if we failed the roll?
        // OR, the user wants "Ghost Sprite will show for their death animation" ONLY if successful.
        // If not successful, "start of the enemy sprite floating up".
        
        effect.isGhostDeath = isGhostSuccess;
        effect.fallbackEnemySprite = enemySprite;
        
        effect.StartEffect();
    }
    
    private float baseScale = 1.0f; 
    private bool isGhostDeath = false;
    private Sprite fallbackEnemySprite;

    private void StartEffect()
    {
        StartCoroutine(PlayEffectSequence());
    }
    
    private void LoadSettingsFromAsset()
    {
        if (EnemyDeathSettings.Instance != null)
        {
            var settings = EnemyDeathSettings.Instance;
            
            if (explosionPrefab == null) explosionPrefab = settings.explosionPrefab;
            
            // LOGIC CHANGE:
            // If isGhostDeath == true -> Use Settings Ghost Sprite
            // If isGhostDeath == false -> Use fallbackEnemySprite (the enemy's actual sprite)
            
            if (isGhostDeath)
            {
                // Use the cool ghost sprite
                if (settings.ghostSprite != null) ghostSprite = settings.ghostSprite;
            }
            else
            {
                // Use the enemy's own sprite (boring death)
                ghostSprite = fallbackEnemySprite;
            }
            
            // ... rest of settings ...
            
            explosionDuration = settings.explosionDuration;
            // Multiply settings scale by enemy scale
            explosionScale = settings.explosionScale * baseScale;
            ghostScale = settings.ghostScale * baseScale;
            
            ghostRiseSpeed = settings.ghostRiseSpeed;
            ghostDuration = settings.ghostDuration;
            sineWaveAmplitude = settings.sineWaveAmplitude;
            sineWaveFrequency = settings.sineWaveFrequency;
            ghostTint = settings.ghostTint;
        }
    }
    
    private IEnumerator PlayEffectSequence()
    {
        // Load settings from ScriptableObject if available
        LoadSettingsFromAsset();
        
        // 1. Spawn Explosion (it handles its own destruction)
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
        
        // Use scale from settings
        ghost.transform.localScale = Vector3.one * ghostScale;
        
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
