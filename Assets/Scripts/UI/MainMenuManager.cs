using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Attached to the Main Menu canvas. Handles button clicks for starting the game
/// and managing the menu state.
/// </summary>
public class MainMenuManager : MonoBehaviour
{
    [Header("Menu Elements")]
    public GameObject mainPanel;
    public GameObject settingsPanel;
    public GameObject creditsPanel;

    private void Start()
    {
        ShowMain();
        
        // Ensure BGM starts if we have a track for the menu
        // AudioManager.Instance?.PlayMusic(menuMusic);
    }

    /// <summary>
    /// Called by the Play button.
    /// Shows the loading screen and triggers the GameManager to load the first chapter.
    /// </summary>
    public void PlayGame()
    {
        if (GameManager.Instance != null)
        {
            // The loading screen will be shown by GameManager.StartGame()
            GameManager.Instance.StartGame();
        }
        else
        {
            Debug.LogError("[MainMenuManager] No GameManager found in scene! Please ensure the GameManager prefab is in the Main Menu scene.");
        }
    }

    public void ShowSettings()
    {
        mainPanel.SetActive(false);
        settingsPanel.SetActive(true);
    }

    public void ShowMain()
    {
        mainPanel.SetActive(true);
        if (settingsPanel != null) settingsPanel.SetActive(false);
        if (creditsPanel != null) creditsPanel.SetActive(false);
    }

    public void QuitGame()
    {
        Debug.Log("[MainMenuManager] Quit Game requested.");
        Application.Quit();
    }
}
