using UnityEngine;
using System.Collections.Generic;
using System.IO;

public enum AudioLoadMode
{
    Manual,      // Load từ Inspector (sounds array)
    FromFolder   // Load tự động từ thư mục Resources
}

public class AudioManager : MonoBehaviour
{
    public static AudioManager Instance { get; private set; }
    
    [System.Serializable]
    public class Sound
    {
        public string name;
        public AudioClip clip;
    }
    
    [Header("Audio Settings")]
    [Tooltip("Chế độ load audio: Manual (từ Inspector) hoặc FromFolder (từ thư mục Resources)")]
    [SerializeField] private AudioLoadMode loadMode = AudioLoadMode.Manual;
    
    [Tooltip("Danh sách các audio clip (tự động load khi loadMode = FromFolder)")]
    [SerializeField] private Sound[] sounds;
    
    [Tooltip("Đường dẫn thư mục trong Resources chứa audio clips (ví dụ: 'Audio/SFX' hoặc 'Sounds')")]
    [SerializeField] private string audioFolderPath = "Audio";
    
    [Tooltip("AudioSource để phát sound effects")]
    [SerializeField] private AudioSource sfxSource;
    
    [Tooltip("AudioSource để phát background music")]
    [SerializeField] private AudioSource musicSource;
    
    private Dictionary<string, AudioClip> soundDictionary = new Dictionary<string, AudioClip>();
    
    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            InitializeAudio();
        }
        else
        {
            Destroy(gameObject);
        }
    }
    
    private void InitializeAudio()
    {
        // Tạo AudioSource nếu chưa có
        if (sfxSource == null)
        {
            sfxSource = gameObject.AddComponent<AudioSource>();
            sfxSource.playOnAwake = false;
        }
        
        if (musicSource == null)
        {
            musicSource = gameObject.AddComponent<AudioSource>();
            musicSource.playOnAwake = false;
            musicSource.loop = true;
        }
        
        // Load audio theo chế độ đã chọn
        if (loadMode == AudioLoadMode.FromFolder)
        {
            LoadSoundsFromFolder();
        }
        
        // Load từ mảng sounds vào dictionary (dùng cho cả Manual và FromFolder)
        LoadSoundsFromArray();
        
        // Load settings từ PlayerPrefs và áp dụng
        LoadAudioSettings();
        
        Debug.Log($"AudioManager: Đã load {soundDictionary.Count} audio clips.");
    }
    
    /// <summary>
    /// Load audio settings từ PlayerPrefs và áp dụng
    /// </summary>
    private void LoadAudioSettings()
    {
        // Load SFX setting (mặc định là bật = 1)
        bool sfxEnabled = PlayerPrefs.GetInt("SFXEnabled", 1) == 1;
        SetSFXVolume(sfxEnabled ? 1f : 0f);
        
        // Load Music setting (mặc định là bật = 1)
        bool musicEnabled = PlayerPrefs.GetInt("MusicEnabled", 1) == 1;
        SetMusicVolume(musicEnabled ? 1f : 0f);
    }
    
    /// <summary>
    /// Load tất cả AudioClip từ thư mục Resources và đưa vào mảng sounds
    /// </summary>
    private void LoadSoundsFromFolder()
    {
        if (string.IsNullOrEmpty(audioFolderPath))
        {
            Debug.LogWarning("AudioManager: Đường dẫn thư mục audio không được để trống!");
            return;
        }
        
        // Load tất cả AudioClip từ thư mục Resources
        AudioClip[] clips = Resources.LoadAll<AudioClip>(audioFolderPath);
        
        if (clips == null || clips.Length == 0)
        {
            Debug.LogWarning($"AudioManager: Không tìm thấy audio clip nào trong thư mục 'Resources/{audioFolderPath}'!");
            return;
        }
        
        // Tạo mảng Sound[] từ các clip đã load
        sounds = new Sound[clips.Length];
        for (int i = 0; i < clips.Length; i++)
        {
            if (clips[i] != null)
            {
                sounds[i] = new Sound
                {
                    name = clips[i].name, // Tên file không có extension
                    clip = clips[i]
                };
            }
        }
        
        Debug.Log($"AudioManager: Đã load {clips.Length} audio clips từ 'Resources/{audioFolderPath}' vào mảng sounds");
    }
    
    /// <summary>
    /// Load từ mảng sounds vào dictionary
    /// </summary>
    private void LoadSoundsFromArray()
    {
        if (sounds != null)
        {
            foreach (Sound sound in sounds)
            {
                if (sound != null && !string.IsNullOrEmpty(sound.name) && sound.clip != null)
                {
                    if (!soundDictionary.ContainsKey(sound.name))
                    {
                        soundDictionary.Add(sound.name, sound.clip);
                    }
                    else
                    {
                        Debug.LogWarning($"AudioManager: Tên '{sound.name}' đã tồn tại, bỏ qua.");
                    }
                }
            }
        }
    }
    
    /// <summary>
    /// Load lại audio từ thư mục (có thể gọi từ code nếu cần)
    /// </summary>
    public void ReloadSounds()
    {
        soundDictionary.Clear();
        if (loadMode == AudioLoadMode.FromFolder)
        {
            LoadSoundsFromFolder();
        }
        LoadSoundsFromArray();
    }
    
    /// <summary>
    /// Phát sound effect theo tên
    /// </summary>
    /// <param name="soundName">Tên của sound (phải khớp với tên trong danh sách sounds)</param>
    /// <param name="volume">Volume (0-1), mặc định là 1. Sẽ được nhân với volume từ settings</param>
    public void PlaySound(string soundName, float volume = 1f)
    {
        if (string.IsNullOrEmpty(soundName))
        {
            Debug.LogWarning("AudioManager: Tên sound không được để trống!");
            return;
        }
        
        if (soundDictionary.ContainsKey(soundName))
        {
            if (sfxSource != null)
            {
                // Sử dụng volume từ AudioSource (đã được set từ settings) và nhân với volume được truyền vào
                float finalVolume = volume * sfxSource.volume;
                
                sfxSource.PlayOneShot(soundDictionary[soundName], finalVolume);
            }
        }
        else
        {
            Debug.LogWarning($"AudioManager: Không tìm thấy sound với tên '{soundName}'!");
        }
    }
    
    /// <summary>
    /// Phát background music theo tên
    /// </summary>
    /// <param name="musicName">Tên của music</param>
    /// <param name="volume">Volume (0-1), mặc định là 1. Sẽ được nhân với volume từ settings</param>
    /// <param name="loop">Có lặp lại không, mặc định là true</param>
    public void PlayMusic(string musicName, float volume = 1f, bool loop = true)
    {
        if (string.IsNullOrEmpty(musicName))
        {
            Debug.LogWarning("AudioManager: Tên music không được để trống!");
            return;
        }
        
        if (soundDictionary.ContainsKey(musicName))
        {
            if (musicSource != null)
            {
                // Sử dụng volume từ AudioSource (đã được set từ settings) và nhân với volume được truyền vào
                float finalVolume = volume * musicSource.volume;
                
                musicSource.clip = soundDictionary[musicName];
                musicSource.volume = finalVolume;
                musicSource.loop = loop;
                musicSource.Play();
            }
        }
        else
        {
            Debug.LogWarning($"AudioManager: Không tìm thấy music với tên '{musicName}'!");
        }
    }
    
    /// <summary>
    /// Dừng background music
    /// </summary>
    public void StopMusic()
    {
        if (musicSource != null && musicSource.isPlaying)
        {
            musicSource.Stop();
        }
    }
    
    /// <summary>
    /// Đặt volume cho sound effects
    /// </summary>
    public void SetSFXVolume(float volume)
    {
        if (sfxSource != null)
        {
            sfxSource.volume = Mathf.Clamp01(volume);
        }
    }
    
    /// <summary>
    /// Đặt volume cho background music
    /// </summary>
    public void SetMusicVolume(float volume)
    {
        if (musicSource != null)
        {
            musicSource.volume = Mathf.Clamp01(volume);
        }
    }

    public void PlayBackSound() {
        PlaySound("se_back");
    }

    public void PlayChangeSound() {
        PlaySound("se_button_change");
    }

    public void PlayCloseSound() {
        PlaySound("se_button_close");
    }

    public void PlayPopupSound() {
        PlaySound("se_button_popup");
    }

    public void PlayClickSound() {
        PlaySound("se_button_click");
    }

    public void PlaySuccessSound() {
        PlaySound("se_buy_success");
    }

    public void PlayFailSound() {
        PlaySound("se_not_enough");
    }

    public void PlaySelectSound() {
        PlaySound("se_button_select");
    }

    public void PlayHurtSound() {
        PlaySound("se_player_hurt");
    }

    public void PlayHomeMusic() {
        PlayMusic("bgm_home");
    }

    public void PlayGameplayMusic() {
        PlayMusic("bgm_gameplay");
    }

    public void PlayWinSound() {
        PlaySound("se_pve_win");
    }

    public void PlayLoseSound() {
        PlaySound("se_pve_lose");
    }

    public void PlayRewardSound() {
        PlaySound("se_open_reward");
    }

    public void PlayExplosionSound() {
        PlaySound("se_explosion");
    }
    public void PlayDiggingSound() {
        PlaySound("se_dig_cell");
    }

    public void PlayGemFoundSound() {
        PlaySound("se_gem_found");
    }

    public void PlayGemCollectSound() {
        PlaySound("se_gem_collect");
    }
}
