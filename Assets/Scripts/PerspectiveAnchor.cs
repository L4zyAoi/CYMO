using UnityEngine;

/// <summary>
/// Defines a perspective anchor point in a section.
/// The character's scale is interpolated based on proximity to these anchors.
/// 
/// SETUP:
/// 1. Create empty GameObjects at key positions in your scene (foreground, midground, background).
/// 2. Attach this component to each.
/// 3. Set the Scale value for that depth (e.g., 0.6 for far away, 1.0 for close).
/// 4. Add all anchors to the section's anchors array in the inspector.
/// </summary>
public class PerspectiveAnchor : MonoBehaviour
{
    [Tooltip("The scale the character should have at this anchor's position.")]
    [Range(0.1f, 2.0f)]
    public float scale = 1.0f;

    [Tooltip("Radius of influence for this anchor. " +
             "Anchors within this distance contribute to the character's scale.")]
    public float influenceRadius = 5.0f;

    [Tooltip("If true, this anchor uses Y position only (vertical perspective). " +
             "If false, uses distance in all directions.")]
    public bool useYAxisOnly = true;

    /// <summary>Get the world position of this anchor.</summary>
    public Vector2 GetPosition() => transform.position;

    /// <summary>Calculate the distance from a world point to this anchor.</summary>
    public float GetDistance(Vector2 worldPoint)
    {
        if (useYAxisOnly)
        {
            return Mathf.Abs(worldPoint.y - transform.position.y);
        }
        else
        {
            return Vector2.Distance(worldPoint, transform.position);
        }
    }

    /// <summary>Check if a world point is within this anchor's influence radius.</summary>
    public bool IsWithinInfluence(Vector2 worldPoint)
    {
        return GetDistance(worldPoint) <= influenceRadius;
    }

#if UNITY_EDITOR
    void OnDrawGizmosSelected()
    {
        // Draw anchor point
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, 0.2f);

        // Draw influence radius
        Gizmos.color = new Color(0, 1, 1, 0.3f);
        if (useYAxisOnly)
        {
            // Draw as horizontal lines for Y-only
            Gizmos.DrawLine(
                transform.position + new Vector3(-influenceRadius, 0, 0),
                transform.position + new Vector3(influenceRadius, 0, 0)
            );
        }
        else
        {
            Gizmos.DrawWireSphere(transform.position, influenceRadius);
        }

        // Draw scale text
        GUI.color = new Color(0, 1, 1, 1);
    }
#endif
}
