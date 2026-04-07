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
    
    [Header("Opening Cutscene")]
    [Tooltip("Optional: name of a cutscene (from CutsceneManager) to play when this chapter starts.")]
    public string openingCutsceneName = "";

    [Tooltip("If true, the opening cutscene will only play once and then be skipped on subsequent starts.")]
    public bool openingCutscenePlayOnce = true;

    [Header("Opening Video")]
    [Tooltip("Optional: video clip to play when this chapter starts.")]
    public UnityEngine.Video.VideoClip openingVideoClip;

    [Tooltip("If true, the opening video will only play once and then be skipped on subsequent starts.")]
    public bool openingVideoPlayOnce = true;

    [Header("Map/Chapter Transition")]
    [Tooltip("Optional: a video clip to play as a transition when this chapter is exited (e.g., before loading the next chapter).")]
    public UnityEngine.Video.VideoClip transitionVideoClip;

    [Tooltip("If true, the transition video will only play once (skipped on subsequent exits).")]
    public bool transitionVideoPlayOnce = false;

    [Tooltip("All quest items (badges) that are relevant to this chapter. These will be displayed as slots in the Progress Panel.")]
    public ItemData[] chapterBadges = new ItemData[0];

    [Header("Completion Cinematic")]
    [Tooltip("Optional: video clip to play when the chapter's badges are all collected.")]
    public UnityEngine.Video.VideoClip completionVideoClip;

    [Tooltip("If true, the completion video will only play once and then be skipped on subsequent completions.")]
    public bool completionVideoPlayOnce = true;

    [Tooltip("Optional: name of a sprite-based cutscene (from CutsceneManager) to play when the chapter's badges are all collected.")]
    public string completionCutsceneName = "";

    [Tooltip("If true, the completion cutscene will only play once and then be skipped on subsequent completions.")]
    public bool completionCutscenePlayOnce = true;

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
