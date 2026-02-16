using UnityEngine;
using System.Collections;

public class TeleporterEnemy : EnemyAI
{
    [Header("Teleporter Settings")]
    public float minTeleportDistance = 3f;
    public float maxTeleportDistance = 8f;
    public float timeToAim = 1.0f;       // Time visible before shooting
    public float minTimeAfterShot = 0.1f;
    public float maxTimeAfterShot = 0.5f;
    public float hideDuration = 2.0f;    // Time invisible
    
    [Header("Projectile Settings")]
    public float projectileSpeed = 7f;
    public float projectileDamage = 10f;
    public GameObject projectilePrefab;
    
    // Internal State
    private bool isHidden = false;
    private Coroutine behaviorCoroutine;
    private Coroutine recoveryCoroutine;
    
    // References
    private Collider2D[] myColliders;
    private SpriteRenderer[] allRenderers;

    private void OnEnable()
    {
        // Restart behavior if re-enabled (and not dead)
        if (currentHealth > 0)
        {
             // Force a fresh start.
             behaviorCoroutine = null;
             recoveryCoroutine = null;
             StartBehavior();
        }
    }

    private void OnDisable()
    {
        // When disabled, Unity stops all coroutines on this MonoBehaviour.
        // We MUST clear our references so OnEnable knows they are gone.
        behaviorCoroutine = null;
        recoveryCoroutine = null;
    }

    protected override void OnEnemyStart()
    {
        maxHealth = 40f; 
        currentHealth = maxHealth;
        speed = 0f; 
        
        spriteRenderer = GetComponent<SpriteRenderer>();
        // Grab ALL renderers (body + beak + whatever else)
        allRenderers = GetComponentsInChildren<SpriteRenderer>();
        
        myColliders = GetComponentsInChildren<Collider2D>();
        
        if (projectilePrefab == null)
        {
            projectilePrefab = CreateProjectilePrefab();
        }
        
        // OPTIMIZATION: Teleporter Enemy does not need pathfinding.
        // It teleports instead of walking. This prevents heavy calculations.
        usePathfinding = false;
        
        StartBehavior();
        
        Debug.Log("TeleporterEnemy: Initialized with Whack-a-Mole behavior");
    }

    void StartBehavior()
    {
        StopAllBehaviorCoroutines();
        behaviorCoroutine = StartCoroutine(BehaviorLoop());
    }

    void StopAllBehaviorCoroutines()
    {
        if (behaviorCoroutine != null) StopCoroutine(behaviorCoroutine);
        if (recoveryCoroutine != null) StopCoroutine(recoveryCoroutine);
        behaviorCoroutine = null;
        recoveryCoroutine = null;
    }

    // Watchdog
    private float hiddenTimer = 0f;

    // Override Update to disable default movement logic from EnemyAI
    protected override void OnEnemyUpdate()
    {
        // No movement logic needed here
        
        // WATCHDOG: If hidden for too long (stuck?), force appear
        if (isHidden)
        {
            hiddenTimer += Time.deltaTime;
            if (hiddenTimer > 3.0f) // Max expected hide time ~2s
            {
                Debug.LogWarning("TeleporterEnemy: Watchdog triggered! Force appearing stuck enemy.");
                Appear();
                hiddenTimer = 0f;
                // Restart behavior just in case
                if (behaviorCoroutine == null) StartBehavior();
            }
        }
        else
        {
            hiddenTimer = 0f;
            
            // Look at player ONLY when visible
            if (target != null && currentHealth > 0)
            {
                float dirX = target.position.x - transform.position.x;
                if (dirX > 0.1f)
                {
                    // Look Right
                    transform.rotation = Quaternion.Euler(0, 0, 0);
                }
                else if (dirX < -0.1f)
                {
                    // Look Left
                    transform.rotation = Quaternion.Euler(0, 180, 0);
                }
            }
        }
    }

    IEnumerator BehaviorLoop()
    {
        // Initial start delay
        yield return new WaitForSeconds(1.0f);

        while (true)
        {
            // 1. Appear near player
            Appear();
            
            // 2. Wait/Aim
            yield return new WaitForSeconds(timeToAim);
            
            // 3. Shoot (if still alive and target valid)
            if (currentHealth > 0 && target != null)
            {
                ShootProjectile();
            }
            
            // 4. Vulnerable window after shot
            yield return new WaitForSeconds(Random.Range(minTimeAfterShot, maxTimeAfterShot));
            
            // 5. Disappear
            Disappear();
            
            // 6. Wait while hidden
            yield return new WaitForSeconds(hideDuration);
        }
    }

    void Appear()
    {
        TeleportNearPlayer();
        SetVisibility(true);
    }

    void Disappear()
    {
        SetVisibility(false);
    }

    void SetVisibility(bool visible)
    {
        isHidden = !visible;
        
        if (allRenderers != null)
        {
            foreach (var sr in allRenderers)
            {
                sr.enabled = visible;
            }
        }
        else if (spriteRenderer != null)
        {
             // Fallback if array not init
             spriteRenderer.enabled = visible;
        }
        
        if (myColliders != null)
        {
            foreach (var col in myColliders)
            {
                col.enabled = visible;
            }
        }
        
        var healthBar = GetComponentInChildren<EnemyHealthBar>();
        if (healthBar != null) healthBar.SetVisibility(visible);
    }

    void TeleportNearPlayer()
    {
        if (target == null) 
        {
            FindPlayer(); 
            if (target == null) return;
        }
        
        Bounds roomBounds = GetRoomBounds();
        Vector3 newPosition = Vector3.zero;
        bool validPosition = false;
        int attempts = 0;
        
        // Try to find a position in a "donut" shape around the player
        while (!validPosition && attempts < 20)
        {
            Vector2 randomDir = Random.insideUnitCircle.normalized;
            float distance = Random.Range(minTeleportDistance, maxTeleportDistance);
            Vector3 candidatePos = target.position + (Vector3)(randomDir * distance);
            candidatePos.z = transform.position.z;
            
            // Check 1: Inside Room
            // Check 2: Not inside a wall (IsPositionValid)
            // Check 3: Line of Sight to Player (New!)
            if (roomBounds.Contains(candidatePos) && IsPositionValid(candidatePos))
            {
                // Linecast checks if there's an obstacle between candidate and target
                RaycastHit2D hit = Physics2D.Linecast(candidatePos, target.position, LayerMask.GetMask("Obstacle"));
                if (hit.collider == null)
                {
                    newPosition = candidatePos;
                    validPosition = true;
                }
            }
            
            attempts++;
        }
        
        // Fallback: Random point in room if donut fails
        if (!validPosition)
        {
             // Even for fallback, try to find a valid one, but relax constraints if needed
             // For now, just pick a random valid spot
             newPosition = new Vector3(
                 Random.Range(roomBounds.min.x + 1f, roomBounds.max.x - 1f),
                 Random.Range(roomBounds.min.y + 1f, roomBounds.max.y - 1f),
                 transform.position.z
             );
        }

        transform.position = newPosition;
    }

    void FindPlayer()
    {
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player != null) target = player.transform;
    }

    public override void TakeDamage(float damage)
    {
        if (isHidden) return; 
        
        base.TakeDamage(damage);
        
        // User requested REMOVAL of reactive teleport.
        // The enemy now simply takes damage and continues its behavior loop until it dies or its timer runs out.
    }

    protected override void OnEnemyDeath()
    {
        Debug.Log("TeleporterEnemy: Dead!");
        StopAllBehaviorCoroutines();
    }

    IEnumerator RecoverFromHit()
    {
        // Wait a bit (simulate retreating/relocating)
        yield return new WaitForSeconds(hideDuration * 0.5f);
        
        // Restart the normal behavior
        behaviorCoroutine = StartCoroutine(BehaviorLoop());
        recoveryCoroutine = null;
    }

    void ShootProjectile()
    {
        if (projectilePrefab == null || target == null) return;
        
        // Recalculate direction right now to be accurate
        Vector2 direction = (target.position - transform.position).normalized;
        
        GameObject projectileObj = Instantiate(projectilePrefab, transform.position, Quaternion.identity);
        
        // ROBUSTNESS FRONT:
        // Ensure the projectile has a Rigidbody and it's set to Dynamic (for velocity to work reliably)
        // This overrides whatever settings might be on the user's prefab
        Rigidbody2D rb = projectileObj.GetComponent<Rigidbody2D>();
        if (rb == null) rb = projectileObj.AddComponent<Rigidbody2D>();
        
        rb.bodyType = RigidbodyType2D.Dynamic;
        rb.gravityScale = 0f;
        rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous; 
        rb.freezeRotation = true;

        // Ensure it has a Collider (Trigger)
        Collider2D col = projectileObj.GetComponent<Collider2D>();
        if (col == null) 
        {
            CircleCollider2D circle = projectileObj.AddComponent<CircleCollider2D>();
            circle.radius = 0.5f;
            circle.isTrigger = true;
        }

        // Ensure logic script exists
        EnemyProjectile projectile = projectileObj.GetComponent<EnemyProjectile>();
        if (projectile == null) projectile = projectileObj.AddComponent<EnemyProjectile>();
        
        // Initialize
        projectile.Initialize(direction, projectileSpeed, projectileDamage);
        projectile.SetOwner(gameObject); // Prevent friendly fire on self

        // VISIBILITY FRONT:
        // Force the sprite to be on the "Object" layer
        SpriteRenderer sr = projectileObj.GetComponent<SpriteRenderer>();
        if (sr != null)
        {
            sr.sortingLayerName = "Object";
            sr.sortingOrder = 10; // High order to be on top of floor/walls
        }
    }

    // Helper to create default projectile if none assigned
    GameObject CreateProjectilePrefab()
    {
        GameObject prefab = new GameObject("TeleporterProjectile_Generated");
        
        SpriteRenderer sr = prefab.AddComponent<SpriteRenderer>();
        sr.sprite = CreateCircleSprite();
        sr.color = new Color(0.6f, 0f, 0.8f); // Purple
        sr.sortingLayerName = "Object"; // Ensure it's on the correct layer
        sr.sortingOrder = 10;
        prefab.transform.localScale = Vector3.one * 0.4f;
        
        // Critical: Needs Collider and Rigidbody for EnemyProjectile.cs to work!
        CircleCollider2D collider = prefab.AddComponent<CircleCollider2D>();
        collider.isTrigger = true;
        collider.radius = 0.5f;
        
        Rigidbody2D rb = prefab.AddComponent<Rigidbody2D>();
        rb.gravityScale = 0;
        rb.bodyType = RigidbodyType2D.Dynamic; // Must be Dynamic for linearVelocity to work reliably without custom physics steps
        rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
        rb.freezeRotation = true;
        
        prefab.AddComponent<EnemyProjectile>();
        
        return prefab;
    }

    Sprite CreateCircleSprite()
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
}
