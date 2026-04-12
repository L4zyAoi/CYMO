using UnityEngine;
using System;

/// <summary>
/// Shared ScriptableObject that tracks fragment collection state.
/// Can work in sequential mode (Map 1 style) or non-sequential mode (all visible at once).
/// All FragmentPickup objects in the same set reference this asset.
///
/// CREATE: Right-click → Create → CYMO → Fragment Group
/// </summary>
[CreateAssetMenu(menuName = "CYMO/Fragment Group", fileName = "NewFragmentGroup")]
public class FragmentGroupData : ScriptableObject
{
    [Tooltip("Display name for this fragment group (used in notifications).")]
    public string groupName = "Item";

    [Tooltip("Total number of fragments in this group.")]
    public int totalFragments = 3;

    [Tooltip("If true, fragments must be collected in sequence (0,1,2...). If false, fragments can be collected in any order.")]
    public bool requireSequentialCollection = true;

    [Tooltip("The full item added to inventory when all fragments are collected.")]
    public ItemData resultItem;

    // Runtime state (resets on play)
    private int currentSequenceIndex = 0;
    private int collectedCount = 0;
    private bool[] collectedFlags;

    // Event fired when sequence advances (to update visibility)
    public event Action OnSequenceChanged;

    // Reset when entering Play mode so editor state doesn't carry over
    void OnEnable()
    {
        ResetRuntimeState();
    }

    private void ResetRuntimeState()
    {
        int safeTotal = Mathf.Max(1, totalFragments);
        collectedFlags = new bool[safeTotal];
        collectedCount = 0;
        currentSequenceIndex = 0;
    }

    private void EnsureRuntimeState()
    {
        int safeTotal = Mathf.Max(1, totalFragments);

        if (collectedFlags == null || collectedFlags.Length != safeTotal)
        {
            // Runtime safety: if size changes or state is missing, reset to a valid state.
            collectedFlags = new bool[safeTotal];
            collectedCount = 0;
            currentSequenceIndex = 0;
        }
    }

    /// <summary>
    /// Collect a fragment at the specified index.
    /// Returns true if this was the LAST fragment (group now complete).
    /// </summary>
    public bool CollectFragment(int sequenceIndex)
    {
        EnsureRuntimeState();

        if (sequenceIndex < 0 || sequenceIndex >= collectedFlags.Length)
        {
            Debug.LogWarning($"[FragmentGroup] Index {sequenceIndex} is out of range for '{groupName}' ({collectedFlags.Length}).");
            return false;
        }

        if (collectedFlags[sequenceIndex])
        {
            Debug.LogWarning($"[FragmentGroup] Sequence {sequenceIndex} in '{groupName}' was already collected.");
            return false;
        }

        // Sequential mode requires strict order.
        if (requireSequentialCollection && sequenceIndex != currentSequenceIndex)
        {
            Debug.LogWarning($"[FragmentGroup] Tried to collect sequence {sequenceIndex}, but current is {currentSequenceIndex}!");
            return false;
        }

        collectedFlags[sequenceIndex] = true;
        collectedCount++;

        if (requireSequentialCollection)
        {
            // Advance until first uncollected index.
            while (currentSequenceIndex < collectedFlags.Length && collectedFlags[currentSequenceIndex])
                currentSequenceIndex++;
        }
        else
        {
            // For non-sequential mode, expose progress count for compatibility.
            currentSequenceIndex = Mathf.Clamp(collectedCount, 0, collectedFlags.Length);
        }

        Debug.Log($"[FragmentGroup] '{groupName}': Collected sequence {sequenceIndex}. Progress: {collectedCount}/{collectedFlags.Length}");

        // Notify all fragments to update visibility
        OnSequenceChanged?.Invoke();

        // Check if all collected
        bool isComplete = collectedCount >= collectedFlags.Length;
        if (isComplete)
            Debug.Log($"[FragmentGroup] '{groupName}': ALL FRAGMENTS COLLECTED!");

        return isComplete;
    }

    public bool IsFragmentCollected(int index)
    {
        EnsureRuntimeState();
        if (index < 0 || index >= collectedFlags.Length) return false;
        return collectedFlags[index];
    }

    public int CurrentSequenceIndex => currentSequenceIndex;
    public int RemainingFragments   => Mathf.Max(0, Mathf.Max(1, totalFragments) - collectedCount);
    public bool IsComplete          => collectedCount >= Mathf.Max(1, totalFragments);
    public bool RequireSequentialCollection => requireSequentialCollection;
}
