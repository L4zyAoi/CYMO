using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// A high-performance localized sprite sequence player for UI Images.
/// Perfect for playing pre-rendered animations like the 120-frame book sequence.
/// </summary>
[RequireComponent(typeof(Image))]
public class UISequencePlayer : MonoBehaviour
{
    [Header("Sequence Settings")]
    [Tooltip("The array of sprites to play in sequence.")]
    public Sprite[] frames;

    [Tooltip("Frames per second for the animation.")]
    public float fps = 24f;

    [Tooltip("Whether the sequence should loop automatically.")]
    public bool loop = true;

    [Tooltip("Whether to play the sequence automatically when the object is enabled.")]
    public bool playOnEnable = true;

    private Image targetImage;
    private int currentFrame = 0;
    private float frameTimer = 0f;
    private bool isPlaying = false;

    private void Awake()
    {
        targetImage = GetComponent<Image>();
    }

    private void OnEnable()
    {
        if (playOnEnable)
            Play();
    }

    private void OnDisable()
    {
        Stop();
    }

    /// <summary>
    /// Starts playing the sprite sequence from the current frame.
    /// </summary>
    public void Play()
    {
        if (frames == null || frames.Length == 0) return;
        isPlaying = true;
        UpdateSprite();
    }

    /// <summary>
    /// Stops the animation and optionally resets to the first frame.
    /// </summary>
    public void Stop(bool reset = false)
    {
        isPlaying = false;
        if (reset)
        {
            currentFrame = 0;
            frameTimer = 0f;
            UpdateSprite();
        }
    }

    /// <summary>
    /// Resets and plays the animation from the first frame.
    /// </summary>
    public void Restart()
    {
        currentFrame = 0;
        frameTimer = 0f;
        Play();
    }

    private void Update()
    {
        if (!isPlaying || frames == null || frames.Length == 0) return;

        frameTimer += Time.unscaledDeltaTime; // Use unscaled time so it plays even if the game is paused!

        if (frameTimer >= (1f / fps))
        {
            frameTimer = 0f;
            currentFrame++;

            if (currentFrame >= frames.Length)
            {
                if (loop)
                {
                    currentFrame = 0;
                }
                else
                {
                    currentFrame = frames.Length - 1;
                    isPlaying = false;
                }
            }

            UpdateSprite();
        }
    }

    private void UpdateSprite()
    {
        if (targetImage != null && frames != null && currentFrame < frames.Length)
        {
            targetImage.sprite = frames[currentFrame];
        }
    }

    /// <summary>
    /// Manually set the sprite sequence.
    /// </summary>
    public void SetFrames(Sprite[] newFrames)
    {
        frames = newFrames;
        Restart();
    }
}
