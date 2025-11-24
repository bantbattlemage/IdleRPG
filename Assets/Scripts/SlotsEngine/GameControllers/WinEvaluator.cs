using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using EvaluatorCore;

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

    // No-op logger stub kept so callers in game code can still invoke logging without Unity IO dependencies
    public void LogSpinResult(GameSymbol[] grid, int columns, int[] rowsPerColumn, List<WinlineDefinition> winlines, List<WinData> winData)
    {
        // intentionally no-op; logging was removed during refactor
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
