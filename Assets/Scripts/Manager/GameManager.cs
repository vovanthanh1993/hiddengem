using UnityEngine;
using UnityEngine.SceneManagement;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }
    
    [SerializeField] private StageManager stageManager;
    [SerializeField] private BoardManager boardManager;
    
    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
        }
    }
    
    private void Start()
    {
        InitializeGame();
    }
    
    private void InitializeGame()
    {
        // Load reached stage from PlayerPrefs, default is stage 1
        int savedStage = LoadReachedStage();
        
        // Check if all stages are completed
        if (stageManager != null && stageManager.StageConfigData != null)
        {
            int totalStages = stageManager.StageConfigData.stageConfigs.Length;
            
            // If savedStage exceeds totalStages, all stages are completed
            if (savedStage > totalStages)
            {
                // All stages completed, only show CompleteText and resetButton
                ShowCompletionScreen();
                return; // Don't load stage anymore
            }
        }
        
        // Not all stages completed, load stage normally
        stageManager.LoadStage(savedStage);
    }
    
    /// <summary>
    /// Show completion screen when all stages are completed
    /// </summary>
    private void ShowCompletionScreen()
    {
        // Hide board
        if (boardManager != null)
        {
            boardManager.SetBoardVisible(false);
        }
        
        // Show complete text and resetButton
        if (UIManager.Instance != null)
        {
            UIManager.Instance.ShowCompletionScreen();
        }
    }
    
    /// <summary>
    /// Load reached stage from PlayerPrefs
    /// </summary>
    private int LoadReachedStage()
    {
        int savedStage = PlayerPrefs.GetInt("ReachedStage", 1); // Default is stage 1
        Debug.Log($"[GameManager] Load stage from PlayerPrefs: {savedStage}");
        return savedStage;
    }
    
    /// <summary>
    /// Clear all saved data in PlayerPrefs and reset game to initial state
    /// </summary>
    public void ClearAllSavedData()
    {
        Debug.Log("=== CLEARING ALL SAVED DATA ===");
        
        // Delete all PlayerPrefs
        PlayerPrefs.DeleteAll();
        PlayerPrefs.Save();
        
        Debug.Log("All saved data has been cleared!");
        Debug.Log("Game will be reset to initial state when restart.");
        
        // Can reload scene immediately or just notify
        // Uncomment the line below if you want to reload scene immediately:
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }
    
    public void DigCellUI(CellUI cell)
    {
        if (cell == null || cell.IsRevealed) return;
        
        bool success = boardManager.DigCell(cell.BoardX, cell.BoardY);
        
        if (success)
        {
            // Check if stage is complete
            if (stageManager.CheckStageComplete())
            {
                stageManager.CompleteStage();
            }
        }
    }
    
    public void OnAddPickaxeConfirmed(bool confirmed)
    {
        if (confirmed)
        {
            PickaxeManager.Instance.AddPickaxes(100);
        }
    }
}

