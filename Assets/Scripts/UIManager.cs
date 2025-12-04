using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class UIManager : MonoBehaviour
{
    public static UIManager Instance { get; private set; }
    
    [Header("UI References")]
    [SerializeField] private TextMeshProUGUI pickaxeCountText;
    [SerializeField] private TextMeshProUGUI timerText;
    [SerializeField] private TextMeshProUGUI stageText;
    [SerializeField] private Button[] stageButtons;
    
    [Header("Popups")]
    [SerializeField] private GameObject addPickaxePopup;
    [SerializeField] private Button addPickaxeYesButton;
    [SerializeField] private Button addPickaxeNoButton;
    
    [Header("Reward")]
    [SerializeField] private GameObject rewardChestPopup;
    [SerializeField] private TextMeshProUGUI rewardText;
    
    private float eventTimer = 0f;
    private bool timerRunning = false;
    
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
        
        // Setup stage buttons - chỉ để hiển thị, không cần click
        // Buttons sẽ tự động highlight khi stage thay đổi
        for (int i = 0; i < stageButtons.Length; i++)
        {
            // Vô hiệu hóa interactable để không thể click
            stageButtons[i].interactable = false;
        }
        
        // Setup add pickaxe popup buttons
        if (addPickaxeYesButton != null)
        {
            addPickaxeYesButton.onClick.AddListener(() => OnAddPickaxeConfirmed(true));
        }
        if (addPickaxeNoButton != null)
        {
            addPickaxeNoButton.onClick.AddListener(() => OnAddPickaxeConfirmed(false));
        }
        
        // Hide popups initially
        if (addPickaxePopup != null)
            addPickaxePopup.SetActive(false);
        if (rewardChestPopup != null)
            rewardChestPopup.SetActive(false);
        
        // Start timer (example: 10 hours)
        StartTimer(10 * 60 * 60);
        
        // Initialize stage display
        if (StageManager.Instance != null)
        {
            UpdateStage(StageManager.Instance.CurrentStageId);
        }
    }
    
    private void SubscribeToEvents()
    {
        if (PickaxeManager.Instance != null)
        {
            PickaxeManager.Instance.OnPickaxeChanged += UpdatePickaxeCount;
        }
        
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
    
    private void UpdatePickaxeCount(int count)
    {
        if (pickaxeCountText != null)
        {
            pickaxeCountText.text = count.ToString();
        }
    }
    
    public void UpdateStage(int stageId)
    {
        if (stageText != null)
        {
            stageText.text = $"Gate {GetRomanNumeral(stageId)}";
        }
        
        // Highlight current stage button
        for (int i = 0; i < stageButtons.Length; i++)
        {
            if (i == stageId - 1)
            {
                // Highlight button
                var colors = stageButtons[i].colors;
                colors.normalColor = Color.yellow;
                stageButtons[i].colors = colors;
            }
            else
            {
                var colors = stageButtons[i].colors;
                colors.normalColor = Color.white;
                stageButtons[i].colors = colors;
            }
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
    

    
    public void ShowAddPickaxePopup()
    {
        if (addPickaxePopup != null)
        {
            addPickaxePopup.SetActive(true);
        }
    }
    
    private void OnAddPickaxeConfirmed(bool confirmed)
    {
        if (addPickaxePopup != null)
            addPickaxePopup.SetActive(false);
        
        GameManager.Instance?.OnAddPickaxeConfirmed(confirmed);
    }
    
    public void ShowRewardChest(StageRewardConfig reward)
    {
        if (rewardChestPopup != null)
        {
            rewardChestPopup.SetActive(true);
            if (rewardText != null && reward != null)
            {
                rewardText.text = $"{reward.amount}";
            }
        }
    }
    
    private void OnDestroy()
    {
        if (PickaxeManager.Instance != null)
        {
            PickaxeManager.Instance.OnPickaxeChanged -= UpdatePickaxeCount;
        }
        
        if (StageManager.Instance != null)
        {
            StageManager.Instance.OnStageChanged -= UpdateStage;
        }
    }
}

