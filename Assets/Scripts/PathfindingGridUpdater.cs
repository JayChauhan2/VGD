using UnityEngine;

/// <summary>
/// Lightweight static helper that caches the scene's PathfindingGrid and
/// exposes two methods for BreakableBox (and anything else) to call when
/// an obstacle appears or disappears.
/// </summary>
public static class PathfindingGridUpdater
{
    private static PathfindingGrid _grid;

    /// <summary>Returns the cached grid, auto-finding it if needed.</summary>
    private static PathfindingGrid Grid
    {
        get
        {
            if (_grid == null)
                _grid = Object.FindFirstObjectByType<PathfindingGrid>();
            return _grid;
        }
    }

    /// <summary>
    /// Call when an obstacle (e.g. a box) is placed in the world.
    /// Marks nearby pathfinding nodes as blocked.
    /// </summary>
    /// <param name="worldPos">World position of the obstacle's centre.</param>
    /// <param name="radius">Half-extent of the obstacle (use Collider bounds half-width).</param>
    public static void NotifyObstaclePlaced(Vector3 worldPos, float radius)
    {
        PathfindingGrid g = Grid;
        if (g == null || !g.IsInitialized) return;
        g.UpdateNodesInRadius(worldPos, radius);
    }

    /// <summary>
    /// Call when an obstacle is removed from the world.
    /// IMPORTANT: disable/destroy the obstacle's collider BEFORE calling this
    /// so the overlap check finds clean air and marks the nodes walkable.
    /// </summary>
    /// <param name="worldPos">World position of the obstacle's centre.</param>
    /// <param name="radius">Half-extent of the obstacle (use Collider bounds half-width).</param>
    public static void NotifyObstacleRemoved(Vector3 worldPos, float radius)
    {
        PathfindingGrid g = Grid;
        if (g == null || !g.IsInitialized) return;
        g.UpdateNodesInRadius(worldPos, radius);
    }

    /// <summary>Clears the cached reference (e.g. on scene reload).</summary>
    public static void InvalidateCache() => _grid = null;
}
