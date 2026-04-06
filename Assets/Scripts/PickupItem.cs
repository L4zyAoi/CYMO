using UnityEngine;

/// <summary>
/// Attach to any world GameObject that the player can pick up.
/// Requires a Collider2D on the "Interactable" layer.
///
/// SETUP:
///  1. Set this GameObject's Layer to "Interactable"
///     (Add via Edit → Project Settings → Tags and Layers).
///
///  2. Add a Collider2D sized to the item sprite.
///
///  3. Assign the ItemData asset to 'item'.
///
/// Aoi: Click detection is handled by PointAndClickController via a
///      layer-masked raycast — so WalkableArea can't block it.
/// </summary>
public class PickupItem : MonoBehaviour
{
    [Tooltip("The item definition this world object represents.")]
    public ItemData item;

    [Tooltip("Optional: shown when the cursor hovers over this item.")]
    public GameObject hoverHighlight;

    [Tooltip("Sound played when this item is picked up.")]
    public AudioClip pickupSFX;

    // Hover highlight
    public void SetHighlight(bool on)
    {
        if (hoverHighlight != null)
            hoverHighlight.SetActive(on);
    }

    /// <summary>
    /// Attempts to add this item to the inventory.
    /// Returns true if picked up, false if inventory was full.
    /// Called by PointAndClickController on click
    /// </summary>
    public bool TryPickup()
    {
        if (item == null)
        {
            Debug.LogWarning($"[PickupItem] No ItemData assigned on '{gameObject.name}'!", this);
            return false;
        }

        if (InventoryManager.Instance == null)
        {
            Debug.LogError("[PickupItem] No InventoryManager in scene!");
            return false;
        }

        if (item.isQuestItem)
        {
            if (pickupSFX != null) AudioManager.Instance?.PlaySFX(pickupSFX);
            InventoryManager.Instance.AddQuestItem(item);
            gameObject.SetActive(false);
            return true;
        }

        if (InventoryManager.Instance.TryAddItem(item))
        {
            if (pickupSFX != null) AudioManager.Instance?.PlaySFX(pickupSFX);
            gameObject.SetActive(false); // hide from world
            return true;
        }

        Debug.Log($"[PickupItem] Inventory full — could not pick up '{item.itemName}'.");
        return false;
    }
}
