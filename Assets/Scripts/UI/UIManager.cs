using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class UIManager : MonoBehaviour
{
    public static UIManager Instance { get; private set; }
    
    [Header("UI References")]
    [SerializeField] private TextMeshProUGUI pickaxeCountText;
    [SerializeField] private StagePanel stagePanel; // Reference đến StagePanel
    [SerializeField] private AddPickaxePopup addPickaxePopup; // Reference đến AddPickaxePopup
    [SerializeField] private RewardChestPopup rewardChestPopup; // Reference đến RewardChestPopup
    [SerializeField] private SettingPanel settingPanel; // Reference đến SettingPanel
    
    [Header("Complete Screen")]
    [SerializeField] private TextMeshProUGUI completeText; // Text hiển thị khi hoàn thành tất cả stage
    
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
        
        HideCompletionScreen(); // Ẩn complete screen và text ban đầu
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
    
    // Method để unlock button của stage vừa hoàn thành
    public void UnlockStageRewardButton(int completedStageId)
    {
        if (stagePanel != null)
        {
            stagePanel.UnlockStageRewardButton(completedStageId);
        }
    }
    
    public void UpdateStage(int stageId)
    {
        // Update stage trong StagePanel
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
        // Hiển thị complete text (đã có sẵn text)
        if (completeText != null)
        {
            completeText.gameObject.SetActive(true);
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

