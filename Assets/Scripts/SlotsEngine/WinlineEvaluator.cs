using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

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
	private List<WinData> currentSpinWinData;
	public List<WinData> CurrentSpinWinData => currentSpinWinData;

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

		for (int i = 0; i < winlines.Count; i++)
		{
			WinlineDefinition winlineDef = winlines[i];
			var concrete = winlineDef.GenerateIndexes(columns, rowsPerColumn);

			if (concrete == null || concrete.Length == 0) continue;

			// Enforce wins always begin on the leftmost column: only concrete[0] can be the trigger
			int firstIndex = concrete.Length > 0 ? concrete[0] : -1;
			if (firstIndex < 0 || firstIndex >= grid.Length)
			{
				if (Application.isEditor || Debug.isDebugBuild)
					Debug.Log($"Winline {i}: leftmost cell invalid or missing (idx={firstIndex}).");
				continue;
			}

			var trigger = grid[firstIndex];
			if (trigger == null)
			{
				if (Application.isEditor || Debug.isDebugBuild)
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
						if (si < 0 || si >= grid.Length) break;
						int col = si % columns;
						int row = si / columns;
						if (row >= rowsPerColumn[col]) break; // truncated column

						var candidate = grid[si];
						if (candidate == null) continue;
						// Prefer the first non-wild symbol that can trigger wins
						if (!candidate.IsWild && candidate.MinWinDepth >= 0)
						{
							altIndex = si;
							break;
						}
					}

					if (altIndex >= 0)
					{
						if (Application.isEditor || Debug.isDebugBuild)
							Debug.Log($"Winline {i}: leftmost is wild without multipliers; using symbol at idx={altIndex} ('{grid[altIndex].Name}') as trigger.");
						trigger = grid[altIndex];
					}
					else
					{
						if (Application.isEditor || Debug.isDebugBuild)
							Debug.Log($"Winline {i}: leftmost symbol '{trigger.Name}' cannot trigger wins (MinWinDepth={trigger.MinWinDepth}).");
						continue;
					}
				}
				else
				{
					if (Application.isEditor || Debug.isDebugBuild)
						Debug.Log($"Winline {i}: leftmost symbol '{trigger.Name}' cannot trigger wins (MinWinDepth={trigger.MinWinDepth}).");
					continue;
				}
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

			// Validate: sufficient depth and valid multiplier exists
			if (matchCount >= trigger.MinWinDepth)
			{
				int[] multipliers = trigger.BaseValueMultiplier ?? Array.Empty<int>();
				
				// Multiplier index is (matchCount - 1) since arrays are 0-indexed
				// Example: 3 matches = multipliers[2], 5 matches = multipliers[4]
				int multIndex = matchCount - 1;
				
				if (multipliers.Length > 0)
				{
					// Clamp to the last available multiplier if match exceeds defined multipliers
					if (multIndex >= multipliers.Length)
					{
						multIndex = multipliers.Length - 1;
						if (Application.isEditor || Debug.isDebugBuild)
							Debug.Log($"Winline {i}: match count {matchCount} exceeds multiplier array length {multipliers.Length}, using last multiplier.");
					}
					
					int multiplier = multipliers[multIndex];
					
					if (multiplier > 0)
					{
						int value = multiplier * winlineDef.WinMultiplier * GamePlayer.Instance.CurrentBet.CreditCost;
						winData.Add(new WinData(i, value, winningIndexes.ToArray()));
						
						if (Application.isEditor || Debug.isDebugBuild)
							Debug.Log($"Winline {i}: WIN! trigger={trigger.Name} matches={matchCount} multiplier={multiplier} lineMultiplier={winlineDef.WinMultiplier} totalValue={value}");
					}
					else
					{
						if (Application.isEditor || Debug.isDebugBuild)
							Debug.Log($"Winline {i}: match count {matchCount} valid but multiplier is 0 (no win).");
					}
				}
				else
				{
					if (Application.isEditor || Debug.isDebugBuild)
						Debug.Log($"Winline {i}: trigger '{trigger.Name}' has no multipliers defined.");
				}
			}
			else
			{
				if (Application.isEditor || Debug.isDebugBuild)
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
