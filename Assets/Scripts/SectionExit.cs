using UnityEngine;

/// <summary>
/// A trigger zone placed on a walkable path that transports the player
/// to another section (same map) or another map entirely.
///
/// SETUP:
///  1. Create a child GameObject on the WalkableArea (or scene root).
/// 
///  2. Add a BoxCollider2D → enable "Is Trigger".
/// 
///  3. Size/position the box so it spans the narrow exit path.
/// 
///  4. Attach this script.
/// 
///  5. Set Exit Type and target fields in the Inspector.
/// 
///  6. Make sure the Player GameObject has a Collider2D and its tag is "Player".
/// </summary>
public class SectionExit : MonoBehaviour
{
    public enum ExitType
    {
        /// <summary>Stay in the same scene — only reposition player + pan camera.</summary>
        SameMap,
        /// <summary>Load a different scene (map transition).</summary>
        NewMap
    }

    [Header("Exit Configuration")]
    public ExitType exitType = ExitType.SameMap;

    [Header("Same-Map Transition")]
    [Tooltip("Index of the section (in the current MapData) to go to.")]
    public int targetSectionIndex = 0;

    [Header("New-Map Transition")]
    [Tooltip("The MapData asset to load.")]
    public MapData targetMap;

    [Tooltip("Which section within the target map to spawn at.")]
    public int targetMapSectionIndex = 0;

    [Tooltip("Leave null to stay in the current chapter.")]
    public ChapterData targetChapter;

    // Prevent double-firing
    private bool triggered = false;

    #region Unity callbacks
    void OnTriggerEnter2D(Collider2D other)
    {
        if (triggered) return;
        if (!other.CompareTag("Player")) return;

        triggered = true;
        Fire();
    }

    private void Fire()
    {
        if (GameManager.Instance == null)
        {
            Debug.LogError("[SectionExit] No GameManager found in scene!");
            triggered = false;
            return;
        }

        switch (exitType)
        {
            case ExitType.SameMap:
                GameManager.Instance.GoToSection(targetSectionIndex);
                break;

            case ExitType.NewMap:
                if (targetMap == null)
                {
                    Debug.LogError("[SectionExit] New-Map exit has no targetMap assigned!", this);
                    triggered = false;
                    return;
                }
                GameManager.Instance.GoToMap(targetMap, targetMapSectionIndex, targetChapter);
                break;
        }

        // Re-enable after a short delay so the player can walk back through
        Invoke(nameof(ResetTrigger), 1.5f);
    }

    private void ResetTrigger() => triggered = false;
    #endregion

    #region Gizmo
    void OnDrawGizmos()
    {
        Gizmos.color = exitType == ExitType.SameMap
            ? new Color(0.2f, 0.6f, 1f, 0.35f)   // blue  = same map
            : new Color(1f, 0.5f, 0.1f, 0.35f);   // orange = new map

        BoxCollider2D box = GetComponent<BoxCollider2D>();
        if (box != null)
        {
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.DrawCube(box.offset, box.size);
        }
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = exitType == ExitType.SameMap
            ? new Color(0.2f, 0.6f, 1f, 0.8f)
            : new Color(1f, 0.5f, 0.1f, 0.8f);

        BoxCollider2D box = GetComponent<BoxCollider2D>();
        if (box != null)
        {
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.DrawWireCube(box.offset, box.size);
        }
    }
    #endregion
}
