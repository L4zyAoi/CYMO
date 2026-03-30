using System;
using System.Collections;
using UnityEngine;

/// <summary>
/// Manages cutscene playback with sprite sequences.
/// Plays character animation cutscenes from sprite sequences.
/// The cutscene can be placed anywhere in the scene (e.g., overlaid on the character).
///
/// SETUP:
///  1. Create an empty GameObject in your scene, name it "CutsceneAnimator"
///  2. Add a SpriteRenderer component to it
///  3. Add a UISequencePlayer component (or use a custom animator)
///  4. Attach this script to the GameObject
///  5. Assign the CutsceneManager script reference
///  6. Create CutsceneData assets for each cutscene in your project
///  7. Call PlayCutscene() when you want to trigger a cutscene
/// </summary>
public class CutsceneManager : MonoBehaviour
{
    public static CutsceneManager Instance { get; private set; }

    [System.Serializable]
    public class CutsceneData
    {
        [Tooltip("Name of this cutscene")]
        public string cutsceneName;
        
        [Tooltip("Sprite frames for this cutscene animation")]
        public Sprite[] frames;
        
        [Tooltip("Frames per second for playback")]
        public float fps = 24f;
        
        [Tooltip("World position to place the cutscene animator (optional)")]
        public Vector2 spawnPosition = Vector2.zero;
        
        [Tooltip("Use spawn position? If false, uses current animator position")]
        public bool useSpawnPosition = false;
        
        [Tooltip("Optional background music for this cutscene")]
        public AudioClip backgroundMusic;
        
        [Tooltip("Should the music loop?")]
        public bool loopMusic = false;
    }

    [Header("Cutscene Animator")]
    [SerializeField] private SpriteRenderer spriteRenderer;
    [SerializeField] private CanvasGroup canvasGroup; // Optional fade effect

    [Header("Cutscene Settings")]
    [SerializeField] private CutsceneData[] cutscenes;

    private bool isPlayingCutscene = false;
    private Action onCutsceneComplete;
    private Coroutine currentCutsceneCoroutine = null;
    private GameObject disabledPlayerDuringCutscene = null;
    
    // Track which cutscenes have already been played
    private static System.Collections.Generic.HashSet<string> playedCutscenes = 
        new System.Collections.Generic.HashSet<string>();

    private void Awake()
    {
        Debug.Log("[CutsceneManager] Awake() called!");
        
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning("[CutsceneManager] Duplicate instance found, destroying this one.");
            Destroy(gameObject);
            return;
        }
        Instance = this;
        Debug.Log("[CutsceneManager] Instance set successfully!");

        // Get SpriteRenderer (required)
        if (spriteRenderer == null)
        {
            spriteRenderer = GetComponent<SpriteRenderer>();
            Debug.Log($"[CutsceneManager] SpriteRenderer auto-found: {(spriteRenderer != null ? "SUCCESS" : "FAILED")}");
        }

        if (spriteRenderer == null)
        {
            Debug.LogError("[CutsceneManager] NO SpriteRenderer found! Attach this to a GameObject with a SpriteRenderer.");
            enabled = false;
            return;
        }

        // Get CanvasGroup if available (for fade effects)
        canvasGroup = GetComponent<CanvasGroup>();
        Debug.Log($"[CutsceneManager] CanvasGroup found: {(canvasGroup != null ? "YES" : "NO")}");
        
        Debug.Log($"[CutsceneManager] Setup complete! Ready to play cutscenes. Current cutscenes: {(cutscenes != null ? cutscenes.Length : 0)}");
    }

    private void Start()
    {
        Debug.Log($"[CutsceneManager] Start() - Instance exists: {Instance != null}");
        
        // Debug: Show camera position
        Camera mainCam = Camera.main;
        if (mainCam != null)
        {
            Debug.Log($"[CutsceneManager] Main Camera position: {mainCam.transform.position}");
        }
        
        // Debug: Show CutsceneAnimator position
        Debug.Log($"[CutsceneManager] CutsceneAnimator position: {transform.position}");
        Debug.Log($"[CutsceneManager] >>> MOVE CutsceneAnimator to match camera view! <<<");
        
        if (cutscenes != null && cutscenes.Length > 0)
        {
            Debug.Log("[CutsceneManager] Assigned cutscenes:");
            for (int i = 0; i < cutscenes.Length; i++)
            {
                Debug.Log($"  [{i}] '{cutscenes[i].cutsceneName}' - Frames: {cutscenes[i].frames.Length}");
            }
        }
    }

    /// <summary>
    /// Play a cutscene by name, but only if it hasn't been played before.
    /// Returns false if cutscene not found or already played.
    /// </summary>
    public bool PlayCutsceneByNameOnce(string cutsceneName, Action onComplete = null)
    {
        // Check if already played
        if (playedCutscenes.Contains(cutsceneName))
        {
            Debug.Log($"[CutsceneManager] Cutscene '{cutsceneName}' already played. Skipping.");
            return false;
        }

        if (PlayCutsceneByName(cutsceneName, () => {
            // Mark as played after completion
            playedCutscenes.Add(cutsceneName);
            onComplete?.Invoke();
        }))
        {
            return true;
        }

        return false;
    }

    /// <summary>
    /// Play a cutscene by name. Returns false if cutscene not found.
    /// </summary>
    public bool PlayCutsceneByName(string cutsceneName, Action onComplete = null)
    {
        Debug.Log($"[CutsceneManager] PlayCutsceneByName('{cutsceneName}') called!");
        
        if (cutscenes == null)
        {
            Debug.LogError("[CutsceneManager] NO CUTSCENES ASSIGNED!");
            return false;
        }

        Debug.Log($"[CutsceneManager] Searching through {cutscenes.Length} cutscenes...");
        
        foreach (var cutscene in cutscenes)
        {
            if (cutscene.cutsceneName == cutsceneName)
            {
                Debug.Log($"[CutsceneManager] FOUND cutscene '{cutsceneName}'! Starting playback...");
                PlayCutscene(cutscene, onComplete);
                return true;
            }
        }

        Debug.LogWarning($"[CutsceneManager] Cutscene '{cutsceneName}' NOT FOUND! Available: {string.Join(", ", System.Linq.Enumerable.Select(cutscenes, c => $"'{c.cutsceneName}'"))}");
        return false;
    }

    /// <summary>
    /// Play a cutscene directly with a CutsceneData object.
    /// </summary>
    public void PlayCutscene(CutsceneData cutsceneData, Action onComplete = null)
    {
        Debug.Log($"[CutsceneManager] PlayCutscene() called for '{cutsceneData?.cutsceneName}'");
        
        if (isPlayingCutscene)
        {
            Debug.LogWarning("[CutsceneManager] Already playing a cutscene! Ignoring request.");
            return;
        }

        if (cutsceneData == null || cutsceneData.frames == null || cutsceneData.frames.Length == 0)
        {
            Debug.LogError("[CutsceneManager] Invalid cutscene data! Frames: " + (cutsceneData?.frames?.Length ?? 0));
            return;
        }

        Debug.Log($"[CutsceneManager] Valid cutscene! Frames: {cutsceneData.frames.Length}, FPS: {cutsceneData.fps}");
        
        onCutsceneComplete = onComplete;
        // Start the cutscene coroutine and keep a handle so we don't stop unrelated coroutines
        if (currentCutsceneCoroutine != null)
        {
            Debug.LogWarning("[CutsceneManager] currentCutsceneCoroutine was not null; stopping it before starting new one.");
            StopCoroutine(currentCutsceneCoroutine);
            currentCutsceneCoroutine = null;
        }

        currentCutsceneCoroutine = StartCoroutine(PlayCutsceneCoroutine(cutsceneData));
    }

    private IEnumerator PlayCutsceneCoroutine(CutsceneData cutsceneData)
    {
        Debug.Log($"[CutsceneManager] PlayCutsceneCoroutine STARTED for '{cutsceneData.cutsceneName}'");
        isPlayingCutscene = true;
        gameObject.SetActive(true);

        // --- NEW: Disable Player ---
        GameObject playerObject = null;
        if (GameManager.Instance != null && GameManager.Instance.playerTransform != null)
        {
            playerObject = GameManager.Instance.playerTransform.gameObject;
            playerObject.SetActive(false);
            disabledPlayerDuringCutscene = playerObject;
            Debug.Log("[CutsceneManager] Player character disabled for cutscene.");
        }

        // Reposition if needed
        if (cutsceneData.useSpawnPosition)
        {
            Debug.Log($"[CutsceneManager] Positioning at {cutsceneData.spawnPosition}");
            transform.position = new Vector3(cutsceneData.spawnPosition.x, cutsceneData.spawnPosition.y, transform.position.z);
        }

        // Make absolutely sure sprite renderer is visible
        if (spriteRenderer != null)
        {
            spriteRenderer.enabled = true;
            Color c = spriteRenderer.color;
            c.a = 1f;
            spriteRenderer.color = c;
            spriteRenderer.sortingOrder = 100; // High sorting order to be on top
            
            Debug.Log($"[CutsceneManager] SpriteRenderer forced visible:" +
                $"\n  enabled: {spriteRenderer.enabled}" +
                $"\n  color: {spriteRenderer.color}" +
                $"\n  sortingOrder: {spriteRenderer.sortingOrder}" +
                $"\n  gameObject.active: {gameObject.activeSelf}" +
                $"\n  position: {transform.position}");
        }

        // Fade in if CanvasGroup exists
        if (canvasGroup != null)
        {
            Debug.Log("[CutsceneManager] Fading in...");
            yield return StartCoroutine(FadeIn(0.2f));
        }
        else
        {
            Debug.Log("[CutsceneManager] No CanvasGroup, setting sprite renderer color to white");
            spriteRenderer.color = Color.white; // Ensure visible
        }

        // Play background music if assigned
        if (cutsceneData.backgroundMusic != null && AudioManager.Instance != null)
        {
            Debug.Log($"[CutsceneManager] Playing music: {cutsceneData.backgroundMusic.name}");
            AudioManager.Instance.PlayMusic(cutsceneData.backgroundMusic);
        }

        // Play the sprite animation
        Debug.Log($"[CutsceneManager] Starting animation with {cutsceneData.frames.Length} frames at {cutsceneData.fps} FPS");
        yield return StartCoroutine(AnimateSpriteSequence(cutsceneData.frames, cutsceneData.fps));

        Debug.Log($"[CutsceneManager] Animation finished for '{cutsceneData.cutsceneName}'");

        // Fade out if CanvasGroup exists
        if (canvasGroup != null)
        {
            Debug.Log("[CutsceneManager] Fading out...");
            yield return StartCoroutine(FadeOut(0.2f));
        }

        // Stop music
        if (cutsceneData.backgroundMusic != null && AudioManager.Instance != null)
        {
            Debug.Log("[CutsceneManager] Stopping music");
            AudioManager.Instance.StopMusic();
        }

        // --- NEW: Re-enable Player ---
        if (disabledPlayerDuringCutscene != null)
        {
            disabledPlayerDuringCutscene.SetActive(true);
            Debug.Log("[CutsceneManager] Player character re-enabled after cutscene.");
            disabledPlayerDuringCutscene = null;
        }

        isPlayingCutscene = false;
        //gameObject.SetActive(false);

        // Clear coroutine handle
        currentCutsceneCoroutine = null;

        Debug.Log($"[CutsceneManager] Playback complete! Calling completion callback...");
        // Fire completion callback
        onCutsceneComplete?.Invoke();
    }

    private IEnumerator AnimateSpriteSequence(Sprite[] frames, float fps)
    {
        if (frames == null || frames.Length == 0)
        {
            Debug.LogWarning("[CutsceneManager] No sprite frames to animate!");
            yield break;
        }

        float frameDuration = 1f / fps;
        int frameIndex = 0;

        while (isPlayingCutscene && frameIndex < frames.Length)
        {
            if (spriteRenderer != null && frames[frameIndex] != null)
            {
                spriteRenderer.sprite = frames[frameIndex];
                frameIndex++;
            }

            yield return new WaitForSeconds(frameDuration);
        }
    }

    /// <summary>
    /// Skip the current cutscene immediately.
    /// </summary>
    public void SkipCutscene()
    {
        if (!isPlayingCutscene) return;

        Debug.Log("[CutsceneManager] Cutscene skipped.");
        // Stop only the active cutscene coroutine (do not stop other coroutines on this object)
        if (currentCutsceneCoroutine != null)
        {
            StopCoroutine(currentCutsceneCoroutine);
            currentCutsceneCoroutine = null;
        }

        if (AudioManager.Instance != null)
            AudioManager.Instance.StopMusic();

        // Re-enable player if we disabled it
        if (disabledPlayerDuringCutscene != null)
        {
            disabledPlayerDuringCutscene.SetActive(true);
            disabledPlayerDuringCutscene = null;
            Debug.Log("[CutsceneManager] Player character re-enabled after skip.");
        }

        isPlayingCutscene = false;
        gameObject.SetActive(false);
        onCutsceneComplete?.Invoke();
    }

    private IEnumerator FadeIn(float duration)
    {
        if (canvasGroup == null) yield break;
        
        canvasGroup.alpha = 0f;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            canvasGroup.alpha = Mathf.Clamp01(elapsed / duration);
            yield return null;
        }

        canvasGroup.alpha = 1f;
    }

    private IEnumerator FadeOut(float duration)
    {
        if (canvasGroup == null) yield break;
        
        canvasGroup.alpha = 1f;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            canvasGroup.alpha = Mathf.Clamp01(1f - (elapsed / duration));
            yield return null;
        }

        canvasGroup.alpha = 0f;
    }

    /// <summary>
    /// Check if a cutscene is currently playing.
    /// </summary>
    public bool IsPlayingCutscene => isPlayingCutscene;

    /// <summary>
    /// Check if a cutscene has already been played.
    /// </summary>
    public bool HasCutsceneBeenPlayed(string cutsceneName)
    {
        return playedCutscenes.Contains(cutsceneName);
    }

    /// <summary>
    /// Manually mark a cutscene as played (useful for debugging or skipping on load).
    /// </summary>
    public void MarkCutsceneAsPlayed(string cutsceneName)
    {
        playedCutscenes.Add(cutsceneName);
    }

    /// <summary>
    /// Clear all played cutscene history (for testing or new game+).
    /// </summary>
    public void ClearCutsceneHistory()
    {
        playedCutscenes.Clear();
        Debug.Log("[CutsceneManager] Cutscene history cleared.");
    }

    /// <summary>
    /// Get a cutscene by name (for manual setup or debugging).
    /// </summary>
    public CutsceneData GetCutsceneByName(string cutsceneName)
    {
        if (cutscenes == null) return null;

        foreach (var cutscene in cutscenes)
        {
            if (cutscene.cutsceneName == cutsceneName)
                return cutscene;
        }

        return null;
    }

    /// <summary>
    /// Play multiple cutscenes sequentially. Each one waits for the previous to finish.
    /// </summary>
    /// <param name="cutsceneNames">Array of cutscene names to play in order.</param>
    /// <param name="playOnce">If true, each cutscene only plays once (skipped if already played).</param>
    /// <param name="onAllComplete">Called after all cutscenes have finished (or been skipped).</param>
    public void PlayCutsceneSequence(string[] cutsceneNames, bool playOnce = true, Action onAllComplete = null)
    {
        if (cutsceneNames == null || cutsceneNames.Length == 0)
        {
            Debug.Log("[CutsceneManager] PlayCutsceneSequence: No cutscenes to play.");
            onAllComplete?.Invoke();
            return;
        }

        StartCoroutine(PlaySequenceCoroutine(cutsceneNames, playOnce, onAllComplete));
    }

    private IEnumerator PlaySequenceCoroutine(string[] cutsceneNames, bool playOnce, Action onAllComplete)
    {
        Debug.Log($"[CutsceneManager] Starting cutscene sequence with {cutsceneNames.Length} cutscene(s)...");

        for (int i = 0; i < cutsceneNames.Length; i++)
        {
            string name = cutsceneNames[i];
            if (string.IsNullOrEmpty(name)) continue;

            // Check if already played (when playOnce is enabled)
            if (playOnce && playedCutscenes.Contains(name))
            {
                Debug.Log($"[CutsceneManager] Sequence [{i}] '{name}' already played. Skipping.");
                continue;
            }

            // Wait until no cutscene is playing (safety for chaining)
            while (isPlayingCutscene)
                yield return null;

            Debug.Log($"[CutsceneManager] Sequence [{i}] Playing '{name}'...");

            bool finished = false;

            if (playOnce)
            {
                PlayCutsceneByNameOnce(name, () => { finished = true; });
            }
            else
            {
                if (!PlayCutsceneByName(name, () => { finished = true; }))
                {
                    Debug.LogWarning($"[CutsceneManager] Sequence [{i}] '{name}' not found. Skipping.");
                    continue;
                }
            }

            // Wait for this cutscene to finish
            while (!finished)
                yield return null;

            Debug.Log($"[CutsceneManager] Sequence [{i}] '{name}' finished.");
        }

        Debug.Log("[CutsceneManager] Cutscene sequence complete.");
		gameObject.SetActive(false);
		onAllComplete?.Invoke();
    }
}
