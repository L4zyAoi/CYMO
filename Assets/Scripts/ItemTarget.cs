using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// A world (or UI) object that accepts a specific item being dragged from the inventory.
///
/// SETUP:
///  1. Attach to any interactable object in the scene (door, puzzle socket, NPC, etc.).
/// 
///  2. Assign the ItemData the object requires in "Required Item".
/// 
///  3. Wire up "On Item Used" in the Inspector to trigger puzzle/dialogue/event logic.
/// </summary>
public class ItemTarget : MonoBehaviour
{
    [Tooltip("The specific item that must be dropped here to trigger the action.")]
    public ItemData requiredItem;

    [Tooltip("Fired when the correct item is successfully dropped on this target. " +
             "Wire up puzzle logic, dialogue triggers, door unlocks, etc. here.")]
    public UnityEvent OnItemUsed;

    [Tooltip("Optional: GameObject shown while hovering a dragged item over this target.")]
    public GameObject dropHighlight;

    /// <summary>
    /// Attempt to use the given item on this target.
    /// Returns true if the item matched and was consumed.
    /// Called by InventorySlotUI when the player drops an item here
    /// </summary>
    public bool TryUse(ItemData droppedItem)
    {
        if (droppedItem == null) return false;

        if (droppedItem == requiredItem)
        {
            // Correct item — consume it and fire the event
            InventoryManager.Instance.RemoveItem(droppedItem);
            OnItemUsed?.Invoke();
            Debug.Log($"[ItemTarget] '{droppedItem.itemName}' used on '{gameObject.name}'.");
            return true;
        }

        // Wrong item
        Debug.Log($"[ItemTarget] '{droppedItem.itemName}' doesn't work here.");
        return false;
    }

    // Highlight while hovering
    public void ShowDropHighlight(bool show)
    {
        if (dropHighlight != null)
            dropHighlight.SetActive(show);
    }
}
