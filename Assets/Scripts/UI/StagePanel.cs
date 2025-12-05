using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DG.Tweening;

public class StagePanel : MonoBehaviour
{
    [Header("Stage Panel")]
    [SerializeField] private Button[] chestButtons; // List of buttons corresponding to rewards for each stage
    [SerializeField] private Image[] chestOpenImages; // List of chest open images corresponding to each stage
    [SerializeField] private TextMeshProUGUI stageText; // Text displaying current stage
    [SerializeField] private TextMeshProUGUI timerText; // Text displaying timer
    
    private float eventTimer = 0f;
    private bool timerRunning = false;
    
    private void Start()
    {
        InitializeStagePanel();
        SubscribeToEvents();
        LoadRewardStates(); // Load unlock and claimed states from PlayerPrefs
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
        // Setup stage buttons with rewards for each stage
        SetupStageButtons();
        
        // Hide all chest open images initially
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
        
        // Setup each button with corresponding reward
        for (int i = 0; i < chestButtons.Length; i++)
        {
            if (chestButtons[i] == null) continue;
            
            int stageId = i + 1; // Stage ID starts from 1
            
            // Get reward for this stage
            StageRewardConfig reward = GetRewardForStage(stageId);
            
            // Display reward on button
            UpdateStageButtonReward(chestButtons[i], stageId, reward);
            
            // Initially disable all buttons (no stage completed yet)
            chestButtons[i].interactable = false;
            
            // Setup click handler to claim reward
            int capturedStageId = stageId; // Capture for use in lambda
            chestButtons[i].onClick.RemoveAllListeners();
            chestButtons[i].onClick.AddListener(() => OnStageButtonClicked(capturedStageId));
        }
    }
    
    private StageRewardConfig GetRewardForStage(int stageId)
    {
        // Get StageConfigData from StageManager
        if (StageManager.Instance != null && StageManager.Instance.StageConfigData != null)
        {
            return StageManager.Instance.StageConfigData.GetRewardConfig(stageId);
        }
        return null;
    }
    
    private void UpdateStageButtonReward(Button button, int stageId, StageRewardConfig reward)
    {
        if (button == null) return;
        
        // Find Text component in button to display reward
        TextMeshProUGUI buttonText = button.GetComponentInChildren<TextMeshProUGUI>();
        if (buttonText == null)
        {
            // If no Text, create new or use regular Text
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
        // Get reward for this stage
        StageRewardConfig reward = GetRewardForStage(stageId);
        if (reward != null)
        {
            // Play sound when opening chest
            AudioManager.Instance.PlayRewardSound();
            
            // Claim reward immediately
            if (reward.rewardType == RewardType.Pickaxe)
            {
                PickaxeManager.Instance.AddPickaxes(reward.amount);
                Debug.Log($"Claimed reward for Stage {stageId}: {reward.amount} pickaxes");
            }
            
            // Hide button and show corresponding chest open image
            if (stageId > 0 && stageId <= chestButtons.Length)
            {
                // Hide button
                chestButtons[stageId - 1].gameObject.SetActive(false);
                
                // Show corresponding chest open image
                if (chestOpenImages != null && stageId <= chestOpenImages.Length && chestOpenImages[stageId - 1] != null)
                {
                    chestOpenImages[stageId - 1].gameObject.SetActive(true);
                }
                
                // Save claimed state to PlayerPrefs
                SaveRewardClaimed(stageId, true);
            }
            
            // Show RewardChestPopup so player knows reward was received
            if (UIManager.Instance != null)
            {
                UIManager.Instance.ShowRewardChest(reward);
            }
        }
    }
    
    private void InitializeChestOpenImages()
    {
        // Hide all chest open images initially
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
    
    // Method to unlock button of the stage that was just completed
    public void UnlockStageRewardButton(int completedStageId)
    {
        if (chestButtons == null || completedStageId <= 0 || completedStageId > chestButtons.Length)
            return;
        
        int buttonIndex = completedStageId - 1;
        if (chestButtons[buttonIndex] != null)
        {
            chestButtons[buttonIndex].interactable = true;
            Debug.Log($"Unlocked reward button for Stage {completedStageId}");
            
            // Save unlock state to PlayerPrefs
            SaveRewardUnlocked(completedStageId, true);
            
            // Animation zoom in/out repeated 2 times in 3s
            RectTransform buttonRect = chestButtons[buttonIndex].GetComponent<RectTransform>();
            if (buttonRect != null)
            {
                Vector3 originalScale = buttonRect.localScale;
                Vector3 targetScale = originalScale * 1.4f; // Zoom to 140%
                
                // Create zoom in/out animation repeated 2 times in 3s (each cycle 1.5s)
                Sequence zoomSequence = DOTween.Sequence();
                zoomSequence.Append(buttonRect.DOScale(targetScale, 0.75f).SetEase(Ease.OutBack));
                zoomSequence.Append(buttonRect.DOScale(originalScale, 0.75f).SetEase(Ease.InBack));
                zoomSequence.SetLoops(2, LoopType.Restart); // Repeat 2 times
                zoomSequence.SetAutoKill(true);
            }
        }
    }
    
    public void UpdateStage(int stageId)
    {
        // Update stage text
        if (stageText != null)
        {
            stageText.text = $"Gate {GetRomanNumeral(stageId)}";
        }
    }
    
    #region Save/Load Reward States
    
    /// <summary>
    /// Save unlock state of reward to PlayerPrefs
    /// </summary>
    private void SaveRewardUnlocked(int stageId, bool unlocked)
    {
        PlayerPrefs.SetInt($"Stage_{stageId}_Unlocked", unlocked ? 1 : 0);
        PlayerPrefs.Save();
        Debug.Log($"Saved unlock state for stage {stageId}: {unlocked}");
    }
    
    /// <summary>
    /// Save claimed state of reward to PlayerPrefs
    /// </summary>
    private void SaveRewardClaimed(int stageId, bool claimed)
    {
        PlayerPrefs.SetInt($"Stage_{stageId}_Claimed", claimed ? 1 : 0);
        PlayerPrefs.Save();
        Debug.Log($"Saved claimed state for stage {stageId}: {claimed}");
    }
    
    /// <summary>
    /// Load unlock state of reward from PlayerPrefs
    /// </summary>
    private bool LoadRewardUnlocked(int stageId)
    {
        return PlayerPrefs.GetInt($"Stage_{stageId}_Unlocked", 0) == 1;
    }
    
    /// <summary>
    /// Load claimed state of reward from PlayerPrefs
    /// </summary>
    private bool LoadRewardClaimed(int stageId)
    {
        return PlayerPrefs.GetInt($"Stage_{stageId}_Claimed", 0) == 1;
    }
    
    /// <summary>
    /// Load all reward states from PlayerPrefs and apply to UI
    /// </summary>
    private void LoadRewardStates()
    {
        if (chestButtons == null) return;
        
        // Load state for all stages
        for (int i = 0; i < chestButtons.Length; i++)
        {
            int stageId = i + 1;
            bool isUnlocked = LoadRewardUnlocked(stageId);
            bool isClaimed = LoadRewardClaimed(stageId);
            
            if (chestButtons[i] != null)
            {
                if (isClaimed)
                {
                    // If already claimed, hide button and show chest open image
                    chestButtons[i].gameObject.SetActive(false);
                    
                    if (chestOpenImages != null && i < chestOpenImages.Length && chestOpenImages[i] != null)
                    {
                        chestOpenImages[i].gameObject.SetActive(true);
                    }
                }
                else if (isUnlocked)
                {
                    // If unlocked but not claimed yet, enable button
                    chestButtons[i].interactable = true;
                    chestButtons[i].gameObject.SetActive(true);
                }
                else
                {
                    // Not unlocked, disable button
                    chestButtons[i].interactable = false;
                    chestButtons[i].gameObject.SetActive(true);
                }
            }
        }
        
        Debug.Log("Loaded reward states from PlayerPrefs");
    }
    
    #endregion
    
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

