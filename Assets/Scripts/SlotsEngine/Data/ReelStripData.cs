using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using WeightedRandomNamespace;
using System;
using EvaluatorCore;

[System.Serializable]
public class ReelStripData : Data
{
    [SerializeField] private int stripSize;
    [SerializeField] private string[] symbolDefinitionKeys; // original authoring definition keys
    [SerializeField] private string definitionKey;          // reel strip definition asset key
    [SerializeField] private string instanceKey;            // unique per runtime instance (used for per-strip associations)

    // New: Persist runtime symbol accessor ids so we can reconstruct mutable runtime symbols
    [SerializeField] private int[] runtimeSymbolAccessorIds; // parallel to runtimeSymbols list
    [SerializeField] private string[] runtimeSymbolKeys;     // fallback sprite/name keys (legacy / migration support)

    [System.NonSerialized] private SymbolDefinition[] symbolDefinitions; // authoring definitions (immutable)
    [System.NonSerialized] private ReelStripDefinition definition;

    // Reserved counts logic retained for weighting by authoring definitions
    [System.NonSerialized] private int[] fixedCounts; // reserved counts per symbol (definition index)
    [System.NonSerialized] private int[] remainingCounts; // remaining reserved counts per symbol for current spin (for depletable ones)
    [System.NonSerialized] private bool[] countDepletable; // whether corresponding fixedCounts entry depletes
    [System.NonSerialized] private int internalPicksSoFar;

    // New: mutable per-strip runtime symbol instances (independent of definitions)
    [System.NonSerialized] private List<SymbolData> runtimeSymbols;

    [NonSerialized] private bool editLocked;

    public int StripSize => stripSize;
    public SymbolDefinition[] SymbolDefinitions { get { EnsureResolved(); return symbolDefinitions; } }
    public ReelStripDefinition Definition { get { EnsureResolved(); return definition; } }
    public string InstanceKey => instanceKey;
    public IReadOnlyList<SymbolData> RuntimeSymbols { get { EnsureResolved(); return runtimeSymbols; } }
    public bool IsEditLocked => editLocked;

    public void SetEditLock(bool locked) { editLocked = locked; }

    public ReelStripData(ReelStripDefinition def, int size, SymbolDefinition[] syms, int[] counts = null, bool[] depletable = null)
    {
        stripSize = size;
        definition = def;
        symbolDefinitions = syms != null ? (SymbolDefinition[])syms.Clone() : null; // clone array for safety
        fixedCounts = counts != null && syms != null && counts.Length == syms.Length ? (int[])counts.Clone() : null;
        countDepletable = depletable != null && syms != null && depletable.Length == syms.Length ? (bool[])depletable.Clone() : null;
        if (fixedCounts != null && countDepletable == null)
        {
            countDepletable = new bool[fixedCounts.Length];
            for (int i = 0; i < countDepletable.Length; i++) countDepletable[i] = true;
        }
        remainingCounts = fixedCounts != null ? (int[])fixedCounts.Clone() : null;
        internalPicksSoFar = 0;
        definitionKey = def != null ? def.name : null;
        instanceKey = Guid.NewGuid().ToString();

        if (symbolDefinitions != null)
        {
            symbolDefinitionKeys = new string[symbolDefinitions.Length];
            for (int i = 0; i < symbolDefinitions.Length; i++) symbolDefinitionKeys[i] = symbolDefinitions[i] != null ? symbolDefinitions[i].name : null;
        }

        // Initialize runtime symbols from definitions (each gets its own instance)
        runtimeSymbols = new List<SymbolData>();
        if (symbolDefinitions != null)
        {
            for (int i = 0; i < symbolDefinitions.Length; i++)
            {
                var defSym = symbolDefinitions[i];
                if (defSym == null) { runtimeSymbols.Add(null); continue; }
                var inst = defSym.CreateInstance();
                RegisterRuntimeSymbol(inst);
                runtimeSymbols.Add(inst); // ADD missing push
            }
        }

        // After registering runtime symbol instances, ensure persistence arrays reflect their accessor ids and keys
        SyncPersistenceArrays();
    }

    private void EnsureResolved()
    {
        if (definition == null && !string.IsNullOrEmpty(definitionKey)) definition = DefinitionResolver.Resolve<ReelStripDefinition>(definitionKey);

        if ((symbolDefinitions == null || symbolDefinitions.Length == 0) && symbolDefinitionKeys != null)
        {
            symbolDefinitions = new SymbolDefinition[symbolDefinitionKeys.Length];
            for (int i = 0; i < symbolDefinitionKeys.Length; i++)
            {
                SymbolDefinition resolved = null;
                // Use centralized manager only
                if (SymbolDefinitionManager.Instance != null)
                {
                    resolved = SymbolDefinitionManager.Instance.GetDefinitionOrNull(symbolDefinitionKeys[i]);
                }
                // Do not fallback to DefinitionResolver here; manager is authoritative
                symbolDefinitions[i] = resolved;
            }
        }

        // Initialize runtimeSymbols list if missing (handles load/migration cases)
        if (runtimeSymbols == null)
        {
            runtimeSymbols = new List<SymbolData>();
        }

        // Reconstruct runtime symbols from persisted accessor ids first (preferred for mutated data)
        if (runtimeSymbolAccessorIds != null && runtimeSymbolAccessorIds.Length > 0 && runtimeSymbols.Count == 0)
        {
            for (int i = 0; i < runtimeSymbolAccessorIds.Length; i++)
            {
                SymbolData inst = null;
                int accessor = runtimeSymbolAccessorIds[i];
                if (accessor > 0 && SymbolDataManager.Instance != null && SymbolDataManager.Instance.TryGetData(accessor, out var stored))
                {
                    inst = stored;
                }
                else if (runtimeSymbolKeys != null && i < runtimeSymbolKeys.Length && !string.IsNullOrEmpty(runtimeSymbolKeys[i]))
                {
                    // Previously we would create an ad-hoc SymbolData here which could produce default names like "Symbol0".
                    // Treat this as an error unless we can resolve to an existing persisted SymbolData or a SymbolDefinition.
                    string key = runtimeSymbolKeys[i];
                    var sprite = AssetResolver.ResolveSprite(key);
                    if (sprite == null)
                    {
                        throw new InvalidOperationException($"ReelStripData.EnsureResolved: failed to resolve Sprite for runtimeSymbolKeys[{i}]='{key}'. Persisted runtimeSymbolKeys must resolve to valid Sprite assets.");
                    }

                    // Try to find an existing persisted SymbolData with matching SpriteKey
                    SymbolData persisted = null;
                    if (SymbolDataManager.Instance != null)
                    {
                        var all = SymbolDataManager.Instance.GetAllData();
                        for (int j = 0; j < all.Count; j++) { var s = all[j]; if (s == null) continue; if (!string.IsNullOrEmpty(s.SpriteKey) && string.Equals(s.SpriteKey, key, StringComparison.OrdinalIgnoreCase)) { persisted = s; break; } }
                    }

                    if (persisted != null)
                    {
                        inst = persisted;
                        if (inst.Sprite == null) inst.Sprite = sprite;
                    }
                    else
                    {
                        // Try to resolve an authoring definition for this key
                        SymbolDefinition def = null;
                        if (SymbolDefinitionManager.Instance != null)
                        {
                            SymbolDefinitionManager.Instance.TryGetDefinition(key, out def);
                        }

                        if (def != null)
                        {
                            inst = def.CreateInstance();
                            if (inst.Sprite == null) inst.Sprite = sprite;
                            RegisterRuntimeSymbol(inst);
                        }
                        else
                        {
                            // Do not create ad-hoc SymbolData; surface error to require data correction.
                            throw new InvalidOperationException($"ReelStripData.EnsureResolved: runtimeSymbolKeys[{i}]='{key}' did not match any persisted SymbolData or any SymbolDefinition. Persisted data must reference a valid definition or SymbolData accessor.");
                        }
                    }
                }
                runtimeSymbols.Add(inst);
            }
        }

        // If still empty (new or legacy save), build from definitions
        // Only auto-populate from authoring definitions when there is no persisted runtime symbol arrays
        // If a persisted runtime list exists (even empty) we must respect it as the authoritative edited state.
        if (runtimeSymbols.Count == 0 && symbolDefinitions != null && runtimeSymbolAccessorIds == null && runtimeSymbolKeys == null)
        {
            for (int i = 0; i < symbolDefinitions.Length; i++)
            {
                var def = symbolDefinitions[i];
                if (def == null) { runtimeSymbols.Add(null); continue; }
                var inst = def.CreateInstance();
                RegisterRuntimeSymbol(inst);
                runtimeSymbols.Add(inst); // ADD missing push
            }
        }

        // Ensure persistence arrays match runtime list length
        SyncPersistenceArrays();

        // Reserved counts resolution
        if (fixedCounts == null && definition != null && definition.SymbolCounts != null && symbolDefinitions != null)
        {
            var src = definition.SymbolCounts;
            if (src.Length == symbolDefinitions.Length)
            {
                fixedCounts = (int[])src.Clone();
                remainingCounts = (int[])fixedCounts.Clone();
            }
        }
        if (countDepletable == null && definition != null && definition.SymbolCountsDepletable != null && symbolDefinitions != null)
        {
            var dep = definition.SymbolCountsDepletable;
            if (dep.Length == symbolDefinitions.Length)
            {
                countDepletable = (bool[])dep.Clone();
            }
        }
        if (fixedCounts != null && countDepletable == null)
        {
            countDepletable = new bool[fixedCounts.Length]; for (int i = 0; i < countDepletable.Length; i++) countDepletable[i] = true;
        }
    }

    private void SyncPersistenceArrays()
    {
        int len = runtimeSymbols != null ? runtimeSymbols.Count : 0;
        // When there are no runtime symbols, clear persistence arrays so EnsureResolved does not
        // reconstruct previously-removed symbols from stale accessor ids / keys.
        if (len == 0)
        {
            runtimeSymbolAccessorIds = new int[0];
            runtimeSymbolKeys = new string[0];
            return;
        }
        if (runtimeSymbolAccessorIds == null || runtimeSymbolAccessorIds.Length != len)
        {
            runtimeSymbolAccessorIds = new int[len];
        }
        if (runtimeSymbolKeys == null || runtimeSymbolKeys.Length != len)
        {
            runtimeSymbolKeys = new string[len];
        }
        for (int i = 0; i < len; i++)
        {
            var sym = runtimeSymbols[i];
            runtimeSymbolAccessorIds[i] = sym != null ? sym.AccessorId : -1;
            // Persist the sprite key (asset key) rather than the display name so we can reliably resolve sprites on load
            runtimeSymbolKeys[i] = sym != null ? sym.SpriteKey : null;
        }
    }

    private void RegisterRuntimeSymbol(SymbolData inst)
    {
        if (inst == null) return;
        if (SymbolDataManager.Instance != null && inst.AccessorId == 0)
        {
            SymbolDataManager.Instance.AddNewData(inst);
        }

        // Ensure runtime SymbolData carries a persistent spriteKey when possible.
        // If no spriteKey is set, attempt to match this symbol's Name against the strip's definitions
        // (authoring SymbolName, asset name, or sprite asset name) and persist the definition sprite name.
        if (string.IsNullOrEmpty(inst.SpriteKey))
        {
            EnsureResolved(); // populate symbolDefinitions if needed
            if (symbolDefinitions != null)
            {
                for (int i = 0; i < symbolDefinitions.Length; i++)
                {
                    var def = symbolDefinitions[i];
                    if (def == null) continue;
                    // case-insensitive matching to be forgiving
                    if (!string.IsNullOrEmpty(def.SymbolName) && string.Equals(def.SymbolName, inst.Name, StringComparison.OrdinalIgnoreCase))
                    {
                        if (def.SymbolSprite != null)
                        {
                            inst.SetSpriteKey(def.SymbolSprite.name);
                            break;
                        }
                    }
                    if (string.Equals(def.name, inst.Name, StringComparison.OrdinalIgnoreCase))
                    {
                        if (def.SymbolSprite != null)
                        {
                            inst.SetSpriteKey(def.SymbolSprite.name);
                            break;
                        }
                    }
                    if (def.SymbolSprite != null && string.Equals(def.SymbolSprite.name, inst.Name, StringComparison.OrdinalIgnoreCase))
                    {
                        inst.SetSpriteKey(def.SymbolSprite.name);
                        break;
                    }
                }
            }

            // If we still don't have a SpriteKey at this point, that's an invariant violation — surface immediately.
            if (string.IsNullOrEmpty(inst.SpriteKey))
            {
                throw new InvalidOperationException($"ReelStripData.RegisterRuntimeSymbol: SymbolData '{inst.Name ?? "<unnamed>"}' (accessorId={inst.AccessorId}) has no SpriteKey and could not be matched to a definition.");
            }
        }
    }

    public void ResetSpinCounts()
    {
        EnsureResolved();
        if (fixedCounts != null) remainingCounts = (int[])fixedCounts.Clone();
        internalPicksSoFar = 0;
    }

    // Public API: add a runtime symbol (e.g., from inventory). Optionally built from a SymbolDefinition or raw data.
    public void AddRuntimeSymbol(SymbolData symbol)
    {
        EnsureResolved();
        if (editLocked)
        {
            Debug.LogWarning("ReelStripData: Attempted to edit strip while spin is active. Edit ignored.");
            return;
        }
        if (symbol == null) return;
        RegisterRuntimeSymbol(symbol);
        runtimeSymbols.Add(symbol);
        SyncPersistenceArrays();
        ReelStripDataManager.Instance.UpdateRuntimeStrip(this);
    }

    public void RemoveRuntimeSymbolAt(int index)
    {
        EnsureResolved();
        if (editLocked)
        {
            Debug.LogWarning("ReelStripData: Attempted to edit strip while spin is active. Edit ignored.");
            return;
        }
        if (runtimeSymbols == null || index < 0 || index >= runtimeSymbols.Count) return;

        Debug.Log($"ReelStripData.RemoveRuntimeSymbolAt: removing index={index} currentCount={runtimeSymbols.Count}");
        var sym = runtimeSymbols[index];
        runtimeSymbols.RemoveAt(index);
        Debug.Log($"ReelStripData.RemoveRuntimeSymbolAt: removed. newCount={runtimeSymbols.Count}");
        SyncPersistenceArrays();
        ReelStripDataManager.Instance.UpdateRuntimeStrip(this);
        CleanupSymbolIfOrphan(sym);
    }

    public void RemoveRuntimeSymbol(SymbolData symbol)
    {
        EnsureResolved();
        if (editLocked)
        {
            Debug.LogWarning("ReelStripData: Attempted to edit strip while spin is active. Edit ignored.");
            return;
        }
        if (runtimeSymbols == null || symbol == null) return;
        Debug.Log($"ReelStripData.RemoveRuntimeSymbol: attempting to remove symbol accessor={symbol.AccessorId} name={symbol.Name} countBefore={runtimeSymbols.Count}");
        bool removed = runtimeSymbols.Remove(symbol);
        Debug.Log($"ReelStripData.RemoveRuntimeSymbol: removed={removed} newCount={runtimeSymbols?.Count}");
        if (!removed) return;
        SyncPersistenceArrays();
        ReelStripDataManager.Instance.UpdateRuntimeStrip(this);
        CleanupSymbolIfOrphan(symbol);
    }

    private void CleanupSymbolIfOrphan(SymbolData symbol)
    {
        if (symbol == null || SymbolDataManager.Instance == null) return;
        // If no other reel strips reference this accessor id, remove it from manager.
        int accessor = symbol.AccessorId;
        if (accessor <= 0) return;
        bool stillUsed = false;
        // Scan all reel strips managed.
        var all = ReelStripDataManager.Instance.ReadOnlyLocalData;
        if (all != null)
        {
            foreach (var kv in all)
            {
                var rs = kv.Value; if (rs == null) continue;
                var list = rs.RuntimeSymbols;
                if (list == null) continue;
                for (int i = 0; i < list.Count; i++)
                {
                    var other = list[i];
                    if (other != null && other.AccessorId == accessor) { stillUsed = true; break; }
                }
                if (stillUsed) break;
            }
        }
        if (!stillUsed)
        {
            SymbolDataManager.Instance.RemoveDataIfExists(symbol);
        }
    }

    // Weighted selection now uses runtimeSymbols list (mutable instances). Falls back to definitions when needed.
    public SymbolData GetWeightedSymbol() => GetWeightedSymbol(null);

    public SymbolData GetWeightedSymbol(List<SymbolData> existingSelections) => GetWeightedSymbol(existingSelections, consumeReserved: true);

    /// <summary>
    /// Weighted selection across runtimeSymbols. Uses original definition weights where possible.
    /// Returns a reference to an existing runtime SymbolData (not a new clone) so modifications persist.
    /// </summary>
    public SymbolData GetWeightedSymbol(List<SymbolData> existingSelections, bool consumeReserved, bool useSeededRng = true)
    {
        EnsureResolved();
        if (runtimeSymbols == null || runtimeSymbols.Count == 0) return null;

        // Use definition arrays for weight / reserved counts alignment; ensure lengths match
        var defs = symbolDefinitions;
        int count = Mathf.Min(defs != null ? defs.Length : 0, runtimeSymbols.Count);
        if (count == 0) return null;

        int picksSoFar = existingSelections != null ? existingSelections.Count : internalPicksSoFar;
        int remainingSpins = Mathf.Max(stripSize - picksSoFar, 0);

        // group usage for match constraints
        Dictionary<int, int> groupUsage = null;
        if (existingSelections != null && existingSelections.Count > 0)
        {
            groupUsage = new Dictionary<int, int>();
            for (int i = 0; i < existingSelections.Count; i++)
            {
                var s = existingSelections[i]; if (s == null) continue; int g = s.MatchGroupId; if (g != 0) groupUsage[g] = groupUsage.TryGetValue(g, out var c) ? c + 1 : 1;
            }
        }

        int reservedRemaining = 0;
        if (fixedCounts != null)
        {
            for (int i = 0; i < fixedCounts.Length && i < count; i++)
            {
                int contribution;
                bool depleteFlag = countDepletable != null && i < countDepletable.Length ? countDepletable[i] : true;
                int rem = remainingCounts != null && i < remainingCounts.Length ? remainingCounts[i] : 0;
                if (countDepletable != null && !depleteFlag)
                {
                    contribution = Mathf.Max(fixedCounts[i], 0);
                }
                else
                {
                    contribution = Mathf.Max(rem, 0);
                }
                reservedRemaining += contribution;
            }
            if (reservedRemaining > remainingSpins) reservedRemaining = remainingSpins;
        }
        int randomPoolSize = Mathf.Max(remainingSpins - reservedRemaining, 0);

        float totalRandomWeight = 0f; bool[] randomEligible = new bool[count];
        for (int i = 0; i < count; i++)
        {
            var def = defs[i]; if (def == null) continue;
            int rem = remainingCounts != null && i < remainingCounts.Length ? remainingCounts[i] : 0;
            bool deplete = countDepletable != null && i < countDepletable.Length ? countDepletable[i] : true;
            bool hasReserve = fixedCounts != null && i < fixedCounts.Length && fixedCounts[i] > 0;
            bool reservedActive = hasReserve && (deplete ? rem > 0 : fixedCounts[i] > 0);
            if (!reservedActive)
            {
                randomEligible[i] = true; totalRandomWeight += def.Weight;
            }
        }

        var candidates = new List<(int index, float weight)>();
        for (int i = 0; i < count; i++)
        {
            var def = defs[i]; var sym = runtimeSymbols[i]; if (def == null || sym == null) continue;

            if (existingSelections != null)
            {
                int max = def.MaxPerReel; if (max >= 0)
                {
                    int used = 0;
                    if (sym.MatchGroupId > 0) { if (groupUsage != null) groupUsage.TryGetValue(sym.MatchGroupId, out used); }
                    else if (existingSelections != null)
                    {
                        for (int e = 0; e < existingSelections.Count; e++) { var s = existingSelections[e]; if (s == null) continue; if (!string.IsNullOrEmpty(sym.Name) && sym.Name == s.Name) used++; }
                    }
                    if (used >= max) continue;
                }
            }

            bool depleteFlag = countDepletable != null && i < countDepletable.Length ? countDepletable[i] : true;
            int fixedRem = remainingCounts != null && i < remainingCounts.Length ? remainingCounts[i] : 0;
            int fixedOrig = fixedCounts != null && i < fixedCounts.Length ? fixedCounts[i] : 0;

            float weightUnits;
            if ((depleteFlag && fixedRem > 0) || (!depleteFlag && fixedOrig > 0))
            {
                weightUnits = Mathf.Min(depleteFlag ? fixedRem : fixedOrig, remainingSpins);
            }
            else
            {
                if (randomPoolSize <= 0 || !randomEligible[i] || totalRandomWeight <= 0f) continue;
                weightUnits = (def.Weight / totalRandomWeight) * randomPoolSize;
                if (weightUnits <= 0f) continue;
            }
            candidates.Add((i, weightUnits));
        }

        if (candidates.Count == 0)
        {
            for (int i = 0; i < count; i++) { var def = defs[i]; var sym = runtimeSymbols[i]; if (def == null || sym == null) continue; candidates.Add((i, Mathf.Max(def.Weight, 1f))); }
            if (candidates.Count == 0) return null;
        }

        int pickedIndex = candidates[0].index; // fallback
        if (useSeededRng)
        {
            // Build tuple list for WeightedRandom
            var picker = new List<(SymbolDefinition, float)>(candidates.Count);
            for (int i = 0; i < candidates.Count; i++) picker.Add((defs[candidates[i].index], candidates[i].weight));
            var pickedDef = WeightedRandom.Pick(picker) ?? defs[candidates[0].index];
            // map picked definition back to index
            for (int i = 0; i < count; i++) if (defs[i] == pickedDef) { pickedIndex = i; break; }
        }
        else
        {
            float total = 0f;
            for (int i = 0; i < candidates.Count; i++) total += candidates[i].weight;
            float r = (float)(RNGManager.UnseededDouble() * total);
            for (int i = 0; i < candidates.Count; i++)
            {
                r -= candidates[i].weight;
                if (r <= 0f) { pickedIndex = candidates[i].index; break; }
            }
        }

        // Consume reserved counts
        if (consumeReserved && remainingCounts != null && fixedCounts != null && pickedIndex >= 0 && pickedIndex < count)
        {
            bool deplete = countDepletable != null && pickedIndex < countDepletable.Length ? countDepletable[pickedIndex] : true;
            if (deplete && remainingCounts[pickedIndex] > 0) remainingCounts[pickedIndex]--;
        }

        if (existingSelections == null) internalPicksSoFar++;
        return runtimeSymbols[pickedIndex];
    }
}
