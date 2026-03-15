using System.Collections;
using TMPro.Examples;
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

	[Header("Optional: per-section backgrounds")]
	[Tooltip("Assign per-section background mappings. Use Section Index (from MapData) —\n" +
			 "this avoids mismatches when MapData sections are not in the same order as your scene list.")]
	public SectionBackground[] sectionBackgrounds;

	[System.Serializable]
	public struct SectionBackground
	{
		[Tooltip("Index of the section in the current MapData this background applies to.")]
		public int sectionIndex;
		[Tooltip("SpriteRenderer in the scene to use for fitting the camera for that section.")]
		public SpriteRenderer background;
	}

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

        // Initial scene setup (for the first scene before any transitions)
        RebindSceneReferences();
    }

    /// <summary>
    /// Rebind cameraController and playerTransform to the current scene's objects.
    /// Called after scene loads and in Awake for initial setup.
    /// </summary>
    private void RebindSceneReferences()
    {
        // Find camera if not assigned or stale
        if (cameraController == null)
        {
            cameraController = FindObjectOfType<CameraController>();
            if (cameraController != null)
                Debug.Log($"[GameManager] CameraController bound to '{cameraController.gameObject.name}'.");
            else
                Debug.LogError("[GameManager] No CameraController found in scene! Section transitions will not pan the camera.");
        }

        // Find player if not assigned or stale
        if (playerTransform == null)
        {
            PointAndClickController controller = FindObjectOfType<PointAndClickController>();
            if (controller != null)
            {
                playerTransform = controller.transform;
                Debug.Log($"[GameManager] playerTransform bound to '{playerTransform.name}'.");
            }
            else
                Debug.LogError("[GameManager] No PointAndClickController found in scene! Section transitions will not move the player.");
        }
    }
    #endregion

	#region Section Transitions
	/// <summary>
	/// Resolves an exit id (searched across all sections in the map) to a destination section index.
	/// Falls back to treating the value as a direct section index for backward compatibility.
	/// </summary>
	public void GoToSectionFromExitOrSection(int exitOrSectionIndex)
	{
		if (TryResolveExitIdInMap(exitOrSectionIndex, out SectionData.ExitLink exitLink))
		{
			string resolvedExitName = string.IsNullOrWhiteSpace(exitLink.exitName) ? "(unnamed)" : exitLink.exitName;
			Debug.Log($"[GameManager] Resolved exit id {exitOrSectionIndex} ('{resolvedExitName}') -> section index {exitLink.targetSectionIndex} in map '{currMap?.mapName}'.");
			GoToSection(exitLink.targetSectionIndex, exitLink.useOverrideSpawnPoint, exitLink.overrideSpawnPoint);
			return;
		}

		Debug.Log($"[GameManager] Exit id {exitOrSectionIndex} not found in map '{currMap?.mapName}'. Treating as direct section index (backward compatible).");
		GoToSection(exitOrSectionIndex);
	}

	/// <summary>
	/// Move the player to another section within the currently loaded map.
	/// No scene load — just reposition the player and pan the camera.
	/// </summary>
	public void GoToSection(int sectionIndex)
	{
		GoToSection(sectionIndex, false, default);
	}

	private void GoToSection(int sectionIndex, bool useOverrideSpawnPoint, Vector2 overrideSpawnPoint)
	{
		if (currMap == null)
		{
			Debug.LogError("[GameManager] GoToSection: currMap is null. Cannot transition.");
			return;
		}

		if (currMap.sections == null || currMap.sections.Length == 0)
		{
			Debug.LogError($"[GameManager] GoToSection: currMap '{currMap.mapName}' has no sections.");
			return;
		}

		if (sectionIndex < 0 || sectionIndex >= currMap.sections.Length)
		{
			Debug.LogError($"[GameManager] GoToSection: section index {sectionIndex} is out of range for map '{currMap.mapName}' (sections count = {currMap.sections.Length}).");
			return;
		}

		SectionData section = currMap.sections[sectionIndex];
		if (section == null)
		{
			Debug.LogError($"[GameManager] GoToSection: section index {sectionIndex} is null in currMap '{currMap.mapName}'.");
			return;
		}

		currSectionIndex = sectionIndex;

		// DIAGNOSTIC LOGS
		Debug.Log($"[GameManager] GoToSection called -> sectionIndex={sectionIndex}, sectionName='{section.sectionName}', defaultSpawnPoint={section.spawnPoint}, useOverrideSpawnPoint={useOverrideSpawnPoint}, overrideSpawnPoint={overrideSpawnPoint}");
		Debug.Log($"[GameManager] section.cameraBounds={section.cameraBounds}");
		Debug.Log($"[GameManager] playerTransform={(playerTransform == null ? "NULL" : playerTransform.name)}, cameraController={(cameraController == null ? "NULL" : cameraController.name)}");

		// Find the active PointAndClickController (prefer the one on playerTransform but fallback if needed)
		PointAndClickController controller = null;
		if (playerTransform != null)
			controller = playerTransform.GetComponent<PointAndClickController>();

		if (controller == null)
		{
			controller = FindObjectOfType<PointAndClickController>();
			if (controller != null)
				Debug.Log($"[GameManager] Using fallback PointAndClickController on '{controller.gameObject.name}'.");
		}

		// Stop movement on the actual controller and teleport it (prevent physics carry-over)
		if (controller != null)
		{
			controller.StopMovement();

			Vector2 spawnPoint = useOverrideSpawnPoint ? overrideSpawnPoint : section.spawnPoint;

			// Teleport the controller GameObject (so the same Rigidbody2D is moved)
			controller.transform.position = spawnPoint;
			Debug.Log($"[GameManager] Player teleported to spawnPoint: {spawnPoint}" +
				(useOverrideSpawnPoint ? " (from ExitLink override)" : " (from SectionData.spawnPoint)"));

			Rigidbody2D rb = controller.GetComponent<Rigidbody2D>();
			if (rb != null)
			{
				rb.position = spawnPoint;
				rb.linearVelocity = Vector2.zero;
				rb.angularVelocity = 0f;
				rb.Sleep();
			}

			// Keep playerTransform in sync with the active controller for future references
			if (playerTransform != controller.transform)
			{
				playerTransform = controller.transform;
				Debug.Log("[GameManager] playerTransform updated to active controller transform.");
			}
		}
		else
		{
			// Last resort: move whatever was assigned in the inspector
			if (playerTransform != null)
			{
				Debug.LogWarning("[GameManager] No PointAndClickController found; moving playerTransform directly.");
				Vector2 spawnPoint = useOverrideSpawnPoint ? overrideSpawnPoint : section.spawnPoint;
				playerTransform.position = spawnPoint;
			}
			else
			{
				Debug.LogError("[GameManager] No playerTransform and no PointAndClickController found; cannot place player on section entry.");
			}
		}

		// Tell the camera to move to this section's bounds
		if (cameraController != null)
		{
			cameraController.MoveToSection(section);
			Debug.Log("[GameManager] cameraController.MoveToSection invoked with cameraBounds: " + section.cameraBounds);

			// If a mapped background exists for this section index, schedule fit.
			SpriteRenderer bg = FindBackgroundForSection(currSectionIndex);
			if (bg != null)
			{
				StartCoroutine(FitBackgroundNextFrame(currSectionIndex, bg, 0f, true));
				Debug.Log($"[GameManager] scheduled FitToBackground for section index {currSectionIndex} (mapped background).");
			}
			else
			{
				Debug.Log($"[GameManager] No mapped background for section index {currSectionIndex}.");
				// Ensure no stale inspector background is used by controller
				cameraController.backgroundToFit = null;
			}
		}
		else
		{
			Debug.LogError("[GameManager] cameraController is null — camera will NOT pan. Did you assign it in the Inspector or is it from a stale scene?");
		}

		Debug.Log($"[GameManager] Entered section: {section.sectionName}");
	}
	#endregion

	#region Map Transitions
	public void GoToMap(MapData targetMap, int sectionIndex = 0, ChapterData targetChapter = null)
	{
		if (targetMap == null) return;

		if (targetChapter != null) currChapter = targetChapter;
		currMap = targetMap;

		// Clamp the requested sectionIndex to the targetMap's section range before storing and using it.
		int clampedIndex = sectionIndex;
		if (targetMap.sections != null && targetMap.sections.Length > 0)
			clampedIndex = Mathf.Clamp(sectionIndex, 0, targetMap.sections.Length - 1);

		currSectionIndex = clampedIndex;

		StartCoroutine(LoadMapScene(targetMap.sceneName, clampedIndex));
	}

	private IEnumerator LoadMapScene(string sceneName, int sectionIndex)
	{
		AsyncOperation op = SceneManager.LoadSceneAsync(sceneName);
		yield return new WaitUntil(() => op.isDone);

		Debug.Log($"[GameManager] Scene '{sceneName}' loaded. Rebinding scene references...");

		cameraController = null;
		playerTransform = null;
		RebindSceneReferences();

		// VALIDATION: confirm currMap is set and the section index is valid
		if (currMap == null)
		{
			Debug.LogError($"[GameManager] LoadMapScene: currMap is null after setting targetMap! Check GoToMap call.");
			yield break;
		}

		SectionData targetSection = currMap.GetSection(sectionIndex);
		if (targetSection == null)
		{
			Debug.LogError($"[GameManager] LoadMapScene: section index {sectionIndex} does not exist in currMap '{currMap.mapName}'. currMap has {currMap.sections.Length} sections.");
			yield break;
		}

		Debug.Log($"[GameManager] LoadMapScene validation passed. currMap='{currMap.mapName}', targetSection='{targetSection.sectionName}', sectionIndex={sectionIndex}");

		// After re-binding and the scene is ready, place the player in the requested section
		GoToSection(sectionIndex);
	}
	#endregion

	#region Helpers
	/// <summary>
	/// Search all sections in the current map for a matching exit id.
	/// This allows exits in one section to reference mappings defined in any section.
	/// </summary>
	private bool TryResolveExitIdInMap(int exitId, out SectionData.ExitLink exitLink)
	{
		exitLink = default;

		if (currMap == null || currMap.sections == null)
			return false;

		for (int i = 0; i < currMap.sections.Length; i++)
		{
			SectionData section = currMap.sections[i];
			if (section == null) continue;
			if (section.TryGetExitLink(exitId, out exitLink))
				return true;
		}

		return false;
	}

	/// <summary>
	/// Lookup helper: find a mapped background for the given section index.
	/// </summary>
	private SpriteRenderer FindBackgroundForSection(int sectionIndex)
	{
		if (sectionBackgrounds == null || sectionBackgrounds.Length == 0) return null;
		for (int i = 0; i < sectionBackgrounds.Length; i++)
		{
			if (sectionBackgrounds[i].background == null) continue;
			if (sectionBackgrounds[i].sectionIndex == sectionIndex)
				return sectionBackgrounds[i].background;
		}
		return null;
	}

	private IEnumerator FitBackgroundNextFrame(int sectionIndex, SpriteRenderer bg, float padding, bool center)
	{
		const int maxFramesToWait = 8;
		int waited = 0;
		while (waited < maxFramesToWait &&
		      (bg == null || bg.sprite == null || bg.bounds.size.sqrMagnitude < 0.0001f))
		{
			waited++;
			yield return null;
		}

		if (bg == null)
		{
			Debug.LogWarning("[GameManager] Deferred FitToBackground aborted: background became null.");
			yield break;
		}

		if (cameraController == null)
		{
			Debug.LogWarning("[GameManager] Deferred FitToBackground aborted: cameraController is null.");
			yield break;
		}

		Rect sectionBounds = new Rect();
		if (currMap != null)
		{
			SectionData sd = currMap.GetSection(sectionIndex);
			if (sd != null) sectionBounds = sd.cameraBounds;
		}

		Vector2 bgWorldSize = bg.bounds.size;
		bool bgLargerThanSection = bgWorldSize.x > sectionBounds.width || bgWorldSize.y > sectionBounds.height;

		cameraController.FitToBackground(bg, padding, center, overrideBounds: bgLargerThanSection);

		Debug.Log($"[GameManager] cameraController.FitToBackground executed (deferred). bgSize={bgWorldSize}, sectionBounds={sectionBounds}, overrideBounds={bgLargerThanSection}");
	}

	public void GoToNextMap()
	{
		if (currChapter == null || currMap == null) return;

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