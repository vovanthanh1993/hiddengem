using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class StagePanel : MonoBehaviour
{
    [Header("Stage Panel")]
    [SerializeField] private Button[] chestButtons; // List button tương ứng với phần thưởng của các stage
    [SerializeField] private Image[] chestOpenImages; // List image chest open tương ứng với các stage
    [SerializeField] private TextMeshProUGUI stageText; // Text hiển thị stage hiện tại
    [SerializeField] private TextMeshProUGUI timerText; // Text hiển thị timer
    
    private float eventTimer = 0f;
    private bool timerRunning = false;
    
    private void Start()
    {
        InitializeStagePanel();
        SubscribeToEvents();
    }
    
    private void SubscribeToEvents()
    {
        if (StageManager.Instance != null)
        {
            StageManager.Instance.OnStageChanged += UpdateStage;
        }
    }
    
    private void Update()
    {
        if (timerRunning)
        {
            eventTimer -= Time.deltaTime;
            if (eventTimer < 0)
                eventTimer = 0;
            
            UpdateTimer();
        }
    }
    
    public void StartTimer(float seconds)
    {
        eventTimer = seconds;
        timerRunning = true;
    }
    
    private void UpdateTimer()
    {
        if (timerText == null) return;
        
        int hours = Mathf.FloorToInt(eventTimer / 3600);
        int minutes = Mathf.FloorToInt((eventTimer % 3600) / 60);
        int seconds = Mathf.FloorToInt(eventTimer % 60);
        
        timerText.text = $"{hours:00}:{minutes:00}:{seconds:00}";
    }
    
    private void InitializeStagePanel()
    {
        // Setup stage buttons với phần thưởng của từng stage
        SetupStageButtons();
        
        // Ẩn tất cả chest open images ban đầu
        InitializeChestOpenImages();
        
        // Start timer (example: 10 hours)
        StartTimer(10 * 60 * 60);
        
        // Initialize stage display
        if (StageManager.Instance != null)
        {
            UpdateStage(StageManager.Instance.CurrentStageId);
        }
    }
    
    private void SetupStageButtons()
    {
        if (chestButtons == null || chestButtons.Length == 0) return;
        
        // Setup từng button với phần thưởng tương ứng
        for (int i = 0; i < chestButtons.Length; i++)
        {
            if (chestButtons[i] == null) continue;
            
            int stageId = i + 1; // Stage ID bắt đầu từ 1
            
            // Lấy phần thưởng của stage này
            StageRewardConfig reward = GetRewardForStage(stageId);
            
            // Hiển thị phần thưởng trên button
            UpdateStageButtonReward(chestButtons[i], stageId, reward);
            
            // Ban đầu disable tất cả buttons (chưa hoàn thành stage nào)
            chestButtons[i].interactable = false;
            
            // Setup click handler để nhận phần thưởng
            int capturedStageId = stageId; // Capture để dùng trong lambda
            chestButtons[i].onClick.RemoveAllListeners();
            chestButtons[i].onClick.AddListener(() => OnStageButtonClicked(capturedStageId));
        }
    }
    
    private StageRewardConfig GetRewardForStage(int stageId)
    {
        // Lấy StageConfigData từ StageManager
        if (StageManager.Instance != null && StageManager.Instance.StageConfigData != null)
        {
            return StageManager.Instance.StageConfigData.GetRewardConfig(stageId);
        }
        return null;
    }
    
    private void UpdateStageButtonReward(Button button, int stageId, StageRewardConfig reward)
    {
        if (button == null) return;
        
        // Tìm Text component trong button để hiển thị phần thưởng
        TextMeshProUGUI buttonText = button.GetComponentInChildren<TextMeshProUGUI>();
        if (buttonText == null)
        {
            // Nếu không có Text, tạo mới hoặc dùng Text thông thường
            var text = button.GetComponentInChildren<UnityEngine.UI.Text>();
            if (text != null)
            {
                if (reward != null)
                {
                    text.text = $"{GetRomanNumeral(stageId)}\n+{reward.amount}";
                }
                else
                {
                    text.text = GetRomanNumeral(stageId);
                }
            }
        }
        else
        {
            if (reward != null)
            {
                buttonText.text = $"{GetRomanNumeral(stageId)}\n+{reward.amount}";
            }
            else
            {
                buttonText.text = GetRomanNumeral(stageId);
            }
        }
    }
    
    private void OnStageButtonClicked(int stageId)
    {
        // Lấy phần thưởng của stage này
        StageRewardConfig reward = GetRewardForStage(stageId);
        if (reward != null)
        {
            // Phát sound khi mở rương
            AudioManager.Instance.PlayRewardSound();
            
            // Nhận phần thưởng ngay lập tức
            if (reward.rewardType == RewardType.Pickaxe)
            {
                PickaxeManager.Instance.AddPickaxes(reward.amount);
                Debug.Log($"Claimed reward for Stage {stageId}: {reward.amount} pickaxes");
            }
            
            // Ẩn button và hiện chest open image tương ứng
            if (stageId > 0 && stageId <= chestButtons.Length)
            {
                // Ẩn button
                chestButtons[stageId - 1].gameObject.SetActive(false);
                
                // Hiện chest open image tương ứng
                if (chestOpenImages != null && stageId <= chestOpenImages.Length && chestOpenImages[stageId - 1] != null)
                {
                    chestOpenImages[stageId - 1].gameObject.SetActive(true);
                }
            }
            
            // Hiển thị RewardChestPopup để người chơi biết đã nhận phần thưởng
            if (UIManager.Instance != null)
            {
                UIManager.Instance.ShowRewardChest(reward);
            }
        }
    }
    
    private void InitializeChestOpenImages()
    {
        // Ẩn tất cả chest open images ban đầu
        if (chestOpenImages != null)
        {
            foreach (var chestImage in chestOpenImages)
            {
                if (chestImage != null)
                {
                    chestImage.gameObject.SetActive(false);
                }
            }
        }
    }
    
    // Method để unlock button của stage vừa hoàn thành
    public void UnlockStageRewardButton(int completedStageId)
    {
        if (chestButtons == null || completedStageId <= 0 || completedStageId > chestButtons.Length)
            return;
        
        int buttonIndex = completedStageId - 1;
        if (chestButtons[buttonIndex] != null)
        {
            chestButtons[buttonIndex].interactable = true;
            Debug.Log($"Unlocked reward button for Stage {completedStageId}");
        }
    }
    
    public void UpdateStage(int stageId)
    {
        // Update stage text
        if (stageText != null)
        {
            stageText.text = $"Gate {GetRomanNumeral(stageId)}";
        }
        
        // Highlight current stage button
        UpdateStageHighlight(stageId);
    }
    
    public void UpdateStageHighlight(int stageId)
    {
        // Highlight current stage button
        if (chestButtons == null) return;
        
        for (int i = 0; i < chestButtons.Length; i++)
        {
            if (chestButtons[i] == null) continue;
            
            if (i == stageId - 1)
            {
                // Highlight button
                var colors = chestButtons[i].colors;
                colors.normalColor = Color.yellow;
                chestButtons[i].colors = colors;
            }
            else
            {
                var colors = chestButtons[i].colors;
                colors.normalColor = Color.white;
                chestButtons[i].colors = colors;
            }
        }
    }
    
    private void OnDestroy()
    {
        if (StageManager.Instance != null)
        {
            StageManager.Instance.OnStageChanged -= UpdateStage;
        }
    }
    
    private string GetRomanNumeral(int number)
    {
        switch (number)
        {
            case 1: return "I";
            case 2: return "II";
            case 3: return "III";
            case 4: return "IV";
            case 5: return "V";
            default: return number.ToString();
        }
    }
}

