using UnityEngine;

/// <summary>
/// Describes one chapter and the maps it contains.
///
/// CREATE: Right-click in Project → Create → CYMO → Chapter Data
/// </summary>
[CreateAssetMenu(menuName = "CYMO/Chapter Data", fileName = "NewChapter")]
public class ChapterData : ScriptableObject
{
    [Tooltip("Display name for this chapter (e.g. 'Chapter 1 – The Beginning').")]
    public string chapName = "New Chapter";

    [Tooltip("Maps that belong to this chapter, in story order.")]
    public MapData[] maps = new MapData[0];

    [Tooltip("Index of the map the player enters when starting this chapter.")]
    public int defaultMapIndex = 0;

    #region Helpers
    /// <summary>
    /// Returns the entry map for this chapter.
    /// </summary>
    public MapData DefaultMap =>
        maps.Length > 0 ? maps[defaultMapIndex] : null;

    /// <summary>
    /// Safe map getter.
    /// </summary>
    public MapData GetMap(int index)
    {
        if (maps == null || maps.Length == 0) return null;
        index = Mathf.Clamp(index, 0, maps.Length - 1);
        return maps[index];
    }
    #endregion
}
