using UnityEngine;

public class ReelStripData : Data
{
	private int stripSize;
	public int StripSize => stripSize;

	private SymbolDefinition[] symbols;
	public SymbolDefinition[] Symbols => symbols;

	private ReelStripDefinition definition;
	public ReelStripDefinition Definition => definition;

	public ReelStripData(ReelStripDefinition def, int size, SymbolDefinition[] syms)
	{
		definition = def;
		stripSize = size;
		symbols = syms;
	}

	public SymbolData GetWeightedSymbol()
	{
		var weightedSymbolTuple = new (SymbolDefinition, float)[symbols.Length];

		for (int i = 0; i < symbols.Length; i++)
		{
			float adjustedWeight = symbols[i].Weight / stripSize;

			weightedSymbolTuple[i] = (symbols[i], adjustedWeight);
		}

		SymbolDefinition symbolToReturn = WeightedRandom.Pick(weightedSymbolTuple);
		return symbolToReturn.CreateInstance();
	}
}
