using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Rendering;

/// <summary>
/// Manages cinematic transitions between sections.
/// Instead of a simple camera pan, the screen fades to black and the 
/// character walks across the empty screen to reach the next location.
/// Also hides UI during the transition for a cinematic effect.
///
/// SETUP:
///  1. Create a child GameObject on your Main Camera named "TransitionCurtain".
///  2. Add a SpriteRenderer to it with a solid black square sprite.
///  3. Set its Scale massive (e.g. 50, 50, 1) and Sorting Layer to "UI" or "Transition".
///  4. Set its Alpha to 0 (invisible).
///  5. Attach this script to an object (e.g. GameManager).
/// </summary>
public class TransitionManager : MonoBehaviour
{
    public static TransitionManager Instance { get; private set; }

    [Header("Settings")]
    public float fadeDuration = 0.5f;
    public float walkSpeed = 5f;
    [Tooltip("Time to wait while screen is black (allows camera to pan and sections to load).")]
    public float blackScreenDuration = 0.8f;
    [Tooltip("The Sorting Layer the character should move to during transition (to be on top of the curtain).")]
    public string transitionSortingLayer = "Transition";

    [Header("Audio")]
    public AudioClip transitionSFX;

    [Header("References")]
    public SpriteRenderer curtain;
    [Tooltip("Optional UI Canvas to hide during transitions. If null, will auto-find.")]
    public CanvasGroup uiCanvasGroup;

    private bool isTransitioning = false;
    public bool IsTransitioning => isTransitioning;

    void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);

        // Auto-find curtain if not assigned
        if (curtain == null)
        {
            Camera mainCam = Camera.main;
            if (mainCam != null)
            {
                Transform curtainTransform = mainCam.transform.Find("TransitionCurtain");
                if (curtainTransform != null)
                {
                    curtain = curtainTransform.GetComponent<SpriteRenderer>();
                }
            }
        }

        // Auto-find UI Canvas if not assigned
        if (uiCanvasGroup == null)
        {
            // More efficient approach: iterate through canvases instead of FindFirstObjectByType
            Canvas[] canvases = Resources.FindObjectsOfTypeAll<Canvas>();
            foreach (Canvas canvas in canvases)
            {
                if (!canvas.gameObject.activeInHierarchy) continue;
                
                uiCanvasGroup = canvas.GetComponent<CanvasGroup>();
                if (uiCanvasGroup == null)
                {
                    uiCanvasGroup = canvas.gameObject.AddComponent<CanvasGroup>();
                }
                break;
            }
        }

        // Initialize curtain to invisible
        if (curtain != null)
        {
            // Ensure curtain is positioned in front of camera
            Vector3 curtainPos = curtain.transform.position;
            curtainPos.z = 1f; // In front of camera (camera is typically at z=0 or negative)
            curtain.transform.position = curtainPos;

            // Ensure curtain has proper scale to cover screen
            if (curtain.transform.localScale.magnitude < 10f) // If too small
            {
                curtain.transform.localScale = new Vector3(100f, 100f, 1f);
            }

            // Initialize alpha to 0 (invisible)
            Color c = curtain.color;
            c.a = 0;
            curtain.color = c;
        }
    }

    /// <summary>
    /// Plays a simple black screen fade transition.
    /// Called by GameManager when entering a new section.
    /// onMiddle: callback runs while screen is black (update section state and move player).
    /// onEnd: callback runs after screen fades back in.
    /// </summary>
    public IEnumerator PlayWalkTransition(Transform player, Vector2 movementDir, System.Action onMiddle, System.Action onEnd = null, Vector2 sourceExitWalkTarget = default, Vector2 destinationExitWalkTarget = default)
    {
        if (isTransitioning) yield break;
        isTransitioning = true;

        Debug.Log("[TransitionManager] PlayWalkTransition started.");

        // 0. Hide UI
        if (uiCanvasGroup != null)
        {
            yield return StartCoroutine(FadeUI(0f));
        }

        // 1. Fade to Black
        if (transitionSFX != null) AudioManager.Instance?.PlaySFX(transitionSFX);
        yield return StartCoroutine(FadeCurtain(1f));

        // 2. Middle Action (usually Snap Camera/Update Section State and move player)
        onMiddle?.Invoke();

        // 3. Wait while screen is black to let camera pan and sections load
        Debug.Log($"[TransitionManager] Screen is black for {blackScreenDuration}s. Camera panning and loading sections...");
        yield return new WaitForSeconds(blackScreenDuration);

        // 4. Fade Back from Black
        yield return StartCoroutine(FadeCurtain(0f));

        // 5. Show UI again
        if (uiCanvasGroup != null)
        {
            yield return StartCoroutine(FadeUI(1f));
        }

        // Fire the end callback (after screen is fully revealed)
        onEnd?.Invoke();

        isTransitioning = false;
    }

    private IEnumerator FadeUI(float targetAlpha)
    {
        if (uiCanvasGroup == null) yield break;

        float startAlpha = uiCanvasGroup.alpha;
        float elapsed = 0f;

        while (elapsed < fadeDuration)
        {
            elapsed += Time.deltaTime;
            uiCanvasGroup.alpha = Mathf.Lerp(startAlpha, targetAlpha, elapsed / fadeDuration);
            yield return null;
        }

        uiCanvasGroup.alpha = targetAlpha;
    }

    private IEnumerator FadeCurtain(float targetAlpha)
    {
        if (curtain == null)
        {
            Debug.LogError("[TransitionManager] Cannot fade curtain: SpriteRenderer is null!");
            yield break;
        }

        float startAlpha = curtain.color.a;
        float elapsed = 0f;

        Debug.Log($"[TransitionManager] Fading curtain from alpha {startAlpha} to {targetAlpha} over {fadeDuration}s.");

        while (elapsed < fadeDuration)
        {
            elapsed += Time.deltaTime;
            Color c = curtain.color;
            c.a = Mathf.Lerp(startAlpha, targetAlpha, elapsed / fadeDuration);
            curtain.color = c;
            yield return null;
        }

        Color finalC = curtain.color;
        finalC.a = targetAlpha;
        curtain.color = finalC;

        Debug.Log($"[TransitionManager] Curtain fade complete (alpha now = {finalC.a}).");
    }
}
