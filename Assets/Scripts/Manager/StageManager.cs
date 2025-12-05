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
    [SerializeField] private ParticleSystem stageCompleteParticlePrefab; // Particle effect when stage is completed
    [SerializeField] private float stageCompleteParticleDuration = 1f; // Duration to display particle
    
    public GemConfigData GetGemConfigData() => gemConfigData;
    
    private int currentStageId = 1;
    private StageConfig currentStageConfig;
    private List<Gem> stageGems;
    private Dictionary<Gem, GemOrientation> gemOrientations; // Store orientation for each gem
    private bool isStageCompleted = false;
    
    public event Action<int> OnStageChanged;
    
    public int CurrentStageId => currentStageId;
    public StageConfig CurrentStageConfig => currentStageConfig;
    public StageConfigData StageConfigData => stageConfigData;
    
    /// <summary>
    /// Save reached stage to PlayerPrefs
    /// </summary>
    private void SaveReachedStage(int stageId)
    {
        // Get current reached stage
        int currentReachedStage = PlayerPrefs.GetInt("ReachedStage", 1);
        
        // Only save if new stage is greater than current reached stage
        if (stageId > currentReachedStage)
        {
            PlayerPrefs.SetInt("ReachedStage", stageId);
            PlayerPrefs.Save(); // Important: must call Save() to save to file immediately
            Debug.Log($"Saved reached stage: {stageId} (previous: {currentReachedStage})");
        }
        else
        {
            Debug.Log($"Not saving stage {stageId} because higher stage already exists: {currentReachedStage}");
        }
    }
    
    /// <summary>
    /// Load reached stage from PlayerPrefs
    /// </summary>
    public int LoadReachedStage()
    {
        int savedStage = PlayerPrefs.GetInt("ReachedStage", 1); // Default is stage 1
        Debug.Log($"Load reached stage from PlayerPrefs: {savedStage}");
        return savedStage;
    }
    
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
        
        // Automatically load asset from Resources if not assigned
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
        // Don't load stage here anymore because GameManager will load reached stage
        // LoadStage(1); // Removed - GameManager will load stage from PlayerPrefs
    }
    
    public void LoadStage(int stageId)
    {
        // Check null before using
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
        
        // Don't place gems on board immediately, only store in pool
        // Gems will be spawned when player digs correctly
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
        // Clear old gems from previous stage (if any)
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
                // Store orientation for this gem
                gemOrientations[gem] = request.orientation;
            }
        }
    }
    
    public bool CheckStageComplete()
    {
        if (isStageCompleted) return false; // Already completed, don't check again
        
        var collectedGems = boardManager.GetCollectedGems();
        int totalGemsNeeded = boardManager.GetTotalGemsNeeded();
        return collectedGems.Count >= totalGemsNeeded && totalGemsNeeded > 0;
    }
    
    public void CompleteStage()
    {
        if (isStageCompleted) return; // Avoid calling multiple times
        
        isStageCompleted = true;
        
        // Play win sound when stage is completed
        if (AudioManager.Instance != null)
        {
            AudioManager.Instance.PlayWinSound();
        }
        
        // Spawn particle effect in the middle of screen when stage is completed (delay 0.2s)
        StartCoroutine(DelayedSpawnStageCompleteParticle(0.2f));
        
        // Disable input to prevent digging
        if (boardManager != null)
        {
            boardManager.SetInputEnabled(false);
        }
        
        int completedStageId = currentStageId;
        
        // Save next stage (unlocked) immediately when stage is completed
        // To ensure save even if user leaves game before loading next stage
        // When completing stage 5 (last stage), will save as 6 to know all stages are completed
        int nextStageId = completedStageId + 1;
        SaveReachedStage(nextStageId);
        
        // Unlock reward button for the stage that was just completed
        UIManager.Instance?.UnlockStageRewardButton(completedStageId);
        
        // Delay 1s before switching to next stage
        StartCoroutine(DelayedLoadNextStage(1f));
    }
    
    private System.Collections.IEnumerator DelayedLoadNextStage(float delay)
    {
        yield return new WaitForSeconds(delay);
        
        // Automatically switch to next stage
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
        // Hide board
        if (boardManager != null)
        {
            boardManager.SetBoardVisible(false);
        }
        
        // Show completion screen
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
            int nextStageId = currentStageId + 1;
            LoadStage(nextStageId);
            
            // Stage already saved in CompleteStage(), no need to save again here
        }
        else
        {
            Debug.Log("All stages completed!");
            // Show completion screen
            // Stage already saved in CompleteStage(), no need to save again here
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
        
        // Create particle system instance
        ParticleSystem particle = Instantiate(stageCompleteParticlePrefab);
        
        // Place particle in the middle of screen
        // With Canvas Screen Space Overlay, need to calculate world space position
        Canvas canvas = FindObjectOfType<Canvas>();
        if (canvas != null && canvas.renderMode == RenderMode.ScreenSpaceOverlay)
        {
            // Get screen size
            Vector2 screenSize = new Vector2(Screen.width, Screen.height);
            Vector2 centerScreen = screenSize * 0.5f;
            
            // Convert screen position to world position
            // With Screen Space Overlay, need to place particle at a distance before camera
            Camera mainCamera = Camera.main;
            if (mainCamera != null)
            {
                Vector3 worldPosition = mainCamera.ScreenToWorldPoint(new Vector3(centerScreen.x, centerScreen.y, mainCamera.nearClipPlane + 1f));
                worldPosition.z = mainCamera.transform.position.z + 1f; // Place slightly before camera
                particle.transform.position = worldPosition;
            }
            else
            {
                // If no camera, place at origin
                particle.transform.position = Vector3.zero;
            }
        }
        else
        {
            // If no Canvas or Canvas is not Screen Space Overlay, place at origin
            particle.transform.position = Vector3.zero;
        }
        
        // Ensure Particle System renders correctly
        var renderer = particle.GetComponent<ParticleSystemRenderer>();
        if (renderer != null)
        {
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
        }
        
        // Play particle system
        particle.Play();
        
        // Destroy particle after completion
        Destroy(particle.gameObject, stageCompleteParticleDuration);
    }
    
    private void OnDestroy()
    {
        OnStageChanged = null;
    }
}

