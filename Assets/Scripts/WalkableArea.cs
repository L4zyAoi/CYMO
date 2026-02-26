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

        // ClosestPoint returns the nearest point ON the collider surface
        return poly.ClosestPoint(worldPoint);
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
}
