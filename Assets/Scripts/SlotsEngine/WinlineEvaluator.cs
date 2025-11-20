using System;
using System.Collections.Generic;
using System.Linq;

public class WinlineEvaluator : Singleton<WinlineEvaluator>
{
	private List<WinData> currentSpinWinData;
	public List<WinData> CurrentSpinWinData => currentSpinWinData;

	// New API: explicit columns and rows per column
	/// <summary>
	/// Evaluate wins for a rectangular grid represented in column-major layout.
	/// Caller provides explicit number of columns and an array with rows per column.
	/// Returns a list of WinData for lines that match a symbol across a winline definition.
	/// </summary>
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

		for (int i = 0; i < winlines.Count; i++)
		{
			WinlineDefinition winlineDef = winlines[i];
			var concrete = winlineDef.GenerateIndexes(columns, rowsPerColumn);

			if (concrete == null || concrete.Length == 0) continue;

			// Ensure the first index is in range
			if (concrete[0] < 0 || concrete[0] >= grid.Length) continue;

			string symbolToMatch = grid[concrete[0]]?.Name;
			if (string.IsNullOrEmpty(symbolToMatch)) continue;

			List<int> winningIndexes = new List<int>();

			for (int k = 0; k < concrete.Length; k++)
			{
				int symbolIndex = concrete[k];
				if (symbolIndex < 0 || symbolIndex >= grid.Length) break;

				// Map symbolIndex back to column and row to verify that that column actually contains that row
				int col = symbolIndex % columns;
				int row = symbolIndex / columns;

				// If the target row isn't present on that column, the winline is truncated here.
				if (row >= rowsPerColumn[col]) break; // that column doesn't have that row

				var cell = grid[symbolIndex];
				if (cell == null) break;

				if (cell.Name == symbolToMatch)
				{
					winningIndexes.Add(symbolIndex);
				}
				else
				{
					// mismatch - winline stops
					break;
				}
			}

			// Only award wins when the depth exceeds the symbol's MinWinDepth and at least one match
			if (winningIndexes.Count > 0 && winningIndexes.Count > grid[winningIndexes[0]].MinWinDepth)
			{
				int lineIndex = i;
				int value = grid[winningIndexes[0]].BaseValueMultiplier[winningIndexes.Count - 1];
				value *= winlineDef.WinMultiplier;
				value *= GamePlayer.Instance.CurrentBet.CreditCost;

				WinData data = new WinData(lineIndex, value, winningIndexes.ToArray());
				winData.Add(data);
			}
		}

		currentSpinWinData = winData;

		return winData;
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
