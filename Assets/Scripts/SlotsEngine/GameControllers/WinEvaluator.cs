using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using EvaluatorCore;
using System.Text;
using System.IO;

/// <summary>
/// Adapter that delegates pure win-evaluation logic to a Unity-independent core implementation.
/// This class converts Unity-specific types (`SymbolData`, `WinlineDefinition`, `GameSymbol`) into
/// plain POCO representations consumed by `CoreWinlineEvaluator` and maps results back to Unity types.
/// Logging and file IO removed as part of the refactor to keep core logic standalone.
/// </summary>
public class WinEvaluator : Singleton<WinEvaluator>
{
    // Public toggle kept for editor convenience but logging implementation removed per instructions
    public bool LoggingEnabled = false;

    private List<WinData> currentSpinWinData;

    public List<WinData> CurrentSpinWinData => currentSpinWinData ?? (currentSpinWinData = new List<WinData>());

    // No-op spin notification kept for backwards compatibility with callers (previous logging behavior removed)
    public void NotifySpinStarted()
    {
        // intentionally no-op; left to preserve public API for callers
    }

    // Detailed consolidated logger: writes a single log file under persistentDataPath/SpinLogs and emits one Debug.Log containing the same payload.
    // Avoids spamming the Unity log by emitting a single consolidated string.
    public void LogSpinResult(GameSymbol[] grid, int columns, int[] rowsPerColumn, List<WinlineDefinition> winlines, List<WinData> winData)
    {
        if (!LoggingEnabled) return;
        try
        {
            var sb = new StringBuilder();
            var now = DateTime.UtcNow;
            sb.AppendLine($"=== Spin Log {now:yyyy-MM-dd HH:mm:ss.fff} UTC ===");

            // rows per column
            if (rowsPerColumn != null)
            {
                sb.AppendLine("RowsPerColumn: " + string.Join(",", rowsPerColumn.Select(x => x.ToString())));
            }
            else
            {
                sb.AppendLine("RowsPerColumn: null");
            }

            int gridLen = grid != null ? grid.Length : 0;
            sb.AppendLine($"GridLength: {gridLen}, Columns: {columns}");

            // describe grid
            sb.AppendLine("Grid cells (index -> col,row : Name | IsWild | BaseValue | MinWinDepth | WinMode | PayScaling | MatchGroupId | AllowWildMatch):");
            if (grid != null)
            {
                for (int i = 0; i < grid.Length; i++)
                {
                    var gs = grid[i];
                    int col = (columns > 0) ? (i % columns) : 0;
                    int row = (columns > 0) ? (i / columns) : 0;
                    string name = "<null>";
                    string details = "";
                    if (gs != null)
                    {
                        var sd = gs.CurrentSymbolData;
                        if (sd != null)
                        {
                            name = sd.Name ?? "<noname>";
                            details = $"IsWild={sd.IsWild},BaseValue={sd.BaseValue},MinWinDepth={sd.MinWinDepth},WinMode={sd.WinMode},PayScaling={sd.PayScaling},MatchGroupId={sd.MatchGroupId},AllowWild={sd.AllowWildMatch}";
                        }
                        else
                        {
                            name = "<no SymbolData>";
                        }
                    }
                    sb.AppendLine($"  [{i}] -> ({col},{row}) : {name} | {details}");
                }
            }

            // Describe winlines and generated indexes
            sb.AppendLine("WinlineDefinitions and generated indexes:");
            if (winlines != null)
            {
                for (int wi = 0; wi < winlines.Count; wi++)
                {
                    var wl = winlines[wi];
                    if (wl == null) { sb.AppendLine($"  [{wi}] <null>"); continue; }
                    try
                    {
                        var pattern = wl.GenerateIndexes(columns, rowsPerColumn);
                        sb.AppendLine($"  [{wi}] {wl.name} (PatternType={wl.Pattern}, RowIndex={wl.RowIndex}, RowOffset={wl.RowOffset}, WinMultiplier={wl.WinMultiplier}):");
                        if (pattern != null && pattern.Length > 0)
                        {
                            var parts = new List<string>();
                            for (int p = 0; p < pattern.Length; p++)
                            {
                                int idx = pattern[p];
                                string valid = "invalid";
                                string sym = "<null>";
                                if (idx >= 0 && idx < gridLen)
                                {
                                    int col = idx % columns; int row = idx / columns;
                                    if (rowsPerColumn != null && col < rowsPerColumn.Length && row < rowsPerColumn[col]) valid = "valid";
                                    var gs = grid[idx]; if (gs != null && gs.CurrentSymbolData != null) sym = gs.CurrentSymbolData.Name ?? "<noname>";
                                }
                                parts.Add($"{idx}({valid})->{sym}");
                            }
                            sb.AppendLine("    " + string.Join(", ", parts));
                        }
                        else sb.AppendLine("    <empty pattern>");
                    }
                    catch (Exception ex)
                    {
                        sb.AppendLine($"  [{wi}] {wl.name} - GenerateIndexes threw: {ex.Message}");
                    }
                }
            }

            // WinData results
            sb.AppendLine("Evaluated Wins:");
            if (winData != null && winData.Count > 0)
            {
                foreach (var w in winData)
                {
                    var idxs = w.WinningSymbolIndexes != null ? string.Join(",", w.WinningSymbolIndexes) : "";
                    sb.AppendLine($"  LineIndex={w.LineIndex}, WinValue={w.WinValue}, Indexes=[{idxs}]");
                }
            }
            else
            {
                sb.AppendLine("  <no wins>");
            }

            sb.AppendLine("=== End Spin Log ===");

            // Ensure directory exists
            string dir = Path.Combine(Application.persistentDataPath, "SpinLogs");
            try { Directory.CreateDirectory(dir); } catch { }

            // Write file (timestamped)
            string fileName = $"spin_{now:yyyyMMdd_HHmmssfff}.log";
            string fullPath = Path.Combine(dir, fileName);
            try
            {
                File.WriteAllText(fullPath, sb.ToString());
            }
            catch (Exception ex)
            {
                // If file write fails, still log consolidated message to editor log
                Debug.LogWarning($"WinEvaluator: Failed to write spin log to '{fullPath}': {ex.Message}");
            }

            // Emit a single consolidated entry to the Unity Editor log to aid quick debugging
            Debug.Log(sb.ToString());
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"WinEvaluator: Exception during LogSpinResult: {ex}");
        }
    }

    public List<WinData> EvaluateWins(SymbolData[] grid, int columns, int[] rowsPerColumn, List<WinlineDefinition> winlines)
    {
        if (grid == null || winlines == null) { currentSpinWinData = new List<WinData>(); return currentSpinWinData; }

        // Convert SymbolData[] to PlainSymbolData[] (strip Unity types like Sprite)
        PlainSymbolData[] plain = new PlainSymbolData[grid.Length];
        for (int i = 0; i < grid.Length; i++)
        {
            var s = grid[i];
            if (s == null) { plain[i] = null; continue; }
            // Map Unity enums to EvaluatorCore enums by integer cast to avoid duplicate type issues
            plain[i] = new PlainSymbolData(s.Name, s.BaseValue, s.MinWinDepth, s.IsWild, (EvaluatorCore.SymbolWinMode)(int)s.WinMode, (EvaluatorCore.PayScaling)(int)s.PayScaling, s.TotalCountTrigger, s.MatchGroupId, s.AllowWildMatch);
        }

        // Convert winlines to int[] patterns using their GenerateIndexes
        var patterns = new List<int[]>();
        var multipliers = new List<int>();
        for (int i = 0; i < winlines.Count; i++)
        {
            var wl = winlines[i]; if (wl == null) continue;
            patterns.Add(wl.GenerateIndexes(columns, rowsPerColumn));
            multipliers.Add(wl.WinMultiplier);
        }

        var coreResults = CoreWinlineEvaluator.EvaluateWins(plain, columns, rowsPerColumn, patterns, multipliers, 1);

        // Map CoreWinData back to Unity WinData
        var results = new List<WinData>();
        foreach (var c in coreResults)
        {
            results.Add(new WinData(c.LineIndex, c.WinValue, c.WinningSymbolIndexes));
        }

        currentSpinWinData = results;
        return results;
    }

    // Convenience overloads that delegate to the refactored EvaluateWins
    public List<WinData> EvaluateWinsFromGameSymbols(GameSymbol[] gameSymbols, int columns, int[] rowsPerColumn, List<WinlineDefinition> winlines)
    {
        if (gameSymbols == null) return new List<WinData>();
        SymbolData[] grid = gameSymbols.ToSymbolDatas();
        return EvaluateWins(grid, columns, rowsPerColumn, winlines);
    }

    public List<WinData> EvaluateWinsFromColumns(List<GameSymbol[]> columns, List<WinlineDefinition> winlines)
    {
        if (columns == null || columns.Count == 0) return new List<WinData>();

        int cols = columns.Count;
        int[] rowsPerColumn = new int[cols];
        for (int c = 0; c < cols; c++) rowsPerColumn[c] = columns[c] != null ? columns[c].Length : 0;

        GameSymbol[] gridSymbols = Helpers.CombineColumnsToGrid(columns);

        return EvaluateWinsFromGameSymbols(gridSymbols, cols, rowsPerColumn, winlines);
    }

    public List<WinData> EvaluateWins(SymbolData[] grid, int columns, int rows, List<WinlineDefinition> winlines)
    {
        int[] perCol = new int[columns];
        for (int i = 0; i < columns; i++) perCol[i] = rows;
        return EvaluateWins(grid, columns, perCol, winlines);
    }
}
