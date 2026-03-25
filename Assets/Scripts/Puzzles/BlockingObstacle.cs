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

    [Tooltip("Fired after the full pull sequence completes.")]
    public UnityEvent OnPulled;

    [Header("UI")]
    [Tooltip("The PullProgressUI that shows the hold progress. Assign if using visual feedback.")]
    public PullProgressUI progressUI;

    [Header("Animation (Optional)")]
    public Animator animator;
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

        // Both modes play the pull animation as visual feedback
        if (animator != null && !string.IsNullOrEmpty(triggerPull))
            animator.SetTrigger(triggerPull);

        if (requiresItem) return; // visual only — no hold timer in item mode

        isHolding = true;
        holdTimer = 0f;
        progressUI?.Show(true);
    }

    /// <summary>
    /// Call when the mouse button is released or cursor leaves.
    /// </summary>
    public void CancelHold()
    {
        isHolding = false;
        holdTimer = 0f;
        progressUI?.SetProgress(0f);
        progressUI?.Show(false);

        if (animator != null)
        {
            if (requiresItem)
            {
                // Puzzle 2: requires specific "hide" animation
                if (!string.IsNullOrEmpty(triggerHide))
                    animator.SetTrigger(triggerHide);
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

        // Only play the entry animation if the player actually walked into OUR section
        if (sectionIndex == mySectionIndex)
        {
            if (animator != null && !string.IsNullOrEmpty(triggerEntry))
                animator.SetTrigger(triggerEntry);
            
            // Unsubscribe — the entry anim only ever plays once.
            if (GameManager.Instance != null)
                GameManager.Instance.onSectionEntered -= OnSectionEntered;
        }
    }

    #region Logic
    private void CompletePull()
    {
        completed = true;
        isHolding = false;
        progressUI?.Show(false);

        // Hide visually and disable interaction — NOT SetActive(false)!
        // SetActive(false) kills all coroutines on this GameObject before
        // SpawnDebrisSequence() can finish, causing "Coroutine on inactive object".
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

        // Disable collider immediately so the player can't interact again
        Collider2D col = GetComponent<Collider2D>();
        if (col != null) col.enabled = false;

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
        yield return new WaitForSeconds(debrisDelay);

        // Open the path
        if (pathExtension != null)
            pathExtension.gameObject.SetActive(true);

        // Play debris animation/particles
        if (debrisObject != null)
            debrisObject.SetActive(true);

        // Make the quest item collectible
        if (questItem != null)
            questItem.gameObject.SetActive(true);

        OnPulled?.Invoke();
    }
    #endregion
}
