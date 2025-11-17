using System.Collections.Generic;
using System.Linq;

public class WinlineEvaluator : Singleton<WinlineEvaluator>
{
	private List<WinData> currentSpinWinData;
	public List<WinData> CurrentSpinWinData => currentSpinWinData;

	public List<WinData> EvaluateWins(SymbolDefinition[] grid, WinlineDefinition[] winlines)
	{
		List<WinData> winData = new List<WinData>();

		for(int i = 0; i < winlines.Length; i++)
		{
			WinlineDefinition winline = winlines[i];

			string symbolToMatch = grid[winline.SymbolIndexes[0]].Name;
			List<int> winningIndexes = new List<int>();

			foreach (int symbolIndex in winline.SymbolIndexes)
			{
				if (grid[symbolIndex].Name == symbolToMatch)
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
				value *= winline.WinMultiplier;
				value *= GamePlayer.Instance.CurrentBet.CreditCost;

				WinData data = new WinData(lineIndex, value, winningIndexes.ToArray());
				winData.Add(data);
			}
		}

		currentSpinWinData = winData;

		return winData;
	}
}
