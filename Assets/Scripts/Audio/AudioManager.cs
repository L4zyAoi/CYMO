using UnityEngine;
using UnityEngine.Audio;

/// <summary>
/// A persistent singleton that manages Background Music (BGM) and Sound Effects (SFX).
/// Handles play/stop calls, volume management, and simple BGM looping.
/// </summary>
public class AudioManager : MonoBehaviour
{
    public static AudioManager Instance { get; private set; }

    [Header("Audio Sources")]
    [SerializeField] private AudioSource musicSource;
    [SerializeField] private AudioSource sfxSource;
    
    private System.Collections.Generic.Dictionary<AudioClip, AudioSource> loopingSFX = new System.Collections.Generic.Dictionary<AudioClip, AudioSource>();

    [Header("Audio Mixer")]
    public AudioMixer mainMixer;
    public string musicParam = "MusicVol";
    public string sfxParam = "SFXVol";
    public string masterParam = "MasterVol";

    [Header("User Settings")]
    [Range(0f, 1f)] public float masterVolume = 0.8f;
    [Range(0f, 1f)] public float musicVolume = 0.5f;
    [Range(0f, 1f)] public float sfxVolume = 0.5f;

    void Awake()
    {
        // Persistence pattern
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        // READINESS CHECK: Ensure the user actually assigned everything!
        bool isReady = true;
        
        if (mainMixer == null) 
        { 
            Debug.LogError("[AudioManager] CRITICAL: Main Mixer is NOT assigned in the Inspector!"); 
            isReady = false; 
        }

        if (musicSource == null) 
        { 
            Debug.LogError("[AudioManager] CRITICAL: Music Source is NOT assigned in the Inspector!"); 
            isReady = false; 
        }

        if (sfxSource == null) 
        { 
            Debug.LogError("[AudioManager] CRITICAL: SFX Source is NOT assigned in the Inspector!"); 
            isReady = false; 
        }

        if (!isReady)
        {
            Debug.LogError("[AudioManager] Manager is NOT fully configured. Sound will NOT play until fixed!");
        }

        // Load saved volume settings if they exist
        masterVolume = PlayerPrefs.GetFloat("MasterVolume", 0.8f);
        musicVolume = PlayerPrefs.GetFloat("MusicVolume", 0.5f);
        sfxVolume = PlayerPrefs.GetFloat("SFXVolume", 0.5f);

        ApplyAllVolumes();
    }

    private void ApplyAllVolumes()
    {
        // For the Mixer setup, we want the AudioSources themselves to be at 1.0
        // because the Mixer Group handles the actual volume levels.
        if (musicSource != null) 
        {
            musicSource.volume = 1f;
            musicSource.spatialBlend = 0f; // Force to 2D (no distance-based muting)
        }
        
        if (sfxSource != null) 
        {
            sfxSource.volume = 1f;
            sfxSource.spatialBlend = 0f; // Force to 2D
        }

        // AUDIO LISTENER CHECK: Ensure your ears are "ON" in the scene!
        if (FindAnyObjectByType<AudioListener>() == null)
            Debug.LogWarning("[AudioManager] CRITICAL: No AudioListener found in the scene! You won't hear any sound. Ensure there is one attached to your Main Camera.");

        // ROUTING HEALTH CHECK: Ensure the sources are actually plugged into the Mixer groups.
        if (musicSource != null && musicSource.outputAudioMixerGroup == null)
            Debug.LogWarning("[AudioManager] WARNING: Music Source is NOT routed to a Mixer Group! Volume sliders will not work for music.");
        
        if (sfxSource != null && sfxSource.outputAudioMixerGroup == null)
            Debug.LogWarning("[AudioManager] WARNING: SFX Source is NOT routed to a Mixer Group! Volume sliders will not work for SFX.");

        SetMasterVolume(masterVolume);
        SetMusicVolume(musicVolume);
        SetSFXVolume(sfxVolume);
    }

    /// <summary>
    /// Play a sound effect one-shot.
    /// </summary>
    public void PlaySFX(AudioClip clip, float volumeScale = 1.0f)
    {
        if (clip == null)
        {
            Debug.LogError("[AudioManager] PlaySFX FAILED: clip is NULL!");
            return;
        }

        if (sfxSource == null)
        {
            Debug.LogError("[AudioManager] PlaySFX FAILED: sfxSource is NULL! Is AudioManager properly set up?");
            return;
        }

        float finalVol = volumeScale * sfxVolume;
        sfxSource.PlayOneShot(clip, finalVol);
    }

    /// <summary>
    /// Play background music. If the clip is the same as the current playing clip, 
    /// it won't restart.
    /// </summary>
    public void PlayMusic(AudioClip clip, bool loop = true)
    {
        if (clip == null || musicSource == null) return;
        
        // If it's already playing the right track, don't restart it!
        if (musicSource.clip == clip && musicSource.isPlaying) return;

        musicSource.clip = clip;
        musicSource.loop = loop;
        
        // Ensure volume is synchronized with settings
        musicSource.volume = 1.0f; // Let the Mixer Group handle the actual volume
        musicSource.Play();
        
        // Re-apply volumes to the mixer one more time in case it just woke up!
        ApplyAllVolumes();
    }

    /// <summary>
    /// Stop background music.
    /// </summary>
    public void StopMusic()
    {
        if (musicSource != null)
        {
            musicSource.Stop();
        }
    }

    /// <summary>
    /// Play a sound effect that loops until StopLoopingSFX is called.
    /// </summary>
    public void StartLoopingSFX(AudioClip clip, float volumeScale = 1.0f)
    {
        if (clip == null)
        {
            Debug.LogError("[AudioManager] StartLoopingSFX: clip is NULL!");
            return;
        }

        if (loopingSFX.ContainsKey(clip))
        {
            Debug.LogWarning($"[AudioManager] StartLoopingSFX: '{clip.name}' is already looping!");
            return;
        }

        GameObject loopGO = new GameObject("LoopingSFX_" + clip.name);
        loopGO.transform.SetParent(transform);
        AudioSource source = loopGO.AddComponent<AudioSource>();
        
        source.clip = clip;
        source.loop = true;
        source.volume = sfxVolume * volumeScale;
        
        // Route to the SFX mixer group, just like the main SFX source
        if (sfxSource != null && sfxSource.outputAudioMixerGroup != null)
        {
            source.outputAudioMixerGroup = sfxSource.outputAudioMixerGroup;
        }
        else
        {
            Debug.LogError($"[AudioManager] CRITICAL: Could not route looping SFX '{clip.name}' to mixer group!");
        }
        
        source.Play();
        loopingSFX.Add(clip, source);
    }

    /// <summary>
    /// Stop a specific looping sound effect.
    /// </summary>
    public void StopLoopingSFX(AudioClip clip)
    {
        if (clip == null) return;
        if (loopingSFX.TryGetValue(clip, out AudioSource source))
        {
            if (source != null)
            {
                source.Stop();
                Destroy(source.gameObject);
            }
            loopingSFX.Remove(clip);
        }
    }

    /// <summary>
    /// Update master volume.
    /// </summary>
    public void SetMasterVolume(float volume)
    {
        masterVolume = Mathf.Clamp01(volume);
        ApplyMixerVolume(masterParam, masterVolume);
        PlayerPrefs.SetFloat("MasterVolume", masterVolume);
    }

    /// <summary>
    /// Update music volume during play.
    /// </summary>
    public void SetMusicVolume(float volume)
    {
        musicVolume = Mathf.Clamp01(volume);
        ApplyMixerVolume(musicParam, musicVolume);
        PlayerPrefs.SetFloat("MusicVolume", musicVolume);
    }

    /// <summary>
    /// Update SFX volume.
    /// </summary>
    public void SetSFXVolume(float volume)
    {
        sfxVolume = Mathf.Clamp01(volume);
        ApplyMixerVolume(sfxParam, sfxVolume);
        PlayerPrefs.SetFloat("SFXVolume", sfxVolume);

        // Note: PlayOneShot doesn't need scaling because the SFX Group in the Mixer handles it!
    }

    /// <summary>
    /// Resets all audio settings to default.
    /// </summary>
    [ContextMenu("DEBUG: Reset Settings")]
    public void ResetAudioSettings()
    {
        PlayerPrefs.DeleteKey("MasterVolume");
        PlayerPrefs.DeleteKey("MusicVolume");
        PlayerPrefs.DeleteKey("SFXVolume");

        masterVolume = 0.8f;
        musicVolume = 0.5f;
        sfxVolume = 0.5f;

        ApplyAllVolumes();
        Debug.Log("[AudioManager] Audio settings reset to defaults.");
    }

    [ContextMenu("DEBUG: Test SFX Logic")]
    public void TestSFX()
    {
        if (sfxSource == null) { Debug.LogError("Test Failed: SFX Source is null."); return; }
        
        Debug.Log("[AudioManager] FORCE TESTING: Opening Master + SFX pipes to 100%...");
        SetMasterVolume(1.0f);
        SetSFXVolume(1.0f);
        
        if (sfxSource.clip != null)
        {
            Debug.Log($"[AudioManager] Playing assigned test clip: {sfxSource.clip.name}");
            sfxSource.Play();
        }
        else
        {
            Debug.LogWarning("[AudioManager] No clip assigned to SFX Source slot to test with!");
        }
    }

    [ContextMenu("DEBUG: Auto-Fix Everything")]
    public void AutoFixEverything()
    {
        if (mainMixer == null) { Debug.LogError("[AudioManager] FAILED: Main Mixer is null. Please assign it in the Inspector first."); return; }

        AudioSource[] sources = GetComponentsInChildren<AudioSource>();
        if (sources.Length < 1)
        {
            Debug.LogWarning("[AudioManager] No AudioSources found! Please create child GameObjects with AudioSource components first.");
            return;
        }

        // Heuristic: First source is Music, Second is SFX
        musicSource = (sources.Length > 0) ? sources[0] : musicSource;
        sfxSource = (sources.Length > 1) ? sources[1] : sfxSource;

        // Try to find the Mixer Groups and PLUG THEM IN
        AudioMixerGroup[] musicGroups = mainMixer.FindMatchingGroups("Music");
        AudioMixerGroup[] sfxGroups = mainMixer.FindMatchingGroups("SFX");

        if (musicSource != null && musicGroups.Length > 0)
        {
            musicSource.outputAudioMixerGroup = musicGroups[0];
            Debug.Log($"[AudioManager] Success: Plugged '{musicSource.name}' into Mixer Group 'Music'.");
        }

        if (sfxSource != null && sfxGroups.Length > 0)
        {
            sfxSource.outputAudioMixerGroup = sfxGroups[0];
            Debug.Log($"[AudioManager] Success: Plugged '{sfxSource.name}' into Mixer Group 'SFX'.");
        }

        Debug.Log($"[AudioManager] Auto-Fix Complete. Music: {(musicSource ? musicSource.name : "MISSING")}, SFX: {(sfxSource ? sfxSource.name : "MISSING")}");
    }

    /// <summary>
    /// Converts a 0-1 slider value to a logarithmic decibel value (-80 to 0).
    /// </summary>
    private void ApplyMixerVolume(string parameter, float sliderValue)
    {
        if (mainMixer == null)
        {
            Debug.LogError($"[AudioManager] ApplyMixerVolume FAILED: mainMixer is NULL!");
            return;
        }

        if (string.IsNullOrEmpty(parameter))
        {
            Debug.LogError($"[AudioManager] ApplyMixerVolume FAILED: parameter name is empty!");
            return;
        }

        // Logarithmic mapping: 0.0001 is -80 dB, 1.0 is 0 dB
        float dB = Mathf.Log10(Mathf.Max(0.0001f, sliderValue)) * 20f;
        
        Debug.Log($"[AudioManager] ApplyMixerVolume: Setting '{parameter}' to {dB:F2} dB (Slider: {sliderValue:P0})");
        
        bool success = mainMixer.SetFloat(parameter, dB);

        if (success)
            Debug.Log($"[AudioManager] ? Mixer volume set successfully for '{parameter}'");
        else
            Debug.LogError($"[AudioManager] ? FAILED to set '{parameter}' on mixer. CHECK:" +
                $"\n  1. Is '{parameter}' EXPOSED in the Audio Mixer?" +
                $"\n  2. Is the parameter name spelled EXACTLY the same (Case Sensitive)?" +
                $"\n  3. Is mainMixer assigned in the Inspector?" +
                $"\n  Attempted to set: '{parameter}' = {dB:F2}dB");
    }

    // The Mixer Group handles visual volumes now, so sources can stay at 1.0
}
