using UnityEngine;

/// <summary>
/// Singleton manager for the Book UI Gallery.
/// Handles pausing input and toggling the book's visibility.
/// </summary>
public class BookUIManager : MonoBehaviour
{
    public static BookUIManager Instance { get; private set; }

    [System.Serializable]
    public class BookPage
    {
        public string pageName; // Just for organization in inspector
        public Sprite[] animationFrames;
    }

    [Header("UI References")]
    [Tooltip("The main container panel for the Book UI.")]
    public GameObject bookPanel;
    
    [Tooltip("The script that plays the 120-frame sequences.")]
    public UISequencePlayer sequencePlayer;

    [Header("Page Content")]
    public BookPage[] pages;
    private int currentPageIndex = 0;

    [Tooltip("Whether the book is currently open (blocks player movement).")]
    public bool IsOpen { get; private set; }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        
        // DontDestroyOnLoad(gameObject); // Optional: if you want it to persist between chapters

        if (bookPanel != null)
            bookPanel.SetActive(false);
    }

    /// <summary>
    /// Opens the book and pauses player movement/interactions.
    /// Link your HUD button to this method.
    /// </summary>
    public void OpenBook()
    {
        if (pages == null || pages.Length == 0)
        {
            Debug.LogWarning("[BookUIManager] No pages assigned in the BookUIManager!");
            return;
        }

        IsOpen = true;
        if (bookPanel != null)
            bookPanel.SetActive(true);

        // Always start on the first page when opening
        currentPageIndex = 0;
        DisplayCurrentPage();

        Debug.Log("[BookUIManager] Book Opened.");
    }

    /// <summary>
    /// Closes the book and resumes the game.
    /// Link your close button in the book panel to this.
    /// </summary>
    public void CloseBook()
    {
        IsOpen = false;
        if (bookPanel != null)
            bookPanel.SetActive(false);

        // Time.timeScale = 1f;

        Debug.Log("[BookUIManager] Book Closed.");
    }

    /// <summary>
    /// Simple toggle for convenience.
    /// </summary>
    public void ToggleBook()
    {
        if (IsOpen) CloseBook();
        else OpenBook();
    }

    #region Navigation
    public void NextPage()
    {
        if (pages == null || pages.Length <= 1) return;
        
        currentPageIndex++;
        if (currentPageIndex >= pages.Length) currentPageIndex = 0; // Wrap to start or clamp
        
        DisplayCurrentPage();
    }

    public void PreviousPage()
    {
        if (pages == null || pages.Length <= 1) return;

        currentPageIndex--;
        if (currentPageIndex < 0) currentPageIndex = pages.Length - 1; // Wrap to end or clamp

        DisplayCurrentPage();
    }

    private void DisplayCurrentPage()
    {
        if (sequencePlayer != null && pages.Length > currentPageIndex)
        {
            sequencePlayer.SetFrames(pages[currentPageIndex].animationFrames);
            Debug.Log($"[BookUIManager] Showing Page {currentPageIndex + 1}: {pages[currentPageIndex].pageName}");
        }
    }
    #endregion
}
