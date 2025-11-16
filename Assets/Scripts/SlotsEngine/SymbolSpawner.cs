using UnityEngine;

public class SymbolSpawner : Singleton<SymbolSpawner>
{
	[SerializeField] private SymbolDefinition[] symbols;

	public SymbolDefinition GetRandomSymbol()
	{
		return symbols[Random.Range(0, symbols.Length)];
	}
}
