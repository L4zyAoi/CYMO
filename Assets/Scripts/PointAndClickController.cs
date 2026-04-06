using System;
using System.Collections;
using UnityEngine;

[System.Serializable]
public class ScalingZone
{
    [Tooltip("Where the character enters this perspective lane (outer edge).")]
    public Transform entryPoint;

    [Tooltip("Deep point of the lane where character reaches smallest size.")]
    public Transform vanishingPoint;

    [Tooltip("Smallest size when near the vanishing point.")]
    public float minScale = 0.25f;

    [Tooltip("Largest size when near the entry point.")]
    public float maxScale = 1.0f;
}

/// <summary>
/// Point-and-click 2D character controller.
/// Character only moves when an interactable (SectionExit, pickup, etc.) is clicked.
/// Free-click movement is intentionally disabled.
///
/// SETUP:
///  1. Attach to the character. Needs Rigidbody2D (Dynamic, Gravity Scale = 0).
///  
///  2. Camera tagged "MainCamera".
///  
///  3. Assign WalkableAreas array.
///  
///  4. Set Interactable Layer mask — all PickupItem, FragmentPickup, BlockingObstacle,
///     ItemTarget, and SectionExit objects must be on this layer.
///     
///  5. Assign a CursorManager in the scene for pointer cursor changes.
/// </summary>
[RequireComponent(typeof(Rigidbody2D))]
public class PointAndClickController : MonoBehaviour
{
    #region Variables
    [Header("Movement")]
    [Tooltip("Movement speed in units per second.")]
    public float moveSpd = 5f;

    [Tooltip("Distance from the target at which the character is considered to have arrived.")]
    public float stopDist = 0.1f;

    [Header("Walkable Area")]
    [Tooltip("All walkable regions for this scene. Add WalkableArea_Extension here " +
             "(disabled by default) — enable it when puzzles open new paths.")]
    public WalkableArea[] walkableAreas;

    [Header("Interactables")]
    [Tooltip("Layer that PickupItem and ItemTarget objects live on. " +
             "Clicks on this layer are handled as interactions, not movement.")]
    public LayerMask interactableLayer;

    [Header("Click Indicator (Optional)")]
    [Tooltip("Prefab spawned at the click position to give visual feedback.")]
    public GameObject clickIndicatorPrefab;

    [Header("Animation")]
    [Tooltip("Optional Animator for the character.")]
    public Animator animator;
    [Tooltip("Boolean parameter name in the Animator to trigger walking.")]
    public string isWalkingParam = "isWalking";

    [Header("Celebration")]
    [Tooltip("Trigger parameter name to play a celebration animation (optional).")]
    public string celebrationTrigger = "celebrate";
    [Tooltip("Direct animator state name to crossfade to when no trigger is present (optional).")]
    public string celebrationState = "celebrate";
    [Tooltip("If true, will use direct state crossfade when the trigger parameter is missing.")]
    public bool celebrationUseDirectStateFallback = true;
    [Tooltip("Cross-fade duration used when applying the direct state fallback.")]
    public float celebrationDirectStateCrossFade = 0.03f;
    [Tooltip("If > 0, automatically end the celebration after this many seconds and restore idle/walk state.")]
    public float celebrationDuration = 1.5f;
    [Tooltip("Optional sound effect to play when celebration starts.")]
    public AudioClip celebrationSFX;
    [Tooltip("Volume scale for the celebration SFX (0-1).")]
    [Range(0f, 1f)] public float celebrationSFXVolume = 1.0f;

    [Header("Audio")]
    public AudioClip clickSFX;
    public AudioClip walkSFX;
    [Tooltip("Time in seconds between each footstep sound.")]
    public float stepInterval = 0.45f;

    [Header("Perspective Scaling Zones")]
    [Tooltip("Optional per-exit/per-lane scaling setup. If assigned, these are used instead of anchor-based scaling.")]
    public ScalingZone[] scalingZones;

    private Rigidbody2D rb;
    private Camera mainCam;
    private Vector2 targetPos;
    
    private bool _isMoving;
    private bool isMoving
    {
        get => _isMoving;
        set
        {
            if (_isMoving == value) return;
            _isMoving = value;
            if (animator != null && !string.IsNullOrEmpty(isWalkingParam))
            {
                animator.SetBool(isWalkingParam, _isMoving);
            }
        }
    }
    private GameObject currentIndicator;
    private BlockingObstacle activeObstacle; // obstacle currently being held
    private LampPuzzle activeLamp;            // lamp currently being held
    private DraggablePickup activeDraggable;  // world pickup currently being dragged
    private Action onArrivedCallback;        // fires once when character reaches targetPos

    private System.Collections.Generic.List<Vector2> currentPath;
    private int currentPathIndex = 0;

    private float stepTimer = 0f;
    private bool warnedNoCam = false;
    private float defaultFacingSign = 1f;
    private Collider2D cachedHoverCollider = null;
    private float lastHoverCheckTime = 0f;
    private const float HOVER_CHECK_INTERVAL = 0.1f; // Check hover every 100ms instead of every frame

    [Header("Debug (Perspective scaling)")]
    public bool debugPerspectiveScaling = false;
    public bool debugPerspectiveScalingOnlyWhenMoving = true;
    public float debugPerspectiveLogInterval = 0.25f;

    private float _debugPerspectiveNextLogTime = 0f;
	#endregion

	#region Unity callbacks 
	void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        mainCam = Camera.main;

        defaultFacingSign = transform.localScale.x < 0f ? -1f : 1f;

        // Freeze rotation so the character doesn't spin 
        // from physics collisions
        rb.freezeRotation = true;

        targetPos = rb.position;
    }

    void OnEnable()
    {
        // Subscribe to section changes to reset size instantly
        if (GameManager.Instance != null)
            GameManager.Instance.onSectionEntered += OnSectionEntered;
        else
            StartCoroutine(SubscribeToGameManager());
    }

    void OnDisable()
    {
        if (GameManager.Instance != null)
            GameManager.Instance.onSectionEntered -= OnSectionEntered;
    }

    private System.Collections.IEnumerator SubscribeToGameManager()
    {
        yield return new WaitUntil(() => GameManager.Instance != null);
        GameManager.Instance.onSectionEntered += OnSectionEntered;
    }

    private void OnSectionEntered(int sectionIndex)
    {
        // Immediately reset size when entering a new section
        UpdatePerspectiveScale();
    }

    void Update()
    {
		// Attempt to recover a missing camera reference at runtime
		if (mainCam == null)
		{
			mainCam = Camera.main;
			if (mainCam == null && !warnedNoCam)
			{
				Debug.LogError("[PointAndClickController] No Camera tagged 'MainCamera' found in scene. Point-and-click input and cursor hover are disabled until a MainCamera exists.");
				warnedNoCam = true;
			}
		}

		if (mainCam == null)
			return; // cannot process input/hover without a camera

		// Skip all input while the game is paused, transitioning, or the Book is open
		bool isPaused = (PauseManager.Instance != null && PauseManager.Instance.IsPaused);
		bool isTransitioning = (TransitionManager.Instance != null && TransitionManager.Instance.IsTransitioning);
		bool isBookOpen = (BookUIManager.Instance != null && BookUIManager.Instance.IsOpen);
		
		if (isPaused || isTransitioning || isBookOpen)
			return;

		HandleInput();
        UpdateHoverCursor();
        HandleFootsteps();
    }

    private void HandleFootsteps()
    {
        if (isMoving && walkSFX != null)
        {
            stepTimer += Time.deltaTime;
            if (stepTimer >= stepInterval)
            {
                stepTimer = 0f;
                AudioManager.Instance?.PlaySFX(walkSFX);
            }
        }
        else
        {
            stepTimer = 0f; // Reset timer when stopped
        }
    }

    void FixedUpdate()
    {
        MoveCharacter();
    }
    #endregion

    #region Input
    private void HandleInput()
    {
        // Update world-drag if active: follow cursor while held
        if (activeDraggable != null)
        {
            if (Input.GetMouseButton(0))
            {
                activeDraggable.UpdateDrag(Input.mousePosition);
                return; // consume input while dragging
            }
            // if mouse button was released, allow the normal MouseButtonUp block below to run
        }

        if (Input.GetMouseButtonDown(0))
        {
            Vector3 screenPoint = Input.mousePosition;
            screenPoint.z = -mainCam.transform.position.z;
            Vector2 worldPoint = mainCam.ScreenToWorldPoint(screenPoint);

            Collider2D hit = Physics2D.OverlapPoint(worldPoint, interactableLayer);
            if (hit != null)
            {
                // Play interaction click sound
                if (clickSFX != null) AudioManager.Instance?.PlaySFX(clickSFX);

                PickupItem pickup = hit.GetComponent<PickupItem>();
                DraggablePickup draggable = hit.GetComponent<DraggablePickup>();
                if (draggable != null && draggable.allowDirectDrag)
                {
                    activeDraggable = draggable;
                    activeDraggable.BeginDrag();
                    return;
                }

                if (pickup != null) { pickup.TryPickup(); return; }

                FragmentPickup fragment = hit.GetComponent<FragmentPickup>();
                if (fragment != null) { fragment.TryCollect(); return; }

                BlockingObstacle obstacle = hit.GetComponent<BlockingObstacle>();
                if (obstacle != null) { activeObstacle = obstacle; activeObstacle.StartHold(); return; }

                LampPuzzle lamp = hit.GetComponent<LampPuzzle>();
                if (lamp != null) { activeLamp = lamp; activeLamp.StartHold(); return; }

                SectionExit exit = hit.GetComponent<SectionExit>();
                if (exit != null) { exit.OnClicked(); return; }

                ItemTarget target = hit.GetComponent<ItemTarget>();
                if (target != null) return; // drag-drop UI handles this
            }
            // Clicking empty space does nothing — movement is exit-driven only
        }

        if (Input.GetMouseButtonUp(0))
        {
            if (activeObstacle != null)
            {
                activeObstacle.CancelHold();
                activeObstacle = null;
            }
            if (activeLamp != null)
            {
                activeLamp.CancelHold();
                activeLamp = null;
            }
            if (activeDraggable != null)
            {
                activeDraggable.EndDrag();
                activeDraggable = null;
            }
        }
    }

    /// <summary>
    /// Raycasts the interactable layer periodically (every 100ms).
    /// Shows the pointer cursor when hovering over anything interactive.
    /// Throttled to improve performance on low-end devices.
    /// </summary>
    private void UpdateHoverCursor()
    {
        lastHoverCheckTime += Time.deltaTime;
        if (lastHoverCheckTime < HOVER_CHECK_INTERVAL)
        {
            // Use cached result - don't raycast every frame
            if (cachedHoverCollider != null)
                CursorManager.SetPointer();
            else
                CursorManager.SetDefault();
            return;
        }

        lastHoverCheckTime = 0f;

        Vector3 sp = Input.mousePosition;
        sp.z = -mainCam.transform.position.z;
        Vector2 wp = mainCam.ScreenToWorldPoint(sp);

        cachedHoverCollider = Physics2D.OverlapPoint(wp, interactableLayer);
        if (cachedHoverCollider != null)
            CursorManager.SetPointer();
        else
            CursorManager.SetDefault();
    }
    #endregion

    #region Movement
    /// <summary>Move the character to a world position.</summary>
    public void SetDestination(Vector2 worldPos)
    {
        onArrivedCallback = null;
        targetPos = worldPos;
        currentPath = WalkableArea.GetPath(walkableAreas, rb.position, targetPos);
        currentPathIndex = 0;
        isMoving  = true;
        SpawnClickIndicator(worldPos);
    }

    /// <summary>
    /// Move to a world position and fire a callback on arrival.
    /// Used by SectionExit to trigger the section transition once the
    /// character has actually walked to the exit point.
    /// </summary>
    public void SetDestinationWithCallback(Vector2 worldPos, Action onArrived)
    {
        onArrivedCallback = onArrived;
        targetPos = worldPos;
        currentPath = WalkableArea.GetPath(walkableAreas, rb.position, targetPos);
        currentPathIndex = 0;
        isMoving  = true;
        SpawnClickIndicator(worldPos);
    }

    /// <summary>
    /// Immediately stops the character in place.
    /// Called by GameManager on section transitions so momentum doesn't carry over.
    /// </summary>
    public void StopMovement()
    {
        isMoving = false;
        rb.linearVelocity = Vector2.zero;
        targetPos = rb.position;
        DestroyClickIndicator();

        // Restore default facing when fully stopped
        UpdatePerspectiveScale(defaultFacingSign);
    }

    /// <summary>
    /// Resets the character's scale to normal (1, 1, 1) while preserving horizontal flip.
    /// Called when entering a new section to remove any perspective scaling artifacts.
    /// </summary>
    public void ResetPerspectiveScale()
    {
        // Preserve the horizontal flip direction
        float sideSign = transform.localScale.x < 0 ? -1f : 1f;
        transform.localScale = new Vector3(sideSign, 1f, 1f);
    }

    public void UpdatePerspectiveScale(float sideSign = 0)
    {
        // Use current flip sign if not provided
        if (sideSign == 0) sideSign = transform.localScale.x < 0 ? -1f : 1f;

        SectionData currentSection = GameManager.Instance != null ? GameManager.Instance.currSection : null;
        
        float currentScale = 1.0f;
        if (currentSection != null && currentSection.usePerspectiveScaling)
        {
            if (scalingZones != null && scalingZones.Length > 0)
            {
                currentScale = CalculateScaleFromScalingZones(rb.position, scalingZones);
            }
            else
            {
                // Fallback to legacy Y-based scaling
                currentScale = CalculateScaleFromYRange(rb.position, currentSection);
            }
        }

        transform.localScale = new Vector3(sideSign * currentScale, currentScale, currentScale);
    }

    /// <summary>
    /// Calculate scale from configured scaling lanes.
    /// The lane whose segment is closest to the character drives the scale.
    /// </summary>
    private float CalculateScaleFromScalingZones(Vector2 characterPos, ScalingZone[] zones)
    {
        if (zones == null || zones.Length == 0) return 1.0f;

        bool foundZone = false;
        float bestDistanceSqr = float.MaxValue;
        float bestScale = 1.0f;

        foreach (ScalingZone zone in zones)
        {
            if (zone == null || zone.entryPoint == null || zone.vanishingPoint == null) continue;

            Vector2 entry = zone.entryPoint.position;
            Vector2 vanish = zone.vanishingPoint.position;
            Vector2 lane = vanish - entry;

            if (lane.sqrMagnitude < 0.000001f) continue;

            // Project character onto lane from entry -> vanishing point.
            float t = Mathf.Clamp01(Vector2.Dot(characterPos - entry, lane) / lane.sqrMagnitude);
            Vector2 closest = entry + lane * t;
            float distanceSqr = (characterPos - closest).sqrMagnitude;

            if (distanceSqr < bestDistanceSqr)
            {
                bestDistanceSqr = distanceSqr;
                bestScale = Mathf.Lerp(zone.maxScale, zone.minScale, t);
                foundZone = true;
            }
        }

        return foundZone ? bestScale : 1.0f;
    }

    /// <summary>
    /// Calculate character scale using legacy Y-based linear interpolation.
    /// Kept for backward compatibility with existing sections.
    /// </summary>
    private float CalculateScaleFromYRange(Vector2 characterPos, SectionData section)
    {
        float rangeTopY = section.topY;
        float rangeBottomY = section.bottomY;
        
        //if (isMoving && currentPath != null && currentPath.Count > 0)
        //{
        //    // Expand the range to encompass both current and target positions
        //    rangeTopY = Mathf.Min(section.topY, targetPos.y);
        //    rangeBottomY = Mathf.Max(section.bottomY, targetPos.y);
        //}

        const float EPS = 0.0001f;
        if (Mathf.Abs(rangeBottomY - rangeTopY) < EPS)
        {
            // Fallback: don't allow a degenerate range (prevent snapping to minScale)
            return transform.localScale.y;
            //return 1.0f;
        }

		float t = Mathf.InverseLerp(rangeTopY, rangeBottomY, characterPos.y);
		float scale = Mathf.Lerp(section.minScale, section.maxScale, t);

		if (debugPerspectiveScaling)
		{
			if (!debugPerspectiveScalingOnlyWhenMoving || isMoving)
			{
				if (Time.time >= _debugPerspectiveNextLogTime)
				{
					_debugPerspectiveNextLogTime = Time.time + Mathf.Max(0.05f, debugPerspectiveLogInterval);

					Debug.Log(
						$"[PerspectiveFallback] section='{section.sectionName}' " +
						$"posY={characterPos.y:F3} targetY={targetPos.y:F3} isMoving={isMoving} " +
						$"topY={section.topY:F3} bottomY={section.bottomY:F3} " +
						$"rangeTopY={rangeTopY:F3} rangeBottomY={rangeBottomY:F3} " +
						$"minScale={section.minScale:F3} maxScale={section.maxScale:F3} " +
						$"t={t:F3} scale={scale:F3}",
						this
					);
				}
			}
		}

		return scale;
	}

    private void MoveCharacter()
    {
        if (!isMoving || currentPath == null || currentPathIndex >= currentPath.Count) return;

        Vector2 waypoint = currentPath[currentPathIndex];
        float distToWaypoint = Vector2.Distance(rb.position, waypoint);

        if (distToWaypoint <= stopDist)
        {
            currentPathIndex++;
            if (currentPathIndex >= currentPath.Count)
            {
                rb.MovePosition(targetPos);
                rb.linearVelocity = Vector2.zero;
                isMoving = false;
                DestroyClickIndicator();

                // Restore default facing when fully stopped
                UpdatePerspectiveScale(defaultFacingSign);

                // Fire arrival callback (e.g. SectionExit transition)
                Action cb = onArrivedCallback;
                onArrivedCallback = null;
                cb?.Invoke();
                return;
            }
            waypoint = currentPath[currentPathIndex];
        }

        // Move toward the current waypoint
        Vector2 direction = (waypoint - rb.position).normalized;

        // Flip the sprite based on horizontal movement direction
        if (Mathf.Abs(direction.x) > 0.01f)
        {
            float sideSign = direction.x > 0 ? -1f : 1f;
            UpdatePerspectiveScale(sideSign);
        }
        else
        {
            float sideSign = transform.localScale.x < 0 ? -1f : 1f;
            UpdatePerspectiveScale(sideSign);
        }

        Vector2 nextPos = rb.position + direction * moveSpd * Time.fixedDeltaTime;

        // Clamp every physics step so the player never clips through a border.
        // Aoi: for monkey brain & for later refactoring, it prevents the player from clipping through the walls
        nextPos = WalkableArea.ClampToNearest(walkableAreas, nextPos);

        rb.MovePosition(nextPos);
    }
    #endregion

    #region Celebration / Emotes
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

    /// <summary>
    /// Public API to play a celebration/emote animation on this character.
    /// If an animator trigger name is assigned it will be used; otherwise a direct state cross-fade
    /// will be attempted when `celebrationUseDirectStateFallback` is enabled.
    /// </summary>
    public void PlayCelebration(float overrideDuration = -1f)
    {
        if (animator == null)
        {
            Debug.LogWarning("[PointAndClickController] PlayCelebration called but Animator is null.");
            return;
        }

        // Stop movement so the celebration animation isn't interrupted
        isMoving = false;

        // Ensure walking parameter is cleared
        if (!string.IsNullOrEmpty(isWalkingParam))
            animator.SetBool(isWalkingParam, false);

        // Try trigger first
        if (!string.IsNullOrEmpty(celebrationTrigger) && HasTriggerParameter(animator, celebrationTrigger))
        {
            animator.SetTrigger(celebrationTrigger);
        }
        else if (celebrationUseDirectStateFallback && !string.IsNullOrEmpty(celebrationState) && animator.isActiveAndEnabled)
        {
            try
            {
                animator.CrossFadeInFixedTime(celebrationState, Mathf.Max(0f, celebrationDirectStateCrossFade));
            }
            catch (Exception)
            {
                Debug.LogWarning("[PointAndClickController] CrossFade to celebration state failed (state may not exist).");
            }
        }
        else
        {
            // No celebration trigger/state configured - nothing to do
        }

        // Play celebration SFX (via global AudioManager so it continues even if this GameObject
        // gets disabled during a cutscene).
        if (celebrationSFX != null && AudioManager.Instance != null)
        {
            AudioManager.Instance.PlaySFX(celebrationSFX, celebrationSFXVolume);
        }

        // Optionally end celebration after a duration
        float dur = (overrideDuration > 0f) ? overrideDuration : celebrationDuration;
        if (dur > 0f)
        {
            StartCoroutine(EndCelebrationAfter(dur));
        }
    }

    private IEnumerator EndCelebrationAfter(float duration)
    {
        yield return new WaitForSeconds(duration);

        // Clear any transient flags. We don't force a specific idle state since Animator graph
        // should handle transitions based on parameters; we only restore walking flag to false.
        if (animator != null && !string.IsNullOrEmpty(isWalkingParam))
            animator.SetBool(isWalkingParam, false);
    }
    #endregion

    #region Click Indicator 
    private void SpawnClickIndicator(Vector2 position)
    {
        if (clickIndicatorPrefab == null) return;

        DestroyClickIndicator();
        currentIndicator = Instantiate(clickIndicatorPrefab, position, Quaternion.identity);
    }

    private void DestroyClickIndicator()
    {
        if (currentIndicator != null)
        {
            Destroy(currentIndicator);
            currentIndicator = null;
        }
    }
    #endregion
}
