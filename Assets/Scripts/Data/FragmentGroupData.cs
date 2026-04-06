using UnityEngine;
using System;

/// <summary>
/// Shared ScriptableObject that tracks fragment collection in sequence.
/// Fragments appear one at a time: collect fragment 0, then fragment 1 appears, etc.
/// All FragmentPickup objects in the same set reference this asset.
///
/// CREATE: Right-click → Create → CYMO → Fragment Group
/// </summary>
[CreateAssetMenu(menuName = "CYMO/Fragment Group", fileName = "NewFragmentGroup")]
public class FragmentGroupData : ScriptableObject
{
    [Tooltip("Display name for this fragment group (used in notifications).")]
    public string groupName = "Item";

    [Tooltip("Total number of fragments that exist in sequence (0, 1, 2, etc).")]
    public int totalFragments = 3;

    [Tooltip("The full item added to inventory when all fragments are collected.")]
    public ItemData resultItem;

    // Runtime state (resets on play)
    private int currentSequenceIndex = 0;

    // Event fired when sequence advances (to update visibility)
    public event Action OnSequenceChanged;

    // Reset when entering Play mode so editor state doesn't carry over
    void OnEnable() => currentSequenceIndex = 0;

    /// <summary>
    /// Collect a fragment at the specified sequence index.
    /// Returns true if this was the LAST fragment (group now complete).
    /// </summary>
    public bool CollectFragment(int sequenceIndex)
    {
        // Only allow collecting the current sequence index
        if (sequenceIndex != currentSequenceIndex)
        {
            Debug.LogWarning($"[FragmentGroup] Tried to collect sequence {sequenceIndex}, but current is {currentSequenceIndex}!");
            return false;
        }

        // Advance to next sequence
        currentSequenceIndex++;
        Debug.Log($"[FragmentGroup] '{groupName}': Collected sequence {sequenceIndex}. Next: {currentSequenceIndex}/{totalFragments}");

        // Notify all fragments to update visibility
        OnSequenceChanged?.Invoke();

        // Check if all collected
        bool isComplete = currentSequenceIndex >= totalFragments;
        if (isComplete)
            Debug.Log($"[FragmentGroup] '{groupName}': ALL FRAGMENTS COLLECTED!");

        return isComplete;
    }

    public int CurrentSequenceIndex => currentSequenceIndex;
    public int RemainingFragments   => totalFragments - currentSequenceIndex;
    public bool IsComplete          => currentSequenceIndex >= totalFragments;
}
