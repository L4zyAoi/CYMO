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
}
