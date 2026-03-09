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
    [Header("Pull Settings")]
    [Tooltip("Seconds the player must hold to complete the pull.")]
    public float holdDuration = 1.5f;

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

    private float   holdTimer   = 0f;
    private bool    isHolding   = false;
    private bool    completed   = false;

    // Per-frame input 
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

    // Called by PointAndClickController
    /// <summary>Call when the player presses the mouse button over this obstacle.</summary>
    public void StartHold()
    {
        if (completed) return;
        isHolding = true;
        holdTimer = 0f;
        progressUI?.Show(true);
    }

    /// <summary>Call when the mouse button is released or cursor leaves.</summary>
    public void CancelHold()
    {
        isHolding = false;
        holdTimer = 0f;
        progressUI?.SetProgress(0f);
        progressUI?.Show(false);
    }

    // Logic

    private void CompletePull()
    {
        completed = true;
        isHolding = false;
        progressUI?.Show(false);

        // Hide the obstacle immediately
        gameObject.SetActive(false);

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
}
