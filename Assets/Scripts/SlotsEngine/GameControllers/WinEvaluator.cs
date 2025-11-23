using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using System.IO;
using System.Text;
#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// Evaluates slot machine wins across a grid of symbols and provides optional detailed logging.
///
/// Features:
/// - Supports arbitrary number of reels and non-uniform rows per reel (variable symbol counts per column).
/// - Line wins always start from the leftmost column (column 0) and cannot skip gaps.
/// - Respects each symbol's `MinWinDepth`, `PayScaling`, and wild matching behavior (`Matches`).
/// - Supports additional non-line win modes via `SymbolWinMode`:
///   - `SingleOnReel`: awards per landed instance of a non-wild symbol.
///   - `TotalCount`: awards when the total count of a symbol group on the grid reaches a configured threshold (wilds ignored).
/// - Gracefully handles reel additions/removals and irregular grids.
/// - Optional editor/development logging to Unity Console and persistent log files when `LoggingEnabled` is true.
///
/// Grid indexing used across the project is row-major: index = row * columns + column (row 0 = bottom, column 0 = left).
/// </summary>
public class WinEvaluator : Singleton<WinEvaluator>
{
    // Simple public toggle visible in the inspector so it can be enabled/disabled in the Scene Editor.
    [Tooltip("Enable detailed winline logging (clear console at spin start when enabled).")]
    public bool LoggingEnabled = false; // default to false to avoid noisy logs in builds

    private List<WinData> currentSpinWinData;
    private bool clearedConsoleForCurrentSpin;

    // File logging state
    private string currentSpinLogFilePath;
    private static int spinCounter = 0;

    /// <summary>
    /// The most recent set of win results produced by an evaluation. Never null.
    /// </summary>
    public List<WinData> CurrentSpinWinData => currentSpinWinData ?? (currentSpinWinData = new List<WinData>());

    /// <summary>
    /// Notify the evaluator that a new spin has started. When logging is enabled the evaluator
    /// will clear the Unity console once at the start of the spin to keep logs focused.
    /// Call this from your spin-start logic (for example `SlotsEngine.SpinOrStopReels`).
    /// Also rotates persistent spin log files and initializes a new per-spin log.
    /// </summary>
    public void NotifySpinStarted()
    {
        clearedConsoleForCurrentSpin = false;
        spinCounter++;

        try
        {
            string dir = Path.Combine(Application.persistentDataPath, "SpinLogs");
            if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
            string filename = $"spin_{DateTime.Now:yyyyMMdd_HHmmss_fff}_{spinCounter}.log";
            currentSpinLogFilePath = Path.Combine(dir, filename);

            // create empty file (write once)
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            File.WriteAllText(currentSpinLogFilePath, $"Spin log started: {DateTime.Now:O}{Environment.NewLine}");
#endif

            // Prune old logs in persistent dir, keep only the 5 most recent
            PruneOldSpinLogs(dir, 5);

            if (LoggingEnabled)
            {
#if UNITY_EDITOR
                // Clear editor console once at spin start to make logs easier to follow
                ClearConsole();
#endif
            }
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"Failed to initialize spin log file: {ex.Message}");
            currentSpinLogFilePath = null;
        }
    }

    /// <summary>
    /// Delete older spin log files in the given directory, keeping only the most recent `keep` files.
    /// </summary>
    /// <param name="directory">Directory to prune</param>
    /// <param name="keep">Number of most recent files to keep</param>
    private void PruneOldSpinLogs(string directory, int keep)
    {
        try
        {
            if (string.IsNullOrEmpty(directory) || !Directory.Exists(directory)) return;
            var files = Directory.GetFiles(directory, "spin_*.log");
            if (files == null || files.Length <= keep) return;

            var ordered = files.Select(p => new FileInfo(p))
                               .OrderByDescending(fi => fi.LastWriteTimeUtc)
                               .ToList();

            for (int i = keep; i < ordered.Count; i++)
            {
                try
                {
                    ordered[i].Delete();
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"Failed to delete old spin log '{ordered[i].FullName}': {ex.Message}");
                }
            }
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"PruneOldSpinLogs failed: {ex.Message}");
        }
    }

    /// <summary>
    /// Clears the Unity Editor console once per spin when logging is enabled (Editor only).
    /// </summary>
    private void ClearConsole()
    {
#if UNITY_EDITOR
        if (clearedConsoleForCurrentSpin) return;
        try
        {
            var logEntries = Type.GetType("UnityEditor.LogEntries, UnityEditor.dll");
            var clearMethod = logEntries?.GetMethod("Clear", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public);
            clearMethod?.Invoke(null, null);
            clearedConsoleForCurrentSpin = true;
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"Failed to clear editor console: {ex.Message}");
        }
#endif
    }

    /// <summary>
    /// Appends the provided StringBuilder contents to the current spin log file (Editor/Dev builds only).
    /// </summary>
    private void AppendToCurrentSpinLog(StringBuilder builder)
    {
        if (builder == null) return;

        if (string.IsNullOrEmpty(currentSpinLogFilePath))
        {
            try { NotifySpinStarted(); } catch { }
            if (string.IsNullOrEmpty(currentSpinLogFilePath)) return;
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        try
        {
            File.AppendAllText(currentSpinLogFilePath, builder.ToString(), Encoding.UTF8);
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"Failed to write to persistent spin log file: {ex.Message}");
        }
#endif
    }

    /// <summary>
    /// Write a human-readable record of the presented grid, winlines under evaluation and resulting win data to the current spin log file.
    /// Safe to call even if logging wasn't enabled; will still create the file if NotifySpinStarted wasn't called.
    /// Only writes when `LoggingEnabled` is true to avoid unnecessary IO.
    /// </summary>
    public void LogSpinResult(GameSymbol[] grid, int columns, int[] rowsPerColumn, List<WinlineDefinition> winlines, List<WinData> winData)
    {
        // If logging is disabled, do nothing to avoid creating log files or printing paths.
        if (!LoggingEnabled)
            return;

        try
        {
            var sb = new StringBuilder();

            sb.AppendLine("---- Spin Result ----");
            sb.AppendLine($"Timestamp: {DateTime.Now:O}");
            sb.AppendLine($"Columns: {columns}");

            if (rowsPerColumn != null)
            {
                sb.Append("RowsPerColumn: [");
                for (int i = 0; i < rowsPerColumn.Length; i++)
                {
                    if (i > 0) sb.Append(",");
                    sb.Append(rowsPerColumn[i]);
                }
                sb.AppendLine("]");
            }
            else
            {
                sb.AppendLine("RowsPerColumn: []");
            }

            sb.AppendLine();
            sb.AppendLine("Grid contents (index => name | baseValue | minWinDepth | isWild | allowWildMatch):");

            if (grid != null)
            {
                for (int i = 0; i < grid.Length; i++)
                {
                    var gs = grid[i];
                    if (gs == null)
                    {
                        sb.AppendLine($"[{i}] => null");
                        continue;
                    }

                    var sd = gs.CurrentSymbolData;
                    string name = sd != null ? sd.Name : "(null)";
                    int baseVal = sd != null ? sd.BaseValue : 0;
                    int min = sd != null ? sd.MinWinDepth : -999;
                    bool isWild = sd != null ? sd.IsWild : false;
                    bool allowWild = sd != null ? sd.AllowWildMatch : false;
                    sb.AppendLine($"[{i}] => {name} | base={baseVal} | minWin={min} | isWild={isWild} | allowWild={allowWild}");
                }
            }
            else
            {
                sb.AppendLine("Grid is null");
            }

            sb.AppendLine();
            sb.AppendLine("Evaluated Winlines (asset name / generated indexes):");
            if (winlines != null)
            {
                for (int wi = 0; wi < winlines.Count; wi++)
                {
                    var wl = winlines[wi];
                    int[] concrete = wl.GenerateIndexes(columns, rowsPerColumn ?? new int[columns]);
                    if (concrete == null) concrete = new int[0];

                    sb.Append("Winline["); sb.Append(wi); sb.Append(") '"); sb.Append(wl.name); sb.Append("' pattern="); sb.Append(wl.Pattern); sb.Append(" concrete[");
                    for (int c = 0; c < concrete.Length; c++)
                    {
                        if (c > 0) sb.Append(","); sb.Append(concrete[c]);
                    }
                    sb.AppendLine("]");
                }
            }

            sb.AppendLine();
            sb.AppendLine("Win evaluation results:");

            if (winData != null && winData.Count > 0)
            {
                int total = 0;
                for (int i = 0; i < winData.Count; i++)
                {
                    var w = winData[i];
                    total += w.WinValue;

                    // Build names array without LINQ allocations
                    var idxs = w.WinningSymbolIndexes ?? new int[0];
                    sb.Append($"Win[{i}] LineIndex={w.LineIndex} WinValue={w.WinValue} Indexes[");
                    for (int x = 0; x < idxs.Length; x++)
                    {
                        if (x > 0) sb.Append(",");
                        sb.Append(idxs[x]);
                    }
                    sb.Append("] names[");

                    for (int x = 0; x < idxs.Length; x++)
                    {
                        if (x > 0) sb.Append(",");
                        int idx = idxs[x];
                        if (idx >= 0 && grid != null && idx < grid.Length && grid[idx] != null && grid[idx].CurrentSymbolData != null)
                            sb.Append(grid[idx].CurrentSymbolData.Name ?? "(null)");
                        else
                            sb.Append("null");
                    }
                    sb.AppendLine("]");
                }
                sb.AppendLine($"TotalWin={total}");
            }
            else
            {
                sb.AppendLine("No wins");
            }

            sb.AppendLine("---- End Spin ----\n\n");

            // Persist to file in one operation
            AppendToCurrentSpinLog(sb);

            // Also print location for convenience (editor/log) - omitted to avoid noise
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"Exception while logging spin result: {ex.Message}");
        }
    }

    /// <summary>
    /// Evaluate wins for a rectangular grid represented in row-major layout.
    /// Rules:
    /// 1. Wins must start at column 0 (leftmost reel)
    /// 2. The leftmost symbol determines what must match along the winline
    /// 3. Matching continues left-to-right until a non-match or grid boundary
    /// 4. Win is valid if match count &gt;= trigger symbol's MinWinDepth
    /// 5. Wild symbols match according to SymbolData.Matches() logic
    /// </summary>
    /// <param name="grid">Row-major symbol grid (size = maxRows * columns)</param>
    /// <param name="columns">Number of columns (reels)</param>
    /// <param name="rowsPerColumn">Actual rows available in each column (handles varying reel sizes)</param>
    /// <param name="winlines">List of winline patterns to evaluate</param>
    /// <returns>List of wins detected this evaluation</returns>
    public List<WinData> EvaluateWins(SymbolData[] grid, int columns, int[] rowsPerColumn, List<WinlineDefinition> winlines)
    {
        List<WinData> winData = new List<WinData>();

        if (grid == null || grid.Length == 0 || winlines == null || winlines.Count == 0)
        {
            currentSpinWinData = winData;
            return winData;
        }

        // validate dimensions
        if (columns <= 0 || rowsPerColumn == null || rowsPerColumn.Length != columns)
        {
            currentSpinWinData = winData;
            return winData;
        }

        int maxRows = 0;
        for (int i = 0; i < rowsPerColumn.Length; i++) if (rowsPerColumn[i] > maxRows) maxRows = rowsPerColumn[i];
        int expectedGridSize = maxRows * columns;

        // grid returned by CombineColumnsToGrid will use maxRows * columns layout; if caller provided such grid, accept it.
        if (grid.Length != expectedGridSize)
        {
            // If grid length doesn't match expected rectangular grid, attempt graceful fallback by padding/resizing.
            Array.Resize(ref grid, expectedGridSize);
        }

        // If logging is enabled and we haven't yet cleared for the current spin, clear once now.
        if (LoggingEnabled && !clearedConsoleForCurrentSpin)
        {
            ClearConsole();
            clearedConsoleForCurrentSpin = true;
        }

        for (int i = 0; i < winlines.Count; i++)
        {
            WinlineDefinition winlineDef = winlines[i];
            var concrete = winlineDef.GenerateIndexes(columns, rowsPerColumn);

            if (concrete == null || concrete.Length == 0) continue;

            // Enforce wins always begin on the leftmost column: only concrete[0] can be the trigger
            int firstIndex = concrete.Length > 0 ? concrete[0] : -1;
            if (firstIndex < 0 || firstIndex >= grid.Length)
            {
                continue;
            }

            var trigger = grid[firstIndex];
            if (trigger == null)
            {
                continue;
            }

            // Fallback: also treat a leftmost wild with non-positive BaseValue as needing substitution.
            bool needsFallback = (trigger.MinWinDepth < 0 || trigger.WinMode != SymbolWinMode.LineMatch) || (trigger.IsWild && trigger.BaseValue <= 0);
            if (needsFallback)
            {
                if (trigger.IsWild)
                {
                    int altIndex = -1;
                    for (int s = 1; s < concrete.Length; s++)
                    {
                        int si = concrete[s]; if (si < 0 || si >= grid.Length) continue;
                        int col = si % columns; int row = si / columns; if (row >= rowsPerColumn[col]) continue;
                        var cand = grid[si]; if (cand == null) continue;
                        if (!cand.IsWild && cand.MinWinDepth >= 0 && cand.BaseValue > 0 && cand.WinMode == SymbolWinMode.LineMatch)
                        { altIndex = si; trigger = cand; break; }
                    }
                    if (altIndex >= 0)
                    {
                        // no-op
                    }
                    else
                    {
                        continue;
                    }
                }
                else
                {
                    continue;
                }
            }

            // If the resolved trigger has no positive BaseValue it cannot award a win; skip early
            if (trigger.BaseValue <= 0)
            {
                continue;
            }

            // Attempt to build the winning sequence starting from column 0
            var winningIndexes = new List<int>();
            for (int k = 0; k < concrete.Length; k++)
            {
                int symbolIndex = concrete[k];
                if (symbolIndex < 0 || symbolIndex >= grid.Length) break;

                int col = symbolIndex % columns;
                int row = symbolIndex / columns;
                // If the column doesn't contain that row (truncated reel), stop matching
                if (row >= rowsPerColumn[col]) break;

                var cell = grid[symbolIndex];
                if (cell == null) break;

                // Use new MatchGroup-aware Matches logic (SymbolData.Matches already considers MatchGroup)
                if (cell.Matches(trigger))
                {
                    winningIndexes.Add(symbolIndex);
                }
                else
                {
                    break; // Gap detected - terminate this winline
                }
            }

            int matchCount = winningIndexes.Count;

            // Validate: sufficient depth and valid base value exists
            if (matchCount >= trigger.MinWinDepth)
            {
                int baseVal = trigger.BaseValue;
                // baseVal already checked earlier; keep check here in case trigger was changed
                if (baseVal <= 0)
                {
                    continue;
                }

                // Compute scaled payout according to PayScaling. Depth-based scaling starts counting at the triggered win depth index.
                int winDepth = matchCount - trigger.MinWinDepth; // 0-based additional depth beyond min
                long scaled = baseVal;
                switch (trigger.PayScaling)
                {
                    case PayScaling.DepthSquared:
                    {
                        long multiplier = 1L << winDepth; // 2^winDepth
                        scaled = baseVal * multiplier;
                        break;
                    }
                    case PayScaling.PerSymbol:
                    {
                        // Pay base value times number of matched symbols for line wins
                        // Important: trigger (min depth) still uses contiguous prefix (matchCount) to determine if a win is awarded.
                        // For PerSymbol on LineMatch wins we calculate payout using the total number of matching symbols across the
                        // entire evaluated winline pattern (concrete), not only the contiguous prefix. This preserves trigger semantics
                        // but allows symbols later in the line to contribute to the per-symbol payout.
                        int totalMatchesAcrossLine = 0;
                        for (int c = 0; c < concrete.Length; c++)
                        {
                            int idx = concrete[c];
                            if (idx < 0 || idx >= grid.Length) continue;
                            int col = idx % columns;
                            int row = idx / columns;
                            if (row >= rowsPerColumn[col]) continue;
                            var cellForCount = grid[idx];
                            if (cellForCount == null) continue;
                            if (cellForCount.Matches(trigger)) totalMatchesAcrossLine++;
                        }

                        // Fallback to contiguous matchCount if something unexpected happens (defensive)
                        if (totalMatchesAcrossLine <= 0) totalMatchesAcrossLine = matchCount;
                        scaled = (long)baseVal * totalMatchesAcrossLine;
                        break;
                    }
                    default:
                        scaled = baseVal;
                        break;
                }

                // Apply line multiplier and credit cost
                long creditCost = 1;
                if (GamePlayer.Instance != null && GamePlayer.Instance.CurrentBet != null)
                    creditCost = GamePlayer.Instance.CurrentBet.CreditCost;

                long total = scaled * winlineDef.WinMultiplier * creditCost;
                if (total > int.MaxValue) total = int.MaxValue;
                int finalValue = (int)total;

                winData.Add(new WinData(i, finalValue, winningIndexes.ToArray()));
            }
        }

        // --- Evaluate symbol-level win modes (SingleOnReel, TotalCount) ---
        try
        {
            // We'll use -1 as the LineIndex to indicate a non-winline award
            const int NonWinlineIndex = -1;

            // Keep track of TotalCount triggers we've already evaluated by matchgroup id
            HashSet<int> totalCountProcessed = new HashSet<int>();

            for (int idx = 0; idx < grid.Length; idx++)
            {
                var cell = grid[idx];
                if (cell == null) continue;

                // Ignore wild symbols for non-line win modes entirely
                if (cell.IsWild) continue;

                // SingleOnReel: award per landed non-wild symbol instance
                if (cell.WinMode == SymbolWinMode.SingleOnReel)
                {
                    if (cell.BaseValue <= 0) continue;

                    long creditCost = 1;
                    if (GamePlayer.Instance != null && GamePlayer.Instance.CurrentBet != null)
                        creditCost = GamePlayer.Instance.CurrentBet.CreditCost;

                    // Apply PayScaling for SingleOnReel; PerSymbol == baseValue * 1, DepthSquared has no extra depth for single instance
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

                    long total = scaled * creditCost;
                    if (total > int.MaxValue) total = int.MaxValue;
                    int finalValue = (int)total;

                    winData.Add(new WinData(NonWinlineIndex, finalValue, new int[] { idx }));
                }

                // TotalCount: evaluate once per match-group id rather than per symbol name
                if (cell.WinMode == SymbolWinMode.TotalCount)
                {
                    int groupId = cell.MatchGroupId;
                    if (totalCountProcessed.Contains(groupId)) continue;
                    totalCountProcessed.Add(groupId);

                    if (cell.TotalCountTrigger <= 0) continue;
                    if (cell.BaseValue <= 0) continue;

                    // Gather all indexes in the grid that match this symbol by MatchGroupId. Ignore wilds entirely.
                    var matching = new List<int>();
                    int exactMatches = 0;
                    for (int j = 0; j < grid.Length; j++)
                    {
                        var other = grid[j];
                        if (other == null) continue;
                        if (other.IsWild) continue; // explicitly ignore wilds for TotalCount

                        // Use MatchGroupId equality for TotalCount matching to count grouped symbols together
                        if (other.MatchGroupId != 0 && other.MatchGroupId == groupId)
                        {
                            matching.Add(j);
                            if (!string.IsNullOrEmpty(other.Name)) exactMatches++;
                        }
                    }

                    if (exactMatches == 0)
                    {
                        continue;
                    }

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
                                // Pay baseValue times the number of matched symbols for TotalCount wins
                                scaled = (long)cell.BaseValue * count;
                                break;
                            default:
                                scaled = cell.BaseValue;
                                break;
                        }

                        long creditCost = 1;
                        if (GamePlayer.Instance != null && GamePlayer.Instance.CurrentBet != null)
                            creditCost = GamePlayer.Instance.CurrentBet.CreditCost;

                        long total = scaled * creditCost;
                        if (total > int.MaxValue) total = int.MaxValue;
                        int finalValue = (int)total;

                        winData.Add(new WinData(NonWinlineIndex, finalValue, matching.ToArray()));
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"SymbolWin evaluation exception: {ex.Message}");
        }

        currentSpinWinData = winData;
        return winData;
    }

    /// <summary>
    /// Convenience overload to evaluate from visual `GameSymbol` grid. Converts to `SymbolData[]` and delegates to `EvaluateWins`.
    /// `rowsPerColumn` should reflect the number of valid rows in each column (may be non-uniform).
    /// </summary>
    public List<WinData> EvaluateWinsFromGameSymbols(GameSymbol[] gameSymbols, int columns, int[] rowsPerColumn, List<WinlineDefinition> winlines)
    {
        if (gameSymbols == null) return new List<WinData>();
        // Convert visual symbols to runtime SymbolData array; extension ToSymbolDatas handles nulls.
        SymbolData[] grid = gameSymbols.ToSymbolDatas();
        return EvaluateWins(grid, columns, rowsPerColumn, winlines);
    }

    /// <summary>
    /// Convenience overload to evaluate from per-column `GameSymbol` arrays. Handles varying reel sizes and constructs the row-major grid.
    /// </summary>
    public List<WinData> EvaluateWinsFromColumns(List<GameSymbol[]> columns, List<WinlineDefinition> winlines)
    {
        if (columns == null || columns.Count == 0) return new List<WinData>();

        int cols = columns.Count;
        int[] rowsPerColumn = new int[cols];
        for (int c = 0; c < cols; c++) rowsPerColumn[c] = columns[c] != null ? columns[c].Length : 0;

        // Build row-major grid using helper
        GameSymbol[] gridSymbols = Helpers.CombineColumnsToGrid(columns);

        return EvaluateWinsFromGameSymbols(gridSymbols, cols, rowsPerColumn, winlines);
    }

    /// <summary>
    /// Backwards-compatible overload for uniform rows per column. Constructs a per-column array and delegates to the main overload.
    /// </summary>
    public List<WinData> EvaluateWins(SymbolData[] grid, int columns, int rows, List<WinlineDefinition> winlines)
    {
        int[] perCol = new int[columns];
        for (int i = 0; i < columns; i++) perCol[i] = rows;
        return EvaluateWins(grid, columns, perCol, winlines);
    }
}
