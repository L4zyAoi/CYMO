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

    private Rigidbody2D rb;
    private Camera mainCam;
    private Vector2 targetPos;
    private bool isMoving;
    private GameObject currentIndicator;
    private BlockingObstacle activeObstacle; // obstacle currently being held
    private Action onArrivedCallback;        // fires once when character reaches targetPos

    private System.Collections.Generic.List<Vector2> currentPath;
    private int currentPathIndex = 0;

    private bool warnedNoCam = false;
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
		HandleInput();
        UpdateHoverCursor();
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
                PickupItem pickup = hit.GetComponent<PickupItem>();
                if (pickup != null) { pickup.TryPickup(); return; }

                FragmentPickup fragment = hit.GetComponent<FragmentPickup>();
                if (fragment != null) { fragment.TryCollect(); return; }

                BlockingObstacle obstacle = hit.GetComponent<BlockingObstacle>();
                if (obstacle != null) { activeObstacle = obstacle; activeObstacle.StartHold(); return; }

                LampPuzzle lamp = hit.GetComponent<LampPuzzle>();
                if (lamp != null) { lamp.ActivateLamp(); return; }

                SectionExit exit = hit.GetComponent<SectionExit>();
                if (exit != null) { exit.OnClicked(); return; }

                ItemTarget target = hit.GetComponent<ItemTarget>();
                if (target != null) return; // drag-drop UI handles this
            }
            // Clicking empty space does nothing — movement is exit-driven only
        }

        if (Input.GetMouseButtonUp(0) && activeObstacle != null)
        {
            activeObstacle.CancelHold();
            activeObstacle = null;
        }
    }

    /// <summary>
    /// Raycasts the interactable layer every frame.
    /// Shows the pointer cursor when hovering over anything interactive.
    /// </summary>
    private void UpdateHoverCursor()
    {
        Vector3 sp = Input.mousePosition;
        sp.z = -mainCam.transform.position.z;
        Vector2 wp = mainCam.ScreenToWorldPoint(sp);

        Collider2D hit = Physics2D.OverlapPoint(wp, interactableLayer);
        if (hit != null)
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
