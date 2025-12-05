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
    [SerializeField] private ParticleSystem stageCompleteParticlePrefab; // Particle effect khi hoàn thành stage
    [SerializeField] private float stageCompleteParticleDuration = 1f; // Thời gian hiển thị particle
    
    public GemConfigData GetGemConfigData() => gemConfigData;
    
    private int currentStageId = 1;
    private StageConfig currentStageConfig;
    private List<Gem> stageGems;
    private Dictionary<Gem, GemOrientation> gemOrientations; // Lưu orientation cho mỗi gem
    private bool isStageCompleted = false;
    
    public event Action<int> OnStageChanged;
    
    public int CurrentStageId => currentStageId;
    public StageConfig CurrentStageConfig => currentStageConfig;
    public StageConfigData StageConfigData => stageConfigData;
    
    public List<Gem> GetCollectedGems()
    {
        if (boardManager == null) return new List<Gem>();
        return boardManager.GetCollectedGems();
    }
    
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
        
        // Tự động load asset từ Resources nếu chưa được assign
        if (stageConfigData == null)
        {
            stageConfigData = Resources.Load<StageConfigData>("StageConfigData");
            if (stageConfigData == null)
            {
                Debug.LogError("Failed to load StageConfigData from Resources!");
            }
        }
        
        if (gemConfigData == null)
        {
            gemConfigData = Resources.Load<GemConfigData>("GemConfigData");
            if (gemConfigData == null)
            {
                Debug.LogError("Failed to load GemConfigData from Resources!");
            }
        }
    }
    
    private void Start()
    {
        LoadStage(1);
    }
    
    public void LoadStage(int stageId)
    {
        // Kiểm tra null trước khi sử dụng
        if (stageConfigData == null)
        {
            Debug.LogError("StageConfigData is not assigned!");
            return;
        }
        
        if (boardManager == null)
        {
            Debug.LogError("BoardManager is not assigned!");
            return;
        }
        
        if (gemConfigData == null)
        {
            Debug.LogError("GemConfigData is not assigned!");
            return;
        }
        
        currentStageId = stageId;
        currentStageConfig = stageConfigData.GetStageConfig(stageId);
        isStageCompleted = false; // Reset completion flag
        
        if (currentStageConfig == null)
        {
            Debug.LogError($"Stage {stageId} not found!");
            return;
        }
        
        // Initialize board (sẽ enable input tự động)
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
        
        // Enable input sau khi load stage xong
        boardManager.SetInputEnabled(true);
        
        // Notify UI about stage change
        OnStageChanged?.Invoke(stageId);
    }
    
    private void CreateStageGems()
    {
        // Xóa các gem cũ từ stage trước (nếu có)
        if (stageGems != null)
        {
            foreach (var oldGem in stageGems)
            {
                if (oldGem != null && oldGem.gameObject != null)
                {
                    Destroy(oldGem.gameObject);
                }
            }
        }
        
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
        
        // Phát sound win khi hoàn thành stage
        if (AudioManager.Instance != null)
        {
            AudioManager.Instance.PlayWinSound();
        }
        
        // Spawn particle effect ở giữa màn hình khi hoàn thành stage (delay 0.2s)
        StartCoroutine(DelayedSpawnStageCompleteParticle(0.2f));
        
        // Disable input để không cho đào nữa
        if (boardManager != null)
        {
            boardManager.SetInputEnabled(false);
        }
        
        int completedStageId = currentStageId;
        
        // Mở khóa button phần thưởng của stage vừa hoàn thành
        UIManager.Instance?.UnlockStageRewardButton(completedStageId);
        
        // Delay 1s trước khi chuyển sang stage tiếp theo
        StartCoroutine(DelayedLoadNextStage(1f));
    }
    
    private System.Collections.IEnumerator DelayedLoadNextStage(float delay)
    {
        yield return new WaitForSeconds(delay);
        
        // Tự động chuyển sang stage tiếp theo
        if (currentStageId < stageConfigData.stageConfigs.Length)
        {
            LoadNextStage();
        }
        else
        {
            Debug.Log("All stages completed!");
            // Show completion screen
            ShowCompletionScreen();
        }
    }
    
    private void ShowCompletionScreen()
    {
        // Ẩn board
        if (boardManager != null)
        {
            boardManager.SetBoardVisible(false);
        }
        
        // Hiển thị complete screen
        UIManager.Instance?.ShowCompletionScreen();
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
    
    private System.Collections.IEnumerator DelayedSpawnStageCompleteParticle(float delay)
    {
        yield return new WaitForSeconds(delay);
        SpawnStageCompleteParticle();
    }
    
    private void SpawnStageCompleteParticle()
    {
        if (stageCompleteParticlePrefab == null)
        {
            Debug.LogWarning("Stage complete particle prefab is null!");
            return;
        }
        
        // Tạo particle system instance
        ParticleSystem particle = Instantiate(stageCompleteParticlePrefab);
        
        // Đặt particle ở giữa màn hình
        // Với Canvas Screen Space Overlay, cần tính toán vị trí world space
        Canvas canvas = FindObjectOfType<Canvas>();
        if (canvas != null && canvas.renderMode == RenderMode.ScreenSpaceOverlay)
        {
            // Lấy kích thước màn hình
            Vector2 screenSize = new Vector2(Screen.width, Screen.height);
            Vector2 centerScreen = screenSize * 0.5f;
            
            // Convert screen position sang world position
            // Với Screen Space Overlay, cần đặt particle ở một khoảng cách trước camera
            Camera mainCamera = Camera.main;
            if (mainCamera != null)
            {
                Vector3 worldPosition = mainCamera.ScreenToWorldPoint(new Vector3(centerScreen.x, centerScreen.y, mainCamera.nearClipPlane + 1f));
                worldPosition.z = mainCamera.transform.position.z + 1f; // Đặt trước camera một chút
                particle.transform.position = worldPosition;
            }
            else
            {
                // Nếu không có camera, đặt ở origin
                particle.transform.position = Vector3.zero;
            }
        }
        else
        {
            // Nếu không có Canvas hoặc Canvas không phải Screen Space Overlay, đặt ở origin
            particle.transform.position = Vector3.zero;
        }
        
        // Đảm bảo Particle System render đúng
        var renderer = particle.GetComponent<ParticleSystemRenderer>();
        if (renderer != null)
        {
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
        }
        
        // Play particle system
        particle.Play();
        
        // Destroy particle sau khi hoàn thành
        Destroy(particle.gameObject, stageCompleteParticleDuration);
    }
    
    private void OnDestroy()
    {
        OnStageChanged = null;
    }
}

