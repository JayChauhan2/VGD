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

    public float maxHealth = 100f;
    private float currentHealth;
    private Room parentRoom;

    void Start()
    {
        currentHealth = maxHealth;
        
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
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            if (player != null) target = player.transform;
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
            yield return new WaitForSeconds(0.5f);
        }

        while (true)
        {
            // Retry finding player if missing
            if (target == null)
            {
                GameObject player = GameObject.FindGameObjectWithTag("Player");
                if (player != null) target = player.transform;
            }

            if (target != null && pathfinding != null && pathfinding.IsGridReady)
            {
                path = pathfinding.FindPath(transform.position, target.position);
                if (path != null && path.Count > 0)
                {
                    targetIndex = 0;
                }
            }
            yield return new WaitForSeconds(pathUpdateDelay);
        }
    }

    void Update()
    {
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
}
