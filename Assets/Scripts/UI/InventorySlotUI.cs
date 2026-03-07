using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;

/// <summary>
/// One inventory slot in the HUD. Displays the held item and acts as a drag source.
/// Attach one instance to each of the 4 slot root GameObjects in the Canvas.
///
/// SETUP (per slot):
///  1. Create a UI Image GameObject inside InventoryPanel → name it "Slot0" etc.
/// 
///  2. Add a child Image named "Icon" (shows item icon; disable when empty).
/// 
///  3. Add a child TextMeshProUGUI named "Tooltip" (shows item name on hover; start disabled).
/// 
///  4. Attach this script. Assign SlotIndex (0-3), IconImage, TooltipText.
/// 
///  5. The Canvas must have a GraphicRaycaster; the scene must have an EventSystem.
/// </summary>
public class InventorySlotUI : MonoBehaviour,
    IPointerEnterHandler, IPointerExitHandler,
    IBeginDragHandler, IDragHandler, IEndDragHandler
{
    [Header("Slot Config")]
    [Tooltip("Which inventory slot this UI element represents (0–3).")]
    public int slotIdx;

    [Header("References")]
    public Image       iconImg;     // child Image that shows the item icon
    public GameObject  tooltipObj; // child Text/TMP shown on hover
    public TMP_Text    tooltipTxt;   // label inside tooltipObject

    [Header("Drag Ghost")]
    [Tooltip("A CanvasGroup on the ghost image, used to make the ghost semi-transparent.")]
    public float ghostAlpha = 0.7f;

    private Canvas       rootCanvas;
    private GameObject   ghost;        // floating icon that follows the cursor
    private ItemData     draggedItem;  // item currently being dragged

    #region Unity callbacks
    void Awake() => rootCanvas = GetComponentInParent<Canvas>();
    

    void OnEnable()
    {
        if (InventoryManager.Instance != null)
            InventoryManager.Instance.OnInvenChanged += Refresh;
        Refresh();
    }

    void OnDisable()
    {
        if (InventoryManager.Instance != null)
            InventoryManager.Instance.OnInvenChanged -= Refresh;
    }
    #endregion

    #region Display
    private void Refresh()
    {
        ItemData item = InventoryManager.Instance?.GetSlot(slotIdx);

        if (item != null)
        {
            iconImg.sprite  = item.icon;
            iconImg.enabled = true;
        }
        else
        {
            iconImg.sprite  = null;
            iconImg.enabled = false;
        }

        if (tooltipObj != null) tooltipObj.SetActive(false);
    }
    #endregion

    #region Hover tooltip
    public void OnPointerEnter(PointerEventData _)
    {
        ItemData item = InventoryManager.Instance?.GetSlot(slotIdx);
        if (item == null || tooltipObj == null) return;

        tooltipObj.SetActive(true);
        if (tooltipTxt != null) tooltipTxt.text = item.itemName;
    }

    public void OnPointerExit(PointerEventData _)
    {
        if (tooltipObj != null) tooltipObj.SetActive(false);
    }
    #endregion

    #region Drag
    public void OnBeginDrag(PointerEventData eventData)
    {
        draggedItem = InventoryManager.Instance?.GetSlot(slotIdx);
        if (draggedItem == null) 
        { 
            eventData.pointerDrag = null; 
            return; 
        } // nothing to drag

        // Hide tooltip while dragging
        if (tooltipObj != null) tooltipObj.SetActive(false);

        // Spawn a semi-transparent ghost icon that follows the cursor
        ghost = new GameObject("DragGhost");
        ghost.transform.SetParent(rootCanvas.transform, false);
        ghost.transform.SetAsLastSibling(); // render on top

        Image ghostImg = ghost.AddComponent<Image>();
        ghostImg.sprite        = draggedItem.icon;
        ghostImg.raycastTarget = false; // ghost must not block raycasts

        RectTransform rt = ghost.GetComponent<RectTransform>();
        rt.sizeDelta = iconImg.rectTransform.sizeDelta;

        CanvasGroup cg = ghost.AddComponent<CanvasGroup>();
        cg.alpha         = ghostAlpha;
        cg.blocksRaycasts = false;

        UpdateGhostPosition(eventData);
    }

    public void OnDrag(PointerEventData eventData)
    {
        UpdateGhostPosition(eventData);

        // Highlight any ItemTarget the ghost is hovering over
        ItemTarget hovered = GetHoveredItemTarget(eventData);
        // (Highlight is handled by ItemTarget's own OnPointerEnter/Exit via EventSystem
        //  but since ghost has raycastTarget=false, we do it manually here)
        hovered?.ShowDropHighlight(true);
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        // Destroy the ghost regardless of outcome
        if (ghost != null) 
        { 
            Destroy(ghost); 
            ghost = null; 
        }

        if (draggedItem == null) return;

        // Check if we dropped onto an ItemTarget in the world via Physics2D raycast
        ItemTarget target = GetWorldItemTarget();

        if (target != null)
        {
            target.ShowDropHighlight(false);
            bool used = target.TryUse(draggedItem);
            if (used)
            {
                draggedItem = null;
                return; // item consumed — InventoryManager already fired OnInventoryChanged
            }
        }

        // Dropped on nothing / wrong target — item stays in slot (no change needed)
        draggedItem = null;
    }
    #endregion

    #region Helpers
    private void UpdateGhostPosition(PointerEventData eventData)
    {
        if (ghost == null) return;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            rootCanvas.transform as RectTransform,
            eventData.position,
            rootCanvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : rootCanvas.worldCamera,
            out Vector2 localPos);
        ghost.GetComponent<RectTransform>().localPosition = localPos;
    }

    /// <summary>
    /// Raycast into the world (Physics2D) from the current mouse position
    /// to find an ItemTarget under the cursor.
    /// </summary>
    private ItemTarget GetWorldItemTarget()
    {
        Vector2 worldPos = Camera.main.ScreenToWorldPoint(Input.mousePosition);
        Collider2D hit   = Physics2D.OverlapPoint(worldPos);
        return hit != null ? hit.GetComponent<ItemTarget>() : null;
    }

    /// <summary>
    /// Used during OnDrag to light up the hovered target.
    /// </summary>
    private ItemTarget GetHoveredItemTarget(PointerEventData eventData)
    {
        Vector2 worldPos = Camera.main.ScreenToWorldPoint(eventData.position);
        Collider2D hit   = Physics2D.OverlapPoint(worldPos);
        return hit != null ? hit.GetComponent<ItemTarget>() : null;
    }
    #endregion
}
