using UnityEngine;

/// <summary>
/// Manages the game cursor state globally.
/// Call CursorManager.SetPointer() / SetDefault() from anywhere.
///
/// SETUP:
///  1. Attach to any persistent GameObject (e.g. the GameManager object).
///  2. In the Inspector, assign a Texture2D for the pointing-finger cursor.
///     Import settings for the texture: TextureType = Cursor, Read/Write = ON.
///  3. Hotspot: the pixel in the texture that acts as the "tip" of the cursor.
///     For a finger pointer, (0, 0) is top-left. Typical hotspot is around (10, 2).
/// </summary>
public class CursorManager : MonoBehaviour
{
    public static CursorManager Instance { get; private set; }

    [Tooltip("Default arrow cursor. Leave null to use the OS default.")]
    public Texture2D defaultCursor;

    [Tooltip("Pointing-finger cursor shown when hovering over interactable objects.")]
    public Texture2D pointerCursor;

    [Tooltip("Pixel offset of the cursor's active point within the texture.")]
    public Vector2 pointerHotspot = new Vector2(10, 2);

    private bool isPointer = false;

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(this); return; }
        Instance = this;
        SetDefault();
    }

    // ── Static helpers ─────────────────────────────────────────────────────
    public static void SetPointer()  => Instance?.ApplyPointer(true);
    public static void SetDefault()  => Instance?.ApplyPointer(false);

    // ── Private ────────────────────────────────────────────────────────────
    private void ApplyPointer(bool pointer)
    {
        if (pointer == isPointer) return; // no change — avoid redundant SetCursor calls
        isPointer = pointer;

        if (pointer)
            Cursor.SetCursor(pointerCursor, pointerHotspot, CursorMode.Auto);
        else
            Cursor.SetCursor(defaultCursor, Vector2.zero, CursorMode.Auto);
    }
}
