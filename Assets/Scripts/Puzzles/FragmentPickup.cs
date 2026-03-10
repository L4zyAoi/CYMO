using UnityEngine;

/// <summary>
/// A world fragment of an item. Place several of these in the scene, all pointing
/// to the same FragmentGroupData. When the last one is collected, the full item
/// is assembled and a notification is shown.
///
/// SETUP:
///  1. Set Layer to "Interactable".
/// 
///  2. Add a Collider2D sized to the fragment sprite.
/// 
///  3. Assign the shared FragmentGroupData asset to 'group'.
/// 
///  4. (Optional) Assign a collectEffect GameObject (particle/sprite flash).
/// </summary>
public class FragmentPickup : MonoBehaviour
{
    [Tooltip("The shared group this fragment belongs to. " +
             "All fragments in the same set must reference the same asset.")]
    public FragmentGroupData group;

    [Tooltip("Optional: a GameObject (particle/sprite) briefly shown on collection. " +
             "It will be enabled and then destroyed after 2 seconds.")]
    public GameObject collectEffect;

    // Called by PointAndClickController
    public void TryCollect()
    {
        if (group == null)
        {
            Debug.LogWarning($"[FragmentPickup] No FragmentGroupData on '{gameObject.name}'!", this);
            return;
        }

        // Hide this fragment immediately
        gameObject.SetActive(false);

        // Play collect effect at the fragment's position
        if (collectEffect != null)
        {
            GameObject fx = Instantiate(collectEffect, transform.position, Quaternion.identity);
            Destroy(fx, 2f);
        }

        bool lastFragment = group.TryCollect();

        if (lastFragment)
            OnAllFragmentsCollected();
        else
            ShowFragmentHint();
    }

    // Assembly
    private void OnAllFragmentsCollected()
    {
        if (group.resultItem == null)
        {
            Debug.LogWarning($"[FragmentPickup] Group '{group.groupName}' has no resultItem assigned!");
            return;
        }

        bool added = InventoryManager.Instance != null &&
                     InventoryManager.Instance.TryAddItem(group.resultItem);

        string message = added
            ? $"{group.groupName} assembled!"
            : $"{group.groupName} assembled — but inventory is full!";

        GameNotification.Show(message);
    }

    // Per-fragment hint
    private void ShowFragmentHint()
    {
        int remaining = group.RemainingCount;
        string msg = remaining == 1
            ? $"Almost there... 1 piece of {group.groupName} left."
            : $"{remaining} pieces of {group.groupName} left.";

        GameNotification.Show(msg);
    }
}
