using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

/// <summary>
/// Manages the loading screen with an animated sprite sequence.
/// Displays while scenes are being loaded asynchronously.
///
/// SETUP:
///  1. Create a Canvas in the Main Menu scene, name it "LoadingScreen"
///  2. Add an Image component as a child for the animated sprite
///  3. Attach this script to the Canvas
///  4. Assign the sprite frames array in the Inspector
///  5. Set the frame duration and loop settings as desired
///  6. Call ShowLoadingScreen() when you want to start loading a new scene
/// </summary>
public class LoadingScreenManager : MonoBehaviour
{
    public static LoadingScreenManager Instance { get; private set; }

    [Header("Loading Screen UI")]
    [SerializeField] private Image animatedImage;
    
    [Header("Animation Settings")]
    [SerializeField] private Sprite[] spriteFrames;
    [SerializeField] private float frameDuration = 0.1f;
    [SerializeField] private bool loopAnimation = true;

    private CanvasGroup canvasGroup;
    private Coroutine animationCoroutine;
    private bool isLoading = false;

    private void Awake()
    {
        // Setup singleton pattern
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        canvasGroup = GetComponent<CanvasGroup>();
        if (canvasGroup == null)
        {
            canvasGroup = gameObject.AddComponent<CanvasGroup>();
        }

        // Start hidden but keep the GameObject ACTIVE so coroutines work
        // and the singleton remains accessible
        HideImmediate();
    }

    /// <summary>
    /// Instantly hide visuals without disabling the GameObject.
    /// </summary>
    private void HideImmediate()
    {
        if (canvasGroup != null)
        {
            canvasGroup.alpha = 0f;
            canvasGroup.blocksRaycasts = false;
            canvasGroup.interactable = false;
        }
    }

    /// <summary>
    /// Shows the loading screen and animates through the sprite sequence.
    /// </summary>
    public void ShowLoadingScreen()
    {
        if (isLoading) return;

        Debug.Log("[LoadingScreenManager] ShowLoadingScreen called.");
        isLoading = true;
        
        // Make visible and block input
        if (canvasGroup != null)
        {
            canvasGroup.alpha = 0f;
            canvasGroup.blocksRaycasts = true;
            canvasGroup.interactable = true;
        }
        
        StopAllCoroutines();
        StartCoroutine(FadeInAndAnimate());
    }

    /// <summary>
    /// Hides the loading screen with a fade out.
    /// </summary>
    public void HideLoadingScreen()
    {
        StopAllCoroutines();
        StartCoroutine(FadeOut());
    }

    private IEnumerator FadeInAndAnimate()
    {
        // Fade in
        float fadeInDuration = 0.3f;
        float elapsed = 0f;
        while (elapsed < fadeInDuration)
        {
            elapsed += Time.deltaTime;
            canvasGroup.alpha = Mathf.Clamp01(elapsed / fadeInDuration);
            yield return null;
        }
        canvasGroup.alpha = 1f;

        // Animate through sprite frames
        if (animationCoroutine != null)
            StopCoroutine(animationCoroutine);
        animationCoroutine = StartCoroutine(AnimateSpriteSequence());
    }

    private IEnumerator AnimateSpriteSequence()
    {
        if (spriteFrames == null || spriteFrames.Length == 0)
        {
            Debug.LogWarning("[LoadingScreenManager] No sprite frames assigned!");
            yield break;
        }

        int frameIndex = 0;
        while (isLoading)
        {
            if (animatedImage != null && spriteFrames.Length > 0)
            {
                animatedImage.sprite = spriteFrames[frameIndex];
                frameIndex++;

                if (frameIndex >= spriteFrames.Length)
                {
                    if (loopAnimation)
                        frameIndex = 0;
                    else
                        break;
                }
            }

            yield return new WaitForSeconds(frameDuration);
        }
    }

    private IEnumerator FadeOut()
    {
        isLoading = false;
        float fadeOutDuration = 0.3f;
        float elapsed = 0f;

        while (elapsed < fadeOutDuration)
        {
            elapsed += Time.deltaTime;
            canvasGroup.alpha = Mathf.Clamp01(1f - (elapsed / fadeOutDuration));
            yield return null;
        }

        HideImmediate();
        Debug.Log("[LoadingScreenManager] Loading screen hidden.");
    }

    /// <summary>
    /// Call this when a scene has finished loading to hide the loading screen.
    /// </summary>
    public void OnSceneLoadComplete()
    {
        HideLoadingScreen();
    }
}

