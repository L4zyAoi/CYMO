using System.Collections;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// A static lamp in a dark section. The player holds the mouse on it to
/// grow a circular light mask that "punches through" the dark overlay,
/// revealing the bright background. No URP lights needed.
///
/// SETUP:
///  1. Place the Bright background SpriteRenderer.
///
///  2. Place the Dark background SpriteRenderer on top (higher Order In Layer).
///     On the Dark SpriteRenderer, set Mask Interaction = "Visible Outside Mask".
///
///  3. Create a child of the Lamp with a SpriteMask component.
///     Assign a circular radial gradient sprite (white center, transparent edge).
///     Set the SpriteMask's initial scale small (e.g. 0.5, 0.5, 1).
///     Assign it to 'lightMask' below.
///
///  4. Give the Lamp an "Interactable" Layer + Collider2D.
///
///  5. Attach this script. Assign darkBackground and lightMask.
/// </summary>
public class LampPuzzle : MonoBehaviour
{
    [Header("Visuals")]
    [Tooltip("The dark overlay SpriteRenderer. Set its Mask Interaction to 'Visible Outside Mask'.")]
    public SpriteRenderer darkBackground;

    [Tooltip("A SpriteMask with a circular gradient sprite, placed at the lamp's position.")]
    public SpriteMask lightMask;

    [Header("Player Character")]
    [Tooltip("The player character's SpriteRenderer. Will be tinted dark in this section.")]
    public SpriteRenderer playerSprite;

    [Tooltip("The dark tint color applied to the player (e.g. dark grey).")]
    public Color darkTint = new Color(0.15f, 0.15f, 0.2f, 1f);

    [Header("Section")]
    [Tooltip("Which section index this dark room is in. Player is only darkened when in this section.")]
    public int currSectionIndex = 0;

    [Header("Mask Growth")]
    [Tooltip("Starting scale of the light mask (small glow).")]
    public float startMaskScale = 0.5f;

    [Tooltip("Final scale — big enough to cover the entire section.")]
    public float maxMaskScale = 15f;

    [Header("Hold Settings")]
    [Tooltip("How long the player must hold to fully light the room.")]
    public float holdDuration = 2f;

    [Tooltip("How long the final crossfade takes after the hold completes.")]
    public float fadeDuration = 0.5f;

    [Header("Pull Progress UI (Optional)")]
    public PullProgressUI progressUI;

    [Header("Animation (Optional)")]
    public Animator animator;
    public string triggerPull = "Pull";
    public string triggerIdle = "Idle";

    [Header("Events")]
    [Tooltip("Fired when the lamp is fully pulled and the room lights up.")]
    public UnityEvent OnLampTurnedOn;

    private bool isLit = false;
    private bool isHolding = false;
    private bool playerInSection = false;
    private float holdTimer = 0f;

    void Start()
    {
        if (lightMask != null)
            lightMask.transform.localScale = Vector3.one * startMaskScale;
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

    private System.Collections.IEnumerator SubscribeToGameManager()
    {
        yield return new WaitUntil(() => GameManager.Instance != null);
        GameManager.Instance.onSectionEntered += OnSectionEntered;

        // If the player is already in this section
        if (GameManager.Instance.currSectionIndex == currSectionIndex)
            OnSectionEntered(currSectionIndex);
    }

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
            // Leaving the dark section — restore the player
            if (playerSprite != null)
                playerSprite.color = Color.white;
        }
    }

    void Update()
    {
        if (isLit) return;

        // --- 1. Holding Logic (Grows the Light) ---
        if (isHolding)
        {
            if (Input.GetMouseButton(0))
            {
                holdTimer += Time.deltaTime;
                float t = Mathf.Clamp01(holdTimer / holdDuration);
                progressUI?.SetProgress(t);

                // Grow the mask outward — punches a bigger hole in the dark overlay
                if (lightMask != null)
                {
                    float scale = Mathf.Lerp(startMaskScale, maxMaskScale, t);
                    lightMask.transform.localScale = Vector3.one * scale;
                }

                if (holdTimer >= holdDuration)
                    CompletePull();
            }
            else
            {
                CancelHold();
            }
        }

        // --- 2. Proximity Lighting (Always active while in section) ---
        if (playerInSection && playerSprite != null)
        {
            float distance = Vector2.Distance(playerSprite.transform.position, transform.position);
            
            // Calculate current visual radius in world units roughly based on mask scale
            // (Assuming mask sprite is ~1 unit radius at scale 1)
            float currentRadius = lightMask != null ? lightMask.transform.localScale.x : startMaskScale;
            
            // Adjust the feather/buffer so the character starts lighting up early
            float lightStart = currentRadius * 1.2f; 
            float lightEnd = currentRadius * 0.5f;   // Fully lit when inside the core
            
            float proximityT = Mathf.InverseLerp(lightStart, lightEnd, distance);
            playerSprite.color = Color.Lerp(darkTint, Color.white, proximityT);
        }
    }

    public void StartHold()
    {
        if (isLit) return;
        isHolding = true;
        holdTimer = 0f;
        progressUI?.Show(true);

        if (animator != null && !string.IsNullOrEmpty(triggerPull))
            animator.SetTrigger(triggerPull);
    }

    public void CancelHold()
    {
        if (isLit) return;

        isHolding = false;
        holdTimer = 0f;
        progressUI?.SetProgress(0f);
        progressUI?.Show(false);

        // Reset mask back to small glow
        if (lightMask != null)
            lightMask.transform.localScale = Vector3.one * startMaskScale;

        if (animator != null && !string.IsNullOrEmpty(triggerIdle))
            animator.SetTrigger(triggerIdle);
    }

    private void CompletePull()
    {
        isLit = true;
        isHolding = false;
        progressUI?.Show(false);

        OnLampTurnedOn?.Invoke();

        if (darkBackground != null)
            StartCoroutine(FinalFadeRoutine());
    }

    private IEnumerator FinalFadeRoutine()
    {
        // Fade out the dark overlay entirely
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

        // Hide the mask too — no longer needed
        if (lightMask != null)
            lightMask.gameObject.SetActive(false);

        Collider2D col = GetComponent<Collider2D>();
        if (col != null) col.enabled = false;
    }
}
