using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// A small radial-fill UI element that appears over the obstacle while the player holds.
///
/// SETUP:
///  1. In the scene Canvas, create a UI Image → set Image Type to "Filled",
///     Fill Method to "Radial 360", Fill Origin to "Top".
/// 
///  2. Attach this script. Assign the Image to 'fillImage'.
/// 
///  3. Set the RectTransform to follow the obstacle (position it in world space
///     via a World Space Canvas, or keep in Overlay and set its anchored position manually).
/// 
///  4. Assign 'progressUI' on the BlockingObstacle in the Inspector.
///
/// TIP: For a world-space indicator, set the Canvas Render Mode to "World Space"
///      and position this panel directly above the obstacle sprite.
/// </summary>
public class PullProgressUI : MonoBehaviour
{
    [Tooltip("The filled Image that shows pull progress (Image Type = Filled, Radial 360).")]
    public Image fillImage;

    [Tooltip("Optional background ring image shown while holding.")]
    public GameObject backgroundRing;

    void Awake()
    {
        // Start hidden
        gameObject.SetActive(false);
        if (fillImage != null) fillImage.fillAmount = 0f;
    }

    /// <summary>Show or hide the entire progress indicator.</summary>
    public void Show(bool visible)
    {
        gameObject.SetActive(visible);
        if (!visible && fillImage != null)
            fillImage.fillAmount = 0f;
    }

    /// <summary>Set fill amount 0–1.</summary>
    public void SetProgress(float t)
    {
        if (fillImage != null)
            fillImage.fillAmount = Mathf.Clamp01(t);
    }
}
