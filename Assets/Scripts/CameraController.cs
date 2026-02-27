using System.Collections;
using UnityEngine;

/// <summary>
/// Confines and smoothly pans the camera between section bounds.
///
/// SETUP:
///  1. Attach this to the Main Camera GameObject.
///  2. Assign this component to the CameraController field on GameManager.
/// </summary>
public class CameraController : MonoBehaviour
{
    [Header("Pan Settings")]
    [Tooltip("Time in seconds to pan to a new section.")]
    public float panDuration = 0.6f;

    [Tooltip("Easing curve for the pan (leave as default for smooth ease-in-out).")]
    public AnimationCurve panCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    private Rect currBounds;
    private Camera cam;
    private Coroutine panCoroutine;

    void Awake()
    {
        cam = GetComponent<Camera>();
    }

    /// <summary>
    /// Pan to and confine the camera to the given section's bounds.
    /// This should be called by GameManager
    /// </summary>
    public void MoveToSection(SectionData section)
    {
        currBounds = section.cameraBounds;
        Vector3 targetPos = ClampedCameraPos(new Vector2(
            section.cameraBounds.center.x,
            section.cameraBounds.center.y));

        if (panCoroutine != null) StopCoroutine(panCoroutine);
        panCoroutine = StartCoroutine(PanTo(targetPos));
    }

    // Per-frame confinement
    void LateUpdate()
    {
        // Keep the camera clamped to the current section bounds every frame
        // (handles the player walking near edges, etc.)
        Vector3 clamped = ClampedCameraPos(transform.position);
        transform.position = clamped;
    }

    #region Helpers
    private Vector3 ClampedCameraPos(Vector2 desired)
    {
        float halfH = cam.orthographicSize;
        float halfW = halfH * cam.aspect;

        float x = Mathf.Clamp(desired.x,
            currBounds.xMin + halfW,
            currBounds.xMax - halfW);

        float y = Mathf.Clamp(desired.y,
            currBounds.yMin + halfH,
            currBounds.yMax - halfH);

        // If the section is smaller than the camera view, just center it
        if (currBounds.width  < halfW * 2f) x = currBounds.center.x;
        if (currBounds.height < halfH * 2f) y = currBounds.center.y;

        return new Vector3(x, y, transform.position.z);
    }

    private IEnumerator PanTo(Vector3 target)
    {
        Vector3 start = transform.position;
        float elapsed = 0f;

        while (elapsed < panDuration)
        {
            elapsed += Time.deltaTime;
            float t = panCurve.Evaluate(Mathf.Clamp01(elapsed / panDuration));
            transform.position = Vector3.Lerp(start, target, t);
            yield return null;
        }

        transform.position = target;
        panCoroutine = null;
    }
    #endregion

    #region Gizmo
    void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1f, 1f, 0f, 0.4f);
        Gizmos.DrawWireCube(
            new Vector3(currBounds.center.x, currBounds.center.y, 0f),
            new Vector3(currBounds.width, currBounds.height, 0f));
    }
    #endregion
}
