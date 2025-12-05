using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class UIManager : MonoBehaviour
{
    public static UIManager Instance { get; private set; }
    
    [Header("UI References")]
    [SerializeField] private TextMeshProUGUI pickaxeCountText;
    [SerializeField] private StagePanel stagePanel; // Reference to StagePanel
    [SerializeField] private AddPickaxePopup addPickaxePopup; // Reference to AddPickaxePopup
    [SerializeField] private RewardChestPopup rewardChestPopup; // Reference to RewardChestPopup
    [SerializeField] private SettingPanel settingPanel; // Reference to SettingPanel

    [SerializeField] private GamePlayPanel gamePlayPanel; // Reference to GamePlayPanel
    
    [Header("Complete Screen")]
    [SerializeField] private TextMeshProUGUI completeText; // Text displayed when all stages are completed
    
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
        InitializeUI();
        SubscribeToEvents();
    }
    
    private void InitializeUI()
    {
        // Initialize pickaxe count
        if (PickaxeManager.Instance != null)
        {
            UpdatePickaxeCount(PickaxeManager.Instance.CurrentPickaxes);
        }
        
        // Check if all stages are completed to decide whether to show CompleteText
        CheckAndShowCompletionScreen();
    }
    
    /// <summary>
    /// Check and show completion screen if all stages are completed
    /// </summary>
    private void CheckAndShowCompletionScreen()
    {
        if (StageManager.Instance != null && StageManager.Instance.StageConfigData != null)
        {
            int savedStage = PlayerPrefs.GetInt("ReachedStage", 1);
            int totalStages = StageManager.Instance.StageConfigData.stageConfigs.Length;
            
            // If all stages are completed (savedStage > totalStages), show CompleteText
            if (savedStage > totalStages)
            {
                ShowCompletionScreen();
            }
            else
            {
                HideCompletionScreen(); // Hide if not all stages completed
            }
        }
        else
        {
            HideCompletionScreen(); // Hide if StageManager is not available
        }
    }
    
    private void SubscribeToEvents()
    {
        if (PickaxeManager.Instance != null)
        {
            PickaxeManager.Instance.OnPickaxeChanged += UpdatePickaxeCount;
        }
    }
    
    private void UpdatePickaxeCount(int count)
    {
        if (pickaxeCountText != null)
        {
            pickaxeCountText.text = count.ToString();
        }
    }
    
    // Method to unlock button of the stage that was just completed
    public void UnlockStageRewardButton(int completedStageId)
    {
        if (stagePanel != null)
        {
            stagePanel.UnlockStageRewardButton(completedStageId);
        }
    }
    
    public void UpdateStage(int stageId)
    {
        // Update stage in StagePanel
        if (stagePanel != null)
        {
            stagePanel.UpdateStage(stageId);
        }
    }
    

    
    public void ShowAddPickaxePopup()
    {
        if (addPickaxePopup != null)
        {
            addPickaxePopup.Show();
        }
    }
    
    public void ShowRewardChest(StageRewardConfig reward)
    {
        if (rewardChestPopup != null)
        {
            rewardChestPopup.Show(reward);
        }
    }
    
    public void HideRewardChest()
    {
        if (rewardChestPopup != null)
        {
            rewardChestPopup.Hide();
        }
    }
    
    public void ShowSettingPanel()
    {
        if (settingPanel != null)
        {
            settingPanel.gameObject.SetActive(true);
        }
    }
    
    public void HideSettingPanel()
    {
        if (settingPanel != null)
        {
            settingPanel.gameObject.SetActive(false);
        }
    }
    
    
    public void ShowCompletionScreen()
    {
        // Show complete text (text already exists)
        if (completeText != null)
        {
            completeText.gameObject.SetActive(true);
        }
        
        // Show resetButton in GamePlayPanel
        ShowResetButton();
    }
    
    private void ShowResetButton()
    {
        // Find GamePlayPanel and show resetButton
        if (gamePlayPanel != null)
        {
            gamePlayPanel.ShowResetButton();
        }
    }
    
    public void HideCompletionScreen()
    {
        if (completeText != null)
        {
            completeText.gameObject.SetActive(false);
        }
    }
    
    private void OnDestroy()
    {
        if (PickaxeManager.Instance != null)
        {
            PickaxeManager.Instance.OnPickaxeChanged -= UpdatePickaxeCount;
        }
    }
}

