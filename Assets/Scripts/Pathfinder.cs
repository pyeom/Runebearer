using System.Collections.Generic;
using UnityEngine;

public static class Pathfinder
{
    private static readonly Vector2Int[] Directions =
    {
        new Vector2Int( 1,  0),
        new Vector2Int(-1,  0),
        new Vector2Int( 0,  1),
        new Vector2Int( 0, -1),
    };

    /// <summary>
    /// Returns an ordered list of grid cells from start to end (exclusive of start),
    /// or null if no path exists.
    /// </summary>
    public static List<Vector2Int> FindPath(Vector2Int start, Vector2Int end, HashSet<Vector2Int> walkable)
    {
        if (!walkable.Contains(start) || !walkable.Contains(end))
            return null;

        if (start == end)
            return new List<Vector2Int>();

        var openSet   = new List<Vector2Int> { start };
        var closedSet = new HashSet<Vector2Int>();
        var cameFrom  = new Dictionary<Vector2Int, Vector2Int>();
        var gScore    = new Dictionary<Vector2Int, float> { [start] = 0f };
        var fScore    = new Dictionary<Vector2Int, float> { [start] = Heuristic(start, end) };

        while (openSet.Count > 0)
        {
            Vector2Int current = GetLowestF(openSet, fScore);

            if (current == end)
                return ReconstructPath(cameFrom, current);

            openSet.Remove(current);
            closedSet.Add(current);

            foreach (var dir in Directions)
            {
                Vector2Int neighbor = current + dir;

                if (!walkable.Contains(neighbor) || closedSet.Contains(neighbor))
                    continue;

                float tentativeG = gScore[current] + 1f;

                if (!gScore.TryGetValue(neighbor, out float existingG) || tentativeG < existingG)
                {
                    cameFrom[neighbor] = current;
                    gScore[neighbor]   = tentativeG;
                    fScore[neighbor]   = tentativeG + Heuristic(neighbor, end);

                    if (!openSet.Contains(neighbor))
                        openSet.Add(neighbor);
                }
            }
        }

        return null; // no path found
    }

    private static float Heuristic(Vector2Int a, Vector2Int b)
    {
        return Mathf.Abs(a.x - b.x) + Mathf.Abs(a.y - b.y);
    }

    private static Vector2Int GetLowestF(List<Vector2Int> openSet, Dictionary<Vector2Int, float> fScore)
    {
        Vector2Int best = openSet[0];
        float bestF = fScore.TryGetValue(best, out float v) ? v : float.MaxValue;

        for (int i = 1; i < openSet.Count; i++)
        {
            float f = fScore.TryGetValue(openSet[i], out float fi) ? fi : float.MaxValue;
            if (f < bestF) { bestF = f; best = openSet[i]; }
        }

        return best;
    }

    private static List<Vector2Int> ReconstructPath(Dictionary<Vector2Int, Vector2Int> cameFrom, Vector2Int current)
    {
        var path = new List<Vector2Int>();
        while (cameFrom.ContainsKey(current))
        {
            path.Add(current);
            current = cameFrom[current];
        }
        path.Reverse();
        return path;
    }
}
