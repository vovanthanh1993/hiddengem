using System;
using System.Collections.Generic;
using UnityEngine;

public enum GemOrientation
{
    Horizontal,  // Nằm ngang (width > height sau khi xoay)
    Vertical      // Nằm dọc (height > width sau khi xoay)
}

[Serializable]
public class StageGemRequest
{
    public int gemId;
    public int count;
    public GemOrientation orientation = GemOrientation.Vertical; // Hướng của gem (mặc định Vertical)
    
    // Constructor không tham số để Unity có thể serialize
    public StageGemRequest()
    {
        gemId = 0;
        count = 0;
        orientation = GemOrientation.Vertical;
    }
    
    public StageGemRequest(int id, int cnt)
    {
        gemId = id;
        count = cnt;
        orientation = GemOrientation.Vertical;
    }
    
    public StageGemRequest(int id, int cnt, GemOrientation orient)
    {
        gemId = id;
        count = cnt;
        orientation = orient;
    }
}

[Serializable]
public class StageConfig
{
    public int stageId;
    public int boardWidth;
    public int boardHeight;
    public int dynamiteCount;
    public List<StageGemRequest> gemRequests;
    
    // Constructor không tham số để Unity có thể serialize
    public StageConfig()
    {
        stageId = 0;
        boardWidth = 0;
        boardHeight = 0;
        dynamiteCount = 0;
        gemRequests = new List<StageGemRequest>();
    }
    
    public StageConfig(int id, int width, int height, int dynamite, List<StageGemRequest> gems)
    {
        stageId = id;
        boardWidth = width;
        boardHeight = height;
        dynamiteCount = dynamite;
        gemRequests = gems;
    }
    
    public int GetTotalGemCount()
    {
        int total = 0;
        foreach (var request in gemRequests)
        {
            total += request.count;
        }
        return total;
    }
}

[CreateAssetMenu(fileName = "StageConfigData", menuName = "HiddenGem/StageConfigData")]
public class StageConfigData : ScriptableObject
{
    public StageConfig[] stageConfigs;
    public StageRewardConfig[] rewardConfigs;
    
    public StageConfig GetStageConfig(int stageId)
    {
        foreach (var config in stageConfigs)
        {
            if (config.stageId == stageId)
                return config;
        }
        return null;
    }
    
    public StageRewardConfig GetRewardConfig(int stageId)
    {
        foreach (var reward in rewardConfigs)
        {
            if (reward.stageId == stageId)
                return reward;
        }
        return null;
    }
}

[Serializable]
public class StageRewardConfig
{
    public int stageId;
    public RewardType rewardType;
    public int amount;
    
    // Constructor không tham số để Unity có thể serialize
    public StageRewardConfig()
    {
        stageId = 0;
        rewardType = RewardType.Pickaxe;
        amount = 0;
    }
    
    public StageRewardConfig(int id, RewardType type, int amt)
    {
        stageId = id;
        rewardType = type;
        amount = amt;
    }
}

public enum RewardType
{
    Pickaxe
}

