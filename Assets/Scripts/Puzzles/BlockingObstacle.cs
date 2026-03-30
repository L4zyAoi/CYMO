using System.Collections;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// A world object that blocks the path and is removed by holding the mouse button.
///
/// SETUP:
///  1. Set Layer to "Interactable".
/// 
///  2. Add a Collider2D covering the visual obstacle.
/// 
///  3. Assign all fields in the Inspector.
/// 
///  4. Create a disabled WalkableArea for the newly opened path section and
///     assign it to pathExtension — it will be enabled on completion.
/// </summary>
public class BlockingObstacle : MonoBehaviour
{
    #region Variables
    [Header("Pull Settings")]
    [Tooltip("Seconds the player must hold to complete the pull.")]
    public float holdDuration = 1.5f;

    [Tooltip("Which section index (in the MapData) this puzzle is located in. " +
             "Used to trigger the entry animation only when the player arrives.")]
    public int mySectionIndex = 0;

    [Header("Mode")]
    [Tooltip("If true, pulling is disabled. Instead, the player must drag an item onto this obstacle " +
             "(via an ItemTarget component) to trigger the auto-fall sequence.")]
    public bool requiresItem = false;

    [Header("On Pulled — Scene References")]
    [Tooltip("The WalkableArea that opens up once the obstacle is removed.")]
    public WalkableArea pathExtension;

    [Tooltip("GameObject (Animator/ParticleSystem) for the falling debris. Starts disabled.")]
    public GameObject debrisObject;

    [Tooltip("The PickupItem that appears at the hole after the pull. Starts disabled.")]
    public PickupItem questItem;

    [Tooltip("Delay in seconds between the obstacle disappearing and the debris/item appearing.")]
    public float debrisDelay = 0.3f;
    
    [Tooltip("If true, the quest item/badge will be automatically added to inventory when it spawns.")]
    public bool autoCollectBadge = true;

    [Tooltip("Fired after the full pull sequence completes.")]
    public UnityEvent OnPulled;

    [Header("UI")]
    [Tooltip("The PullProgressUI that shows the hold progress. Assign if using visual feedback.")]
    public PullProgressUI progressUI;

    [Header("Animation (Optional)")]
    public Animator animator;
    [Tooltip("Delay in seconds before the entry animation plays when entering the section. " +
             "Recommend matching TransitionManager.blackScreenDuration + TransitionManager.fadeDuration (e.g., 0.8 + 0.5 = 1.3s)")]
    public float entryAnimationDelay = 1.3f;
    [Tooltip("Trigger name for the first entry animation.")]
    public string triggerEntry = "Entry";
    [Tooltip("Trigger name for the pulling animation.")]
    public string triggerPull = "Pull";
    [Tooltip("Trigger name for the hide/release animation.")]
    public string triggerHide = "Hide";
    [Tooltip("Trigger name for returning to idle (used when releasing Puzzle 1).")]
    public string triggerIdle = "Idle";
    [Tooltip("Trigger name for the auto-fall animation (used in requiresItem mode).")]
    public string triggerFall = "Fall";

    [Header("Audio")]
    public AudioClip pullSFX;
    public AudioClip successSFX;
    [Tooltip("Sound when the hide/release animation plays (item mode only).")]
    public AudioClip hideAnimationSFX;

    private float holdTimer = 0f;
    private bool isHolding = false;
    private bool completed = false;
    #endregion

    #region Unity Callbacks
    void OnEnable()
    {
        // GameManager might not exist in awake/enable during map load,
        // so we start a coroutine to subscribe once it's ready.
        StartCoroutine(SubscribeToGameManager());
    }

    void OnDisable()
    {
        if (GameManager.Instance != null)
            GameManager.Instance.onSectionEntered -= OnSectionEntered;
    }
    #endregion

    private IEnumerator SubscribeToGameManager()
    {
        yield return new WaitUntil(() => GameManager.Instance != null);
        GameManager.Instance.onSectionEntered += OnSectionEntered;

        // If the player is already in this section when the object enables
        // (e.g. they spawned here directly), trigger entry immediately.
        if (GameManager.Instance.currSectionIndex == mySectionIndex)
            OnSectionEntered(mySectionIndex);
    }

    void Update()
    {
        if (completed) return;

        if (isHolding)
        {
            if (Input.GetMouseButton(0))
            {
                holdTimer += Time.deltaTime;
                progressUI?.SetProgress(holdTimer / holdDuration);

                if (holdTimer >= holdDuration)
                    CompletePull();
            }
            else
            {
                // Released early — reset
                CancelHold();
            }
        }
    }

    /// <summary>
    /// Call when the player presses the mouse button over this obstacle.
    /// Called by PointAndClickController
    /// </summary>
    public void StartHold()
    {
        if (completed) return;

        Debug.Log($"[BlockingObstacle] StartHold triggered on '{gameObject.name}' (holdDuration: {holdDuration})");

        // Both modes play the pull animation as visual feedback
        if (animator != null && !string.IsNullOrEmpty(triggerPull))
            animator.SetTrigger(triggerPull);

        // Play looping pull sound for BOTH puzzle types
        if (pullSFX != null && AudioManager.Instance != null)
        {
            AudioManager.Instance.StartLoopingSFX(pullSFX);
        }

        // Item mode: just animation, no hold timer
        if (requiresItem) return;

        // Pull mode: enable hold timer
        isHolding = true;
        holdTimer = 0f;
        progressUI?.Show(true);
    }

    /// <summary>
    /// Call when the mouse button is released or cursor leaves.
    /// </summary>
    public void CancelHold()
    {
        if (completed) return;

        isHolding = false;
        holdTimer = 0f;
        progressUI?.SetProgress(0f);
        progressUI?.Show(false);

        if (pullSFX != null) AudioManager.Instance?.StopLoopingSFX(pullSFX);

        if (animator != null)
        {
            if (requiresItem)
            {
                // Puzzle 2: requires specific "hide" animation
                if (!string.IsNullOrEmpty(triggerHide))
                    animator.SetTrigger(triggerHide);
                
                // Play hide animation sound
                if (hideAnimationSFX != null && AudioManager.Instance != null)
                {
                    AudioManager.Instance.PlaySFX(hideAnimationSFX);
                }
            }
            else
            {
                // Puzzle 1: returns to idle
                if (!string.IsNullOrEmpty(triggerIdle))
                    animator.SetTrigger(triggerIdle);
            }
        }
    }

    // Section Entry
    private void OnSectionEntered(int sectionIndex)
    {
        if (completed) return;

        // If leaving OUR section, reset animator to idle
        if (sectionIndex != mySectionIndex)
        {
            ResetAnimatorToIdle();
            return;
        }

        // Entering OUR section - play entry animation
        if (animator != null && !string.IsNullOrEmpty(triggerEntry))
        {
            // Start coroutine to delay the entry animation
            StartCoroutine(PlayEntryAnimationDelayed());
        }
        
        // Keep subscribed so entry animation re-triggers if player re-enters
        // (Only unsubscribe when puzzle is completed)
    }

    private void ResetAnimatorToIdle()
    {
        if (animator != null && !string.IsNullOrEmpty(triggerIdle) && !completed)
        {
            animator.SetTrigger(triggerIdle);
            Debug.Log($"[BlockingObstacle] Animator reset to idle for section {mySectionIndex}");
        }
    }

    private IEnumerator PlayEntryAnimationDelayed()
    {
        if (entryAnimationDelay > 0f)
        {
            Debug.Log($"[BlockingObstacle] Entry animation delayed by {entryAnimationDelay}s");
            yield return new WaitForSeconds(entryAnimationDelay);
        }

        if (animator != null && !string.IsNullOrEmpty(triggerEntry))
        {
            animator.SetTrigger(triggerEntry);
            Debug.Log($"[BlockingObstacle] Entry animation triggered for section {mySectionIndex}");
        }
    }

    #region Logic
    private void CompletePull()
    {
        Debug.Log($"[BlockingObstacle] CompletePull reached on '{gameObject.name}'. Starting debris sequence.");
        completed = true;
        isHolding = false;
        progressUI?.Show(false);

        // Unsubscribe from section entry now that puzzle is complete
        if (GameManager.Instance != null)
        {
            GameManager.Instance.onSectionEntered -= OnSectionEntered;
            Debug.Log("[BlockingObstacle] Unsubscribed from section entry (puzzle complete).");
        }

        if (pullSFX != null)
        {
            if (AudioManager.Instance != null)
            {
                Debug.Log($"[BlockingObstacle] Stopping looping SFX: '{pullSFX.name}'");
                AudioManager.Instance.StopLoopingSFX(pullSFX);
            }
            else
            {
                Debug.LogError("[BlockingObstacle] CompletePull: AudioManager.Instance is NULL!");
            }
        }
        
        if (successSFX != null)
        {
            Debug.Log($"[BlockingObstacle] Attempting to play success SFX: '{successSFX.name}'");
            if (AudioManager.Instance != null)
            {
                Debug.Log("[BlockingObstacle] Calling AudioManager.PlaySFX...");
                AudioManager.Instance.PlaySFX(successSFX);
                Debug.Log("[BlockingObstacle] AudioManager.PlaySFX call completed.");
            }
            else
            {
                Debug.LogError($"[BlockingObstacle] Cannot play successSFX on '{gameObject.name}' because AudioManager.Instance is NULL! Ensure the AudioManager prefab is in your scene.");
            }
        }
        else
        {
            Debug.LogWarning("[BlockingObstacle] CompletePull: successSFX is not assigned!");
        }

        // SAFETY: If the animator was controlling children (like the quest item), 
        // disabling it now prevents it from hiding the badge as it spawns.
        if (animator != null) animator.enabled = false;

        // Hide visually and disable interaction
        SpriteRenderer sr = GetComponent<SpriteRenderer>();
        if (sr != null) sr.enabled = false;

        Collider2D col = GetComponent<Collider2D>();
        if (col != null) col.enabled = false;

        StartCoroutine(SpawnDebrisSequence());
    }

    /// <summary>
    /// Called by an ItemTarget's OnItemUsed event (drag-and-drop).
    /// Plays the fall animation and auto-completes the obstacle.
    /// </summary>
    public void UseItemAndComplete()
    {
        if (completed) return;

        completed = true;
        isHolding = false;
        progressUI?.Show(false);

        // Unsubscribe from section entry now that puzzle is complete
        if (GameManager.Instance != null)
        {
            GameManager.Instance.onSectionEntered -= OnSectionEntered;
            Debug.Log("[BlockingObstacle] Unsubscribed from section entry (puzzle complete).");
        }

        // Disable collider immediately so the player can't interact again
        Collider2D col = GetComponent<Collider2D>();
        if (col != null) col.enabled = false;

        if (successSFX != null)
        {
            Debug.Log($"[BlockingObstacle] UseItemAndComplete: Playing success SFX '{successSFX.name}'");
            if (AudioManager.Instance != null)
            {
                AudioManager.Instance.PlaySFX(successSFX);
                Debug.Log("[BlockingObstacle] PlaySFX call completed.");
            }
            else
            {
                Debug.LogError($"[BlockingObstacle] Cannot play successSFX on '{gameObject.name}' because AudioManager.Instance is NULL!");
            }
        }

        if (animator != null && !string.IsNullOrEmpty(triggerFall))
        {
            animator.SetTrigger(triggerFall);
            // Wait for the fall animation to finish, THEN hide and spawn debris
            StartCoroutine(WaitForFallThenComplete());
        }
        else
        {
            // No animator — hide immediately and spawn debris
            SpriteRenderer sr = GetComponent<SpriteRenderer>();
            if (sr != null) sr.enabled = false;
            StartCoroutine(SpawnDebrisSequence());
        }
    }

    private IEnumerator WaitForFallThenComplete()
    {
        // Wait one frame for the Animator to transition into the Fall state
        yield return null;

        // Now wait for that clip to finish
        AnimatorStateInfo state = animator.GetCurrentAnimatorStateInfo(0);
        yield return new WaitForSeconds(state.length);

        // Animation done — hide the sprite and spawn debris/items
        SpriteRenderer sr = GetComponent<SpriteRenderer>();
        if (sr != null) sr.enabled = false;

        StartCoroutine(SpawnDebrisSequence());
    }

    private IEnumerator SpawnDebrisSequence()
    {
        Debug.Log($"[BlockingObstacle] SpawnDebrisSequence waiting {debrisDelay}s...");
        yield return new WaitForSeconds(debrisDelay);

        // Open the path
        if (pathExtension != null)
        {
            pathExtension.gameObject.SetActive(true);
            Debug.Log("[BlockingObstacle] Path extension enabled.");
        }

        // Play debris animation/particles
        if (debrisObject != null)
        {
            debrisObject.SetActive(true);
            Debug.Log("[BlockingObstacle] Debris object enabled.");
        }

        // Make the quest item collectible
        if (questItem == null)
        {
            // FALLBACK: If the reference is missing, check the children automatically
            questItem = GetComponentInChildren<PickupItem>(true);
            if (questItem != null) 
                Debug.Log($"[BlockingObstacle] Found missing questItem reference automatically in children: '{questItem.name}'");
        }

        if (questItem != null)
        {
            questItem.gameObject.SetActive(true);
            Debug.Log($"[BlockingObstacle] Quest item '{questItem.name}' enabled!");
            
            if (autoCollectBadge)
            {
                Debug.Log($"[BlockingObstacle] Auto-collecting quest item: '{questItem.name}'");
                bool success = questItem.TryPickup();
                if (success)
                    Debug.Log("[BlockingObstacle] Auto-collect successful.");
                else
                    Debug.LogWarning("[BlockingObstacle] Auto-collect failed (perhaps inventory is full?). Player must click manually.");
            }
        }
        else
        {
            Debug.LogWarning("[BlockingObstacle] questItem is NULL! No badge will be dropped.");
        }

        OnPulled?.Invoke();
    }
    #endregion
}
