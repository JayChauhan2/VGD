using UnityEngine;
using System.Collections.Generic;
using System.Collections;

public class EnemyAI : MonoBehaviour
{
    public Transform target;
    public float speed = 5f;
    public float pathUpdateDelay = 0.2f;

    protected Pathfinding pathfinding;
    protected List<Node> path;
    protected int targetIndex;

    public bool IsActive { get; private set; } = false;

    public float maxHealth = 100f;
    protected float currentHealth;
    protected Room parentRoom;

    public event System.Action<float, float> OnHealthChanged;

    protected Rigidbody2D rb;

    // Public method to explicitly assign room (e.g. from Spawner)
    public void AssignRoom(Room room)
    {
        parentRoom = room;
        Debug.Log($"EnemyAI: Room explicitly assigned: {(room != null ? room.name : "null")}");
    }

    void Start()
    {
        rb = GetComponent<Rigidbody2D>();
        
        // Auto-add health bar if missing
        if (GetComponent<EnemyHealthBar>() == null)
        {
            gameObject.AddComponent<EnemyHealthBar>();
        }

        currentHealth = maxHealth;
        OnHealthChanged?.Invoke(currentHealth, maxHealth);
        
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
            if (!IsActive)
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
        
        // Virtual hook for subclasses to add custom update logic
        OnEnemyUpdate();
        
        if (path == null || targetIndex >= path.Count) 
        {
            if (rb != null) rb.linearVelocity = Vector2.zero;
            return;
        }

        Vector3 currentWaypoint = path[targetIndex].worldPosition;
        
        // Move towards waypoint using Physics if available
        if (rb != null)
        {
            Vector2 dir = (currentWaypoint - transform.position).normalized;
            rb.linearVelocity = dir * speed;
        }
        else
        {
            Vector3 nextPosition = Vector3.MoveTowards(transform.position, currentWaypoint, speed * Time.deltaTime);
            transform.position = nextPosition;
        }

        // Check if close to waypoint
        if (Vector3.Distance(transform.position, currentWaypoint) < 0.1f)
        {
            targetIndex++;
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
        }
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

    public void SetActive(bool active)
    {
        IsActive = active;
        Debug.Log($"EnemyAI: Active state set to {active}");
        
        // Virtual hook for subclasses
        if (active) OnEnemyActivated();
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
}
