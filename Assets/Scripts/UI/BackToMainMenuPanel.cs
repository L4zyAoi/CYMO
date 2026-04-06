using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>
/// Manages the "Back to Main Menu" confirmation dialog.
/// Displays a panel with background image, "Back to Menu?" prompt, and Yes/No buttons.
///
/// SETUP:
///  1. Create a Canvas in your scene.
///  2. Create a Panel child (with Image component set to your background art).
///  3. Inside the panel, add:
///     - A Text element with "Back to Menu?" prompt
///     - A Button for "Yes"
///     - A Button for "No"
///  4. Attach this script to the Panel GameObject.
///  5. Assign the CanvasGroup, Text, and Buttons in the Inspector.
/// </summary>
public class BackToMainMenuPanel : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private CanvasGroup panelCanvasGroup;
    [SerializeField] private TextMeshProUGUI promptText;
    [SerializeField] private Button yesButton;
    [SerializeField] private Button noButton;

    [Header("Animation Settings")]
    [SerializeField] private float fadeDuration = 0.5f;
    [SerializeField] private float scaleAnimDuration = 0.3f;
    [SerializeField] private bool showOnStart = false;

    private bool isVisible = false;
    private Coroutine fadeCoroutine;
    private Coroutine scaleCoroutine;
    private RectTransform panelRect;
    // Prevent rapid double-toggle (e.g. two events fired in same frame)
    private float lastToggleTime = -10f;
    [SerializeField] private float toggleLockDuration = 0.25f;
    // Guard while show/hide transition is running
    private bool isTransitioning = false;

    private void Awake()
    {
        panelRect = GetComponent<RectTransform>();

        // Auto-find CanvasGroup if not assigned
        if (panelCanvasGroup == null)
        {
            panelCanvasGroup = GetComponent<CanvasGroup>();
            if (panelCanvasGroup == null)
                panelCanvasGroup = gameObject.AddComponent<CanvasGroup>();
        }

        // Auto-find Text if not assigned
        if (promptText == null)
            promptText = GetComponentInChildren<TextMeshProUGUI>();

        // Auto-find buttons if not assigned
        if (yesButton == null || noButton == null)
        {
            Button[] buttons = GetComponentsInChildren<Button>();
            if (buttons.Length >= 2)
            {
                yesButton = buttons[0];
                noButton = buttons[1];
            }
        }

        // Setup button listeners
        if (yesButton != null)
        {
            yesButton.onClick.RemoveListener(OnYesClicked);
            yesButton.onClick.AddListener(OnYesClicked);
            Debug.LogFormat("[BackToMainMenuPanel] yesButton assigned: {0}", yesButton.name);
        }

        if (noButton != null)
        {
            noButton.onClick.RemoveListener(OnNoClicked);
            noButton.onClick.AddListener(OnNoClicked);
            Debug.LogFormat("[BackToMainMenuPanel] noButton assigned: {0}", noButton.name);
        }

        // Initialize visibility
        if (panelCanvasGroup != null)
            panelCanvasGroup.alpha = showOnStart ? 1f : 0f;

        if (panelRect != null)
            panelRect.localScale = showOnStart ? Vector3.one : Vector3.zero;

        isVisible = showOnStart;
    }

    private void Update()
    {
        // Allow ESC key to toggle or show the panel.
        // Support both the old Input API and the new Input System.
#if ENABLE_INPUT_SYSTEM
        if (Keyboard.current != null)
        {
            if (Keyboard.current.escapeKey.wasPressedThisFrame)
            {
                if (isVisible) Hide(); else Show();
            }
        }
        else
        {
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                if (isVisible) Hide(); else Show();
            }
        }
#else
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            if (isVisible) Hide(); else Show();
        }
#endif
    }

    /// <summary>
    /// Show the panel with fade-in and scale-up animation.
    /// </summary>
    public void Show()
    {
        if (isVisible)
        {
            Debug.Log("[BackToMainMenuPanel] Show() called but already visible.");
            return;
        }

        // Stop any running animations
        if (fadeCoroutine != null)
            StopCoroutine(fadeCoroutine);
        if (scaleCoroutine != null)
            StopCoroutine(scaleCoroutine);

        // Force the panel active and immediately set visible state so it appears instantly
        gameObject.SetActive(true);
        if (panelCanvasGroup != null)
        {
            panelCanvasGroup.alpha = 1f;
            panelCanvasGroup.interactable = true;
            panelCanvasGroup.blocksRaycasts = true;
        }
        if (panelRect != null)
        {
            panelRect.localScale = Vector3.one;
        }

        // Start animations for smoothness (optional)
        fadeCoroutine = StartCoroutine(FadePanel(panelCanvasGroup != null ? panelCanvasGroup.alpha : 1f, 1f));
        scaleCoroutine = StartCoroutine(ScalePanel(panelRect != null ? panelRect.localScale : Vector3.one, Vector3.one));

        isVisible = true;
        isTransitioning = true;

        Debug.Log("[BackToMainMenuPanel] Confirmation dialog shown.");
        Debug.LogFormat("[BackToMainMenuPanel] State after Show: active={0} scale={1} alpha={2:F3} interactable={3} blocksRaycasts={4} parentActive={5} canvasExists={6}",
            gameObject.activeSelf,
            (panelRect != null ? panelRect.localScale.ToString() : "null"),
            (panelCanvasGroup != null ? panelCanvasGroup.alpha : -1f),
            (panelCanvasGroup != null ? panelCanvasGroup.interactable : false),
            (panelCanvasGroup != null ? panelCanvasGroup.blocksRaycasts : false),
            (transform.parent != null ? transform.parent.gameObject.activeInHierarchy.ToString() : "no-parent"),
            (GetComponentInParent<Canvas>() != null).ToString());
    }

    /// <summary>
    /// Hide the panel with fade-out and scale-down animation.
    /// </summary>
    public void Hide()
    {
        Hide(false);
    }

    /// <summary>
    /// Hide the panel. If force=true, immediately set alpha/scale and interactions off.
    /// </summary>
    public void Hide(bool force)
    {
        if (!isVisible && !force)
        {
            Debug.Log("[BackToMainMenuPanel] Hide() called but not visible.");
            return;
        }

        if (!force)
        {
            if (Time.time - lastToggleTime < toggleLockDuration)
            {
                Debug.LogFormat("[BackToMainMenuPanel] Hide() locked. time={0:F3} lastToggle={1:F3} diff={2:F3}", Time.time, lastToggleTime, Time.time - lastToggleTime);
                return;
            }

            if (isTransitioning)
            {
                Debug.Log("[BackToMainMenuPanel] Hide() ignored - transition in progress.");
                return;
            }
        }

        // Stop any running animations
        if (fadeCoroutine != null)
            StopCoroutine(fadeCoroutine);
        if (scaleCoroutine != null)
            StopCoroutine(scaleCoroutine);

        if (force)
        {
            // Immediately hide visuals and block interaction
            if (panelCanvasGroup != null)
            {
                panelCanvasGroup.alpha = 0f;
                panelCanvasGroup.interactable = false;
                panelCanvasGroup.blocksRaycasts = false;
            }
            if (panelRect != null)
                panelRect.localScale = Vector3.zero;

            isVisible = false;
            isTransitioning = false;
            lastToggleTime = Time.time;

            Debug.Log("[BackToMainMenuPanel] Confirmation dialog force-hidden.");
            Debug.LogFormat("[BackToMainMenuPanel] Hide(force) set lastToggleTime={0:F3}", lastToggleTime);
            return;
        }

        // Normal hide with animations
        fadeCoroutine = StartCoroutine(FadePanel(1f, 0f));
        scaleCoroutine = StartCoroutine(ScalePanel(Vector3.one, Vector3.zero));
        isVisible = false;

        lastToggleTime = Time.time;
        isTransitioning = true;

        Debug.Log("[BackToMainMenuPanel] Confirmation dialog hidden.");
        Debug.LogFormat("[BackToMainMenuPanel] Hide() set lastToggleTime={0:F3}", lastToggleTime);
    }

    /// <summary>
    /// Toggle panel visibility.
    /// </summary>
    public void Toggle()
    {
        Debug.LogFormat("[BackToMainMenuPanel] Toggle() called. isVisible={0} time={1:F3} lastToggle={2:F3}", isVisible, Time.time, lastToggleTime);
        if (isVisible)
            Hide();
        else
            Show();
    }

    private IEnumerator FadePanel(float startAlpha, float targetAlpha)
    {
        Debug.LogFormat("[BackToMainMenuPanel] FadePanel start: startAlpha={0:F3} targetAlpha={1:F3} duration={2:F3}", startAlpha, targetAlpha, fadeDuration);
        if (panelCanvasGroup == null) yield break;

        float elapsed = 0f;

        while (elapsed < fadeDuration)
        {
            elapsed += Time.deltaTime;
            panelCanvasGroup.alpha = Mathf.Lerp(startAlpha, targetAlpha, elapsed / fadeDuration);
            yield return null;
        }

        panelCanvasGroup.alpha = targetAlpha;
        fadeCoroutine = null;
    }

    private IEnumerator ScalePanel(Vector3 startScale, Vector3 targetScale)
    {
        Debug.LogFormat("[BackToMainMenuPanel] ScalePanel start: startScale={0} targetScale={1} duration={2:F3}", startScale, targetScale, scaleAnimDuration);
        if (panelRect == null) yield break;

        float elapsed = 0f;

        while (elapsed < scaleAnimDuration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / scaleAnimDuration;
            // Ease-out animation for smooth pop effect
            t = 1f - Mathf.Pow(1f - t, 3f);
            panelRect.localScale = Vector3.Lerp(startScale, targetScale, t);
            yield return null;
        }

        panelRect.localScale = targetScale;
        scaleCoroutine = null;

        // If scaling to zero, disable interaction but keep GameObject active
        if (targetScale == Vector3.zero)
        {
            if (panelCanvasGroup != null)
            {
                panelCanvasGroup.interactable = false;
                panelCanvasGroup.blocksRaycasts = false;
            }
            isTransitioning = false; // hide finished
            Debug.Log("[BackToMainMenuPanel] ScalePanel: hide finished");
        }
        else
        {
            // show finished
            if (panelCanvasGroup != null)
            {
                panelCanvasGroup.interactable = true;
                panelCanvasGroup.blocksRaycasts = true;
            }
            isTransitioning = false;
            Debug.Log("[BackToMainMenuPanel] ScalePanel: show finished");
        }
    }

    private void OnYesClicked()
    {
        Debug.Log("[BackToMainMenuPanel] Yes clicked - returning to Main Menu");

        // Ensure time is running before scene load
        Time.timeScale = 1f;

        // Load main menu scene
        UnityEngine.SceneManagement.SceneManager.LoadScene("MainMenuScene");
    }

    private void OnNoClicked()
    {
        Debug.Log("[BackToMainMenuPanel] No clicked - resuming game");
        // Ignore if already hidden (prevents duplicate OnClick calls from multiple input modules/listeners)
        if (!isVisible)
        {
            Debug.Log("[BackToMainMenuPanel] NoClicked ignored because panel not visible.");
            return;
        }

        if (isTransitioning)
        {
            Debug.Log("[BackToMainMenuPanel] NoClicked received during transition — forcing hide.");
            Hide(true); // force immediate hide so UI responds even during transitions
            return;
        }

        Hide();
    }

    public bool IsVisible => isVisible;
}