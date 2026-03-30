using System.Collections;
using UnityEngine;

/// <summary>
/// Confines and smoothly pans the camera between section bounds.
///
/// SETUP:
///  1. Attach this to the Main Camera GameObject.
///  2. Assign this component to the CameraController field on GameManager.
/// </summary>
public class CameraController : MonoBehaviour
{
	[Header("Pan Settings")]
	[Tooltip("Time in seconds to pan to a new section.")]
	public float panDuration = 0.6f;

	[Tooltip("Easing curve for the pan (leave as default for smooth ease-in-out).")]
	public AnimationCurve panCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

	// Optional fit fields (assign in Inspector or call FitToBackground from code)
	[Header("Fit Settings (optional)")]
	[Tooltip("Optional SpriteRenderer to fit the camera to on Start or via script.")]
	public SpriteRenderer backgroundToFit;
	[Tooltip("Extra padding as fraction of the background size (0 = exact fit, 0.1 = 10% padding).")]
	[Range(0f, 1f)]
	public float fitPadding = 0f;
	[Tooltip("If true, the camera will automatically fit the assigned background in Start().")]
	public bool fitOnStart = false;

	public enum ClampMode
	{
		SectionOnly,    // Use SectionData.cameraBounds only (legacy behavior)
		BackgroundOnly, // Use background bounds only
		Union           // Use the union of section and background bounds
	}

	[Header("Clamp Behavior")]
	[Tooltip("Which rectangle(s) should the camera clamp to when confining movement.")]
	public ClampMode clampMode = ClampMode.SectionOnly;

	// sectionBounds is set by MoveToSection (from SectionData.cameraBounds)
	private Rect sectionBounds;
	// backgroundBounds is set when FitToBackground is called
	private Rect backgroundBounds;
	private bool hasBackgroundBounds = false;
	private Rect cachedActiveBounds;
	private bool activeBoundsDirty = true; // Invalidate cache when bounds change

	private Camera cam;
	private Coroutine panCoroutine;

	void Awake()
	{
		cam = GetComponent<Camera>();
	}

	void Start()
	{
		if (fitOnStart && backgroundToFit != null && cam != null && cam.orthographic)
			FitToBackground(backgroundToFit, fitPadding, true, overrideBounds: false);
	}

	/// <summary>
	/// Pan to and confine the camera to the given section's bounds.
	/// This should be called by GameManager
	/// </summary>
	public void MoveToSection(SectionData section)
	{
		if (section == null)
		{
			Debug.LogError("[CameraController] MoveToSection called with null section!");
			return;
		}

		if (cam == null)
		{
			Debug.LogError("[CameraController] MoveToSection: cam is null. GetComponent<Camera> failed in Awake?");
			return;
		}

		// Set the section bounds used for clamping
		sectionBounds = section.cameraBounds;
		activeBoundsDirty = true; // Invalidate cache when bounds change

		Debug.Log($"[CameraController] MoveToSection -> received cameraBounds={sectionBounds}, cam.orthographicSize={cam.orthographicSize}");

		Vector3 targetPos = ClampedCameraPos(new Vector2(
			sectionBounds.center.x,
			sectionBounds.center.y));

		Debug.Log($"[CameraController] MoveToSection -> computed targetPos={targetPos} from sectionCenter={sectionBounds.center}");

		if (panCoroutine != null) StopCoroutine(panCoroutine);
		panCoroutine = StartCoroutine(PanTo(targetPos));
	}

	// Per-frame confinement
	void LateUpdate()
	{
		// Don't clamp while panning! The PanTo routine handles its own smooth movement.
		// If we clamp here during a pan, the camera fights the coroutine.
		if (panCoroutine != null) return;

		// Keep the camera clamped to the current active bounds every frame
		Vector3 clamped = ClampedCameraPos(transform.position);
		transform.position = clamped;
	}

	#region Fit helper
	/// <summary>
	/// Adjust the camera.orthographicSize so the camera fully contains the provided SpriteRenderer.
	/// Optionally center the camera on the sprite bounds.
	/// If overrideBounds is true, the background world rect replaces sectionBounds (legacy behavior).
	/// Default: do NOT overwrite sectionBounds (so SectionData.cameraBounds remain authoritative).
	/// </summary>
	public void FitToBackground(SpriteRenderer spriteRenderer, float padding = 0f, bool center = true, bool overrideBounds = false)
	{
		if (spriteRenderer == null || cam == null || !cam.orthographic)
		{
			Debug.LogWarning("[CameraController] Cannot FitToBackground: missing spriteRenderer or camera is not orthographic.");
			return;
		}

		// Use world bounds (accounts for sprite PPU and object scale)
		Vector2 bgWorldSize = spriteRenderer.bounds.size;
		bgWorldSize *= (1f + Mathf.Clamp01(padding));

		// orthographicSize required to fit background height
		float sizeForHeight = bgWorldSize.y / 2f;

		// orthographicSize required to fit background width (consider camera aspect)
		float sizeForWidth = (bgWorldSize.x / cam.aspect) / 2f;

		// pick the larger to ensure both dimensions are covered
		float requiredSize = Mathf.Max(sizeForHeight, sizeForWidth);

		cam.orthographicSize = requiredSize;

		Vector3 bgCenter = spriteRenderer.bounds.center;

		// Store background bounds separately
		backgroundBounds = new Rect(
			bgCenter.x - bgWorldSize.x * 0.5f,
			bgCenter.y - bgWorldSize.y * 0.5f,
			bgWorldSize.x,
			bgWorldSize.y
		);
		hasBackgroundBounds = true;
		activeBoundsDirty = true; // Invalidate cache when bounds change

		if (overrideBounds)
		{
			// legacy behavior: replace sectionBounds with background world rect
			sectionBounds = backgroundBounds;
		}

		// Only teleport to center if we aren't currently panning
		// (GameManager passes center=true for deferred updates, which would ruin the ease-in pan)
		if (center && panCoroutine == null)
		{
			// Center on background but clamp using active clamp behavior
			Vector3 clamped = ClampedCameraPos(new Vector2(bgCenter.x, bgCenter.y));
			transform.position = new Vector3(clamped.x, clamped.y, transform.position.z);
		}
	}
	#endregion

	#region Helpers
	private Rect GetActiveBounds()
	{
		// Return cached result if not dirty
		if (!activeBoundsDirty)
		{
			return cachedActiveBounds;
		}

		// Choose which rect to use according to clampMode and availability
		switch (clampMode)
		{
			case ClampMode.BackgroundOnly:
				if (hasBackgroundBounds)
					cachedActiveBounds = backgroundBounds;
				else
					cachedActiveBounds = sectionBounds;
				break;

			case ClampMode.Union:
				if (hasBackgroundBounds)
					cachedActiveBounds = UnionRect(sectionBounds, backgroundBounds);
				else
					cachedActiveBounds = sectionBounds;
				break;

			case ClampMode.SectionOnly:
			default:
				cachedActiveBounds = sectionBounds;
				break;
		}

		activeBoundsDirty = false;
		return cachedActiveBounds;
	}

	private Rect UnionRect(Rect a, Rect b)
	{
		// If one rect is empty, return the other
		if (a.width <= 0f || a.height <= 0f) return b;
		if (b.width <= 0f || b.height <= 0f) return a;

		float xMin = Mathf.Min(a.xMin, b.xMin);
		float yMin = Mathf.Min(a.yMin, b.yMin);
		float xMax = Mathf.Max(a.xMax, b.xMax);
		float yMax = Mathf.Max(a.yMax, b.yMax);

		return Rect.MinMaxRect(xMin, yMin, xMax, yMax);
	}

	private Vector3 ClampedCameraPos(Vector2 desired)
	{
		if (cam == null)
			return transform.position;

		Rect active = GetActiveBounds();

		// If active bounds are not set yet, just return current position
		if (active.width <= 0f || active.height <= 0f)
			return transform.position;

		float halfH = cam.orthographicSize;
		float halfW = halfH * cam.aspect;

		float x = Mathf.Clamp(desired.x,
			active.xMin + halfW,
			active.xMax - halfW);

		float y = Mathf.Clamp(desired.y,
			active.yMin + halfH,
			active.yMax - halfH);

		// If the active rect is smaller than the camera view, just center it
		if (active.width < halfW * 2f) x = active.center.x;
		if (active.height < halfH * 2f) y = active.center.y;

		return new Vector3(x, y, transform.position.z);
	}

	private IEnumerator PanTo(Vector3 baseTargetPos)
	{
		Vector3 start = transform.position;
		float elapsed = 0f;

		while (elapsed < panDuration)
		{
			elapsed += Time.deltaTime;
			float t = panCurve.Evaluate(Mathf.Clamp01(elapsed / panDuration));

			// Recalculate target every frame in case FitToBackground 
			// arrived late and changed the active bounds.
			Vector3 liveTarget = ClampedCameraPos(baseTargetPos);
			
			transform.position = Vector3.Lerp(start, liveTarget, t);
			yield return null;
		}

		transform.position = ClampedCameraPos(baseTargetPos);
		panCoroutine = null;
	}
	#endregion

	#region Gizmo
	void OnDrawGizmosSelected()
	{
		Rect active = GetActiveBounds();
		Gizmos.color = new Color(1f, 1f, 0f, 0.4f);
		if (active.width > 0f && active.height > 0f)
		{
			Gizmos.DrawWireCube(
				new Vector3(active.center.x, active.center.y, 0f),
				new Vector3(active.width, active.height, 0f));
		}
	}
	#endregion
}