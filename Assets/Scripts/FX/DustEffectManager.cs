using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Manages multiple dust effect instances in the scene.
/// Allows you to spawn and control multiple dust effects with different settings/prefabs.
///
/// SETUP:
///  1. Create dust effect prefabs (GameObjects with DustEffect script).
///  2. Assign the Main Camera to this manager.
///  3. Add dust effect prefabs to the dustEffectPrefabs array.
///  4. Call SpawnDustEffect() to instantiate a dust effect at runtime,
///     or check showOnStart to spawn them automatically.
/// </summary>
public class DustEffectManager : MonoBehaviour
{
    public static DustEffectManager Instance { get; private set; }

    [Header("References")]
    [SerializeField] private Camera mainCamera;

    [Header("Dust Effect Prefabs")]
    [SerializeField] private GameObject[] dustEffectPrefabs = new GameObject[0];

    [Header("Auto Spawn")]
    [SerializeField] private bool spawnAllOnStart = false;

    private List<DustEffect> activeDustEffects = new List<DustEffect>();

    private void Awake()
    {
        // Enforce singleton
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        // Auto-find Main Camera if not assigned
        if (mainCamera == null)
            mainCamera = Camera.main;

        if (mainCamera == null)
            Debug.LogWarning("[DustEffectManager] No Main Camera found!");
    }

    private void Start()
    {
        // Spawn all effects on start if enabled
        if (spawnAllOnStart)
        {
            for (int i = 0; i < dustEffectPrefabs.Length; i++)
            {
                SpawnDustEffect(i);
            }
        }
    }

    /// <summary>
    /// Spawn a dust effect from a prefab at the specified index.
    /// Returns the instantiated DustEffect component.
    /// </summary>
    public DustEffect SpawnDustEffect(int prefabIndex)
    {
        if (prefabIndex < 0 || prefabIndex >= dustEffectPrefabs.Length)
        {
            Debug.LogError($"[DustEffectManager] Prefab index {prefabIndex} is out of range!");
            return null;
        }

        GameObject prefab = dustEffectPrefabs[prefabIndex];
        if (prefab == null)
        {
            Debug.LogError($"[DustEffectManager] Prefab at index {prefabIndex} is null!");
            return null;
        }

        // Instantiate the prefab as a child of the camera
        GameObject instance = Instantiate(prefab, mainCamera.transform);
        instance.name = $"{prefab.name} (Instance)";

        // Get the DustEffect component
        DustEffect dustEffect = instance.GetComponent<DustEffect>();
        if (dustEffect == null)
        {
            Debug.LogWarning($"[DustEffectManager] Prefab '{prefab.name}' doesn't have a DustEffect component!");
            Destroy(instance);
            return null;
        }

        activeDustEffects.Add(dustEffect);
        Debug.Log($"[DustEffectManager] Spawned dust effect: {prefab.name}");

        return dustEffect;
    }

    /// <summary>
    /// Spawn a dust effect from a prefab by name.
    /// </summary>
    public DustEffect SpawnDustEffectByName(string prefabName)
    {
        for (int i = 0; i < dustEffectPrefabs.Length; i++)
        {
            if (dustEffectPrefabs[i] != null && dustEffectPrefabs[i].name == prefabName)
            {
                return SpawnDustEffect(i);
            }
        }

        Debug.LogError($"[DustEffectManager] No dust effect prefab found with name '{prefabName}'!");
        return null;
    }

    /// <summary>
    /// Remove a dust effect from tracking and optionally destroy it.
    /// </summary>
    public void RemoveDustEffect(DustEffect dustEffect, bool destroy = true)
    {
        if (dustEffect == null) return;

        activeDustEffects.Remove(dustEffect);

        if (destroy)
        {
            Destroy(dustEffect.gameObject);
            Debug.Log("[DustEffectManager] Dust effect removed and destroyed.");
        }
    }

    /// <summary>
    /// Get all currently active dust effects.
    /// </summary>
    public DustEffect[] GetActiveDustEffects()
    {
        return activeDustEffects.ToArray();
    }

    /// <summary>
    /// Apply settings to all active dust effects.
    /// </summary>
    public void ApplySettingsToAll()
    {
        foreach (var dustEffect in activeDustEffects)
        {
            if (dustEffect != null)
                dustEffect.ApplySettings();
        }
    }
}
