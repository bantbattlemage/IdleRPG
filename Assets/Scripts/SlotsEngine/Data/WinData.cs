using UnityEngine;

[System.Serializable]
public class WinData
{
	private int winValue;
	public int WinValue => winValue;

	private int[] winningSymbolIndexes;
	public int[] WinningSymbolIndexes => winningSymbolIndexes;

	private int lineIndex;
	public int LineIndex => lineIndex;

	public WinData(int line, int value, int[] symbolIndexes)
	{
		lineIndex = line;
		winValue = value;
		winningSymbolIndexes = symbolIndexes;
	}
}
