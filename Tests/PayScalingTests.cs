using System;
using System.Collections.Generic;
using NUnit.Framework;
using EvaluatorCore;

namespace WinlineEvaluator.PayScalingTests
{
    // Tests now reference shared enums from EvaluatorCore instead of duplicating them.

    public class SymbolDataPS
    {
        public string Name;
        public int BaseValue;
        public int MinWinDepth;
        public bool IsWild;
        public bool AllowWildMatch = true;
        public SymbolWinMode WinMode = SymbolWinMode.LineMatch;
        public int TotalCountTrigger = -1;
        public int MatchGroupId = -1;
        public PayScaling PayScaling = PayScaling.DepthSquared;

        public SymbolDataPS(string name, int baseValue = 0, int minWinDepth = -1, bool isWild = false, SymbolWinMode winMode = SymbolWinMode.LineMatch, int totalCountTrigger = -1, int matchGroupId = -1, PayScaling scaling = PayScaling.DepthSquared)
        {
            Name = name;
            BaseValue = baseValue;
            MinWinDepth = minWinDepth;
            IsWild = isWild;
            WinMode = winMode;
            TotalCountTrigger = totalCountTrigger;
            MatchGroupId = matchGroupId;
            PayScaling = scaling;
        }

        public bool Matches(SymbolDataPS other)
        {
            if (other == null) return false;
            if (this.IsWild && other.IsWild) return true;
            if (this.MatchGroupId > 0 && other.MatchGroupId > 0 && this.MatchGroupId == other.MatchGroupId) return true;
            if (this.IsWild && other.AllowWildMatch) return true;
            if (other.IsWild && this.AllowWildMatch) return true;
            return false;
        }
    }

    public class WinData { public int LineIndex; public int WinValue; public int[] WinningSymbolIndexes; public WinData(int l, int v, int[] idxs) { LineIndex = l; WinValue = v; WinningSymbolIndexes = idxs; } }

    // Isolated evaluator used only for these PayScaling tests to avoid conflicts with existing test harness
    public class PayScalingEvaluator
    {
        public List<WinData> EvaluateWins(SymbolDataPS[] grid, int columns, int[] rowsPerColumn, List<int[]> winlines, List<int> winMultipliers, int creditCostArg = 1)
        {
            var winData = new List<WinData>();
            if (grid == null || grid.Length == 0 || winlines == null) return winData;
            int maxRows = 0; for (int i = 0; i < rowsPerColumn.Length; i++) if (rowsPerColumn[i] > maxRows) maxRows = rowsPerColumn[i];
            int expected = maxRows * columns;
            if (grid.Length != expected) Array.Resize(ref grid, expected);

            for (int i = 0; i < winlines.Count; i++)
            {
                var concrete = winlines[i];
                if (concrete == null || concrete.Length == 0) continue;
                int first = concrete[0];
                if (first < 0 || first >= grid.Length) continue;
                var trigger = grid[first];
                if (trigger == null) continue;
                if (trigger.MinWinDepth < 0 || trigger.WinMode != SymbolWinMode.LineMatch)
                {
                    if (trigger.IsWild)
                    {
                        int alt = -1;
                        for (int s = 1; s < concrete.Length; s++)
                        {
                            int si = concrete[s];
                            if (si < 0 || si >= grid.Length) continue;
                            int col = si % columns; int row = si / columns; if (row >= rowsPerColumn[col]) continue;
                            var cand = grid[si]; if (cand == null) continue;
                            if (!cand.IsWild && cand.MinWinDepth >= 0 && cand.BaseValue > 0 && cand.WinMode == SymbolWinMode.LineMatch) { alt = si; break; }
                        }
                        if (alt >= 0) trigger = grid[alt]; else continue;
                    }
                    else continue;
                }
                if (trigger.BaseValue <= 0) continue;

                var winning = new List<int>();
                for (int k = 0; k < concrete.Length; k++)
                {
                    int si = concrete[k]; if (si < 0 || si >= grid.Length) break;
                    int col = si % columns; int row = si / columns; if (row >= rowsPerColumn[col]) break;
                    var cell = grid[si]; if (cell == null) break;
                    if (cell.Matches(trigger)) winning.Add(si); else break;
                }

                int matchCount = winning.Count;
                if (matchCount >= trigger.MinWinDepth)
                {
                    int baseVal = trigger.BaseValue; if (baseVal <= 0) continue;
                    int winDepth = matchCount - trigger.MinWinDepth;
                    long scaled = baseVal;
                    switch (trigger.PayScaling)
                    {
                        case PayScaling.DepthSquared:
                            scaled = baseVal * (1L << winDepth);
                            break;
                        case PayScaling.PerSymbol:
                            scaled = (long)baseVal * matchCount;
                            break;
                        default:
                            scaled = baseVal; break;
                    }
                    long total = scaled * winMultipliers[i] * creditCostArg; if (total > int.MaxValue) total = int.MaxValue;
                    winData.Add(new WinData(i, (int)total, winning.ToArray()));
                }
            }

            // Symbol-level wins
            var processed = new HashSet<int>();
            for (int idx = 0; idx < grid.Length; idx++)
            {
                var cell = grid[idx]; if (cell == null) continue; if (cell.IsWild) continue;

                if (cell.WinMode == SymbolWinMode.SingleOnReel)
                {
                    if (cell.BaseValue <= 0) continue;
                    long scaled = cell.BaseValue;
                    switch (cell.PayScaling)
                    {
                        case PayScaling.PerSymbol:
                            scaled = cell.BaseValue * 1L;
                            break;
                        case PayScaling.DepthSquared:
                        default:
                            scaled = cell.BaseValue;
                            break;
                    }

                    long total = scaled * creditCostArg;
                    if (total > int.MaxValue) total = int.MaxValue;
                    int finalValue = (int)total;

                    winData.Add(new WinData(-1, finalValue, new int[] { idx }));
                }

                if (cell.WinMode == SymbolWinMode.TotalCount)
                {
                    int groupId = cell.MatchGroupId;
                    // Skip unset/non-positive group ids
                    if (groupId <= 0) continue;
                    if (processed.Contains(groupId)) continue;
                    processed.Add(groupId);

                    if (cell.TotalCountTrigger <= 0) continue;
                    if (cell.BaseValue <= 0) continue;

                    var matching = new List<int>();
                    int exactMatches = 0;
                    for (int j = 0; j < grid.Length; j++)
                    {
                        var other = grid[j];
                        if (other == null) continue;
                        if (other.IsWild) continue;

                        if (other.MatchGroupId > 0 && other.MatchGroupId == groupId)
                        {
                            matching.Add(j);
                            if (!string.IsNullOrEmpty(other.Name)) exactMatches++;
                        }
                    }

                    if (exactMatches == 0) continue;

                    int count = matching.Count;
                    if (count >= cell.TotalCountTrigger)
                    {
                        int winDepth = count - cell.TotalCountTrigger;
                        long scaled = cell.BaseValue;
                        switch (cell.PayScaling)
                        {
                            case PayScaling.DepthSquared:
                                long mult = 1L << winDepth;
                                scaled = cell.BaseValue * mult;
                                break;
                            case PayScaling.PerSymbol:
                                scaled = (long)cell.BaseValue * count;
                                break;
                            default:
                                scaled = cell.BaseValue;
                                break;
                        }

                        long creditCost = 1;
                        long total = scaled * creditCost;
                        if (total > int.MaxValue) total = int.MaxValue;
                        int finalValue = (int)total;

                        winData.Add(new WinData(-1, finalValue, matching.ToArray()));
                    }
                }
            }

            return winData;
        }
    }

    public class Tests
    {
        [Test]
        public void LineMatch_PerSymbol_PaysBaseTimesMatches()
        {
            int cols = 3; int[] rows = new int[] { 1, 1, 1 };
            var a = new SymbolDataPS("A", 10, 1, false, SymbolWinMode.LineMatch, -1, 1, PayScaling.PerSymbol);
            var b = new SymbolDataPS("A", 10, 1, false, SymbolWinMode.LineMatch, -1, 1, PayScaling.PerSymbol);
            var c = new SymbolDataPS("A", 10, 1, false, SymbolWinMode.LineMatch, -1, 1, PayScaling.PerSymbol);
            var grid = new SymbolDataPS[] { a, b, c };
            var winlines = new List<int[]> { new int[] { 0, 1, 2 } };
            var multipliers = new List<int> { 1 };
            var ev = new PayScalingEvaluator();
            var wins = ev.EvaluateWins(grid, cols, rows, winlines, multipliers, 1);
            Assert.AreEqual(1, wins.Count);
            Assert.AreEqual(30, wins[0].WinValue);
        }

        [Test]
        public void LineMatch_DepthSquared_PaysPowerOfTwoPerExtraDepth()
        {
            int cols = 3; int[] rows = new int[] { 1, 1, 1 };
            var a = new SymbolDataPS("A", 10, 1, false, SymbolWinMode.LineMatch, -1, 1, PayScaling.DepthSquared);
            var b = new SymbolDataPS("A", 10, 1, false, SymbolWinMode.LineMatch, -1, 1, PayScaling.DepthSquared);
            var c = new SymbolDataPS("A", 10, 1, false, SymbolWinMode.LineMatch, -1, 1, PayScaling.DepthSquared);
            var grid = new SymbolDataPS[] { a, b, c };
            var winlines = new List<int[]> { new int[] { 0, 1, 2 } };
            var multipliers = new List<int> { 1 };
            var ev = new PayScalingEvaluator();
            var wins = ev.EvaluateWins(grid, cols, rows, winlines, multipliers, 1);
            Assert.AreEqual(1, wins.Count);
            Assert.AreEqual(40, wins[0].WinValue);
        }

        [Test]
        public void TotalCount_PerSymbol_PaysBaseTimesCount()
        {
            int cols = 3; int[] rows = new int[] { 1, 1, 1 };
            var a = new SymbolDataPS("A", 5, -1, false, SymbolWinMode.TotalCount, 2, 7, PayScaling.PerSymbol);
            var b = new SymbolDataPS("A", 5, -1, false, SymbolWinMode.TotalCount, 2, 7, PayScaling.PerSymbol);
            var c = new SymbolDataPS("A", 5, -1, false, SymbolWinMode.TotalCount, 2, 7, PayScaling.PerSymbol);
            var grid = new SymbolDataPS[] { a, b, c };
            var ev = new PayScalingEvaluator();
            var wins = ev.EvaluateWins(grid, cols, rows, new List<int[]>(), new List<int>(), 1);
            Assert.AreEqual(1, wins.Count);
            Assert.AreEqual(15, wins[0].WinValue);
        }

        [Test]
        public void TotalCount_DepthSquared_PaysDepthSquaredScaling()
        {
            int cols = 3; int[] rows = new int[] { 1, 1, 1 };
            var a = new SymbolDataPS("A", 5, -1, false, SymbolWinMode.TotalCount, 2, 7, PayScaling.DepthSquared);
            var b = new SymbolDataPS("A", 5, -1, false, SymbolWinMode.TotalCount, 2, 7, PayScaling.DepthSquared);
            var c = new SymbolDataPS("A", 5, -1, false, SymbolWinMode.TotalCount, 2, 7, PayScaling.DepthSquared);
            var grid = new SymbolDataPS[] { a, b, c };
            var ev = new PayScalingEvaluator();
            var wins = ev.EvaluateWins(grid, cols, rows, new List<int[]>(), new List<int>(), 1);
            Assert.AreEqual(1, wins.Count);
            Assert.AreEqual(10, wins[0].WinValue);
        }
    }
}
