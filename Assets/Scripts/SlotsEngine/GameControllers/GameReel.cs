using DG.Tweening;
using System;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;

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

	// Flag set by StopReel to indicate we should complete/land on next buffer swap
	private bool completeOnNextSpin = false;

	private Tweener[] activeSpinTweens = new Tweener[2];

	private EventManager eventManager;

	/// <summary>
	/// Initialize using a ReelStripDefinition asset. Creates a runtime strip instance.
	/// </summary>
	public void InitializeReel(ReelData data, int reelID, EventManager slotsEventManager, ReelStripDefinition stripDefinition)
	{
		currentReelData = data;
		id = reelID;
		eventManager = slotsEventManager;
		reelStrip = stripDefinition.CreateInstance();

		EnsureRootsCreated();
		SpawnReel(currentReelData.CurrentSymbolData);
	}

	/// <summary>
	/// Initialize using an existing runtime ReelStripData instance.
	/// </summary>
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

	public SymbolData GetRandomSymbolFromStrip(List<SymbolData> existingSelections)
	{
		return reelStrip.GetWeightedSymbol(existingSelections);
	}

	/// <summary>
	/// Ensure the two persistent roots used for active and buffered symbols exist. These roots are
	/// reused across spins to avoid creating/destroying many GameObjects every spin.
	/// </summary>
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

	/// <summary>
	/// Spawn the visible symbols under the active root. If existingSymbolData is provided it will be used
	/// to deterministically set symbols; otherwise random symbols are taken from the strip.
	/// </summary>
	private void SpawnReel(List<SymbolData> existingSymbolData)
	{
		// Clear any previous symbols on the active root and lists
		ReleaseAllSymbolsInRoot(symbolRoot);
		symbols.Clear();
		topDummySymbols.Clear();
		bottomDummySymbols.Clear();

		float step = currentReelData.SymbolSpacing + currentReelData.SymbolSize;

		// Track selections for per-reel MaxPerReel enforcement when generating symbols
		var selectedForThisReel = new List<SymbolData>();

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
				newSymbol = reelStrip.GetWeightedSymbol(selectedForThisReel);
			}

			sym.InitializeSymbol(newSymbol, eventManager);

			// Use cached component helper to avoid GetComponent each spin
			sym.SetSizeAndLocalY(currentReelData.SymbolSize, step * i);

			symbols.Add(sym);
			if (newSymbol != null) selectedForThisReel.Add(newSymbol);
		}

		// Spawn dummies (buffer visuals) above and below the active list
		SpawnDummySymbols(symbolRoot, true, null, true, selectedForThisReel);
		SpawnDummySymbols(symbolRoot, false, null, true, selectedForThisReel);
	}

	/// <summary>
	/// Begin a spin. Optionally accepts a target solution to land on and a start delay. Triggers
	/// a small bounce before starting the continuous fall animation.
	/// </summary>
	public void BeginSpin(List<SymbolData> solution = null, float startDelay = 0f)
	{
		completeOnNextSpin = false;

		DOTween.Sequence().AppendInterval(startDelay).AppendCallback(() =>
		{
			BounceReel(Vector3.up, strength: 50f, peak: 0.8f, duration: 0.25f, onComplete: () =>
			{
				FallOut(solution, true);
				spinning = true;
				// use enum-based broadcast to match EventManager registrations
				eventManager.BroadcastEvent(SlotsEvent.ReelSpinStarted, ID);
			});
		});
	}

	/// <summary>
	/// Request that the reel complete on its next landing. This accelerates active tweens to produce a slam effect.
	/// </summary>
	public void StopReel(float delay = 0f)
	{
		DOTween.Sequence().AppendInterval(delay).AppendCallback(() =>
		{
			completeOnNextSpin = true;

			//	slam the reels by increasing timeScale on the active tweens
			activeSpinTweens[0].timeScale = 4f;
			activeSpinTweens[1].timeScale = 4f;
		});
	}

	public void ApplySolution(List<SymbolDefinition> symbols)
	{

	}

	/// <summary>
	/// Prepare the buffered root with the next set of symbols. Positions the buffer offscreen and fills it.
	/// If a `solution` is provided it will be used to deterministically populate the buffer.
	/// </summary>
	private void SpawnNextReel(List<SymbolData> solution = null)
	{
		// Position the buffer root offscreen (top). This mirrors the previous Create+position.
		float offsetY = ((currentReelData.SymbolSpacing + currentReelData.SymbolSize) * ((currentReelData.SymbolCount - 1) * 3));
		nextSymbolsRoot.localPosition = new Vector3(0, offsetY, 0);

		// Ensure buffer is cleared and ready
		ReleaseAllSymbolsInRoot(nextSymbolsRoot);

		List<GameSymbol> newSymbols = new List<GameSymbol>();
		float step = currentReelData.SymbolSpacing + currentReelData.SymbolSize;

		// Track picks for MaxPerReel enforcement in buffer generation
		var pickedForBuffer = new List<SymbolData>();

		// Build combined existing selections that include current active symbols so buffer generation respects MaxPerReel
		var combinedExisting = new List<SymbolData>();
		// include any already-picked buffer symbols as we build
		if (pickedForBuffer != null && pickedForBuffer.Count > 0) combinedExisting.AddRange(pickedForBuffer);
		// include currently active landed symbols from `symbols` list
		if (symbols != null)
		{
			for (int s = 0; s < symbols.Count; s++)
			{
				var gs = symbols[s];
				if (gs == null) continue;
				var sd = gs.CurrentSymbolData;
				if (sd != null) combinedExisting.Add(sd);
			}
		}

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
				// Use combinedExisting so MaxPerReel counts include current active symbols as well as picks in the buffer
				def = reelStrip.GetWeightedSymbol(combinedExisting);
			}

			symbol.InitializeSymbol(def, eventManager);

			// Use cached setter to avoid GetComponent allocations
			symbol.SetSizeAndLocalY(currentReelData.SymbolSize, step * i);

			newSymbols.Add(symbol);
			if (def != null)
			{
				pickedForBuffer.Add(def);
				combinedExisting.Add(def); // keep combined up to date for subsequent picks
			}
		}

		// Replace current symbol references with the buffered ones. The visual swap will occur on tween completion.
		symbols = newSymbols;

		// Spawn dummies for the buffer. These are created while the reel is already spinning,
		// so they should remain unfaded (white) when spawned. Pass dim=false to keep them white.
		SpawnDummySymbols(nextSymbolsRoot, true, null, false, combinedExisting);
		SpawnDummySymbols(nextSymbolsRoot, false, null, false, combinedExisting);

		// nextSymbolsRoot is already the buffer root
	}

	/// <summary>
	/// Spawn dummy symbols used to pad the reel visuals above and below the active range.
	/// `bottom` controls which side to create. When `dim` is true the dummy images are tinted to a dim color.
	/// </summary>
	private void SpawnDummySymbols(Transform root, bool bottom = true, List<SymbolData> symbolData = null, bool dim = true, List<SymbolData> existingSelections = null)
	{
		List<GameSymbol> dummies = new List<GameSymbol>();

		int startIndex = !bottom ? currentReelData.SymbolCount : 1;
		int flip = bottom ? -1 : 1;
		int total = bottom ? currentReelData.SymbolCount - 1 : currentReelData.SymbolCount;		
		float step = currentReelData.SymbolSpacing + currentReelData.SymbolSize;

		// Color used for initial dimming
		Color dimColor = new Color(0.5f, 0.5f, 0.5f);

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
				def = reelStrip.GetWeightedSymbol(existingSelections);
			}

			symbol.InitializeSymbol(def, eventManager);

			// Use helper to set size and Y in one call
			float y = (step * (i + startIndex)) * flip;
			symbol.SetSizeAndLocalY(currentReelData.SymbolSize, y);

			// Apply initial color according to `dim` flag. If dim==true (default for initialization/adjust),
			// set to the dim color immediately. If dim==false (used when spawning buffer during spin), keep white.
			var img = symbol.CachedImage;
			if (img != null)
			{
				img.DOKill();
				img.color = dim ? dimColor : Color.white;
			}

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
	/// <summary>
	/// Start the fall animations for the active and buffered roots. When both complete the landing is processed.
	/// </summary>
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

	/// <summary>
	/// Called after both fall tweens complete; triggers landing bounce or immediate completion.
	/// </summary>
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

	/// <summary>
	/// Small visual pulse used before/after landing. Uses custom easing helpers to animate roots.
	/// </summary>
	private void BounceReel(Vector3 direction, float strength = 100f, float duration = 0.5f, float sharpness = 0f, float peak = 0.4f, Action onComplete = null)
	{
		if (nextSymbolsRoot != null)
		{
			nextSymbolsRoot.DOPulseUp(direction, strength, duration, sharpness, peak).SetEase(Ease.Linear);
		}

		symbolRoot.DOPulseUp(direction, strength, duration, sharpness, peak).SetEase(Ease.Linear).OnComplete(() => { if (onComplete != null) onComplete(); });
	}

	/// <summary>
	/// Complete the spin: release old symbols, swap roots, broadcast landed/completed events and optionally
	/// schedule another cycle if not requested to stop.
	/// </summary>
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
			// continue spinning (start another fallout)
			FallOut(solution);
		}
		else
		{
			spinning = false;

			// Debug: dump final symbols for this reel to help verify ordering/mapping
			if ((Application.isEditor || Debug.isDebugBuild) && WinlineEvaluator.Instance != null && WinlineEvaluator.Instance.LoggingEnabled)
			{
				var names = symbols.Select(s => s != null && s.CurrentSymbolData != null ? s.CurrentSymbolData.Name : "(null)").ToArray();
				Debug.Log($"Reel {ID} landed symbols (bottom->top): [{string.Join(",", names)}]");
			}

			for (int i = 0; i < symbols.Count; i++)
			{
				// batch-friendly: broadcast reel-level event instead of per-symbol if desired later
				eventManager.BroadcastEvent(SlotsEvent.SymbolLanded, symbols[i]);
			}

			eventManager.BroadcastEvent(SlotsEvent.ReelCompleted, ID);
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
	/// Using child-at-index-0 loop avoids creating temporary collections each spin.
	/// </summary>
	private void ReleaseAllSymbolsInRoot(Transform root)
	{
		if (root == null) return;

		// Release children until none remain ? avoids allocating collections each spin.
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

		// Helper to update all GameSymbol children under a root: scale position by ratio and set sizeDelta
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
		};

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

		// Ensure dummy symbols reflect dimmed state when resizing outside of a spin.
		if (!spinning)
		{
			DimDummySymbols();
		}
	}

	/// <summary>
	/// Change the number of visible symbols on this reel at runtime. This will update the data model and either
	/// incrementally add/remove visible symbols or fully respawn roots depending on the `incremental` flag.
	/// </summary>
	public void SetSymbolCount(int newCount, bool incremental = true)
	{
		if (newCount < 1) newCount = 1;

		// Do not allow layout changes while spinning
		if (spinning)
		{
			Debug.LogWarning("Cannot change symbol count while reel is spinning.");
			return;
		}

		int oldCount = currentReelData.SymbolCount;
		if (newCount == oldCount) return;

		// Update data model count first
		currentReelData.SetSymbolCount(newCount);

		// Ensure current symbol data list matches the desired length
		var dataList = currentReelData.CurrentSymbolData ?? new List<SymbolData>();

		if (dataList.Count > newCount)
		{
			// trim
			dataList.RemoveRange(newCount, dataList.Count - newCount);
		}
		else if (dataList.Count < newCount)
		{
			// append random symbols from strip to fill
			for (int i = dataList.Count; i < newCount; i++)
			{
				dataList.Add(reelStrip.GetWeightedSymbol(dataList));
			}
		}

		currentReelData.SetCurrentSymbolData(dataList);

		// If full respawn requested, reuse existing SpawnReel behavior
		if (!incremental)
		{
			SpawnReel(currentReelData.CurrentSymbolData);

			// Notify manager to adjust layouts since visual count changed
			SlotsEngineManager.Instance.AdjustSlotsCanvases();
			return;
		}

		// Incremental path: add or remove active symbols without destroying other active symbols
		float step = currentReelData.SymbolSpacing + currentReelData.SymbolSize;

		if (newCount > oldCount)
		{
			// Add new symbols at the end
			for (int i = oldCount; i < newCount; i++)
			{
				GameSymbol sym = GameSymbolPool.Instance.Get(symbolRoot);
				SymbolData def = (currentReelData.CurrentSymbolData != null && currentReelData.CurrentSymbolData.Count > i)
					? currentReelData.CurrentSymbolData[i]
					: reelStrip.GetWeightedSymbol(currentReelData.CurrentSymbolData);
				sym.InitializeSymbol(def, eventManager);
				sym.SetSizeAndLocalY(currentReelData.SymbolSize, step * i);
				symbols.Add(sym);
			}
		}
		else // newCount < oldCount
		{
			// Remove symbols from the end (return to pool)
			for (int i = oldCount - 1; i >= newCount; i--)
			{
				if (i < 0 || i >= symbols.Count) continue;
				var sym = symbols[i];
				if (sym != null) GameSymbolPool.Instance.Release(sym);
				symbols.RemoveAt(i);
			}
		}

		// Recreate dummy symbols: free existing dummies and spawn new ones to reflect the new count
		if (topDummySymbols != null)
		{
			for (int i = 0; i < topDummySymbols.Count; i++) if (topDummySymbols[i] != null) GameSymbolPool.Instance.Release(topDummySymbols[i]);
			topDummySymbols.Clear();
		}

		if (bottomDummySymbols != null)
		{
			for (int i = 0; i < bottomDummySymbols.Count; i++) if (bottomDummySymbols[i] != null) GameSymbolPool.Instance.Release(bottomDummySymbols[i]);
			bottomDummySymbols.Clear();
		}

		// Spawn updated dummies under the active root
		SpawnDummySymbols(symbolRoot, true, null, true, currentReelData.CurrentSymbolData);
		SpawnDummySymbols(symbolRoot, false, null, true, currentReelData.CurrentSymbolData);

		// Clear any buffered symbols in nextSymbolsRoot so future buffer spawn matches new count
		ReleaseAllSymbolsInRoot(nextSymbolsRoot);

		// Recompute buffer offset
		if (nextSymbolsRoot != null)
		{
			float offsetY = ((currentReelData.SymbolSpacing + currentReelData.SymbolSize) * ((currentReelData.SymbolCount - 1) * 3));
			nextSymbolsRoot.localPosition = new Vector3(0, offsetY, 0);
		}

		// Ensure all active symbols have correct size/position
		for (int i = 0; i < symbols.Count; i++)
		{
			var gs = symbols[i];
			if (gs == null) continue;
			var rt = gs.CachedRect;
			if (rt != null)
			{
				rt.sizeDelta = new Vector2(currentReelData.SymbolSize, currentReelData.SymbolSize);
				rt.localPosition = new Vector3(rt.localPosition.x, step * i, 0f);
			}
		}

		// Notify manager to adjust layouts since visual count changed
		SlotsEngineManager.Instance.AdjustSlotsCanvases();
	}
}