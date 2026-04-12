using UnityEngine;

/// <summary>
/// Defines a single quest item — its name, icon and description.
/// CREATE: Right-click in Project → Create → CYMO → Item Data
/// </summary>
[CreateAssetMenu(menuName = "CYMO/Item Data", fileName = "NewItem")]
public class ItemData : ScriptableObject
{
    [Tooltip("Display name shown in the inventory slot tooltip.")]
    public string itemName = "New Item";

    [Tooltip("Short description shown when the player hovers over the slot.")]
    [TextArea(2, 4)]
    public string description;

    [Tooltip("Icon displayed in the inventory slot.")]
    public Sprite icon;

    [Tooltip("Optional: The 'empty frame' sprite shown when this quest item hasn't been collected yet.")]
    public Sprite emptyBadgeIcon;

    [Tooltip("If true, this item appearing in the world will be collected as a 'badge' in the Progress Panel instead of taking up one of the 4 inventory slots.")]
    public bool isQuestItem = false;

    [Header("Inventory Stacking")]
    [Tooltip("If true, duplicate pickups of this item can stack in one inventory slot.")]
    public bool useStacking = false;

    [Min(1)]
    [Tooltip("Maximum amount allowed in a single stack slot for this item.")]
    public int maxStack = 99;
}
