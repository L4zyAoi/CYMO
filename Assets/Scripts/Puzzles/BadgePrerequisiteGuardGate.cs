using System.Collections;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Unlocks a guarded path once all required quest badges are collected.
/// Typical use: assign Badge 1 + Badge 2 as prerequisites, then move guards aside
/// and disable their colliders so Badge 3 becomes accessible.
/// </summary>
[DisallowMultipleComponent]
public class BadgePrerequisiteGuardGate : MonoBehaviour
{
    [Header("Prerequisites")]
    [Tooltip("All badges that must be present in quest inventory before this gate unlocks.")]
    public ItemData[] requiredBadges;

    [Tooltip("Enable verbose logs for setup/debugging.")]
    public bool debugLogs = false;

    [Header("Guard Movement")]
    [Tooltip("Animators for guard characters that should move out of the way on unlock.")]
    public Animator[] guardAnimators;

    [Tooltip("Optional transforms to move aside on unlock (useful when no guard animator is available).")]
    public Transform[] guardsToMove;

    [Tooltip("World-space offset applied to each guard transform when unlocked.")]
    public Vector3 guardMoveOffset = new Vector3(1.6f, 0f, 0f);

    [Tooltip("Duration for the guard move-aside interpolation.")]
    public float guardMoveDuration = 0.45f;

    [Tooltip("Trigger sent to each guard animator when unlocked.")]
    public string unlockTrigger = "move_aside";

    [Tooltip("Colliders that block the player path before unlock.")]
    public Collider2D[] guardBlockingColliders;

    [Tooltip("Delay before disabling blocking colliders to let the move-aside animation read clearly.")]
    public float colliderDisableDelay = 0.6f;

    [Header("Unlock Targets")]
    [Tooltip("Optional objects to enable when gate unlocks (for example: final badge object).")]
    public GameObject[] enableOnUnlock;

    [Tooltip("Fired once when unlock completes.")]
    public UnityEvent OnUnlocked;

    private bool isUnlocked = false;
    private bool isSubscribed = false;
    private Coroutine ensureSubRoutine;
    private Coroutine guardMoveRoutine;

    private void OnEnable()
    {
        if (ensureSubRoutine == null)
            ensureSubRoutine = StartCoroutine(EnsureSubscription());
    }

    private void OnDisable()
    {
        if (ensureSubRoutine != null)
        {
            StopCoroutine(ensureSubRoutine);
            ensureSubRoutine = null;
        }

        Unsubscribe();
    }

    private IEnumerator EnsureSubscription()
    {
        const int maxFramesToWait = 120;
        int waited = 0;

        while (InventoryManager.Instance == null && waited < maxFramesToWait)
        {
            waited++;
            yield return null;
        }

        if (InventoryManager.Instance != null)
        {
            InventoryManager.Instance.OnQuestInvenChanged += OnQuestInventoryChanged;
            isSubscribed = true;
            if (debugLogs) Debug.Log("[BadgePrerequisiteGuardGate] Subscribed to quest inventory changes.");
        }
        else if (debugLogs)
        {
            Debug.LogWarning("[BadgePrerequisiteGuardGate] InventoryManager not found; gate cannot evaluate prerequisites.");
        }

        ensureSubRoutine = null;
        EvaluateUnlock();
    }

    private void Unsubscribe()
    {
        if (!isSubscribed) return;

        if (InventoryManager.Instance != null)
            InventoryManager.Instance.OnQuestInvenChanged -= OnQuestInventoryChanged;

        isSubscribed = false;
    }

    private void OnQuestInventoryChanged()
    {
        EvaluateUnlock();
    }

    [ContextMenu("DEBUG: Evaluate Unlock")]
    public void EvaluateUnlock()
    {
        if (isUnlocked) return;

        if (requiredBadges == null || requiredBadges.Length == 0)
        {
            if (debugLogs)
                Debug.LogWarning("[BadgePrerequisiteGuardGate] No required badges configured.");
            return;
        }

        if (InventoryManager.Instance == null)
        {
            if (debugLogs)
                Debug.LogWarning("[BadgePrerequisiteGuardGate] Cannot evaluate unlock because InventoryManager is missing.");
            return;
        }

        foreach (ItemData badge in requiredBadges)
        {
            if (badge == null) continue;

            if (!InventoryManager.Instance.Contains(badge))
            {
                if (debugLogs)
                    Debug.Log($"[BadgePrerequisiteGuardGate] Waiting for badge: {badge.itemName}");
                return;
            }
        }

        UnlockGate();
    }

    private void UnlockGate()
    {
        if (isUnlocked) return;
        isUnlocked = true;

        TriggerGuardMovement();

        if (colliderDisableDelay > 0f)
            StartCoroutine(DisableCollidersAfterDelay(colliderDisableDelay));
        else
            DisableGuardColliders();

        if (enableOnUnlock != null)
        {
            foreach (GameObject go in enableOnUnlock)
            {
                if (go != null)
                    go.SetActive(true);
            }
        }

        OnUnlocked?.Invoke();

        if (debugLogs)
            Debug.Log("[BadgePrerequisiteGuardGate] Gate unlocked.");
    }

    private void TriggerGuardMovement()
    {
        bool triggeredAnimator = false;

        if (guardAnimators != null)
        {
            foreach (Animator guard in guardAnimators)
            {
                if (guard == null) continue;
                if (string.IsNullOrEmpty(unlockTrigger)) continue;
                guard.SetTrigger(unlockTrigger);
                triggeredAnimator = true;
            }
        }

        // Fallback: move transforms aside directly when no animator trigger was used.
        if (!triggeredAnimator && guardsToMove != null && guardsToMove.Length > 0)
        {
            if (guardMoveRoutine != null)
                StopCoroutine(guardMoveRoutine);
            guardMoveRoutine = StartCoroutine(MoveGuardsAsideRoutine());
        }
    }

    private IEnumerator MoveGuardsAsideRoutine()
    {
        if (guardsToMove == null || guardsToMove.Length == 0)
            yield break;

        Vector3[] startPositions = new Vector3[guardsToMove.Length];
        Vector3[] endPositions = new Vector3[guardsToMove.Length];

        for (int i = 0; i < guardsToMove.Length; i++)
        {
            Transform guard = guardsToMove[i];
            if (guard == null) continue;
            startPositions[i] = guard.position;
            endPositions[i] = guard.position + guardMoveOffset;
        }

        float duration = Mathf.Max(0.01f, guardMoveDuration);
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);

            for (int i = 0; i < guardsToMove.Length; i++)
            {
                Transform guard = guardsToMove[i];
                if (guard == null) continue;
                guard.position = Vector3.Lerp(startPositions[i], endPositions[i], t);
            }

            yield return null;
        }

        guardMoveRoutine = null;
    }

    private IEnumerator DisableCollidersAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);
        DisableGuardColliders();
    }

    private void DisableGuardColliders()
    {
        if (guardBlockingColliders == null) return;

        foreach (Collider2D col in guardBlockingColliders)
        {
            if (col != null)
                col.enabled = false;
        }
    }
}
