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
            OnPickaxeChanged?.Invoke(currentPickaxes);
            return true;
        }
        return false;
    }
    
    public void AddPickaxes(int amount)
    {
        currentPickaxes += amount;
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
}

