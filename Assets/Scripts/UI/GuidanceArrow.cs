using System.Collections;
using UnityEngine;

/// <summary>
/// World-space arrow that follows the player and points toward a target world position.
/// The arrow auto-destroys when the player gets close to the target or after a timeout.
/// </summary>
public class GuidanceArrow : MonoBehaviour
{
    [HideInInspector]
    public bool isInitial = false;
    public Transform playerTransform;
    private Vector3 targetWorldPos;
    public float followHeight = 1.6f;
    public float showDistance = 0.5f; // distance to target at which arrow is removed
    public float maxDuration = 10f;    // fallback removal
    [Tooltip("Adjust if your arrow sprite's forward direction does not match the computed angle.")]
    public float angleOffset = 0f;

    [Header("Initial Arrow FX")]
    [Tooltip("If true, the initial guidance arrow gently floats up and down.")]
    public bool initialFloatEnabled = true;
    [Tooltip("Vertical bob amount in world units for the initial guidance arrow.")]
    public float initialFloatAmplitude = 0.18f;
    [Tooltip("Speed of the bobbing animation for the initial guidance arrow.")]
    public float initialFloatSpeed = 3.2f;

    [Tooltip("If true, the initial guidance arrow fades in/out (blinks).")]
    public bool initialBlinkEnabled = true;
    [Range(0f, 1f)]
    [Tooltip("Minimum alpha reached while blinking. 0 = fully invisible, 1 = no blink.")]
    public float initialBlinkMinAlpha = 0.35f;
    [Tooltip("Speed of the blink animation for the initial guidance arrow.")]
    public float initialBlinkSpeed = 5f;

    private float timer = 0f;
    private bool sectionListenerRegistered = false;
    private int initialSectionIndex = -1;
    private SpriteRenderer[] cachedRenderers;
    private Color[] rendererBaseColors;
    private float floatPhase;
    private float blinkPhase;

    void Awake()
    {
        cachedRenderers = GetComponentsInChildren<SpriteRenderer>(true);
        rendererBaseColors = new Color[cachedRenderers.Length];
        for (int i = 0; i < cachedRenderers.Length; i++)
        {
            rendererBaseColors[i] = cachedRenderers[i] != null ? cachedRenderers[i].color : Color.white;
        }

        floatPhase = Random.Range(0f, Mathf.PI * 2f);
        blinkPhase = Random.Range(0f, Mathf.PI * 2f);
    }

    void OnEnable()
    {
        TryRegisterSectionChangeListener();
    }

    void OnDestroy()
    {
        if (sectionListenerRegistered && GameManager.Instance != null)
        {
            GameManager.Instance.onSectionEntered -= OnSectionEntered;
            sectionListenerRegistered = false;
        }

		// Notify GameManager that this guidance arrow was destroyed so followups can run.
		if (GameManager.Instance != null)
		{
			GameManager.Instance.NotifyGuidanceArrowDestroyed(isInitial);
		}
    }

    public void Initialize(Transform player, Vector3 target)
    {
        playerTransform = player;
        targetWorldPos = target;
        timer = 0f;

        if (GameManager.Instance != null)
            initialSectionIndex = GameManager.Instance.currSectionIndex;

        Debug.Log($"[GuidanceArrow] Initialized. player={(playerTransform!=null?playerTransform.name:"null")}, target={targetWorldPos}");

        // In case Initialize runs before/after OnEnable, make sure listener is registered once.
        TryRegisterSectionChangeListener();
    }

    // New overload: allow caller to anchor the arrow at the target location
    public void Initialize(Transform player, Vector3 target, bool anchorAtTarget)
    {
        playerTransform = player;
        targetWorldPos = target;
        anchorToTarget = anchorAtTarget;
        timer = 0f;

        if (GameManager.Instance != null)
            initialSectionIndex = GameManager.Instance.currSectionIndex;

        Debug.Log($"[GuidanceArrow] Initialized (anchor={anchorToTarget}). player={(playerTransform!=null?playerTransform.name:"null")}, target={targetWorldPos}");

        // In case Initialize runs before/after OnEnable, make sure listener is registered once.
        TryRegisterSectionChangeListener();
    }

    private bool anchorToTarget = false;

    private void TryRegisterSectionChangeListener()
    {
        if (sectionListenerRegistered) return;
        if (GameManager.Instance == null) return;

        GameManager.Instance.onSectionEntered += OnSectionEntered;
        sectionListenerRegistered = true;
    }

    private void OnSectionEntered(int _)
    {
        // Guidance is only meant for the current section. Remove it as soon as player changes section.
        Destroy(gameObject);
    }

    private void ApplyAlphaMultiplier(float alphaMultiplier)
    {
        if (cachedRenderers == null || rendererBaseColors == null)
            return;

        float clamped = Mathf.Clamp01(alphaMultiplier);
        for (int i = 0; i < cachedRenderers.Length; i++)
        {
            SpriteRenderer sr = cachedRenderers[i];
            if (sr == null) continue;

            Color baseColor = i < rendererBaseColors.Length ? rendererBaseColors[i] : sr.color;
            baseColor.a *= clamped;
            sr.color = baseColor;
        }
    }

    void Update()
    {
        if (playerTransform == null)
        {
            Destroy(gameObject);
            return;
        }

        // Remove guidance as soon as any transition begins.
        if (TransitionManager.Instance != null && TransitionManager.Instance.IsTransitioning)
        {
            Destroy(gameObject);
            return;
        }

        // Fallback safety: if section index changed, remove arrow immediately.
        if (GameManager.Instance != null && initialSectionIndex >= 0 && GameManager.Instance.currSectionIndex != initialSectionIndex)
        {
            Destroy(gameObject);
            return;
        }

        // Position arrow (either anchored at the world target, or above the player)
        Vector3 basePos;
        if (anchorToTarget)
        {
            float z = playerTransform != null ? playerTransform.position.z : targetWorldPos.z;
            basePos = new Vector3(targetWorldPos.x, targetWorldPos.y, z);
            if (isInitial && initialFloatEnabled && initialFloatAmplitude > 0f)
            {
                float bob = Mathf.Sin((Time.time * initialFloatSpeed) + floatPhase) * initialFloatAmplitude;
                basePos += Vector3.up * bob;
            }
        }
        else
        {
            basePos = playerTransform.position + Vector3.up * followHeight;
            if (isInitial && initialFloatEnabled && initialFloatAmplitude > 0f)
            {
                float bob = Mathf.Sin((Time.time * initialFloatSpeed) + floatPhase) * initialFloatAmplitude;
                basePos += Vector3.up * bob;
            }
        }

        transform.position = basePos;

        // Rotate to face the appropriate direction.
        // For anchored arrows we want them to point opposite the player-facing vector
        // (so they visually indicate the approach direction). Invert the vector
        // previously used so the anchored arrow points the opposite way.
        Vector3 dir = anchorToTarget ? (basePos - playerTransform.position) : (targetWorldPos - basePos);
        if (dir.sqrMagnitude > 0.0001f)
        {
            float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
            float z = angle - 90f + angleOffset;
            Quaternion rot = Quaternion.Euler(0f, 0f, z);

            // Ensure the arrow visually faces the intended direction. If the computed
            // rotation would make the arrow point away (possible due to sprite pivot
            // or prefab orientation), flip by 180 degrees.
            Vector3 dirNorm = dir.normalized;
            Vector3 forward = rot * Vector3.up;
            if (Vector3.Dot(forward, dirNorm) < 0f)
            {
                rot = rot * Quaternion.Euler(0f, 0f, 180f);
            }

            transform.rotation = rot;
        }

        if (isInitial && initialBlinkEnabled)
        {
            float blinkT = 0.5f + 0.5f * Mathf.Sin((Time.time * initialBlinkSpeed) + blinkPhase);
            float alpha = Mathf.Lerp(initialBlinkMinAlpha, 1f, blinkT);
            ApplyAlphaMultiplier(alpha);
        }
        else
        {
            ApplyAlphaMultiplier(1f);
        }

        // Check proximity to target
        float dist = Vector2.Distance(new Vector2(playerTransform.position.x, playerTransform.position.y), new Vector2(targetWorldPos.x, targetWorldPos.y));
        timer += Time.deltaTime;
        if (dist <= showDistance || timer >= maxDuration)
        {
            Destroy(gameObject);
        }
    }
}
