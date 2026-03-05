using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Central singleton that tracks which chapter/map/section the player is in
/// and handles all transitions between them.
///
/// SETUP:
///  1. Create an empty GameObject in the first scene, name it "GameManager".
/// 
///  2. Attach this script to it.
/// 
///  3. Assign the three ChapterData assets in the Inspector.
/// 
///  4. Set Starting Chapter / Starting Map Index to match the opening scene.
/// 
///  5. Assign the Player transform so the manager can reposition it on section entry.
/// </summary>
public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    [Header("Chapters")]
    [Tooltip("All three chapter assets, in order.")]
    public ChapterData[] chapters = new ChapterData[3];

    [Header("Starting State")]
    public int startingChapterIndex = 0;
    public int startingMapIndex     = 0;
    public int startingSectionIndex = 0;

    [Header("References")]
    [Tooltip("The player GameObject — repositioned on every section entry.")]
    public Transform playerTransform;

    [Tooltip("Camera controller used to pan between sections. " +
             "Assign your CameraController here once it exists.")]
    public CameraController cameraController;

    public ChapterData currChapter { get; private set; }
    public MapData     currMap     { get; private set; }
    public int         currSectionIndex { get; private set; }
    public SectionData currSection =>
        currMap?.GetSection(currSectionIndex);

    #region Unity callbacks
    void Awake()
    {
        // Enforce singleton
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        // Set starting state
        if (chapters.Length > 0)
        {
            currChapter = chapters[Mathf.Clamp(startingChapterIndex, 0, chapters.Length - 1)];
            currMap     = currChapter?.GetMap(startingMapIndex);
            currSectionIndex = startingSectionIndex;
        }
    }
    #endregion

    #region Section Transitions
    /// <summary>
    /// Move the player to another section within the currently loaded map.
    /// No scene load — just reposition the player and pan the camera.
    /// </summary>
    public void GoToSection(int sectionIndex)
    {
        if (currMap == null) return;

        SectionData section = currMap.GetSection(sectionIndex);
        if (section == null) return;

        currSectionIndex = sectionIndex;

        // Stop any in-progress movement so momentum doesn't carry into the new section
        PointAndClickController controller =
            playerTransform != null ? playerTransform.GetComponent<PointAndClickController>() : null;
        controller?.StopMovement();

        // Reposition player
        if (playerTransform != null)
            playerTransform.position = section.spawnPoint;

        // Tell the camera to move to this section's bounds
        if (cameraController != null)
            cameraController.MoveToSection(section);

        Debug.Log($"[GameManager] Entered section: {section.sectionName}");
    }
    #endregion

    #region Map Transitions
    /// <summary>
    /// Load a different map (triggers a scene load).
    /// Optionally jump straight to a specific chapter too.
    /// </summary>
    public void GoToMap(MapData targetMap, int sectionIndex = 0, ChapterData targetChapter = null)
    {
        if (targetMap == null) return;

        if (targetChapter != null) currChapter = targetChapter;
        currMap          = targetMap;
        currSectionIndex = sectionIndex;

        StartCoroutine(LoadMapScene(targetMap.sceneName, sectionIndex));
    }

    private IEnumerator LoadMapScene(string sceneName, int sectionIndex)
    {
        // TODO: show a loading screen / fade here
        AsyncOperation op = SceneManager.LoadSceneAsync(sceneName);
        yield return new WaitUntil(() => op.isDone);

        // After load, the scene's objects have initialised — place the player
        GoToSection(sectionIndex);
    }
    #endregion

    #region Helpers
    /// <summary>Move to the next map in the current chapter.</summary>
    public void GoToNextMap()
    {
        if (currChapter == null || currMap == null) return;

        // Find current map index inside the chapter
        for (int i = 0; i < currChapter.maps.Length; i++)
        {
            if (currChapter.maps[i] == currMap)
            {
                int next = i + 1;
                if (next < currChapter.maps.Length)
                    GoToMap(currChapter.maps[next]);
                else
                    Debug.LogWarning("[GameManager] Already on the last map of this chapter.");
                return;
            }
        }
    }

    /// <summary>Move to the next chapter (first map, first section).</summary>
    public void GoToNextChapter()
    {
        for (int i = 0; i < chapters.Length; i++)
        {
            if (chapters[i] == currChapter)
            {
                int next = i + 1;
                if (next < chapters.Length)
                    GoToMap(chapters[next].DefaultMap, 0, chapters[next]);
                else
                    Debug.LogWarning("[GameManager] Already on the last chapter.");
                return;
            }
        }
    }
    #endregion
}
