using UnityEngine;

/// <summary>
/// Shared ScriptableObject that tracks how many fragments of an item have been
/// collected. All FragmentPickup objects in the same set reference this asset.
///
/// CREATE: Right-click → Create → CYMO → Fragment Group
/// </summary>
[CreateAssetMenu(menuName = "CYMO/Fragment Group", fileName = "NewFragmentGroup")]
public class FragmentGroupData : ScriptableObject
{
    [Tooltip("Display name for this fragment group (used in notifications).")]
    public string groupName = "Item";

    [Tooltip("Total number of fragments that exist in the world for this group.")]
    public int totalFragments = 3;

    [Tooltip("The full item added to inventory when all fragments are collected.")]
    public ItemData resultItem;

    // Runtime state (resets on play) 
    private int collectedCount = 0;

    // Reset when entering Play mode so editor state doesn't carry over
    void OnEnable() => collectedCount = 0;

    /// <summary>
    /// Register one fragment as collected.
    /// Returns true if this was the LAST fragment (group now complete).
    /// </summary>
    public bool TryCollect()
    {
        if (collectedCount >= totalFragments) return false; // already complete

        collectedCount++;
        Debug.Log($"[FragmentGroup] '{groupName}': {collectedCount}/{totalFragments} collected.");
        return collectedCount >= totalFragments;
    }

    public int CollectedCount  => collectedCount;
    public int RemainingCount  => totalFragments - collectedCount;
    public bool IsComplete     => collectedCount >= totalFragments;
}
