using System.Collections;
using UnityEngine;
using TMPro;

/// <summary>
/// Simple fade-in / fade-out notification banner.
/// Call GameNotification.Show("message") from anywhere.
///
/// SETUP:
///  1. In your Canvas, create an Image panel → child TMP Text.
/// 
///  2. Attach this script to the panel. Assign 'label' to the TMP Text.
/// 
///  3. The panel starts hidden (CanvasGroup alpha = 0).
/// 
///  4. There should be only ONE GameNotification in the scene
///     (the static Instance is set automatically in Awake).
/// </summary>
public class GameNotification : MonoBehaviour
{
    public static GameNotification Instance { get; private set; }

    [Tooltip("The TMP text that shows the message.")]
    public TMP_Text label;

    [Tooltip("Seconds the message stays fully visible.")]
    public float holdTime = 2f;

    [Tooltip("Seconds for fade-in and fade-out.")]
    public float fadeDuration = 0.3f;

    private CanvasGroup  canvasGroup;
    private Coroutine    activeRoutine;

    void Awake()
    {
        Instance     = this;
        canvasGroup  = GetComponent<CanvasGroup>();
        if (canvasGroup == null) canvasGroup = gameObject.AddComponent<CanvasGroup>();
        canvasGroup.alpha = 0f;
    }

    /// <summary>
    /// Show a notification message. Safe to call from anywhere.
    /// </summary>
    public static void Show(string message)
    {
        if (Instance == null)
        {
            Debug.Log($"[Notification] {message}"); // fallback if UI not set up
            return;
        }
        Instance.Display(message);
    }

    private void Display(string message)
    {
        if (label != null) label.text = message;

        if (activeRoutine != null) StopCoroutine(activeRoutine);
        activeRoutine = StartCoroutine(FadeRoutine());
    }

    private IEnumerator FadeRoutine()
    {
        // Fade in
        yield return Fade(0f, 1f, fadeDuration);

        // Hold
        yield return new WaitForSeconds(holdTime);

        // Fade out
        yield return Fade(1f, 0f, fadeDuration);

        activeRoutine = null;
    }

    private IEnumerator Fade(float from, float to, float duration)
    {
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            canvasGroup.alpha = Mathf.Lerp(from, to, elapsed / duration);
            yield return null;
        }
        canvasGroup.alpha = to;
    }
}
