using System;
using DG.Tweening;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class GameReel : MonoBehaviour
{
	[SerializeField] private GameObject SymbolPrefab;

	private ReelData currentReelData;
	public ReelData CurrentReelData => currentReelData;

	private ReelStripData reelStrip;
	public ReelStripData ReelStrip => reelStrip;

	public int ID => id;
	private int id;

	public bool Spinning => spinning;
	private bool spinning = false;

	public List<GameSymbol> Symbols => symbols;


	private Transform symbolRoot;
	private Transform nextSymbolsRoot;

	private List<GameSymbol> symbols = new List<GameSymbol>();
	private List<GameSymbol> topDummySymbols = new List<GameSymbol>();
	private List<GameSymbol> bottomDummySymbols = new List<GameSymbol>();

	private bool completeOnNextSpin = false;

	private Tweener[] activeSpinTweens = new Tweener[2];

	private EventManager eventManager;

	public void InitializeReel(ReelData data, int reelID, EventManager slotsEventManager, ReelStripDefinition stripDefinition)
	{
		currentReelData = data;
		id = reelID;
		eventManager = slotsEventManager;
		reelStrip = stripDefinition.CreateInstance();

		SpawnReel();
	}

	public SymbolData GetRandomSymbolFromStrip()
	{
		return reelStrip.GetWeightedSymbol();
	}

	private void SpawnReel()
	{
		symbolRoot = new GameObject("SymbolRoot").transform;
		symbolRoot.parent = transform;
		symbolRoot.localScale = new Vector3(1, 1, 1);

		for (int i = 0; i < currentReelData.SymbolCount; i++)
		{
			GameObject symbol = Instantiate(SymbolPrefab, symbolRoot);
			GameSymbol sym = symbol.GetComponent<GameSymbol>();
			sym.InitializeSymbol(GetRandomSymbolFromStrip(), eventManager);

			symbol.GetComponent<RectTransform>().sizeDelta = new Vector2(currentReelData.SymbolSize, currentReelData.SymbolSize);
			symbol.transform.localPosition = new Vector3(0, (currentReelData.SymbolSpacing + currentReelData.SymbolSize) * i, 0);

			symbols.Add(sym);
		}

		SpawnDummySymbols(symbolRoot);
		SpawnDummySymbols(symbolRoot, false);
	}

	public void BeginSpin(List<SymbolData> solution = null, float startDelay = 0f)
	{
		completeOnNextSpin = false;
		
		DOTween.Sequence().AppendInterval(startDelay).AppendCallback(() =>
		{
			BounceReel(Vector3.up, strength: 50f, peak: 0.8f, duration: 0.25f, onComplete:() =>
			{
				FallOut(solution, true);
				spinning = true;
				eventManager.BroadcastEvent("ReelSpinStarted", ID);
			});
		});
	}

	public void StopReel(float delay = 0f)
	{
		DOTween.Sequence().AppendInterval(delay).AppendCallback(() =>
		{
			completeOnNextSpin = true;

			//	slam the reels
			activeSpinTweens[0].timeScale = 4f;
			activeSpinTweens[1].timeScale = 4f;
		});
	}

	public void ApplySolution(List<SymbolDefinition> symbols)
	{

	}

	private void SpawnNextReel(List<SymbolData> solution = null)
	{
		Transform nextReel = new GameObject("Next").transform;
		nextReel.parent = transform;
		nextReel.localScale = new Vector3(1, 1, 1);
		nextReel.transform.localPosition = new Vector3(0, ((currentReelData.SymbolSpacing + currentReelData.SymbolSize) * ((currentReelData.SymbolCount-1) * 3)), 0);

		List<GameSymbol> newSymbols = new List<GameSymbol>();
		for (int i = 0; i < currentReelData.SymbolCount; i++)
		{
			GameSymbol symbol = Instantiate(SymbolPrefab, nextReel).GetComponent<GameSymbol>();

			SymbolData def;

			if (solution != null)
			{
				def = solution[i];
			}
			else
			{
				def = reelStrip.GetWeightedSymbol();
			}

			symbol.InitializeSymbol(def, eventManager);
			symbol.GetComponent<RectTransform>().sizeDelta = new Vector2(currentReelData.SymbolSize, currentReelData.SymbolSize);
			symbol.transform.localPosition = new Vector3(0, ((currentReelData.SymbolSpacing + currentReelData.SymbolSize) * i), 0);

			newSymbols.Add(symbol);
		}

		symbols = newSymbols;

		SpawnDummySymbols(nextReel);
		SpawnDummySymbols(nextReel, false);

		nextSymbolsRoot = nextReel;
	}

	private void SpawnDummySymbols(Transform root, bool bottom = true, List<SymbolData> symbolData = null)
	{
		List<GameSymbol> dummies = new List<GameSymbol>();

		int startIndex = !bottom ? currentReelData.SymbolCount : 1;
		int flip = bottom ? -1 : 1;
		int total = bottom ? currentReelData.SymbolCount - 1 : currentReelData.SymbolCount;
		string name = bottom ? "bot" : "top";

		for (int i = 0; i < total; i++)
		{
			GameSymbol symbol = Instantiate(SymbolPrefab, root).GetComponent<GameSymbol>();
			symbol.name = name;

			SymbolData def;
			if (symbolData != null)
			{
				def = symbolData[i];
			}
			else
			{
				def = reelStrip.GetWeightedSymbol();
			}

			symbol.InitializeSymbol(def, eventManager);
			symbol.GetComponent<RectTransform>().sizeDelta = new Vector2(currentReelData.SymbolSize, currentReelData.SymbolSize);
			symbol.transform.localPosition = new Vector3(0, ((currentReelData.SymbolSpacing + currentReelData.SymbolSize) * (i + startIndex)) * flip, 0);
			dummies.Add(symbol);
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

	private bool sequenceA = false;
	private bool sequenceB = false;
	public void FallOut(List<SymbolData> solution = null, bool kickback = false)
	{
		ResetDimmedSymbols();
		SpawnNextReel(solution);

		sequenceA = false;
		sequenceB = false;

		float fallDistance = -nextSymbolsRoot.transform.localPosition.y;
		float duration = currentReelData.ReelSpinDuration;

		activeSpinTweens[0] = symbolRoot.transform.DOLocalMoveY(fallDistance, duration).OnComplete(() =>
		{
			sequenceA = true;

			CheckBeginLandingBounce(solution);

		}).SetEase(Ease.Linear);

		activeSpinTweens[1] = nextSymbolsRoot.transform.DOLocalMoveY(0, duration).OnComplete(() =>
		{
			sequenceB = true;

			CheckBeginLandingBounce(solution);

		}).SetEase(Ease.Linear);
	}

	private void CheckBeginLandingBounce(List<SymbolData> solution)
	{
		if (sequenceA && sequenceB)
		{
			sequenceA = false;
			sequenceB = false;

			if (completeOnNextSpin)
			{
				BounceReel(Vector3.down, peak: 0.25f, duration: 0.25f, onComplete: () => CompleteReelSpin(solution));
			}
			else
			{
				CompleteReelSpin(solution);
			}
		}
	}

	private void BounceReel(Vector3 direction, float strength = 100f, float duration = 0.5f, float sharpness = 0f, float peak = 0.4f, Action onComplete = null)
	{
		if (nextSymbolsRoot != null)
		{
			nextSymbolsRoot.DOPulseUp(direction, strength, duration, sharpness, peak).SetEase(Ease.Linear);
		}

		symbolRoot.DOPulseUp(direction, strength, duration, sharpness, peak).SetEase(Ease.Linear).OnComplete(() => { if (onComplete != null) onComplete(); });

		//if (nextSymbolsRoot != null)
		//{
		//	nextSymbolsRoot.DOEdgeBounceLinear(Vector3.up, 1000f, 0.5f).SetEase(Ease.Linear);
		//}

		//symbolRoot.DOEdgeBounceLinear(Vector3.up, 1000f, 0.5f).SetEase(Ease.Linear).OnComplete(() => { if (onComplete != null) onComplete(); });
	}

	private void CompleteReelSpin(List<SymbolData> solution)
	{
		Destroy(symbolRoot.gameObject);
		symbolRoot = nextSymbolsRoot;

		if (!completeOnNextSpin)
		{
			FallOut(solution);
		}
		else
		{
			spinning = false;

			for (int i = 0; i < symbols.Count; i++)
			{
				eventManager.BroadcastEvent("SymbolLanded", symbols[i]);
			}

			eventManager.BroadcastEvent("ReelCompleted", ID);
		}
	}

	public void DimDummySymbols()
	{
		foreach (GameSymbol g in topDummySymbols)
		{
			g.GetComponent<Image>().DOColor(new Color(0.5f, 0.5f, 0.5f), 0.1f);
		}

		foreach (GameSymbol g in bottomDummySymbols)
		{
			g.GetComponent<Image>().DOColor(new Color(0.5f, 0.5f, 0.5f), 0.1f);
		}
	}

	public void ResetDimmedSymbols()
	{
		foreach (GameSymbol g in topDummySymbols)
		{
			var image = g.GetComponent<Image>();
			image.DOKill();
			image.color = Color.white;
		}

		foreach (GameSymbol g in bottomDummySymbols)
		{
			var image = g.GetComponent<Image>();
			image.DOKill();
			image.color = Color.white;
		}
	}
}
