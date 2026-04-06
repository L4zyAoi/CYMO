using UnityEngine;

/// <summary>
/// Allows a world `PickupItem` to be dragged directly to an `ItemTarget` in the scene
/// (bypassing the inventory UI). Attach to any world object that also has `PickupItem`.
///
/// Behavior:
/// - Call `BeginDrag()` from input code (PointAndClickController starts it).
/// - Call `UpdateDrag(screenPosition)` every frame while dragging.
/// - Call `EndDrag()` on release; if dropped on a matching `ItemTarget`, the target
///   will be notified (via `TryUse`) and the world object will be disabled/destroyed.
/// </summary>
public class DraggablePickup : MonoBehaviour
{
    [Tooltip("Enable direct world dragging for this pickup.")]
    public bool allowDirectDrag = true;

    [Tooltip("Layer mask to detect drop targets (should include Interactable/ItemTarget layer).")]
    public LayerMask dropLayerMask;

    private PickupItem pickupItem;
    private Collider2D col;
    private bool isDragging = false;
    private Vector3 originalPosition;

    void Awake()
    {
        pickupItem = GetComponent<PickupItem>();
        col = GetComponent<Collider2D>();

        // If user didn't set a mask, try to use the 'Interactable' layer if it exists.
        if (dropLayerMask.value == 0)
        {
            int mask = LayerMask.NameToLayer("Interactable");
            if (mask >= 0) dropLayerMask = (1 << mask);
        }
    }

    public void BeginDrag()
    {
        if (!allowDirectDrag) return;
        isDragging = true;
        originalPosition = transform.position;
        if (col != null) col.enabled = false; // prevent blocking raycasts while dragging
    }

    public void UpdateDrag(Vector2 screenPosition)
    {
        if (!isDragging) return;
        Camera cam = Camera.main;
        if (cam == null) return;
        Vector3 world = cam.ScreenToWorldPoint(new Vector3(screenPosition.x, screenPosition.y, -cam.transform.position.z));
        world.z = transform.position.z;
        transform.position = world;
    }

    public void EndDrag()
    {
        if (!isDragging) return;
        isDragging = false;

        Camera cam = Camera.main;
        if (cam == null)
        {
            RestoreAfterFailedDrop();
            return;
        }

        Vector2 worldPos = cam.ScreenToWorldPoint(Input.mousePosition);
        Collider2D[] hits = Physics2D.OverlapPointAll(worldPos, dropLayerMask);

        foreach (var hit in hits)
        {
            if (hit == null) continue;
            ItemTarget target = hit.GetComponent<ItemTarget>();
            if (target == null) continue;

            // Attempt to use the item on the target. Use the underlying ItemData if available.
            bool used = target.TryUse(pickupItem != null ? pickupItem.item : null);
            if (used)
            {
                // Item was accepted — remove/hide this world object
                gameObject.SetActive(false);
                return;
            }
        }

        // Nothing accepted it -> restore
        RestoreAfterFailedDrop();
    }

    private void RestoreAfterFailedDrop()
    {
        if (col != null) col.enabled = true;
        transform.position = originalPosition;
    }
}
