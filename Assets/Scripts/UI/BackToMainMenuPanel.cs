using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

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
            yesButton.onClick.AddListener(OnYesClicked);

        if (noButton != null)
            noButton.onClick.AddListener(OnNoClicked);

        // Initialize visibility
        if (panelCanvasGroup != null)
            panelCanvasGroup.alpha = showOnStart ? 1f : 0f;

        if (panelRect != null)
            panelRect.localScale = showOnStart ? Vector3.one : Vector3.zero;

        isVisible = showOnStart;
    }

    private void Update()
    {
        // Allow ESC key to toggle or show the panel
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            if (isVisible)
                Hide();
            else
                Show();
        }
    }

    /// <summary>
    /// Show the panel with fade-in and scale-up animation.
    /// </summary>
    public void Show()
    {
        if (isVisible) return;

        if (fadeCoroutine != null)
            StopCoroutine(fadeCoroutine);
        if (scaleCoroutine != null)
            StopCoroutine(scaleCoroutine);

        gameObject.SetActive(true);
        fadeCoroutine = StartCoroutine(FadePanel(0f, 1f));
        scaleCoroutine = StartCoroutine(ScalePanel(Vector3.zero, Vector3.one));
        isVisible = true;

        Debug.Log("[BackToMainMenuPanel] Confirmation dialog shown.");
    }

    /// <summary>
    /// Hide the panel with fade-out and scale-down animation.
    /// </summary>
    public void Hide()
    {
        if (!isVisible) return;

        if (fadeCoroutine != null)
            StopCoroutine(fadeCoroutine);
        if (scaleCoroutine != null)
            StopCoroutine(scaleCoroutine);

        fadeCoroutine = StartCoroutine(FadePanel(1f, 0f));
        scaleCoroutine = StartCoroutine(ScalePanel(Vector3.one, Vector3.zero));
        isVisible = false;

        Debug.Log("[BackToMainMenuPanel] Confirmation dialog hidden.");
    }

    /// <summary>
    /// Toggle panel visibility.
    /// </summary>
    public void Toggle()
    {
        if (isVisible)
            Hide();
        else
            Show();
    }

    private IEnumerator FadePanel(float startAlpha, float targetAlpha)
    {
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

        // Deactivate if scaling to zero
        if (targetScale == Vector3.zero)
            gameObject.SetActive(false);
    }

    private void OnYesClicked()
    {
        Debug.Log("[BackToMainMenuPanel] Yes clicked - returning to Main Menu");

        // Ensure time is running before scene load
        Time.timeScale = 1f;

        // Load main menu scene
        UnityEngine.SceneManagement.SceneManager.LoadScene("MainMenu");
    }

    private void OnNoClicked()
    {
        Debug.Log("[BackToMainMenuPanel] No clicked - resuming game");
        Hide();
    }

    public bool IsVisible => isVisible;
}