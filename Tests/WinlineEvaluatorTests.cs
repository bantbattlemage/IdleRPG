using NUnit.Framework;
using System.Collections.Generic;
using System.Linq;

namespace WinlineEvaluator.Tests
{
    public enum SymbolWinMode { LineMatch, SingleOnReel, TotalCount }
    public enum PayScaling { DepthSquared, PerSymbol }

    public class SymbolData
    {
        public string Name;
        public int BaseValue;
        public int MinWinDepth;
        public bool IsWild;
        public bool AllowWildMatch = true;
        public SymbolWinMode WinMode = SymbolWinMode.LineMatch;
        public int TotalCountTrigger = -1;
        public int MatchGroupId = 0;
        public PayScaling PayScaling = PayScaling.DepthSquared;
        public SymbolData(string name, int baseValue = 0, int minWinDepth = -1, bool isWild = false, SymbolWinMode winMode = SymbolWinMode.LineMatch, PayScaling scaling = PayScaling.DepthSquared, int totalCountTrigger = -1, int matchGroupId = 0, bool allowWild = true)
        {
            Name = name; BaseValue = baseValue; MinWinDepth = minWinDepth; IsWild = isWild; WinMode = winMode; PayScaling = scaling; TotalCountTrigger = totalCountTrigger; MatchGroupId = matchGroupId; AllowWildMatch = allowWild;
        }
        public bool Matches(SymbolData other)
        {
            if (other == null) return false;
            if (IsWild && other.IsWild) return true;
            if (MatchGroupId != 0 && other.MatchGroupId != 0 && MatchGroupId == other.MatchGroupId) return true;
            if (IsWild && other.AllowWildMatch) return true;
            if (other.IsWild && AllowWildMatch) return true;
            return Name == other.Name; // direct name match fallback (production uses Name through data objects)
        }
    }

    public class WinData { public int LineIndex; public int Value; public int[] Indexes; public WinData(int li, int val, int[] idx) { LineIndex = li; Value = val; Indexes = idx; } }

    // Minimal evaluator mirroring production WinEvaluator logic relevant to tests
    public class WinlineEvaluator
    {
        public List<WinData> EvaluateWins(SymbolData[] grid, int columns, int[] rowsPerColumn, List<int[]> winlines, List<int> winMultipliers, int creditCost = 1)
        {
            var results = new List<WinData>();
            if (grid == null || winlines == null) return results;
            for (int i = 0; i < winlines.Count; i++)
            {
                var pattern = winlines[i]; if (pattern == null || pattern.Length == 0) continue;
                int first = pattern[0]; if (first < 0 || first >= grid.Length) continue;
                var trigger = grid[first]; if (trigger == null) continue;
                bool needsFallback = (trigger.MinWinDepth < 0 || trigger.WinMode != SymbolWinMode.LineMatch) || (trigger.IsWild && trigger.BaseValue <= 0);
                if (needsFallback)
                {
                    if (trigger.IsWild)
                    {
                        int alt = -1;
                        for (int p = 1; p < pattern.Length; p++)
                        {
                            int idx = pattern[p]; if (idx < 0 || idx >= grid.Length) continue;
                            int col = idx % columns; int row = idx / columns; if (row >= rowsPerColumn[col]) continue;
                            var cand = grid[idx]; if (cand == null) continue;
                            if (!cand.IsWild && cand.MinWinDepth >= 0 && cand.BaseValue > 0 && cand.WinMode == SymbolWinMode.LineMatch) { alt = idx; trigger = cand; break; }
                        }
                        if (alt < 0) continue; // no fallback paying trigger
                    }
                    else continue;
                }
                if (trigger.WinMode != SymbolWinMode.LineMatch) continue;
                if (trigger.BaseValue <= 0) continue;
                var matched = new List<int>();
                for (int k = 0; k < pattern.Length; k++)
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
                    if (trigger.PayScaling == PayScaling.DepthSquared) scaled = trigger.BaseValue * (1L << extraDepth);
                    else if (trigger.PayScaling == PayScaling.PerSymbol)
                    {
                        // New behavior: count matching symbols across the entire evaluated winline pattern (pattern), not only the contiguous prefix
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
                    }
                    long total = scaled * (i < winMultipliers.Count ? winMultipliers[i] : 1) * creditCost; if (total > int.MaxValue) total = int.MaxValue;
                    results.Add(new WinData(i, (int)total, matched.ToArray()));
                }
            }
            // Symbol-level modes
            const int NonLine = -1; var totalProcessed = new HashSet<int>();
            for (int idx = 0; idx < grid.Length; idx++)
            {
                var cell = grid[idx]; if (cell == null) continue; if (cell.IsWild) continue;
                if (cell.WinMode == SymbolWinMode.SingleOnReel && cell.BaseValue > 0)
                {
                    long val = cell.BaseValue * creditCost; if (val > int.MaxValue) val = int.MaxValue;
                    results.Add(new WinData(NonLine, (int)val, new int[] { idx }));
                }
                if (cell.WinMode == SymbolWinMode.TotalCount && cell.BaseValue > 0 && cell.TotalCountTrigger > 0)
                {
                    if (totalProcessed.Contains(cell.MatchGroupId)) continue; totalProcessed.Add(cell.MatchGroupId);
                    var memberIndexes = new List<int>();
                    for (int j = 0; j < grid.Length; j++)
                    {
                        var other = grid[j]; if (other == null || other.IsWild) continue;
                        if (other.MatchGroupId != 0 && other.MatchGroupId == cell.MatchGroupId) memberIndexes.Add(j);
                    }
                    int count = memberIndexes.Count;
                    if (count >= cell.TotalCountTrigger)
                    {
                        int extra = count - cell.TotalCountTrigger; long scaled = cell.BaseValue;
                        if (cell.PayScaling == PayScaling.DepthSquared) scaled = cell.BaseValue * (1L << extra);
                        else if (cell.PayScaling == PayScaling.PerSymbol) scaled = (long)cell.BaseValue * count;
                        long total = scaled * creditCost; if (total > int.MaxValue) total = int.MaxValue;
                        results.Add(new WinData(NonLine, (int)total, memberIndexes.ToArray()));
                    }
                }
            }
            return results;
        }
    }

    public class UpdatedTests
    {
        private SymbolData WildNP() => new SymbolData("W", 0, -1, true); // non-paying wild
        private SymbolData WildPay() => new SymbolData("WP", 5, 3, true, SymbolWinMode.LineMatch, PayScaling.PerSymbol); // paying wild
        private SymbolData SymA() => new SymbolData("A", 2, 3, false, SymbolWinMode.LineMatch, PayScaling.DepthSquared);
        private SymbolData SymB() => new SymbolData("B", 4, 3, false, SymbolWinMode.LineMatch, PayScaling.PerSymbol);
        private SymbolData SymCount() => new SymbolData("CNT", 3, -1, false, SymbolWinMode.TotalCount, PayScaling.PerSymbol, totalCountTrigger: 3, matchGroupId: 9);
        private SymbolData SymSingle() => new SymbolData("S", 5, -1, false, SymbolWinMode.SingleOnReel, PayScaling.PerSymbol);

        [Test]
        public void DepthSquaredScaling_Works()
        {
            var a = SymA();
            var grid = new[] { a, a, a, a, a }; // 5 matches, min=3 => extraDepth=2 => base 2 * 2^2 = 8
            var winlines = new List<int[]> { new int[] {0,1,2,3,4} };
            var mults = new List<int> { 1 };
            var ev = new WinlineEvaluator();
            var wins = ev.EvaluateWins(grid, 5, new[] {1,1,1,1,1}, winlines, mults);
            Assert.AreEqual(1, wins.Count);
            Assert.AreEqual(8, wins[0].Value);
        }

        [Test]
        public void PerSymbolScaling_Works()
        {
            var b = SymB(); b.MinWinDepth = 1; // allow immediate win
            var grid = new[] { b, b, b, b }; // count=4 => base 4 * 4 =16
            var winlines = new List<int[]> { new int[] {0,1,2,3} }; var mults = new List<int> {1};
            var ev = new WinlineEvaluator(); var wins = ev.EvaluateWins(grid,4,new[]{1,1,1,1},winlines,mults);
            Assert.AreEqual(1, wins.Count); Assert.AreEqual(16, wins[0].Value);
        }

        [Test]
        public void PerSymbol_IncludesLaterMatchesAcrossLine()
        {
            // New behavior: PerSymbol payout counts all matches across the winline pattern, even if a gap exists in the contiguous prefix.
            var a = new SymbolData("A", 10, 1, false, SymbolWinMode.LineMatch, PayScaling.PerSymbol);
            var b = new SymbolData("B", 1, -1, false);
            var grid = new[] { a, b, a };
            var winlines = new List<int[]> { new int[] {0,1,2} };
            var mults = new List<int> {1};
            var ev = new WinlineEvaluator();
            var wins = ev.EvaluateWins(grid,3,new[]{1,1,1},winlines,mults);
            // trigger MinWinDepth=1 satisfied by contiguous prefix (only index 0), but payout should count both index 0 and 2 => 2 * base 10 = 20
            Assert.AreEqual(1, wins.Count);
            Assert.AreEqual(20, wins[0].Value);
        }

        [Test]
        public void PerSymbol_StillRequiresContiguousTriggerDepth()
        {
            // Ensure trigger requirement unchanged: contiguous prefix must meet MinWinDepth even if total matches across line reach the threshold.
            var a = new SymbolData("A", 5, 3, false, SymbolWinMode.LineMatch, PayScaling.PerSymbol); // MinWinDepth = 3
            var b = new SymbolData("B", 1, -1, false);
            // Pattern positions 0,1,2,3 -> matches at 0,2,3 (total 3) but contiguous prefix only 1 -> should NOT award
            var grid = new[] { a, b, a, a };
            var winlines = new List<int[]> { new int[] {0,1,2,3} };
            var mults = new List<int> {1};
            var ev = new WinlineEvaluator();
            var wins = ev.EvaluateWins(grid,4,new[]{1,1,1,1},winlines,mults);
            Assert.AreEqual(0, wins.Count);
        }

        [Test]
        public void WildFallback_W2W_LineAwards()
        {
            // Pattern W (non-paying wild), B, W -> should fallback to B trigger then match all 3 for MinWinDepth=3
            var w = WildNP(); var b = SymB(); b.MinWinDepth = 3; var grid = new[] { w, b, w };
            var winlines = new List<int[]> { new int[] {0,1,2} }; var mults = new List<int> {1};
            var ev = new WinlineEvaluator(); var wins = ev.EvaluateWins(grid,3,new[]{1,1,1},winlines,mults);
            Assert.AreEqual(1, wins.Count); // PerSymbol scaling: base 4 * 3 symbols = 12
            Assert.AreEqual(12, wins[0].Value);
        }

        [Test]
        public void WildFallback_NoPayingTrigger_NoWin()
        {
            var w = WildNP(); var grid = new[] { w, w, w };
            var winlines = new List<int[]> { new int[] {0,1,2} }; var mults = new List<int> {1};
            var ev = new WinlineEvaluator(); var wins = ev.EvaluateWins(grid,3,new[]{1,1,1},winlines,mults);
            Assert.AreEqual(0, wins.Count);
        }

        [Test]
        public void PayingWild_AsTrigger_PerSymbolScaling()
        {
            var wp = WildPay(); // MinWinDepth=3, base 5, PerSymbol
            var grid = new[] { wp, wp, wp, wp }; // 4 matches => base 5 * 4 = 20
            var winlines = new List<int[]> { new int[] {0,1,2,3} }; var mults = new List<int> {1};
            var ev = new WinlineEvaluator(); var wins = ev.EvaluateWins(grid,4,new[]{1,1,1,1},winlines,mults);
            Assert.AreEqual(1, wins.Count); Assert.AreEqual(20, wins[0].Value);
        }

        [Test]
        public void TruncatedColumn_StopsMatch()
        {
            var a = SymA(); var grid = new[] { a, a, a }; // third position will be truncated logically
            var winlines = new List<int[]> { new int[] {0,1,2} }; var rows = new[] {1,0,1}; // column1 has 0 rows => match stops at idx1
            var ev = new WinlineEvaluator(); var wins = ev.EvaluateWins(grid,3,rows,winlines,new List<int>{1});
            // Only first cell valid, insufficient depth
            Assert.AreEqual(0, wins.Count);
        }

        [Test]
        public void OverlappingLines_BothAward()
        {
            var a = SymA(); a.MinWinDepth = 3;
            // Grid (row0): indexes 0..4 all A
            var grid = new[] { a,a,a,a,a };
            var line1 = new int[] {0,1,2,3,4};
            var line2 = new int[] {0,1,2};
            var wins = new WinlineEvaluator().EvaluateWins(grid,5,new[]{1,1,1,1,1}, new List<int[]>{line1,line2}, new List<int>{1,1});
            Assert.AreEqual(2, wins.Count);
            // line1: 5 matches -> extraDepth=2 => 2*2^2=8; line2: 3 matches -> extraDepth=0 => 2
            Assert.IsTrue(wins.Any(w=>w.LineIndex==0 && w.Value==8));
            Assert.IsTrue(wins.Any(w=>w.LineIndex==1 && w.Value==2));
        }

        [Test]
        public void SingleOnReel_AwardsEachInstance()
        {
            var s = SymSingle(); var grid = new[] { s,s,s };
            var wins = new WinlineEvaluator().EvaluateWins(grid,3,new[]{1,1,1}, new List<int[]>(), new List<int>());
            Assert.AreEqual(3, wins.Count);
            Assert.AreEqual(15, wins.Sum(x=>x.Value));
        }

        [Test]
        public void TotalCount_IgnoresWilds_GroupAwards()
        {
            var cnt = SymCount(); var w = WildNP();
            var grid = new[] { cnt, cnt, w, cnt }; // wild ignored -> 3 matching (threshold 3)
            var wins = new WinlineEvaluator().EvaluateWins(grid,4,new[]{1,1,1,1}, new List<int[]>(), new List<int>());
            Assert.AreEqual(1, wins.Count);
            // PerSymbol scaling: base 3 * count(3) =9
            Assert.AreEqual(9, wins[0].Value);
        }

        [Test]
        public void Wilds_DoNotTrigger_TotalCount_OnTheirOwn()
        {
            var wildCount = new SymbolData("W", baseValue:5, minWinDepth:-1, isWild:true, winMode: SymbolWinMode.TotalCount, scaling: PayScaling.PerSymbol, totalCountTrigger:1, matchGroupId:7);
            var grid = new[] { wildCount, wildCount };
            var wins = new WinlineEvaluator().EvaluateWins(grid,2,new[]{1,1}, new List<int[]>(), new List<int>());
            Assert.AreEqual(0, wins.Count);
        }

        [Test]
        public void PerSymbol_CountsWildMatchesAcrossLine()
        {
            // PerSymbol payout should count wilds that are permitted to match the trigger
            var a = new SymbolData("A", 2, 1, false, SymbolWinMode.LineMatch, PayScaling.PerSymbol);
            var w = new SymbolData("W", 0, -1, true, SymbolWinMode.LineMatch, PayScaling.PerSymbol, totalCountTrigger: -1, matchGroupId: 0, allowWild: true);
            var grid = new[] { a, w, a };
            var winlines = new List<int[]> { new int[] { 0, 1, 2 } };
            var mults = new List<int> { 1 };
            var ev = new WinlineEvaluator();
            var wins = ev.EvaluateWins(grid, 3, new[] { 1, 1, 1 }, winlines, mults);
            // trigger contiguous prefix satisfied by index 0 => MinWinDepth=1, payout counts wild at idx1 -> totalMatches=3 => 3 * base 2 = 6
            Assert.AreEqual(1, wins.Count);
            Assert.AreEqual(6, wins[0].Value);
        }

        [Test]
        public void TotalCount_DepthSquared_Works_InWinlineEvaluatorTests()
        {
            var cnt = new SymbolData("CNT", 3, -1, false, SymbolWinMode.TotalCount, PayScaling.DepthSquared, totalCountTrigger: 2, matchGroupId: 11);
            var grid = new[] { cnt, cnt, cnt };
            var ev = new WinlineEvaluator();
            var wins = ev.EvaluateWins(grid, 3, new[] { 1, 1, 1 }, new List<int[]>(), new List<int>());
            Assert.AreEqual(1, wins.Count);
            // count=3 trigger=2 extra=1 => scaled = base 3 * 2^1 = 6
            Assert.AreEqual(6, wins[0].Value);
        }
    }
}
