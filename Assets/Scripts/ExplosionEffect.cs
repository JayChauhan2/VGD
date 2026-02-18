using UnityEngine;
using System.Collections.Generic;

public class ExplosionEffect : MonoBehaviour
{
    [Header("Explosion Settings")]
    public float explosionRadius = 3f;
    public float playerDamage = 25f;
    public float enemyDamage = 50f;
    public float visualDuration = 0.5f;
    public bool ignoreBombers = false; // New flag
    public GameObject sourceToIgnore; // Entity to NOT damage (e.g. the bomber that exploded)
    
    private LineRenderer circleRenderer;
    private float spawnTime;

    void Start()
    {
        spawnTime = Time.time;
        
        // Create visual effect
        CreateVisualEffect();
        
        // Apply damage
        ApplyExplosionDamage();
        
        // Destroy after visual duration
        Destroy(gameObject, visualDuration);
    }

    void CreateVisualEffect()
    {
        // Create circle visual using LineRenderer
        circleRenderer = gameObject.AddComponent<LineRenderer>();
        circleRenderer.useWorldSpace = false;
        circleRenderer.startWidth = 0.1f;
        circleRenderer.endWidth = 0.1f;
        circleRenderer.positionCount = 50;
        
        // Set color (orange/red for explosion)
        circleRenderer.startColor = new Color(1f, 0.5f, 0f, 0.8f);
        circleRenderer.endColor = new Color(1f, 0.2f, 0f, 0.8f);
        
        // Draw circle
        float angleStep = 360f / 50;
        for (int i = 0; i < 50; i++)
        {
            float angle = i * angleStep * Mathf.Deg2Rad;
            float x = Mathf.Cos(angle) * explosionRadius;
            float y = Mathf.Sin(angle) * explosionRadius;
            circleRenderer.SetPosition(i, new Vector3(x, y, 0));
        }
    }

    void Update()
    {
        // Fade out over time
        if (circleRenderer != null)
        {
            float alpha = 1f - ((Time.time - spawnTime) / visualDuration);
            Color startColor = circleRenderer.startColor;
            Color endColor = circleRenderer.endColor;
            startColor.a = alpha * 0.8f;
            endColor.a = alpha * 0.8f;
            circleRenderer.startColor = startColor;
            circleRenderer.endColor = endColor;
        }
    }

    void ApplyExplosionDamage()
    {
        // Find all colliders in explosion radius
        Collider2D[] hitColliders = Physics2D.OverlapCircleAll(transform.position, explosionRadius);
        
        // Track unique entities to prevent double damage from multiple colliders on same object
        HashSet<GameObject> processedEntities = new HashSet<GameObject>();
        
        foreach (Collider2D hitCollider in hitColliders)
        {
            // Damage player
            if (hitCollider.CompareTag("Player"))
            {
                if (processedEntities.Contains(hitCollider.gameObject)) continue;
                if (sourceToIgnore != null && hitCollider.gameObject == sourceToIgnore) continue;

                processedEntities.Add(hitCollider.gameObject);

                PlayerHealth playerHealth = hitCollider.GetComponent<PlayerHealth>();
                if (playerHealth != null)
                {
                    Vector2 knockbackDir = (hitCollider.transform.position - transform.position).normalized;
                    playerHealth.TakeDamage(playerDamage, knockbackDir);
                }
            }
            
            // Damage other enemies
            // Use GetComponentInParent to handle child colliders
            EnemyAI enemy = hitCollider.GetComponentInParent<EnemyAI>();
            if (enemy != null)
            {
                if (processedEntities.Contains(enemy.gameObject)) continue;
                if (sourceToIgnore != null && enemy.gameObject == sourceToIgnore) continue;

                processedEntities.Add(enemy.gameObject);

                // Skip if this is a Bomber and we should ignore them
                if (ignoreBombers && enemy is BomberEnemy)
                {
                    continue;
                }
                
                // Reduce damage for Bombers to prevent chain reaction wipes
                float finalDamage = enemyDamage;
                if (enemy is BomberEnemy)
                {
                    finalDamage *= 0.5f; 
                }

                enemy.TakeDamage(finalDamage);
            }


            // Damage Breakable Boxes
            BreakableBox box = hitCollider.GetComponent<BreakableBox>();
            if (box != null)
            {
                if (processedEntities.Contains(box.gameObject)) continue;
                processedEntities.Add(box.gameObject);
                
                // Boxes take full enemy damage from explosions
                box.TakeDamage(enemyDamage, (hitCollider.transform.position - transform.position).normalized);
            }
        }
        
        Debug.Log($"Explosion at {transform.position} hit {hitColliders.Length} colliders ({processedEntities.Count} unique entities)");
    }

    // Static helper method to create explosion
    public static void CreateExplosion(Vector3 position, float radius = 3f, float playerDmg = 25f, float enemyDmg = 50f, bool ignoreBombers = false, GameObject source = null)
    {
        GameObject explosionObj = new GameObject("Explosion");
        explosionObj.transform.position = position;
        
        ExplosionEffect explosion = explosionObj.AddComponent<ExplosionEffect>();
        explosion.explosionRadius = radius;
        explosion.playerDamage = playerDmg;
        explosion.enemyDamage = enemyDmg;
        explosion.ignoreBombers = ignoreBombers;
        explosion.sourceToIgnore = source;
    }
}
