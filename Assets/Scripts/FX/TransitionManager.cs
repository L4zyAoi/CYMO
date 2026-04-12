using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

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
        if (Instance != null && Instance != this)
        {
            // TransitionManager references scene objects (curtain/canvas), so prefer the newest scene instance.
            TransitionManager oldInstance = Instance;
            Debug.Log($"[TransitionManager] Replacing previous instance from scene '{oldInstance.gameObject.scene.name}' with current scene '{gameObject.scene.name}'.");
            Instance = this;
            if (oldInstance != null)
                Destroy(oldInstance);
        }
        else
        {
            Instance = this;
        }

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
            uiCanvasGroup = FindCanvasGroupInScene(gameObject.scene);

            // Fallback if no active canvas exists in the same scene.
            if (uiCanvasGroup == null)
            {
                uiCanvasGroup = FindCanvasGroupInAnyScene();
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

    private CanvasGroup FindCanvasGroupInScene(Scene scene)
    {
        if (!scene.IsValid())
            return null;

		Canvas[] canvases = FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (Canvas canvas in canvases)
        {
            if (canvas == null) continue;
            if (!canvas.gameObject.activeInHierarchy) continue;
            if (!canvas.gameObject.scene.IsValid() || !canvas.gameObject.scene.isLoaded) continue;
            if (canvas.gameObject.scene != scene) continue;

            CanvasGroup cg = canvas.GetComponent<CanvasGroup>();
            if (cg == null)
            {
                cg = canvas.gameObject.AddComponent<CanvasGroup>();
            }
            return cg;
        }

        return null;
    }

    private CanvasGroup FindCanvasGroupInAnyScene()
    {
		Canvas[] canvases = FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (Canvas canvas in canvases)
        {
            if (canvas == null) continue;
            if (!canvas.gameObject.activeInHierarchy) continue;
            if (!canvas.gameObject.scene.IsValid() || !canvas.gameObject.scene.isLoaded) continue;

            CanvasGroup cg = canvas.GetComponent<CanvasGroup>();
            if (cg == null)
            {
                cg = canvas.gameObject.AddComponent<CanvasGroup>();
            }
            return cg;
        }

        return null;
    }

    /// <summary>
    /// Plays a simple black screen fade transition.
    /// Called by GameManager when entering a new section.
    /// onMiddle: callback runs while screen is black (update section state and move player).
    /// onEnd: callback runs after screen fades back in.
    /// </summary>
    public IEnumerator PlayWalkTransition(Transform player, Vector2 movementDir, System.Action onMiddle, System.Action onEnd = null, Vector2 sourceExitWalkTarget = default, Vector2 destinationExitWalkTarget = default, bool startFromBlack = false)
    {
        if (isTransitioning) yield break;
        isTransitioning = true;

        Debug.Log($"[TransitionManager] PlayWalkTransition started. startFromBlack={startFromBlack}");

        // 0. Hide UI
        if (uiCanvasGroup != null)
        {
            if (startFromBlack)
            {
                uiCanvasGroup.alpha = 0f;
                uiCanvasGroup.interactable = false;
                uiCanvasGroup.blocksRaycasts = false;
            }
            else
            {
                yield return StartCoroutine(FadeUI(0f));
            }
        }

        // 1. Fade to Black
        if (startFromBlack)
        {
            SetCurtainAlphaImmediate(1f);
        }
        else
        {
            if (transitionSFX != null) AudioManager.Instance?.PlaySFX(transitionSFX);
            yield return StartCoroutine(FadeCurtain(1f));
        }

        // 2. Middle Action (usually Snap Camera/Update Section State and move player)
        onMiddle?.Invoke();

        // 3. Wait while screen is black to let camera pan and sections load
        Debug.Log($"[TransitionManager] Screen is black for {blackScreenDuration}s. Camera panning and loading sections...");
        yield return new WaitForSeconds(blackScreenDuration);

        bool waitingForVideo = VideoCutsceneManager.Instance != null && VideoCutsceneManager.Instance.IsPlaying;
        if (waitingForVideo)
        {
            const float maxExtraBlackWait = 2.0f;
            float extraWait = 0f;
            while (extraWait < maxExtraBlackWait &&
                   VideoCutsceneManager.Instance != null &&
                   VideoCutsceneManager.Instance.IsPlaying &&
                   !VideoCutsceneManager.Instance.HasPresentedFirstFrame)
            {
                extraWait += Time.deltaTime;
                yield return null;
            }

            if (uiCanvasGroup != null)
            {
                // Make video UI visible while still black to avoid revealing gameplay for a frame.
                yield return StartCoroutine(FadeUI(1f));
            }

            // Fade the black curtain after video UI is ready.
            yield return StartCoroutine(FadeCurtain(0f));
        }
        else
        {
            // 4. Fade Back from Black
            yield return StartCoroutine(FadeCurtain(0f));

            // 5. Show UI again
            if (uiCanvasGroup != null)
            {
                yield return StartCoroutine(FadeUI(1f));
            }
        }

        // Fire the end callback (after screen is fully revealed)
        onEnd?.Invoke();

        isTransitioning = false;
    }

    private IEnumerator FadeUI(float targetAlpha)
    {
        if (uiCanvasGroup == null) yield break;

        bool showUI = targetAlpha > 0.5f;
        if (showUI)
        {
            // Re-enable button interaction before fade-in completes.
            uiCanvasGroup.interactable = true;
            uiCanvasGroup.blocksRaycasts = true;
        }

        float startAlpha = uiCanvasGroup.alpha;
        float elapsed = 0f;

        while (elapsed < fadeDuration)
        {
            elapsed += Time.deltaTime;
            uiCanvasGroup.alpha = Mathf.Lerp(startAlpha, targetAlpha, elapsed / fadeDuration);
            yield return null;
        }

        uiCanvasGroup.alpha = targetAlpha;

        if (!showUI)
        {
            // Disable interaction once UI is hidden.
            uiCanvasGroup.interactable = false;
            uiCanvasGroup.blocksRaycasts = false;
        }
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

    private void SetCurtainAlphaImmediate(float alpha)
    {
        if (curtain == null)
        {
            Debug.LogError("[TransitionManager] Cannot set curtain alpha immediately: SpriteRenderer is null!");
            return;
        }

        Color c = curtain.color;
        c.a = Mathf.Clamp01(alpha);
        curtain.color = c;
    }
}
