using UnityEngine;
using TMPro;
using UnityEngine.UI;

public class RewardChestPopup : MonoBehaviour
{
    [Header("Reward Chest Popup")]
    [SerializeField] private TextMeshProUGUI rewardText;
    [SerializeField] private Button okButton;

    private void Awake()
    {
        InitializePopup();
    }
    
    private void InitializePopup()
    {
        // Setup ok button
        if (okButton != null)
        {
            okButton.onClick.RemoveAllListeners();
            okButton.onClick.AddListener(OnOkButtonClicked);
        }
    }
    
    public void Show(StageRewardConfig reward)
    {
        // Play popup sound when displaying popup
        if (AudioManager.Instance != null)
        {
            AudioManager.Instance.PlayPopupSound();
        }
        
        gameObject.SetActive(true);
        
        // Update reward text
        if (rewardText != null && reward != null)
        {
            rewardText.text = $"{reward.amount}";
        }
    }
    
    public void Hide()
    {
        gameObject.SetActive(false);
    }

    private void OnOkButtonClicked()
    {
        // Play click sound when hiding popup
        if (AudioManager.Instance != null)
        {
            AudioManager.Instance.PlayClickSound();
        }
        Hide();
    }

    private void OnDestroy()
    {
        okButton.onClick.RemoveListener(OnOkButtonClicked);
    }
}
