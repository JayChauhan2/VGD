using UnityEngine;
using System.Collections.Generic;

public class PathfindingGrid : MonoBehaviour
{
    public LayerMask unwalkableMask;
    public Vector2 gridWorldSize;
    public float nodeRadius;
    Node[,] grid;

    float nodeDiameter;
    int gridSizeX, gridSizeY;
    public bool IsInitialized => grid != null;

    // Optional: Draw gizmos to see the grid
    public bool displayGridGizmos = true;

    void Awake()
    {
        if (nodeRadius <= 0)
        {
            nodeRadius = 0.5f; // Default to 0.5 if not set
            Debug.LogWarning("PathfindingGrid: nodeRadius was 0 or negative. Defaulting to 0.5.");
        }
        nodeDiameter = nodeRadius * 2;
    }

    public void CreateGrid(Vector2 center, Vector2 size)
    {
        gridWorldSize = size;
        transform.position = center; // Center the grid on the map
        
        nodeDiameter = nodeRadius * 2; // Recalculate in case it changed
        gridSizeX = Mathf.RoundToInt(gridWorldSize.x / nodeDiameter);
        gridSizeY = Mathf.RoundToInt(gridWorldSize.y / nodeDiameter);

        Debug.Log($"PathfindingGrid: Creating grid. Size: {gridWorldSize}, NodeRadius: {nodeRadius}, GridDimensions: {gridSizeX}x{gridSizeY}");

        if (gridSizeX <= 0 || gridSizeY <= 0)
        {
            Debug.LogError($"PathfindingGrid: Invalid grid size calculated! gridSizeX: {gridSizeX}, gridSizeY: {gridSizeY}");
            return;
        }

        grid = new Node[gridSizeX, gridSizeY];
        
        Vector3 worldBottomLeft = transform.position - Vector3.right * gridWorldSize.x / 2 - Vector3.up * gridWorldSize.y / 2;

        for (int x = 0; x < gridSizeX; x++)
        {
            for (int y = 0; y < gridSizeY; y++)
            {
                Vector3 worldPoint = worldBottomLeft + Vector3.right * (x * nodeDiameter + nodeRadius) + Vector3.up * (y * nodeDiameter + nodeRadius);
                // Check if walkable
                bool walkable = !(Physics2D.OverlapCircle(worldPoint, nodeRadius - 0.1f, unwalkableMask));
                grid[x, y] = new Node(walkable, worldPoint, x, y);
            }
        }
    }

    public List<Node> GetNeighbors(Node node)
    {
        List<Node> neighbors = new List<Node>();

        for (int x = -1; x <= 1; x++)
        {
            for (int y = -1; y <= 1; y++)
            {
                if (x == 0 && y == 0) continue;

                int checkX = node.gridX + x;
                int checkY = node.gridY + y;

                if (checkX >= 0 && checkX < gridSizeX && checkY >= 0 && checkY < gridSizeY)
                {
                    neighbors.Add(grid[checkX, checkY]);
                }
            }
        }

        return neighbors;
    }

    public Node NodeFromWorldPoint(Vector3 worldPosition)
    {
        if (grid == null) return null;

        // Convert world pos to grid percent
        float percentX = (worldPosition.x + gridWorldSize.x / 2) / gridWorldSize.x; // Assumes grid center is 0,0 relative to transform? No, see calculation below.
        
        // Adjust for grid center being at transform.position
        // (worldPosition - transform.position) gives local pos relative to center
        Vector3 localPos = worldPosition - transform.position;
        percentX = (localPos.x + gridWorldSize.x / 2) / gridWorldSize.x;
        float percentY = (localPos.y + gridWorldSize.y / 2) / gridWorldSize.y;
        
        percentX = Mathf.Clamp01(percentX);
        percentY = Mathf.Clamp01(percentY);

        int x = Mathf.RoundToInt((gridSizeX - 1) * percentX);
        int y = Mathf.RoundToInt((gridSizeY - 1) * percentY);
        if (x < 0 || x >= gridSizeX || y < 0 || y >= gridSizeY)
        {
            Debug.LogWarning($"PathfindingGrid: Requested node out of bounds. WorldPos: {worldPosition}, GridIndex: {x},{y}, Dimensions: {gridSizeX}x{gridSizeY}");
            return null;
        }

        Node node = grid[x, y];
        if (node == null)
        {
            Debug.LogError($"PathfindingGrid: Found null node at index {x},{y}. Grid might not be fully populated.");
        }
        return node;
    }

    void OnDrawGizmos()
    {
        Gizmos.DrawWireCube(transform.position, new Vector3(gridWorldSize.x, gridWorldSize.y, 1));

        if (grid != null && displayGridGizmos)
        {
            foreach (Node n in grid)
            {
                Gizmos.color = (n.walkable) ? new Color(1,1,1,0.3f) : new Color(1,0,0,0.3f);
                Gizmos.DrawCube(n.worldPosition, Vector3.one * (nodeDiameter - .1f));
            }
        }
    }
}
