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

    // Scene-configurable cap for how many spin log files are retained in the SpinLogs folder.
    // A value <= 0 means no pruning (retain all logs).
    [Tooltip("Maximum number of spin log files to retain. Set to 0 or negative for no limit.")]
    public int MaxSpinLogs = 100;

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
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                        Debug.LogException(ex);
#endif
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
            try
            {
                Directory.CreateDirectory(dir);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"WinEvaluator: Failed to create SpinLogs directory '{dir}': {ex.Message}");
            }

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

            // Prune oldest logs if MaxSpinLogs is positive
            try
            {
                if (MaxSpinLogs > 0)
                {
                    var files = Directory.Exists(dir) ? Directory.GetFiles(dir, "spin_*.log") : Array.Empty<string>();
                    if (files.Length > MaxSpinLogs)
                    {
                        // Order by creation time then by name as fallback
                        var ordered = files.Select(f => new FileInfo(f)).OrderBy(fi => fi.CreationTimeUtc).ThenBy(fi => fi.Name).ToList();
                        int toRemove = ordered.Count - MaxSpinLogs;
                        for (int r = 0; r < toRemove; r++)
                        {
                            try { ordered[r].Delete(); } catch (Exception ex)
                            {
                                Debug.LogWarning($"WinEvaluator: Failed to delete old spin log '{ordered[r].FullName}': {ex.Message}");
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"WinEvaluator: Failed while pruning spin logs: {ex.Message}");
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
        int maxRows = 0; for (int c = 0; c < rowsPerColumn.Length; c++) if (rowsPerColumn[c] > maxRows) maxRows = rowsPerColumn[c];
        for (int i = 0; i < winlines.Count; i++)
        {
            var wl = winlines[i]; if (wl == null) continue;
            try
            {
                // Expand StraightAcross and diagonal definitions so every concrete pattern derived from a definition is evaluated.
                if (wl.Pattern == WinlineDefinition.PatternType.DiagonalDown || wl.Pattern == WinlineDefinition.PatternType.DiagonalUp)
                {
                    for (int ro = 0; ro < maxRows; ro++)
                    {
                        var clone = wl.CloneForRuntime();
                        clone.SetRowOffset(ro);
                        patterns.Add(clone.GenerateIndexes(columns, rowsPerColumn));
                        multipliers.Add(clone.WinMultiplier);
                    }
                }
                else if (wl.Pattern == WinlineDefinition.PatternType.StraightAcross)
                {
                    // Evaluate every visual row as a straight-across candidate
                    for (int r = 0; r < maxRows; r++)
                    {
                        var clone = wl.CloneForRuntime();
                        clone.SetRowIndex(r);
                        patterns.Add(clone.GenerateIndexes(columns, rowsPerColumn));
                        multipliers.Add(clone.WinMultiplier);
                    }
                }
                else if (wl.Pattern == WinlineDefinition.PatternType.ZigzagW || wl.Pattern == WinlineDefinition.PatternType.ZigzagM)
                {
                    // Expand zigzag patterns across every possible rowOffset so all vertical shifts are evaluated
                    for (int ro = 0; ro < maxRows; ro++)
                    {
                        var clone = wl.CloneForRuntime();
                        clone.SetRowOffset(ro);
                        patterns.Add(clone.GenerateIndexes(columns, rowsPerColumn));
                        multipliers.Add(clone.WinMultiplier);
                    }
                }
                else
                {
                    patterns.Add(wl.GenerateIndexes(columns, rowsPerColumn));
                    multipliers.Add(wl.WinMultiplier);
                }
            }
            catch (Exception ex)
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                Debug.LogException(ex);
#endif
                // Fall back to attempting the basic expansion once; log above if it fails
                patterns.Add(wl.GenerateIndexes(columns, rowsPerColumn));
                multipliers.Add(wl.WinMultiplier);
            }
        }

        // Diagnostic: when logging is enabled, reproduce per-winline decision steps so we can trace misses
        if (LoggingEnabled)
        {
            try
            {
                var diagSb = new StringBuilder();
                diagSb.AppendLine("=== WinEvaluator per-winline diagnostics ===");
                for (int i = 0; i < patterns.Count; i++)
                {
                    var pattern = patterns[i];
                    diagSb.AppendLine($"Winline {i} (length={pattern?.Length ?? 0}):");
                    if (pattern == null || pattern.Length == 0) { diagSb.AppendLine("  <empty pattern>"); continue; }

                    int pStart = -1;
                    for (int pi = 0; pi < pattern.Length; pi++)
                    {
                        int candidate = pattern[pi];
                        if (candidate < 0 || candidate >= plain.Length) continue;
                        int col = candidate % columns; int row = candidate / columns; if (row >= rowsPerColumn[col]) continue;
                        var candCell = plain[candidate]; if (candCell == null) continue;
                        pStart = pi; break;
                    }
                    diagSb.AppendLine($"  pStart={pStart}");

                    bool earlierValidExists = false;
                    for (int pj = 0; pj < pStart; pj++)
                    {
                        int candidate = pattern[pj];
                        if (candidate < 0 || candidate >= plain.Length) continue;
                        int col = candidate % columns; int row = candidate / columns; if (row >= rowsPerColumn[col]) continue;
                        var candCell = plain[candidate]; if (candCell == null) continue;
                        earlierValidExists = true; break;
                    }
                    diagSb.AppendLine($"  earlierValidExists={earlierValidExists}");

                    if (pStart < 0)
                    {
                        diagSb.AppendLine("  No usable start candidate found");
                        continue;
                    }

                    int first = pattern[pStart];
                    var trigger = plain[first];
                    diagSb.AppendLine($"  firstIndex={first} trigger={(trigger!=null?trigger.Name:"<null>")}, IsWild={trigger?.IsWild}, MinWinDepth={trigger?.MinWinDepth}, WinMode={trigger?.WinMode}, BaseValue={trigger?.BaseValue}");

                    bool needsFallback = (trigger.MinWinDepth < 0 || trigger.WinMode != EvaluatorCore.SymbolWinMode.LineMatch) || (trigger.IsWild && trigger.BaseValue <= 0);
                    diagSb.AppendLine($"  needsFallback={needsFallback}");
                    int chosenTriggerIdx = first;
                    if (needsFallback && trigger.IsWild)
                    {
                        int alt = -1;
                        for (int p = pStart + 1; p < pattern.Length; p++)
                        {
                            int idx = pattern[p]; if (idx < 0 || idx >= plain.Length) continue;
                            int col = idx % columns; int row = idx / columns; if (row >= rowsPerColumn[col]) continue;
                            var cand = plain[idx]; if (cand == null) continue;
                            if (cand.MinWinDepth >= 0 && cand.BaseValue > 0 && cand.WinMode == EvaluatorCore.SymbolWinMode.LineMatch) { alt = idx; chosenTriggerIdx = idx; break; }
                        }
                        diagSb.AppendLine($"  fallbackAltIndex={ (chosenTriggerIdx==first? -1: chosenTriggerIdx)}");
                    }

                    // Compute matched contiguously starting from pStart
                    var matched = new List<int>();
                    for (int k = pStart; k < pattern.Length; k++)
                    {
                        int gi = pattern[k]; if (gi < 0 || gi >= plain.Length) break;
                        int col = gi % columns; int row = gi / columns; if (row >= rowsPerColumn[col]) break;
                        var cell = plain[gi]; if (cell == null) break;
                        if (cell.Matches(plain[chosenTriggerIdx])) matched.Add(gi); else break;
                    }
                    diagSb.AppendLine($"  matchedCount={matched.Count} matchedIndexes=[{string.Join(",", matched)}]");

                    int minDepth = trigger.MinWinDepth;
                    diagSb.AppendLine($"  trigger.MinWinDepth={minDepth}");
                    bool wouldWin = (matched.Count >= minDepth && trigger.BaseValue > 0 && trigger.WinMode == EvaluatorCore.SymbolWinMode.LineMatch);
                    diagSb.AppendLine($"  wouldWin={wouldWin}");
                }
                diagSb.AppendLine("=== End diagnostics ===");
                Debug.Log(diagSb.ToString());
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"WinEvaluator: diagnostics failed: {ex.Message}");
            }
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
