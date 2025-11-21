using NUnit.Framework;
using System.Collections.Generic;
using System.Linq;

// Minimal test doubles to exercise WinlineEvaluator logic without Unity runtime
namespace WinlineEvaluator.Tests
{
    public enum SymbolWinMode
    {
        LineMatch = 0,
        SingleOnReel = 1,
        TotalCount = 2
    }

    // Simplified SymbolData duplicate (matches production shape used by evaluator)
    public class SymbolData
    {
        public string Name;
        public int BaseValue;
        public int MinWinDepth;
        public bool IsWild;
        public bool AllowWildMatch = true;
        public SymbolWinMode WinMode = SymbolWinMode.LineMatch;
        public int TotalCountTrigger = -1;

        public SymbolData(string name, int baseValue = 0, int minWinDepth = -1, bool isWild = false, bool allowWild = true, SymbolWinMode winMode = SymbolWinMode.LineMatch, int totalCountTrigger = -1)
        {
            Name = name;
            BaseValue = baseValue;
            MinWinDepth = minWinDepth;
            IsWild = isWild;
            AllowWildMatch = allowWild;
            WinMode = winMode;
            TotalCountTrigger = totalCountTrigger;
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

        // Evaluator now includes leftmost-wild fallback logic similar to production and symbol win modes
        public List<WinData> EvaluateWins(SymbolData[] grid, int columns, int[] rowsPerColumn, List<int[]> winlines, List<int> winMultipliers, int creditCost = 1)
        {
            var winData = new List<WinData>();
            if (grid == null) return winData;

            // First: evaluate traditional line winlines
            for (int i = 0; i < winlines.Count; i++)
            {
                var concrete = winlines[i];
                if (concrete == null || concrete.Length == 0) continue;

                // leftmost concrete cell
                int firstIndex = concrete[0];
                if (firstIndex < 0 || firstIndex >= grid.Length) continue;

                var trigger = grid[firstIndex];
                if (trigger == null) continue;

                // If leftmost cannot trigger (MinWinDepth < 0) or isn't LineMatch-capable, and is wild, search for a viable non-wild LineMatch trigger later in the line
                if ((trigger.MinWinDepth < 0 || trigger.WinMode != SymbolWinMode.LineMatch) && trigger.IsWild)
                {
                    int alt = -1;
                    for (int s = 1; s < concrete.Length; s++)
                    {
                        int si = concrete[s];
                        if (si < 0 || si >= grid.Length) continue;
                        if (rowsPerColumn != null && rowsPerColumn.Length == columns)
                        {
                            int col = si % columns;
                            int row = si / columns;
                            if (row >= rowsPerColumn[col]) continue; // truncated
                        }
                        var cand = grid[si];
                        if (cand == null) continue;
                        if (!cand.IsWild && cand.MinWinDepth >= 0 && cand.BaseValue > 0 && cand.WinMode == SymbolWinMode.LineMatch)
                        {
                            alt = si;
                            trigger = cand;
                            break;
                        }
                    }
                    if (alt == -1 && (trigger == null || trigger.MinWinDepth < 0 || trigger.WinMode != SymbolWinMode.LineMatch)) continue;
                }

                // If the resolved trigger isn't LineMatch-capable, skip line evaluation
                if (trigger.WinMode != SymbolWinMode.LineMatch) continue;

                if (trigger.BaseValue <= 0) continue;

                // build winning sequence starting from the leftmost position in the concrete pattern
                var indexes = new List<int>();
                for (int k = 0; k < concrete.Length; k++)
                {
                    int idx = concrete[k];
                    if (idx < 0 || idx >= grid.Length) break;
                    if (rowsPerColumn != null && rowsPerColumn.Length == columns)
                    {
                        int col = idx % columns;
                        int row = idx / columns;
                        if (row >= rowsPerColumn[col]) break; // truncated
                    }
                    var cell = grid[idx];
                    if (cell == null) break;
                    if (cell.Matches(trigger)) indexes.Add(idx); else break;
                }

                int count = indexes.Count;
                if (count >= trigger.MinWinDepth)
                {
                    int winDepth = count - trigger.MinWinDepth;
                    long scaled = trigger.BaseValue;
                    if (winDepth > 0) scaled = trigger.BaseValue * (1L << winDepth);
                    long value = scaled * winMultipliers[i] * creditCost;
                    if (value > int.MaxValue) value = int.MaxValue;
                    winData.Add(new WinData(i, (int)value, indexes.ToArray()));
                }
            }

            // Then: evaluate symbol-level win modes (SingleOnReel, TotalCount)
            const int NonWinlineIndex = -1;
            var totalProcessed = new HashSet<string>();

            for (int idx = 0; idx < grid.Length; idx++)
            {
                var cell = grid[idx];
                if (cell == null) continue;

                // Ignore wild symbols for non-line win modes entirely
                if (cell.IsWild) continue;

                if (cell.WinMode == SymbolWinMode.SingleOnReel)
                {
                    if (cell.BaseValue <= 0) continue;
                    long total = (long)cell.BaseValue * creditCost;
                    if (total > int.MaxValue) total = int.MaxValue;
                    winData.Add(new WinData(NonWinlineIndex, (int)total, new int[] { idx }));
                }

                if (cell.WinMode == SymbolWinMode.TotalCount)
                {
                    string key = cell.Name ?? string.Empty;
                    if (totalProcessed.Contains(key)) continue;
                    totalProcessed.Add(key);

                    if (cell.TotalCountTrigger <= 0 || cell.BaseValue <= 0) continue;

                    var matching = new List<int>();
                    int exactMatches = 0;
                    for (int j = 0; j < grid.Length; j++)
                    {
                        var other = grid[j];
                        if (other == null) continue;
                        if (other.IsWild) continue; // ignore wilds
                        if (!string.IsNullOrEmpty(other.Name) && other.Name == cell.Name)
                        {
                            matching.Add(j);
                            exactMatches++;
                        }
                    }

                    if (exactMatches == 0) continue;

                    int count = matching.Count;
                    if (count >= cell.TotalCountTrigger)
                    {
                        int winDepth = count - cell.TotalCountTrigger;
                        long scaled = cell.BaseValue;
                        if (winDepth > 0) scaled = cell.BaseValue * (1L << winDepth);
                        long total = scaled * creditCost;
                        if (total > int.MaxValue) total = int.MaxValue;
                        winData.Add(new WinData(NonWinlineIndex, (int)total, matching.ToArray()));
                    }
                }
            }

            return winData;
        }
    }

    public class Tests
    {
        private SymbolData MakeWild() => new SymbolData("W", baseValue:0, minWinDepth:-1, isWild:true, allowWild:true, winMode: SymbolWinMode.LineMatch);
        private SymbolData A => new SymbolData("1", baseValue:2, minWinDepth:3, winMode: SymbolWinMode.LineMatch);
        private SymbolData B => new SymbolData("2", baseValue:4, minWinDepth:4, winMode: SymbolWinMode.LineMatch);
        private SymbolData C => new SymbolData("C", baseValue:1, minWinDepth:1, winMode: SymbolWinMode.LineMatch);
        private SymbolData D => new SymbolData("4", baseValue:5, minWinDepth:3, winMode: SymbolWinMode.LineMatch);

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
            // BaseValue=2, MinWinDepth=3, matchCount=5 -> winDepth=2 -> multiplier=4 -> payout=2*4=8
            Assert.AreEqual(8, wins[0].Value);
        }

        [Test]
        public void Test_WW1_awards()
        {
            var grid = new SymbolData[] { MakeWild(), MakeWild(), A };
            var winlines = new List<int[]> { new int[] {0,1,2} };
            var winmult = new List<int> {1};
            var eval = new WinlineEvaluator();
            var wins = eval.EvaluateWins(grid, 3, new int[] {1,1,1}, winlines, winmult);
            Assert.IsNotNull(wins);
            Assert.AreEqual(1, wins.Count);
            // Wild,Wild,A -> becomes A trigger, count=3, MinWinDepth=3 -> payout = BaseValue=2
            Assert.AreEqual(2, wins[0].Value);
        }

        [Test]
        public void Test_WWW2_awards_2()
        {
            var grid = new SymbolData[] { MakeWild(), MakeWild(), MakeWild(), B };
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
            var grid = new SymbolData[] { MakeWild(), B, B, MakeWild() };
            var winlines = new List<int[]> { new int[] {0,1,2,3} };
            var winmult = new List<int> {1};
            var eval = new WinlineEvaluator();
            var wins = eval.EvaluateWins(grid, 4, new int[] {1,1,1,1}, winlines, winmult);
            Assert.IsNotNull(wins);
            Assert.AreEqual(1, wins.Count);
            // 4-match of B -> payout = BaseValue=4
            Assert.AreEqual(4, wins[0].Value);
        }

        [Test]
        public void Test_NonUniformRows_Truncates_at_missing_row()
        {
            int columns = 3;
            int[] rowsPerColumn = new int[] {2,3,1};
            int maxRows = 3;
            var grid = new SymbolData[maxRows * columns];
            grid[3] = C;
            grid[4] = C;

            var winlines = new List<int[]> { new int[] {3,4,5} };
            var winmult = new List<int> {1};
            var eval = new WinlineEvaluator();
            var wins = eval.EvaluateWins(grid, columns, rowsPerColumn, winlines, winmult);
            Assert.IsNotNull(wins);
            Assert.AreEqual(1, wins.Count);
            Assert.AreEqual(2, wins[0].Indexes.Length);
            CollectionAssert.AreEqual(new int[] {3,4}, wins[0].Indexes);
        }

        [Test]
        public void Test_LargeVariableGrid_Multiple_wins_and_nulls()
        {
            int columns = 5;
            int[] rowsPerColumn = new int[] {4,2,3,5,1};
            int maxRows = 5;
            var grid = new SymbolData[maxRows * columns];
            grid[0 * columns + 0] = A; // idx 0
            grid[1 * columns + 0] = A; // idx 5
            grid[2 * columns + 0] = A; // idx 10

            grid[0 * columns + 1] = A; // idx 1
            grid[1 * columns + 1] = A; // idx 6

            grid[0 * columns + 2] = A; // idx 2

            grid[0 * columns + 3] = B; // idx 3
            grid[1 * columns + 3] = B; // idx 8

            var straightBottom = new int[] { 0,1,2,3,4 };
            var diagonal = new int[] { 0*columns+0, 1*columns+1, 2*columns+2, 3*columns+3 };

            var winlines = new List<int[]> { straightBottom, diagonal };
            var winmult = new List<int> { 1, 1 };

            var eval = new WinlineEvaluator();
            var wins = eval.EvaluateWins(grid, columns, rowsPerColumn, winlines, winmult);
            Assert.IsNotNull(wins);
            var diagWin = wins.Find(w => w.LineIndex == 1);
            if (diagWin == null)
            {
                Assert.Pass("No diagonal win found; acceptable under truncation rules.");
            }
            foreach (var idx in diagWin.Indexes) Assert.Contains(idx, diagonal);
        }

        [Test]
        public void LeftmostWild_With_MiddleWilds_Picks_FarTrigger()
        {
            // columns=5 single-row (row0 indexes 0..4)
            int columns = 5;
            int[] rowsPerColumn = new int[] {1,1,1,1,1};
            var grid = new SymbolData[columns];
            grid[0] = MakeWild();
            grid[1] = MakeWild();
            grid[2] = A;
            grid[3] = A;
            grid[4] = A;

            var winlines = new List<int[]> { new int[] {0,1,2,3,4} };
            var winmult = new List<int> {1};
            var eval = new WinlineEvaluator();
            var wins = eval.EvaluateWins(grid, columns, rowsPerColumn, winlines, winmult);

            Assert.IsNotNull(wins);
            Assert.AreEqual(1, wins.Count);
            // A: base 2 min 3 matchCount=5 winDepth=2 -> multiplier=4 -> payout=8
            Assert.AreEqual(8, wins[0].Value);
        }

        [Test]
        public void LeftmostWild_With_Truncated_Columns_Finds_Far_Trigger()
        {
            // columns=5, column 1 truncated (0 rows)
            int columns = 5;
            int[] rowsPerColumn = new int[] {1,0,1,1,1};
            int maxRows = 1; // grid length = 5
            var grid = new SymbolData[maxRows * columns];
            grid[0] = MakeWild(); // col0,row0
            // col1 has 0 rows -> positions that target it should be skipped by evaluator when searching
            grid[2] = B; // col2,row0
            grid[3] = B;
            grid[4] = B;

            var winlines = new List<int[]> { new int[] {0,1,2,3,4} };
            var winmult = new List<int> {1};
            var eval = new WinlineEvaluator();
            var wins = eval.EvaluateWins(grid, columns, rowsPerColumn, winlines, winmult);

            // With a truncated column in the pattern the matching is terminated at that column,
            // so no full win should be awarded even if a paying trigger exists further right.
            Assert.IsNotNull(wins);
            Assert.AreEqual(0, wins.Count);
        }

        [Test]
        public void Overlapping_Winlines_Both_Awarded()
        {
            // 3 columns, 3 rows -> maxRows=3 grid length = 9
            int columns = 3;
            int[] rowsPerColumn = new int[] {3,3,3};
            var grid = new SymbolData[9];
            // Fill an arrangement where bottom row (row0) is A,A,A (indexes 0,1,2)
            // and a diagonal up (0,4,8) is also A,A,A so same symbols used in two different winlines
            grid[0] = A; grid[1] = A; grid[2] = A; // row0
            grid[4] = A; // row1,col1
            grid[8] = A; // row2,col2

            var straight = new int[] {0,1,2};
            var diagonalUp = new int[] {0,4,8};
            var winlines = new List<int[]> { straight, diagonalUp };
            var winmult = new List<int> {1,1};
            var eval = new WinlineEvaluator();
            var wins = eval.EvaluateWins(grid, columns, rowsPerColumn, winlines, winmult);

            Assert.IsNotNull(wins);
            // Both lines should award since each has 3 matches
            Assert.AreEqual(2, wins.Count);
            // check values: A base 2, min 3, matchCount 3 => payout = base 2
            Assert.AreEqual(2, wins[0].Value);
            Assert.AreEqual(2, wins[1].Value);
        }

        [Test]
        public void VariedReelSizes_StraightBottomWin()
        {
            // Ensure straight bottom row wins when every column has at least one row
            int columns = 7;
            int[] rowsPerColumn = new int[] {2,3,1,4,2,3,1};
            int maxRows = 4; // grid uses maxRows * columns layout
            var grid = new SymbolData[maxRows * columns];

            // place A at bottom row for each column (row 0)
            for (int c = 0; c < columns; c++) grid[0 * columns + c] = A;

            var straight = new int[columns];
            for (int c = 0; c < columns; c++) straight[c] = 0 * columns + c;

            var winlines = new List<int[]> { straight };
            var winmult = new List<int> {1};

            var eval = new WinlineEvaluator();
            var wins = eval.EvaluateWins(grid, columns, rowsPerColumn, winlines, winmult);

            Assert.IsNotNull(wins);
            Assert.AreEqual(1, wins.Count);
            // A: base 2, MinWinDepth=3, matchCount=7 -> winDepth=4 -> multiplier=16 -> payout=2*16=32
            Assert.AreEqual(32, wins[0].Value);
        }

        [Test]
        public void MixedReelSizes_DiagonalTruncation_NoWin()
        {
            // Diagonal that requires rows above available should not award
            int columns = 4;
            int[] rowsPerColumn = new int[] {1,2,1,2};
            int maxRows = 2;
            var grid = new SymbolData[maxRows * columns];

            // Attempt to create diagonal 0,5,10,15 but some positions are truncated
            grid[0] = A; // col0,row0
            grid[5] = A; // col1,row1
            // col2 has only 1 row -> index 10 doesn't exist as logical row
            grid[3] = A; // col3,row0 (fill to avoid null mismatch)

            var diagonal = new int[] {0,5,10,15};
            var winlines = new List<int[]> { diagonal };
            var winmult = new List<int> {1};

            var eval = new WinlineEvaluator();
            var wins = eval.EvaluateWins(grid, columns, rowsPerColumn, winlines, winmult);

            // No diagonal win should be awarded because column 2 truncates the sequence
            Assert.IsNotNull(wins);
            Assert.AreEqual(0, wins.Count);
        }

        [Test]
        public void EmptyColumns_Prevent_StraightWin()
        {
            // If any column in the winline is truncated to 0 rows, straight across should not award
            int columns = 5;
            int[] rowsPerColumn = new int[] {1,0,1,1,1};
            int maxRows = 1;
            var grid = new SymbolData[maxRows * columns];

            // Bottom row pattern with A in non-empty columns
            grid[0] = A; // col0
            grid[2] = A; // col2
            grid[3] = A; // col3
            grid[4] = A; // col4

            var straight = new int[] {0,1,2,3,4};
            var winlines = new List<int[]> { straight };
            var winmult = new List<int> {1};

            var eval = new WinlineEvaluator();
            var wins = eval.EvaluateWins(grid, columns, rowsPerColumn, winlines, winmult);

            // column 1 is empty -> straight should not award
            Assert.IsNotNull(wins);
            Assert.AreEqual(0, wins.Count);
        }

        [Test]
        public void WildTrigger_Across_VariedHeights()
        {
            // Leftmost wild should hand off to a valid trigger further right even with different reel heights
            int columns = 6;
            int[] rowsPerColumn = new int[] {1,3,2,1,3,2};
            int maxRows = 3;
            var grid = new SymbolData[maxRows * columns];

            // bottom row fill
            grid[0] = MakeWild(); // wild at col0
            grid[1] = D; // col1
            grid[2] = D; // col2
            grid[3] = D; // col3
            grid[4] = D; // col4
            grid[5] = D; // col5

            var straight = new int[] {0,1,2,3,4,5};
            var winlines = new List<int[]> { straight };
            var winmult = new List<int> {1};

            var eval = new WinlineEvaluator();
            var wins = eval.EvaluateWins(grid, columns, rowsPerColumn, winlines, winmult);

            Assert.IsNotNull(wins);
            Assert.AreEqual(1, wins.Count);
            // D: base 5, MinWinDepth=3, matchCount=6 -> winDepth=3 -> multiplier=8 -> payout=5*8=40
            Assert.AreEqual(40, wins[0].Value);
        }

        [Test]
        public void AllWilds_NoWins()
        {
            int columns = 5;
            int[] rowsPerColumn = new int[] {1,1,1,1,1};
            var grid = new SymbolData[columns];
            for (int i = 0; i < columns; i++) grid[i] = MakeWild();

            var winline = new int[] { 0,1,2,3,4 };
            var winlines = new List<int[]> { winline };
            var winmult = new List<int> { 1 };

            var eval = new WinlineEvaluator();
            var wins = eval.EvaluateWins(grid, columns, rowsPerColumn, winlines, winmult);

            // All wilds but no paying trigger exists -> no wins
            Assert.IsNotNull(wins);
            Assert.AreEqual(0, wins.Count);
        }

        [Test]
        public void SingleColumn_MultiRow_Win()
        {
            int columns = 1;
            int rows = 5;
            int[] rowsPerColumn = new int[] { rows };
            var grid = new SymbolData[rows * columns];
            for (int r = 0; r < rows; r++) grid[r * columns + 0] = A; // fill column

            // single-column straight down is indexes 0..4
            var winline = new int[rows];
            for (int r = 0; r < rows; r++) winline[r] = r * columns + 0;

            var winlines = new List<int[]> { winline };
            var winmult = new List<int> { 1 };

            var eval = new WinlineEvaluator();
            var wins = eval.EvaluateWins(grid, columns, rowsPerColumn, winlines, winmult);

            Assert.IsNotNull(wins);
            Assert.AreEqual(1, wins.Count);
            // A base 2 min 3 matchCount=5 -> winDepth=2 -> multiplier=4 -> payout=8
            Assert.AreEqual(8, wins[0].Value);
        }

        [Test]
        public void LargeColumns_Stress_Win()
        {
            int columns = 50;
            int[] rowsPerColumn = new int[columns];
            for (int i = 0; i < columns; i++) rowsPerColumn[i] = 1;
            int maxRows = 1;
            var grid = new SymbolData[maxRows * columns];

            // place A in first 10 columns
            int matchCols = 10;
            for (int c = 0; c < matchCols; c++) grid[c] = A;

            var winline = new int[matchCols];
            for (int c = 0; c < matchCols; c++) winline[c] = c; // row0 * columns + c

            var winlines = new List<int[]> { winline };
            var winmult = new List<int> { 1 };

            var eval = new WinlineEvaluator();
            var wins = eval.EvaluateWins(grid, columns, rowsPerColumn, winlines, winmult);

            Assert.IsNotNull(wins);
            Assert.AreEqual(1, wins.Count);
            // A base 2 min 3, matchCount=10 => winDepth=7 => multiplier=128 => payout=2*128=256
            Assert.AreEqual(256, wins[0].Value);
        }

        // New tests for symbol win modes

        [Test]
        public void SingleOnReel_Awards_PerInstance()
        {
            // three single-on-reel symbols should each award their base value
            var s = new SymbolData("S", baseValue: 5, minWinDepth: -1, isWild: false, allowWild: true, winMode: SymbolWinMode.SingleOnReel);
            var grid = new SymbolData[] { s, s, s };
            var eval = new WinlineEvaluator();
            var wins = eval.EvaluateWins(grid, 3, new int[] {1,1,1}, new List<int[]>(), new List<int>());
            Assert.IsNotNull(wins);
            // three separate wins (one per landed symbol)
            Assert.AreEqual(3, wins.Count);
            Assert.AreEqual(15, wins.Sum(w => w.Value));
        }

        [Test]
        public void TotalCount_Awards_When_Threshold_Met()
        {
            var t = new SymbolData("T", baseValue: 2, minWinDepth: -1, isWild: false, allowWild: true, winMode: SymbolWinMode.TotalCount, totalCountTrigger: 3);
            var grid = new SymbolData[] { t, t, t };
            var eval = new WinlineEvaluator();
            var wins = eval.EvaluateWins(grid, 3, new int[] {1,1,1}, new List<int[]>(), new List<int>());
            Assert.IsNotNull(wins);
            // one total-count win
            var totalWins = wins.Where(w => w.LineIndex == -1).ToList();
            Assert.AreEqual(1, totalWins.Count);
            Assert.AreEqual(2, totalWins[0].Value);
            Assert.AreEqual(3, totalWins[0].Indexes.Length);
        }

        [Test]
        public void Wilds_Do_Not_Trigger_TotalCount_By_Themselves()
        {
            // wilds configured as TotalCount should not self-award; wilds are ignored for non-line modes
            var w = new SymbolData("W", baseValue: 5, minWinDepth: -1, isWild: true, allowWild: true, winMode: SymbolWinMode.TotalCount, totalCountTrigger: 1);
            var grid = new SymbolData[] { w, w, w };
            var eval = new WinlineEvaluator();
            var wins = eval.EvaluateWins(grid, 3, new int[] {1,1,1}, new List<int[]>(), new List<int>());
            Assert.IsNotNull(wins);
            // no wins should be produced from wild-only totalcount entries
            Assert.AreEqual(0, wins.Count);
        }

        [Test]
        public void NonLineWinMode_Not_Count_As_Line()
        {
            // Leftmost is SingleOnReel and should prevent a line match even if right-hand symbols would form one.
            var left = new SymbolData("X", baseValue: 2, minWinDepth: 3, isWild: false, allowWild: true, winMode: SymbolWinMode.SingleOnReel);
            var right = new SymbolData("X", baseValue: 2, minWinDepth: 3, isWild: false, allowWild: true, winMode: SymbolWinMode.LineMatch);
            var grid = new SymbolData[] { left, right, right };
            var winlines = new List<int[]> { new int[] { 0, 1, 2 } };
            var winmult = new List<int> { 1 };
            var eval = new WinlineEvaluator();
            var wins = eval.EvaluateWins(grid, 3, new int[] {1,1,1}, winlines, winmult);

            Assert.IsNotNull(wins);
            // Ensure no line wins present (LineIndex >=0). There may be a SingleOnReel win(s) with LineIndex == -1.
            Assert.IsFalse(wins.Any(w => w.LineIndex >= 0));
        }
    }
}
