using UnityEngine;

/// <summary>
/// A global atmospheric effect that attaches to the Main Camera.
/// It creates/manages a Particle System that simulates drifting dust 
/// particles covering the screen with circular, blurred particles.
/// </summary>
[RequireComponent(typeof(Camera))]
public class DustEffect : MonoBehaviour
{
    [Header("Settings")]
    public int maxParticles = 100;
    public float emissionRate = 10f;
    public float minSize = 0.05f;
    public float maxSize = 0.2f;
    
    [Range(0f, 1f)]
    public float opacity = 0.3f;
    
    public float minSpeed = 0.1f;
    public float maxSpeed = 0.4f;

    [Header("Noise & Drift")]
    public float noiseStrength = 0.5f;
    public float noiseFrequency = 0.5f;

    [Header("Particle Appearance")]
    [Tooltip("Circle sprite for the dust particles. Leave empty to use default.")]
    public Sprite circleSprite;
    [Tooltip("Enable soft glow/blur effect on particles.")]
    public bool enableBlur = true;
    [Range(0f, 1f)]
    [Tooltip("Amount of blur/softness (0 = sharp, 1 = very soft).")]
    public float blurAmount = 0.5f;

    private ParticleSystem dustPS;
    private ParticleSystemRenderer psRenderer;

    void Awake()
    {
        SetupParticleSystem();
    }

    void SetupParticleSystem()
    {
        // Check if we already have a child system
        dustPS = GetComponentInChildren<ParticleSystem>();
        
        if (dustPS == null)
        {
            GameObject go = new GameObject("DustParticles");
            go.transform.SetParent(transform);
            go.transform.localPosition = new Vector3(0, 0, 10); // 10 units in front of camera
            dustPS = go.AddComponent<ParticleSystem>();
        }

        var main = dustPS.main;
        main.maxParticles = maxParticles;
        main.startSize = new ParticleSystem.MinMaxCurve(minSize, maxSize);
        main.startColor = new Color(1, 1, 1, opacity);
        main.startSpeed = new ParticleSystem.MinMaxCurve(minSpeed, maxSpeed);
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.prewarm = true;

        var emission = dustPS.emission;
        emission.rateOverTime = emissionRate;

        var shape = dustPS.shape;
        shape.shapeType = ParticleSystemShapeType.Box;
        // Size it based on typical orthographic camera view
        Camera cam = GetComponent<Camera>();
        float height = cam.orthographic ? cam.orthographicSize * 2 : 10f;
        float width = height * cam.aspect;
        shape.scale = new Vector3(width * 2, height * 2, 5); // Wide enough to cover panning

        var velocity = dustPS.velocityOverLifetime;
        velocity.enabled = true;
        velocity.x = new ParticleSystem.MinMaxCurve(-0.2f, 0.2f);
        velocity.y = new ParticleSystem.MinMaxCurve(-0.1f, 0.1f);
        velocity.z = new ParticleSystem.MinMaxCurve(0f, 0f); // Must match X/Y mode

        var noise = dustPS.noise;
        noise.enabled = true;
        noise.strength = noiseStrength;
        noise.frequency = noiseFrequency;
        noise.scrollSpeed = 0.2f;

        var colorOverLifetime = dustPS.colorOverLifetime;
        colorOverLifetime.enabled = true;
        Gradient gradient = new Gradient();
        gradient.SetKeys(
            new GradientColorKey[] { new GradientColorKey(Color.white, 0.0f), new GradientColorKey(Color.white, 1.0f) },
            new GradientAlphaKey[] { new GradientAlphaKey(0, 0.0f), new GradientAlphaKey(opacity, 0.2f), new GradientAlphaKey(opacity, 0.8f), new GradientAlphaKey(0, 1.0f) }
        );
        colorOverLifetime.color = gradient;

        psRenderer = dustPS.GetComponent<ParticleSystemRenderer>();
        psRenderer.renderMode = ParticleSystemRenderMode.Billboard;

        // Apply circle sprite if available
        if (circleSprite != null)
        {
            psRenderer.material = CreateBlurredCircleMaterial();
        }
        else
        {
            // Fallback to default particle
            if (psRenderer.sharedMaterial == null)
            {
                psRenderer.material = new Material(Shader.Find("Particles/Standard Unlit"));
            }
        }
    }

    /// <summary>
    /// Creates a material with soft edges for a blurred circle effect.
    /// </summary>
    private Material CreateBlurredCircleMaterial()
    {
        Material mat = new Material(Shader.Find("Sprites/Default"));
        
        if (circleSprite != null)
        {
            mat.mainTexture = circleSprite.texture;
        }

        // Adjust material properties for soft/blurred appearance
        if (enableBlur)
        {
            // Reduce alpha to create soft glow effect
            mat.color = new Color(1f, 1f, 1f, 1f - blurAmount * 0.5f);
            
            // Enable additive blending for softer look
            mat.SetInt("_BlendSrc", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            mat.SetInt("_BlendDst", (int)UnityEngine.Rendering.BlendMode.One);
        }

        return mat;
    }

    // Call this if you change settings at runtime/inspector
    [ContextMenu("Apply Settings")]
    public void ApplySettings()
    {
        if (dustPS == null) SetupParticleSystem();
        
        var main = dustPS.main;
        main.maxParticles = maxParticles;
        main.startSize = new ParticleSystem.MinMaxCurve(minSize, maxSize);
        main.startColor = new Color(1, 1, 1, opacity);
        main.startSpeed = new ParticleSystem.MinMaxCurve(minSpeed, maxSpeed);
        
        var emission = dustPS.emission;
        emission.rateOverTime = emissionRate;
        
        var noise = dustPS.noise;
        noise.strength = noiseStrength;
        noise.frequency = noiseFrequency;

        // Reapply blur material if needed
        if (psRenderer != null && enableBlur)
        {
            psRenderer.material = CreateBlurredCircleMaterial();
        }
    }
}
