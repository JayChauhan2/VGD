using UnityEngine;
using System.Collections.Generic;
using System.Collections;

public class EnemyAI : MonoBehaviour
{
    public Transform target;
    public float speed = 5f;
    public float pathUpdateDelay = 0.2f;

    Pathfinding pathfinding;
    List<Node> path;
    int targetIndex;

    public bool IsActive { get; private set; } = false;

    public float maxHealth = 100f;
    private float currentHealth;
    private Room parentRoom;

    public event System.Action<float, float> OnHealthChanged;

    void Start()
    {
        // Auto-add health bar if missing
        if (GetComponent<EnemyHealthBar>() == null)
        {
            gameObject.AddComponent<EnemyHealthBar>();
        }

        currentHealth = maxHealth;
        OnHealthChanged?.Invoke(currentHealth, maxHealth);
        
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
    }
    
    void CheckPathfinding()
    {
        pathfinding = FindFirstObjectByType<Pathfinding>(); // Unity 2023+ standard
        // if (pathfinding == null) pathfinding = FindObjectOfType<Pathfinding>(); // Deprecated fix


        if (pathfinding == null) Debug.LogError("EnemyAI: Pathfinding component not found via FindFirstObjectByType or FindObjectOfType!");
        if (target == null) Debug.LogWarning("EnemyAI: Target is null! Enemy will not move.");
    }

    public void TakeDamage(float damage)
    {
        currentHealth -= damage;
        OnHealthChanged?.Invoke(currentHealth, maxHealth);
        Debug.Log($"Enemy Health: {currentHealth}/{maxHealth}");
        
        if (currentHealth <= 0)
        {
            Die();
        }
    }

    private void Die()
    {
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
        if (path == null || targetIndex >= path.Count) return;

        Vector3 currentWaypoint = path[targetIndex].worldPosition;
        
        // Move towards waypoint
        transform.position = Vector3.MoveTowards(transform.position, currentWaypoint, speed * Time.deltaTime);

        // Check if close to waypoint
        if (Vector3.Distance(transform.position, currentWaypoint) < 0.1f)
        {
            targetIndex++;
        }
    }

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
    }
}
