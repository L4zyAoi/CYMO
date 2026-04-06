using UnityEngine;

/// <summary>
/// Attach to in-scene guidance arrow objects whose SpriteRenderer should be
/// enabled when the initial opening guidance arrow disappears.
/// The component will optionally start with its SpriteRenderer disabled.
/// </summary>
public class SceneGuidanceMarker : MonoBehaviour
{
    [Tooltip("If true, this marker will be activated when the initial opening guidance arrow disappears.")]
    public bool activateOnInitialArrowDisappear = true;

    [Tooltip("If true, the SpriteRenderer will be disabled on start and only enabled when activated.")]
    public bool startHidden = true;

    [Tooltip("If true, the marker GameObject is forced active when ActivateSprite is called.")]
    public bool forceGameObjectActiveOnActivate = true;

    [Tooltip("Minimum SpriteRenderer sorting order applied on activation. Use a high value if the marker appears behind backgrounds.")]
    public int minSortingOrderOnActivate = 50;

    [Header("Disable On Entry")]
    [Tooltip("If true, the marker will be disabled the first time the player enters the designated section.")]
    public bool disableAfterPlayerEntry = false;
    [Tooltip("If true, fully deactivate the GameObject when disabling on player entry; otherwise only the SpriteRenderer will be disabled.")]
    public bool deactivateGameObjectOnEntry = false;
    [Tooltip("Player GameObject tag used to detect entry via trigger collisions.")]
    public string playerTag = "Player";

    private SpriteRenderer sr;
    private bool hasBeenDisabledByPlayerEntry = false;

    void Awake()
    {
        sr = GetComponent<SpriteRenderer>();
    }

    void Start()
    {
        if (sr != null && startHidden)
            sr.enabled = false;
    }

    public void ActivateSprite()
    {
        if (disableAfterPlayerEntry && hasBeenDisabledByPlayerEntry)
            return;

        if (forceGameObjectActiveOnActivate && !gameObject.activeSelf)
            gameObject.SetActive(true);

        if (sr == null)
            sr = GetComponent<SpriteRenderer>();

        if (sr != null)
        {
            sr.enabled = true;
            if (sr.sortingOrder < minSortingOrderOnActivate)
                sr.sortingOrder = minSortingOrderOnActivate;
        }
        else
            gameObject.SetActive(true);
    }

    /// <summary>
    /// Call this when the player enters the desired section. If the toggle is enabled
    /// the marker will be disabled (SpriteRenderer or whole GameObject) and will not
    /// reactivate on future ActivateSprite calls.
    /// </summary>
    public void PlayerEnteredSection()
    {
        if (!disableAfterPlayerEntry || hasBeenDisabledByPlayerEntry)
            return;

        if (sr == null)
            sr = GetComponent<SpriteRenderer>();

        if (deactivateGameObjectOnEntry)
        {
            gameObject.SetActive(false);
        }
        else if (sr != null)
        {
            sr.enabled = false;
        }
        else
        {
            gameObject.SetActive(false);
        }

        hasBeenDisabledByPlayerEntry = true;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!disableAfterPlayerEntry || hasBeenDisabledByPlayerEntry) return;
        if (other != null && !string.IsNullOrEmpty(playerTag) && other.CompareTag(playerTag))
            PlayerEnteredSection();
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!disableAfterPlayerEntry || hasBeenDisabledByPlayerEntry) return;
        if (other != null && !string.IsNullOrEmpty(playerTag) && other.CompareTag(playerTag))
            PlayerEnteredSection();
    }
}
