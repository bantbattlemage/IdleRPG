using UnityEngine;

public class ReelData : Data
{
	[SerializeField] private int index;
	public int Index => index;

	[SerializeField] private SymbolData[] reelStrip;
	public SymbolData[] ReelStrip => reelStrip;

	[SerializeField] private SymbolData[] currentSymbolData;
	public SymbolData[] CurrentSymbolData => currentSymbolData;
}
