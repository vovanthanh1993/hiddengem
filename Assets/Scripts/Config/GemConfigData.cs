using System;
using UnityEngine;

[System.Serializable]
public class GemConfig
{
    public int gemId;
    public int width;
    public int height;
    public Color gemColor;
    public Sprite gemSprite; // Sprite cho gem này
    
    // Constructor không tham số để Unity có thể serialize
    public GemConfig()
    {
        gemId = 0;
        width = 1;
        height = 1;
        gemColor = Color.white;
        gemSprite = null;
    }
    
    public GemConfig(int id, int w, int h, Color color)
    {
        gemId = id;
        width = w;
        height = h;
        gemColor = color;
        gemSprite = null;
    }
    
    public GemConfig(int id, int w, int h, Color color, Sprite sprite)
    {
        gemId = id;
        width = w;
        height = h;
        gemColor = color;
        gemSprite = sprite;
    }
}

[CreateAssetMenu(fileName = "GemConfigData", menuName = "HiddenGem/GemConfigData")]
public class GemConfigData : ScriptableObject
{
    public GemConfig[] gemConfigs;
    
    public GemConfig GetGemConfig(int gemId)
    {
        foreach (var config in gemConfigs)
        {
            if (config.gemId == gemId)
                return config;
        }
        return null;
    }
}

