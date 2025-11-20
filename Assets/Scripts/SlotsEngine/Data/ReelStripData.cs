using UnityEngine;

public class ReelStripData : Data
{
	[SerializeField] private int stripSize;
	// store definition keys instead of direct references
	[SerializeField] private string[] symbolDefinitionKeys;
	[SerializeField] private string definitionKey;

	// runtime caches (non-serialized)
	[System.NonSerialized] private SymbolDefinition[] symbolDefinitions;
	[System.NonSerialized] private ReelStripDefinition definition;

	public int StripSize => stripSize;
	public SymbolDefinition[] SymbolDefinitions
	{
		get
		{
			EnsureResolved();
			return symbolDefinitions;
		}
	}

	public ReelStripDefinition Definition
	{
		get
		{
			EnsureResolved();
			return definition;
		}
	}

	public ReelStripData(ReelStripDefinition def, int size, SymbolDefinition[] syms)
	{
		stripSize = size;
	
definition = def;
		symbolDefinitions = syms;

		// store keys
		definitionKey = def != null ? def.name : null;
		if (syms != null)
		{
			symbolDefinitionKeys = new string[syms.Length];
			for (int i = 0; i < syms.Length; i++) symbolDefinitionKeys[i] = syms[i].name;
		}
	}

	private void EnsureResolved()
	{
		if (definition == null && !string.IsNullOrEmpty(definitionKey))
		{
			definition = DefinitionResolver.Resolve<ReelStripDefinition>(definitionKey);
		}

		if ((symbolDefinitions == null || symbolDefinitions.Length == 0) && symbolDefinitionKeys != null)
		{
			symbolDefinitions = new SymbolDefinition[symbolDefinitionKeys.Length];
			for (int i = 0; i < symbolDefinitionKeys.Length; i++)
			{
				symbolDefinitions[i] = DefinitionResolver.Resolve<SymbolDefinition>(symbolDefinitionKeys[i]);
			}
		}
	}

	public SymbolData GetWeightedSymbol()
	{
		EnsureResolved();

		var weightedSymbolTuple = new (SymbolDefinition, float)[symbolDefinitions.Length];

		for (int i = 0; i < symbolDefinitions.Length; i++)
		{
			float adjustedWeight = symbolDefinitions[i].Weight / stripSize;

			weightedSymbolTuple[i] = (symbolDefinitions[i], adjustedWeight);
		}

		SymbolDefinition symbolToReturn = WeightedRandom.Pick(weightedSymbolTuple);
		return symbolToReturn.CreateInstance();
	}
}
