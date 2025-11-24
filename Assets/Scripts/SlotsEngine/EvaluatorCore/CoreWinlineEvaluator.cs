using System;
using System.Collections.Generic;
using System.Linq;
using EvaluatorCore;

namespace EvaluatorCore
{
	public class CoreWinData { public int LineIndex; public int WinValue; public int[] WinningSymbolIndexes; public CoreWinData(int l, int v, int[] idxs) { LineIndex = l; WinValue = v; WinningSymbolIndexes = idxs; } }

    public static class CoreWinlineEvaluator
    {
        /*
         * Helper semantics (centralized):
         * - IsPayingLineTrigger(s): true when a symbol is a valid paying trigger for line wins.
         *   (WinMode == LineMatch, MinWinDepth >= 0, BaseValue > 0)
         * - IsNonPayingWild(s): true when a symbol is a wild but is NOT a paying LineMatch trigger.
         *   Non-paying wilds act as match extenders and may participate in a narrow "wild-only" fallback.
         * - PatternPosValid(idx,...): verifies that a pattern index maps inside the grid bounds, the
         *   row exists for the column (jagged column support), and the grid cell is non-null.
         *
         * Overall evaluation high-level rules (intended and enforced):
         * 1) Anchoring: if the leftmost pattern slot (pattern[0]) maps to a valid, in-bounds, non-null cell,
         *    the line is anchored to that leftmost column and matching/trigger selection must respect that anchor.
         * 2) Wild-only fallback: If anchored and the anchored leftmost cell is a non-paying wild (IsNonPayingWild),
         *    the evaluator will perform a "wild-only" fallback: search forward from the anchored slot to find the
         *    first PAYING LineMatch trigger (IsPayingLineTrigger). That paying symbol becomes the chosen trigger,
         *    but the pattern remains anchored to pattern[0] so leading wilds may extend the chosen trigger.
         * 3) No permissive fallback when pattern[0] is missing: If pattern[0] is invalid/missing (e.g. -1 or maps
         *    outside rows), the pattern is NOT considered anchored and -- to avoid ambiguity across reels -- the
         *    evaluator will SKIP the pattern (no fallback to later reels). This enforces that wins only originate
         *    from the owning slot's leftmost reel when an explicit anchor exists.
         * 4) When pattern[0] is invalid (and we allowed permissive behavior previously), the earlier design allowed
         *    searching from the first usable position for paying triggers. That permissive behavior is now disabled
         *    by (3) to satisfy strict slot-leftmost semantics. Only the wild-only fallback when anchor exists is allowed.
         * 5) Blocking rule: if any earlier usable pattern position before the first usable position could itself be a
         *    paying trigger, the pattern is skipped so we don't prefer a later trigger over an earlier paying candidate.
         * 6) Matching: once a trigger is chosen the evaluator computes a contiguous match starting at the earliest
         *    matching slot between the anchored/usable leftmost and the chosen trigger. Matching stops on invalid
         *    pattern entries (-1), out-of-bounds rows, or null cells. That means a null/invalid entry acts as a hard break.
         * 7) Pay scaling: DepthSquared and PerSymbol behaviors are applied as before. PerSymbol counts all matches across
         *    the pattern that Match the chosen trigger symbol (including wilds that match) and uses that to scale pay.
         *
         * Note: these rules were intentionally chosen to guarantee anchor semantics while allowing non-paying wilds to
         * extend a later paying trigger in a narrow, predictable way. The helper functions below consolidate the
         * predicates so future changes won't accidentally diverge behaviors in multiple places.
         */

        // Helper: true when symbol is a paying LineMatch trigger (can start a line win)
        private static bool IsPayingLineTrigger(PlainSymbolData s)
        {
            return s != null && s.WinMode == SymbolWinMode.LineMatch && s.MinWinDepth >= 0 && s.BaseValue > 0;
        }

        // Helper: true when symbol is a wild but not a paying LineMatch trigger.
        private static bool IsNonPayingWild(PlainSymbolData s)
        {
            return s != null && s.IsWild && !IsPayingLineTrigger(s);
        }

        // Helper: validate that a pattern position maps to a usable grid cell
        private static bool PatternPosValid(int candidate, PlainSymbolData[] grid, int columns, int[] rowsPerColumn)
        {
            if (candidate < 0 || candidate >= grid.Length) return false;
            int col = candidate % columns; int row = candidate / columns; if (row >= rowsPerColumn[col]) return false;
            return grid[candidate] != null;
        }

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

                int pStart = -1;
                for (int pi = 0; pi < pattern.Length; pi++)
                {
                    int candidate = pattern[pi];
                    if (!PatternPosValid(candidate, grid, columns, rowsPerColumn)) continue;
                    var candCell = grid[candidate];
                    pStart = pi; break;
                }
                if (pStart < 0) continue;

                bool pattern0Valid = (pattern.Length > 0) && PatternPosValid(pattern[0], grid, columns, rowsPerColumn);
                if (!pattern0Valid) continue;

                int firstUsable = pStart; int anchoredStart = 0;

                int first = pattern[anchoredStart];
                var trigger = (first >= 0 && first < grid.Length) ? grid[first] : null;
                if (trigger == null) continue;

                int chosenTriggerPatternPos = anchoredStart;

                if (IsNonPayingWild(trigger))
                {
                    int altPos = -1;
                    for (int p = anchoredStart + 1; p < pattern.Length; p++)
                    {
                        int idx = pattern[p]; if (idx < 0 || idx >= grid.Length) continue;
                        int col = idx % columns; int row = idx / columns; if (row >= rowsPerColumn[col]) continue;
                        var cand = grid[idx]; if (cand == null) continue;
                        if (IsPayingLineTrigger(cand)) { altPos = p; break; }
                    }
                    if (altPos < 0) continue;

                    chosenTriggerPatternPos = altPos;
                    first = pattern[chosenTriggerPatternPos];
                    trigger = grid[first];
                    if (trigger == null) continue;
                }
                else
                {
                    if (!IsPayingLineTrigger(trigger)) continue;
                }

                bool earlierValidExists = false;
                for (int pj = 0; pj < firstUsable; pj++)
                {
                    int candidate = pattern[pj];
                    if (candidate < 0 || candidate >= grid.Length) continue;
                    int col = candidate % columns; int row = candidate / columns; if (row >= rowsPerColumn[col]) continue;
                    var candCell = grid[candidate]; if (candCell == null) continue;
                    if (IsPayingLineTrigger(candCell)) { earlierValidExists = true; break; }
                }
                if (earlierValidExists) continue;

                var matched = new List<int>();
                int matchStart = chosenTriggerPatternPos;
                if (chosenTriggerPatternPos > firstUsable)
                {
                    for (int kk = firstUsable; kk < chosenTriggerPatternPos; kk++)
                    {
                        int gi = pattern[kk]; if (gi < 0 || gi >= grid.Length) continue;
                        int col = gi % columns; int row = gi / columns; if (row >= rowsPerColumn[col]) continue;
                        var cell = grid[gi]; if (cell == null) continue;
                        if (cell.Matches(trigger)) { matchStart = kk; break; }
                    }
                }

                for (int k = matchStart; k < pattern.Length; k++)
                {
                    int gi = pattern[k]; if (gi < 0 || gi >= grid.Length) break;
                    int col = gi % columns; int row = gi / columns; if (row >= rowsPerColumn[col]) break;
                    var cell = grid[gi]; if (cell == null) break;
                    if (cell.Matches(trigger)) matched.Add(gi); else break;
                }

                if (matched.Count > 0)
                {
                    var posMap = new Dictionary<int, int>();
                    for (int pi = 0; pi < pattern.Length; pi++) if (pattern[pi] >= 0) posMap[pattern[pi]] = pi;
                    var positions = matched.Select(mi => posMap.ContainsKey(mi) ? posMap[mi] : -1000).ToList();
                    int triggerPos = chosenTriggerPatternPos;
                    int idxInMatched = positions.FindIndex(p => p == triggerPos);
                    if (idxInMatched >= 0)
                    {
                        int startIdx = idxInMatched; int endIdx = idxInMatched;
                        while (startIdx - 1 >= 0 && positions[startIdx - 1] == positions[startIdx] - 1) startIdx--;
                        while (endIdx + 1 < positions.Count && positions[endIdx + 1] == positions[endIdx] + 1) endIdx++;
                        if (startIdx != 0 || endIdx != positions.Count - 1)
                        {
                            matched = matched.GetRange(startIdx, endIdx - startIdx + 1);
                        }
                    }
                    else
                    {
                        matched.Clear();
                    }
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
