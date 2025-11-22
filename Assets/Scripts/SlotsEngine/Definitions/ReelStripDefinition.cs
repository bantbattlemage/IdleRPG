using System;
using UnityEngine;

public class ReelStripDefinition : BaseDefinition<ReelStripData>
{
    [SerializeField] private int stripSize;
    public int StripSize => stripSize;
    
    [SerializeField] private SymbolDefinition[] symbols;
    public SymbolDefinition[] Symbols => symbols;

    // Optional: fixed count for each symbol on this strip. Length should match Symbols length.
    [SerializeField] private int[] symbolCounts;
    public int[] SymbolCounts => symbolCounts;

    // Optional: whether fixed counts are depletable during a spin (default true = depletable).
    [SerializeField] private bool[] symbolCountsDepletable;
    public bool[] SymbolCountsDepletable => symbolCountsDepletable;

    public override ReelStripData CreateInstance()
    {
        int[] counts = (symbolCounts != null && symbols != null && symbolCounts.Length == symbols.Length) ? symbolCounts : null;
        bool[] flags = (symbolCountsDepletable != null && symbols != null && symbolCountsDepletable.Length == symbols.Length) ? symbolCountsDepletable : null;
        return new ReelStripData(this, stripSize, symbols, counts, flags);
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (stripSize < 1) stripSize = 1;
        if (symbols != null)
        {
            if (symbolCounts == null || symbolCounts.Length != symbols.Length)
            {
                Array.Resize(ref symbolCounts, symbols.Length);
            }
            if (symbolCountsDepletable == null || symbolCountsDepletable.Length != symbols.Length)
            {
                int oldLen = symbolCountsDepletable != null ? symbolCountsDepletable.Length : 0;
                Array.Resize(ref symbolCountsDepletable, symbols.Length);
                for (int i = oldLen; i < symbolCountsDepletable.Length; i++) symbolCountsDepletable[i] = true; // default true
            }

            int total = 0;
            for (int i = 0; i < symbolCounts.Length; i++)
            {
                if (symbolCounts[i] < 0) symbolCounts[i] = 0;
                total += symbolCounts[i];
            }
            if (total > stripSize)
            {
                float scale = stripSize / (float)total;
                int newTotal = 0;
                for (int i = 0; i < symbolCounts.Length; i++)
                {
                    symbolCounts[i] = Mathf.FloorToInt(symbolCounts[i] * scale);
                    newTotal += symbolCounts[i];
                }
                int deficit = stripSize - newTotal;
                for (int i = 0; i < symbolCounts.Length && deficit > 0; i++)
                {
                    symbolCounts[i]++;
                    deficit--;
                }
            }
        }
    }
#endif
}