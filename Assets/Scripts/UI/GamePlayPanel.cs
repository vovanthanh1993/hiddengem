using UnityEngine;
using UnityEngine.UI;

public class GamePlayPanel : MonoBehaviour
{
    [Header("UI Buttons")]
    [SerializeField] private Button settingBtn;
    [SerializeField] private Button resetButton;

    private void Start() {
        settingBtn.onClick.AddListener(OnSettingButtonClicked);
        resetButton.onClick.AddListener(OnResetButtonClicked);
        
        // Hide resetButton initially, only show when all stages are completed
        if (resetButton != null)
        {
            resetButton.gameObject.SetActive(false);
        }
        
        // Subscribe to events to know when all stages are completed
        SubscribeToEvents();
    }
    
    private void SubscribeToEvents()
    {
        // Check if all stages are completed on startup
        CheckAllStagesCompleted();
    }
    
    private void CheckAllStagesCompleted()
    {
        if (StageManager.Instance != null && StageManager.Instance.StageConfigData != null)
        {
            int currentStage = StageManager.Instance.CurrentStageId;
            int totalStages = StageManager.Instance.StageConfigData.stageConfigs.Length;
            
            // If currentStage exceeds totalStages, all stages are completed
            if (currentStage > totalStages)
            {
                ShowResetButton();
            }
        }
    }

    private void OnSettingButtonClicked() {
        if (UIManager.Instance != null)
        {
            UIManager.Instance.ShowSettingPanel();
            AudioManager.Instance.PlayPopupSound();
        }
    }

    private void OnResetButtonClicked() {
        AudioManager.Instance.PlayClickSound();
        if (GameManager.Instance != null)
        {
            GameManager.Instance.ClearAllSavedData();
        }
    }
    
    /// <summary>
    /// Method for UIManager to call when all stages are completed
    /// </summary>
    public void ShowResetButton()
    {
        if (resetButton != null)
        {
            resetButton.gameObject.SetActive(true);
        }
    }
}
