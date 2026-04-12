using System;
using UnityEngine;

/// <summary>
/// Manages the player's 4-slot inventory and quest badge list.
/// Persistent singleton — should survive scene loads.
///
/// SETUP:
///  1. Create an empty GameObject → attach this script.
/// 
///  2. It will persist automatically via DontDestroyOnLoad.
///     (Can share the existing GameManager GameObject if preferred.)
/// </summary>
public class InventoryManager : MonoBehaviour
{
    public static InventoryManager Instance { get; private set; }

    // Max inventory slots
    public const int SlotCnt = 4;
    private ItemData[] slots = new ItemData[SlotCnt];
    private int[] slotCounts = new int[SlotCnt];

    // Separate collection for non-functional "badge" items
    private System.Collections.Generic.List<ItemData> questItems = new System.Collections.Generic.List<ItemData>();

    /// <summary>
    /// Fired whenever a slot changes (add or remove). 
    /// UI subscribes here.
    /// </summary>
    public event Action OnInvenChanged;
    public event Action OnQuestInvenChanged;
    /// <summary>
    /// Fired when a quest/badge item is added to the quest inventory. Provides the added ItemData.
    /// </summary>
    public event Action<ItemData> OnQuestItemAdded;

    #region Unity callback(s)
    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }
    #endregion

    #region Public method(s)
    /// <summary>
    /// Tries to add an item to the first empty slot.
    /// Returns true on success, false if all slots are full.
    /// </summary>
    public bool TryAddItem(ItemData item)
    {
        if (item == null) return false;

        // Stack into an existing slot first when the item supports stacking.
        if (item.useStacking)
        {
            int maxStack = Mathf.Max(1, item.maxStack);

            for (int i = 0; i < SlotCnt; i++)
            {
                if (slots[i] != item) continue;

                int currentCount = Mathf.Max(1, slotCounts[i]);
                if (currentCount >= maxStack) continue;

                slotCounts[i] = currentCount + 1;
                OnInvenChanged?.Invoke();
                return true;
            }
        }

        // Otherwise place into the first empty slot.
        for (int i = 0; i < SlotCnt; i++)
        {
            if (slots[i] == null)
            {
                slots[i] = item;
                slotCounts[i] = 1;
                OnInvenChanged?.Invoke();
                return true;
            }
        }

        return false; // inventory full
    }

    /// <summary>
    /// Adds a non-functional quest item (badge) to the separate progress list.
    /// Does not take up one of the 4 slots.
    /// </summary>
    public void AddQuestItem(ItemData item)
    {
        if (item == null) return;
        if (questItems.Contains(item)) return;

        questItems.Add(item);
        // Notify per-item subscribers first
        OnQuestItemAdded?.Invoke(item);
        // Notify general quest inventory changed listeners
        OnQuestInvenChanged?.Invoke();
        Debug.Log($"[InventoryManager] Quest item collected: {item.itemName}");
    }

    /// <summary>
    /// Returns a read-only list of all collected quest items.
    /// </summary>
    public System.Collections.Generic.IEnumerable<ItemData> GetQuestItems() => questItems;

    /// <summary>
    /// Removes the first occurrence of the given item from the inventory.
    /// </summary>
    public void RemoveItem(ItemData item)
    {
        TryRemoveItemAmount(item, 1);
    }

    /// <summary>
    /// Removes an amount of an item from the standard inventory slots.
    /// Returns true when all requested units were removed.
    /// </summary>
    public bool TryRemoveItemAmount(ItemData item, int amount)
    {
        if (item == null || amount <= 0) return false;
        if (GetItemCount(item) < amount) return false;

        int remaining = amount;
        for (int i = 0; i < SlotCnt; i++)
        {
            if (slots[i] != item) continue;

            int unitCount = Mathf.Max(1, slotCounts[i]);
            int take = Mathf.Min(unitCount, remaining);

            unitCount -= take;
            remaining -= take;

            if (unitCount <= 0)
            {
                slots[i] = null;
                slotCounts[i] = 0;
            }
            else
            {
                slotCounts[i] = unitCount;
            }

            if (remaining <= 0)
                break;
        }

        OnInvenChanged?.Invoke();
        return true;
    }

    /// <summary>
    /// Returns the item in the given slot index, or null if empty.
    /// </summary>
    public ItemData GetSlot(int index) =>
        (index >= 0 && index < SlotCnt) ? slots[index] : null;

    /// <summary>
    /// Returns the visible count for a slot item (0 if empty).
    /// </summary>
    public int GetSlotCount(int index)
    {
        if (index < 0 || index >= SlotCnt) return 0;
        if (slots[index] == null) return 0;
        return Mathf.Max(1, slotCounts[index]);
    }

    /// <summary>
    /// Returns the total amount of an item in the 4-slot inventory.
    /// </summary>
    public int GetItemCount(ItemData item)
    {
        if (item == null) return 0;

        int count = 0;
        for (int i = 0; i < SlotCnt; i++)
        {
            if (slots[i] != item) continue;
            count += Mathf.Max(1, slotCounts[i]);
        }

        return count;
    }

    /// <summary>
    /// True if every slot is occupied.
    /// </summary>
    public bool IsFull()
    {
        foreach (var s in slots)
            if (s == null) return false;
        return true;
    }

    /// <summary>
    /// True if the item is currently in any slot.
    /// </summary>
    public bool Contains(ItemData item)
    {
        // 1. Check standard 4-slot inventory
        for (int i = 0; i < SlotCnt; i++)
        {
            if (slots[i] != item) continue;
            if (Mathf.Max(1, slotCounts[i]) > 0) return true;
        }

        // 2. Check quest/badge list
        if (questItems != null && questItems.Contains(item))
            return true;

        return false;
    }
    #endregion
}
