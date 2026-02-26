using UnityEngine;

/// <summary>
/// Describes one map (a Unity scene) and all of its sections.
///
/// CREATE: Right-click in Project → Create → CYMO → Map Data
/// </summary>
[CreateAssetMenu(menuName = "CYMO/Map Data", fileName = "NewMap")]
public class MapData : ScriptableObject
{
    [Tooltip("Display name for this map (e.g. 'Rainy Village').")]
    public string mapName = "New Map";

    [Tooltip("Exact name of the Unity scene to load for this map. " +
             "Must match the scene filename AND be added to Build Settings.")]
    public string sceneName;

    [Tooltip("Sections that exist within this map, in any order.")]
    public SectionData[] sections = new SectionData[0];

    [Tooltip("Index into 'sections' where the player spawns when entering this map for the first time.")]
    public int defaultSectionIndex = 0;

    #region Helpers
    /// <summary>
    /// Returns the default section, or null if the array is empty.
    /// </summary>
    public SectionData DefaultSection =>
        sections.Length > 0 ? sections[defaultSectionIndex] : null;

    /// <summary>
    /// Safe section getter — clamps index to valid range.
    /// </summary>
    public SectionData GetSection(int index)
    {
        if (sections == null || sections.Length == 0) return null;
        index = Mathf.Clamp(index, 0, sections.Length - 1);
        return sections[index];
    }
    #endregion
}
