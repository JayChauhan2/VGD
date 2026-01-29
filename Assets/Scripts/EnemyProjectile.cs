using UnityEngine;

public class EnemyProjectile : MonoBehaviour
{
    [Header("Projectile Settings")]
    public float speed = 8f;
    public float damage = 10f;
    public float lifetime = 5f;
    
    private Vector2 direction;
    private float spawnTime;

    public void Initialize(Vector2 dir, float projectileSpeed, float projectileDamage)
    {
        direction = dir.normalized;
        speed = projectileSpeed;
        damage = projectileDamage;
        spawnTime = Time.time;
    }

    void Update()
    {
        // Move projectile
        transform.position += (Vector3)direction * speed * Time.deltaTime;
        
        // Destroy after lifetime
        if (Time.time - spawnTime > lifetime)
        {
            Destroy(gameObject);
        }
    }

    void OnTriggerEnter2D(Collider2D collision)
    {
        // Damage player
        if (collision.CompareTag("Player"))
        {
            PlayerHealth playerHealth = collision.GetComponent<PlayerHealth>();
            if (playerHealth != null)
            {
                Vector2 knockbackDir = direction;
                playerHealth.TakeDamage(damage, knockbackDir);
            }
            Destroy(gameObject);
        }
        
        // Destroy on walls
        if (collision.CompareTag("Wall") || collision.gameObject.layer == LayerMask.NameToLayer("Wall"))
        {
            Destroy(gameObject);
        }
    }
}
