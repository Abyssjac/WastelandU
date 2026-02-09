using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "LoadoutMVP/AllModuleDatabase", fileName = "MD_Database_")]
public class AllModuleDatabase : ScriptableObject
{
    [Header("All ModuleData assets (Module SO)")]
    [SerializeField] private List<ModuleData> modules = new List<ModuleData>();

    // Runtime lookup cache (rebuilt on enable / validate)
    private Dictionary<string, ModuleData> _byId;

    /// <summary>
    /// Returns the ModuleData for the given id, or null if not found/invalid.
    /// </summary>
    public ModuleData GetById(string id)
    {
        if (string.IsNullOrEmpty(id)) return null;
        EnsureCache();
        _byId.TryGetValue(id, out var module);
        return module;
    }

    /// <summary>
    /// Try-get variant to avoid allocations / log spam.
    /// </summary>
    public bool TryGetById(string id, out ModuleData moduleData)
    {
        moduleData = null;
        if (string.IsNullOrEmpty(id)) return false;
        EnsureCache();
        return _byId.TryGetValue(id, out moduleData);
    }

    /// <summary>
    /// Exposes all modules (read-only).
    /// </summary>
    public IReadOnlyList<ModuleData> AllModules => modules;

    private void OnEnable()
    {
        RebuildCache();
    }

    private void OnValidate()
    {
        // Keep cache in sync when edited in Inspector
        RebuildCache();
    }

    private void EnsureCache()
    {
        if (_byId == null) RebuildCache();
    }

    private void RebuildCache()
    {
        if (_byId == null)
            _byId = new Dictionary<string, ModuleData>(StringComparer.Ordinal);
        else
            _byId.Clear();

        if (modules == null) return;

        for (int i = 0; i < modules.Count; i++)
        {
            var m = modules[i];
            if (m == null) continue;

            if (string.IsNullOrEmpty(m.id))
            {
                Debug.LogWarning($"[AllModuleDatabase] ModuleData '{m.name}' has empty id.", this);
                continue;
            }

            if (_byId.TryGetValue(m.id, out var existing) && existing != null && existing != m)
            {
                Debug.LogWarning(
                    $"[AllModuleDatabase] Duplicate ModuleData id '{m.id}'. Keeping '{existing.name}', ignoring '{m.name}'.",
                    this);
                continue;
            }

            _byId[m.id] = m;
        }
    }
}