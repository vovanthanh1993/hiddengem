using UnityEngine;
using UnityEngine.UI;

public class AddPickaxePopup : MonoBehaviour
{
    [Header("Add Pickaxe Popup")]
    [SerializeField] private Button yesButton;
    [SerializeField] private Button noButton;
    
    private void Awake()
    {
        InitializePopup();
    }
    
    private void InitializePopup()
    {
        // Setup buttons
        if (yesButton != null)
        {
            yesButton.onClick.RemoveAllListeners();
            yesButton.onClick.AddListener(() => OnConfirmed(true));
        }
        
        if (noButton != null)
        {
            noButton.onClick.RemoveAllListeners();
            noButton.onClick.AddListener(() => OnConfirmed(false));
        }
    }
    
    public void Show()
    {
        // Play popup sound when displaying popup
        if (AudioManager.Instance != null)
        {
            AudioManager.Instance.PlayPopupSound();
        }
        
        gameObject.SetActive(true);
    }
    
    public void Hide()
    {
        gameObject.SetActive(false);
    }
    
    private void OnConfirmed(bool confirmed)
    {
        Hide();
        
        if (GameManager.Instance != null)
        {
            GameManager.Instance.OnAddPickaxeConfirmed(confirmed);
            AudioManager.Instance.PlayClickSound();
        }
    }
}
