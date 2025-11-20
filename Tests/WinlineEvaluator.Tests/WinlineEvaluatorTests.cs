using NUnit.Framework;
using System.Collections.Generic;

// Minimal test doubles to exercise WinlineEvaluator logic without Unity runtime
namespace WinlineEvaluator.Tests
{
    // Simplified SymbolData duplicate (matches production shape used by evaluator)
    public class SymbolData
    {
        public string Name;
        public int[] BaseValueMultiplier;
        public bool IsWild;
        public bool AllowWildMatch = true;

        public SymbolData(string name, int[] multipliers = null, bool isWild = false, bool allowWild = true)
        {
            Name = name;
            BaseValueMultiplier = multipliers;
            IsWild = isWild;
            AllowWildMatch = allowWild;
        }

        public int MinWinDepth
        {
            get
            {
                if (BaseValueMultiplier == null) return -1;
                for (int i = 0; i < BaseValueMultiplier.Length; i++) if (BaseValueMultiplier[i] > 0) return i;
                return -1;
            }
        }

        public bool Matches(SymbolData other)
        {
            if (other == null) return false;
            if (!string.IsNullOrEmpty(Name) && Name == other.Name) return true;
            if (IsWild && other.IsWild) return true;
            if (IsWild && other.AllowWildMatch) return true;
            if (other.IsWild && AllowWildMatch) return true;
            return false;
        }
    }

    // Simplified evaluator copied & adapted from project to run in plain .NET test
    public class WinlineEvaluator
    {
        public class WinData { public int LineIndex; public int Value; public int[] Indexes; public WinData(int l, int v, int[] i) { LineIndex = l; Value = v; Indexes = i; } }

        // Updated to respect rowsPerColumn and column-major indexing with bottom-left origin
        public List<WinData> EvaluateWins(SymbolData[] grid, int columns, int[] rowsPerColumn, List<int[]> winlines, List<int> winMultipliers, int creditCost = 1)
        {
            var winData = new List<WinData>();
            if (grid == null) return winData;
            for (int i = 0; i < winlines.Count; i++)
            {
                var concrete = winlines[i];
                if (concrete == null || concrete.Length == 0) continue;

                // candidates
                var candidates = new List<(int idx, SymbolData sym)>();
                foreach (var idx in concrete)
                {
                    if (idx < 0 || idx >= grid.Length) continue;
                    // compute col/row to validate existence in rowsPerColumn
                    if (rowsPerColumn != null && rowsPerColumn.Length == columns)
                    {
                        int col = idx % columns;
                        int row = idx / columns;
                        if (row >= rowsPerColumn[col]) continue; // truncated cell
                    }
                    var s = grid[idx];
                    if (s == null) continue;
                    if (s.MinWinDepth == -1) continue;
                    candidates.Add((idx, s));
                }
                if (candidates.Count == 0) continue;

                int bestCount = 0; int bestValue = 0; int[] bestIndexes = null;
                foreach (var cand in candidates)
                {
                    var trigger = cand.sym;
                    var indexes = new List<int>();
                    foreach (var idx in concrete)
                    {
                        if (idx < 0 || idx >= grid.Length) break;
                        if (rowsPerColumn != null && rowsPerColumn.Length == columns)
                        {
                            int col = idx % columns;
                            int row = idx / columns;
                            if (row >= rowsPerColumn[col]) break;
                        }
                        var cell = grid[idx];
                        if (cell == null) break;
                        if (cell.Matches(trigger)) indexes.Add(idx); else break;
                    }
                    int count = indexes.Count;
                    if (count > 0 && count >= trigger.MinWinDepth)
                    {
                        int multIndex = count - 1;
                        var mults = trigger.BaseValueMultiplier ?? new int[0];
                        if (mults.Length == 0) continue;
                        if (multIndex >= mults.Length) multIndex = mults.Length - 1;
                        int value = mults[multIndex] * winMultipliers[i] * creditCost;
                        if (count > bestCount) { bestCount = count; bestValue = value; bestIndexes = indexes.ToArray(); }
                    }
                }
                if (bestCount > 0) winData.Add(new WinData(i, bestValue, bestIndexes));
            }
            return winData;
        }
    }

    public class Tests
    {
        // Configure multipliers to match game ScriptableObjects expectations for tests
        // Symbol A ("1"): third value (index 2) = 2, fifth value (index 4) = 4
        private SymbolData A => new SymbolData("1", new int[] {0,0,2,0,4});
        // Symbol B ("2"): fourth value (index 3) = 4
        private SymbolData B => new SymbolData("2", new int[] {0,0,0,4});
        private SymbolData Wild => new SymbolData("W", null, isWild: true, allowWild: true);
        // Symbol C: low-depth symbol that can form small wins for tests
        private SymbolData C => new SymbolData("C", new int[] {1,2,3});

        [Test]
        public void Test_11111_straight_awards()
        {
            var grid = new SymbolData[] { A, A, A, A, A };
            var winlines = new List<int[]> { new int[] {0,1,2,3,4} };
            var winmult = new List<int> {1};
            var eval = new WinlineEvaluator();
            var wins = eval.EvaluateWins(grid, 5, new int[] {1,1,1,1,1}, winlines, winmult);
            Assert.IsNotNull(wins);
            Assert.AreEqual(1, wins.Count);
            Assert.AreEqual(4, wins[0].Value);
        }

        [Test]
        public void Test_WW1_awards()
        {
            var grid = new SymbolData[] { Wild, Wild, A };
            var winlines = new List<int[]> { new int[] {0,1,2} };
            var winmult = new List<int> {1};
            var eval = new WinlineEvaluator();
            var wins = eval.EvaluateWins(grid, 3, new int[] {1,1,1}, winlines, winmult);
            Assert.IsNotNull(wins);
            Assert.AreEqual(1, wins.Count);
            Assert.AreEqual(2, wins[0].Value);
        }

        [Test]
        public void Test_WWW2_awards_2()
        {
            var grid = new SymbolData[] { Wild, Wild, Wild, B };
            var winlines = new List<int[]> { new int[] {0,1,2,3} };
            var winmult = new List<int> {1};
            var eval = new WinlineEvaluator();
            var wins = eval.EvaluateWins(grid, 4, new int[] {1,1,1,1}, winlines, winmult);
            Assert.IsNotNull(wins);
            Assert.AreEqual(1, wins.Count);
            Assert.AreEqual(4, wins[0].Value);
        }

        [Test]
        public void Test_W22W_awards()
        {
            var grid = new SymbolData[] { Wild, B, B, Wild };
            var winlines = new List<int[]> { new int[] {0,1,2,3} };
            var winmult = new List<int> {1};
            var eval = new WinlineEvaluator();
            var wins = eval.EvaluateWins(grid, 4, new int[] {1,1,1,1}, winlines, winmult);
            Assert.IsNotNull(wins);
            Assert.AreEqual(1, wins.Count);
            // With B's multipliers above, a 4-match should award index3 = 4
            Assert.AreEqual(4, wins[0].Value);
        }

        [Test]
        public void Test_NonUniformRows_Truncates_at_missing_row()
        {
            // columns = 3, rows per column = [2,3,1]
            // maxRows = 3 => grid length = 9 (row * columns + col)
            // We'll target row index 1 (second row): indexes = 3,4,5
            // Column 2 has only 1 row, so index 5 is invalid (treated as missing)
            int columns = 3;
            int[] rowsPerColumn = new int[] {2,3,1};
            int maxRows = 3;
            var grid = new SymbolData[maxRows * columns];
            // Fill grid column-major: index = row*columns + col
            // place C at (col0,row1) => idx=1*3+0=3
            grid[3] = C;
            // place C at (col1,row1) => idx=4
            grid[4] = C;
            // column2 row1 does not exist => leave grid[5] null

            var winlines = new List<int[]> { new int[] {3,4,5} };
            var winmult = new List<int> {1};
            var eval = new WinlineEvaluator();
            var wins = eval.EvaluateWins(grid, columns, rowsPerColumn, winlines, winmult);
            Assert.IsNotNull(wins);
            // Expect a truncated 2-symbol win (indexes 3 and 4) because column2 is missing row1
            Assert.AreEqual(1, wins.Count);
            Assert.AreEqual(2, wins[0].Indexes.Length);
            CollectionAssert.AreEqual(new int[] {3,4}, wins[0].Indexes);
        }

        [Test]
        public void Test_LargeVariableGrid_Multiple_wins_and_nulls()
        {
            // Create a 5-column layout with varying rows
            int columns = 5;
            int[] rowsPerColumn = new int[] {4,2,3,5,1};
            int maxRows = 5;
            var grid = new SymbolData[maxRows * columns];
            // Fill some cells; leave others null to emulate different reel heights
            // Column 0 rows 0..3 -> put symbol A at row0..2 to create multi-match across first three columns
            grid[0 * columns + 0] = A; // idx 0
            grid[1 * columns + 0] = A; // idx 5
            grid[2 * columns + 0] = A; // idx 10

            // Column 1 has only 2 rows; place A at row0 and row1
            grid[0 * columns + 1] = A; // idx 1
            grid[1 * columns + 1] = A; // idx 6

            // Column 2 has 3 rows; place A at row0 -> forms a shorter across line
            grid[0 * columns + 2] = A; // idx 2

            // Column 3 has 5 rows; place B at row0.. to create diagonal match with column0
            grid[0 * columns + 3] = B; // idx 3
            grid[1 * columns + 3] = B; // idx 8

            // Column 4 has 1 row; leave it null to ensure truncation

            // Define a straight-across at bottom row: indexes [0,1,2,3,4]
            var straightBottom = new int[] { 0,1,2,3,4 };
            // Define a staggered line across rows [0,col0],[1,col1],[2,col2],[3,col3] -> concrete indexes
            var diagonal = new int[] { 0*columns+0, 1*columns+1, 2*columns+2, 3*columns+3 };

            var winlines = new List<int[]> { straightBottom, diagonal };
            var winmult = new List<int> { 1, 1 };

            var eval = new WinlineEvaluator();
            var wins = eval.EvaluateWins(grid, columns, rowsPerColumn, winlines, winmult);
            Assert.IsNotNull(wins);
            // We expect at least the diagonal to produce wins depending on matching rules; verify indexes for diagonal
            var diagWin = wins.Find(w => w.LineIndex == 1);
            Assert.IsNotNull(diagWin);
            // The diagonal should only include positions that actually matched; check that indexes are subset of diagonal
            foreach (var idx in diagWin.Indexes) Assert.Contains(idx, diagonal);
        }
    }
}
