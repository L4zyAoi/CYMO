using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;

/// <summary>
/// Plays full-screen video cutscenes using a VideoPlayer + RenderTexture + RawImage.
/// Exposes a simple API: PlayVideoClip / PlayVideoClipOnce / SkipVideo.
/// </summary>
public class VideoCutsceneManager : MonoBehaviour
{
    public static VideoCutsceneManager Instance { get; private set; }

    [Header("References")]
    public VideoPlayer videoPlayer;
    public AudioSource audioSource; // target audio for the video
    public RawImage rawImage;       // optional UI element to display the RenderTexture
    public GameObject videoUIRoot;  // optional root to enable/disable whole UI
    [Header("Optional UI")]
    [Tooltip("Optional root GameObject for the Skip button or other UI that should be enabled while a video is playing.")]
    public GameObject skipUIRoot;

    private Action onComplete;
    private bool isPlaying = false;
    private bool hasPresentedFirstFrame = false;
    private static HashSet<string> playedVideos = new HashSet<string>();
    // Temporary RenderTexture created at runtime when VideoPlayer has no target texture.
    private RenderTexture runtimeRenderTexture = null;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        if (videoPlayer == null)
            videoPlayer = GetComponent<VideoPlayer>();

        if (audioSource == null)
            audioSource = GetComponent<AudioSource>();

        if (videoPlayer != null)
            videoPlayer.playOnAwake = false;

        if (audioSource != null)
            audioSource.playOnAwake = false;

        if (videoUIRoot != null)
            videoUIRoot.SetActive(false);
        if (skipUIRoot != null)
            skipUIRoot.SetActive(false);
    }

    /// <summary>
    /// Play the clip and call onComplete when finished. Returns immediately.
    /// </summary>
    public void PlayVideoClip(VideoClip clip, Action onComplete)
    {
        if (clip == null || videoPlayer == null)
        {
            Debug.LogWarning("[VideoCutsceneManager] PlayVideoClip called with null clip or missing VideoPlayer.");
            onComplete?.Invoke();
            return;
        }

        if (isPlaying)
        {
            Debug.LogWarning("[VideoCutsceneManager] Already playing a video. Ignoring new request.");
            return;
        }

        this.onComplete = onComplete;
        isPlaying = true;
        hasPresentedFirstFrame = false;

        if (videoUIRoot != null)
            videoUIRoot.SetActive(true);
        if (skipUIRoot != null)
            skipUIRoot.SetActive(true);

        videoPlayer.clip = clip;
        videoPlayer.isLooping = false;
        videoPlayer.audioOutputMode = VideoAudioOutputMode.AudioSource;
        if (audioSource != null)
        {
            audioSource.loop = false;
            videoPlayer.SetTargetAudioSource(0, audioSource);
        }

        if (rawImage != null)
        {
            if (videoPlayer.targetTexture == null)
            {
                // Ensure the VideoPlayer renders to a RenderTexture we own so the RawImage can display it.
                videoPlayer.renderMode = VideoRenderMode.RenderTexture;
                int w = Mathf.Max(2, Screen.width);
                int h = Mathf.Max(2, Screen.height);
                runtimeRenderTexture = new RenderTexture(w, h, 0, RenderTextureFormat.Default);
                runtimeRenderTexture.Create();
                videoPlayer.targetTexture = runtimeRenderTexture;
                rawImage.texture = runtimeRenderTexture;
            }
            else
            {
                rawImage.texture = videoPlayer.targetTexture;
            }
        }

        // Defensive unsubscribe in case of a previous interrupted playback.
        videoPlayer.loopPointReached -= OnVideoFinished;
        videoPlayer.loopPointReached += OnVideoFinished;
        videoPlayer.Play();

        Debug.Log($"[VideoCutsceneManager] renderMode={videoPlayer.renderMode}, targetTexture={(videoPlayer.targetTexture != null ? videoPlayer.targetTexture.name : "null")}");
        Debug.Log($"[VideoCutsceneManager] Playing video clip: {clip.name}");
    }

    private void Update()
    {
        if (!isPlaying || hasPresentedFirstFrame || videoPlayer == null)
            return;

        if (videoPlayer.isPlaying && videoPlayer.frame >= 0)
        {
            hasPresentedFirstFrame = true;
            Debug.Log("[VideoCutsceneManager] First video frame is ready.");
        }
    }

    /// <summary>
    /// Play the clip only if it hasn't been played before (by clip.name). Returns true when playback started.
    /// </summary>
    public bool PlayVideoClipOnce(VideoClip clip, Action onComplete)
    {
        if (clip == null) return false;
        if (playedVideos.Contains(clip.name))
        {
            Debug.Log($"[VideoCutsceneManager] Video '{clip.name}' already played. Skipping.");
            return false;
        }

        PlayVideoClip(clip, () => {
            playedVideos.Add(clip.name);
            onComplete?.Invoke();
        });

        return true;
    }

    private void OnVideoFinished(VideoPlayer vp)
    {
        CleanupAndComplete();
    }

    private void CleanupAndComplete()
    {
        if (!isPlaying) return;
        isPlaying = false;
        hasPresentedFirstFrame = false;

        if (videoPlayer != null)
        {
            videoPlayer.loopPointReached -= OnVideoFinished;

            if (videoPlayer.isPlaying)
                videoPlayer.Stop();

            // Detach clip/audio target so stale audio does not bleed into later transitions.
            videoPlayer.clip = null;
            if (audioSource != null)
                videoPlayer.SetTargetAudioSource(0, null);
        }

        if (audioSource != null && audioSource.isPlaying)
            audioSource.Stop();

        if (audioSource != null)
            audioSource.clip = null;

        var cb = onComplete;
        onComplete = null;
        Debug.Log($"[VideoCutsceneManager] Cleanup complete. Invoking onComplete={(cb != null)}");
        cb?.Invoke();

        // If callback started another playback, do not tear down shared UI/RT state.
        if (isPlaying)
        {
            Debug.Log("[VideoCutsceneManager] New playback started during onComplete. Skipping teardown for previous playback.");
            return;
        }

        if (videoUIRoot != null)
            videoUIRoot.SetActive(false);

        if (skipUIRoot != null)
            skipUIRoot.SetActive(false);

        // Clean up any runtime-created RenderTexture
        if (runtimeRenderTexture != null)
        {
            if (videoPlayer != null && videoPlayer.targetTexture == runtimeRenderTexture)
                videoPlayer.targetTexture = null;
            if (rawImage != null && rawImage.texture == runtimeRenderTexture)
                rawImage.texture = null;
            try
            {
                runtimeRenderTexture.Release();
            }
            catch { }
            Destroy(runtimeRenderTexture);
            runtimeRenderTexture = null;
        }
    }

    /// <summary>
    /// Skip the currently playing video and invoke completion callback.
    /// </summary>
    public void SkipVideo()
    {
        if (!isPlaying) return;
        Debug.Log("[VideoCutsceneManager] Video skipped.");
        CleanupAndComplete();
    }

    public bool IsPlaying => isPlaying;
    public bool HasPresentedFirstFrame => hasPresentedFirstFrame;
}
