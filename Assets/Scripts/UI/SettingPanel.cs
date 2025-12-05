using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;

public class SettingPanel : MonoBehaviour
{
    public Button homeBtn;
    public Button closeBtn;
    
    [Header("SFX Settings")]
    [Tooltip("Button to toggle SFX on/off")]
    public Button sfxOnButton;
    [Tooltip("Button to toggle SFX on/off")]
    public Button sfxOffButton;
    
    [Header("Music Settings")]
    [Tooltip("Button to toggle Music on/off")]
    public Button musicOnButton;
    [Tooltip("Button to toggle Music on/off")]
    public Button musicOffButton;
    
    private bool sfxEnabled = true;
    private bool musicEnabled = true;

    private void OnEnable() {
        // Load settings from PlayerPrefs or use default values
        LoadSettings();
        UpdateUI();
    }

    void Start() {
        homeBtn.onClick.AddListener(OnHomeButtonClicked);   
        closeBtn.onClick.AddListener(OnCloseButtonClicked);
        
        // SFX buttons
        // Click On button → turn off sound (enabled = false)
        if (sfxOnButton != null)
            sfxOnButton.onClick.AddListener(() => OnSFXToggle(false));
        // Click Off button → turn on sound (enabled = true)
        if (sfxOffButton != null)
            sfxOffButton.onClick.AddListener(() => OnSFXToggle(true));
        
        // Music buttons
        // Click On button → turn off sound (enabled = false)
        if (musicOnButton != null)
            musicOnButton.onClick.AddListener(() => OnMusicToggle(false));
        // Click Off button → turn on sound (enabled = true)
        if (musicOffButton != null)
            musicOffButton.onClick.AddListener(() => OnMusicToggle(true));
    }

    public void OnHomeButtonClicked(){
        AudioManager.Instance.PlayClickSound();
        SceneManager.LoadScene("HomeScene");
        gameObject.SetActive(false);
    }

    public void OnCloseButtonClicked(){
        AudioManager.Instance.PlayCloseSound();
        gameObject.SetActive(false);
    }
    
    #region SFX Toggle Control
    
    private void OnSFXToggle(bool enabled)
    {
        sfxEnabled = enabled;
        ApplySFXSettings();
        UpdateUI();
        SaveSettings();
        
        // Play sound when adjusting
        if (AudioManager.Instance != null && sfxEnabled)
        {
            AudioManager.Instance.PlayChangeSound();
        }
    }
    
    private void ApplySFXSettings()
    {
        if (AudioManager.Instance != null)
        {
            AudioManager.Instance.SetSFXVolume(sfxEnabled ? 1f : 0f);
        }
    }
    
    #endregion
    
    #region Music Toggle Control
    
    private void OnMusicToggle(bool enabled)
    {
        musicEnabled = enabled;
        ApplyMusicSettings();
        UpdateUI();
        SaveSettings();
        
        // Play sound when adjusting
        if (AudioManager.Instance != null && sfxEnabled)
        {
            AudioManager.Instance.PlayChangeSound();
        }
    }
    
    private void ApplyMusicSettings()
    {
        if (AudioManager.Instance != null)
        {
            AudioManager.Instance.SetMusicVolume(musicEnabled ? 1f : 0f);
        }
    }
    
    #endregion
    
    #region UI Update
    
    private void UpdateUI()
    {
        // Update SFX buttons - show/hide based on state
        if (sfxOnButton != null)
            sfxOnButton.gameObject.SetActive(sfxEnabled); // Show when enabled
        if (sfxOffButton != null)
            sfxOffButton.gameObject.SetActive(!sfxEnabled); // Show when disabled
        
        // Update Music buttons - show/hide based on state
        if (musicOnButton != null)
            musicOnButton.gameObject.SetActive(musicEnabled); // Show when enabled
        if (musicOffButton != null)
            musicOffButton.gameObject.SetActive(!musicEnabled); // Show when disabled
    }
    
    #endregion
    
    #region Save/Load Settings
    
    private void SaveSettings()
    {
        PlayerPrefs.SetInt("SFXEnabled", sfxEnabled ? 1 : 0);
        PlayerPrefs.SetInt("MusicEnabled", musicEnabled ? 1 : 0);
        PlayerPrefs.Save();
    }
    
    private void LoadSettings()
    {
        sfxEnabled = PlayerPrefs.GetInt("SFXEnabled", 1) == 1;
        musicEnabled = PlayerPrefs.GetInt("MusicEnabled", 1) == 1;
        
        // Apply settings immediately when loading
        ApplySFXSettings();
        ApplyMusicSettings();
    }
    
    #endregion
}
