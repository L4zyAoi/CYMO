using System;
using UnityEngine;

/// <summary>
/// Manages the player's 4-slot quest item inventory.
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

    /// <summary>
    /// Fired whenever a slot changes (add or remove). 
    /// UI subscribes here.
    /// </summary>
    public event Action OnInvenChanged;

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
        for (int i = 0; i < SlotCnt; i++)
        {
            if (slots[i] == null)
            {
                slots[i] = item;
                OnInvenChanged?.Invoke();
                return true;
            }
        }
        return false; // inventory full
    }

    /// <summary>
    /// Removes the first occurrence of the given item from the inventory.
    /// </summary>
    public void RemoveItem(ItemData item)
    {
        for (int i = 0; i < SlotCnt; i++)
        {
            if (slots[i] == item)
            {
                slots[i] = null;
                OnInvenChanged?.Invoke();
                return;
            }
        }
    }

    /// <summary>
    /// Returns the item in the given slot index, or null if empty.
    /// </summary>
    public ItemData GetSlot(int index) =>
        (index >= 0 && index < SlotCnt) ? slots[index] : null;

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
        foreach (var s in slots)
            if (s == item) return true;
        return false;
    }
    #endregion
}
