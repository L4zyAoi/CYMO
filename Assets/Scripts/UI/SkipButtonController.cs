using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Shows a Skip button while a video/cutscene is playing and forwards clicks / keypress (Esc) to VideoCutsceneManager.SkipVideo().
/// Attach this to a UI GameObject (e.g., the Skip button parent) and assign the Button in the inspector.
/// </summary>
public class SkipButtonController : MonoBehaviour
{
    [Tooltip("UI Button that will be shown while the cutscene is playing.")]
    public Button skipButton;

    [Tooltip("Key to skip the cutscene (default: Escape)")]
    public KeyCode skipKey = KeyCode.Escape;

    void Start()
    {
        if (skipButton == null)
        {
            Debug.LogWarning("[SkipButtonController] skipButton not assigned. Trying to find a Button component on the same GameObject.");
            skipButton = GetComponent<Button>();
            if (skipButton == null)
            {
                Debug.LogWarning("[SkipButtonController] No Button found. Disabling controller.");
                enabled = false;
                return;
            }
        }

        // Hide initially — but don't deactivate the GameObject that hosts this script,
        // because deactivating it will stop this component from running and prevent
        // Update() from re-showing the button.
        if (skipButton.gameObject != this.gameObject)
            skipButton.gameObject.SetActive(false);

        // Add click listener
        skipButton.onClick.AddListener(() => {
            if (VideoCutsceneManager.Instance != null)
                VideoCutsceneManager.Instance.SkipVideo();
        });
    }

    void Update()
    {
        var mgr = VideoCutsceneManager.Instance;
        bool shouldShow = mgr != null && mgr.IsPlaying;

        if (skipButton != null && skipButton.gameObject.activeSelf != shouldShow)
            skipButton.gameObject.SetActive(shouldShow);

        if (shouldShow && Input.GetKeyDown(skipKey))
        {
            mgr.SkipVideo();
        }
    }
}
