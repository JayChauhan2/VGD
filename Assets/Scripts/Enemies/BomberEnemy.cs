using UnityEngine;

public class BomberEnemy : EnemyAI
{
    [Header("Bomber Settings")]
    public float explosionRadius = 3f;
    public float playerDamage = 30f;
    public float enemyDamage = 60f;
    
    private bool hasExploded = false;

    protected override void OnEnemyStart()
    {
        maxHealth = 60f;
        currentHealth = maxHealth;
        speed = 3.5f;
        
        Debug.Log("BomberEnemy: Initialized - will explode on death!");
    }

    protected override void OnEnemyDeath()
    {
        if (!hasExploded)
        {
            Explode();
            hasExploded = true;
        }
    }

    void Explode()
    {
        Debug.Log("BomberEnemy: EXPLODING!");
        
        // Create explosion effect
        ExplosionEffect.CreateExplosion(transform.position, explosionRadius, playerDamage, enemyDamage);
    }

    protected override void OnEnemyUpdate()
    {
        // Visual indicator - pulse red when close to player
        if (target != null)
        {
            float distanceToPlayer = Vector2.Distance(transform.position, target.position);
            
            if (distanceToPlayer < 2f)
            {
                // Pulse warning
                SpriteRenderer sr = GetComponent<SpriteRenderer>();
                if (sr != null)
                {
                    float pulse = Mathf.PingPong(Time.time * 5f, 1f);
                    sr.color = Color.Lerp(new Color(1f, 0.5f, 0f), Color.red, pulse);
                }
            }
        }
    }
}
