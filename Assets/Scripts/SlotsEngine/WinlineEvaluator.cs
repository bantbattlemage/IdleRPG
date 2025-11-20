using System;
using System.Collections.Generic;
using System.Linq;

public class WinlineEvaluator : Singleton<WinlineEvaluator>
{
	private List<WinData> currentSpinWinData;
	public List<WinData> CurrentSpinWinData => currentSpinWinData;

	// New API: explicit columns and rows per column
	public List<WinData> EvaluateWins(SymbolData[] grid, int columns, int[] rowsPerColumn, List<WinlineDefinition> winlines)
	{
		List<WinData> winData = new List<WinData>();

		if (grid == null || grid.Length == 0 || winlines == null || winlines.Count == 0)
		{
			currentSpinWinData = winData;
			return winData;
		}

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
			// If grid length doesn't match expected rectangular grid, attempt graceful fallback by padding.
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

				if (row >= rowsPerColumn[col]) break; // that column doesn't have that row

				var cell = grid[symbolIndex];
				if (cell == null) break;

				if (cell.Name == symbolToMatch)
				{
					winningIndexes.Add(symbolIndex);
				}
				else
				{
					break;
				}
			}

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

	// Backwards-compatible overload: uniform rows
	public List<WinData> EvaluateWins(SymbolData[] grid, int columns, int rows, List<WinlineDefinition> winlines)
	{
		int[] perCol = new int[columns];
		for (int i = 0; i < columns; i++) perCol[i] = rows;
		return EvaluateWins(grid, columns, perCol, winlines);
	}
}
