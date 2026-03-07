using UnityEngine;

/// <summary>
/// Point-and-click 2D character controller.
///
/// SETUP:
///  1. Attach this script to the character GameObject.
///  
///  2. Make sure the character has a Rigidbody2D 
///     (Body Type = Dynamic, Gravity Scale = 0).
///     
///  3. The Main Camera must be tagged "MainCamera" 
///     (Unity should default this, but I'm putting this here 
///     just in case i forgor :D).
///     
///  4. Assign a WalkableArea so the character only walks on valid paths.
///
///  5. Set up an "Interactable" Layer (Edit → Project Settings → Tags and Layers).
///     Assign all PickupItem and ItemTarget GameObjects to that layer, then
///     set the Interactable Layer mask on this component.
///
///  6. Optionally assign a ClickIndicatorPrefab 
///     to show a marker at the click position.
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
    [Tooltip("Defines where the character is allowed to walk. " +
             "Clicks outside the area are snapped to the nearest border point.")]
    public WalkableArea walkableArea;

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

    // Keep a reference so it can destroy the old indicator 
    // on a new click
    private GameObject currentIndicator;
    #endregion

    #region Unity callbacks 
    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        mainCam = Camera.main;

        // Freeze rotation so the character doesn't spin 
        // from physics collisions
        rb.freezeRotation = true;

        // Start at the character's current world position
        targetPos = rb.position;
    }

    void Update()
    {
        HandleInput();
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

            // Check for interactable objects first (layer-masked, ignores WalkableArea)
            Collider2D hit = Physics2D.OverlapPoint(worldPoint, interactableLayer);
            if (hit != null)
            {
                // Try pickup
                PickupItem pickup = hit.GetComponent<PickupItem>();
                if (pickup != null) { pickup.TryPickup(); return; }

                // Try item target (drag-drop handles this, but support direct click too)
                ItemTarget target = hit.GetComponent<ItemTarget>();
                if (target != null) return; // let drag-drop UI handle it
            }

            // No interactable hit — treat as a movement click
            if (walkableArea != null)
                worldPoint = walkableArea.ClampToArea(worldPoint);

            SetDestination(worldPoint);
        }
    }
    #endregion

    #region Movement 
    /// <summary>
    /// Call this to programmatically send the character 
    /// to a world position.
    /// </summary>
    public void SetDestination(Vector2 worldPos)
    {
        targetPos = worldPos;
        isMoving = true;
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
        if (!isMoving) return;

        float distToTarget = Vector2.Distance(rb.position, targetPos);

        if (distToTarget <= stopDist)
        {
            // Snap exactly to the target and stop
            rb.MovePosition(targetPos);
            rb.linearVelocity = Vector2.zero;
            isMoving = false;
            DestroyClickIndicator();
            return;
        }

        // Move toward the target
        Vector2 direction = (targetPos - rb.position).normalized;
        Vector2 nextPos = rb.position + direction * moveSpd * Time.fixedDeltaTime;


        // Clamp the next position to the walkable area every physics step.
        // This is what actually prevents clipping through the border --
        // clamping only the click destination isn't enough because the
        // straight-line path between two valid points can briefly exit
        // a concave polygon or graze the edge.
        
        // Aoi: for monkey brain & for later refactoring, it prevents the player from clipping through the walls
        if (walkableArea != null)
            nextPos = walkableArea.ClampToArea(nextPos);

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
