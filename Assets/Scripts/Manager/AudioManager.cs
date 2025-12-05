using UnityEngine;
using System.Collections.Generic;
using System.IO;

public enum AudioLoadMode
{
    Manual,      // Load from Inspector (sounds array)
    FromFolder   // Load automatically from Resources folder
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
    [Tooltip("Audio load mode: Manual (from Inspector) or FromFolder (from Resources folder)")]
    [SerializeField] private AudioLoadMode loadMode = AudioLoadMode.Manual;
    
    [Tooltip("List of audio clips (automatically loaded when loadMode = FromFolder)")]
    [SerializeField] private Sound[] sounds;
    
    [Tooltip("Folder path in Resources containing audio clips (e.g., 'Audio/SFX' or 'Sounds')")]
    [SerializeField] private string audioFolderPath = "Audio";
    
    [Tooltip("AudioSource for playing sound effects")]
    [SerializeField] private AudioSource sfxSource;
    
    [Tooltip("AudioSource for playing background music")]
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
        // Create AudioSource if not exists
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
        
        // Load audio based on selected mode
        if (loadMode == AudioLoadMode.FromFolder)
        {
            LoadSoundsFromFolder();
        }
        
        // Load from sounds array into dictionary (used for both Manual and FromFolder)
        LoadSoundsFromArray();
        
        // Load settings from PlayerPrefs and apply
        LoadAudioSettings();
        
        Debug.Log($"AudioManager: Loaded {soundDictionary.Count} audio clips.");
    }
    
    /// <summary>
    /// Load audio settings from PlayerPrefs and apply
    /// </summary>
    private void LoadAudioSettings()
    {
        // Load SFX setting (default is enabled = 1)
        bool sfxEnabled = PlayerPrefs.GetInt("SFXEnabled", 1) == 1;
        SetSFXVolume(sfxEnabled ? 1f : 0f);
        
        // Load Music setting (default is enabled = 1)
        bool musicEnabled = PlayerPrefs.GetInt("MusicEnabled", 1) == 1;
        SetMusicVolume(musicEnabled ? 1f : 0f);
    }
    
    /// <summary>
    /// Load all AudioClips from Resources folder and add to sounds array
    /// </summary>
    private void LoadSoundsFromFolder()
    {
        if (string.IsNullOrEmpty(audioFolderPath))
        {
            Debug.LogWarning("AudioManager: Audio folder path cannot be empty!");
            return;
        }
        
        // Load all AudioClips from Resources folder
        AudioClip[] clips = Resources.LoadAll<AudioClip>(audioFolderPath);
        
        if (clips == null || clips.Length == 0)
        {
            Debug.LogWarning($"AudioManager: No audio clips found in folder 'Resources/{audioFolderPath}'!");
            return;
        }
        
        // Create Sound[] array from loaded clips
        sounds = new Sound[clips.Length];
        for (int i = 0; i < clips.Length; i++)
        {
            if (clips[i] != null)
            {
                sounds[i] = new Sound
                {
                    name = clips[i].name, // File name without extension
                    clip = clips[i]
                };
            }
        }
        
        Debug.Log($"AudioManager: Loaded {clips.Length} audio clips from 'Resources/{audioFolderPath}' into sounds array");
    }
    
    /// <summary>
    /// Load from sounds array into dictionary
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
                        Debug.LogWarning($"AudioManager: Name '{sound.name}' already exists, skipping.");
                    }
                }
            }
        }
    }
    
    /// <summary>
    /// Reload audio from folder (can be called from code if needed)
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
    /// Play sound effect by name
    /// </summary>
    /// <param name="soundName">Name of the sound (must match name in sounds list)</param>
    /// <param name="volume">Volume (0-1), default is 1. Will be multiplied with volume from settings</param>
    public void PlaySound(string soundName, float volume = 1f)
    {
        if (string.IsNullOrEmpty(soundName))
        {
            Debug.LogWarning("AudioManager: Sound name cannot be empty!");
            return;
        }
        
        if (soundDictionary.ContainsKey(soundName))
        {
            if (sfxSource != null)
            {
                // Use volume from AudioSource (already set from settings) and multiply with passed volume
                float finalVolume = volume * sfxSource.volume;
                
                sfxSource.PlayOneShot(soundDictionary[soundName], finalVolume);
            }
        }
        else
        {
            Debug.LogWarning($"AudioManager: Sound with name '{soundName}' not found!");
        }
    }
    
    /// <summary>
    /// Play background music by name
    /// </summary>
    /// <param name="musicName">Name of the music</param>
    /// <param name="volume">Volume (0-1), default is 1. Will be multiplied with volume from settings</param>
    /// <param name="loop">Whether to loop, default is true</param>
    public void PlayMusic(string musicName, float volume = 1f, bool loop = true)
    {
        if (string.IsNullOrEmpty(musicName))
        {
            Debug.LogWarning("AudioManager: Music name cannot be empty!");
            return;
        }
        
        if (soundDictionary.ContainsKey(musicName))
        {
            if (musicSource != null)
            {
                // Use volume from AudioSource (already set from settings) and multiply with passed volume
                float finalVolume = volume * musicSource.volume;
                
                musicSource.clip = soundDictionary[musicName];
                musicSource.volume = finalVolume;
                musicSource.loop = loop;
                musicSource.Play();
            }
        }
        else
        {
            Debug.LogWarning($"AudioManager: Music with name '{musicName}' not found!");
        }
    }
    
    /// <summary>
    /// Stop background music
    /// </summary>
    public void StopMusic()
    {
        if (musicSource != null && musicSource.isPlaying)
        {
            musicSource.Stop();
        }
    }
    
    /// <summary>
    /// Set volume for sound effects
    /// </summary>
    public void SetSFXVolume(float volume)
    {
        if (sfxSource != null)
        {
            sfxSource.volume = Mathf.Clamp01(volume);
        }
    }
    
    /// <summary>
    /// Set volume for background music
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
