using System;
using UnityEngine;

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

    [Header("Audio")]
    public AudioClip clickSFX;
    public AudioClip walkSFX;
    [Tooltip("Time in seconds between each footstep sound.")]
    public float stepInterval = 0.45f;

    private Rigidbody2D rb;
    private Camera mainCam;
    private Vector2 targetPos;
    private PerspectiveAnchor[] cachedAnchors;
    private float anchorCacheTime = 0f;
    private const float ANCHOR_CACHE_DURATION = 5f; // Cache for 5 seconds (increased from 2 for better perf)
    
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
    private Action onArrivedCallback;        // fires once when character reaches targetPos

    private System.Collections.Generic.List<Vector2> currentPath;
    private int currentPathIndex = 0;

    private float stepTimer = 0f;
    private bool warnedNoCam = false;
    private Collider2D cachedHoverCollider = null;
    private float lastHoverCheckTime = 0f;
    private const float HOVER_CHECK_INTERVAL = 0.1f; // Check hover every 100ms instead of every frame
    #endregion

    #region Unity callbacks 
    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        mainCam = Camera.main;

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
        InvalidateAnchorCache();
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
            // Try to get anchors from cache or discover them
            PerspectiveAnchor[] anchors = GetPerspectiveAnchors();
            
            if (anchors != null && anchors.Length > 0)
            {
                currentScale = CalculateScaleFromAnchors(rb.position, anchors);
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
    /// Get all PerspectiveAnchor components in the scene with caching.
    /// </summary>
    private PerspectiveAnchor[] GetPerspectiveAnchors()
    {
        // Return cached anchors if still valid
        if (cachedAnchors != null && anchorCacheTime < ANCHOR_CACHE_DURATION)
        {
            anchorCacheTime += Time.deltaTime;
            return cachedAnchors;
        }

        // Only rediscover anchors after cache expires
        // Use more efficient caching by checking if cache is populated
        if (cachedAnchors == null)
        {
            cachedAnchors = FindObjectsOfType<PerspectiveAnchor>(includeInactive: false);
        }
        
        anchorCacheTime = 0f;
        return cachedAnchors;
    }

    /// <summary>
    /// Reset the anchor cache when entering a new section.
    /// </summary>
    private void InvalidateAnchorCache()
    {
        cachedAnchors = null;
        anchorCacheTime = ANCHOR_CACHE_DURATION;
    }

    /// <summary>
    /// Calculate character scale based on proximity to perspective anchors.
    /// Uses weighted interpolation between nearby anchors.
    /// Optimized to early-exit from distant anchors.
    /// </summary>
    private float CalculateScaleFromAnchors(Vector2 characterPos, PerspectiveAnchor[] anchors)
    {
        if (anchors == null || anchors.Length == 0) return 1.0f;

        float totalWeight = 0f;
        float weightedScale = 0f;
        bool foundInfluence = false;

        // Calculate weights for all anchors within influence range
        foreach (var anchor in anchors)
        {
            if (anchor == null || !anchor.gameObject.activeInHierarchy) continue;

            float distance = anchor.GetDistance(characterPos);
            
            // Early exit for distant anchors - skip expensive weight calculation
            if (distance > anchor.influenceRadius)
            {
                continue;
            }

            foundInfluence = true;

            // Use inverse distance squared for smooth falloff, with small epsilon to prevent division issues
            float weight = 1f / (1f + distance * distance);
            totalWeight += weight;
            weightedScale += weight * anchor.scale;
        }

        // If no anchors are in range, use the closest anchor's scale
        if (!foundInfluence)
        {
            float closestDistance = float.MaxValue;
            float closestScale = 1.0f;

            foreach (var anchor in anchors)
            {
                if (anchor == null || !anchor.gameObject.activeInHierarchy) continue;

                float distance = anchor.GetDistance(characterPos);
                if (distance < closestDistance)
                {
                    closestDistance = distance;
                    closestScale = anchor.scale;
                }
            }

            return closestScale;
        }

        return totalWeight > 0 ? weightedScale / totalWeight : 1.0f;
    }

    /// <summary>
    /// Calculate character scale using legacy Y-based linear interpolation.
    /// Kept for backward compatibility with existing sections.
    /// </summary>
    private float CalculateScaleFromYRange(Vector2 characterPos, SectionData section)
    {
        float rangeTopY = section.topY;
        float rangeBottomY = section.bottomY;
        
        if (isMoving && currentPath != null && currentPath.Count > 0)
        {
            // Expand the range to encompass both current and target positions
            rangeTopY = Mathf.Min(section.topY, targetPos.y);
            rangeBottomY = Mathf.Max(section.bottomY, targetPos.y);
        }
        
        float t = Mathf.InverseLerp(rangeTopY, rangeBottomY, characterPos.y);
        return Mathf.Lerp(section.minScale, section.maxScale, t);
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
