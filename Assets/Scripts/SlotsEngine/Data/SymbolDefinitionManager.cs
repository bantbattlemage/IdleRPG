using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// Central authority for SymbolDefinition assets. Scans loaded assets at Awake and provides
/// lookup helpers by asset name, authoring SymbolName, or associated Sprite name.
/// ReelStripData and other systems should query this manager instead of resolving definitions themselves.
/// </summary>
public class SymbolDefinitionManager : Singleton<SymbolDefinitionManager>
{
    private Dictionary<string, SymbolDefinition> definitionsByAssetName = new Dictionary<string, SymbolDefinition>();
    private Dictionary<string, List<SymbolDefinition>> definitionsBySymbolName = new Dictionary<string, List<SymbolDefinition>>();
    private Dictionary<string, SymbolDefinition> definitionsBySpriteName = new Dictionary<string, SymbolDefinition>();

    protected override void Awake()
    {
        base.Awake();
        Refresh();
    }

    /// <summary>
    /// Re-scan loaded SymbolDefinition assets and rebuild internal lookup tables.
    /// Call when definitions are added/removed at runtime (editor tools) or to refresh cache.
    /// </summary>
    public void Refresh()
    {
        definitionsByAssetName.Clear();
        definitionsBySymbolName.Clear();
        definitionsBySpriteName.Clear();

        // Try to load definitions from Resources explicitly first. Use LoadAll("") to search the Resources folders.
        var collected = new List<SymbolDefinition>();
        try
        {
            var loaded = Resources.LoadAll<SymbolDefinition>("");
            if (loaded != null && loaded.Length > 0) collected.AddRange(loaded);
        }
        catch (System.Exception)
        {
            // Ignore - some platforms/environments may limit Resources API
        }

        // Always also try FindObjectsOfTypeAll as a fallback (covers editor asset database objects)
        try
        {
            var found = Resources.FindObjectsOfTypeAll<SymbolDefinition>();
            if (found != null && found.Length > 0) collected.AddRange(found);
        }
        catch (System.Exception)
        {
            // ignore
        }

        // Deduplicate
        var all = collected.Distinct().ToArray();

        if (all == null || all.Length == 0)
        {
            // Fail fast: SymbolDefinitionManager must have definitions available for runtime systems to function.
            throw new InvalidOperationException("SymbolDefinitionManager.Refresh: no SymbolDefinition assets found via Resources.LoadAll or FindObjectsOfTypeAll. Ensure definitions are placed under a Resources folder or the editor has loaded the assets before runtime.");
        }

        foreach (var def in all)
        {
            if (def == null) continue;
            definitionsByAssetName[def.name] = def;

            if (!string.IsNullOrEmpty(def.SymbolName))
            {
                if (!definitionsBySymbolName.TryGetValue(def.SymbolName, out var list))
                {
                    list = new List<SymbolDefinition>();
                    definitionsBySymbolName[def.SymbolName] = list;
                }
                list.Add(def);
            }

            if (def.SymbolSprite != null && !string.IsNullOrEmpty(def.SymbolSprite.name))
            {
                definitionsBySpriteName[def.SymbolSprite.name] = def;
            }
        }
    }

    public List<SymbolDefinition> GetAllDefinitions()
    {
        return new List<SymbolDefinition>(definitionsByAssetName.Values);
    }

    /// <summary>
    /// Tries to resolve a SymbolDefinition by a flexible key. The key is checked against:
    /// - asset name
    /// - symbol's authored `SymbolName`
    /// - sprite asset name
    /// Case-insensitive matches are attempted as a fallback.
    /// </summary>
    public bool TryGetDefinition(string key, out SymbolDefinition def)
    {
        def = null;
        if (string.IsNullOrEmpty(key)) return false;

        if (definitionsByAssetName.TryGetValue(key, out def)) return true;
        if (definitionsBySpriteName.TryGetValue(key, out def)) return true;
        if (definitionsBySymbolName.TryGetValue(key, out var list) && list != null && list.Count > 0)
        {
            def = list[0];
            return true;
        }

        // Fallback case-insensitive search
        foreach (var kv in definitionsByAssetName)
        {
            if (string.Equals(kv.Key, key, System.StringComparison.OrdinalIgnoreCase))
            {
                def = kv.Value; return true;
            }
        }
        foreach (var kv in definitionsBySpriteName)
        {
            if (string.Equals(kv.Key, key, System.StringComparison.OrdinalIgnoreCase))
            {
                def = kv.Value; return true;
            }
        }
        foreach (var kv in definitionsBySymbolName)
        {
            if (string.Equals(kv.Key, key, System.StringComparison.OrdinalIgnoreCase) && kv.Value != null && kv.Value.Count > 0)
            {
                def = kv.Value[0]; return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Try to find a definition by key but prefer the provided preferredDefinitions collection first.
    /// This allows strip-local definitions to be considered before the global set.
    /// </summary>
    public bool TryGetDefinition(string key, IEnumerable<SymbolDefinition> preferredDefinitions, out SymbolDefinition def)
    {
        def = null;
        if (!string.IsNullOrEmpty(key) && preferredDefinitions != null)
        {
            foreach (var pd in preferredDefinitions)
            {
                if (pd == null) continue;
                if (string.Equals(pd.name, key, StringComparison.Ordinal) || string.Equals(pd.name, key, StringComparison.OrdinalIgnoreCase)) { def = pd; return true; }
                if (!string.IsNullOrEmpty(pd.SymbolName) && (string.Equals(pd.SymbolName, key, StringComparison.Ordinal) || string.Equals(pd.SymbolName, key, StringComparison.OrdinalIgnoreCase))) { def = pd; return true; }
                if (pd.SymbolSprite != null && (string.Equals(pd.SymbolSprite.name, key, StringComparison.Ordinal) || string.Equals(pd.SymbolSprite.name, key, StringComparison.OrdinalIgnoreCase))) { def = pd; return true; }
            }
        }

        return TryGetDefinition(key, out def);
    }

    /// <summary>
    /// Convenience accessor that returns null when not found.
    /// </summary>
    public SymbolDefinition GetDefinitionOrNull(string key)
    {
        return TryGetDefinition(key, out var d) ? d : null;
    }

    /// <summary>
    /// Convenience accessor that prefers a set of local definitions before falling back to the global registry.
    /// Returns null when not found.
    /// </summary>
    public SymbolDefinition GetDefinitionOrNull(string key, IEnumerable<SymbolDefinition> preferredDefinitions)
    {
        return TryGetDefinition(key, preferredDefinitions, out var d) ? d : null;
    }
}
