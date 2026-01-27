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

    void Start()
    {
        // Try to find the Player if target not assigned
        if (target == null)
        {
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            if (player != null) target = player.transform;
        }

        pathfinding = FindFirstObjectByType<Pathfinding>(); // Unity 2023+ standard
        if (pathfinding == null) 
            pathfinding = FindObjectOfType<Pathfinding>(); // Fallback

        if (pathfinding == null) Debug.LogError("EnemyAI: Pathfinding component not found via FindFirstObjectByType or FindObjectOfType!");
        if (target == null) Debug.LogWarning("EnemyAI: Target is null! Enemy will not move.");
        else Debug.Log($"EnemyAI: Initialized. Target: {target.name}, Pathfinding found: {pathfinding != null}");

        StartCoroutine(UpdatePath());
    }

    IEnumerator UpdatePath()
    {
        if (target == null) yield break;

        while (true)
        {
            if (pathfinding != null)
            {
                path = pathfinding.FindPath(transform.position, target.position);
                if (path != null && path.Count > 0)
                {
                    targetIndex = 0;
                }
                else
                {
                    Debug.LogWarning($"EnemyAI: Path calculation returned null or empty path to target {target.position}");
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
