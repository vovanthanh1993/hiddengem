using UnityEngine;
using System.Collections.Generic;

#if UNITY_EDITOR
using UnityEditor;
#endif

public class GameDataInitializer : MonoBehaviour
{
    [ContextMenu("Initialize Game Data")]
    public void InitializeGameData()
    {
        CreateGemConfigData();
        CreateStageConfigData();
    }
    
    private void CreateGemConfigData()
    {
        GemConfigData gemData = ScriptableObject.CreateInstance<GemConfigData>();
        
        gemData.gemConfigs = new GemConfig[]
        {
            new GemConfig(1, 1, 2, Color.red),
            new GemConfig(2, 1, 3, Color.blue),
            new GemConfig(3, 1, 4, Color.green),
            new GemConfig(4, 1, 5, Color.yellow),
            new GemConfig(5, 2, 2, Color.magenta),
            new GemConfig(6, 2, 3, Color.cyan),
            new GemConfig(7, 2, 4, new Color(1f, 0.5f, 0f)), // Orange
            new GemConfig(8, 3, 3, Color.white),
            new GemConfig(9, 4, 4, new Color(0.5f, 0f, 0.5f)) // Purple
        };
        
        #if UNITY_EDITOR
        AssetDatabase.CreateAsset(gemData, "Assets/GemConfigData.asset");
        AssetDatabase.SaveAssets();
        Debug.Log("GemConfigData created!");
        #endif
    }
    
    private void CreateStageConfigData()
    {
        StageConfigData stageData = ScriptableObject.CreateInstance<StageConfigData>();
        
        // Stage 1: 4x4, 0 Dynamite, 4 Gems (1x2, 1x2, 1x3, 2x2)
        List<StageGemRequest> stage1Gems = new List<StageGemRequest>
        {
            new StageGemRequest(1, 2), // 2x 1x2
            new StageGemRequest(2, 1), // 1x 1x3
            new StageGemRequest(5, 1)  // 1x 2x2
        };
        
        // Stage 2: 5x5, 0 Dynamite, 5 Gems (1x2, 1x2, 1x3, 1x3, 2x3)
        List<StageGemRequest> stage2Gems = new List<StageGemRequest>
        {
            new StageGemRequest(1, 2), // 2x 1x2
            new StageGemRequest(2, 2), // 2x 1x3
            new StageGemRequest(6, 1)  // 1x 2x3
        };
        
        // Stage 3: 6x6, 1 Dynamite, 6 Gems (1x2, 1x3, 1x3, 2x2, 2x3, 2x3)
        List<StageGemRequest> stage3Gems = new List<StageGemRequest>
        {
            new StageGemRequest(1, 1), // 1x 1x2
            new StageGemRequest(2, 2), // 2x 1x3
            new StageGemRequest(5, 1), // 1x 2x2
            new StageGemRequest(6, 2)  // 2x 2x3
        };
        
        // Stage 4: 7x7, 2 Dynamite, 7 Gems (1x2, 1x4, 1x4, 2x2, 2x2, 2x3, 3x3)
        List<StageGemRequest> stage4Gems = new List<StageGemRequest>
        {
            new StageGemRequest(1, 1), // 1x 1x2
            new StageGemRequest(3, 2), // 2x 1x4
            new StageGemRequest(5, 2), // 2x 2x2
            new StageGemRequest(6, 1), // 1x 2x3
            new StageGemRequest(8, 1)  // 1x 3x3
        };
        
        // Stage 5: 8x8, 2 Dynamite, 8 Gems (1x2, 1x2, 1x3, 1x3, 1x5, 1x5, 2x4, 4x4)
        List<StageGemRequest> stage5Gems = new List<StageGemRequest>
        {
            new StageGemRequest(1, 2), // 2x 1x2
            new StageGemRequest(2, 2), // 2x 1x3
            new StageGemRequest(4, 2), // 2x 1x5
            new StageGemRequest(7, 1), // 1x 2x4
            new StageGemRequest(9, 1)  // 1x 4x4
        };
        
        stageData.stageConfigs = new StageConfig[]
        {
            new StageConfig(1, 4, 4, 0, stage1Gems),
            new StageConfig(2, 5, 5, 0, stage2Gems),
            new StageConfig(3, 6, 6, 1, stage3Gems),
            new StageConfig(4, 7, 7, 2, stage4Gems),
            new StageConfig(5, 8, 8, 2, stage5Gems)
        };
        
        stageData.rewardConfigs = new StageRewardConfig[]
        {
            new StageRewardConfig(1, RewardType.Pickaxe, 5),
            new StageRewardConfig(2, RewardType.Pickaxe, 10),
            new StageRewardConfig(3, RewardType.Pickaxe, 15),
            new StageRewardConfig(4, RewardType.Pickaxe, 20),
            new StageRewardConfig(5, RewardType.Pickaxe, 25)
        };
        
        #if UNITY_EDITOR
        AssetDatabase.CreateAsset(stageData, "Assets/StageConfigData.asset");
        AssetDatabase.SaveAssets();
        Debug.Log("StageConfigData created!");
        #endif
    }
}

