using UnityEngine;

using UnityEngine.Serialization;

/// <summary>
/// Clickable exit zone that walks the character to the exit then
/// transitions to the target section or map.
///
/// SETUP:
///  1. Set Layer to "Interactable" (same as PickupItem / BlockingObstacle).
///  2. Add a BoxCollider2D (Is Trigger: OFF for click detection).
///     Size it to cover the visible exit area.
///  3. Attach this script and set Exit Type + target fields.
///  4. Set Walk Target to a world point INSIDE the nearest WalkableArea —
///     this is where the character walks before the transition fires.
///     (Use the Gizmo sphere in the Scene view to position it.)
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

    [Header("Walk Navigation")]
    [Tooltip("World position the character walks to before the transition fires. " +
             "Must be inside a WalkableArea. Position using the Scene-view gizmo.")]
    public Vector2 walkTarget;

    [Header("Same-Map Transition")]
    [FormerlySerializedAs("targetSectionIndex")]
    [UnityEngine.Serialization.FormerlySerializedAs("targetExitId")]
    [Tooltip("Exit id for same-map transitions. " +
             "If current SectionData has an ExitLink with this id, that mapping is used. " +
             "Otherwise this is treated as a direct section index (backward compatible).")]
    public int exitId = 0;

    [Header("New-Map Transition")]
    [Tooltip("The MapData asset to load.")]
    public MapData targetMap;

    [Tooltip("Which section within the target map to spawn at.")]
    public int targetMapSectionIndex = 0;

    [Tooltip("Leave null to stay in the current chapter.")]
    public ChapterData targetChapter;

    private bool triggered = false;

    #region Interaction
    /// <summary>
    /// Called by PointAndClickController when the player clicks this exit.
    /// Sends the character to walkTarget, then fires the transition on arrival.
    /// </summary>
    public void OnClicked()
    {
        if (triggered) return;

        PointAndClickController player =
            FindFirstObjectByType<PointAndClickController>();

        if (player == null)
        {
            Debug.LogError("[SectionExit] No PointAndClickController found in scene!");
            return;
        }

        triggered = true;
        player.SetDestinationWithCallback(walkTarget, Fire);
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
                GameManager.Instance.GoToSectionFromExitOrSection(exitId, walkTarget);
                break;

            case ExitType.NewMap:
                if (targetMap == null)
                {
                    Debug.LogError("[SectionExit] New-Map exit has no targetMap assigned!", this);
                    triggered = false;
                    return;
                }
                GameManager.Instance.GoToMap(targetMap, targetMapSectionIndex, targetChapter, walkTarget);
                break;
        }

        Invoke(nameof(ResetTrigger), 1.5f);
    }

    private void ResetTrigger() => triggered = false;
    #endregion

    #region Gizmos
    void OnDrawGizmos()
    {
        Color c = exitType == ExitType.SameMap
            ? new Color(0.2f, 0.6f, 1f, 0.35f)
            : new Color(1f, 0.5f, 0.1f, 0.35f);
        Gizmos.color = c;

        BoxCollider2D box = GetComponent<BoxCollider2D>();
        if (box != null)
        {
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.DrawCube(box.offset, box.size);
        }

        // Draw the walk target so you can position it in the Scene view
        Gizmos.matrix = Matrix4x4.identity;
        Gizmos.color  = Color.yellow;
        Gizmos.DrawSphere(walkTarget, 0.12f);
    }

    void OnDrawGizmosSelected()
    {
        Color c = exitType == ExitType.SameMap
            ? new Color(0.2f, 0.6f, 1f, 0.8f)
            : new Color(1f, 0.5f, 0.1f, 0.8f);
        Gizmos.color = c;

        BoxCollider2D box = GetComponent<BoxCollider2D>();
        if (box != null)
        {
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.DrawWireCube(box.offset, box.size);
        }

        Gizmos.matrix = Matrix4x4.identity;
        Gizmos.color  = Color.yellow;
        Gizmos.DrawWireSphere(walkTarget, 0.15f);
    }
    #endregion
}
