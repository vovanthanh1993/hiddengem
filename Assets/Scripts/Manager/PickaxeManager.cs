using UnityEngine;
using System;

public class PickaxeManager : MonoBehaviour
{
    public static PickaxeManager Instance { get; private set; }
    
    [SerializeField] private int currentPickaxes = 100;
    
    public event Action<int> OnPickaxeChanged;
    
    public int CurrentPickaxes => currentPickaxes;
    
    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            
            // Load pickaxe count from PlayerPrefs on startup
            LoadPickaxeCount();
        }
        else
        {
            Destroy(gameObject);
        }
    }
    
    public bool UsePickaxe(int amount)
    {
        if (currentPickaxes >= amount)
        {
            currentPickaxes -= amount;
            SavePickaxeCount(); // Save after using
            OnPickaxeChanged?.Invoke(currentPickaxes);
            return true;
        }
        return false;
    }
    
    public void AddPickaxes(int amount)
    {
        currentPickaxes += amount;
        SavePickaxeCount(); // Save after adding
        OnPickaxeChanged?.Invoke(currentPickaxes);
    }
    
    public bool HasEnoughPickaxes(int amount)
    {
        return currentPickaxes >= amount;
    }
    
    public void ShowAddPickaxePopup()
    {
        // TODO: Show popup "Add 100 Pickaxe?" with Yes/No buttons
        UIManager.Instance?.ShowAddPickaxePopup();
    }
    
    #region Save/Load Pickaxe Count
    
    /// <summary>
    /// Save pickaxe count to PlayerPrefs
    /// </summary>
    private void SavePickaxeCount()
    {
        PlayerPrefs.SetInt("PickaxeCount", currentPickaxes);
        PlayerPrefs.Save();
        Debug.Log($"Saved pickaxe count: {currentPickaxes}");
    }
    
    /// <summary>
    /// Load pickaxe count from PlayerPrefs
    /// </summary>
    private void LoadPickaxeCount()
    {
        // Load from PlayerPrefs, default is current value in Inspector (100)
        int savedPickaxes = PlayerPrefs.GetInt("PickaxeCount", currentPickaxes);
        currentPickaxes = savedPickaxes;
        
        // Notify UI to update
        OnPickaxeChanged?.Invoke(currentPickaxes);
        
        Debug.Log($"Loaded pickaxe count from PlayerPrefs: {currentPickaxes}");
    }
    
    #endregion
}

