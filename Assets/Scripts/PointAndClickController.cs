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
///  4. Optionally assign a ClickIndicatorPrefab 
///     to show a marker at the click position.
/// </summary>
[RequireComponent(typeof(Rigidbody2D))]
public class PointAndClickController : MonoBehaviour
{
    [Header("Movement")]
    [Tooltip("Movement speed in units per second.")]
    public float moveSpd = 5f;

    [Tooltip("Distance from the target at which the character is considered to have arrived.")]
    public float stopDist = 0.1f;

    [Header("Click Indicator (Optional)")]
    [Tooltip("Prefab spawned at the click position to give visual feedback.")]
    public GameObject clickIndicatorPrefab;

    // Private state 
    private Rigidbody2D rb;
    private Camera mainCam;
    private Vector2 targetPos;
    private bool isMoving;

    // Keep a reference so we can destroy the old indicator 
    // on a new click
    private GameObject currentIndicator;

    // Unity callbacks 
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

    // Input 
    private void HandleInput()
    {
        if (Input.GetMouseButtonDown(0))
        {
            // This should convert the screen-space click into a 2D world point
            // PS: it does :DD
            Vector3 screenPoint = Input.mousePosition;
            screenPoint.z = -mainCam.transform.position.z; // keep on the z=0 plane
            Vector2 worldPoint = mainCam.ScreenToWorldPoint(screenPoint);

            SetDestination(worldPoint);
        }
    }

    // Movement 
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
        rb.MovePosition(rb.position + direction * moveSpd * Time.fixedDeltaTime);
    }

    // Click Indicator 
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
}
