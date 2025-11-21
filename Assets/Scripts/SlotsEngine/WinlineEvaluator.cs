using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
using System.Reflection;
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
    public List<WinData> CurrentSpinWinData => currentSpinWinData;

    // Internal flag to ensure we only clear the console once per spin when logging is enabled.
    private bool clearedConsoleForCurrentSpin = false;

    /// <summary>
    /// Notify the evaluator that a new spin has started. When logging is enabled the evaluator
    /// will clear the Unity console once at the start of the spin to keep logs focused.
    /// Call this from your spin-start logic (for example SlotsEngine/SpinOrStopReels).
    /// </summary>
    public void NotifySpinStarted()
    {
        // Reset the cleared flag and immediately clear if logging is enabled.
        clearedConsoleForCurrentSpin = false;
        if (LoggingEnabled)
        {
            ClearConsole();
            clearedConsoleForCurrentSpin = true;
        }
    }

    /// <summary>
    /// Editor-only helper to clear the Unity console. No-op in builds.
    /// </summary>
    private void ClearConsole()
    {
#if UNITY_EDITOR
        // Attempt to call UnityEditor.LogEntries.Clear() via reflection to avoid internal API access issues.
        try
        {
            var asm = Assembly.GetAssembly(typeof(Editor));
            if (asm != null)
            {
                var logEntriesType = asm.GetType("UnityEditor.LogEntries");
                if (logEntriesType != null)
                {
                    var clearMethod = logEntriesType.GetMethod("Clear", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
                    if (clearMethod != null)
                    {
                        clearMethod.Invoke(null, null);
                        return;
                    }
                }
            }
            // Fallback: use Debug.Log to indicate we couldn't clear programmatically
            Debug.Log("WinlineEvaluator: (editor) unable to programmatically clear console via reflection.");
        }
        catch (Exception ex)
        {
            Debug.Log($"WinlineEvaluator: exception while attempting to clear console: {ex.Message}");
        }
#endif
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

            // Only -1 indicates "cannot trigger"; 0 and positive are valid minimums (0 shouldn't occur by design)
            if (trigger.MinWinDepth < 0)
            {
                // If the leftmost symbol is a wild with no multipliers, attempt to find the
                // first viable non-wild symbol along the winline to serve as the trigger.
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
                        // Prefer the first non-wild symbol that can trigger wins and has a positive BaseValue
                        if (!candidate.IsWild && candidate.MinWinDepth >= 0 && candidate.BaseValue > 0)
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
                            Debug.Log($"Winline {i}: leftmost symbol '{trigger.Name}' cannot trigger wins (MinWinDepth={trigger.MinWinDepth}).");
                        continue;
                    }
                }
                else
                {
                    if (LoggingEnabled && (Application.isEditor || Debug.isDebugBuild))
                        Debug.Log($"Winline {i}: leftmost symbol '{trigger.Name}' cannot trigger wins (MinWinDepth={trigger.MinWinDepth}).");
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
                        // Payout progression: for baseVal = B and MinWinDepth = M
                        // matches = M -> payout = B (winDepth = 0)
                        // matches = M+1 -> payout = B * 2 (winDepth = 1)
                        // matches = M+2 -> payout = B * 4 (winDepth = 2)
                        // i.e. multiplier = 2^(winDepth)
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
