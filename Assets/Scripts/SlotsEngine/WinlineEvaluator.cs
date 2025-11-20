using System;
using System.Collections.Generic;
using System.Linq;

public class WinlineEvaluator : Singleton<WinlineEvaluator>
{
	private List<WinData> currentSpinWinData;
	public List<WinData> CurrentSpinWinData => currentSpinWinData;

	// Now requires explicit grid dimensions so pattern definitions can generate correct indexes
	public List<WinData> EvaluateWins(SymbolData[] grid, int columns, int rows, List<WinlineDefinition> winlines)
	{
		List<WinData> winData = new List<WinData>();

		if (grid == null || grid.Length == 0 || winlines == null || winlines.Count == 0)
		{
			currentSpinWinData = winData;
			return winData;
		}

		// Validate dimensions and clamp
		if (columns <= 0) columns = 1;
		if (rows <= 0) rows = Math.Max(1, grid.Length / columns);

		for (int i = 0; i < winlines.Count; i++)
		{
			WinlineDefinition winlineDef = winlines[i];
			var concrete = winlineDef.GenerateIndexes(columns, rows);

			if (concrete == null || concrete.Length == 0) continue;

			// Ensure the first index is in range
			if (concrete[0] < 0 || concrete[0] >= grid.Length) continue;

			string symbolToMatch = grid[concrete[0]]?.Name;
			if (string.IsNullOrEmpty(symbolToMatch)) continue;

			List<int> winningIndexes = new List<int>();

			foreach (int symbolIndex in concrete)
			{
				if (symbolIndex < 0 || symbolIndex >= grid.Length) break;

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
}
