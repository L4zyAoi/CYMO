using System.Collections;
using UnityEngine;
using UnityEngine.Events;

public class MolePuzzle : MonoBehaviour
{
    [Header("Mole Settings")]
    [Tooltip("Assign a disabled mole GameObject (e.g., child) or set a prefab and enable 'usePrefab' to instantiate.")]
    public GameObject moleObject;
    [Tooltip("If true, instantiate 'molePrefab' instead of enabling 'moleObject'.")]
    public bool usePrefab = false;
    public GameObject molePrefab;
    [Tooltip("How many hits are required to collect the mole.")]
    public int hitsRequired = 3;
    [Tooltip("Optional: automatically hide the mole after this many seconds. Set to <= 0 to disable.")]
    public float autoHideTime = 0f;

    [Header("Audio")]
    public AudioClip spawnSFX;
    public AudioClip hitSFX;
    public AudioClip collectSFX;

    [Header("Badge")]
    public ItemData badgeItem;
    [Tooltip("If true, the badge will be automatically added to inventory when collected.")]
    public bool autoCollectBadge = true;

    [Header("Events")]
    public UnityEvent OnBadgeCollected;
    public UnityEvent<ItemData> OnBadgeCollectedWithData;

    [Header("Animation")]
    public Animator animator;
    [Tooltip("Animator trigger played when the mole first appears")]
    public string triggerAppear = "appear";
    [Tooltip("Animator trigger played when the mole hides")]
    public string triggerHide = "hide";
    [Tooltip("Animator trigger played when the mole comes up at a new position")]
    public string triggerComeUp = "come_up";
    [Tooltip("Animator trigger played when the mole is hit")]
    public string triggerHit = "hit";
    [Tooltip("Animator trigger played on successful collection")]
    public string triggerSuccess = "success";
    [Tooltip("Direct state name fallback for appear animation")]
    public string stateAppear = "appear";
    [Tooltip("Direct state name fallback for hide animation")]
    public string stateHide = "hide";
    [Tooltip("Direct state name fallback for come-up animation")]
    public string stateComeUp = "come_up";
    [Tooltip("Direct state name fallback for hit animation")]
    public string stateHit = "been_hit";
    [Tooltip("Direct state name fallback for success animation")]
    public string stateSuccess = "success";
    [Tooltip("If true, force-play animation states directly as a fallback in addition to triggers.")]
    public bool useDirectStateFallback = true;
    [Tooltip("Cross-fade duration for direct state fallback.")]
    public float directStateCrossFade = 0.03f;
    public float appearAnimDuration = 0.5f;
    public float hideAnimDuration = 0.5f;
    [Tooltip("Additional delay after hide animation completes before visuals are disabled (seconds).")]
    public float hidePostDelay = 2f;
    public float comeUpAnimDuration = 0.5f;
    public float hitAnimDuration = 0.1f;
    public float successAnimDuration = 0.7f;

    [Header("Debug")]
    [Tooltip("Enable verbose debug logs for animation/flow diagnostics.")]
    public bool debugLogs = false;

    [Header("Behavior")]
    [Tooltip("If true, keeps popping until collected. Leave true for normal mole gameplay.")]
    public bool loopPops = true;
    [Tooltip("Legacy setting kept for inspector compatibility.")]
    public int minPops = 1;
    [Tooltip("Legacy setting kept for inspector compatibility.")]
    public int maxPops = 3;
    [Tooltip("Minimum time the mole stays up (seconds).")]
    public float minIdleTime = 0.6f;
    [Tooltip("Maximum time the mole stays up (seconds).")]
    public float maxIdleTime = 1.6f;
    [Tooltip("Minimum time the mole stays hidden between pops (seconds).")]
    public float minHiddenTime = 0.2f;
    [Tooltip("Maximum time the mole stays hidden between pops (seconds).")]
    public float maxHiddenTime = 1.2f;

    [Header("Pop Locations")]
    [Tooltip("Optional fixed positions where the mole can come up. If empty, it uses start position or random radius.")]
    public Transform[] popPoints;
    [Tooltip("Optional random radius around start position when no pop points are assigned.")]
    public float randomRadius = 0f;

    private int hitCount = 0;
    private bool isActive = false;
    private GameObject currentInstance;
    private Coroutine popCoroutine;
    private Vector3 startPosition;
    private bool hasStartPosition = false;
    private int lastPopPointIndex = -1;

    private void Awake()
    {
        // Default to hidden so mole doesn't appear until explicitly activated.
        isActive = false;
        hitCount = 0;

        if (!usePrefab && moleObject != null)
        {
            currentInstance = moleObject;
            startPosition = currentInstance.transform.position;
            hasStartPosition = true;
            SetMoleVisibility(false);
        }
        else
        {
            currentInstance = null;
            hasStartPosition = false;
        }
    }

    public void ActivateMole()
    {
        // Ensure this behaviour can run coroutines even if it was disabled/inactive before.
        if (!gameObject.activeInHierarchy)
            gameObject.SetActive(true);
        if (!enabled)
            enabled = true;

        if (isActive || popCoroutine != null) return;

        hitCount = 0;
        lastPopPointIndex = -1;

        if (usePrefab && molePrefab != null)
        {
            if (currentInstance == null)
                currentInstance = Instantiate(molePrefab, transform.position, Quaternion.identity, transform);

            currentInstance.transform.position = transform.position;
            SetMoleVisibility(false);
        }
        else if (moleObject != null)
        {
            currentInstance = moleObject;
            SetMoleVisibility(false);
        }
        else
        {
            Debug.LogWarning("[MolePuzzle] No moleObject or molePrefab assigned.");
            return;
        }

        startPosition = currentInstance.transform.position;
        hasStartPosition = true;

        if (spawnSFX != null && AudioManager.Instance != null)
            AudioManager.Instance.PlaySFX(spawnSFX);

        // Start scripted flow: appear -> hide -> come_up(same) -> hide -> come_up(other locations).
        popCoroutine = StartCoroutine(PopCycleRoutine());
    }

    private IEnumerator PopCycleRoutine()
    {
        if (currentInstance == null)
        {
            popCoroutine = null;
            yield break;
        }

        // 1) Initial appear at placed location.
        currentInstance.transform.position = startPosition;
        SetMoleVisibility(true);
        isActive = true; // Allow hit during come-up/appear.
        TriggerAnimation(triggerAppear, stateAppear);
        yield return new WaitForSeconds(Mathf.Max(0f, appearAnimDuration));

        if (hitCount >= hitsRequired)
        {
            popCoroutine = null;
            yield break;
        }

        // 2) Delay briefly to allow the appear animation to finish, then hide.
        yield return new WaitForSeconds(1.2f);
        yield return HideCurrentRoutine();

        if (hitCount >= hitsRequired)
        {
            popCoroutine = null;
            yield break;
        }

        // 3) Come up once at the exact start position.
        yield return new WaitForSeconds(Random.Range(minHiddenTime, maxHiddenTime));
        currentInstance.transform.position = startPosition;
        SetMoleVisibility(true);
        isActive = true;
        TriggerAnimation(
            string.IsNullOrEmpty(triggerComeUp) ? triggerAppear : triggerComeUp,
            string.IsNullOrEmpty(stateComeUp) ? stateAppear : stateComeUp
        );

        yield return new WaitForSeconds(Mathf.Max(0f, comeUpAnimDuration));
        yield return WaitUpWindow();

        // 4) Keep cycling through other locations until collected.
        while (hitCount < hitsRequired)
        {
            yield return HideCurrentRoutine();

            if (hitCount >= hitsRequired)
                break;

            yield return new WaitForSeconds(Random.Range(minHiddenTime, maxHiddenTime));

            Vector3 nextPosition = GetNextPopPosition();
            currentInstance.transform.position = nextPosition;
            SetMoleVisibility(true);
            isActive = true;
            TriggerAnimation(
                string.IsNullOrEmpty(triggerComeUp) ? triggerAppear : triggerComeUp,
                string.IsNullOrEmpty(stateComeUp) ? stateAppear : stateComeUp
            );

            yield return new WaitForSeconds(Mathf.Max(0f, comeUpAnimDuration));
            yield return WaitUpWindow();
        }

        if (hitCount < hitsRequired)
        {
            isActive = false;
            SetMoleVisibility(false);
        }

        popCoroutine = null;
    }

    private IEnumerator WaitUpWindow()
    {
        float upTime = (autoHideTime > 0f) ? autoHideTime : Random.Range(minIdleTime, maxIdleTime);
        float elapsed = 0f;

        while (elapsed < upTime && hitCount < hitsRequired)
        {
            elapsed += Time.deltaTime;
            yield return null;
        }
    }

    private IEnumerator HideCurrentRoutine()
    {
        isActive = false;
        if (debugLogs) Debug.Log($"[MolePuzzle] HideCurrentRoutine: triggering hide (duration={hideAnimDuration})");
        TriggerAnimation(triggerHide, stateHide);
        // Wait for the hide animation to complete if possible, otherwise fall back to the configured duration.
        yield return StartCoroutine(WaitForAnimationToComplete(GetTargetAnimator(), stateHide, hideAnimDuration));

        // Extra configurable delay requested by user (default 3s) before disabling visuals.
        if (debugLogs) Debug.Log($"[MolePuzzle] HideCurrentRoutine: hide animation complete; waiting {hidePostDelay}s before hiding visuals.");
        yield return new WaitForSeconds(Mathf.Max(0f, hidePostDelay));

        if (debugLogs) Debug.Log("[MolePuzzle] HideCurrentRoutine: hide delay complete; hiding visuals.");
        SetMoleVisibility(false);
    }

    private void SetMoleVisibility(bool visible)
    {
        if (currentInstance == null)
            return;

        // If the mole object is the same GameObject as this script, keep it active
        // so coroutines can still run; only toggle visuals/colliders instead.
        if (currentInstance == gameObject)
        {
            SpriteRenderer[] spriteRenderers = currentInstance.GetComponentsInChildren<SpriteRenderer>(true);
            foreach (SpriteRenderer sr in spriteRenderers)
                sr.enabled = visible;

            Collider2D[] colliders2D = currentInstance.GetComponentsInChildren<Collider2D>(true);
            foreach (Collider2D col in colliders2D)
                col.enabled = visible;

            Collider[] colliders3D = currentInstance.GetComponentsInChildren<Collider>(true);
            foreach (Collider col in colliders3D)
                col.enabled = visible;

            return;
        }

        if (currentInstance.activeSelf != visible)
            currentInstance.SetActive(visible);
    }

    private Vector3 GetNextPopPosition()
    {
        if (popPoints != null && popPoints.Length > 0)
        {
            if (popPoints.Length == 1)
            {
                Transform onlyPoint = popPoints[0];
                if (onlyPoint != null)
                    return onlyPoint.position;
            }
            else
            {
                int chosenIndex = -1;
                int safety = 0;

                while (safety < 12)
                {
                    int candidate = Random.Range(0, popPoints.Length);
                    if (candidate == lastPopPointIndex)
                    {
                        safety++;
                        continue;
                    }

                    if (popPoints[candidate] == null)
                    {
                        safety++;
                        continue;
                    }

                    chosenIndex = candidate;
                    break;
                }

                if (chosenIndex >= 0)
                {
                    lastPopPointIndex = chosenIndex;
                    return popPoints[chosenIndex].position;
                }
            }
        }

        if (randomRadius > 0f && hasStartPosition)
        {
            Vector2 offset = Random.insideUnitCircle * randomRadius;
            return startPosition + new Vector3(offset.x, offset.y, 0f);
        }

        return hasStartPosition ? startPosition : transform.position;
    }

    private Animator GetTargetAnimator()
    {
        if (animator != null)
            return animator;

        if (currentInstance != null)
        {
            Animator a = currentInstance.GetComponent<Animator>();
            if (a != null) return a;

            // Fallback: animator might be on a child of the current instance
            a = currentInstance.GetComponentInChildren<Animator>(true);
            if (a != null) return a;
        }

        return null;
    }

    private Animator GetTargetAnimatorWithDebug()
    {
        Animator target = GetTargetAnimator();
        if (!debugLogs) return target;

        if (target != null)
        {
            Debug.Log($"[MolePuzzle] Target Animator: '{target.gameObject.name}', enabled={target.enabled}, isActiveAndEnabled={target.isActiveAndEnabled}");
        }
        else
        {
            Debug.Log("[MolePuzzle] Target Animator: null");
        }

        return target;
    }

    private bool HasTriggerParameter(Animator targetAnimator, string triggerName)
    {
        if (targetAnimator == null || string.IsNullOrEmpty(triggerName))
            return false;

        AnimatorControllerParameter[] parameters = targetAnimator.parameters;
        for (int i = 0; i < parameters.Length; i++)
        {
            AnimatorControllerParameter p = parameters[i];
            if (p.type == AnimatorControllerParameterType.Trigger && p.name == triggerName)
                return true;
        }

        return false;
    }

    private bool HasTriggerParameterWithDebug(Animator targetAnimator, string triggerName)
    {
        bool found = HasTriggerParameter(targetAnimator, triggerName);
        if (debugLogs)
        {
            Debug.Log($"[MolePuzzle] HasTriggerParameter('{triggerName}') => {found} (animator={(targetAnimator!=null?targetAnimator.gameObject.name:"null")})");
        }
        return found;
    }

    private IEnumerator WaitForAnimationToComplete(Animator targetAnimator, string stateName, float maxWait)
    {
        if (targetAnimator == null || string.IsNullOrEmpty(stateName))
        {
            if (debugLogs) Debug.Log($"[MolePuzzle] WaitForAnimationToComplete: no animator/state; waiting fallback {maxWait}s");
            yield return new WaitForSeconds(Mathf.Max(0f, maxWait));
            yield break;
        }

        int stateHash = Animator.StringToHash(stateName);
        float elapsed = 0f;
        float timeout = Mathf.Max(0.01f, maxWait * 2f);
        if (debugLogs) Debug.Log($"[MolePuzzle] WaitForAnimationToComplete: waiting for state '{stateName}' (timeout={timeout:F2}s)");
        bool entered = false;

        while (elapsed < timeout)
        {
            if (!targetAnimator.isActiveAndEnabled)
            {
                if (debugLogs) Debug.Log("[MolePuzzle] Animator disabled while waiting; aborting wait.");
                break;
            }

            AnimatorStateInfo st = targetAnimator.GetCurrentAnimatorStateInfo(0);
            if (st.shortNameHash == stateHash || st.fullPathHash == stateHash)
            {
                entered = true;
                if (debugLogs) Debug.Log($"[MolePuzzle] Animator in state '{stateName}', normalizedTime={st.normalizedTime:F2}");
                if (st.normalizedTime >= 1f)
                {
                    if (debugLogs) Debug.Log($"[MolePuzzle] Animator state '{stateName}' finished.");
                    yield break;
                }
            }

            elapsed += Time.deltaTime;
            yield return null;
        }

        // Fallback: if we never saw the state enter, or we timed out, wait the configured duration then proceed.
        if (!entered)
        {
            if (debugLogs) Debug.Log($"[MolePuzzle] WaitForAnimationToComplete: state '{stateName}' not observed; falling back to wait {maxWait}s");
            yield return new WaitForSeconds(Mathf.Max(0f, maxWait));
        }
        else
        {
            if (debugLogs) Debug.Log($"[MolePuzzle] WaitForAnimationToComplete: timed out after {elapsed:F2}s for state '{stateName}'");
        }
    }

    private void TriggerAnimation(string triggerName, string stateName)
    {
        Animator targetAnimator = GetTargetAnimatorWithDebug();
        if (targetAnimator == null)
        {
            if (debugLogs) Debug.Log("[MolePuzzle] TriggerAnimation aborted: no Animator available.");
            return;
        }

        if (!string.IsNullOrEmpty(triggerName) && HasTriggerParameterWithDebug(targetAnimator, triggerName))
        {
            if (debugLogs) Debug.Log($"[MolePuzzle] Setting trigger '{triggerName}' on animator '{targetAnimator.gameObject.name}'");
            targetAnimator.SetTrigger(triggerName);
        }
        else if (!string.IsNullOrEmpty(triggerName) && debugLogs)
        {
            Debug.Log($"[MolePuzzle] Trigger '{triggerName}' not found on animator '{targetAnimator.gameObject.name}'");
        }

        if (useDirectStateFallback && !string.IsNullOrEmpty(stateName) && targetAnimator.isActiveAndEnabled)
        {
            if (debugLogs) Debug.Log($"[MolePuzzle] Cross-fading to state '{stateName}' (crossFade={directStateCrossFade}) on '{targetAnimator.gameObject.name}'");
            try
            {
                targetAnimator.CrossFadeInFixedTime(stateName, Mathf.Max(0f, directStateCrossFade));
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning($"[MolePuzzle] CrossFade failed for state '{stateName}': {ex.Message}");
            }
        }
    }

    public void Hit()
    {
        if (!isActive) return;
        hitCount++;

        if (hitSFX != null && AudioManager.Instance != null)
            AudioManager.Instance.PlaySFX(hitSFX);

        TriggerAnimation(triggerHit, stateHit);

        Debug.Log($"[MolePuzzle] Hit {hitCount}/{hitsRequired}");

        if (hitCount >= hitsRequired)
        {
            // Stop pop cycle so collect routine runs cleanly
            if (popCoroutine != null)
            {
                StopCoroutine(popCoroutine);
                popCoroutine = null;
            }
            StartCoroutine(CollectRoutine());
        }
    }

    private void OnMouseDown()
    {
        // Allow direct clicks on the mole (requires Collider2D/Collider)
        Hit();
    }

    private IEnumerator CollectRoutine()
    {
        isActive = false;

        // Ensure pop-cycle is stopped so success animation plays without interruption
        if (popCoroutine != null)
        {
            StopCoroutine(popCoroutine);
            popCoroutine = null;
        }

        TriggerAnimation(triggerSuccess, stateSuccess);

        if (collectSFX != null && AudioManager.Instance != null)
            AudioManager.Instance.PlaySFX(collectSFX);

        yield return new WaitForSeconds(successAnimDuration);

        SetMoleVisibility(false);

        if (badgeItem != null && autoCollectBadge)
        {
            if (InventoryManager.Instance != null)
            {
                if (badgeItem.isQuestItem)
                {
                    InventoryManager.Instance.AddQuestItem(badgeItem);
                    Debug.Log($"[MolePuzzle] Auto-collected quest badge: '{badgeItem.itemName}'");
                }
                else
                {
                    bool added = InventoryManager.Instance.TryAddItem(badgeItem);
                    if (added)
                        Debug.Log($"[MolePuzzle] Auto-collected item: '{badgeItem.itemName}'");
                    else
                        Debug.LogWarning($"[MolePuzzle] Auto-collect failed (inventory full): '{badgeItem.itemName}'");
                }
            }
            else
            {
                Debug.LogError($"[MolePuzzle] Auto-collect requested but InventoryManager.Instance is NULL! Badge: '{badgeItem.itemName}'");
            }
        }

        OnBadgeCollected?.Invoke();
        if (badgeItem != null)
            OnBadgeCollectedWithData?.Invoke(badgeItem);

        Debug.Log("[MolePuzzle] Badge collected via mole!");
    }

    public void DeactivateMole()
    {
        // Cancel any running pop-cycle
        if (popCoroutine != null)
        {
            StopCoroutine(popCoroutine);
            popCoroutine = null;
        }

        isActive = false;
        TriggerAnimation(triggerHide, stateHide);
        StartCoroutine(DeactivateAfterDelay(hideAnimDuration));
    }

    private IEnumerator DeactivateAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);
        SetMoleVisibility(false);
        isActive = false;
    }

    /// <summary>
    /// Move the mole to a new world position and play the "come up" animation.
    /// </summary>
    public void ComeUpAt(Vector3 worldPosition)
    {
        if (popCoroutine != null)
        {
            StopCoroutine(popCoroutine);
            popCoroutine = null;
        }

        StartCoroutine(ComeUpRoutine(worldPosition));
    }

    private IEnumerator ComeUpRoutine(Vector3 newPos)
    {
        if (currentInstance == null)
            yield break;

        SetMoleVisibility(false);
        yield return new WaitForSeconds(Random.Range(minHiddenTime, maxHiddenTime));

        currentInstance.transform.position = newPos;

        SetMoleVisibility(true);
        isActive = true; // Allow hit while rising
        TriggerAnimation(
            string.IsNullOrEmpty(triggerComeUp) ? triggerAppear : triggerComeUp,
            string.IsNullOrEmpty(stateComeUp) ? stateAppear : stateComeUp
        );

        yield return new WaitForSeconds(Mathf.Max(0f, comeUpAnimDuration));

        if (autoHideTime > 0f)
        {
            yield return new WaitForSeconds(autoHideTime);
            if (isActive)
                DeactivateMole();
        }
    }
}
