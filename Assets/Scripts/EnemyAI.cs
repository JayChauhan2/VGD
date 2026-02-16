using UnityEngine;
using System.Collections.Generic;
using System.Collections;

public class EnemyAI : MonoBehaviour
{
    public Transform target;
    public float speed = 3f;
    public float pathUpdateDelay = 0.2f;
    
    [Header("Loot Settings")]
    public GameObject coinPrefab;

    protected Pathfinding pathfinding;
    protected List<Node> path;
    protected int targetIndex;

    public bool IsActive { get; private set; } = false;
    public bool IsKnockedBack { get; private set; } = false; // New flag for knockback state

    public float maxHealth = 100f;
    protected float currentHealth;
    protected Room parentRoom;

    public event System.Action<float, float> OnHealthChanged;

    protected Rigidbody2D rb;

    protected Animator animator;
    protected SpriteRenderer spriteRenderer;

    // Public method to explicitly assign room (e.g. from Spawner)
    public void AssignRoom(Room room)
    {
        parentRoom = room;
        Debug.Log($"EnemyAI: Room explicitly assigned: {(room != null ? room.name : "null")}");
    }

    void Start()
    {
        rb = GetComponent<Rigidbody2D>();
        animator = GetComponent<Animator>();
        spriteRenderer = GetComponent<SpriteRenderer>();
        if (spriteRenderer == null) spriteRenderer = GetComponentInChildren<SpriteRenderer>();

        // Ensure physics settings are correct for collision
        if (rb != null)
        {
            rb.bodyType = RigidbodyType2D.Dynamic;
            rb.constraints = RigidbodyConstraints2D.FreezeRotation;
            rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous; // Helps with getting stuck
        }

        // Auto-add health bar if missing
        if (GetComponent<EnemyHealthBar>() == null)
        {
            gameObject.AddComponent<EnemyHealthBar>();
        }

        currentHealth = maxHealth;
        OnHealthChanged?.Invoke(currentHealth, maxHealth);
        
        // Ensure visibility by forcing the sorting layer
        if (spriteRenderer != null)
        {
            spriteRenderer.sortingLayerName = "Object";
            // Removed hardcoded sortingOrder = 5 to allow dynamic depth sorting
        }
        
        // If room not already assigned (e.g. by Spawner), try to find it
        if (parentRoom == null)
        {
            // 1. Try Hierarchy
            parentRoom = GetComponentInParent<Room>();
            
            // 2. Try Spatial Lookup via RoomManager
            if (parentRoom == null && RoomManager.Instance != null && RoomManager.Instance.IsInitialized())
            {
                parentRoom = RoomManager.Instance.GetRoomAt(transform.position);
            }
            
            // 3. Try Global Room Search (Handle manually placed rooms)
            if (parentRoom == null)
            {
                parentRoom = Room.GetRoomContaining(transform.position);
            }
        }

        if (parentRoom != null)
        {
            Debug.Log($"EnemyAI: Found Room '{parentRoom.name}' and registering myself.");
            parentRoom.RegisterEnemy(this);
        }
        else
        {
            Debug.LogError($"EnemyAI: Could not find ANY Room for enemy at {transform.position}! Tried: Parent, Manager, and Bounds.");
        }

        // Try to find the Player if target not assigned
        if (target == null)
        {
            FindPlayer();
            if (target == null) Debug.LogWarning("EnemyAI: Player with tag 'Player' not found in Start. Will keep searching.");
        }

        CheckPathfinding();
        StartCoroutine(UpdatePath());
        
        // Virtual hook for subclasses
        OnEnemyStart();
    }
    
    // Virtual method for subclasses to override
    protected virtual void OnEnemyStart() { }
    
    void CheckPathfinding()
    {
        pathfinding = FindFirstObjectByType<Pathfinding>(); // Unity 2023+ standard
        // if (pathfinding == null) pathfinding = FindObjectOfType<Pathfinding>(); // Deprecated fix


        if (pathfinding == null) Debug.LogError("EnemyAI: Pathfinding component not found via FindFirstObjectByType or FindObjectOfType!");
        if (target == null) Debug.LogWarning("EnemyAI: Target is null! Enemy will not move.");
    }

    public virtual void TakeDamage(float damage)
    {
        currentHealth -= damage;
        OnHealthChanged?.Invoke(currentHealth, maxHealth);
        Debug.Log($"Enemy Health: {currentHealth}/{maxHealth}");
        
        if (currentHealth <= 0)
        {
            Die();
        }
    }

    protected virtual void Die()
    {
        DropLoot();
        // Virtual hook for subclasses (called before room notification)
        OnEnemyDeath();
        
        if (parentRoom != null)
        {
            Debug.Log($"EnemyAI: Dying, telling Room {parentRoom.name} I am dead.");
            parentRoom.EnemyDefeated(this);
        }
        else
        {
            Debug.LogError("EnemyAI: Dying but I have NO PARENT ROOM! I cannot unlock doors.");
        }
        Destroy(gameObject);
    }
    
    // Virtual method for subclasses to override (e.g., spawn enemies, explode)
    protected virtual void OnEnemyDeath() { }

    IEnumerator UpdatePath()
    {
        while (pathfinding == null || !pathfinding.IsGridReady)
        {
            Debug.LogWarning($"EnemyAI: Waiting for Pathfinding Grid... (Pathfinding Obj: '{(pathfinding != null ? pathfinding.name : "null")}', IsGridReady: {pathfinding?.IsGridReady})");
            yield return new WaitForSeconds(1.0f);
        }

        while (true)
        {
            if (!IsActive || IsKnockedBack) // Pause path updates if knocked back
            {
                yield return new WaitForSeconds(pathUpdateDelay);
                continue;
            }

            // Retry finding player if missing
            if (target == null)
            {
                FindPlayer();
            }

            if (target != null && pathfinding != null && pathfinding.IsGridReady)
            {
                path = pathfinding.FindPath(transform.position, target.position);
                if (path != null && path.Count > 0)
                {
                    targetIndex = 0;
                }
                else
                {
                     Debug.LogWarning($"EnemyAI: Path calculation failed (Result null or empty). TargetPos: {target.position}");
                }
            }
            yield return new WaitForSeconds(pathUpdateDelay);
        }
    }

    void Update()
    {
        if (!IsActive) return;
        
        // suspend movement logic while knocked back so physics can work
        if (IsKnockedBack) return; 
        
        // Virtual hook for subclasses to add custom update logic
        OnEnemyUpdate();
        
        Vector2 velocity = Vector2.zero;

        if (path == null || targetIndex >= path.Count) 
        {
            if (rb != null) rb.linearVelocity = Vector2.zero;
        }
        else
        {
            Vector3 currentWaypoint = path[targetIndex].worldPosition;
            
            // Move towards waypoint using Physics if available
            if (rb != null)
            {
                Vector2 dir = (currentWaypoint - transform.position).normalized;
                velocity = dir * speed;
                rb.linearVelocity = velocity;
            }
            else
            {
                Vector3 nextPosition = Vector3.MoveTowards(transform.position, currentWaypoint, speed * Time.deltaTime);
                velocity = (nextPosition - transform.position) / Time.deltaTime;
                transform.position = nextPosition;
            }

            // Check if close to waypoint
            if (Vector3.Distance(transform.position, currentWaypoint) < 0.1f)
            {
                targetIndex++;
            }
        }
        
        UpdateAnimation(velocity);
        CheckTouchDamage();
    }
    
    protected virtual void UpdateAnimation(Vector2 velocity)
    {
        if (animator != null)
        {
            animator.SetFloat("Speed", velocity.magnitude);
        }

        if (spriteRenderer != null)
        {
             // Dynamic Depth Sorting
             // Multiply Y by -100 to give lower objects higher order (draw on top)
             spriteRenderer.sortingOrder = Mathf.RoundToInt(transform.position.y * -100);

            if (velocity.sqrMagnitude > 0.01f)
            {
                if (velocity.x < -0.01f) spriteRenderer.flipX = true;
                else if (velocity.x > 0.01f) spriteRenderer.flipX = false;
            }
        }
    }
    
    // Virtual method for subclasses to add custom update behavior
    protected virtual void OnEnemyUpdate() { }

    void FindPlayer()
    {
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player != null)
        {
            target = player.transform;
            Debug.Log($"EnemyAI: Player found (Name: {player.name}) and assigned as target.");
            
            // Ignore collision with Player so we don't push them around
            IgnoreCollisionWithPlayer(player);
        }
    }

    void IgnoreCollisionWithPlayer(GameObject player)
    {
        Collider2D[] myColliders = GetComponentsInChildren<Collider2D>();
        Collider2D[] playerColliders = player.GetComponentsInChildren<Collider2D>();

        foreach (Collider2D myCol in myColliders)
        {
            foreach (Collider2D playerCol in playerColliders)
            {
                Physics2D.IgnoreCollision(myCol, playerCol, true);
            }
        }
        // Debug.Log("EnemyAI: Ignored collision with Player.");
    }

    void OnDrawGizmos()
    {
        if (path != null)
        {
            for (int i = targetIndex; i < path.Count; i++)
            {
                Gizmos.color = Color.black;
                Gizmos.DrawCube(path[i].worldPosition, Vector3.one * 0.2f);

                if (i == targetIndex)
                {
                    Gizmos.DrawLine(transform.position, path[i].worldPosition);
                }
                else
                {
                    Gizmos.DrawLine(path[i-1].worldPosition, path[i].worldPosition);
                }
            }
        }
    }


    private float lastDamageTime;
    public float damageCooldown = 1.0f;
    public float touchDamageRange = 0.8f; // Distance to check for "touching" player

    // Replaced OnCollisionStay2D with custom check in Update
    protected void CheckTouchDamage()
    {
        if (target != null)
        {
            float distance = Vector2.Distance(transform.position, target.position);
            
            // Check if close enough to "touch"
            if (distance <= touchDamageRange)
            {
                if (Time.time >= lastDamageTime + damageCooldown)
                {
                    PlayerHealth playerHealth = target.GetComponent<PlayerHealth>();
                    if (playerHealth != null)
                    {
                        Vector2 knockbackDir = (target.position - transform.position).normalized;
                        playerHealth.TakeDamage(10f, knockbackDir);
                        lastDamageTime = Time.time;
                        // Debug.Log($"EnemyAI: Touched Player! Dealt Damage. Distance: {distance}");
                    }
                }
            }
        }
    }

    /* 
    // Removed to allow passing through player
    void OnCollisionStay2D(Collision2D collision)
    {
        if (collision.gameObject.CompareTag("Player"))
        {
            if (Time.time >= lastDamageTime + damageCooldown)
            {
                PlayerHealth playerHealth = collision.gameObject.GetComponent<PlayerHealth>();
                if (playerHealth != null)
                {
                    Vector2 knockbackDir = (collision.transform.position - transform.position).normalized;
                    playerHealth.TakeDamage(10f, knockbackDir);
                    lastDamageTime = Time.time;
                }
            }
        }
    }
    */

    private Coroutine activationCoroutine;

    public void SetActive(bool active)
    {
        if (active)
        {
            if (IsActive) return; // Already active

            // Start delay if not already running
            if (activationCoroutine != null) StopCoroutine(activationCoroutine);
            activationCoroutine = StartCoroutine(ActivateRoutine());
        }
        else
        {
            if (activationCoroutine != null) StopCoroutine(activationCoroutine);
            IsActive = false;
            Debug.Log($"EnemyAI: Active state set to {active}");
        }
    }

    private IEnumerator ActivateRoutine()
    {
        yield return new WaitForSeconds(0.5f);
        IsActive = true;
        Debug.Log($"EnemyAI: Active state set to true (after delay)");
        OnEnemyActivated();
        activationCoroutine = null;
    }
    
    // Virtual method for subclasses to override when enemy becomes active
    protected virtual void OnEnemyActivated() { }
    
    // Helper method for subclasses to get room bounds
    protected Bounds GetRoomBounds()
    {
        if (parentRoom != null)
        {
            Vector3 center = parentRoom.transform.position;
            Vector2 size = parentRoom.roomSize;
            return new Bounds(center, new Vector3(size.x, size.y, 1f));
        }
        return new Bounds(transform.position, Vector3.one * 20f);
    }
    // Helper method to check if a position is valid (not inside a wall)
    protected bool IsPositionValid(Vector3 position, float radius = 0.3f)
    {
        // Check for walls at the position
        Collider2D hit = Physics2D.OverlapCircle(position, radius, LayerMask.GetMask("Obstacle"));
        if (hit != null) return false;
        
        // Also check if inside room bounds
        if (parentRoom != null)
        {
            Bounds bounds = GetRoomBounds();
            // Shrink bounds slightly to avoid hugging walls too closely
            bounds.Expand(-1f); 
            if (!bounds.Contains(position)) return false;
        }
        
        return true;
    }

    // Helper method for safe movement (stops at walls)
    protected void MoveSafely(Vector3 direction, float distance)
    {
        // Cast a ray to check for walls
        RaycastHit2D hit = Physics2D.Raycast(transform.position, direction, distance + 0.5f, LayerMask.GetMask("Obstacle"));
        
        if (hit.collider == null)
        {
            // Clear path, move normally
            transform.position += direction * distance;
        }
        else
        {
            // Wall detected, limit movement to hit point (minus buffer)
            float safeDistance = Mathf.Max(0, hit.distance - 0.5f);
            if (safeDistance > 0)
            {
                transform.position += direction * Mathf.Min(distance, safeDistance);
            }
        }
    }

    public virtual void DropLoot()
    {
        // Fallback: Try to load from RoomManager if not locally assigned
        if (coinPrefab == null && RoomManager.Instance != null)
        {
            coinPrefab = RoomManager.Instance.coinPrefab;
        }

        if (coinPrefab == null) 
        {
             Debug.LogWarning("EnemyAI: coinPrefab is missing! Please assign it in RoomManager or on the Enemy.");
             return;
        }

        float roll = Random.value;
        int coinCount = 0;

        // Loot Table
        // 3 Coins: 5% Chance (Roll < 0.05) - "Half of 2 coins chance"
        // 2 Coins: 10% Chance (Roll < 0.15) - "Half of 1 coin chance"
        // 1 Coin: 20% Chance (Roll < 0.35) - "1/5 chance"
        // Nothing: 65% Chance (Roll >= 0.35)

        if (roll < 0.05f) coinCount = 3;
        else if (roll < 0.15f) coinCount = 2; // 0.05 + 0.10
        else if (roll < 0.35f) coinCount = 1; // 0.15 + 0.20
        
        // Removed TEMP 100% drop code

        for(int i=0; i<coinCount; i++)
        {
            float mimicRoll = Random.value;
            GameObject prefabToSpawn = coinPrefab;
            
            // Each coin has a 1/5 (20%) chance to be a Mimic
            if (mimicRoll < 0.2f && RoomManager.Instance != null && RoomManager.Instance.mimicPrefab != null)
            {
                prefabToSpawn = RoomManager.Instance.mimicPrefab;
                Debug.Log("EnemyAI: Spawning Mimic instead of Coin!");
            }
            
            Vector2 offset = Random.insideUnitCircle * 0.5f;
            Instantiate(prefabToSpawn, transform.position + (Vector3)offset, Quaternion.identity);
        }
        
        if (coinCount > 0) Debug.Log($"EnemyAI: Dropped {coinCount} items (Roll: {roll:F3})");
    }

    public virtual void MarkAsDetected()
    {
        // Debug.Log($"EnemyAI: MarkAsDetected called on {gameObject.name}");
        
        if (GameHUD.Instance != null)
        {
            // Request UI marker (always visible on top of darkness)
            // Offset 0 so it's ON the enemy
            GameHUD.Instance.ShowEnemyMarker(transform, Vector3.zero, 2.0f);
        }
    }

    public void ApplyKnockback(Vector2 force, float duration)
    {
        // Allow knockback even if not active (e.g. idle enemy gets hit)
        // We rely on IsKnockedBack flag to pause AI, which works even if not active yet.
        
        // Don't call StopCoroutine(UpdatePath()) because checking the flag in the loop is cleaner
        // and StopCoroutine(Method()) doesn't work as intended here anyway.
        
        StartCoroutine(KnockbackRoutine(force, duration));
    }

    private IEnumerator KnockbackRoutine(Vector2 force, float duration)
    {
        IsKnockedBack = true;
        Debug.Log($"EnemyAI: Knockback started. Force: {force}");
        
        float originalDrag = 0f;
        if (rb != null)
        {
            rb.bodyType = RigidbodyType2D.Dynamic; // Explicitly set Dynamic (isKinematic = false is same but this is clearer)
            rb.constraints = RigidbodyConstraints2D.FreezeRotation; // Allow X/Y movement, freeze rotation
            
            originalDrag = rb.linearDamping;
            rb.linearDamping = 0; // Remove drag so they fly freely for the duration
            
            // Apply velocity directly. 
            // Note: If 'force' is actually a force vector (N), we should use AddForce.
            // But if we want instant predictable speed, we set velocity.
            // The previous code passed "30 * distancePercent", which is a scalar speed if treated as velocity.
            // Let's rely on setting velocity for arcade responsiveness.
            rb.linearVelocity = force; 
            
            Debug.Log($"EnemyAI: Applied Velocity {rb.linearVelocity} | Constraints: {rb.constraints} | BodyType: {rb.bodyType}");
        }
        
        yield return new WaitForSeconds(duration);
        
        Debug.Log("EnemyAI: Knockback ended.");
        
        // Recovery
        if (rb != null)
        {
             rb.linearVelocity = Vector2.zero; 
             rb.linearDamping = originalDrag;
        }
        
        IsKnockedBack = false;
        
        // Reset path to ensure we recalculate immediately after recovery
        targetIndex = 0; 
        path = null; 
    }
}
