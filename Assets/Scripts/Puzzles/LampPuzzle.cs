using System.Collections;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// A 2-stage drag (segmented hold) lamp puzzle.
/// The player must hold the mouse twice (with a release in between) to light the room.
/// Supports 5 specific animation states: unlit_idling, first_interact, second_interact, lit, and lit_idling.
/// </summary>
public class LampPuzzle : MonoBehaviour
{
    #region Variables
    [Header("Visuals - Layers")]
    [Tooltip("The dark overlay SpriteRenderer. Set its Mask Interaction to 'Visible Outside Mask'.")]
    public SpriteRenderer darkBackground;

    [Tooltip("A SpriteMask with a circular gradient sprite, placed at the lamp's position.")]
    public SpriteMask lightMask;

    [Header("Player Tracking")]
    [Tooltip("The player character's SpriteRenderer. Will be tinted dark in this section.")]
    public SpriteRenderer playerSprite;
    [Tooltip("The dark tint color applied to the player.")]
    public Color darkTint = new Color(0.15f, 0.15f, 0.2f, 1f);
    public int currSectionIndex = 0;

    [Header("Mask Scaling")]
    public float startMaskScale = 0.5f;
    [Tooltip("The scale at the end of the first drag.")]
    public float midMaskScale = 4.0f;
    [Tooltip("The final scale that covers the room.")]
    public float maxMaskScale = 15f;

    [Header("2-Stage Drag Timing")]
    [Tooltip("Seconds to hold for the first segment.")]
    public float stage1HoldTime = 2f;
    [Tooltip("Seconds to hold for the second segment.")]
    public float stage2HoldTime = 2f;
    public float fadeDuration = 0.5f;

    [Header("UI & Audio")]
    public PullProgressUI progressUI;
    [Tooltip("Sound while pulling Stage 1.")]
    public AudioClip stage1PullSFX;
    [Tooltip("Sound when Stage 1 completes (first pop/click).")]
    public AudioClip stage1CompleteSFX;
    [Tooltip("Sound while pulling Stage 2.")]
    public AudioClip stage2PullSFX;
    [Tooltip("Sound when the lamp ignites (final success).")]
    public AudioClip igniteSFX;

    [Header("Animations")]
    public Animator animator;
    public string triggerUnlitIdling = "unlit_idling";
    public string boolIsPulling = "isPulling";
    public string intStage = "currentStage";
    public string triggerFirstInteract = "first_interact";
    public string triggerSecondInteract = "second_interact";
    public string triggerLit = "lit";
    public string triggerLitIdling = "lit_idling";

    [Header("Events")]
    public UnityEvent OnLampTurnedOn;
    
    [Header("Bonus Puzzle")]
    [Tooltip("Optional MolePuzzle to spawn after the room lights up.")]
    public MolePuzzle molePuzzle;

    private int currentStage = 0; // 0: Start, 1: Halfway, 2: Finished
    private bool isLit = false;
    private bool isHolding = false;
    private bool playerInSection = false;
    private float holdTimer = 0f;
    private bool needsRelease = false; // Safeguard: player must let go after Stage 1
    #endregion

    #region Unity Callbacks
    void Start()
    {
        if (lightMask != null)
            lightMask.transform.localScale = Vector3.one * startMaskScale;

        // Play initial idling state
        if (animator != null && !string.IsNullOrEmpty(triggerUnlitIdling))
            animator.SetTrigger(triggerUnlitIdling);
    }

    void OnEnable()
    {
        StartCoroutine(SubscribeToGameManager());
    }

    void OnDisable()
    {
        if (GameManager.Instance != null)
            GameManager.Instance.onSectionEntered -= OnSectionEntered;
    }

    private IEnumerator SubscribeToGameManager()
    {
        yield return new WaitUntil(() => GameManager.Instance != null);
        GameManager.Instance.onSectionEntered += OnSectionEntered;

        if (GameManager.Instance.currSectionIndex == currSectionIndex)
            OnSectionEntered(currSectionIndex);
    }

    void Update()
    {
        if (isLit) return;

        HandleHolding();
        HandleProximityLighting();
    }
    #endregion

    #region Interaction Logic
    public void StartHold()
    {
        if (isLit) return;

        // If we just finished Stage 1, we MUST wait for the player to release the mouse
        // before they can start Stage 2.
        if (needsRelease)
        {
            Debug.Log("[LampPuzzle] Waiting for mouse release before Stage 2 can start...");
            return;
        }

        Debug.Log($"[LampPuzzle] StartHold called. Current Stage: {currentStage}");

        // Reset timer for the current segment
        holdTimer = 0f;
        isHolding = true;
        progressUI?.Show(true);

        // Play appropriate pull sound for this stage
        AudioClip pullSound = (currentStage == 0) ? stage1PullSFX : stage2PullSFX;
        if (pullSound != null && AudioManager.Instance != null)
        {
            AudioManager.Instance.StartLoopingSFX(pullSound);
        }

        // Tell the animator we ARE pulling and WHICH stage we are on
        if (animator != null)
        {
            // Safety: Reset any stale triggers before starting the new pull
            if (!string.IsNullOrEmpty(triggerFirstInteract)) animator.ResetTrigger(triggerFirstInteract);
            if (!string.IsNullOrEmpty(triggerSecondInteract)) animator.ResetTrigger(triggerSecondInteract);

            if (!string.IsNullOrEmpty(boolIsPulling)) animator.SetBool(boolIsPulling, true);
            if (!string.IsNullOrEmpty(intStage)) animator.SetInteger(intStage, currentStage);
            
            Debug.Log($"[LampPuzzle] Animator Sync: isPulling=True, currentStage={currentStage}");
        }
    }

    public void CancelHold()
    {
        if (isLit) return;
        
        // If the player releases the mouse, we can now allow the next stage to start
        if (needsRelease)
        {
            needsRelease = false;
            Debug.Log("[LampPuzzle] Mouse released. Stage 2 is now available.");
        }

        if (isHolding)
            Debug.Log($"[LampPuzzle] CancelHold called. Current Stage: {currentStage}");

        isHolding = false;
        holdTimer = 0f;
        progressUI?.SetProgress(0f);
        progressUI?.Show(false);

        // Reset mask back to the START of the current stage
        if (lightMask != null)
        {
            float targetResetScale = (currentStage == 0) ? startMaskScale : midMaskScale;
            lightMask.transform.localScale = Vector3.one * targetResetScale;
        }

        // Stop the appropriate pull sound for this stage
        AudioClip pullSound = (currentStage == 0) ? stage1PullSFX : stage2PullSFX;
        if (pullSound != null && AudioManager.Instance != null)
        {
            AudioManager.Instance.StopLoopingSFX(pullSound);
        }

        // Tell the animator we STOPPED pulling
        if (animator != null)
        {
            if (!string.IsNullOrEmpty(boolIsPulling)) animator.SetBool(boolIsPulling, false);
            if (!string.IsNullOrEmpty(intStage)) animator.SetInteger(intStage, currentStage);
        }
    }

    private void HandleHolding()
    {
        if (!isHolding) return;

        if (Input.GetMouseButton(0))
        {
            holdTimer += Time.deltaTime;

            if (currentStage == 0)
            {
                // Stage 1: Grow to Midpoint
                float t = Mathf.Clamp01(holdTimer / stage1HoldTime);
                progressUI?.SetProgress(t);
                
                if (lightMask != null)
                {
                    float scale = Mathf.Lerp(startMaskScale, midMaskScale, t);
                    lightMask.transform.localScale = Vector3.one * scale;
                }

                if (t >= 1.0f)
                    FinishStageOne();
            }
            else if (currentStage == 1)
            {
                // Stage 2: Grow to Max
                float t = Mathf.Clamp01(holdTimer / stage2HoldTime);
                progressUI?.SetProgress(t);

                if (lightMask != null)
                {
                    float scale = Mathf.Lerp(midMaskScale, maxMaskScale, t);
                    lightMask.transform.localScale = Vector3.one * scale;
                }

                if (t >= 1.0f)
                    CompletePull();
            }
        }
        else
        {
            CancelHold();
        }
    }

    private void FinishStageOne()
    {
        currentStage = 1;
        isHolding = false; 
        needsRelease = true; 
        progressUI?.Show(false);

        // Stop stage 1 pull sound
        if (stage1PullSFX != null && AudioManager.Instance != null)
        {
            AudioManager.Instance.StopLoopingSFX(stage1PullSFX);
        }

        // Play stage 1 complete sound (pop/click)
        if (stage1CompleteSFX != null && AudioManager.Instance != null)
        {
            AudioManager.Instance.PlaySFX(stage1CompleteSFX);
        }

        // STOP pulling animation and play the final stage-one POP
        if (animator != null)
        {
            if (!string.IsNullOrEmpty(boolIsPulling)) animator.SetBool(boolIsPulling, false);
            if (!string.IsNullOrEmpty(intStage)) animator.SetInteger(intStage, currentStage);
            if (!string.IsNullOrEmpty(triggerFirstInteract)) animator.SetTrigger(triggerFirstInteract);
        }

        Debug.Log("[LampPuzzle] STAGE 1 COMPLETE! Please release and hold again for Stage 2.");
    }

    private void CompletePull()
    {
        currentStage = 2;
        isLit = true;
        isHolding = false;
        progressUI?.Show(false);

        // Stop stage 2 pull sound
        if (stage2PullSFX != null && AudioManager.Instance != null)
        {
            AudioManager.Instance.StopLoopingSFX(stage2PullSFX);
        }

        // Play ignite sound
        if (igniteSFX != null && AudioManager.Instance != null)
        {
            AudioManager.Instance.PlaySFX(igniteSFX);
        }

        // START SEQUENTIAL ANIMATION
        StartCoroutine(FullCompletionSequence());
        
        OnLampTurnedOn?.Invoke();
    }

    private IEnumerator FullCompletionSequence()
    {
        Debug.Log("[LampPuzzle] Starting Completion Animation Sequence...");

        // STOP pulling animation and play the final stage-two POP
        if (animator != null)
        {
            if (!string.IsNullOrEmpty(boolIsPulling)) animator.SetBool(boolIsPulling, false);
            if (!string.IsNullOrEmpty(intStage)) animator.SetInteger(intStage, currentStage);
            if (!string.IsNullOrEmpty(triggerSecondInteract)) animator.SetTrigger(triggerSecondInteract);
        }

        // 1. Wait for the interaction to happen
        yield return new WaitForSeconds(0.6f); 

        if (animator != null && !string.IsNullOrEmpty(triggerLit))
        {
            animator.SetTrigger(triggerLit);
            Debug.Log("[LampPuzzle] Triggered: lit");
        }

        // 3. Start the actual visual fade-out of the dark room
        if (darkBackground != null)
        {
            yield return FinalFadeRoutine();
        }

        // Activate mole puzzle after room-light completion, even if fade visuals are not configured.
        TryActivateMolePuzzle();

        // 4. Finally, settle into the looping idling state
        if (animator != null && !string.IsNullOrEmpty(triggerLitIdling))
        {
            animator.SetTrigger(triggerLitIdling);
            Debug.Log("[LampPuzzle] Triggered: lit_idling");
        }
    }

    private IEnumerator FinalFadeRoutine()
    {
        Debug.Log("[LampPuzzle] Starting dark overlay fade...");
        Color color = darkBackground.color;
        float startAlpha = color.a;
        float elapsed = 0f;

        while (elapsed < fadeDuration)
        {
            elapsed += Time.deltaTime;
            color.a = Mathf.Lerp(startAlpha, 0f, elapsed / fadeDuration);
            darkBackground.color = color;
            yield return null;
        }

        color.a = 0f;
        darkBackground.color = color;
        darkBackground.enabled = false;

        if (lightMask != null)
            lightMask.gameObject.SetActive(false);

        // Switch to the final idling animation
        if (animator != null && !string.IsNullOrEmpty(triggerLitIdling))
            animator.SetTrigger(triggerLitIdling);

        Collider2D col = GetComponent<Collider2D>();
        if (col != null) col.enabled = false;

        // Play the section's background music now that the room is lit
        SectionData section = GameManager.Instance?.currSection;
        if (section != null && section.backgroundMusic != null && AudioManager.Instance != null)
        {
            Debug.Log($"[LampPuzzle] Room lit! Playing background music: {section.backgroundMusic.name}");
            AudioManager.Instance.PlayMusic(section.backgroundMusic, loop: false);
            
            // Mark this section as having custom music so autoPlayMusic doesn't interfere
            GameManager.MarkSectionWithCustomMusic(currSectionIndex);
        }
        
    }

    private void TryActivateMolePuzzle()
    {
        MolePuzzle target = molePuzzle;

        if (target == null)
        {
            // Fallback: find one in scene (including inactive) in case inspector reference was missed.
            MolePuzzle[] molePuzzles = FindObjectsByType<MolePuzzle>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            if (molePuzzles.Length > 0)
                target = molePuzzles[0];
            if (target != null)
            {
                molePuzzle = target;
                Debug.Log("[LampPuzzle] Auto-linked MolePuzzle from scene.");
            }
        }

        if (target != null)
        {
            Debug.Log("[LampPuzzle] Activating mole puzzle...");
            target.ActivateMole();
        }
        else
        {
            Debug.LogWarning("[LampPuzzle] MolePuzzle was not found. Assign 'molePuzzle' in inspector or place one in scene.");
        }
    }
    #endregion

    #region Lighting System
    private void OnSectionEntered(int sectionIndex)
    {
        if (isLit) return;

        if (sectionIndex == currSectionIndex)
        {
            playerInSection = true;
        }
        else
        {
            playerInSection = false;
            if (playerSprite != null)
                playerSprite.color = Color.white;
        }
    }

    private void HandleProximityLighting()
    {
        if (!playerInSection || playerSprite == null) return;

        float distance = Vector2.Distance(playerSprite.transform.position, transform.position);
        float currentRadius = lightMask != null ? lightMask.transform.localScale.x : startMaskScale;

        float lightStart = currentRadius * 1.5f; 
        float lightEnd = currentRadius * 0.7f;
        
        float proximityT = Mathf.InverseLerp(lightStart, lightEnd, distance);
        playerSprite.color = Color.Lerp(darkTint, Color.white, proximityT);
    }
    #endregion
}
