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
/// Evaluates slot machine winlines across a grid of symbols.
/// 
/// Key Features:
/// • Supports arbitrary number of reels and non-uniform symbols per reel
/// • Wins always start from the leftmost column (column 0)
/// • A gap in matching symbols terminates the win (no skipping)
/// • Respects each symbol's MinWinDepth requirement
/// • Supports Wild symbols that substitute for other symbols
/// • Dynamic - handles reel additions/removals gracefully
/// </summary>
public class WinlineEvaluator : Singleton<WinlineEvaluator>
{
    // Simple public toggle visible in the inspector so it can be enabled/disabled in the Scene Editor.
    [Tooltip("Enable detailed winline logging (clear console at spin start when enabled).")]
    public bool LoggingEnabled = true;

    private List<WinData> currentSpinWinData;
    private bool clearedConsoleForCurrentSpin;

    // File logging state
    private string currentSpinLogFilePath;
    private static int spinCounter = 0;

    public List<WinData> CurrentSpinWinData => currentSpinWinData ?? (currentSpinWinData = new List<WinData>());

    /// <summary>
    /// Notify the evaluator that a new spin has started. When logging is enabled the evaluator
    /// will clear the Unity console once at the start of the spin to keep logs focused.
    /// Call this from your spin-start logic (for example SlotsEngine/SpinOrStopReels).
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

            // create empty file
            File.WriteAllText(currentSpinLogFilePath, $"Spin log started: {DateTime.Now:O}{Environment.NewLine}");

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
                               .OrderByDescending(fi => fi.CreationTimeUtc)
                               .ToList();

            for (int i = keep; i < ordered.Count; i++)
            {
                try
                {
                    ordered[i].Delete();
                }
                catch { /* best-effort cleanup */ }
            }
        }
        catch { /* ignore prune failures */ }
    }

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

    private void AppendToCurrentSpinLog(string text)
    {
        if (string.IsNullOrEmpty(currentSpinLogFilePath))
        {
            // ensure a file exists
            try
            {
                NotifySpinStarted();
            }
            catch { }
            if (string.IsNullOrEmpty(currentSpinLogFilePath)) return;
        }

        // Append to persistent data path log
        try
        {
            if (!string.IsNullOrEmpty(currentSpinLogFilePath))
                File.AppendAllText(currentSpinLogFilePath, text + Environment.NewLine, Encoding.UTF8);
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"Failed to write to persistent spin log file: {ex.Message}");
        }
    }

    /// <summary>
    /// Write a human-readable record of the presented grid, winlines under evaluation and resulting win data to the current spin log file.
    /// Safe to call even if logging wasn't enabled; will still create the file if NotifySpinStarted wasn't called.
    /// </summary>
    public void LogSpinResult(GameSymbol[] grid, int columns, int[] rowsPerColumn, List<WinlineDefinition> winlines, List<WinData> winData)
    {
        try
        {
            AppendToCurrentSpinLog("---- Spin Result ----");
            AppendToCurrentSpinLog($"Timestamp: {DateTime.Now:O}");
            AppendToCurrentSpinLog($"Columns: {columns}");
            AppendToCurrentSpinLog($"RowsPerColumn: [{(rowsPerColumn != null ? string.Join(",", rowsPerColumn) : string.Empty)}]");

            AppendToCurrentSpinLog("\nGrid contents (index => name | baseValue | minWinDepth | isWild | allowWildMatch):");
            if (grid != null)
            {
                for (int i = 0; i < grid.Length; i++)
                {
                    var gs = grid[i];
                    if (gs == null)
                    {
                        AppendToCurrentSpinLog($"[{i}] => null");
                        continue;
                    }

                    var sd = gs.CurrentSymbolData;
                    string name = sd != null ? sd.Name : "(null)";
                    int baseVal = sd != null ? sd.BaseValue : 0;
                    int min = sd != null ? sd.MinWinDepth : -999;
                    bool isWild = sd != null ? sd.IsWild : false;
                    bool allowWild = sd != null ? sd.AllowWildMatch : false;
                    AppendToCurrentSpinLog($"[{i}] => {name} | base={baseVal} | minWin={min} | isWild={isWild} | allowWild={allowWild}");
                }
            }
            else
            {
                AppendToCurrentSpinLog("Grid is null");
            }

            AppendToCurrentSpinLog("\nEvaluated Winlines (asset name / generated indexes):");
            if (winlines != null)
            {
                for (int wi = 0; wi < winlines.Count; wi++)
                {
                    var wl = winlines[wi];
                    int[] concrete = wl.GenerateIndexes(columns, rowsPerColumn ?? new int[columns]);
                    if (concrete == null) concrete = new int[0];
                    AppendToCurrentSpinLog($"Winline[{wi}] '{wl.name}' pattern={wl.Pattern} concrete=[{string.Join(",", concrete)}]");
                }
            }

            AppendToCurrentSpinLog("\nWin evaluation results:");
            if (winData != null && winData.Count > 0)
            {
                int total = 0;
                for (int i = 0; i < winData.Count; i++)
                {
                    var w = winData[i];
                    total += w.WinValue;
                    var names = (w.WinningSymbolIndexes ?? new int[0]).Select(idx => (idx >= 0 && idx < (grid?.Length ?? 0) && grid[idx] != null) ? grid[idx].CurrentSymbolData?.Name ?? "(null)" : "null").ToArray();
                    AppendToCurrentSpinLog($"Win[{i}] LineIndex={w.LineIndex} WinValue={w.WinValue} Indexes=[{(w.WinningSymbolIndexes != null ? string.Join(",", w.WinningSymbolIndexes) : "")} ] names=[{string.Join(",", names)}]");
                }
                AppendToCurrentSpinLog($"TotalWin={total}");
            }
            else
            {
                AppendToCurrentSpinLog("No wins");
            }

            AppendToCurrentSpinLog("---- End Spin ----\n\n");

            // Also print location for convenience
            if (!string.IsNullOrEmpty(currentSpinLogFilePath))
            {
                Debug.Log($"Spin log written to: {currentSpinLogFilePath}");
            }
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"Exception while logging spin result: {ex.Message}");
        }
    }

    /// <summary>
    /// Evaluate wins for a rectangular grid represented in column-major layout.
    /// Grid indexing: index = row * columns + column (row 0 = bottom, column 0 = left).
    /// 
    /// Rules:
    /// 1. Wins must start at column 0 (leftmost reel)
    /// 2. The leftmost symbol determines what must match along the winline
    /// 3. Matching continues left-to-right until a non-match or grid boundary
    /// 4. Win is valid if match count >= trigger symbol's MinWinDepth
    /// 5. Wild symbols match according to SymbolData.Matches() logic
    /// </summary>
    /// <param name="grid">Column-major symbol grid (size = maxRows * columns)</param>
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

        int maxRows = rowsPerColumn.Max();
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
                if (LoggingEnabled && (Application.isEditor || Debug.isDebugBuild))
                    Debug.Log($"Winline {i}: leftmost cell invalid or missing (idx={firstIndex}).");
                continue;
            }

            var trigger = grid[firstIndex];
            if (trigger == null)
            {
                if (LoggingEnabled && (Application.isEditor || Debug.isDebugBuild))
                    Debug.Log($"Winline {i}: leftmost cell is null (idx={firstIndex}).");
                continue;
            }

            // If the trigger isn't a LineMatch-capable symbol, skip line evaluation.
            // However allow wild leftmost to hand off to a LineMatch candidate further along the line.
            if (trigger.MinWinDepth < 0 || trigger.WinMode != SymbolWinMode.LineMatch)
            {
                if (trigger.IsWild)
                {
                    int altIndex = -1;
                    for (int s = 1; s < concrete.Length; s++)
                    {
                        int si = concrete[s];
                        if (si < 0 || si >= grid.Length) continue; // skip invalid entries, continue searching
                        int col = si % columns;
                        int row = si / columns;
                        if (row >= rowsPerColumn[col]) continue; // truncated column - skip and continue

                        var candidate = grid[si];
                        if (candidate == null) continue;
                        // Prefer the first non-wild symbol that can trigger line wins and has a positive BaseValue
                        if (!candidate.IsWild && candidate.MinWinDepth >= 0 && candidate.BaseValue > 0 && candidate.WinMode == SymbolWinMode.LineMatch)
                        {
                            altIndex = si;
                            break;
                        }
                    }

                    if (altIndex >= 0)
                    {
                        if (LoggingEnabled && (Application.isEditor || Debug.isDebugBuild))
                            Debug.Log($"Winline {i}: leftmost is wild without multipliers; using symbol at idx={altIndex} ('{grid[altIndex].Name}') as trigger.");
                        trigger = grid[altIndex];
                    }
                    else
                    {
                        if (LoggingEnabled && (Application.isEditor || Debug.isDebugBuild))
                            Debug.Log($"Winline {i}: leftmost symbol '{trigger.Name}' cannot trigger line wins (MinWinDepth={trigger.MinWinDepth}, WinMode={trigger.WinMode}).");
                        continue;
                    }
                }
                else
                {
                    if (LoggingEnabled && (Application.isEditor || Debug.isDebugBuild))
                        Debug.Log($"Winline {i}: leftmost symbol '{trigger.Name}' cannot trigger line wins (MinWinDepth={trigger.MinWinDepth}, WinMode={trigger.WinMode}).");
                    continue;
                }
            }

            // If the resolved trigger has no positive BaseValue it cannot award a win; skip early
            if (trigger.BaseValue <= 0)
            {
                if (LoggingEnabled && (Application.isEditor || Debug.isDebugBuild))
                    Debug.Log($"Winline {i}: trigger '{trigger.Name}' cannot award wins because BaseValue={trigger.BaseValue}.");
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
                    if (LoggingEnabled && (Application.isEditor || Debug.isDebugBuild))
                        Debug.Log($"Winline {i}: trigger '{trigger.Name}' has non-positive BaseValue ({baseVal}).");
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
                    default:
                        scaled = baseVal;
                        break;
                }

                // Apply line multiplier and credit cost
                long total = scaled * winlineDef.WinMultiplier * (long)GamePlayer.Instance.CurrentBet.CreditCost;
                if (total > int.MaxValue) total = int.MaxValue;
                int finalValue = (int)total;

                winData.Add(new WinData(i, finalValue, winningIndexes.ToArray()));

                if (LoggingEnabled && (Application.isEditor || Debug.isDebugBuild))
                    Debug.Log($"Winline {i}: WIN! trigger={trigger.Name} matches={matchCount} baseValue={baseVal} scaled={scaled} lineMultiplier={winlineDef.WinMultiplier} totalValue={finalValue}");
            }
            else
            {
                if (LoggingEnabled && (Application.isEditor || Debug.isDebugBuild))
                    Debug.Log($"Winline {i}: insufficient matches - found={matchCount} required={trigger.MinWinDepth}.");
            }
        }

        // --- New: evaluate symbol-level win modes (SingleOnReel, TotalCount) ---
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

                    long total = (long)cell.BaseValue * (long)GamePlayer.Instance.CurrentBet.CreditCost;
                    if (total > int.MaxValue) total = int.MaxValue;
                    int finalValue = (int)total;

                    winData.Add(new WinData(NonWinlineIndex, finalValue, new int[] { idx }));

                    if (LoggingEnabled && (Application.isEditor || Debug.isDebugBuild))
                        Debug.Log($"SymbolWin: SingleOnReel trigger={cell.Name} at idx={idx} value={finalValue}");

                    // continue; allow multiple SingleOnReel instances across grid
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
                        if (LoggingEnabled && (Application.isEditor || Debug.isDebugBuild))
                            Debug.Log($"SymbolWin: TotalCount trigger '{cell.Name}' skipped because no exact symbol instances were present (wilds ignored). GroupId={groupId}");
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
                            default:
                                scaled = cell.BaseValue;
                                break;
                        }

                        long total = scaled * (long)GamePlayer.Instance.CurrentBet.CreditCost;
                        if (total > int.MaxValue) total = int.MaxValue;
                        int finalValue = (int)total;

                        winData.Add(new WinData(NonWinlineIndex, finalValue, matching.ToArray()));

                        if (LoggingEnabled && (Application.isEditor || Debug.isDebugBuild))
                            Debug.Log($"SymbolWin: TotalCount WIN! trigger={cell.Name} count={count} required={cell.TotalCountTrigger} baseValue={cell.BaseValue} scaled={scaled} totalValue={finalValue} GroupId={groupId}");
                    }
                    else
                    {
                        if (LoggingEnabled && (Application.isEditor || Debug.isDebugBuild))
                            Debug.Log($"SymbolWin: TotalCount '{cell.Name}' not reached ({count}/{cell.TotalCountTrigger}). GroupId={groupId}");
                    }
                }
            }
        }
        catch (Exception ex)
        {
            if (LoggingEnabled && (Application.isEditor || Debug.isDebugBuild)) Debug.Log($"SymbolWin evaluation exception: {ex.Message}");
        }

        currentSpinWinData = winData;

        return winData;
    }

    /// <summary>
    /// Convenience overload to evaluate from visual GameSymbol grid. Converts to SymbolData[] and delegates to EvaluateWins.
    /// rowsPerColumn should reflect the number of valid rows in each column (may be non-uniform).
    /// </summary>
    public List<WinData> EvaluateWinsFromGameSymbols(GameSymbol[] gameSymbols, int columns, int[] rowsPerColumn, List<WinlineDefinition> winlines)
    {
        if (gameSymbols == null) return new List<WinData>();
        // Convert visual symbols to runtime SymbolData array; extension ToSymbolDatas handles nulls.
        SymbolData[] grid = gameSymbols.ToSymbolDatas();
        return EvaluateWins(grid, columns, rowsPerColumn, winlines);
    }

    /// <summary>
    /// Convenience overload to evaluate from per-column GameSymbol arrays. Handles varying reel sizes and constructs grid.
    /// </summary>
    public List<WinData> EvaluateWinsFromColumns(List<GameSymbol[]> columns, List<WinlineDefinition> winlines)
    {
        if (columns == null || columns.Count == 0) return new List<WinData>();

        int cols = columns.Count;
        int[] rowsPerColumn = new int[cols];
        for (int c = 0; c < cols; c++) rowsPerColumn[c] = columns[c] != null ? columns[c].Length : 0;

        // Build column-major grid using helper
        GameSymbol[] gridSymbols = Helpers.CombineColumnsToGrid(columns);

        return EvaluateWinsFromGameSymbols(gridSymbols, cols, rowsPerColumn, winlines);
    }

    /// <summary>
    /// Backwards-compatible overload for uniform rows per column.
    /// Constructs a per-column array and delegates to the main overload.
    /// </summary>
    public List<WinData> EvaluateWins(SymbolData[] grid, int columns, int rows, List<WinlineDefinition> winlines)
    {
        int[] perCol = new int[columns];
        for (int i = 0; i < columns; i++) perCol[i] = rows;
        return EvaluateWins(grid, columns, perCol, winlines);
    }
}
