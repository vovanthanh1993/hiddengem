using System;
using System.Collections.Generic;
using UnityEngine;

public class StageManager : MonoBehaviour
{
    public static StageManager Instance { get; private set; }
    
    [SerializeField] private StageConfigData stageConfigData;
    [SerializeField] private GemConfigData gemConfigData;
    [SerializeField] private BoardManager boardManager;
    [SerializeField] private GameObject gemPrefab;
    
    public GemConfigData GetGemConfigData() => gemConfigData;
    
    private int currentStageId = 1;
    private StageConfig currentStageConfig;
    private List<Gem> stageGems;
    private Dictionary<Gem, GemOrientation> gemOrientations; // Lưu orientation cho mỗi gem
    private bool isStageCompleted = false;
    
    public event Action<int> OnStageChanged;
    
    public int CurrentStageId => currentStageId;
    public StageConfig CurrentStageConfig => currentStageConfig;
    
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
        LoadStage(1);
    }
    
    public void LoadStage(int stageId)
    {
        currentStageId = stageId;
        currentStageConfig = stageConfigData.GetStageConfig(stageId);
        isStageCompleted = false; // Reset completion flag
        
        if (currentStageConfig == null)
        {
            Debug.LogError($"Stage {stageId} not found!");
            return;
        }
        
        // Initialize board
        boardManager.InitializeBoard(
            currentStageConfig.boardWidth,
            currentStageConfig.boardHeight
        );
        
        // Create gems for this stage
        CreateStageGems();
        
        // Không đặt gems lên board ngay từ đầu, chỉ lưu vào pool
        // Gems sẽ được spawn khi người chơi đào trúng
        boardManager.InitializeGemPool(stageGems, gemConfigData, gemOrientations);
        
        // Place dynamites
        boardManager.PlaceDynamites(currentStageConfig.dynamiteCount);
        
        // Notify UI about stage change
        OnStageChanged?.Invoke(stageId);
    }
    
    private void CreateStageGems()
    {
        stageGems = new List<Gem>();
        gemOrientations = new Dictionary<Gem, GemOrientation>();
        
        foreach (var request in currentStageConfig.gemRequests)
        {
            var gemConfig = gemConfigData.GetGemConfig(request.gemId);
            if (gemConfig == null) continue;
            
            for (int i = 0; i < request.count; i++)
            {
                Gem gem;
                if (gemPrefab != null)
                {
                    gem = Instantiate(gemPrefab).GetComponent<Gem>();
                }
                else
                {
                    GameObject gemObj = new GameObject($"Gem_{request.gemId}_{i}");
                    gem = gemObj.AddComponent<Gem>();
                }
                
                gem.Initialize(
                    gemConfig.gemId,
                    gemConfig.width,
                    gemConfig.height,
                    gemConfig.gemColor
                );
                
                stageGems.Add(gem);
                // Lưu orientation cho gem này
                gemOrientations[gem] = request.orientation;
            }
        }
    }
    
    public bool CheckStageComplete()
    {
        if (isStageCompleted) return false; // Đã hoàn thành rồi, không check lại
        
        var collectedGems = boardManager.GetCollectedGems();
        int totalGemsNeeded = boardManager.GetTotalGemsNeeded();
        return collectedGems.Count >= totalGemsNeeded && totalGemsNeeded > 0;
    }
    
    public void CompleteStage()
    {
        if (isStageCompleted) return; // Tránh gọi nhiều lần
        
        isStageCompleted = true;
        
        var reward = stageConfigData.GetRewardConfig(currentStageId);
        if (reward != null)
        {
            if (reward.rewardType == RewardType.Pickaxe)
            {
                PickaxeManager.Instance.AddPickaxes(reward.amount);
            }
        }
        
        // Show reward chest
        UIManager.Instance?.ShowRewardChest(reward);
        
        // Tự động chuyển sang stage tiếp theo sau một khoảng thời gian ngắn
        if (currentStageId < stageConfigData.stageConfigs.Length)
        {
            StartCoroutine(AutoLoadNextStage(2f)); // Đợi 2 giây để show reward trước khi chuyển stage
        }
        else
        {
            Debug.Log("All stages completed!");
            // Show completion screen
        }
    }
    
    private System.Collections.IEnumerator AutoLoadNextStage(float delay)
    {
        yield return new WaitForSeconds(delay);
        LoadNextStage();
    }
    
    public void LoadNextStage()
    {
        if (currentStageId < stageConfigData.stageConfigs.Length)
        {
            LoadStage(currentStageId + 1);
        }
        else
        {
            Debug.Log("All stages completed!");
            // Show completion screen
        }
    }
    
    private void OnDestroy()
    {
        OnStageChanged = null;
    }
}

