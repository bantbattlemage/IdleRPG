using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using WeightedRandomNamespace;

public class ReelStripData : Data
{
    [SerializeField] private int stripSize;
    [SerializeField] private string[] symbolDefinitionKeys;
    [SerializeField] private string definitionKey;

    [System.NonSerialized] private SymbolDefinition[] symbolDefinitions;
    [System.NonSerialized] private ReelStripDefinition definition;

    [System.NonSerialized] private int[] fixedCounts; // reserved counts per symbol
    [System.NonSerialized] private int[] remainingCounts; // remaining reserved counts per symbol for current spin (for depletable ones)
    [System.NonSerialized] private bool[] countDepletable; // whether corresponding fixedCounts entry depletes
    [System.NonSerialized] private int internalPicksSoFar;

    public int StripSize => stripSize;
    public SymbolDefinition[] SymbolDefinitions { get { EnsureResolved(); return symbolDefinitions; } }
    public ReelStripDefinition Definition { get { EnsureResolved(); return definition; } }

    public ReelStripData(ReelStripDefinition def, int size, SymbolDefinition[] syms, int[] counts = null, bool[] depletable = null)
    {
        stripSize = size;
        definition = def;
        symbolDefinitions = syms;
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
        if (syms != null)
        {
            symbolDefinitionKeys = new string[syms.Length];
            for (int i = 0; i < syms.Length; i++) symbolDefinitionKeys[i] = syms[i].name;
        }
    }

    private void EnsureResolved()
    {
        if (definition == null && !string.IsNullOrEmpty(definitionKey)) definition = DefinitionResolver.Resolve<ReelStripDefinition>(definitionKey);
        if ((symbolDefinitions == null || symbolDefinitions.Length == 0) && symbolDefinitionKeys != null)
        {
            symbolDefinitions = new SymbolDefinition[symbolDefinitionKeys.Length];
            for (int i = 0; i < symbolDefinitionKeys.Length; i++) symbolDefinitions[i] = DefinitionResolver.Resolve<SymbolDefinition>(symbolDefinitionKeys[i]);
        }
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

    public void ResetSpinCounts()
    {
        EnsureResolved();
        if (fixedCounts != null) remainingCounts = (int[])fixedCounts.Clone();
        internalPicksSoFar = 0;
    }

    // Simple variant delegates to constrained version without tracking list.
    public SymbolData GetWeightedSymbol() => GetWeightedSymbol(null);

    public SymbolData GetWeightedSymbol(List<SymbolData> existingSelections) => GetWeightedSymbol(existingSelections, consumeReserved: true);

    /// <summary>
    /// Core selection overload with ability to skip consuming reserved counts (used for dummy symbols).
    /// Added optional parameter `useSeededRng` to allow callers to bypass the project's seeded RNG.
    /// </summary>
    public SymbolData GetWeightedSymbol(List<SymbolData> existingSelections, bool consumeReserved, bool useSeededRng = true)
    {
        EnsureResolved();
        if (symbolDefinitions == null || symbolDefinitions.Length == 0) return null;

        // Track remaining pulls for this spin step
        int picksSoFar = existingSelections != null ? existingSelections.Count : internalPicksSoFar;
        int remainingSpins = Mathf.Max(stripSize - picksSoFar, 0);

        Dictionary<int, int> groupUsage = null;
        if (existingSelections != null && existingSelections.Count > 0)
        {
            groupUsage = new Dictionary<int, int>();
            for (int i = 0; i < existingSelections.Count; i++)
            {
                var s = existingSelections[i]; if (s == null) continue; int g = s.MatchGroupId; if (g != 0) groupUsage[g] = groupUsage.TryGetValue(g, out var c) ? c + 1 : 1;
            }
        }

        // Compute reserved remaining: non-depletable uses original count; depletable uses remaining count.
        int reservedRemaining = 0;
        if (fixedCounts != null)
        {
            for (int i = 0; i < fixedCounts.Length; i++)
            {
                int contribution;
                if (countDepletable != null && !countDepletable[i])
                {
                    contribution = Mathf.Max(fixedCounts[i], 0);
                }
                else
                {
                    int rem = remainingCounts != null && i < remainingCounts.Length ? remainingCounts[i] : 0;
                    contribution = Mathf.Max(rem, 0);
                }
                reservedRemaining += contribution;
            }
            if (reservedRemaining > remainingSpins) reservedRemaining = remainingSpins;
        }
        int randomPoolSize = Mathf.Max(remainingSpins - reservedRemaining, 0);

        // Total weight across random-eligible symbols (either non-reserved or depletable with remaining == 0).
        float totalRandomWeight = 0f; bool[] randomEligible = new bool[symbolDefinitions.Length];
        for (int i = 0; i < symbolDefinitions.Length; i++)
        {
            var def = symbolDefinitions[i]; if (def == null) continue;
            int rem = remainingCounts != null && i < remainingCounts.Length ? remainingCounts[i] : 0;
            bool deplete = countDepletable != null && i < countDepletable.Length ? countDepletable[i] : true;
            bool hasReserve = fixedCounts != null && i < fixedCounts.Length && fixedCounts[i] > 0;
            bool reservedActive = hasReserve && (deplete ? rem > 0 : fixedCounts[i] > 0);
            if (!reservedActive)
            {
                randomEligible[i] = true; totalRandomWeight += def.Weight;
            }
        }

        var candidates = new List<(SymbolDefinition def, float weight, int index)>();
        for (int i = 0; i < symbolDefinitions.Length; i++)
        {
            var def = symbolDefinitions[i]; if (def == null) continue;

            if (existingSelections != null)
            {
                int max = def.MaxPerReel; if (max >= 0)
                {
                    int used = 0;
                    if (def.MatchGroupId != 0) { if (groupUsage != null) groupUsage.TryGetValue(def.MatchGroupId, out used); }
                    else if (existingSelections != null)
                    {
                        for (int e = 0; e < existingSelections.Count; e++) { var s = existingSelections[e]; if (s == null) continue; if (!string.IsNullOrEmpty(def.SymbolName) && def.SymbolName == s.Name) used++; }
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
                // Reserved portion contributes as weight units equal to remaining (if depletable) or original (if non-depletable)
                // Clamp to remaining spins to avoid overweighting when more reserves than pulls remain.
                weightUnits = Mathf.Min(depleteFlag ? fixedRem : fixedOrig, remainingSpins);
            }
            else
            {
                if (randomPoolSize <= 0 || !randomEligible[i] || totalRandomWeight <= 0f) continue;
                weightUnits = (def.Weight / totalRandomWeight) * randomPoolSize;
                if (weightUnits <= 0f) continue;
            }
            candidates.Add((def, weightUnits, i));
        }

        if (candidates.Count == 0)
        {
            for (int i = 0; i < symbolDefinitions.Length; i++) { var def = symbolDefinitions[i]; if (def == null) continue; candidates.Add((def, Mathf.Max(def.Weight, 1f), i)); }
            if (candidates.Count == 0) return null;
        }

        // Build simple picker list
        var picker = new List<(SymbolDefinition, float)>(candidates.Count);
        for (int i = 0; i < candidates.Count; i++) picker.Add((candidates[i].def, candidates[i].weight));

        SymbolDefinition pickedDef = null;
        if (useSeededRng)
        {
            // Use existing centralized RNG via WeightedRandom (which calls RNGManager)
            pickedDef = WeightedRandom.Pick(picker) ?? symbolDefinitions.FirstOrDefault(d => d != null);
        }
        else
        {
            // Use UnityEngine.Random (non-seeded by RNGManager) for dummy/non-deterministic picks
            float total = 0f;
            for (int i = 0; i < picker.Count; i++) total += picker[i].Item2;
            if (total <= 0f)
            {
                pickedDef = picker.Count > 0 ? picker[0].Item1 : symbolDefinitions.FirstOrDefault(d => d != null);
            }
            else
            {
                float r = UnityEngine.Random.value * total;
                for (int i = 0; i < picker.Count; i++)
                {
                    r -= picker[i].Item2;
                    if (r <= 0f)
                    {
                        pickedDef = picker[i].Item1;
                        break;
                    }
                }
                if (pickedDef == null) pickedDef = picker[picker.Count - 1].Item1;
            }
        }

        if (pickedDef == null) return null;

        // Consume only if depletable and requested.
        if (consumeReserved && remainingCounts != null && fixedCounts != null)
        {
            for (int i = 0; i < symbolDefinitions.Length; i++)
            {
                if (symbolDefinitions[i] == pickedDef)
                {
                    bool deplete = countDepletable != null && i < countDepletable.Length ? countDepletable[i] : true;
                    if (deplete && remainingCounts[i] > 0) remainingCounts[i]--;
                    break;
                }
            }
        }

        if (existingSelections == null) internalPicksSoFar++;
        return pickedDef.CreateInstance();
    }
}
