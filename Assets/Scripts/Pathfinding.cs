using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class Pathfinding : MonoBehaviour
{
    PathfindingGrid grid;
    public bool IsGridReady => grid != null && grid.IsInitialized;

    void Awake()
    {
        grid = GetComponent<PathfindingGrid>();
        if (grid == null) Debug.LogError($"Pathfinding: Missing 'PathfindingGrid' component on GameObject '{name}'!");
    }

    public List<Node> FindPath(Vector3 startPos, Vector3 targetPos)
    {
        if (!IsGridReady) return null;

        Node startNode = grid.NodeFromWorldPoint(startPos);
        Node targetNode = grid.NodeFromWorldPoint(targetPos);
        
        // Validation
        if (startNode == null || targetNode == null) 
        {
            // Debug.LogError($"Pathfinding: Unable to get nodes... (suppressed)");
            return null;
        }

        // Check if target node is walkable
        if (!targetNode.walkable)
        {
            // Find closest walkable node using BFS
            Node closestWalkable = FindClosestWalkableNode(targetNode);
            if (closestWalkable != null)
            {
                targetNode = closestWalkable;
            }
            else
            {
                 // No walkable node found nearby? Just fail or keep targetNode (which will fail search)
                 // Debug.LogWarning("Pathfinding: Target is unwalkable and no walkable neighbor found.");
            }
        }

        List<Node> openSet = new List<Node>();
        HashSet<Node> closedSet = new HashSet<Node>();
        openSet.Add(startNode);

        int iterations = 0;
        while (openSet.Count > 0)
        {
            iterations++;
            if (iterations > 10000)
            {
                Debug.LogError("Pathfinding: A* loop safety break triggered! Search exceeded 10,000 nodes. Grid might be too large or logic trapped.");
                return null;
            }

            Node currentNode = openSet[0];
            for (int i = 1; i < openSet.Count; i++)
            {
                if (openSet[i].fCost < currentNode.fCost || openSet[i].fCost == currentNode.fCost && openSet[i].hCost < currentNode.hCost)
                {
                    currentNode = openSet[i];
                }
            }

            openSet.Remove(currentNode);
            closedSet.Add(currentNode);

            if (currentNode == targetNode)
            {
                return RetracePath(startNode, targetNode);
            }

            foreach (Node neighbor in grid.GetNeighbors(currentNode))
            {
                if (!neighbor.walkable || closedSet.Contains(neighbor))
                {
                    continue;
                }

                int newMovementCostToNeighbor = currentNode.gCost + GetDistance(currentNode, neighbor);
                if (newMovementCostToNeighbor < neighbor.gCost || !openSet.Contains(neighbor))
                {
                    neighbor.gCost = newMovementCostToNeighbor;
                    neighbor.hCost = GetDistance(neighbor, targetNode);
                    neighbor.parent = currentNode;

                    if (!openSet.Contains(neighbor))
                        openSet.Add(neighbor);
                }
            }
        }
        Debug.LogWarning("Pathfinding: OpenSet exhausted. Path not found.");
        return null;
    }

    Node FindClosestWalkableNode(Node startNode)
    {
        Queue<Node> queue = new Queue<Node>();
        HashSet<Node> visited = new HashSet<Node>();
        queue.Enqueue(startNode);
        visited.Add(startNode);

        // Limit search depth to avoid infinite loops or huge performance hits

        int steps = 0;

        while (queue.Count > 0 && steps < 100) // heuristic limit
        {
            Node current = queue.Dequeue();
            steps++;

            if (current.walkable) return current;

            foreach (Node neighbor in grid.GetNeighbors(current))
            {
                if (!visited.Contains(neighbor))
                {
                    visited.Add(neighbor);
                    queue.Enqueue(neighbor);
                }
            }
        }
        return null;
    }

    List<Node> RetracePath(Node startNode, Node endNode)
    {
        List<Node> path = new List<Node>();
        Node currentNode = endNode;

        int safetyCounter = 0;
        while (currentNode != startNode)
        {
            path.Add(currentNode);
            currentNode = currentNode.parent;
            
            safetyCounter++;
            if (safetyCounter > 5000)
            {
                Debug.LogError("Pathfinding: Infinite loop detected in RetracePath! Breaking safety.");
                break;
            }
            if (currentNode == null) break; // Should not happen but good safety
        }
        path.Reverse();
        return path;
    }

    int GetDistance(Node nodeA, Node nodeB)
    {
        int dstX = Mathf.Abs(nodeA.gridX - nodeB.gridX);
        int dstY = Mathf.Abs(nodeA.gridY - nodeB.gridY);

        if (dstX > dstY)
            return 14 * dstY + 10 * (dstX - dstY);
        return 14 * dstX + 10 * (dstY - dstX);
    }
}
