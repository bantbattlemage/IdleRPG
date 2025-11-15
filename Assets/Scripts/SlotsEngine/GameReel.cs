using System.Collections.Generic;
using UnityEngine;

public class GameReel : MonoBehaviour, IReel
{
	[SerializeField] private GameObject SymbolPrefab;

	private ReelDefinition definition;
	private List<GameSymbol> symbols = new List<GameSymbol>();

	public void InitializeReel(ReelDefinition reelDefinition)
	{
		definition = reelDefinition;

		for (int i = 0; i < definition.SymbolCount; i++)
		{
			GameObject symbol = Instantiate(SymbolPrefab, transform);
			GameSymbol sym = symbol.GetComponent<GameSymbol>();
			symbol.GetComponent<RectTransform>().sizeDelta = new Vector2(definition.SymbolSize, definition.SymbolSize);
			symbol.transform.localPosition = new Vector3(0, (definition.SymbolSpacing + definition.SymbolSize) * i, 0);

			symbols.Add(sym);
		}
	}
}
