using UnityEngine;

/// <summary>
/// Optional visual indicator that appears at the clicked position.
///
/// SETUP:
///  1. Create a small GameObject (e.g. a circle sprite or a simple sprite).
/// 
///  2. Attach this script to it.
/// 
///  3. Assign it as the ClickIndicatorPrefab on PointAndClickController.
///
/// The indicator will pulse and then fade out automatically.
/// It is also destroyed by PointAndClickController 
/// when the character arrives.
/// </summary>
public class ClickIndicator : MonoBehaviour
{
    [Header("Animation")]
    [Tooltip("How long the indicator stays visible before fading.")]
    public float lifetime = 1f;

    [Tooltip("Speed of the pulsing scale animation.")]
    public float pulseSpd = 4f;

    [Tooltip("Pulse scale amplitude (0 = no pulse).")]
    public float pulseAmp = 0.15f;

    // Private state
    private SpriteRenderer spriteRenderer;
    private float timer;
    private Vector3 baseScale;

    void Awake()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
        baseScale = transform.localScale;
        timer = lifetime;
    }

    void Update()
    {
        timer -= Time.deltaTime;

        // Pulsing scale
        float pulse = 1f + Mathf.Sin(Time.time * pulseSpd) * pulseAmp;
        transform.localScale = baseScale * pulse;

        // Fade out over the last half of the lifetime
        if (spriteRenderer != null)
        {
            float alpha = Mathf.Clamp01(timer / (lifetime * 0.5f));
            Color c = spriteRenderer.color;
            c.a = alpha;
            spriteRenderer.color = c;
        }

        if (timer <= 0f)
        {
            Destroy(gameObject);
        }
    }
}
