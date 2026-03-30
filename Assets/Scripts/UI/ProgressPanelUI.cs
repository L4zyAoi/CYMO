using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Manages the Progress Panel HUD. Displays quest badges for the current chapter
/// and swaps their sprites between "Empty Frame" and "Filled" as the player collects them.
/// 
/// SETUP:
///  1. Attach this to your Progress Panel GameObject.
///  
///  2. Assign your Badge Prefab (a UI Image).
///  
///  3. Make sure your ChapterData has badges listed in its 'Chapter Badges' array.
/// </summary>
public class ProgressPanelUI : MonoBehaviour
{
    [Header("Config")]
    [Tooltip("A prefab with an Image component used to display the badge icon.")]
    public GameObject badgePrefab;

    void Start()
    {
        if (InventoryManager.Instance != null)
            InventoryManager.Instance.OnQuestInvenChanged += RefreshIcons;

        if (GameManager.Instance != null)
            GameManager.Instance.onChapterChanged += RebuildPanel;

        // Force an initial setup for the starting chapter
        RebuildPanel(GameManager.Instance?.currChapter);
    }

    void OnDestroy()
    {
        if (InventoryManager.Instance != null)
            InventoryManager.Instance.OnQuestInvenChanged -= RefreshIcons;

        if (GameManager.Instance != null)
            GameManager.Instance.onChapterChanged -= RebuildPanel;
    }

    /// <summary>
    /// Completely clears the panel and spawns new slots for the given chapter.
    /// </summary>
    public void RebuildPanel(ChapterData chapter)
    {
        if (badgePrefab == null)
        {
            Debug.LogWarning("[ProgressPanelUI] badgePrefab is not assigned in the Inspector!");
            return;
        }

        // 1. Clear existing slots
        foreach (Transform child in transform)
            Destroy(child.gameObject);

        if (chapter == null)
        {
            // If the game just started and GameManager isn't ready, this is normal.
            // Start() will trigger it again or onChapterChanged will fire soon.
            return;
        }

        if (chapter.chapterBadges == null || chapter.chapterBadges.Length == 0)
        {
            Debug.LogWarning($"[ProgressPanelUI] Current chapter '{chapter.chapName}' has no badges assigned in its chapterBadges array.");
            return;
        }

        // 2. Spawn a slot for each badge in this chapter
        foreach (var item in chapter.chapterBadges)
        {
            if (item == null) continue;

            GameObject badgeObj = Instantiate(badgePrefab, transform);
            badgeObj.name = "BadgeSlot_" + item.itemName;

            // Immediately set its sprite (Empty or Filled)
            RefreshSlotIcon(badgeObj, item);
        }
    }

    /// <summary>
    /// Updates only the sprites of existing slots (called when a badge is picked up).
    /// </summary>
    public void RefreshIcons()
    {
        ChapterData chapter = GameManager.Instance?.currChapter;
        if (chapter == null || chapter.chapterBadges == null) return;

        // Match existing children to chapter badges
        for (int i = 0; i < transform.childCount; i++)
        {
            if (i >= chapter.chapterBadges.Length) break;
            
            Transform child = transform.GetChild(i);
            ItemData data = chapter.chapterBadges[i];
            
            RefreshSlotIcon(child.gameObject, data);
        }
    }

    private void RefreshSlotIcon(GameObject slotObj, ItemData data)
    {
        Image img = slotObj.GetComponent<Image>();
        if (img == null) img = slotObj.GetComponentInChildren<Image>();

        if (img != null && data != null)
        {
            bool isCollected = InventoryManager.Instance != null && InventoryManager.Instance.Contains(data);
            
            // Logic: Is it in the inventory? Show 'Full', else show 'Empty'
            Sprite spriteToUse = isCollected ? data.icon : data.emptyBadgeIcon;
            
            if (spriteToUse != null)
            {
                img.sprite = spriteToUse;
                img.enabled = true;
                img.preserveAspect = true;
            }
            else
            {
                // If the data is missing a sprite, hide it so it doesn't show a white box
                img.enabled = false;
            }

            // Visual juice: Dim the alpha slightly if it's just an empty frame
            Color c = img.color;
            c.a = isCollected ? 1f : 0.6f;
            img.color = c;
        }
    }
}
