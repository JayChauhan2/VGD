using UnityEngine;

public class EnemyProjectile : MonoBehaviour
{
    [Header("Projectile Settings")]
    public float speed = 8f;
    public float damage = 10f;
    public float lifetime = 5f;
    
    private Rigidbody2D rb;
    private Vector2 direction;
    private float spawnTime;

    public void Initialize(Vector2 dir, float projectileSpeed, float projectileDamage)
    {
        direction = dir.normalized;
        speed = projectileSpeed;
        damage = projectileDamage;
        spawnTime = Time.time;
        
        rb = GetComponent<Rigidbody2D>();
        if (rb != null)
        {
             // Use physics for movement to ensure reliable collision detection
             rb.linearVelocity = direction * speed;
        }
    }

    void Update()
    {
        // If we have a RB, we let physics handle movement.
        // If not, we do manual movement (fallback).
        if (rb == null)
        {
            transform.position += (Vector3)direction * speed * Time.deltaTime;
        }
        
        // Destroy after lifetime
        if (Time.time - spawnTime > lifetime)
        {
            Destroy(gameObject);
        }
    }

    private GameObject owner;

    public void SetOwner(GameObject ownerObject)
    {
        this.owner = ownerObject;
    }

    void OnTriggerEnter2D(Collider2D collision)
    {
        if (collision == null) return;
        
        // Ignore collision with the shooter (owner)
        if (owner != null && collision.gameObject == owner) return;
        
        // Debug.Log($"EnemyProjectile hit: {collision.gameObject.name} (Layer: {LayerMask.LayerToName(collision.gameObject.layer)})");

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
            return;
        }

        // Damage other enemies (Friendly Fire)
        EnemyAI enemy = collision.GetComponent<EnemyAI>();
        if (enemy != null)
        {
            enemy.TakeDamage(damage);
            Destroy(gameObject); // Destroy bullet on impact
            return;
        }
        


        // Damage Breakable Boxes
        BreakableBox box = collision.GetComponent<BreakableBox>();
        if (box != null)
        {
            box.TakeDamage(damage, direction);
            Destroy(gameObject);
            return;
        }
        
        // Destroy on walls (Obstacle Layer)
        if (collision.gameObject.layer == LayerMask.NameToLayer("Obstacle"))
        {
            Destroy(gameObject);
            return;
        }
        
        // Explicitly check for "Default" layer if that's what walls are on (just in case)
        if (collision.gameObject.layer == 0) // Default
        {
             // Optional: Destroy on default layer objects that aren't the enemy itself?
             // But the enemy itself might be on Default. 
             // We configured IgnoreCollision in SpitterEnemy, so we should be safe.
             // Let's not destroy on Default indiscriminately yet.
        }
    }
}
