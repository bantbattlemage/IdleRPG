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
            // Leading non-paying wilds should allow a later paying trigger to win (wild fallback)
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

        // Jagged / diagonal tests consolidated from separate test file
        [Test]
        public void PayingCandidateAfterNonPayingWild_TriggersWin_DiagonalUp()
        {
            var ev = new WinlineEvaluator();
            int columns = 3;
            int[] rowsPerColumn = new int[] { 3, 3, 2 };
            int maxRows = 3;
            SymbolData[] grid = new SymbolData[maxRows * columns];

            int[] pattern = new int[3];
            pattern[0] = 2 * columns + 0; // col0 row2
            pattern[1] = 2 * columns + 1; // col1 row2
            pattern[2] = 1 * columns + 2; // col2 row1

            grid[pattern[0]] = new SymbolData("W", 0, -1, true);
            grid[pattern[1]] = new SymbolData("W", 0, -1, true);
            grid[pattern[2]] = new SymbolData("A", 5, 1, false, SymbolWinMode.LineMatch, PayScaling.PerSymbol);

            var wins = ev.EvaluateWins(grid, columns, rowsPerColumn, new List<int[]> { pattern }, new List<int> { 1 }, 1);
            // Leading non-paying wilds should allow a later paying trigger to win
            Assert.AreEqual(1, wins.Count, "Expected one win when paying candidate appears after non-paying wilds.");
            Assert.AreEqual(15, wins[0].Value, "PerSymbol scaling: base 5 * 3 matches = 15");
        }

        [Test]
        public void PayingCandidateAfterNonPayingWild_TriggersWin_DiagonalDown()
        {
            var ev = new WinlineEvaluator();
            int columns = 4;
            int[] rowsPerColumn = new int[] { 4, 3, 4, 2 };
            int maxRows = 4;
            SymbolData[] grid = new SymbolData[maxRows * columns];

            int[] pattern = new int[4];
            pattern[0] = 3 * columns + 0; // col0 row3
            pattern[1] = 2 * columns + 1; // col1 row2
            pattern[2] = 3 * columns + 2; // col2 row3
            pattern[3] = 1 * columns + 3; // col3 row1

            grid[pattern[0]] = new SymbolData("W", 0, -1, true);
            grid[pattern[1]] = new SymbolData("W", 0, -1, true);
            grid[pattern[2]] = new SymbolData("B", 3, 1, false, SymbolWinMode.LineMatch, PayScaling.PerSymbol);
            grid[pattern[3]] = new SymbolData("B", 3, 1, false, SymbolWinMode.LineMatch, PayScaling.PerSymbol);

            var wins = ev.EvaluateWins(grid, columns, rowsPerColumn, new List<int[]> { pattern }, new List<int> { 1 }, 1);
            // Leading non-paying wilds should allow the later paying candidate to trigger a win
            Assert.AreEqual(1, wins.Count, "Expected one win when a paying candidate appears later in a diagonal down pattern.");
            Assert.AreEqual(12, wins[0].Value);
        }

        [Test]
        public void DiagonalAndStraight_WildsThenPayingSymbols_AreRecognized()
        {
            var ev = new WinlineEvaluator();
            int columns = 4;
            int[] rowsPerColumn = new int[] { 4, 4, 4, 4 };
            int maxRows = 4;

            // Prepare a rectangular grid
            SymbolData[] grid = new SymbolData[maxRows * columns];

            // We'll test three shapes: straight across, diagonal up (row increases), diagonal down (row decreases)
            var patterns = new List<int[]>();

            // Straight across at row 2
            patterns.Add(new int[] { 2 * columns + 0, 2 * columns + 1, 2 * columns + 2, 2 * columns + 3 });
            // Diagonal up starting at row 0 (rows 0,1,2,3)
            patterns.Add(new int[] { 0 * columns + 0, 1 * columns + 1, 2 * columns + 2, 3 * columns + 3 });
            // Diagonal down starting at row 3 (rows 3,2,1,0)
            patterns.Add(new int[] { 3 * columns + 0, 2 * columns + 1, 1 * columns + 2, 0 * columns + 3 });

            foreach (var pattern in patterns)
            {
                // Clear grid
                for (int i = 0; i < grid.Length; i++) grid[i] = null;

                // Place two non-paying wilds on first two columns of the pattern
                grid[pattern[0]] = new SymbolData("W", 0, -1, true);
                grid[pattern[1]] = new SymbolData("W", 0, -1, true);
                // Place paying symbols (B) on the remaining pattern positions
                grid[pattern[2]] = new SymbolData("B", 4, 1, false, SymbolWinMode.LineMatch, PayScaling.PerSymbol);
                grid[pattern[3]] = new SymbolData("B", 4, 1, false, SymbolWinMode.LineMatch, PayScaling.PerSymbol);

                var wins = ev.EvaluateWins(grid, columns, rowsPerColumn, new List<int[]> { pattern }, new List<int> { 1 }, 1);
                // Leading non-paying wilds should allow a later paying trigger to win
                Assert.AreEqual(1, wins.Count, $"Expected a win for pattern [{string.Join(',', pattern)}]");
                // PerSymbol scaling: base 4 * matches (4) = 16
                Assert.AreEqual(16, wins[0].Value, $"Unexpected win value for pattern [{string.Join(',', pattern)}]");
            }
        }

        [Test]
        public void DiagonalDown_LeftmostMissing_PayingLaterTriggersWin()
        {
            var ev = new WinlineEvaluator();
            int columns = 8;
            int[] rowsPerColumn = new int[] { 7, 7, 6, 6, 8, 4, 4, 4 };
            int maxRows = 8;
            SymbolData[] grid = new SymbolData[maxRows * columns];

            // Pattern from the observed log: leftmost is invalid (-1) then valid entries follow
            int[] pattern = new int[] { -1, 49, 42, 35, 28, 21, 14, 7 };

            // Place a non-paying wild at the first valid position (pattern[1] = 49)
            grid[49] = new SymbolData("W", baseValue: 0, minWinDepth: 0, isWild: true);

            // Place a paying symbol 'X' at pattern[2..4] (42,35,28) so that contiguous matches >= MinWinDepth (3)
            var paying = new SymbolData("X", baseValue: 5, minWinDepth: 3, isWild: false, winMode: SymbolWinMode.LineMatch, scaling: PayScaling.DepthSquared);
            grid[42] = paying;
            grid[35] = paying;
            grid[28] = paying;

            // Ensure other pattern positions are null or non-matching
            grid[21] = null;
            grid[14] = null;
            grid[7] = null;

            var wins = ev.EvaluateWins(grid, columns, rowsPerColumn, new List<int[]> { pattern }, new List<int> { 1 }, 1);
            // Leading non-paying wilds should allow the later paying trigger to award
            Assert.AreEqual(1, wins.Count, "Expected diagonal-down win to be recognized when leftmost positions are missing/wild and paying symbol appears later.");
            Assert.AreEqual(0, wins[0].LineIndex, "Win should be reported for the provided pattern index 0");
            Assert.IsTrue(wins[0].Indexes.Length >= 3, "Winning indexes should include at least the trigger depth");
        }

        [Test]
        public void DiagonalDown_LeftmostRow4_WildsThenPaying_Awards()
        {
            var ev = new WinlineEvaluator();
            int columns = 8;
            int[] rowsPerColumn = new int[] { 7, 7, 6, 6, 8, 4, 4, 4 };
            int maxRows = 8;
            SymbolData[] grid = new SymbolData[maxRows * columns];

            // Diagonal down starting at column0,row4 -> positions: (4,0)=32, (3,1)=25, (2,2)=18, (1,3)=11
            int[] pattern = new int[] { 32, 25, 18, 11, -1, -1, -1, -1 };

            // Place three non-paying wilds followed by a paying symbol
            grid[32] = new SymbolData("W", 0, -1, true);
            grid[25] = new SymbolData("W", 0, -1, true);
            grid[18] = new SymbolData("W", 0, -1, true);
            grid[11] = new SymbolData("X", 5, 3, false, SymbolWinMode.LineMatch, PayScaling.PerSymbol);

            var wins = ev.EvaluateWins(grid, columns, rowsPerColumn, new List<int[]> { pattern }, new List<int> { 1 }, 1);
            // Leading non-paying wilds should allow the later paying trigger to award
            Assert.AreEqual(1, wins.Count, "Expected diagonal-down win to be recognized when leftmost row4 has wilds followed by a paying symbol.");
            Assert.AreEqual(0, wins[0].LineIndex);
            CollectionAssert.IsSubsetOf(new int[] { 32, 25, 18, 11 }, wins[0].Indexes);
        }

        [Test]
        public void SpinLog_2025_11_24_060105_FullGrid_ReproducesWins()
        {
            var ev = new WinlineEvaluator();
            int columns = 8;
            int[] rowsPerColumn = new int[] { 7, 7, 6, 6, 8, 4, 4, 4 };
            int maxRows = 8;
            SymbolData[] grid = new SymbolData[maxRows * columns];

            // Helper local ids from log
            int groupB = 449123273;
            int g3 = 700787558;
            int g1 = 667232320;
            int g0 = 684009939;
            int g2 = 717565177;
            int g4 = 751120415;

            // Populate every non-null cell from the spin log
            grid[0] = new SymbolData("B", baseValue: 5, minWinDepth: 3, isWild: false, winMode: SymbolWinMode.TotalCount, scaling: PayScaling.PerSymbol, totalCountTrigger: 3, matchGroupId: groupB);
            grid[1] = new SymbolData("3", baseValue: 4, minWinDepth: 3, isWild: false, winMode: SymbolWinMode.LineMatch, scaling: PayScaling.DepthSquared, totalCountTrigger: -1, matchGroupId: g3);
            grid[2] = new SymbolData("3", baseValue: 4, minWinDepth: 3, isWild: false, winMode: SymbolWinMode.LineMatch, scaling: PayScaling.DepthSquared, totalCountTrigger: -1, matchGroupId: g3);
            grid[3] = new SymbolData("B", baseValue: 5, minWinDepth: 3, isWild: false, winMode: SymbolWinMode.TotalCount, scaling: PayScaling.PerSymbol, totalCountTrigger: 3, matchGroupId: groupB);
            grid[4] = new SymbolData("3", baseValue: 4, minWinDepth: 3, isWild: false, winMode: SymbolWinMode.LineMatch, scaling: PayScaling.DepthSquared, totalCountTrigger: -1, matchGroupId: g3);
            grid[5] = new SymbolData("1", baseValue: 2, minWinDepth: 3, isWild: false, winMode: SymbolWinMode.LineMatch, scaling: PayScaling.DepthSquared, totalCountTrigger: -1, matchGroupId: g1);
            grid[6] = new SymbolData("0", baseValue: 1, minWinDepth: 3, isWild: false, winMode: SymbolWinMode.LineMatch, scaling: PayScaling.DepthSquared, totalCountTrigger: -1, matchGroupId: g0);
            grid[7] = new SymbolData("0", baseValue: 1, minWinDepth: 3, isWild: false, winMode: SymbolWinMode.LineMatch, scaling: PayScaling.DepthSquared, totalCountTrigger: -1, matchGroupId: g0);

            grid[8] = new SymbolData("1", baseValue: 2, minWinDepth: 3, isWild: false, winMode: SymbolWinMode.LineMatch, scaling: PayScaling.DepthSquared, totalCountTrigger: -1, matchGroupId: g1);
            grid[9] = new SymbolData("B", baseValue: 5, minWinDepth: 3, isWild: false, winMode: SymbolWinMode.TotalCount, scaling: PayScaling.PerSymbol, totalCountTrigger: 3, matchGroupId: groupB);
            grid[10] = new SymbolData("W", baseValue: 0, minWinDepth: 0, isWild: true, winMode: SymbolWinMode.LineMatch, scaling: PayScaling.DepthSquared);
            grid[11] = new SymbolData("3", baseValue: 4, minWinDepth: 3, isWild: false, winMode: SymbolWinMode.LineMatch, scaling: PayScaling.DepthSquared, totalCountTrigger: -1, matchGroupId: g3);
            grid[12] = new SymbolData("W", baseValue: 0, minWinDepth: 0, isWild: true, winMode: SymbolWinMode.LineMatch, scaling: PayScaling.DepthSquared);
            grid[13] = new SymbolData("2", baseValue: 3, minWinDepth: 3, isWild: false, winMode: SymbolWinMode.LineMatch, scaling: PayScaling.DepthSquared, totalCountTrigger: -1, matchGroupId: g2);
            grid[14] = new SymbolData("0", baseValue: 1, minWinDepth: 3, isWild: false, winMode: SymbolWinMode.LineMatch, scaling: PayScaling.DepthSquared, totalCountTrigger: -1, matchGroupId: g0);
            grid[15] = new SymbolData("0", baseValue: 1, minWinDepth: 3, isWild: false, winMode: SymbolWinMode.LineMatch, scaling: PayScaling.DepthSquared, totalCountTrigger: -1, matchGroupId: g0);

            grid[16] = new SymbolData("3", baseValue: 4, minWinDepth: 3, isWild: false, winMode: SymbolWinMode.LineMatch, scaling: PayScaling.DepthSquared, totalCountTrigger: -1, matchGroupId: g3);
            grid[17] = new SymbolData("3", baseValue: 4, minWinDepth: 3, isWild: false, winMode: SymbolWinMode.LineMatch, scaling: PayScaling.DepthSquared, totalCountTrigger: -1, matchGroupId: g3);
            grid[18] = new SymbolData("3", baseValue: 4, minWinDepth: 3, isWild: false, winMode: SymbolWinMode.LineMatch, scaling: PayScaling.DepthSquared, totalCountTrigger: -1, matchGroupId: g3);
            grid[19] = new SymbolData("4", baseValue: 5, minWinDepth: 3, isWild: false, winMode: SymbolWinMode.LineMatch, scaling: PayScaling.DepthSquared, totalCountTrigger: -1, matchGroupId: g4);
            grid[20] = new SymbolData("W", baseValue: 0, minWinDepth: 0, isWild: true, winMode: SymbolWinMode.LineMatch, scaling: PayScaling.DepthSquared);
            grid[21] = new SymbolData("3", baseValue: 4, minWinDepth: 3, isWild: false, winMode: SymbolWinMode.LineMatch, scaling: PayScaling.DepthSquared, totalCountTrigger: -1, matchGroupId: g3);
            grid[22] = new SymbolData("0", baseValue: 1, minWinDepth: 3, isWild: false, winMode: SymbolWinMode.LineMatch, scaling: PayScaling.DepthSquared, totalCountTrigger: -1, matchGroupId: g0);
            grid[23] = new SymbolData("4", baseValue: 5, minWinDepth: 3, isWild: false, winMode: SymbolWinMode.LineMatch, scaling: PayScaling.DepthSquared, totalCountTrigger: -1, matchGroupId: g4);

            grid[24] = new SymbolData("W", baseValue: 0, minWinDepth: 0, isWild: true, winMode: SymbolWinMode.LineMatch, scaling: PayScaling.DepthSquared);
            grid[25] = new SymbolData("4", baseValue: 5, minWinDepth: 3, isWild: false, winMode: SymbolWinMode.LineMatch, scaling: PayScaling.DepthSquared, totalCountTrigger: -1, matchGroupId: g4);
            grid[26] = new SymbolData("B", baseValue: 5, minWinDepth: 3, isWild: false, winMode: SymbolWinMode.TotalCount, scaling: PayScaling.PerSymbol, totalCountTrigger: 3, matchGroupId: groupB);
            grid[27] = new SymbolData("C", baseValue: 5, minWinDepth: -1, isWild: false, winMode: SymbolWinMode.SingleOnReel, scaling: PayScaling.DepthSquared);
            grid[28] = new SymbolData("3", baseValue: 4, minWinDepth: 3, isWild: false, winMode: SymbolWinMode.LineMatch, scaling: PayScaling.DepthSquared, totalCountTrigger: -1, matchGroupId: g3);
            grid[29] = new SymbolData("2", baseValue: 3, minWinDepth: 3, isWild: false, winMode: SymbolWinMode.LineMatch, scaling: PayScaling.DepthSquared, totalCountTrigger: -1, matchGroupId: g2);
            grid[30] = new SymbolData("0", baseValue: 1, minWinDepth: 3, isWild: false, winMode: SymbolWinMode.LineMatch, scaling: PayScaling.DepthSquared, totalCountTrigger: -1, matchGroupId: g0);
            grid[31] = new SymbolData("3", baseValue: 4, minWinDepth: 3, isWild: false, winMode: SymbolWinMode.LineMatch, scaling: PayScaling.DepthSquared, totalCountTrigger: -1, matchGroupId: g3);

            grid[32] = new SymbolData("1", baseValue: 2, minWinDepth: 3, isWild: false, winMode: SymbolWinMode.LineMatch, scaling: PayScaling.DepthSquared, totalCountTrigger: -1, matchGroupId: g1);
            grid[33] = new SymbolData("C", baseValue: 5, minWinDepth: -1, isWild: false, winMode: SymbolWinMode.SingleOnReel, scaling: PayScaling.DepthSquared);
            grid[34] = new SymbolData("W", baseValue: 0, minWinDepth: 0, isWild: true, winMode: SymbolWinMode.LineMatch, scaling: PayScaling.DepthSquared);
            grid[35] = new SymbolData("2", baseValue: 3, minWinDepth: 3, isWild: false, winMode: SymbolWinMode.LineMatch, scaling: PayScaling.DepthSquared, totalCountTrigger: -1, matchGroupId: g2);
            grid[36] = new SymbolData("W", baseValue: 0, minWinDepth: 0, isWild: true, winMode: SymbolWinMode.LineMatch, scaling: PayScaling.DepthSquared);

            grid[40] = new SymbolData("1", baseValue: 2, minWinDepth: 3, isWild: false, winMode: SymbolWinMode.LineMatch, scaling: PayScaling.DepthSquared, totalCountTrigger: -1, matchGroupId: g1);
            grid[41] = new SymbolData("W", baseValue: 0, minWinDepth: 0, isWild: true, winMode: SymbolWinMode.LineMatch, scaling: PayScaling.DepthSquared);
            grid[42] = new SymbolData("W", baseValue: 0, minWinDepth: 0, isWild: true, winMode: SymbolWinMode.LineMatch, scaling: PayScaling.DepthSquared);
            grid[43] = new SymbolData("W", baseValue: 0, minWinDepth: 0, isWild: true, winMode: SymbolWinMode.LineMatch, scaling: PayScaling.DepthSquared);
            grid[44] = new SymbolData("1", baseValue: 2, minWinDepth: 3, isWild: false, winMode: SymbolWinMode.LineMatch, scaling: PayScaling.DepthSquared, totalCountTrigger: -1, matchGroupId: g1);

            grid[48] = new SymbolData("3", baseValue: 4, minWinDepth: 3, isWild: false, winMode: SymbolWinMode.LineMatch, scaling: PayScaling.DepthSquared, totalCountTrigger: -1, matchGroupId: g3);
            grid[49] = new SymbolData("1", baseValue: 2, minWinDepth: 3, isWild: false, winMode: SymbolWinMode.LineMatch, scaling: PayScaling.DepthSquared, totalCountTrigger: -1, matchGroupId: g1);

            grid[52] = new SymbolData("B", baseValue: 5, minWinDepth: 3, isWild: false, winMode: SymbolWinMode.TotalCount, scaling: PayScaling.PerSymbol, totalCountTrigger: 3, matchGroupId: groupB);

            grid[60] = new SymbolData("2", baseValue: 3, minWinDepth: 3, isWild: false, winMode: SymbolWinMode.LineMatch, scaling: PayScaling.DepthSquared, totalCountTrigger: -1, matchGroupId: g2);

            var wins = ev.EvaluateWins(grid, columns, rowsPerColumn, new List<int[]>(), new List<int>());

            Assert.AreEqual(3, wins.Count, "Expected three wins reported in the spin log (one TotalCount group + two SingleOnReel instances)");

            // TotalCount win: base 5 * count(5) = 25
            Assert.IsTrue(wins.Any(w => w.LineIndex == -1 && w.Value == 25 && w.Indexes != null && w.Indexes.SequenceEqual(new int[] { 0, 3, 9, 26, 52 })), "Expected TotalCount win with indexes [0,3,9,26,52] and value 25");

            // SingleOnReel wins: each base 5 -> value 5
            Assert.IsTrue(wins.Any(w => w.LineIndex == -1 && w.Value == 5 && w.Indexes != null && w.Indexes.SequenceEqual(new int[] { 27 })), "Expected SingleOnReel win at index 27 with value 5");
            Assert.IsTrue(wins.Any(w => w.LineIndex == -1 && w.Value == 5 && w.Indexes != null && w.Indexes.SequenceEqual(new int[] { 33 })), "Expected SingleOnReel win at index 33 with value 5");
        }

        [Test]
        public void MissedCase_Wild_Then_Paying_Then_Wild_DiagonalDown()
        {
            var ev = new WinlineEvaluator();
            int columns = 8;
            int[] rowsPerColumn = new int[] { 7, 7, 6, 6, 8, 4, 4, 4 };
            int maxRows = 8;
            SymbolData[] grid = new SymbolData[maxRows * columns];

            // Pattern: start at column0,row3 -> indexes: (3,0)=24, (2,1)=17, (1,2)=10, (0,3)=3 ... but we only need first three
            int[] pattern = new int[] { 24, 17, 10, 3, -1, -1, -1, -1 };

            // Place non-paying wild at first and third positions, paying '3' at middle
            grid[24] = new SymbolData("W", 0, -1, true);
            grid[17] = new SymbolData("3", 4, 3, false, SymbolWinMode.LineMatch, PayScaling.DepthSquared);
            grid[10] = new SymbolData("W", 0, -1, true);

            var wins = ev.EvaluateWins(grid, columns, rowsPerColumn, new List<int[]> { pattern }, new List<int> { 1 }, 1);
            Assert.AreEqual(1, wins.Count, "Expected the diagonal containing W,3,W to be recognized as a win.");
            Assert.AreEqual(0, wins[0].LineIndex);
            CollectionAssert.IsSubsetOf(new int[] { 24, 17, 10 }, wins[0].Indexes);
        }

    }
}
