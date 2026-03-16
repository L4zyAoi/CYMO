using System.Collections;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// A puzzle object that crossfades a dark background into a light background when clicked.
/// Perfect for pre-rendered darkness in a specific section.
///
/// SETUP:
///  1. In your section, place the normal (Bright) background SpriteRenderer.
///  2. Place the Dark background SpriteRenderer exactly on top of it.
///     Make sure its Order In Layer is HIGHER than the Bright one.
///  3. Give the Clickable Lamp an "Interactable" Layer + Collider2D (Is Trigger: OFF).
///  4. Attach this script to the Lamp.
///  5. Assign the DARK background SpriteRenderer to 'darkBackground'.
/// </summary>
public class LampPuzzle : MonoBehaviour
{
    [Header("Visuals")]
    [Tooltip("The dark version of the background. It will fade out to reveal the bright version underneath.")]
    public SpriteRenderer darkBackground;

    [Tooltip("How long the crossfade takes in seconds.")]
    public float fadeDuration = 1.5f;

    [Header("Events")]
    [Tooltip("Fired immediately when the lamp is clicked.")]
    public UnityEvent OnLampTurnedOn;

    private bool isLit = false;

    // Called by PointAndClickController
    public void ActivateLamp()
    {
        if (isLit) return;
        isLit = true;

        OnLampTurnedOn?.Invoke();

        if (darkBackground != null)
            StartCoroutine(FadeDarknessRoutine());
        else
            Debug.LogWarning("[LampPuzzle] No dark background assigned! Lamp activated, but no visual fade will happen.");
    }

    private IEnumerator FadeDarknessRoutine()
    {
        // We fade the dark background's alpha from 1 to 0.
        // The bright background sits underneath and is smoothly revealed.
        Color color = darkBackground.color;
        float elapsed = 0f;

        while (elapsed < fadeDuration)
        {
            elapsed += Time.deltaTime;
            color.a = Mathf.Lerp(1f, 0f, elapsed / fadeDuration);
            darkBackground.color = color;
            yield return null;
        }

        color.a = 0f;
        darkBackground.color = color;
        
        // Disable the dark sprite completely for performance once invisible
        darkBackground.enabled = false;
    }
}
