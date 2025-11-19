using DG.Tweening;
using System;
using System.Collections.Generic;
using UnityEngine;

public class GameReel : MonoBehaviour
{
	private ReelData currentReelData;
	public ReelData CurrentReelData => currentReelData;

	private ReelStripData reelStrip;
	public ReelStripData ReelStrip => reelStrip;

	public int ID => id;
	private int id;

	public bool Spinning => spinning;
	private bool spinning = false;

	public List<GameSymbol> Symbols => symbols;


	// Persistent roots (no longer created/destroyed each spin)
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

		EnsureRootsCreated();
		SpawnReel(currentReelData.CurrentSymbolData);
	}

	public void InitializeReel(ReelData data, int reelID, EventManager slotsEventManager, ReelStripData stripData)
	{
		currentReelData = data;
		id = reelID;
		eventManager = slotsEventManager;
		reelStrip = stripData;

		EnsureRootsCreated();
		SpawnReel(currentReelData.CurrentSymbolData);
	}

	public SymbolData GetRandomSymbolFromStrip()
	{
		return reelStrip.GetWeightedSymbol();
	}

	private void EnsureRootsCreated()
	{
		// Create two persistent roots if missing. They will be reused each spin.
		if (symbolRoot == null)
		{
			symbolRoot = new GameObject("SymbolRoot").transform;
			symbolRoot.parent = transform;
			symbolRoot.localScale = Vector3.one;
			symbolRoot.localPosition = Vector3.zero;
		}

		if (nextSymbolsRoot == null)
		{
			nextSymbolsRoot = new GameObject("NextSymbolsRoot").transform;
			nextSymbolsRoot.parent = transform;
			nextSymbolsRoot.localScale = Vector3.one;

			// initial offscreen position; will be set properly when SpawnNextReel is called
			nextSymbolsRoot.localPosition = Vector3.zero;
		}
	}

	private void SpawnReel(List<SymbolData> existingSymbolData)
	{
		// Clear any previous symbols on the active root and lists
		ReleaseAllSymbolsInRoot(symbolRoot);
		symbols.Clear();
		topDummySymbols.Clear();
		bottomDummySymbols.Clear();

		float step = currentReelData.SymbolSpacing + currentReelData.SymbolSize;

		for (int i = 0; i < currentReelData.SymbolCount; i++)
		{
			GameSymbol sym = GameSymbolPool.Instance.Get(symbolRoot);

			SymbolData newSymbol;

			if (existingSymbolData is { Count: > 0 })
			{
				newSymbol = existingSymbolData[i];
			}
			else
			{
				newSymbol = GetRandomSymbolFromStrip();
			}

			sym.InitializeSymbol(newSymbol, eventManager);

			// Use cached component helper to avoid GetComponent each spin
			sym.SetSizeAndLocalY(currentReelData.SymbolSize, step * i);

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
			BounceReel(Vector3.up, strength: 50f, peak: 0.8f, duration: 0.25f, onComplete: () =>
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
		// Position the buffer root offscreen (top). This mirrors the previous Create+position.
		float offsetY = ((currentReelData.SymbolSpacing + currentReelData.SymbolSize) * ((currentReelData.SymbolCount - 1) * 3));
		nextSymbolsRoot.localPosition = new Vector3(0, offsetY, 0);

		// Ensure buffer is cleared and ready
		ReleaseAllSymbolsInRoot(nextSymbolsRoot);

		List<GameSymbol> newSymbols = new List<GameSymbol>();
		float step = currentReelData.SymbolSpacing + currentReelData.SymbolSize;

		for (int i = 0; i < currentReelData.SymbolCount; i++)
		{
			GameSymbol symbol = GameSymbolPool.Instance.Get(nextSymbolsRoot);

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

			// Use cached setter to avoid GetComponent allocations
			symbol.SetSizeAndLocalY(currentReelData.SymbolSize, step * i);

			newSymbols.Add(symbol);
		}

		symbols = newSymbols;

		SpawnDummySymbols(nextSymbolsRoot);
		SpawnDummySymbols(nextSymbolsRoot, false);

		// nextSymbolsRoot is already the buffer root
	}

	private void SpawnDummySymbols(Transform root, bool bottom = true, List<SymbolData> symbolData = null)
	{
		List<GameSymbol> dummies = new List<GameSymbol>();

		int startIndex = !bottom ? currentReelData.SymbolCount : 1;
		int flip = bottom ? -1 : 1;
		int total = bottom ? currentReelData.SymbolCount - 1 : currentReelData.SymbolCount;		
		float step = currentReelData.SymbolSpacing + currentReelData.SymbolSize;

		for (int i = 0; i < total; i++)
		{
			GameSymbol symbol = GameSymbolPool.Instance.Get(root);

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

			// Use helper to set size and Y in one call
			float y = (step * (i + startIndex)) * flip;
			symbol.SetSizeAndLocalY(currentReelData.SymbolSize, y);

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
	}

	private void CompleteReelSpin(List<SymbolData> solution)
	{
		// Release symbols from the old active root back to the pool
		ReleaseAllSymbolsInRoot(symbolRoot);

		// swap roots (old root becomes buffer for next spawn)
		var old = symbolRoot;
		symbolRoot = nextSymbolsRoot;
		nextSymbolsRoot = old;

		// position the buffer (nextSymbolsRoot) offscreen ready for future spawn
		float offsetY = ((currentReelData.SymbolSpacing + currentReelData.SymbolSize) * ((currentReelData.SymbolCount - 1) * 3));
		nextSymbolsRoot.localPosition = new Vector3(0, offsetY, 0);

		if (!completeOnNextSpin)
		{
			FallOut(solution);
		}
		else
		{
			spinning = false;

			for (int i = 0; i < symbols.Count; i++)
			{
				// batch-friendly: broadcast reel-level event instead of per-symbol if desired later
				eventManager.BroadcastEvent("SymbolLanded", symbols[i]);
			}

			eventManager.BroadcastEvent("ReelCompleted", ID);
		}
	}

	public void DimDummySymbols()
	{
		Color dim = new Color(0.5f, 0.5f, 0.5f);

		foreach (GameSymbol g in topDummySymbols)
		{
			var img = g.CachedImage;
			if (img != null) img.DOColor(dim, 0.1f);
		}

		foreach (GameSymbol g in bottomDummySymbols)
		{
			var img = g.CachedImage;
			if (img != null) img.DOColor(dim, 0.1f);
		}
	}

	public void ResetDimmedSymbols()
	{
		foreach (GameSymbol g in topDummySymbols)
		{
			var image = g.CachedImage;
			if (image == null) continue;
			image.DOKill();
			image.color = Color.white;
		}

		foreach (GameSymbol g in bottomDummySymbols)
		{
			var image = g.CachedImage;
			if (image == null) continue;
			image.DOKill();
			image.color = Color.white;
		}
	}

	/// <summary>
	/// Helper: release all GameSymbol instances parented under <paramref name="root"/> back to the pool.
	/// </summary>
	private void ReleaseAllSymbolsInRoot(Transform root)
	{
		if (root == null) return;

		// Release children until none remain — avoids allocating collections each spin.
		// We always take child at index 0 because Release() will reparent the child under the pool root,
		// which reduces root.childCount and lets us loop without extra allocations.
		while (root.childCount > 0)
		{
			var child = root.GetChild(0);
			if (child == null)
			{
				// Shouldn't happen but guard anyway
				continue;
			}

			if (child.TryGetComponent<GameSymbol>(out var symbol))
			{
				GameSymbolPool.Instance.Release(symbol);
			}
			else
			{
				// Fallback: if a non-symbol slipped in, destroy it (rare)
				GameObject.Destroy(child.gameObject);
			}
		}
	}

	/// <summary>
	/// Adjust symbol sizes and positions in-place to match a new symbol size/spacing.
	/// This updates children under both persistent roots and the next buffer offset without
	/// destroying or recreating any objects.
	/// </summary>
	public void UpdateSymbolLayout(float newSymbolSize, float newSpacing)
	{
		if (currentReelData == null) return;

		// Read old values from data model (these reflect the layout currently applied).
		float oldSymbolSize = currentReelData.SymbolSize;
		float oldSpacing = currentReelData.SymbolSpacing;
		float oldStep = oldSymbolSize + oldSpacing;
		if (oldStep <= 0f) oldStep = 1f; // guard

		float newStep = newSymbolSize + newSpacing;
		float ratio = newStep / oldStep;

		// Update the data model with new values
		currentReelData.SetSymbolSize(newSymbolSize, newSpacing);

		// Helper to update all GameSymbol children under a root: scale position by ratio and set sizeDelta.
		void UpdateRootChildren(Transform root)
		{
			if (root == null) return;

			for (int ci = 0; ci < root.childCount; ci++)
			{
				Transform child = root.GetChild(ci);
				if (child == null) continue;

				if (child.TryGetComponent<GameSymbol>(out var gs))
				{
					var rt = gs.CachedRect;
					if (rt != null)
					{
						// scale Y position relative to previous spacing/size to preserve ordering & signs
						Vector3 lp = rt.localPosition;
						lp.y = lp.y * ratio;
						rt.localPosition = lp;

						rt.sizeDelta = new Vector2(newSymbolSize, newSymbolSize);
					}
				}
			}
		}

		// Update both active and buffer roots
		UpdateRootChildren(symbolRoot);
		UpdateRootChildren(nextSymbolsRoot);

		// Recompute buffer offset for nextSymbolsRoot so future spawns are positioned correctly.
		if (nextSymbolsRoot != null)
		{
			float offsetY = ((currentReelData.SymbolSpacing + currentReelData.SymbolSize) * ((currentReelData.SymbolCount - 1) * 3));
			nextSymbolsRoot.localPosition = new Vector3(0, offsetY, 0);
		}

		// Update tracked symbol lists' transforms as well (they reference the same GameSymbol objects, but tweak just in case)
		float newStepLocal = newSymbolSize + newSpacing;
		for (int i = 0; i < symbols.Count; i++)
		{
			var gs = symbols[i];
			if (gs == null) continue;
			var rt = gs.CachedRect;
			if (rt != null)
			{
				rt.sizeDelta = new Vector2(newSymbolSize, newSymbolSize);
				rt.localPosition = new Vector3(rt.localPosition.x, newStepLocal * i, 0f);
			}
		}

		for (int i = 0; i < bottomDummySymbols.Count; i++)
		{
			var gs = bottomDummySymbols[i];
			if (gs == null) continue;
			var rt = gs.CachedRect;
			if (rt != null)
			{
				rt.sizeDelta = new Vector2(newSymbolSize, newSymbolSize);
				// bottom dummies start at index 1 and flip -1
				float y = (newStepLocal * (i + 1)) * -1f;
				rt.localPosition = new Vector3(rt.localPosition.x, y, 0f);
			}
		}

		for (int i = 0; i < topDummySymbols.Count; i++)
		{
			var gs = topDummySymbols[i];
			if (gs == null) continue;
			var rt = gs.CachedRect;
			if (rt != null)
			{
				rt.sizeDelta = new Vector2(newSymbolSize, newSymbolSize);
				// top dummies start at index SymbolCount and flip +1
				float y = (newStepLocal * (i + currentReelData.SymbolCount)) * 1f;
				rt.localPosition = new Vector3(rt.localPosition.x, y, 0f);
			}
		}
	}
}