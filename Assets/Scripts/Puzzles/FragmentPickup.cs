using UnityEngine;

/// <summary>
/// A world fragment of an item.
/// Multiple FragmentPickup objects share a FragmentGroupData.
/// 
/// Flow (sequential mode): Fragment 1 visible → collect → Fragment 2 appears → etc.
/// Flow (non-sequential mode): all fragments are visible and can be collected in any order.
/// When all fragments in sequence are collected, the full item assembles.
///
/// SETUP:
///  1. Set Layer to "Interactable".
/// 
///  2. Add a Collider2D sized to the fragment sprite.
/// 
///  3. Assign the shared FragmentGroupData asset to 'group'.
/// 
///  4. Assign a sequence number (0 for first, 1 for second, etc).
/// 
///  5. (Optional) Assign a collectEffect GameObject (particle/sprite flash).
/// 
///  6. (Optional) Assign an Animator with "idle" and "fall" triggers.
/// </summary>
public class FragmentPickup : MonoBehaviour
{
    [Tooltip("The shared group this fragment belongs to. " +
             "All fragments in the same set must reference the same asset.")]
    public FragmentGroupData group;

    [Tooltip("Sequence order: 0 = first, 1 = second, 2 = third, etc. " +
             "In non-sequential mode this is still used as the unique fragment index.")]
    public int sequenceIndex = 0;

    [Tooltip("Optional: a GameObject (particle/sprite) briefly shown on collection. " +
             "It will be enabled and then destroyed after 2 seconds.")]
    public GameObject collectEffect;

    [Header("Animation")]
    [Tooltip("Animator component for idle and fall animations.")]
    public Animator animator;
    [Tooltip("Trigger name for idle animation (Appearing/Pre-fall).")]
    public string triggerIdle = "idle";
    [Tooltip("Trigger name for fall animation (when touched/collected).")]
    public string triggerFall = "fall";
    [Tooltip("Trigger name for the steady idle animation AFTER it has fallen (Grounded).")]
    public string triggerIdlePostFall = "post_fall";

    [Header("Audio")]
    [Tooltip("Sound played when this fragment is collected.")]
    public AudioClip collectSFX;
    [Tooltip("Sound played when the final fragment is collected and item assembles.")]
    public AudioClip assembleSuccessSFX;

    [Header("Auto-Collect")]
    [Tooltip("If true, the interaction starts automatically when the player touches it (requires Trigger Collider).")]
    public bool isAutoCollect = false;

    [Header("Fragment Stack (Optional)")]
    [Tooltip("If true, each collected fragment adds one unit of 'fragmentStackItem' to inventory.")]
    public bool useFragmentStack = false;
    [Tooltip("Inventory item used to represent collected fragment pieces.")]
    public ItemData fragmentStackItem;
    [Min(1)]
    [Tooltip("How many fragment stack units are consumed when assembly completes.")]
    public int fragmentStackRequiredForAssemble = 3;
    [Tooltip("If true, the assembled result is added directly to quest/badge inventory instead of a normal slot.")]
    public bool addAssembledResultAsQuestItem = false;

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (isAutoCollect && other.CompareTag("Player"))
        {
            TryCollect();
        }
    }

    private Collider2D fragmentCollider;

    void Start()
    {
        fragmentCollider = GetComponent<Collider2D>();
        
        // Show/hide this fragment based on sequence
        UpdateVisibility();
    }

    void OnEnable()
    {
        if (group != null)
            group.OnSequenceChanged += UpdateVisibility;
    }

    void OnDisable()
    {
        if (group != null)
            group.OnSequenceChanged -= UpdateVisibility;
    }

    private void UpdateVisibility()
    {
        if (group == null) return;

        if (group.RequireSequentialCollection)
        {
            UpdateVisibilitySequential();
        }
        else
        {
            UpdateVisibilityNonSequential();
        }
    }

    private void UpdateVisibilitySequential()
    {
        if (group == null) return;

        bool isCurrentFragment = (group.CurrentSequenceIndex == sequenceIndex);

        // If this fragment should be hidden (already collected or not yet reached)
        if (!isCurrentFragment)
        {
            // If it's a PAST fragment, it's currently playing its fall animation.
            // Don't kill it immediately! Wait for the animation to finish.
            if (sequenceIndex < group.CurrentSequenceIndex)
            {
                // Disable collider immediately to prevent double-clicks
                if (fragmentCollider != null) fragmentCollider.enabled = false;
                
                // Keep visuals for a moment during fall, then hide
                StartCoroutine(HideVisualsAfterDelay(3.5f)); 
            }
            else
            {
                // It's a FUTURE fragment - keep it invisible but script ACTIVE
                SetVisualsEnabled(false);
            }
        }
        else
        {
            // It's the CURRENT fragment - prepare to show it
            if (sequenceIndex == 0)
            {
                // First fragment appears immediately
                SetVisualsEnabled(true);
                TriggerIdle();
            }
            else
            {
                // Others wait for the previous one to finish its fall
                StartCoroutine(ShowFragmentWithDelay());
            }
        }
    }

    private void UpdateVisibilityNonSequential()
    {
        if (group == null) return;

        bool collected = group.IsFragmentCollected(sequenceIndex);
        if (collected)
        {
            if (fragmentCollider != null) fragmentCollider.enabled = false;

            SpriteRenderer sr = GetComponent<SpriteRenderer>();
            if (sr != null && sr.enabled)
                StartCoroutine(HideVisualsAfterDelay(3.5f));
            else
                SetVisualsEnabled(false);

            return;
        }

        // Non-sequential: all uncollected fragments should remain available.
        SetVisualsEnabled(true);
        if (fragmentCollider != null) fragmentCollider.enabled = true;
    }

    private void SetVisualsEnabled(bool isEnabled)
    {
        // Toggle SpriteRenderer
        SpriteRenderer sr = GetComponent<SpriteRenderer>();
        if (sr != null) sr.enabled = isEnabled;

        // Toggle Collider
        if (fragmentCollider != null) fragmentCollider.enabled = isEnabled;

        // Toggle children (effects, etc)
        foreach (Transform child in transform)
            child.gameObject.SetActive(isEnabled);
    }

    private void TriggerIdle()
    {
        if (animator != null && !string.IsNullOrEmpty(triggerIdle))
        {
            // We use .Play(state, layer, time) to FORCE the animation to start from frame 0.
            // Since the animator was already "active" while hidden, this is the only
            // way to make the "appearing" part of the animation play again.
            animator.Play(triggerIdle, 0, 0f);
            Debug.Log($"[FragmentPickup] Sequence {sequenceIndex} idle/appearance forced to frame 0.");
        }
    }

    private System.Collections.IEnumerator ShowFragmentWithDelay()
    {
        // 1. Wait for PREVIOUS apple to finish its fall
        yield return new WaitForSeconds(3.5f);

        // 2. Make visible and start the "Appearing" (Idle) animation
        SetVisualsEnabled(true);
        TriggerIdle();

        // 3. Keep the collider OFF while it's appearing/growing
        // (Prevent clicking it until the animation looks "done")
        if (fragmentCollider != null) fragmentCollider.enabled = false;

        yield return new WaitForSeconds(2.0f); // Adjust this to match your "appearing" animation length

        // 4. Finally enable interaction
        if (fragmentCollider != null) fragmentCollider.enabled = true;
        
        Debug.Log($"[FragmentPickup] Sequence {sequenceIndex} is now fully grown and interactable.");
    }

    private System.Collections.IEnumerator HideVisualsAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);
        SetVisualsEnabled(false);
        Debug.Log($"[FragmentPickup] Sequence {sequenceIndex} visuals hidden after animation.");
    }

    // Called by PointAndClickController
    public void TryCollect()
    {
        if (group == null)
        {
            Debug.LogWarning($"[FragmentPickup] No FragmentGroupData on '{gameObject.name}'!", this);
            return;
        }

        // Safety check: sequential mode requires strict ordering.
        if (group.RequireSequentialCollection && group.CurrentSequenceIndex != sequenceIndex)
        {
            Debug.LogWarning($"[FragmentPickup] Tried to collect sequence {sequenceIndex}, but current is {group.CurrentSequenceIndex}!");
            return;
        }

        // Safety check: non-sequential mode should ignore duplicates.
        if (!group.RequireSequentialCollection && group.IsFragmentCollected(sequenceIndex))
        {
            Debug.LogWarning($"[FragmentPickup] Sequence {sequenceIndex} already collected.");
            return;
        }

        // Play collect sound
        if (collectSFX != null && AudioManager.Instance != null)
            AudioManager.Instance.PlaySFX(collectSFX);

        // Play fall animation IMMEDIATELY
        if (animator != null && !string.IsNullOrEmpty(triggerFall))
        {
            // Use .Play() to force an immediate override of the pre_fall/idle animation
            animator.Play(triggerFall, 0, 0f);
            Debug.Log($"[FragmentPickup] Fall animation FORCED for sequence {sequenceIndex}");
        }

        // Play collect effect at the fragment's position
        if (collectEffect != null)
        {
            GameObject fx = Instantiate(collectEffect, transform.position, Quaternion.identity);
            Destroy(fx, 2f);
        }

        // Disable collider with a delay so animation can start
        StartCoroutine(DisableColliderDelayed());

        // Advance the sequence
        bool allCollected = group.CollectFragment(sequenceIndex);

        // Optional stack progress for this puzzle flow.
        AddFragmentStackProgress();

        if (allCollected)
        {
            // FINAL APPLE - Special 3-stage sequence (Fall -> Ground Idle -> Collect)
            StartCoroutine(FinalCollectionSequence());
        }
    }

    private System.Collections.IEnumerator FinalCollectionSequence()
    {
        // 1. Fall (Triggered already in TryCollect, but we wait for it to land)
        Debug.Log("[FragmentPickup] Final Apple: Falling...");
        yield return new WaitForSeconds(3.0f); // Adjust to match your fall duration

        // 2. Play the grounded idle
        if (animator != null && !string.IsNullOrEmpty(triggerIdlePostFall))
        {
            animator.Play(triggerIdlePostFall, 0, 0f); // Force reset to grounded state
            Debug.Log("[FragmentPickup] Final Apple: Landed. Playing grounded idle.");
        }

        // 3. Keep it on the ground for a split second so the player sees it
        yield return new WaitForSeconds(1.0f);

        // 4. Finally assemble and add to inventory
        OnAllFragmentsCollected();
        
        // Final hide
        SetVisualsEnabled(false);
    }

    private System.Collections.IEnumerator DisableColliderDelayed()
    {
        // Wait for the 3-second fall animation to complete before disabling collider
        yield return new WaitForSeconds(3.5f);

        if (fragmentCollider != null)
        {
            fragmentCollider.enabled = false;
            Debug.Log($"[FragmentPickup] Collider disabled for sequence {sequenceIndex}");
        }
    }

    private System.Collections.IEnumerator PlayFallAnimationDelayed()
    {
        // Small delay to ensure animator state transition
        yield return null;

        if (animator != null && !string.IsNullOrEmpty(triggerFall))
        {
            animator.SetTrigger(triggerFall);
            Debug.Log($"[FragmentPickup] Fall animation triggered for sequence {sequenceIndex}");
        }
    }

    [ContextMenu("DEBUG: Reset Animation Names")]
    public void ResetAnimationNames()
    {
        triggerIdle = "idle";
        triggerFall = "fall";
        triggerIdlePostFall = "post_fall";
        Debug.Log("[FragmentPickup] Animation names reset to idle, fall, and post_fall.");
    }

    // Assembly (only when final fragment is collected)
    private void OnAllFragmentsCollected()
    {
        if (group.resultItem == null)
        {
            Debug.LogWarning($"[FragmentPickup] Group '{group.groupName}' has no resultItem assigned!");
            return;
        }

        // Play assemble success sound
        if (assembleSuccessSFX != null && AudioManager.Instance != null)
            AudioManager.Instance.PlaySFX(assembleSuccessSFX);

        if (useFragmentStack)
            ConsumeFragmentStackForAssemble();

        bool added = false;
        if (InventoryManager.Instance != null)
        {
            if (addAssembledResultAsQuestItem)
            {
                bool hadBefore = InventoryManager.Instance.Contains(group.resultItem);
                InventoryManager.Instance.AddQuestItem(group.resultItem);
                added = !hadBefore || InventoryManager.Instance.Contains(group.resultItem);
            }
            else
            {
                added = InventoryManager.Instance.TryAddItem(group.resultItem);
            }
        }

        if (!added)
        {
            Debug.LogWarning($"[FragmentPickup] Could not add '{group.groupName}' to inventory — inventory is full!");
        }
    }

    private void AddFragmentStackProgress()
    {
        if (!useFragmentStack) return;

        if (fragmentStackItem == null)
        {
            Debug.LogWarning($"[FragmentPickup] useFragmentStack enabled on '{gameObject.name}' but fragmentStackItem is missing.");
            return;
        }

        if (InventoryManager.Instance == null)
        {
            Debug.LogError("[FragmentPickup] Cannot track fragment stack because InventoryManager is missing.");
            return;
        }

        bool added = InventoryManager.Instance.TryAddItem(fragmentStackItem);
        if (!added)
        {
            Debug.LogWarning($"[FragmentPickup] Could not add fragment stack item '{fragmentStackItem.itemName}' — inventory is full.");
            return;
        }

        int current = InventoryManager.Instance.GetItemCount(fragmentStackItem);
        Debug.Log($"[FragmentPickup] Fragment stack '{fragmentStackItem.itemName}': {current}");
    }

    private void ConsumeFragmentStackForAssemble()
    {
        if (fragmentStackItem == null || InventoryManager.Instance == null) return;

        int required = Mathf.Max(1, fragmentStackRequiredForAssemble);
        bool removed = InventoryManager.Instance.TryRemoveItemAmount(fragmentStackItem, required);
        if (!removed)
        {
            int has = InventoryManager.Instance.GetItemCount(fragmentStackItem);
            Debug.LogWarning($"[FragmentPickup] Could not consume {required} '{fragmentStackItem.itemName}' for assembly (have {has}).");
        }
    }
}
