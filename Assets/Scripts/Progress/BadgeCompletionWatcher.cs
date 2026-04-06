using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Video;

[DisallowMultipleComponent]
public class BadgeCompletionWatcher : MonoBehaviour
{
    [Header("Sprite Cutscene")]
    [Tooltip("Name of the cutscene (as configured in a CutsceneManager) to play when all chapter badges are collected.")]
    public string cutsceneName = "";
    public bool cutscenePlayOnce = true;

    [Header("Video Cutscene")]
    public VideoClip videoClip;
    public bool videoPlayOnce = true;

    [Tooltip("If true and both video and sprite cutscene are assigned, prefer the video.")]
    public bool preferVideo = true;

    [Tooltip("If true, play the cinematic only once per chapter (default).")]
    public bool playOncePerChapter = true;

    [Tooltip("Enable verbose debug logs for troubleshooting.")]
    public bool debugLogs = false;

    [Header("Celebration")]
    [Tooltip("Make the player character play celebration animation when chapter badges complete.")]
    public bool celebratePlayer = true;
    [Tooltip("Make all characters with a PointAndClickController play celebration as well.")]
    public bool celebrateAllCharacters = false;
    [Tooltip("Override duration passed to character celebration (<=0 uses controller default).")]
    public float celebrationDuration = 0f;

    private bool hasPlayedForCurrentChapter = false;

    // Track subscription state so we can safely unsubscribe
    private bool subscribedInventory = false;
    private bool subscribedChapter = false;
    private Coroutine ensureSubsRoutine = null;

    private void OnEnable()
    {
        // Start a short coroutine to ensure we subscribe even if singletons are created slightly later
        if (ensureSubsRoutine == null)
            ensureSubsRoutine = StartCoroutine(EnsureSubscriptions());
    }

    private void OnDisable()
    {
        if (ensureSubsRoutine != null)
        {
            StopCoroutine(ensureSubsRoutine);
            ensureSubsRoutine = null;
        }

        UnsubscribeAll();
    }

    private IEnumerator EnsureSubscriptions()
    {
        const int maxFramesToWait = 120; // ~2 seconds at 60fps
        int waited = 0;

        while (waited < maxFramesToWait)
        {
            if (!subscribedInventory && InventoryManager.Instance != null)
            {
                InventoryManager.Instance.OnQuestInvenChanged += OnQuestInventoryChanged;
                subscribedInventory = true;
                if (debugLogs) Debug.Log("[BadgeCompletionWatcher] Subscribed to InventoryManager.OnQuestInvenChanged");
            }

            if (!subscribedChapter && GameManager.Instance != null)
            {
                GameManager.Instance.onChapterChanged += OnChapterChanged;
                subscribedChapter = true;
                if (debugLogs) Debug.Log("[BadgeCompletionWatcher] Subscribed to GameManager.onChapterChanged");
            }

            // If we've subscribed to at least one manager, stop waiting early.
            if (subscribedInventory || subscribedChapter)
                break;

            waited++;
            yield return null;
        }

        ensureSubsRoutine = null;

        // Do an initial check in case badges were collected before this object enabled
        CheckForCompletion();
    }

    private void UnsubscribeAll()
    {
        if (subscribedInventory && InventoryManager.Instance != null)
        {
            InventoryManager.Instance.OnQuestInvenChanged -= OnQuestInventoryChanged;
            subscribedInventory = false;
        }

        if (subscribedChapter && GameManager.Instance != null)
        {
            GameManager.Instance.onChapterChanged -= OnChapterChanged;
            subscribedChapter = false;
        }
    }

    private void OnChapterChanged(ChapterData newChapter)
    {
        // Reset per-chapter played state
        hasPlayedForCurrentChapter = false;
        if (debugLogs) Debug.Log($"[BadgeCompletionWatcher] Chapter changed to '{newChapter?.chapName ?? "(null)"}' - resetting played flag.");
        CheckForCompletion();
    }

    private void OnQuestInventoryChanged()
    {
        if (debugLogs) Debug.Log("[BadgeCompletionWatcher] Inventory changed - checking completion.");
        CheckForCompletion();
    }

    private void CheckForCompletion()
    {
        if (playOncePerChapter && hasPlayedForCurrentChapter)
        {
            if (debugLogs) Debug.Log("[BadgeCompletionWatcher] Already played for current chapter; skipping.");
            return;
        }

        if (GameManager.Instance == null || GameManager.Instance.currChapter == null)
        {
            if (debugLogs) Debug.LogWarning("[BadgeCompletionWatcher] GameManager or currChapter is null; cannot check completion.");
            return;
        }

        ChapterData chapter = GameManager.Instance.currChapter;
        if (chapter.chapterBadges == null || chapter.chapterBadges.Length == 0)
        {
            if (debugLogs) Debug.Log("[BadgeCompletionWatcher] Current chapter has no badges; nothing to check.");
            return;
        }

        if (InventoryManager.Instance == null)
        {
            if (debugLogs) Debug.LogWarning("[BadgeCompletionWatcher] InventoryManager.Instance is null; cannot check collected badges.");
            return;
        }

        // Verify every badge in the chapter is present in the quest inventory
        foreach (ItemData badge in chapter.chapterBadges)
        {
            if (badge == null) continue;
            if (!InventoryManager.Instance.Contains(badge))
            {
                if (debugLogs) Debug.Log($"[BadgeCompletionWatcher] Missing badge: {badge.itemName}");
                return; // not complete yet
            }
        }

        // All badges collected -> start celebration then cinematic
        if (debugLogs) Debug.Log("[BadgeCompletionWatcher] All badges collected - starting completion sequence.");
        StartCompletionSequence();
    }

    private void TriggerCinematic()
    {
        // Prefer video if configured
        if (preferVideo && videoClip != null && VideoCutsceneManager.Instance != null)
        {
            if (videoPlayOnce)
            {
                bool started = VideoCutsceneManager.Instance.PlayVideoClipOnce(videoClip, OnCinematicComplete);
                if (debugLogs) Debug.Log($"[BadgeCompletionWatcher] Requested video '{videoClip.name}', started={started}");
                return;
            }
            else
            {
                VideoCutsceneManager.Instance.PlayVideoClip(videoClip, OnCinematicComplete);
                if (debugLogs) Debug.Log($"[BadgeCompletionWatcher] Requested video '{videoClip.name}' (may play multiple times).");
                return;
            }
        }

        // Fallback to sprite-based cutscene
        if (!string.IsNullOrWhiteSpace(cutsceneName) && CutsceneManager.Instance != null)
        {
            if (cutscenePlayOnce)
            {
                bool started = CutsceneManager.Instance.PlayCutsceneByNameOnce(cutsceneName, OnCinematicComplete);
                hasPlayedForCurrentChapter = true;
                if (debugLogs) Debug.Log($"[BadgeCompletionWatcher] Requested cutscene '{cutsceneName}', started={started}");
                return;
            }
            else
            {
                bool started = CutsceneManager.Instance.PlayCutsceneByName(cutsceneName, OnCinematicComplete);
                hasPlayedForCurrentChapter = true;
                if (debugLogs) Debug.Log($"[BadgeCompletionWatcher] Requested cutscene '{cutsceneName}', started={started}");
                return;
            }
        }

        Debug.LogWarning("[BadgeCompletionWatcher] No cinematic configured or corresponding manager missing.");
        // Nothing else to do — celebration already played by StartCompletionSequence()
    }

    private void OnCinematicComplete()
    {
        if (debugLogs) Debug.Log("[BadgeCompletionWatcher] Cinematic completed for chapter badges.");
    }

    private void StartCompletionSequence()
    {
        if (playOncePerChapter && hasPlayedForCurrentChapter)
        {
            if (debugLogs) Debug.Log("[BadgeCompletionWatcher] StartCompletionSequence: already played for this chapter.");
            return;
        }

        // Mark as played to avoid duplicate triggers while we wait/play
        hasPlayedForCurrentChapter = true;

        // First, trigger celebrations
        CelebrateCharacters();

        // Determine wait time: prefer explicit watcher override, else player's controller default, else fallback
        float waitTime = celebrationDuration;
        if (waitTime <= 0f)
        {
            if (GameManager.Instance != null && GameManager.Instance.playerTransform != null)
            {
                var pcc = GameManager.Instance.playerTransform.GetComponent<PointAndClickController>();
                if (pcc != null && pcc.celebrationDuration > 0f)
                    waitTime = pcc.celebrationDuration;
            }
        }
        if (waitTime <= 0f)
            waitTime = 1.5f; // sensible default

        if (debugLogs) Debug.Log($"[BadgeCompletionWatcher] Waiting {waitTime:F2}s for celebration before starting cinematic.");

        StartCoroutine(PlayCinematicAfterWait(waitTime));
    }

    private System.Collections.IEnumerator PlayCinematicAfterWait(float waitTime)
    {
        yield return new WaitForSeconds(waitTime);

        // After celebration, attempt to play cinematic (video preferred)
        TriggerCinematic();
    }

    private void CelebrateCharacters()
    {
        if (celebratePlayer)
        {
            if (GameManager.Instance != null && GameManager.Instance.playerTransform != null)
            {
                var pcc = GameManager.Instance.playerTransform.GetComponent<PointAndClickController>();
                if (pcc != null)
                {
                    pcc.PlayCelebration(celebrationDuration);
                    if (debugLogs) Debug.Log("[BadgeCompletionWatcher] Player celebration triggered.");
                }
                else if (debugLogs)
                {
                    Debug.Log("[BadgeCompletionWatcher] PlayerTransform has no PointAndClickController component.");
                }
            }
            else if (debugLogs)
            {
                Debug.Log("[BadgeCompletionWatcher] Cannot celebrate player: GameManager or playerTransform is null.");
            }
        }

        if (celebrateAllCharacters)
        {
            var all = FindObjectsOfType<PointAndClickController>();
            foreach (var c in all)
            {
                try { c.PlayCelebration(celebrationDuration); } catch { }
            }
            if (debugLogs) Debug.Log($"[BadgeCompletionWatcher] Triggered celebration on {all.Length} characters.");
        }
    }
}
