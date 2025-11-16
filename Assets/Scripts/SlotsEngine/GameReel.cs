using DG.Tweening;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class GameReel : MonoBehaviour, IReel
{
	[SerializeField] private GameObject SymbolPrefab;

	public ReelDefinition Definition => definition;
	public int ID => id;
	private int id;

	public bool Spinning => spinning;
	private bool spinning = false;

	public List<GameSymbol> Symbols => symbols;

	private ReelDefinition definition;
	private Transform symbolRoot;
	private Transform nextSymbolsRoot;

	private List<GameSymbol> symbols = new List<GameSymbol>();
	private List<GameSymbol> topDummySymbols = new List<GameSymbol>();
	private List<GameSymbol> bottomDummySymbols = new List<GameSymbol>();

	private bool completeOnNextSpin = false;

	public void InitializeReel(ReelDefinition reelDefinition, int reelID)
	{
		definition = reelDefinition;
		id = reelID;

		SpawnReel();
	}

	private void SpawnReel()
	{
		symbolRoot = new GameObject("SymbolRoot").transform;
		symbolRoot.parent = transform;
		symbolRoot.localScale = new Vector3(1, 1, 1);

		for (int i = 0; i < definition.SymbolCount; i++)
		{
			GameObject symbol = Instantiate(SymbolPrefab, symbolRoot);
			GameSymbol sym = symbol.GetComponent<GameSymbol>();
			sym.ApplySymbol(SymbolSpawner.Instance.GetRandomSymbol());

			symbol.GetComponent<RectTransform>().sizeDelta = new Vector2(definition.SymbolSize, definition.SymbolSize);
			symbol.transform.localPosition = new Vector3(0, (definition.SymbolSpacing + definition.SymbolSize) * i, 0);

			symbols.Add(sym);
		}

		SpawnDummySymbols(symbolRoot);
		SpawnDummySymbols(symbolRoot, false);
	}

	public void BeginSpin(List<SymbolDefinition> solution = null)
	{
		completeOnNextSpin = false;
		spinning = true;

		FallOut(solution);
	}

	public void CompleteSpin()
	{
		completeOnNextSpin = true;
	}

	public void ApplySolution(List<SymbolDefinition> symbols)
	{

	}

	private void SpawnNextReel(List<SymbolDefinition> solution = null)
	{
		Transform nextReel = new GameObject("Next").transform;
		nextReel.parent = transform;
		nextReel.localScale = new Vector3(1, 1, 1);
		nextReel.transform.localPosition = new Vector3(0, ((definition.SymbolSpacing + definition.SymbolSize) * ((definition.SymbolCount-1) * 3)), 0);

		List<GameSymbol> newSymbols = new List<GameSymbol>();
		for (int i = 0; i < definition.SymbolCount; i++)
		{
			GameObject symbol = Instantiate(SymbolPrefab, nextReel);
			GameSymbol sym = symbol.GetComponent<GameSymbol>();

			SymbolDefinition def;

			if (solution != null)
			{
				def = solution[i];
			}
			else
			{
				def = symbols[i].Definition;
			}

			sym.ApplySymbol(def);

			symbol.GetComponent<RectTransform>().sizeDelta = new Vector2(definition.SymbolSize, definition.SymbolSize);
			symbol.transform.localPosition = new Vector3(0, ((definition.SymbolSpacing + definition.SymbolSize) * i), 0);

			newSymbols.Add(sym);
		}

		symbols = newSymbols;


		SpawnDummySymbols(nextReel);
		SpawnDummySymbols(nextReel, false);

		nextSymbolsRoot = nextReel;
	}

	private void SpawnDummySymbols(Transform root, bool bottom = true, List<SymbolDefinition> definitions = null)
	{
		List<GameSymbol> dummies = new List<GameSymbol>();

		int startIndex = !bottom ? definition.SymbolCount : 1;
		int flip = bottom ? -1 : 1;
		int total = bottom ? definition.SymbolCount - 1 : definition.SymbolCount;
		string name = bottom ? "bot" : "top";

		for (int i = 0; i < total; i++)
		{
			GameObject symbol = Instantiate(SymbolPrefab, root);
			symbol.name = name;

			GameSymbol sym = symbol.GetComponent<GameSymbol>();

			SymbolDefinition def;
			if (definitions != null)
			{
				def = definitions[i];
			}
			else
			{
				def = SymbolSpawner.Instance.GetRandomSymbol();
			}
			sym.ApplySymbol(def);

			symbol.GetComponent<RectTransform>().sizeDelta = new Vector2(definition.SymbolSize, definition.SymbolSize);
			symbol.transform.localPosition = new Vector3(0, ((definition.SymbolSpacing + definition.SymbolSize) * (i + startIndex)) * flip, 0);

			dummies.Add(sym);
		}

		if (bottom)
		{
			bottomDummySymbols = dummies;
		}
		else
		{
			topDummySymbols = dummies;
		}
	}

	public void FallOut(List<SymbolDefinition> solution = null)
	{
		SpawnNextReel(solution);

		float fallDistance = -nextSymbolsRoot.transform.localPosition.y;
		float duration = definition.ReelSpinDuration;

		symbolRoot.transform.DOLocalMoveY(fallDistance, duration - 0.01f).SetEase(Ease.Linear);
		
		nextSymbolsRoot.transform.DOLocalMoveY(0, duration).SetEase(Ease.Linear).OnComplete(() =>
		{
			Destroy(symbolRoot.gameObject);
			symbolRoot = nextSymbolsRoot;

			if (!completeOnNextSpin)
			{
				FallOut(solution);
			}
			else
			{
				for (int i = 0; i < symbols.Count; i++)
				{
					EventManager.Instance.BroadcastEvent("SymbolLanded", symbols[i]);
				}

				spinning = false;
				EventManager.Instance.BroadcastEvent("ReelCompleted", ID);
			}
		});
	}

	private void DestroyTopSymbols()
	{
		foreach (GameSymbol g in topDummySymbols)
		{
			Destroy(g.gameObject);
		}

		topDummySymbols = new List<GameSymbol>();
	}

	private void DestroyBottomSymbols()
	{
		foreach (GameSymbol g in bottomDummySymbols)
		{
			Destroy(g.gameObject);
		}

		bottomDummySymbols = new List<GameSymbol>();
	}
}
