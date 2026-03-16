using UnityEngine;

/// <summary>
/// Defines the walkable region for a scene section.
///
/// SETUP:
///  1. Create a GameObject named "WalkableArea" (or smth idk) in the scene.
///  
///  2. Add a PolygonCollider2D and trace the shape of the walkable floor/path.
///     (Can also use multiple PolygonCollider2D by adding several WalkableArea
///      GameObjects and assigning them all in the PointAndClickController.)
///  
///  3. Set the GameObject's Layer to "Walkable"
///     (Name the layer all u want, just keep it consistent and different from the player's).
///  
///  4. Disable "Used By Effector", disable "Is Trigger" — leave it as a solid collider
///     so Physics2D queries work correctly.
///  
///  5. Assign this component (or its GameObject) to the PointAndClickController.
/// </summary>
[RequireComponent(typeof(PolygonCollider2D))]
public class WalkableArea : MonoBehaviour
{
    private PolygonCollider2D poly;

    void Awake()
    {
        poly = GetComponent<PolygonCollider2D>();
    }

    /// <summary>Returns true if the world point lies inside the walkable polygon.</summary>
    public bool Contains(Vector2 worldPoint)
    {
        return poly.OverlapPoint(worldPoint);
    }

    /// <summary>
    /// Returns the closest point that lies on or inside the walkable polygon.
    /// Useful when the player clicks just outside the path — snap to the border instead of ignoring the click.
    /// </summary>
    public Vector2 ClampToArea(Vector2 worldPoint)
    {
        if (Contains(worldPoint))
            return worldPoint;

        return poly.ClosestPoint(worldPoint);
    }

    /// <summary>
    /// Given an array of active WalkableAreas, returns the point as-is if it
    /// falls inside ANY area, otherwise returns the closest border point across
    /// all of them. Pass only enabled areas.
    /// </summary>
    public static Vector2 ClampToNearest(WalkableArea[] areas, Vector2 worldPoint)
    {
        if (areas == null || areas.Length == 0) return worldPoint;

        // If inside any area — accept the point directly
        foreach (var area in areas)
            if (area != null && area.isActiveAndEnabled && area.Contains(worldPoint))
                return worldPoint;

        // Find the closest border point across all active areas
        float   bestDist  = float.MaxValue;
        Vector2 bestPoint = worldPoint;

        foreach (var area in areas)
        {
            if (area == null || !area.isActiveAndEnabled) continue;
            Vector2 candidate = area.poly.ClosestPoint(worldPoint);
            float   dist      = Vector2.SqrMagnitude(candidate - worldPoint);
            if (dist < bestDist) { bestDist = dist; bestPoint = candidate; }
        }

        return bestPoint;
    }


    #region Gizmos 
    // Aoi: for visual debugging
    // TIP: Enable "Gizmos" in the Scene view to see the walkable boundary at edit time.
    void OnDrawGizmos()
    {
        if (poly == null) poly = GetComponent<PolygonCollider2D>();
        if (poly == null) return;

        Gizmos.color = new Color(0f, 1f, 0.4f, 0.25f);
        DrawPolygon(poly);
    }

    void OnDrawGizmosSelected()
    {
        if (poly == null) poly = GetComponent<PolygonCollider2D>();
        if (poly == null) return;

        Gizmos.color = new Color(0f, 1f, 0.4f, 0.6f);
        DrawPolygon(poly);
    }

    private void DrawPolygon(PolygonCollider2D collider)
    {
        Vector2[] points = collider.points;
        for (int i = 0; i < points.Length; i++)
        {
            Vector2 a = transform.TransformPoint(points[i]);
            Vector2 b = transform.TransformPoint(points[(i + 1) % points.Length]);
            Gizmos.DrawLine(a, b);
        }
    }
    #endregion

    #region Pathfinding (Visibility Graph)
    /// <summary>
    /// Generates a valid path inside the polygons from start to target using a visibility graph.
    /// Insets vertices slightly so the character doesn't scrape the walls.
    /// </summary>
    public static System.Collections.Generic.List<Vector2> GetPath(WalkableArea[] areas, Vector2 start, Vector2 target, float inset = 0.3f)
    {
        System.Collections.Generic.List<WalkableArea> activeAreas = new System.Collections.Generic.List<WalkableArea>();
        if (areas != null)
        {
            foreach (var a in areas)
                if (a != null && a.isActiveAndEnabled && a.poly != null)
                    activeAreas.Add(a);
        }

        if (activeAreas.Count == 0) return new System.Collections.Generic.List<Vector2>() { target };

        System.Collections.Generic.List<Vector2> nodes = new System.Collections.Generic.List<Vector2>();
        nodes.Add(start);
        nodes.Add(target);

        System.Collections.Generic.List<Vector2[]> edges = new System.Collections.Generic.List<Vector2[]>();

        foreach (var area in activeAreas)
        {
            for (int p = 0; p < area.poly.pathCount; p++)
            {
                Vector2[] path = area.poly.GetPath(p);

                for (int i = 0; i < path.Length; i++)
                {
                    Vector2 p1 = area.transform.TransformPoint(path[i]);
                    Vector2 p2 = area.transform.TransformPoint(path[(i + 1) % path.Length]);
                    edges.Add(new Vector2[] { p1, p2 });

                    Vector2 prev = area.transform.TransformPoint(path[(i - 1 + path.Length) % path.Length]);
                    Vector2 curr = p1;
                    Vector2 next = p2;

                    Vector2 dir1 = (prev - curr).normalized;
                    Vector2 dir2 = (next - curr).normalized;
                    Vector2 bisect = (dir1 + dir2).normalized;

                    if (bisect.sqrMagnitude < 0.01f)
                        bisect = new Vector2(-dir1.y, dir1.x);

                    Vector2 c1 = curr + bisect * inset;
                    Vector2 c2 = curr - bisect * inset;

                    if (AnyAreaContains(activeAreas, c1)) nodes.Add(c1);
                    else if (AnyAreaContains(activeAreas, c2)) nodes.Add(c2);
                    else nodes.Add(curr);
                }
            }
        }

        System.Collections.Generic.List<int>[] adj = new System.Collections.Generic.List<int>[nodes.Count];
        for (int i = 0; i < nodes.Count; i++) adj[i] = new System.Collections.Generic.List<int>();

        for (int i = 0; i < nodes.Count; i++)
        {
            for (int j = i + 1; j < nodes.Count; j++)
            {
                if (IsLineOfSightClear(nodes[i], nodes[j], edges, activeAreas))
                {
                    adj[i].Add(j);
                    adj[j].Add(i);
                }
            }
        }

        float[] dist = new float[nodes.Count];
        int[] prevNode = new int[nodes.Count];
        bool[] visited = new bool[nodes.Count];

        for (int i = 0; i < nodes.Count; i++)
        {
            dist[i] = float.MaxValue;
            prevNode[i] = -1;
        }
        dist[0] = 0;

        for (int i = 0; i < nodes.Count; i++)
        {
            int u = -1;
            float minDist = float.MaxValue;
            for (int j = 0; j < nodes.Count; j++)
            {
                if (!visited[j] && dist[j] < minDist)
                {
                    minDist = dist[j];
                    u = j;
                }
            }

            if (u == -1 || u == 1) break;
            visited[u] = true;

            foreach (int v in adj[u])
            {
                float alt = dist[u] + Vector2.Distance(nodes[u], nodes[v]);
                if (alt < dist[v])
                {
                    dist[v] = alt;
                    prevNode[v] = u;
                }
            }
        }

        if (prevNode[1] == -1)
        {
            Debug.LogWarning("[WalkableArea] Target is unreachable by visibility graph. Returning direct line fallback (character might clip walls). Ensure walkTarget is strictly inside the Polygon bounds.");
            return new System.Collections.Generic.List<Vector2>() { target }; // fallback
        }

        System.Collections.Generic.List<int> pathIndices = new System.Collections.Generic.List<int>();
        int currIdx = 1;
        while (currIdx != -1)
        {
            pathIndices.Add(currIdx);
            currIdx = prevNode[currIdx];
        }
        pathIndices.Reverse();

        System.Collections.Generic.List<Vector2> finalPath = new System.Collections.Generic.List<Vector2>();
        for (int i = 1; i < pathIndices.Count; i++)
            finalPath.Add(nodes[pathIndices[i]]);

        return finalPath;
    }

    private static bool AnyAreaContains(System.Collections.Generic.List<WalkableArea> areas, Vector2 pt)
    {
        foreach (var a in areas)
            if (a.Contains(pt)) return true;
        return false;
    }

    private static bool IsLineOfSightClear(Vector2 a, Vector2 b, System.Collections.Generic.List<Vector2[]> edges, System.Collections.Generic.List<WalkableArea> activeAreas)
    {
        // 1. Test against all solid edges
        foreach (var edge in edges)
        {
            if (SegmentsIntersectStrict(a, b, edge[0], edge[1]))
                return false;
        }

        // 2. Sample points along the line to ensure it doesn't cross exterior space in a concave U-bend
        if (!AnyAreaContains(activeAreas, (a + b) * 0.5f)) return false;
        if (!AnyAreaContains(activeAreas, Vector2.Lerp(a, b, 0.25f))) return false;
        if (!AnyAreaContains(activeAreas, Vector2.Lerp(a, b, 0.75f))) return false;

        return true;
    }

    private static bool SegmentsIntersectStrict(Vector2 A, Vector2 B, Vector2 C, Vector2 D)
    {
        float denominator = (B.x - A.x) * (D.y - C.y) - (B.y - A.y) * (D.x - C.x);
        if (Mathf.Abs(denominator) < 0.0001f) return false;

        float numerator1 = (A.y - C.y) * (D.x - C.x) - (A.x - C.x) * (D.y - C.y);
        float numerator2 = (A.y - C.y) * (B.x - A.x) - (A.x - C.x) * (B.y - A.y);

        float t1 = numerator1 / denominator;
        float t2 = numerator2 / denominator;

        // t1 is our path line. We require it to intersect strictly in the middle (>0.001) so rays from vertices don't self-intersect.
        // t2 is the wall edge. We include 0.0 and 1.0 (>= -0.0001) so a ray grazing EXACTLY a wall corner STILL counts as an intersection!
        return (t1 > 0.001f && t1 < 0.999f && t2 >= -0.0001f && t2 <= 1.0001f);
    }
    #endregion
}
