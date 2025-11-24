using NUnit.Framework;
using System.Collections.Generic;
using System.Linq;
using EvaluatorCore;

namespace WinlineEvaluator.Tests
{
    // Test-local lightweight types retained for clarity, but the evaluator logic itself will be the one in EvaluatorCore.
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
        public int MatchGroupId = -1;
        public PayScaling PayScaling = PayScaling.DepthSquared;
        public SymbolData(string name, int baseValue = 0, int minWinDepth = -1, bool isWild = false, SymbolWinMode winMode = SymbolWinMode.LineMatch, PayScaling scaling = PayScaling.DepthSquared, int totalCountTrigger = -1, int matchGroupId = -1, bool allowWild = true)
        {
            Name = name; BaseValue = baseValue; MinWinDepth = minWinDepth; IsWild = isWild; WinMode = winMode; PayScaling = scaling; TotalCountTrigger = totalCountTrigger; MatchGroupId = matchGroupId; AllowWildMatch = allowWild;
        }
        public bool Matches(SymbolData other)
        {
            if (other == null) return false;
            if (IsWild && other.IsWild) return true;
            if (MatchGroupId > 0 && other.MatchGroupId > 0 && MatchGroupId == other.MatchGroupId) return true;
            if (IsWild && other.AllowWildMatch) return true;
            if (other.IsWild && AllowWildMatch) return true;
            return Name == other.Name; // direct name match fallback (production uses Name through data objects)
        }
    }

    public class WinData { public int LineIndex; public int Value; public int[] Indexes; public WinData(int li, int val, int[] idx) { LineIndex = li; Value = val; Indexes = idx; } }

    // Adapter evaluator used by tests - delegates to shared CoreWinlineEvaluator
    public class WinlineEvaluator
    {
        public List<WinData> EvaluateWins(SymbolData[] grid, int columns, int[] rowsPerColumn, List<int[]> winlines, List<int> winMultipliers, int creditCost = 1)
        {
            var results = new List<WinData>();
            if (grid == null || winlines == null) return results;

            // Convert local SymbolData to PlainSymbolData used by core evaluator
            var plain = new EvaluatorCore.PlainSymbolData[grid.Length];
            for (int i = 0; i < grid.Length; i++)
            {
                var s = grid[i]; if (s == null) { plain[i] = null; continue; }
                // Map enums to core enums (names match) by integer cast
                plain[i] = new EvaluatorCore.PlainSymbolData(s.Name, s.BaseValue, s.MinWinDepth, s.IsWild, (EvaluatorCore.SymbolWinMode)(int)s.WinMode, (EvaluatorCore.PayScaling)(int)s.PayScaling, s.TotalCountTrigger, s.MatchGroupId, s.AllowWildMatch);
            }

            var coreResults = EvaluatorCore.CoreWinlineEvaluator.EvaluateWins(plain, columns, rowsPerColumn, winlines, winMultipliers, creditCost);

            foreach (var c in coreResults)
            {
                results.Add(new WinData(c.LineIndex, c.WinValue, c.WinningSymbolIndexes));
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
            var a = new SymbolData("A", 10, 1, false, SymbolWinMode.LineMatch, PayScaling.PerSymbol);
            var b = new SymbolData("B", 1, -1, false);
            var grid = new[] { a, b, a };
            var winlines = new List<int[]> { new int[] {0,1,2} };
            var mults = new List<int> {1};
            var ev = new WinlineEvaluator();
            var wins = ev.EvaluateWins(grid,3,new[]{1,1,1},winlines,mults);
            Assert.AreEqual(1, wins.Count);
            Assert.AreEqual(20, wins[0].Value);
        }

        [Test]
        public void PerSymbol_StillRequiresContiguousTriggerDepth()
        {
            var a = new SymbolData("A", 5, 3, false, SymbolWinMode.LineMatch, PayScaling.PerSymbol);
            var b = new SymbolData("B", 1, -1, false);
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
            var w = WildNP(); var b = SymB(); b.MinWinDepth = 3; var grid = new[] { w, b, w };
            var winlines = new List<int[]> { new int[] {0,1,2} }; var mults = new List<int> {1};
            var ev = new WinlineEvaluator(); var wins = ev.EvaluateWins(grid,3,new[]{1,1,1},winlines,mults);
            Assert.AreEqual(1, wins.Count);
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
            var wp = WildPay();
            var grid = new[] { wp, wp, wp, wp };
            var winlines = new List<int[]> { new int[] {0,1,2,3} }; var mults = new List<int> {1};
            var ev = new WinlineEvaluator(); var wins = ev.EvaluateWins(grid,4,new[]{1,1,1,1},winlines,mults);
            Assert.AreEqual(1, wins.Count); Assert.AreEqual(20, wins[0].Value);
        }

        [Test]
        public void TruncatedColumn_StopsMatch()
        {
            var a = SymA(); var grid = new[] { a, a, a };
            var winlines = new List<int[]> { new int[] {0,1,2} }; var rows = new[] {1,0,1};
            var ev = new WinlineEvaluator(); var wins = ev.EvaluateWins(grid,3,rows,winlines,new List<int>{1});
            Assert.AreEqual(0, wins.Count);
        }

        [Test]
        public void OverlappingLines_BothAward()
        {
            var a = SymA(); a.MinWinDepth = 3;
            var grid = new[] { a,a,a,a,a };
            var line1 = new int[] {0,1,2,3,4};
            var line2 = new int[] {0,1,2};
            var wins = new WinlineEvaluator().EvaluateWins(grid,5,new[]{1,1,1,1,1}, new List<int[]>{line1,line2}, new List<int>{1,1});
            Assert.AreEqual(2, wins.Count);
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
            var grid = new[] { cnt, cnt, w, cnt };
            var wins = new WinlineEvaluator().EvaluateWins(grid,4,new[]{1,1,1,1}, new List<int[]>(), new List<int>());
            Assert.AreEqual(1, wins.Count);
            Assert.AreEqual(9, wins[0].Value);
        }

        [Test]
        public void Wilds_DoNotTrigger_TotalCount_OnTheir_Own()
        {
            var wildCount = new SymbolData("W", baseValue:5, minWinDepth:-1, isWild:true, winMode: SymbolWinMode.TotalCount, scaling: PayScaling.PerSymbol, totalCountTrigger:1, matchGroupId:7);
            var grid = new[] { wildCount, wildCount };
            var wins = new WinlineEvaluator().EvaluateWins(grid,2,new[]{1,1}, new List<int[]>(), new List<int>());
            Assert.AreEqual(0, wins.Count);
        }

        [Test]
        public void PerSymbol_CountsWildMatchesAcrossLine()
        {
            var a = new SymbolData("A", 2, 1, false, SymbolWinMode.LineMatch, PayScaling.PerSymbol);
            var w = new SymbolData("W", 0, -1, true, SymbolWinMode.LineMatch, PayScaling.PerSymbol, totalCountTrigger: -1, matchGroupId: -1, allowWild: true);
            var grid = new[] { a, w, a };
            var winlines = new List<int[]> { new int[] { 0, 1, 2 } };
            var mults = new List<int> { 1 };
            var ev = new WinlineEvaluator();
            var wins = ev.EvaluateWins(grid, 3, new[] { 1, 1, 1 }, winlines, mults);
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
            Assert.AreEqual(6, wins[0].Value);
        }
    }
}
