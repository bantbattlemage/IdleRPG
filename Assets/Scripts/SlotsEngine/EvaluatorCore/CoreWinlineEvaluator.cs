using System;
using System.Collections.Generic;
using System.Linq;
using EvaluatorCore;

namespace EvaluatorCore
{
    public class CoreWinData { public int LineIndex; public int WinValue; public int[] WinningSymbolIndexes; public CoreWinData(int l, int v, int[] idxs) { LineIndex = l; WinValue = v; WinningSymbolIndexes = idxs; } }

    public static class CoreWinlineEvaluator
    {
        // Evaluate using PlainSymbolData and simple patterns (int[] per-column indexes)
        public static List<CoreWinData> EvaluateWins(PlainSymbolData[] grid, int columns, int[] rowsPerColumn, List<int[]> winlines, List<int> winMultipliers, int creditCost = 1)
        {
            var results = new List<CoreWinData>();
            if (grid == null || winlines == null) return results;

            // normalize grid size to rectangular layout
            int maxRows = 0; for (int i = 0; i < rowsPerColumn.Length; i++) if (rowsPerColumn[i] > maxRows) maxRows = rowsPerColumn[i];
            int expected = maxRows * columns;
            if (grid.Length != expected) Array.Resize(ref grid, expected);

            for (int i = 0; i < winlines.Count; i++)
            {
                var pattern = winlines[i]; if (pattern == null || pattern.Length == 0) continue;

                // Find first usable index in pattern (handle jagged columns where some positions can be -1)
                int pStart = -1;
                for (int pi = 0; pi < pattern.Length; pi++)
                {
                    int candidate = pattern[pi];
                    if (candidate < 0 || candidate >= grid.Length) continue;
                    int col = candidate % columns; int row = candidate / columns; if (row >= rowsPerColumn[col]) continue;
                    var candCell = grid[candidate]; if (candCell == null) continue;
                    pStart = pi; break;
                }
                if (pStart < 0) continue;

                // Ensure there are no earlier valid entries (preserve leftmost-visible start semantics)
                bool earlierValidExists = false;
                for (int pj = 0; pj < pStart; pj++)
                {
                    int candidate = pattern[pj];
                    if (candidate < 0 || candidate >= grid.Length) continue;
                    int col = candidate % columns; int row = candidate / columns; if (row >= rowsPerColumn[col]) continue;
                    var candCell = grid[candidate]; if (candCell == null) continue;
                    earlierValidExists = true; break;
                }
                if (earlierValidExists) continue;

                int first = pattern[pStart];
                var trigger = grid[first]; if (trigger == null) continue;

                bool needsFallback = (trigger.MinWinDepth < 0 || trigger.WinMode != SymbolWinMode.LineMatch) || (trigger.IsWild && trigger.BaseValue <= 0);
                if (needsFallback)
                {
                    if (trigger.IsWild)
                    {
                        int alt = -1;
                        for (int p = pStart + 1; p < pattern.Length; p++)
                        {
                            int idx = pattern[p]; if (idx < 0 || idx >= grid.Length) continue;
                            int col = idx % columns; int row = idx / columns; if (row >= rowsPerColumn[col]) continue;
                            var cand = grid[idx]; if (cand == null) continue;
                            // Allow using a paying symbol as fallback, including paying wilds
                            if (cand.MinWinDepth >= 0 && cand.BaseValue > 0 && cand.WinMode == SymbolWinMode.LineMatch) { alt = idx; trigger = cand; break; }
                        }
                        if (alt < 0) continue;
                    }
                    else continue;
                }

                if (trigger.WinMode != SymbolWinMode.LineMatch) continue;
                if (trigger.BaseValue <= 0) continue;

                var matched = new List<int>();
                // Match contiguously starting from the chosen pStart position
                for (int k = pStart; k < pattern.Length; k++)
                {
                    int gi = pattern[k]; if (gi < 0 || gi >= grid.Length) break;
                    int col = gi % columns; int row = gi / columns; if (row >= rowsPerColumn[col]) break;
                    var cell = grid[gi]; if (cell == null) break;
                    if (cell.Matches(trigger)) matched.Add(gi); else break;
                }

                if (matched.Count >= trigger.MinWinDepth)
                {
                    int extraDepth = matched.Count - trigger.MinWinDepth;
                    long scaled = trigger.BaseValue;
                    switch (trigger.PayScaling)
                    {
                        case PayScaling.DepthSquared:
                            scaled = trigger.BaseValue * (1L << extraDepth);
                            break;
                        case PayScaling.PerSymbol:
                            int totalMatchesAcrossLine = 0;
                            for (int k = 0; k < pattern.Length; k++)
                            {
                                int gi = pattern[k]; if (gi < 0 || gi >= grid.Length) continue;
                                int col = gi % columns; int row = gi / columns; if (row >= rowsPerColumn[col]) continue;
                                var cell = grid[gi]; if (cell == null) continue;
                                if (cell.Matches(trigger)) totalMatchesAcrossLine++;
                            }
                            if (totalMatchesAcrossLine <= 0) totalMatchesAcrossLine = matched.Count;
                            scaled = (long)trigger.BaseValue * totalMatchesAcrossLine;
                            break;
                        default:
                            scaled = trigger.BaseValue; break;
                    }

                    long total = scaled * (i < winMultipliers.Count ? winMultipliers[i] : 1) * creditCost; if (total > int.MaxValue) total = int.MaxValue;
                    results.Add(new CoreWinData(i, (int)total, matched.ToArray()));
                }
            }

            // Symbol-level wins
            const int NonLine = -1; var processed = new HashSet<int>();
            for (int idx = 0; idx < grid.Length; idx++)
            {
                var cell = grid[idx]; if (cell == null) continue; if (cell.IsWild) continue;
                if (cell.WinMode == SymbolWinMode.SingleOnReel && cell.BaseValue > 0)
                {
                    long scaled = cell.BaseValue;
                    switch (cell.PayScaling)
                    {
                        case PayScaling.PerSymbol: scaled = cell.BaseValue * 1L; break;
                        default: scaled = cell.BaseValue; break;
                    }
                    long total = scaled * creditCost; if (total > int.MaxValue) total = int.MaxValue;
                    results.Add(new CoreWinData(NonLine, (int)total, new int[] { idx }));
                }

                if (cell.WinMode == SymbolWinMode.TotalCount)
                {
                    int groupId = cell.MatchGroupId;
                    if (groupId <= 0) continue;
                    if (processed.Contains(groupId)) continue;
                    processed.Add(groupId);
                    if (cell.TotalCountTrigger <= 0) continue;
                    if (cell.BaseValue <= 0) continue;

                    var matching = new List<int>(); int exactMatches = 0;
                    for (int j = 0; j < grid.Length; j++)
                    {
                        var other = grid[j]; if (other == null) continue; if (other.IsWild) continue;
                        if (other.MatchGroupId > 0 && other.MatchGroupId == groupId) { matching.Add(j); if (!string.IsNullOrEmpty(other.Name)) exactMatches++; }
                    }
                    if (exactMatches == 0) continue;
                    int count = matching.Count;
                    if (count >= cell.TotalCountTrigger)
                    {
                        int extra = count - cell.TotalCountTrigger; long scaled = cell.BaseValue;
                        switch (cell.PayScaling)
                        {
                            case PayScaling.DepthSquared: scaled = cell.BaseValue * (1L << extra); break;
                            case PayScaling.PerSymbol: scaled = (long)cell.BaseValue * count; break;
                            default: scaled = cell.BaseValue; break;
                        }
                        long total = scaled * creditCost; if (total > int.MaxValue) total = int.MaxValue;
                        results.Add(new CoreWinData(NonLine, (int)total, matching.ToArray()));
                    }
                }
            }

            return results;
        }
    }
}
