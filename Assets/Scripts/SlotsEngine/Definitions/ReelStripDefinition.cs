using UnityEngine;

public class ReelStripDefinition : BaseDefinition<ReelStripData>
{
	[SerializeField] private int stripSize;
	public int StripSize => stripSize;
	
	[SerializeField] private SymbolDefinition[] symbols;
	public SymbolDefinition[] Symbols => symbols;

	public SymbolDefinition GetWeightedSymbol()
	{
		var weightedSymbolTuple = new (SymbolDefinition, float)[symbols.Length];

		for (int i = 0; i < symbols.Length; i++)
		{
			float adjustedWeight = symbols[i].Weight / stripSize;

			weightedSymbolTuple[i] = (symbols[i], adjustedWeight);
		}

		SymbolDefinition symbolToReturn = WeightedRandom.Pick(weightedSymbolTuple);

		return symbolToReturn;
	}

	public override ReelStripData CreateInstance()
	{
		throw new System.NotImplementedException();
	}
}
