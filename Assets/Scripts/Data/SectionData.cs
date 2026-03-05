using System;
using UnityEngine;

/// <summary>
/// A single section within a map — a camera zone the player can be in.
/// Sections are authored on the MapData ScriptableObject, not as separate assets.
/// </summary>
[Serializable]
public class SectionData
{
    [Tooltip("Display name for this section (e.g. 'Town Square', 'Market Alley').")]
    public string sectionName = "New Section";

    [Tooltip("World position where the player is placed when entering this section.")]
    public Vector2 spawnPoint;

    [Tooltip("The camera is confined to this rectangle while the player is in this section. " +
             "Use the Scene view to align it with your background art.")]
    public Rect cameraBounds = new Rect(-8f, -4.5f, 16f, 9f); // default: 16:9 at origin
}
