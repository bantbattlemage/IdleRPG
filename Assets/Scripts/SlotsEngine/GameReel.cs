using DG.Tweening;
using System.Collections.Generic;
using UnityEngine;

public class GameReel : MonoBehaviour, IReel
{
	[SerializeField] private GameObject SymbolPrefab;

	private ReelDefinition definition;
	private List<GameSymbol> symbols = new List<GameSymbol>();
	private Transform symbolRoot;

	private List<GameSymbol> topDummySymbols = new List<GameSymbol>();
	private List<GameSymbol> bottomDummySymbols = new List<GameSymbol>();

	public void InitializeReel(ReelDefinition reelDefinition)
	{
		definition = reelDefinition;
		symbolRoot = new GameObject("SymbolRoot").transform;
		symbolRoot.parent = transform;
		symbolRoot.localScale = new Vector3(1, 1, 1);

		for (int i = 0; i < definition.SymbolCount; i++)
		{
			GameObject symbol = Instantiate(SymbolPrefab, symbolRoot);
			GameSymbol sym = symbol.GetComponent<GameSymbol>();
			symbol.GetComponent<RectTransform>().sizeDelta = new Vector2(definition.SymbolSize, definition.SymbolSize);
			symbol.transform.localPosition = new Vector3(0, (definition.SymbolSpacing + definition.SymbolSize) * i, 0);

			symbols.Add(sym);
		}

		SpawnDummySymbols();
	}

	private void SpawnDummySymbols()
	{
		List<GameSymbol> dummies = new List<GameSymbol>();

		//	run twice, first make top then make bottom
		for (int top = 0; top < 2; top++)
		{
			int bottom = top == 0 ? 1 : -1;
			int startIndex = bottom == 1 ? definition.SymbolCount : 0;

			for (int i = 0; i < definition.SymbolCount; i++)
			{
				GameObject symbol = Instantiate(SymbolPrefab, symbolRoot);
				GameSymbol sym = symbol.GetComponent<GameSymbol>();
				symbol.GetComponent<RectTransform>().sizeDelta = new Vector2(definition.SymbolSize, definition.SymbolSize);
				symbol.transform.localPosition = new Vector3(0, ((definition.SymbolSpacing + definition.SymbolSize) * (i + startIndex)) * bottom, 0);

				dummies.Add(sym);
			}

			if (top == 0)
			{
				topDummySymbols = symbols;
			}
			else
			{
				bottomDummySymbols = symbols;
			}
		}
	}

	public void FallOut()
	{
		float fallDistance = ((definition.SymbolSize + definition.SymbolSpacing) * definition.SymbolCount);

		symbolRoot.DOLocalMoveY(-fallDistance, 1).SetEase(Ease.Linear).OnComplete(() =>
		{
			symbolRoot.localPosition = new Vector3();

			FallOut();
		});
	}
}
