using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class StageGemRequest
{
    public int gemId;
    public int count;
    
    public StageGemRequest(int id, int cnt)
    {
        gemId = id;
        count = cnt;
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

