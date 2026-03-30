using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Manages pausing the game when the settings panel is opened.
///
/// SETUP:
///  1. Create a Canvas with a Settings Panel (initially inactive).
///
///  2. Add a Settings Button to the HUD. Wire its OnClick to PauseManager.TogglePause().
///
///  3. Inside the Settings Panel, add a Resume / Close button.
///     Wire its OnClick to PauseManager.TogglePause() as well.
///
///  4. Attach this script to a persistent GameObject (e.g. GameManager or its own object).
///
///  5. Assign the settingsPanel in the Inspector.
/// </summary>
public class PauseManager : MonoBehaviour
{
    public static PauseManager Instance { get; private set; }

    [Header("UI")]
    [Tooltip("The settings/pause panel. Will be toggled active/inactive.")]
    public GameObject settingsPanel;

    [Header("Sliders")]
    public Slider masterSlider;
    public Slider musicSlider;
    public Slider sfxSlider;

    /// <summary>
    /// True while the settings panel is open and the game is paused.
    /// Other scripts (e.g. PointAndClickController) should check this
    /// before processing input.
    /// </summary>
    public bool IsPaused { get; private set; }

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        // Ensure the panel starts closed
        if (settingsPanel != null)
            settingsPanel.SetActive(false);

        IsPaused = false;
        
        // Match sliders to initial settings
        RefreshSliders();
    }

    /// <summary>
    /// Toggle pause on/off. Wire this to your Settings button AND
    /// the Close/Resume button inside the panel.
    /// </summary>
    public void TogglePause()
    {
        if (IsPaused)
            Resume();
        else
            Pause();
    }

    public void Pause()
    {
        IsPaused = true;
        Time.timeScale = 0f;

        if (settingsPanel != null)
            settingsPanel.SetActive(true);

        RefreshSliders();
    }

    public void Resume()
    {
        IsPaused = false;
        Time.timeScale = 1f;

        if (settingsPanel != null)
            settingsPanel.SetActive(false);
    }

    public void RefreshSliders()
    {
        if (AudioManager.Instance == null) return;

        if (masterSlider != null) masterSlider.value = AudioManager.Instance.masterVolume;
        if (musicSlider != null) musicSlider.value = AudioManager.Instance.musicVolume;
        if (sfxSlider != null) sfxSlider.value = AudioManager.Instance.sfxVolume;
    }

    #region Audio Settings Hooks
    /// <summary>
    /// Wrapper for the master volume slider.
    /// </summary>
    public void SetMasterVolume(float value)
    {
        AudioManager.Instance?.SetMasterVolume(value);
    }

    /// <summary>
    /// Wrapper for the music volume slider in the settings panel.
    /// Link your Music Slider's OnValueChanged to this.
    /// </summary>
    public void SetMusicVolume(float value)
    {
        AudioManager.Instance?.SetMusicVolume(value);
    }

    /// <summary>
    /// Wrapper for the SFX volume slider in the settings panel.
    /// Link your SFX Slider's OnValueChanged to this.
    /// </summary>
    public void SetSFXVolume(float value)
    {
        AudioManager.Instance?.SetSFXVolume(value);
    }
    /// <summary>
    /// Reset all audio settings to default and refresh UI.
    /// </summary>
    public void ResetSettings()
    {
        AudioManager.Instance?.ResetAudioSettings();
        RefreshSliders();
    }
    #endregion

    void OnDestroy()
    {
        // Safety: always restore timeScale if this object is destroyed
        Time.timeScale = 1f;
    }
}
