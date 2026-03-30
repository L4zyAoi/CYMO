using System;
using UnityEngine;

/// <summary>
/// A single section within a map — a camera zone the player can be in.
/// Sections are authored on the MapData ScriptableObject, not as separate assets.
/// </summary>
[Serializable]
public class SectionData
{
    [Serializable]
    public struct ExitLink
    {
        [Tooltip("Optional readable name for this exit mapping (e.g. 'Door A', 'Stairs Down').")]
        public string exitName;

        [Tooltip("Logical exit id used by SectionExit (not section index).")]
        public int exitId;

        [UnityEngine.Serialization.FormerlySerializedAs("targetExitId")]
        [Tooltip("Destination section index in this map.")]
        public int targetSectionIndex;

        [Tooltip("Optional: exit id in the destination section to walk from. If set, character walks from that exit's position to spawn point.")]
        public int destinationExitId;

        [Tooltip("If enabled, this exit uses a custom spawn point instead of the destination section's default spawnPoint.")]
        public bool useOverrideSpawnPoint;

        [Tooltip("Custom spawn point used when Use Override Spawn Point is enabled.")]
        public Vector2 overrideSpawnPoint;
    }

    [Tooltip("Display name for this section (e.g. 'Town Square', 'Market Alley').")]
    public string sectionName = "New Section";

    [Tooltip("World position where the player is placed when entering this section.")]
    public Vector2 spawnPoint;

    [Tooltip("The camera is confined to this rectangle while the player is in this section. " +
             "Use the Scene view to align it with your background art.")]
    public Rect cameraBounds = new Rect(-8f, -4.5f, 16f, 9f); // default: 16:9 at origin

    [Header("BGM")]
    [Tooltip("If true, the section's background music starts automatically on entry. If false, it must be triggered manually.")]
    public bool autoPlayMusic = true;
    [Tooltip("Background music for this section. Leave empty to keep current music.")]
    public AudioClip backgroundMusic;

    [Header("Perspective Scaling")]
    [Tooltip("If true, the character scales based on proximity to perspective anchors in this section. " +
             "Anchors are auto-discovered from all PerspectiveAnchor components in the scene.")]
    public bool usePerspectiveScaling = false;
    [Tooltip("Fallback top Y for linear scaling if no anchors are found.")]
    public float topY = 1.0f;
    [Tooltip("Fallback bottom Y for linear scaling if no anchors are found.")]
    public float bottomY = -1.0f;
    [Range(0.1f, 2.0f)] public float minScale = 0.6f;
    [Range(0.1f, 2.0f)] public float maxScale = 1.0f;

    [Header("Cutscenes")]
    [Tooltip("Optional: names of cutscenes to play (in order) when entering this section. Leave empty for no cutscene.")]
    public string[] onEnterCutsceneNames = new string[0];
    [Tooltip("If true, each cutscene will only play once. Subsequent section entries won't trigger it.")]
    public bool cutscenePlayOnce = true;

    [Tooltip("Optional explicit exit mappings for this section. " +
             "Use this to map an exit id (from SectionExit) to a destination section index.")]
    public ExitLink[] exits = new ExitLink[0];

    public bool TryGetTargetSectionIndex(int exitId, out int targetSectionIndex)
    {
        targetSectionIndex = -1;
        if (exits == null || exits.Length == 0) return false;

        for (int i = 0; i < exits.Length; i++)
        {
            if (exits[i].exitId != exitId) continue;
            targetSectionIndex = exits[i].targetSectionIndex;
            return true;
        }

        return false;
    }

    public bool TryGetExitLink(int exitId, out ExitLink exitLink)
    {
        exitLink = default;
        if (exits == null || exits.Length == 0) return false;

        for (int i = 0; i < exits.Length; i++)
        {
            if (exits[i].exitId != exitId) continue;
            exitLink = exits[i];
            return true;
        }

        return false;
    }
}
