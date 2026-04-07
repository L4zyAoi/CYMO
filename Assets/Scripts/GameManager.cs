using System.Collections;
using System.Collections.Generic;
using TMPro.Examples;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using UnityEngine;
using System.Text.RegularExpressions;
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

	[Header("Guidance")]
	[Tooltip("Prefab for the guidance arrow. It should have a GuidanceArrow component.")]
	public GameObject guidanceArrowPrefab;

	[Tooltip("Optional: exit id to target for the initial guidance arrow. Set to -1 to use default (next section).")]
	public int initialGuidanceExitId = -1;

	[Tooltip("Optional: exit IDs to spawn automatically after the player reaches the initial guidance destination section.")]
	public int[] initialFollowupExitIds = new int[0];

	[Tooltip("If true, activate SceneGuidanceMarker objects in the scene when the initial arrow disappears. Otherwise spawn follow-up arrows using `initialFollowupExitIds`.")]
	public bool activateSceneFollowups = true;

	[Tooltip("If true, follow-up guidance appears only after the player first reaches the initial guidance destination section and then returns to the section where the initial guidance started.")]
	public bool initialFollowupsAfterReturn = true;

	[Tooltip("If true, point at the SectionExit object's transform position. If false, point at its walkTarget.")]
	public bool guidanceUseExitTransform = true;

	public enum InitialGuidanceSpawnMode
	{
		Default = 0,
		WorldPosition = 1,
		SceneObject = 2,
		SectionSpawn = 3
	}

	[Tooltip("Select how the initial guidance arrow spawn target is chosen.")]
	public InitialGuidanceSpawnMode initialGuidanceSpawnMode = InitialGuidanceSpawnMode.Default;

	[Tooltip("When using WorldPosition mode, the world coordinate to point at.")]
	public Vector3 initialGuidanceWorldPosition = Vector3.zero;

	[Tooltip("When using SceneObject mode, a scene GameObject to point at.")]
	public GameObject initialGuidanceSceneObject = null;

	[Tooltip("When using SectionSpawn mode, explicit section index to point at (clamped). -1 to use default next section.")]
	public int initialGuidanceSectionIndexOverride = -1;

	[Header("Cutscene Options")]
	[Tooltip("If true, start all section on-enter cutscenes (video or sprite) while the screen is black during a walk transition.")]
	public bool forceStartCutscenesDuringBlack = false;

	// Internal: whether StartGame initiated a fresh play so we show initial guidance after load
	private bool pendingInitialGuidance = false;
	private Coroutine pendingInitialGuidanceRoutine = null;
	private GameObject activeGuidanceArrow = null;
	private readonly Queue<Vector3> queuedGuidanceTargets = new Queue<Vector3>();
	private Coroutine queuedGuidanceRoutine = null;

	// Track initial-guidance destination so we can spawn followups when that section is entered
	private int initialGuidanceDestinationSection = -1;
	private bool initialGuidanceFollowupsPending = false;
	private int initialGuidanceSourceSection = -1;
	private bool initialGuidanceDestinationVisited = false;

	// Whether the currently active guidance arrow was the initial opening arrow
	private bool activeGuidanceIsInitial = false;
	private bool forceNextSectionTransitionStartBlack = false;

	// Follow-up activation helper
	private Coroutine followupActivationWaitRoutine = null;
	private const float followupActivationVideoWaitTimeout = 5f;

	[System.Serializable]
	public struct SectionBackground
	{
		[Tooltip("Index of the section in the current MapData this background applies to.")]
		public int sectionIndex;
		[Tooltip("SpriteRenderer in the scene to use for fitting the camera for that section.")]
		public SpriteRenderer background;
	}

	/// <summary>
	/// Public helper: show a guidance arrow pointing at the SectionExit with the given exitId (if found in scene).
	/// </summary>
	public void ShowGuidanceToExit(int exitId)
	{
		SectionExit[] allExits = FindObjectsOfType<SectionExit>();
		foreach (SectionExit exit in allExits)
		{
			if (exit.exitId == exitId)
			{
				Vector3 target = new Vector3(exit.walkTarget.x, exit.walkTarget.y, 0f);
				RequestGuidanceArrow(target);
				return;
			}
		}
		Debug.LogWarning($"[GameManager] ShowGuidanceToExit: no SectionExit with id {exitId} found in scene.");
	}

	/// <summary>
	/// Public helper: show a guidance arrow to a specific world position.
	/// If an arrow is already active, this request is queued and shown later.
	/// </summary>
	public void ShowGuidanceToWorldPosition(Vector3 targetWorldPos)
	{
		RequestGuidanceArrow(targetWorldPos);
	}

	private void RequestGuidanceArrow(Vector3 targetWorldPos)
	{
		bool canShowNow =
			activeGuidanceArrow == null &&
			(TransitionManager.Instance == null || !TransitionManager.Instance.IsTransitioning);

		if (canShowNow)
		{
			ShowGuidanceArrow(targetWorldPos);
			return;
		}

		queuedGuidanceTargets.Enqueue(targetWorldPos);
		EnsureQueuedGuidanceRoutine();
	}

	private void EnsureQueuedGuidanceRoutine()
	{
		if (queuedGuidanceRoutine == null)
		{
			queuedGuidanceRoutine = StartCoroutine(ProcessQueuedGuidanceArrows());
		}
	}

	private IEnumerator ProcessQueuedGuidanceArrows()
	{
		while (queuedGuidanceTargets.Count > 0)
		{
			while (activeGuidanceArrow != null ||
			      (TransitionManager.Instance != null && TransitionManager.Instance.IsTransitioning) ||
			      guidanceArrowPrefab == null ||
			      playerTransform == null)
			{
				yield return null;
			}

			Vector3 nextTarget = queuedGuidanceTargets.Dequeue();
			ShowGuidanceArrow(nextTarget);

			while (activeGuidanceArrow != null)
				yield return null;
		}

		queuedGuidanceRoutine = null;
	}

	public ChapterData currChapter 			{ get; private set; }
    public MapData     currMap     			{ get; private set; }
    public int         currSectionIndex 		{ get; private set; }
    public SectionData currSection =>
        currMap?.GetSection(currSectionIndex);

	// Track sections that have custom music (e.g., lamp lit rooms)
	private static HashSet<int> sectionsWithCustomMusic = new HashSet<int>();

	public System.Action<int> onSectionEntered;
	public System.Action<ChapterData> onChapterChanged;

    #region Unity callbacks
    void Awake()
    {
        // Enforce singleton
		if (Instance != null && Instance != this)
		{
			Debug.Log($"[GameManager] Duplicate GameManager in scene '{gameObject.scene.name}' detected. Existing instance id={Instance.GetInstanceID()}, duplicate id={GetInstanceID()}");
			// Transfer any scene-local overrides (for example: initial guidance spawn settings)
			// from the scene's GameManager component to the persistent singleton before
			// destroying the duplicate component. This allows per-scene inspector values
			// (spawn-mode, world position, scene object, etc.) to take effect when
			// GameManager is persisted across loads.
			try
			{
				Instance.ApplySceneOverridesFrom(this);
			}
			catch (System.Exception ex)
			{
				Debug.LogWarning($"[GameManager] Failed to apply scene overrides from duplicate: {ex.Message}");
			}

			Destroy(this);
			return;
		}

		// DontDestroyOnLoad only works reliably for root objects.
		if (transform.parent != null)
		{
			transform.SetParent(null, true);
			Debug.Log("[GameManager] Detached from parent before DontDestroyOnLoad to preserve singleton across scene loads.");
		}

        Instance = this;
        DontDestroyOnLoad(gameObject);
		Debug.Log($"[GameManager] Singleton instance initialized. id={GetInstanceID()}, scene='{gameObject.scene.name}'");

        // Set starting state
        if (chapters.Length > 0)
        {
            currChapter = chapters[Mathf.Clamp(startingChapterIndex, 0, chapters.Length - 1)];
            currMap     = currChapter?.GetMap(startingMapIndex);
            currSectionIndex = startingSectionIndex;
        }

		// Initial scene setup (for the first scene before any transitions)
		RebindSceneReferences();

		// Also listen for sceneLoaded so we rebind when scenes are changed
		// (covers manual/editor scene changes and any loads not routed through LoadMapScene).
		SceneManager.sceneLoaded += OnSceneLoaded;
    }

	void OnDestroy()
	{
		if (Instance == this)
			SceneManager.sceneLoaded -= OnSceneLoaded;
	}

	private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
	{
		// Defer rebinding a frame to allow scene objects to finish initialization
		StartCoroutine(DelayedRebindAfterSceneLoad());
	}

	private IEnumerator DelayedRebindAfterSceneLoad()
	{
		yield return null; // wait one frame
		cameraController = null;
		playerTransform = null;
		RebindSceneReferences();
	}

	/// <summary>
	/// Copy per-scene configuration from a scene-local GameManager component into the
	/// persistent singleton instance. This is used when scenes include a GameManager
	/// with inspector-configured fields (for example initial guidance spawn settings),
	/// but the runtime keeps a single persistent GameManager across scenes.
	/// </summary>
	public void ApplySceneOverridesFrom(GameManager sceneManager)
	{
		if (sceneManager == null) return;

		// Scene object references/mappings (critical for correct camera/background alignment per scene)
		cameraController = sceneManager.cameraController;
		playerTransform = sceneManager.playerTransform;
		sectionBackgrounds = sceneManager.sectionBackgrounds != null
			? (SectionBackground[])sceneManager.sectionBackgrounds.Clone()
			: null;

		// Guidance-related overrides
		initialGuidanceExitId = sceneManager.initialGuidanceExitId;
		initialFollowupExitIds = sceneManager.initialFollowupExitIds != null ? (int[])sceneManager.initialFollowupExitIds.Clone() : new int[0];
		activateSceneFollowups = sceneManager.activateSceneFollowups;
		initialFollowupsAfterReturn = sceneManager.initialFollowupsAfterReturn;
		guidanceUseExitTransform = sceneManager.guidanceUseExitTransform;

		initialGuidanceSpawnMode = sceneManager.initialGuidanceSpawnMode;
		initialGuidanceWorldPosition = sceneManager.initialGuidanceWorldPosition;
		initialGuidanceSceneObject = sceneManager.initialGuidanceSceneObject;
		initialGuidanceSectionIndexOverride = sceneManager.initialGuidanceSectionIndexOverride;

		Debug.Log($"[GameManager] Applied scene overrides from scene GameManager (id={sceneManager.GetInstanceID()}).");
	}

    void Start()
    {
        // on the very first frame the game is actually running, 
        // ensure the background music for the current section starts.
        // Awake is often too early for the AudioManager's Mixer to be ready.
        if (currSection != null && AudioManager.Instance != null)
        {
            if (currSection.backgroundMusic != null && currSection.autoPlayMusic)
            {
                Debug.Log($"[GameManager] Start() - Re-triggering initial music: {currSection.backgroundMusic.name}");
                AudioManager.Instance.PlayMusic(currSection.backgroundMusic);
            }
            else if (!currSection.autoPlayMusic)
            {
                AudioManager.Instance.StopMusic();
            }
        }
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
                Debug.LogWarning("[GameManager] No CameraController found in scene. This is normal in the Main Menu, but required in gameplay scenes.");
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
                Debug.LogWarning("[GameManager] No PointAndClickController found in scene. This is normal in the Main Menu, but required in gameplay scenes.");
        }

		// Try to auto-bind per-scene section backgrounds (scan SpriteRenderer names like "section 2 background")
		TryAutoBindSectionBackgrounds();

		// --- Diagnostics: detect common UI / camera problems when starting play from an individual scene ---
		// EventSystem
		EventSystem es = FindObjectOfType<EventSystem>();
		if (es != null)
			Debug.Log($"[GameManager] EventSystem found: '{es.gameObject.name}' (module={es.currentInputModule?.GetType().Name ?? "null"}).");
		else
			Debug.LogWarning("[GameManager] No EventSystem found in scene; UI buttons will not respond.");

		// Canvas checks
		Canvas[] canvases = FindObjectsOfType<Canvas>();
		if (canvases == null || canvases.Length == 0)
		{
			Debug.LogWarning("[GameManager] No Canvas objects found in scene.");
		}
		else
		{
			foreach (var c in canvases)
			{
				GraphicRaycaster gr = c.GetComponent<GraphicRaycaster>();
				CanvasGroup cg = c.GetComponent<CanvasGroup>();
				string camInfo = c.renderMode == RenderMode.ScreenSpaceCamera ? (c.worldCamera != null ? c.worldCamera.name : "(null)") : c.renderMode.ToString();
				Debug.Log($"[GameManager] Canvas '{c.gameObject.name}' mode={c.renderMode} camera={camInfo} GraphicRaycaster={(gr!=null)} CanvasGroupPresent={(cg!=null)}");

				if (gr == null)
					Debug.LogWarning($"[GameManager] Canvas '{c.gameObject.name}' is missing a GraphicRaycaster; UI will not receive clicks.");
				if (c.renderMode == RenderMode.ScreenSpaceCamera && c.worldCamera == null)
					Debug.LogWarning($"[GameManager] Canvas '{c.gameObject.name}' is ScreenSpace-Camera but has no camera assigned.");
				if (cg != null && cg.blocksRaycasts)
					Debug.LogWarning($"[GameManager] CanvasGroup on '{c.gameObject.name}' blocks raycasts; it may prevent UI interaction.");
			}
		}

		// Also enumerate ALL CanvasGroup components in loaded scenes (including inactive) and log their state
		CanvasGroup[] allCanvasGroups = Resources.FindObjectsOfTypeAll<CanvasGroup>();
		foreach (var cgAll in allCanvasGroups)
		{
			if (cgAll == null) continue;
			// skip assets/prefabs not part of loaded scenes
			if (!cgAll.gameObject.scene.isLoaded) continue;
			Debug.Log($"[GameManager] CanvasGroup (scene) '{cgAll.gameObject.name}' activeInHierarchy={cgAll.gameObject.activeInHierarchy} interactable={cgAll.interactable} blocksRaycasts={cgAll.blocksRaycasts} alpha={cgAll.alpha}");
			if (cgAll.blocksRaycasts)
			{
				Debug.LogWarning($"[GameManager] CanvasGroup '{cgAll.gameObject.name}' currently blocks raycasts. This WILL prevent UI clicks.");
			}
		}
    }
    #endregion

	#region Navigation
    /// <summary>
    /// Starts the game from the very beginning (Chapter 1, Map 1).
    /// Typically called by a "Play" button in the Main Menu.
    /// Shows the loading screen while the scene is being loaded.
    /// </summary>
    public void StartGame()
    {
        if (chapters == null || chapters.Length == 0)
        {
            Debug.LogError("[GameManager] Cannot StartGame: No chapters assigned!");
            return;
        }

		// NOTE: Do NOT show the loading screen here if an opening cutscene/video will play.
		// The loading screen can obscure cutscene UI (e.g., Skip button). Loading screen
		// will be shown when the map actually begins loading in GoToMap.

		currChapter = chapters[0];
		if (currChapter == null)
		{
			Debug.LogError("[GameManager] StartGame aborted: chapters[0] is null.");
			return;
		}

		Debug.Log($"[GameManager] StartGame called. chapter='{currChapter.chapName}', openingVideo={(currChapter.openingVideoClip != null)}, openingCutscene='{currChapter.openingCutsceneName}'");

		// Reset one-shot guidance for this StartGame call.
		// We only enable it if an opening cinematic actually starts.
		pendingInitialGuidance = false;
		if (pendingInitialGuidanceRoutine != null)
		{
			StopCoroutine(pendingInitialGuidanceRoutine);
			pendingInitialGuidanceRoutine = null;
		}
		ClearActiveGuidanceArrow();
		ClearQueuedGuidanceRequests();
        
		// If an opening video is assigned, play it first (video takes precedence over sprite cutscenes)
		if (currChapter.openingVideoClip != null && VideoCutsceneManager.Instance != null)
		{
			if (currChapter.openingVideoPlayOnce)
			{
				if (VideoCutsceneManager.Instance.PlayVideoClipOnce(currChapter.openingVideoClip, () => {
					GoToMap(currChapter.DefaultMap, 0, currChapter);
				}))
				{
					// Avoid overlap between menu/current BGM and the opening video's own audio.
					AudioManager.Instance?.StopMusic();
					pendingInitialGuidance = true;
					Debug.Log("[GameManager] Opening video started. Initial guidance armed for post-load spawn.");
					return; // video will call GoToMap when complete
				}

				Debug.Log("[GameManager] Opening video was skipped because it already played once.");
			}
			else
			{
				// Avoid overlap between menu/current BGM and the opening video's own audio.
				AudioManager.Instance?.StopMusic();
				pendingInitialGuidance = true;
				Debug.Log("[GameManager] Opening video started. Initial guidance armed for post-load spawn.");
				VideoCutsceneManager.Instance.PlayVideoClip(currChapter.openingVideoClip, () => {
					GoToMap(currChapter.DefaultMap, 0, currChapter);
				});
				return; // video started and will call GoToMap when complete
			}
		}

		// If an opening sprite-based cutscene is assigned and a CutsceneManager is present, play it first.
		if (!string.IsNullOrWhiteSpace(currChapter.openingCutsceneName) && CutsceneManager.Instance != null)
		{
			// If configured to only play once, use the helper that respects 'played' state.
			if (currChapter.openingCutscenePlayOnce)
			{
				if (CutsceneManager.Instance.PlayCutsceneByNameOnce(currChapter.openingCutsceneName, () => {
					GoToMap(currChapter.DefaultMap, 0, currChapter);
				}))
				{
					pendingInitialGuidance = true;
					Debug.Log("[GameManager] Opening sprite cutscene started. Initial guidance armed for post-load spawn.");
					return; // cutscene will call GoToMap when complete
				}

				Debug.Log("[GameManager] Opening sprite cutscene was skipped because it already played once.");
			}
			else
			{
				if (CutsceneManager.Instance.PlayCutsceneByName(currChapter.openingCutsceneName, () => {
					GoToMap(currChapter.DefaultMap, 0, currChapter);
				}))
				{
					pendingInitialGuidance = true;
					Debug.Log("[GameManager] Opening sprite cutscene started. Initial guidance armed for post-load spawn.");
					return; // cutscene will call GoToMap when complete
				}

				Debug.LogWarning("[GameManager] Opening sprite cutscene was requested but failed to start.");
			}
		}

		// Fallback: no cutscene available or it was skipped. Keep guidance enabled.
		pendingInitialGuidance = true;
		Debug.Log("[GameManager] No opening cinematic started. Initial guidance armed for post-load spawn.");
		GoToMap(currChapter.DefaultMap, 0, currChapter);
    }
	#endregion

	#region Section Transitions
	/// <summary>
	/// Resolves an exit id (searched across all sections in the map) to a destination section index.
	/// Falls back to treating the value as a direct section index for backward compatibility.
	/// exitWalkTarget: the position the player walked to at the exit (used for transition walk animation).
	/// </summary>
	public void GoToSectionFromExitOrSection(int exitOrSectionIndex, Vector2 exitWalkTarget = default)
	{
		if (TryResolveExitIdInMap(exitOrSectionIndex, out SectionData.ExitLink exitLink))
		{
			string resolvedExitName = string.IsNullOrWhiteSpace(exitLink.exitName) ? "(unnamed)" : exitLink.exitName;
			Debug.Log($"[GameManager] Resolved exit id {exitOrSectionIndex} ('{resolvedExitName}') -> section index {exitLink.targetSectionIndex} in map '{currMap?.mapName}'.");
			
			// Look up destination exit walk target if specified
			Vector2 destinationExitWalkTarget = default;
			if (exitLink.destinationExitId != 0)
			{
				destinationExitWalkTarget = GetExitWalkTarget(exitLink.targetSectionIndex, exitLink.destinationExitId);
				Debug.Log($"[GameManager] Destination exit id {exitLink.destinationExitId} walk target: {destinationExitWalkTarget}");
			}
			
			GoToSection(exitLink.targetSectionIndex, exitLink.useOverrideSpawnPoint, exitLink.overrideSpawnPoint, exitWalkTarget, destinationExitWalkTarget);
			return;
		}

		Debug.Log($"[GameManager] Exit id {exitOrSectionIndex} not found in map '{currMap?.mapName}'. Treating as direct section index (backward compatible).");
		GoToSection(exitOrSectionIndex, false, default, exitWalkTarget);
	}

	/// <summary>
	/// Move the player to another section within the currently loaded map.
	/// No scene load — just reposition the player and pan the camera.
	/// </summary>
	public void GoToSection(int sectionIndex)
	{
		GoToSection(sectionIndex, false, default, default, default);
	}

	private void GoToSection(int sectionIndex, bool useOverrideSpawnPoint, Vector2 overrideSpawnPoint, Vector2 sourceExitWalkTarget = default, Vector2 destinationExitWalkTarget = default)
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

		// Determine movement direction for the transition walk (right if section index increases)
		Vector2 movementDir = (sectionIndex >= currSectionIndex) ? Vector2.right : Vector2.left;
		currSectionIndex = sectionIndex;

		// Guidance should disappear as soon as a section transition begins.
		ClearActiveGuidanceArrow();
		bool startTransitionFromBlack = forceNextSectionTransitionStartBlack;
		forceNextSectionTransitionStartBlack = false;

		// If TransitionManager exists, play the cinematic walk
		if (TransitionManager.Instance != null && playerTransform != null)
		{
			StartCoroutine(TransitionManager.Instance.PlayWalkTransition(playerTransform, movementDir, () => {
				// This code runs while the screen is black
				// If the section has a video assigned, trigger its cutscene while still black
				bool triggerCutsceneDuringBlack = forceStartCutscenesDuringBlack || (section != null && section.onEnterVideoClip != null);
				SnapToSectionImmediate(section, useOverrideSpawnPoint, overrideSpawnPoint, triggerCutsceneDuringBlack);
			}, () => {
				// This callback runs AFTER the walk finishes and screen fades back in
				// Restore the player to their actual spawn point
				RestorePlayerToSpawnPoint(section, useOverrideSpawnPoint, overrideSpawnPoint);
				
				// If we did NOT already trigger the cutscene while black, trigger it now.
				if (section == null || section.onEnterVideoClip == null)
				{
					TriggerSectionCutscene(section);
				}
			}, sourceExitWalkTarget, destinationExitWalkTarget, startTransitionFromBlack));
		}
		else
		{
			// Fallback: simple snap if TransitionManager is missing
			SnapToSectionImmediate(section, useOverrideSpawnPoint, overrideSpawnPoint);
		}
	}

	/// <summary>
	/// Snaps the camera and player to the new section immediately.
	/// Used as the "middle" part of the walk transition or as a fallback.
	/// triggerCutscene: if true, will play cutscenes on section entry. Set to false during transitions (cutscene plays after transition completes).
	/// </summary>
	private void SnapToSectionImmediate(SectionData section, bool useOverride, Vector2 overridePos, bool triggerCutscene = true)
	{
		// Determine background (if any) before moving the camera so we can fit first
		SpriteRenderer bgForSection = null;
		if (cameraController != null)
			bgForSection = FindBackgroundForSection(currSectionIndex);

		// Fallback: if no explicit mapping exists, try to find any background sprite in the scene
		// whose name contains "background" and choose the one nearest this section's spawn point.
		if (bgForSection == null && cameraController != null)
		{
			bgForSection = FindAnyBackgroundFallbackForSection(section, currSectionIndex);
			if (bgForSection != null)
				Debug.Log($"[GameManager] Using fallback scene background '{bgForSection.gameObject.name}' for section {currSectionIndex}.");
		}

		// 2. Teleport Player
		if (playerTransform != null)
		{
			Vector2 spawnPoint = useOverride ? overridePos : section.spawnPoint;
			
		// Stop existing movement
			PointAndClickController controller = playerTransform.GetComponent<PointAndClickController>();
			if (controller != null)
			{
				controller.StopMovement();
				controller.ResetPerspectiveScale();
			}

			// Physical teleport
			playerTransform.position = new Vector3(spawnPoint.x, spawnPoint.y, playerTransform.position.z);
			
			Rigidbody2D rb = playerTransform.GetComponent<Rigidbody2D>();
			if (rb != null)
			{
				rb.position = spawnPoint;
				rb.linearVelocity = Vector2.zero;
				rb.angularVelocity = 0f;
				rb.Sleep();
			}
		}

		// 3. Background Fitting and Camera Move
		if (cameraController != null)
		{
			if (bgForSection != null)
			{
				// Fit the background first (wait until sprite is ready), then move the camera.
				StartCoroutine(FitBackgroundThenMove(section, currSectionIndex, bgForSection, 0f, true));
				Debug.Log($"[GameManager] scheduled FitThenMove for section index {currSectionIndex} (mapped background).");
			}
			else
			{
				Debug.Log($"[GameManager] No mapped background for section index {currSectionIndex}.");
				cameraController.backgroundToFit = null;
				cameraController.ClearFittedBackground();
				cameraController.MoveToSection(section);
			}
		}
		else
		{
			Debug.LogError("[GameManager] cameraController is null — camera will NOT pan. Did you assign it in the Inspector or is it from a stale scene?");
		}


		// 3. Background Music
		if (AudioManager.Instance != null)
		{
			// Skip music if this section has custom music (e.g., lamp is lit)
			if (sectionsWithCustomMusic.Contains(currSectionIndex))
			{
				Debug.Log($"[GameManager] Section {currSectionIndex} has custom music. Skipping autoPlayMusic.");
			}
			// Only play music if BOTH autoPlayMusic is enabled AND backgroundMusic is assigned
			else if (section.autoPlayMusic && section.backgroundMusic != null)
			{
				AudioManager.Instance.PlayMusic(section.backgroundMusic);
				Debug.Log($"[GameManager] Playing background music for section: {section.backgroundMusic.name}");
			}
			else
			{
				// Always stop music when autoPlayMusic is disabled OR no music is assigned
				AudioManager.Instance.StopMusic();
				Debug.Log($"[GameManager] Section has autoPlayMusic disabled or no backgroundMusic assigned. Stopping all music.");
			}
		}

		// 4. Fire Events
		Debug.Log($"[GameManager] Entered section: {section.sectionName}");
		onSectionEntered?.Invoke(currSectionIndex);

		// If initial guidance requested followups, trigger them at the configured timing.
		if (initialGuidanceFollowupsPending && initialGuidanceDestinationSection >= 0)
		{
			if (currSectionIndex == initialGuidanceDestinationSection)
			{
				initialGuidanceDestinationVisited = true;

				// Optional immediate mode: show followups when first destination is reached.
				if (!initialFollowupsAfterReturn)
				{
					ActivatePendingFollowups();
				}
			}
			else if (initialFollowupsAfterReturn && initialGuidanceDestinationVisited && initialGuidanceSourceSection >= 0 && currSectionIndex == initialGuidanceSourceSection)
			{
				// Requested mode: show followups when the player returns to where initial guidance started.
				ActivatePendingFollowups();
			}
		}

		// 5. Trigger Cutscene (if assigned and not yet played)
		// NOTE: During transitions, cutscenes are triggered AFTER the black screen fades away
		if (triggerCutscene)
		{
			TriggerSectionCutscene(section);
		}
	}

	/// <summary>
	/// Triggers section-entry cinematics (video and/or sprite cutscenes).
	/// Called after the section is fully loaded and visible.
	/// </summary>
	private void TriggerSectionCutscene(SectionData section)
	{
		if (section == null)
			return;

		bool hasOnEnterVideo = section.onEnterVideoClip != null;
		bool hasOnEnterCutscenes = section.onEnterCutsceneNames != null && section.onEnterCutsceneNames.Length > 0;
		if (!hasOnEnterVideo && !hasOnEnterCutscenes)
			return;

		System.Action playOnEnterCutscenes = () =>
		{
			if (!hasOnEnterCutscenes)
				return;

			if (CutsceneManager.Instance == null)
			{
				Debug.LogWarning("[GameManager] TriggerSectionCutscene: CutsceneManager.Instance is null!");
				return;
			}

			Debug.Log($"[GameManager] Triggering {section.onEnterCutsceneNames.Length} cutscene(s) for section '{section.sectionName}'.");
			CutsceneManager.Instance.PlayCutsceneSequence(section.onEnterCutsceneNames, section.cutscenePlayOnce);
		};

		// Determine action to run after the on-enter video finishes
		System.Action afterVideoAction;
		if (section.onEnterVideoReturnSectionIndex >= 0)
		{
			int targetSectionIndex = section.onEnterVideoReturnSectionIndex;
			afterVideoAction = () => {
				forceNextSectionTransitionStartBlack = true;
				GoToSection(targetSectionIndex);
			};
		}
		else if (section.onEnterVideoReturnToPreviousSection)
		{
			afterVideoAction = () => {
				forceNextSectionTransitionStartBlack = true;
				GoToPreviousSection();
			};
		}
		else
		{
			afterVideoAction = playOnEnterCutscenes;
		}

		if (!hasOnEnterVideo)
		{
			playOnEnterCutscenes();
			return;
		}

		if (VideoCutsceneManager.Instance == null)
		{
			Debug.LogWarning("[GameManager] TriggerSectionCutscene: VideoCutsceneManager.Instance is null!");
			afterVideoAction();
			return;
		}

		Debug.Log($"[GameManager] Triggering on-enter video for section '{section.sectionName}': {section.onEnterVideoClip.name}");

		if (section.onEnterVideoPlayOnce)
		{
			bool videoStarted = VideoCutsceneManager.Instance.PlayVideoClipOnce(section.onEnterVideoClip, () =>
			{
				afterVideoAction();
			});

			if (!videoStarted)
			{
				afterVideoAction();
			}
		}
		else
		{
			VideoCutsceneManager.Instance.PlayVideoClip(section.onEnterVideoClip, () =>
			{
				afterVideoAction();
			});
		}
	}
	#endregion

	/// <summary>
	/// Restores the player to their actual spawn point after the transition walk finishes.
	/// Called after the screen fades back in.
	/// </summary>
	private void RestorePlayerToSpawnPoint(SectionData section, bool useOverride, Vector2 overridePos)
	{
		if (playerTransform == null) return;

		Vector2 spawnPoint = useOverride ? overridePos : section.spawnPoint;
		playerTransform.position = new Vector3(spawnPoint.x, spawnPoint.y, playerTransform.position.z);

		Rigidbody2D rb = playerTransform.GetComponent<Rigidbody2D>();
		if (rb != null)
		{
			rb.position = spawnPoint;
			rb.linearVelocity = Vector2.zero;
		}

		Debug.Log($"[GameManager] Player restored to spawn point: {spawnPoint}");
	}

	#region Map Transitions
	public void GoToMap(MapData targetMap, int sectionIndex = 0, ChapterData targetChapter = null, Vector2 exitWalkTarget = default)
	{
		if (targetMap == null) return;

		Debug.Log($"[GameManager] GoToMap called. sceneName='{targetMap.sceneName}', requestedSection={sectionIndex}, pendingInitialGuidance={pendingInitialGuidance}");

		ClearActiveGuidanceArrow();
		ClearQueuedGuidanceRequests();
		if (pendingInitialGuidanceRoutine != null)
		{
			StopCoroutine(pendingInitialGuidanceRoutine);
			pendingInitialGuidanceRoutine = null;
		}

		// Show loading screen now that we're about to begin a map load.
		if (LoadingScreenManager.Instance != null)
			LoadingScreenManager.Instance.ShowLoadingScreen();

		if (targetChapter != null && targetChapter != currChapter)
		{
			currChapter = targetChapter;
			onChapterChanged?.Invoke(currChapter);
		}
		currMap = targetMap;

		// Clamp the requested sectionIndex to the targetMap's section range before storing and using it.
		int clampedIndex = sectionIndex;
		if (targetMap.sections != null && targetMap.sections.Length > 0)
			clampedIndex = Mathf.Clamp(sectionIndex, 0, targetMap.sections.Length - 1);

		currSectionIndex = clampedIndex;

		StartCoroutine(LoadMapScene(targetMap.sceneName, clampedIndex, exitWalkTarget));
	}

	private IEnumerator LoadMapScene(string sceneName, int sectionIndex, Vector2 exitWalkTarget = default)
	{
		Debug.Log($"[GameManager] LoadMapScene started. scene='{sceneName}', sectionIndex={sectionIndex}");

		// Give the loading screen a moment to fully fade in before the scene swap
		AsyncOperation op = SceneManager.LoadSceneAsync(sceneName);
		op.allowSceneActivation = false;

		// Wait for scene to be ready (90% loaded) AND a minimum display time
		float minDisplayTime = 2.0f;
		float timer = 0f;
		while (timer < minDisplayTime || op.progress < 0.9f)
		{
			timer += Time.deltaTime;
			if (op.progress >= 0.9f && timer >= minDisplayTime)
				break;
			yield return null;
		}

		// Now let the scene actually switch
		op.allowSceneActivation = true;
		
		// Wait for the scene load to complete, with a fallback timeout
		float sceneActivationTimeout = 10f;
		float activationTimer = 0f;
		while (!op.isDone && activationTimer < sceneActivationTimeout)
		{
			activationTimer += Time.deltaTime;
			yield return null;
		}
		
		if (!op.isDone)
		{
			Debug.LogWarning($"[GameManager] Scene '{sceneName}' did not fully activate within {sceneActivationTimeout} seconds. Proceeding anyway.");
		}

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

		// After re-binding and the scene is ready, place the player in the requested section.
		// IMPORTANT: On fresh map loads, `sectionIndex` is a direct section index, not an exit id.
		// Using GoToSectionFromExitOrSection here can misinterpret values like 0/1 as exit ids
		// (if ExitLink.exitId matches), causing camera/player to land in the wrong section.
		GoToSection(sectionIndex, false, default, exitWalkTarget);

		// Hide the loading screen now that the scene is loaded and ready
		if (LoadingScreenManager.Instance != null)
			LoadingScreenManager.Instance.OnSceneLoadComplete();

		// If this map load came from StartGame and an opening cinematic actually played,
		// wait for the initial section transition (if any) to finish before showing guidance.
		if (pendingInitialGuidance)
		{
			pendingInitialGuidance = false;
			if (pendingInitialGuidanceRoutine != null)
			{
				StopCoroutine(pendingInitialGuidanceRoutine);
			}
			pendingInitialGuidanceRoutine = StartCoroutine(SpawnInitialGuidanceWhenReady(sectionIndex));
		}
	}
	#endregion

	private IEnumerator SpawnInitialGuidanceWhenReady(int sectionIndex)
	{
		while (TransitionManager.Instance != null && TransitionManager.Instance.IsTransitioning)
			yield return null;

		SpawnInitialGuidanceArrow(sectionIndex);
		pendingInitialGuidanceRoutine = null;
	}

	private void SpawnInitialGuidanceArrow(int sectionIndex)
	{
		if (currMap == null || currMap.sections == null || currMap.sections.Length == 0)
			return;

		int clampedSectionIndex = Mathf.Clamp(sectionIndex, 0, currMap.sections.Length - 1);
		initialGuidanceSourceSection = clampedSectionIndex;
		initialGuidanceDestinationVisited = false;

		// Optional override path from inspector settings.
		if (TrySpawnInitialGuidanceByConfiguredMode(clampedSectionIndex))
			return;

		// If a specific exit id was set in the inspector, prefer that.
		if (initialGuidanceExitId != -1)
		{
			Vector2 exitTarget = GetExitWalkTarget(clampedSectionIndex, initialGuidanceExitId);
			if (exitTarget != default)
			{
				// Resolve destination section index for this exit id (if present in currMap)
				if (TryResolveExitIdInMap(initialGuidanceExitId, out SectionData.ExitLink el))
				{
					initialGuidanceDestinationSection = el.targetSectionIndex;
					initialGuidanceFollowupsPending = activateSceneFollowups || (initialFollowupExitIds != null && initialFollowupExitIds.Length > 0 && initialGuidanceDestinationSection >= 0);
					Debug.Log($"[GameManager] Initial guidance destination resolved to section {initialGuidanceDestinationSection}. followupsPending={initialGuidanceFollowupsPending}");
				}
				// Anchor the initial arrow at the exit location instead of above the player
				ShowGuidanceArrow(new Vector3(exitTarget.x, exitTarget.y, 0f), true, true);
				return;
			}

			Debug.LogWarning($"[GameManager] initialGuidanceExitId {initialGuidanceExitId} not found in scene; searching for nearest SectionExit...");

			// Try to find any SectionExit in the scene and target the nearest one as a fallback.
			SectionExit[] allExits = FindObjectsOfType<SectionExit>();
			if (allExits != null && allExits.Length > 0)
			{
				SectionExit nearest = null;
				float bestDist = float.MaxValue;
				Vector3 playerPos = playerTransform != null ? playerTransform.position : Vector3.zero;
				foreach (var ex in allExits)
				{
					float d = Vector2.Distance(new Vector2(playerPos.x, playerPos.y), ex.walkTarget);
					if (d < bestDist)
					{
						bestDist = d;
						nearest = ex;
					}
				}

				if (nearest != null)
				{
					// Try to resolve destination section index for nearest exit
					if (TryResolveExitIdInMap(nearest.exitId, out SectionData.ExitLink nel))
					{
						initialGuidanceDestinationSection = nel.targetSectionIndex;
						initialGuidanceFollowupsPending = activateSceneFollowups || (initialFollowupExitIds != null && initialFollowupExitIds.Length > 0 && initialGuidanceDestinationSection >= 0);
						Debug.Log($"[GameManager] Initial guidance fallback destination resolved to section {initialGuidanceDestinationSection}. followupsPending={initialGuidanceFollowupsPending}");
					}
					Debug.Log($"[GameManager] Falling back to nearest SectionExit id={nearest.exitId} at {nearest.walkTarget}");
					ShowGuidanceArrow(new Vector3(nearest.walkTarget.x, nearest.walkTarget.y, 0f), true, true);
					return;
				}
			}
			else
			{
				Debug.LogWarning("[GameManager] No SectionExit objects found in scene to fallback to.");
			}
		}

		// Compute a reasonable target: the next section's spawnPoint if available, otherwise a point to the right.
		Vector3 guidanceTarget = Vector3.zero;
		int nextIndex = Mathf.Clamp(clampedSectionIndex + 1, 0, currMap.sections.Length - 1);
		if (nextIndex != clampedSectionIndex && currMap.sections[nextIndex] != null)
		{
			SectionData nextSection = currMap.sections[nextIndex];
			guidanceTarget = new Vector3(nextSection.spawnPoint.x, nextSection.spawnPoint.y, 0f);
			initialGuidanceDestinationSection = nextIndex;
			initialGuidanceFollowupsPending = activateSceneFollowups || (initialFollowupExitIds != null && initialFollowupExitIds.Length > 0);
			Debug.Log($"[GameManager] Initial guidance default destination set to next section {initialGuidanceDestinationSection}. followupsPending={initialGuidanceFollowupsPending}");
		}
		else if (playerTransform != null)
		{
			// Fallback: point a few units to the right of the player.
			guidanceTarget = playerTransform.position + new Vector3(5f, 0f, 0f);
		}

			ShowGuidanceArrow(guidanceTarget, true, true);
	}

	private bool TrySpawnInitialGuidanceByConfiguredMode(int currentSectionIndex)
	{
		bool followupsConfigured = activateSceneFollowups || (initialFollowupExitIds != null && initialFollowupExitIds.Length > 0);

		switch (initialGuidanceSpawnMode)
		{
			case InitialGuidanceSpawnMode.Default:
				return false;

			case InitialGuidanceSpawnMode.WorldPosition:
				initialGuidanceDestinationSection = -1;
				initialGuidanceFollowupsPending = followupsConfigured;
				Debug.Log($"[GameManager] Spawning initial guidance using WorldPosition mode at {initialGuidanceWorldPosition}. followupsPending={initialGuidanceFollowupsPending}");
				ShowGuidanceArrow(initialGuidanceWorldPosition, true, true);
				return true;

			case InitialGuidanceSpawnMode.SceneObject:
				if (initialGuidanceSceneObject == null)
				{
					Debug.LogWarning("[GameManager] InitialGuidanceSpawnMode is SceneObject, but no initialGuidanceSceneObject is assigned. Falling back to Default mode.");
					return false;
				}

				initialGuidanceDestinationSection = -1;
				initialGuidanceFollowupsPending = followupsConfigured;
				Debug.Log($"[GameManager] Spawning initial guidance using SceneObject mode at '{initialGuidanceSceneObject.name}' ({initialGuidanceSceneObject.transform.position}). followupsPending={initialGuidanceFollowupsPending}");
				ShowGuidanceArrow(initialGuidanceSceneObject.transform.position, true, true);
				return true;

			case InitialGuidanceSpawnMode.SectionSpawn:
				if (currMap.sections == null || currMap.sections.Length == 0)
					return false;

				int targetSectionIndex = initialGuidanceSectionIndexOverride >= 0
					? Mathf.Clamp(initialGuidanceSectionIndexOverride, 0, currMap.sections.Length - 1)
					: Mathf.Clamp(currentSectionIndex + 1, 0, currMap.sections.Length - 1);

				SectionData targetSection = currMap.GetSection(targetSectionIndex);
				if (targetSection == null)
				{
					Debug.LogWarning($"[GameManager] SectionSpawn mode target section {targetSectionIndex} is null. Falling back to Default mode.");
					return false;
				}

				initialGuidanceDestinationSection = targetSectionIndex;
				if (initialFollowupsAfterReturn && initialGuidanceDestinationSection == currentSectionIndex)
				{
					// Returning to the same section cannot satisfy the return condition; force immediate followups.
					initialGuidanceDestinationSection = -1;
				}
				initialGuidanceFollowupsPending = followupsConfigured;

				Vector3 sectionTarget = new Vector3(targetSection.spawnPoint.x, targetSection.spawnPoint.y, 0f);
				Debug.Log($"[GameManager] Spawning initial guidance using SectionSpawn mode. targetSection={targetSectionIndex}, target={sectionTarget}, followupsPending={initialGuidanceFollowupsPending}");
				ShowGuidanceArrow(sectionTarget, true, true);
				return true;

			default:
				return false;
		}
	}

	private void ClearActiveGuidanceArrow()
	{
		bool wasInitial = activeGuidanceIsInitial;
		if (activeGuidanceArrow != null)
		{
			Destroy(activeGuidanceArrow);
			activeGuidanceArrow = null;
		}
		activeGuidanceIsInitial = false;

		// If the arrow we just cleared was the initial opening arrow and followups are pending,
		// activate scene markers or spawn configured follow-ups.
		if (wasInitial && initialGuidanceFollowupsPending && ShouldTriggerFollowupsOnInitialArrowDisappear())
		{
			// Defer activation until any section on-enter video is playing (if present),
			// otherwise fall back to immediate activation after a short timeout.
			if (followupActivationWaitRoutine != null)
			{
				StopCoroutine(followupActivationWaitRoutine);
				followupActivationWaitRoutine = null;
			}
			followupActivationWaitRoutine = StartCoroutine(WaitForVideoThenActivateFollowups(followupActivationVideoWaitTimeout));
		}
	}

	private void ClearQueuedGuidanceRequests()
	{
		queuedGuidanceTargets.Clear();
		if (queuedGuidanceRoutine != null)
		{
			StopCoroutine(queuedGuidanceRoutine);
			queuedGuidanceRoutine = null;
		}
	}

	/// <summary>
	/// Called by GuidanceArrow when it is destroyed so the GameManager can handle followups.
	/// </summary>
	public void NotifyGuidanceArrowDestroyed(bool wasInitial)
	{
		Debug.Log($"[GameManager] NotifyGuidanceArrowDestroyed called. wasInitial={wasInitial}, pendingFollowups={initialGuidanceFollowupsPending}");
		// Clear internal reference if it still points to the destroyed object
		activeGuidanceArrow = null;
		// Reset initial flag
		activeGuidanceIsInitial = false;

		// Only trigger followups if this was the initial arrow AND followups are pending
		if (wasInitial && initialGuidanceFollowupsPending && ShouldTriggerFollowupsOnInitialArrowDisappear())
		{
			if (followupActivationWaitRoutine != null)
			{
				StopCoroutine(followupActivationWaitRoutine);
				followupActivationWaitRoutine = null;
			}
			followupActivationWaitRoutine = StartCoroutine(WaitForVideoThenActivateFollowups(followupActivationVideoWaitTimeout));
		}
	}

	private bool ShouldTriggerFollowupsOnInitialArrowDisappear()
	{
		if (!initialFollowupsAfterReturn)
			return true;

		// If we cannot track source/destination, fallback to immediate behavior.
		return initialGuidanceDestinationSection < 0 || initialGuidanceSourceSection < 0;
	}

	private void ActivatePendingFollowups()
	{
		if (!initialGuidanceFollowupsPending)
			return;

		// If a wait coroutine is running to delay activation until a video starts, stop it now.
		if (followupActivationWaitRoutine != null)
		{
			StopCoroutine(followupActivationWaitRoutine);
			followupActivationWaitRoutine = null;
		}

		initialGuidanceFollowupsPending = false;
		initialGuidanceDestinationSection = -1;
		initialGuidanceSourceSection = -1;
		initialGuidanceDestinationVisited = false;
		ActivateSceneFollowupMarkersOrSpawn();
	}

	private int ActivateSceneFollowupMarkers()
	{
		// Use Resources.FindObjectsOfTypeAll to include inactive scene objects (prefabs/assets filtered out below)
		SceneGuidanceMarker[] markers = Resources.FindObjectsOfTypeAll<SceneGuidanceMarker>();
		Debug.Log($"[GameManager] ActivateSceneFollowupMarkers: discovered {markers.Length} SceneGuidanceMarker(s) (including inactive). Filtering to loaded scene objects...");
		int activated = 0;
		foreach (var m in markers)
		{
			if (m == null) continue;
			if (!m.activateOnInitialArrowDisappear) continue;
			// Skip markers that belong to assets/prefabs not in the loaded scene
			if (!m.gameObject.scene.isLoaded) continue;
			m.ActivateSprite();
			activated++;
		}
		Debug.Log($"[GameManager] ActivateSceneFollowupMarkers: activated {activated} marker(s) in loaded scenes.");
		return activated;
	}

	private void ActivateSceneFollowupMarkersOrSpawn()
	{
		Debug.Log("[GameManager] ActivateSceneFollowupMarkersOrSpawn called.");
		if (activateSceneFollowups)
		{
			int activatedMarkers = ActivateSceneFollowupMarkers();
			if (activatedMarkers > 0)
				return;

			Debug.LogWarning("[GameManager] No SceneGuidanceMarker objects were activated. Falling back to initialFollowupExitIds.");
		}

		if (initialFollowupExitIds != null && initialFollowupExitIds.Length > 0)
		{
			foreach (int followExitId in initialFollowupExitIds)
			{
				ShowGuidanceToExit(followExitId);
			}
		}
	}

	private IEnumerator WaitForVideoThenActivateFollowups(float timeout)
	{
		float timer = 0f;
		bool activated = false;
		while (timer < timeout)
		{
			if (VideoCutsceneManager.Instance != null)
			{
				if (VideoCutsceneManager.Instance.IsPlaying)
				{
					if (VideoCutsceneManager.Instance.HasPresentedFirstFrame)
					{
						ActivatePendingFollowups();
						activated = true;
						break;
					}
				}
			}
			timer += Time.deltaTime;
			yield return null;
		}

		if (!activated)
		{
			// Fallback: no suitable video started within timeout — activate followups now.
			ActivatePendingFollowups();
		}

		followupActivationWaitRoutine = null;
	}

	/// <summary>
	/// Mark a section as having custom music (e.g., a puzzle solved).
	/// This prevents the section's autoPlayMusic from restarting when re-entering.
	/// </summary>
	public static void MarkSectionWithCustomMusic(int sectionIndex)
	{
		sectionsWithCustomMusic.Add(sectionIndex);
		Debug.Log($"[GameManager] Section {sectionIndex} marked as having custom music.");
	}

	#region Helpers
	/// <summary>
	/// Get the walk target of an exit in a specific section.
	/// Used to find the destination exit's walk target for transitions.
	/// </summary>
	private Vector2 GetExitWalkTarget(int sectionIndex, int exitId)
	{
		if (currMap == null || currMap.sections == null || sectionIndex < 0 || sectionIndex >= currMap.sections.Length)
			return default;

		SectionData section = currMap.sections[sectionIndex];
		if (section == null || section.exits == null)
			return default;

		// Find the SectionExit component in the scene with this exitId in this section
		// For now, we'll search through all SectionExits
		SectionExit[] allExits = FindObjectsOfType<SectionExit>();
		SectionExit best = null;
		float bestDist = float.MaxValue;
		Vector3 playerPos = playerTransform != null ? playerTransform.position : Vector3.zero;
		foreach (SectionExit exit in allExits)
		{
			if (exit.exitId != exitId) continue;

			float d = Vector2.Distance(new Vector2(playerPos.x, playerPos.y), exit.walkTarget);
			if (d < bestDist)
			{
				bestDist = d;
				best = exit;
			}
		}

		if (best != null)
		{
			if (guidanceUseExitTransform)
			{
				Vector3 p = best.transform.position;
				Debug.Log($"[GameManager] Found exit {exitId} transform position (nearest): {p}");
				return new Vector2(p.x, p.y);
			}

			Debug.Log($"[GameManager] Found exit {exitId} walk target (nearest): {best.walkTarget}");
			return best.walkTarget;
		}

		Debug.LogWarning($"[GameManager] Could not find exit id {exitId} in section {sectionIndex} to get walk target.");
		return default;
	}

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

	/// <summary>
	/// Fallback helper: search the scene for any SpriteRenderer whose name contains "background"
	/// and return the one nearest the section's spawn point. This helps scenes that do not
	/// provide an explicit mapping in `sectionBackgrounds` but do include background sprites.
	/// </summary>
	private SpriteRenderer FindAnyBackgroundFallbackForSection(SectionData section, int sectionIndex)
	{
		try
		{
			SpriteRenderer[] allSR = FindObjectsOfType<SpriteRenderer>();
			if (allSR == null || allSR.Length == 0) return null;
			SpriteRenderer best = null;
			float bestDist = float.MaxValue;
			Vector2 target = section != null ? section.spawnPoint : Vector2.zero;
			foreach (var sr in allSR)
			{
				if (sr == null || sr.sprite == null) continue;
				string name = sr.gameObject.name.ToLowerInvariant();
				if (!name.Contains("background")) continue;
				float dist = Mathf.Abs(sr.transform.position.x - target.x) + Mathf.Abs(sr.transform.position.y - target.y);
				if (dist < bestDist)
				{
					bestDist = dist;
					best = sr;
				}
			}
			if (best != null)
				Debug.Log($"[GameManager] Fallback background found for section {sectionIndex}: {best.gameObject.name}");
			return best;
		}
		catch (System.Exception e)
		{
			Debug.LogWarning($"[GameManager] FindAnyBackgroundFallbackForSection failed: {e.Message}");
			return null;
		}
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

	/// <summary>
	/// Fit the provided background (waiting a few frames if necessary) and then move the camera to the section.
	/// This ensures the camera uses the background-derived bounds when the section's cameraBounds are empty.
	/// </summary>
	private IEnumerator FitBackgroundThenMove(SectionData section, int sectionIndex, SpriteRenderer bg, float padding, bool center)
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
			Debug.LogWarning("[GameManager] Deferred FitThenMove aborted: background became null.");
			yield break;
		}

		if (cameraController == null)
		{
			Debug.LogWarning("[GameManager] Deferred FitThenMove aborted: cameraController is null.");
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

		Debug.Log($"[GameManager] cameraController.FitToBackground executed (blocking then move). bgSize={bgWorldSize}, sectionBounds={sectionBounds}, overrideBounds={bgLargerThanSection}");

		// Now that the camera controller has background bounds available, move to the section.
		cameraController.MoveToSection(section);
	}

	/// <summary>
	/// Returns true when the current `sectionBackgrounds` already contains usable references
	/// for the currently loaded scene.
	/// </summary>
	private bool HasUsableSceneBackgroundMappings()
	{
		if (sectionBackgrounds == null || sectionBackgrounds.Length == 0)
			return false;

		for (int i = 0; i < sectionBackgrounds.Length; i++)
		{
			SpriteRenderer bg = sectionBackgrounds[i].background;
			if (bg == null) continue;
			if (!bg.gameObject.scene.isLoaded) continue;
			return true;
		}

		return false;
	}

	/// <summary>
	/// Attempt to auto-bind section backgrounds from scene SpriteRenderers whose names
	/// follow the pattern "section {index} background" (case-insensitive).
	/// This helps the persistent GameManager rebind scene-specific background objects after scene loads.
	/// </summary>
	private void TryAutoBindSectionBackgrounds()
	{
		try
		{
			// If the scene already provided valid mappings (inspector-assigned), keep them.
			if (HasUsableSceneBackgroundMappings())
			{
				Debug.Log("[GameManager] Keeping existing scene sectionBackgrounds mapping.");
				return;
			}

			SpriteRenderer[] allSR = FindObjectsOfType<SpriteRenderer>();
			if (allSR == null || allSR.Length == 0)
				return;

			List<SectionBackground> newList = new List<SectionBackground>();
			Regex r = new Regex(@"\bsection\s*(\d+)\b", RegexOptions.IgnoreCase);
			foreach (var sr in allSR)
			{
				if (sr == null) continue;
				string name = sr.gameObject.name;
				if (string.IsNullOrEmpty(name)) continue;
				if (!name.ToLowerInvariant().Contains("background")) continue;
				var m = r.Match(name);
				if (m.Success)
				{
					if (int.TryParse(m.Groups[1].Value, out int idx))
					{
						SectionBackground sb = new SectionBackground { sectionIndex = idx, background = sr };
						newList.Add(sb);
						Debug.Log($"[GameManager] Auto-bound section background: section {idx} -> {sr.gameObject.name}");
					}
				}
			}

			if (newList.Count > 0)
			{
				// Some scenes name backgrounds as "section 1", "section 2", ... (1-based).
				// Convert to 0-based only when it clearly matches current map section count.
				int expectedSections = (currMap != null && currMap.sections != null) ? currMap.sections.Length : -1;
				bool hasZeroIndex = false;
				int minIndex = int.MaxValue;
				int maxIndex = int.MinValue;
				for (int i = 0; i < newList.Count; i++)
				{
					int idx = newList[i].sectionIndex;
					if (idx == 0) hasZeroIndex = true;
					if (idx < minIndex) minIndex = idx;
					if (idx > maxIndex) maxIndex = idx;
				}

				bool looksOneBased = !hasZeroIndex && minIndex == 1 && expectedSections > 0 && maxIndex <= expectedSections;
				if (looksOneBased)
				{
					for (int i = 0; i < newList.Count; i++)
					{
						SectionBackground sb = newList[i];
						sb.sectionIndex -= 1;
						newList[i] = sb;
					}
					Debug.Log("[GameManager] Auto-bound background names looked 1-based; shifted indices to 0-based.");
				}

				sectionBackgrounds = newList.ToArray();
				Debug.Log($"[GameManager] Rebound {newList.Count} section background(s) from scene.");
			}
		}
		catch (System.Exception e)
		{
			Debug.LogWarning($"[GameManager] TryAutoBindSectionBackgrounds failed: {e.Message}");
		}
	}

	private void ShowGuidanceArrow(Vector3 targetWorldPos)
	{
		ShowGuidanceArrow(targetWorldPos, false, false);
	}

	private void ShowGuidanceArrow(Vector3 targetWorldPos, bool markAsInitial)
	{
		ShowGuidanceArrow(targetWorldPos, markAsInitial, false);
	}

	private void ShowGuidanceArrow(Vector3 targetWorldPos, bool markAsInitial, bool anchorAtTarget)
	{
		Debug.Log($"[GameManager] ShowGuidanceArrow called. target={targetWorldPos}, markAsInitial={markAsInitial}, anchorAtTarget={anchorAtTarget}, guidanceArrowPrefab={(guidanceArrowPrefab!=null?guidanceArrowPrefab.name:"null")}, playerTransform={(playerTransform!=null?playerTransform.name:"null")}" );
		if (guidanceArrowPrefab == null)
		{
			Debug.LogWarning("[GameManager] guidanceArrowPrefab is not assigned; cannot show guidance arrow.");
			return;
		}

		// Remove existing arrow if any
		ClearActiveGuidanceArrow();

		if (playerTransform == null)
		{
			Debug.LogWarning("[GameManager] playerTransform is null; cannot attach guidance arrow.");
			return;
		}

		activeGuidanceArrow = Instantiate(guidanceArrowPrefab);
		GuidanceArrow ga = activeGuidanceArrow.GetComponent<GuidanceArrow>();
		if (ga != null)
		{
			ga.isInitial = markAsInitial;
			ga.Initialize(playerTransform, targetWorldPos, anchorAtTarget);
			Debug.Log($"[GameManager] Guidance arrow instantiated: {activeGuidanceArrow.name}, isInitial={ga.isInitial}");
		}
		else
		{
			Debug.LogWarning("[GameManager] guidanceArrowPrefab does not have a GuidanceArrow component.");
		}

		activeGuidanceIsInitial = markAsInitial;
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

	/// <summary>
	/// Move the player back to the previous section in the current map (if available).
	/// Useful for video-driven scenes where characters walk back to the previous area.
	/// </summary>
	public void GoToPreviousSection()
	{
		if (currMap == null)
		{
			Debug.LogWarning("[GameManager] GoToPreviousSection: currMap is null!");
			return;
		}

		int prevIndex = currSectionIndex - 1;
		if (prevIndex < 0)
		{
			Debug.LogWarning("[GameManager] GoToPreviousSection: already at first section.");
			return;
		}

		GoToSection(prevIndex);
	}
	#endregion

}