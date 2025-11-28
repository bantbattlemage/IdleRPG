using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class SlotsEngine : MonoBehaviour
{
	[SerializeField] private GameObject reelPrefab;
	private SlotsData currentSlotsData; public SlotsData CurrentSlotsData => currentSlotsData;
	private Transform reelsRootTransform; public Transform ReelsRootTransform => reelsRootTransform;
	private List<GameReel> reels = new List<GameReel>(); public List<GameReel> CurrentReels => reels;
	private EventManager eventManager; private SlotsStateMachine stateMachine; private bool spinInProgress = false; private Transform currentReelsGroup;
	public State CurrentState => stateMachine.CurrentState;
	/// <summary>
	/// Sets the current state on the internal <see cref="SlotsStateMachine"/> instance.
	/// </summary>
	/// <param name="state">Target state to apply.</param>
	public void SetState(State state) => stateMachine.SetState(state);

	// NEW: page activation flag for suspension
	private bool pageActive = true;
	/// <summary>
	/// Activates or deactivates the page for this engine. When deactivated reels may suspend animations/updates.
	/// The flag is propagated to child `GameReel` instances.
	/// </summary>
	/// <param name="active">True to mark the page active, false to deactivate.</param>
	public void SetPageActive(bool active)
	{
		pageActive = active;
		foreach (var r in reels)
			try { r?.SetPageActive(active); } catch { }
	}
	/// <summary>
	/// Indicates whether this engine's page is currently active. When false, reels may be suspended.
	/// </summary>
	public bool IsPageActive => pageActive;

	/// <summary>
	/// Creates a fresh `SlotsData` instance from the provided definition and initializes the engine.
	/// </summary>
	public void InitializeSlotsEngine(Transform canvasTransform, SlotsDefinition definition)
	{
		currentSlotsData = definition.CreateInstance();
		for (int i = 0; i < definition.ReelDefinitions.Length; i++)
		{
			ReelData newData = definition.ReelDefinitions[i].CreateInstance();
			currentSlotsData.AddNewReel(newData);
		}
		InitializeSlotsEngine(canvasTransform, currentSlotsData);
	}

	private List<Action<object>> pendingReelChangedHandlers = new List<Action<object>>();

	/// <summary>
	/// Initializes the engine with an existing `SlotsData` instance, wiring events and spawning reels.
	/// Safe to call with externally loaded data (e.g., after persistence load).
	/// </summary>
	public void InitializeSlotsEngine(Transform canvasTransform, SlotsData data)
	{
		currentSlotsData = data;
		try { if (SlotsDataManager.Instance != null && currentSlotsData != null) SlotsDataManager.Instance.UpdateSlotsData(currentSlotsData); } catch (Exception ex) { Debug.LogException(ex); }
		eventManager = new EventManager();
		if (pendingReelChangedHandlers.Count > 0)
		{
			foreach (var h in pendingReelChangedHandlers) { eventManager.RegisterEvent(SlotsEvent.ReelAdded, h); eventManager.RegisterEvent(SlotsEvent.ReelRemoved, h); }
			pendingReelChangedHandlers.Clear();
		}
		stateMachine = new SlotsStateMachine(); stateMachine.InitializeStateMachine(this, eventManager);
		eventManager.RegisterEvent(SlotsEvent.SpinCompleted, OnSpinCompleted);
		eventManager.RegisterEvent(SlotsEvent.ReelSpinStarted, OnReelSpinStarted);
		eventManager.RegisterEvent(SlotsEvent.ReelCompleted, OnReelCompleted);
		eventManager.RegisterEvent(State.Presentation, "Enter", OnPresentationEnter);
		eventManager.RegisterEvent(SlotsEvent.PresentationComplete, OnPresentationComplete);

		// Listen for group-level presentation completion so engines can transition to Idle together
		try { GlobalEventManager.Instance?.RegisterEvent(SlotsEvent.PresentationCompleteGroup, OnPresentationCompleteGroup); } catch { }
		reelsRootTransform = canvasTransform;
		SpawnReels(reelsRootTransform);
	}

	/// <summary>
	/// Starts the state machine; should be called once after initialization.
	/// </summary>
	public void BeginSlots() => stateMachine.BeginStateMachine();

	/// <summary>
	/// High-level spin control used by UI: when <paramref name="spin"/> is true starts a spin, when false attempts to stop reels.
	/// Also validates player credits before initiating a spin.
	/// </summary>
	public void SpinOrStopReels(bool spin)
	{
		if (!spinInProgress && spin) SpinAllReels();
		// Always attempt to stop when a spin is in progress. Do not gate on per-reel Spinning flags or
		// strict stateMachine state here — a single Stop request should stop all reels (including
		// those suspended due to being off-page). Individual reels will unpause as needed to finish.
		else if (spinInProgress && !spin) StopAllReels();
		else if (GamePlayer.Instance.CurrentCredits < GamePlayer.Instance.CurrentBet.CreditCost) SlotConsoleController.Instance.SetConsoleMessage("Not enough credits! Switch to a lower bet or add credits.");
	}

	// Coroutine handle used to stagger starting reels from a single scheduler rather than per-reel sequences
	private Coroutine staggerSpinCoroutine;
	// Counter used to detect when all reels have signalled that their spin started
	private int reelsStartedCount = 0;
	// Tracks whether a stop has already been requested for the current spin
	private bool stopRequested = false;
	// If an external caller requests a stop before all reels have started, defer until ready
	private bool deferredStopRequested = false;

	// Safety margin to wait for per-reel kickup to complete before allowing stops to take effect
	private const float KickupSafetyMargin = 0.25f;

	/// <summary>
	/// Randomizes each reel's visible symbols based on its strip and begins the spin with a slight stagger.
	/// Notifies the `WinEvaluator` that a new spin started when logging is enabled.
	/// </summary>
	private void SpinAllReels()
	{
		// Clear any previous stop request when starting a new spin
		stopRequested = false;
		if (spinInProgress) throw new InvalidOperationException("Spin already in progress!");
		try { if (WinEvaluator.Instance != null && WinEvaluator.Instance.LoggingEnabled) WinEvaluator.Instance.NotifySpinStarted(); } catch { }

		// Prepare per-reel solutions. Pre-size lists to avoid intermediate resizes when many reels are present.
		var perReelSolutions = new List<List<SymbolData>>(reels.Count);
		for (int i = 0; i < reels.Count; i++)
		{
			int symbolCount = reels[i].CurrentReelData != null ? reels[i].CurrentReelData.SymbolCount : 0;
			var testSolution = new List<SymbolData>(Math.Max(1, symbolCount));
			var selectionsForReel = new List<SymbolData>(Math.Max(1, symbolCount));
			for (int k = 0; k < symbolCount; k++) { var symbol = reels[i].GetRandomSymbolFromStrip(selectionsForReel); testSolution.Add(symbol); if (symbol != null) selectionsForReel.Add(symbol); }
			reels[i].CurrentReelData.SetCurrentSymbolData(testSolution);
			perReelSolutions.Add(testSolution);
		}

		// Reset started counter and use a single coroutine to stagger BeginSpinImmediate calls to avoid per-reel scheduling allocations.
		reelsStartedCount = 0;
		// If there are zero reels, nothing to do
		if (reels.Count == 0) return;

		// Use a single coroutine to stagger BeginSpinImmediate calls to avoid per-reel scheduling allocations.
		float falloutStep = 0.025f;
		if (staggerSpinCoroutine != null) StopCoroutine(staggerSpinCoroutine);
		staggerSpinCoroutine = StartCoroutine(StaggeredBeginSpin(perReelSolutions, falloutStep));
	}

	private System.Collections.IEnumerator StaggeredBeginSpin(List<List<SymbolData>> solutions, float step)
	{
		// Schedule all reel starts in the same frame using per-reel delays to avoid creation-time timing differences
		for (int i = 0; i < reels.Count; i++)
		{
			var sol = (i < solutions.Count) ? solutions[i] : null;
			try
			{
				float delay = step * i;
				reels[i].BeginSpin(sol, delay);
			}
			catch { }
		}

		// coroutine completes immediately after scheduling
		staggerSpinCoroutine = null;
		yield break;
	}

	private void OnReelSpinStarted(object obj)
	{
		// Called each time a reel broadcasts that it has started spinning.
		// Mark engine as spinning on the first notification and track how many reels have started.
		if (!spinInProgress)
		{
			spinInProgress = true; stateMachine.SetState(State.Spinning);
		}
		try
		{
			reelsStartedCount++;
			// When every reel has started, notify listeners (e.g., UI) that the Stop button may be enabled.
			if (reelsStartedCount >= reels.Count)
			{
				reelsStartedCount = reels.Count; // clamp
				try { eventManager.BroadcastEvent(SlotsEvent.ReelsAllStarted); } catch { }
				// If a deferred stop was requested earlier, execute it now
				if (deferredStopRequested)
				{
					deferredStopRequested = false;
					StopAllReels();
				}
			}
		}
		catch { }
	}

	/// <summary>
	/// Request that this engine stop when it is ready (i.e., after all reels have started kickup).
	/// If the engine is already ready, the stop will be applied immediately.
	/// Subsequent requests during the same spin are ignored.
	/// </summary>
	public void RequestStopWhenReady()
	{
		if (!spinInProgress) return; // nothing to stop
		if (stopRequested) return; // already requested
		if (reelsStartedCount >= reels.Count)
		{
			StopAllReels();
		}
		else
		{
			deferredStopRequested = true;
		}
	}

	/// <summary>
	/// Requests all reels to stop with a staggered delay and broadcasts a StoppingReels event.
	/// This will request each reel to stop regardless of its current spinning flag. Reels that are
	/// suspended because their page is inactive will be unpaused briefly so they can complete stop
	/// logic; OnReelCompleted will handle updating engine state when all reels have finished.
	/// </summary>
	private void StopAllReels()
	{
		if (!spinInProgress) return; // nothing to stop
		// If a stop has already been requested for this spin, ignore subsequent requests
		if (stopRequested) return;
		stopRequested = true;
		float baseStagger = 0.025f;
		// If this engine/page is inactive, request immediate stops (no stagger) so reels don't wait
		bool enginePageActive = IsPageActive;
		// When active, ensure stop requests don't occur before the last reel's kickup begins.
		// Compute a conservative minimum delay that guarantees all per-reel BeginSpin(startDelay) calls
		// have started and their kickup period elapsed. Use a safety margin equal to expected kickup duration.
		float ensureAllStartedDelay = 0f;
		if (enginePageActive)
		{
			ensureAllStartedDelay = baseStagger * reels.Count + KickupSafetyMargin;
		}
		for (int i = 0; i < reels.Count; i++)
		{
			// Schedule stop so it will not fire before the reel's own scheduled start.
			float delay = enginePageActive ? (ensureAllStartedDelay + baseStagger * i) : 0f;
			reels[i].StopReel(delay);
		}
		// Notify listeners that a stop has been requested
		eventManager.BroadcastEvent(SlotsEvent.StoppingReels);
	}

	/// <summary>
	/// Spawns visual reel instances under the provided canvas transform and positions them in a centered grid.
	/// </summary>
	private void SpawnReels(Transform gameCanvas)
	{
		if (currentReelsGroup != null) { Destroy(currentReelsGroup.gameObject); reels.Clear(); }
		Transform reelsGroup = new GameObject("ReelsGroup").transform; reelsGroup.parent = gameCanvas.transform; reelsGroup.localScale = Vector3.one;

		// Prewarm global pool conservatively based on reel definitions to avoid allocation gaps on first spins
		try
		{
			if (GameSymbolPool.Instance != null && currentSlotsData?.CurrentReelData != null)
			{
				int totalEstimate = 0;
				foreach (var rd in currentSlotsData.CurrentReelData)
				{
					if (rd == null) continue;
					int symbolCount = Mathf.Max(1, rd.SymbolCount);
					int perReelEstimate = symbolCount * 6 + 24; // matches per-reel maxReasonable used elsewhere
					totalEstimate += perReelEstimate;
				}
				if (totalEstimate > 0) GameSymbolPool.Instance.Prewarm(totalEstimate);
			}
		}
		catch { }

		for (int i = 0; i < currentSlotsData.CurrentReelData.Count; i++)
		{
			ReelData data = currentSlotsData.CurrentReelData[i]; GameObject g = Instantiate(reelPrefab, reelsGroup.transform); GameReel reel = g.GetComponent<GameReel>();
			if (data.CurrentReelStrip != null) reel.InitializeReel(data, i, eventManager, data.CurrentReelStrip, this); else reel.InitializeReel(data, i, eventManager, data.DefaultReelStrip, this);
			// Defer precise horizontal positioning until after all reels are created to avoid incremental drift
			reels.Add(reel);
		}

		// Prewarm per-reel pools immediately after creating reels to avoid gaps on first spins
		foreach (var r in reels) r?.PrewarmPooledSymbols();

		// Compute stable centered X positions using a single step value (derived from the first reel definition)
		if (reels.Count > 0)
		{
			ReelData reelDef = currentSlotsData.CurrentReelData[0];
			float stepX = reelDef.SymbolSize + reelDef.SymbolSpacing;
			int n = reels.Count;
			// startX positions the first reel so that reels are centered around x=0
			float startX = -((n - 1) * stepX) / 2f;
			for (int i = 0; i < reels.Count; i++)
			{
				var g = reels[i]?.gameObject;
				if (g != null) g.transform.localPosition = new Vector3(startX + stepX * i, 0f, 0f);
			}

			// Compute vertical centering based on tallest reel (max symbol rows)
			int maxRows = currentSlotsData.CurrentReelData.Max(x => x.SymbolCount);
			float stepY = reelDef.SymbolSize + reelDef.SymbolSpacing;
			float startY = -((maxRows - 1) * stepY) / 2f;
			reelsGroup.transform.localPosition = new Vector3(0f, startY, 0f);
		}
		currentReelsGroup = reelsGroup;
		RegenerateAllReelDummies();

		// Prewarm the GameSymbol pool to avoid runtime expansion: estimate needed based on reels * max symbols per reel
		try
		{
			int maxPerReel = 0;
			if (currentSlotsData?.CurrentReelData != null && currentSlotsData.CurrentReelData.Count > 0)
			{
				foreach (var rd in currentSlotsData.CurrentReelData) if (rd != null && rd.SymbolCount > maxPerReel) maxPerReel = rd.SymbolCount;
			}
			int reelCountFinal = reels.Count > 0 ? reels.Count : (currentSlotsData?.CurrentReelData?.Count ?? 0);
			int prewarmCount = Mathf.Max(0, reelCountFinal * maxPerReel);
			if (prewarmCount > 0 && GameSymbolPool.Instance != null)
			{
				GameSymbolPool.Instance.Prewarm(prewarmCount);
			}
		}
		catch { }
	}

	public void RegenerateAllReelDummies()
	{
		foreach (var r in reels) if (r != null) r.RegenerateDummies();
	}

	/// <summary>
	/// Recomputes X positions for each reel using SymbolSize and SymbolSpacing and re-centers the reels group.
	/// </summary>
	private void RepositionReels()
	{
		if (reels.Count == 0 || currentSlotsData.CurrentReelData.Count == 0) return;
		// Use first reel definition as authoritative for spacing step to ensure consistent layout
		ReelData reelDef = currentSlotsData.CurrentReelData[0];
		float stepX = reelDef.SymbolSize + reelDef.SymbolSpacing;
		int n = reels.Count;
		float startX = -((n - 1) * stepX) / 2f;
		for (int i = 0; i < reels.Count; i++)
		{
			var data = currentSlotsData.CurrentReelData[i];
			var g = reels[i].gameObject;
			if (g != null) g.transform.localPosition = new Vector3(startX + stepX * i, 0f, 0f);
		}

		// Vertical centering based on tallest reel
		int maxRows = currentSlotsData.CurrentReelData.Max(x => x.SymbolCount);
		float stepY = reelDef.SymbolSize + reelDef.SymbolSpacing;
		float startY = -((maxRows - 1) * stepY) / 2f;
		if (currentReelsGroup != null) currentReelsGroup.transform.localPosition = new Vector3(0f, startY, 0f);
	}

	/// <summary>
	/// Adjusts reel symbol size and spacing to fit within the provided height/width, preserving aspect and padding.
	/// Will respawn reels if none exist yet.
	/// </summary>
	public void AdjustReelSize(float totalHeight, float totalWidth)
	{
		if (stateMachine.CurrentState == State.Spinning) throw new InvalidOperationException("Should not adjust reels while they are spinning!");
		if (reels == null || reels.Count == 0)
		{
			int maxSymbolsPre = currentSlotsData.CurrentReelData.Max(x => x.SymbolCount);
			float availableStepPre = (totalHeight / maxSymbolsPre) * 0.8f;
			float spacingPre = availableStepPre * 0.2f;
			foreach (ReelData r in currentSlotsData.CurrentReelData) r.SetSymbolSize(availableStepPre - spacingPre, spacingPre);
			SpawnReels(reelsRootTransform);
			return;
		}
		int maxSymbols = currentSlotsData.CurrentReelData.Max(x => x.SymbolCount);
		int reelCount = currentSlotsData.CurrentReelData.Count;
		const float heightFill = 0.8f; const float widthFill = 0.95f; const float spacingFactor = 0.1f;
		float symbolMaxByHeight = float.MaxValue; if (totalHeight > 0f && maxSymbols > 0) { float stepPerSymbol = 1f + spacingFactor; symbolMaxByHeight = (totalHeight * heightFill) / (maxSymbols * stepPerSymbol); }
		float symbolMaxByWidth = float.MaxValue; if (totalWidth > 0f && reelCount > 0) { float denom = reelCount + (reelCount - 1) * spacingFactor; if (denom > 0f) symbolMaxByWidth = (totalWidth * widthFill) / denom; }
		float chosenSymbolSize = Math.Min(symbolMaxByHeight, symbolMaxByWidth); if (float.IsInfinity(chosenSymbolSize) || float.IsNaN(chosenSymbolSize) || chosenSymbolSize <= 0f) chosenSymbolSize = currentSlotsData.CurrentReelData[0].SymbolSize;
		float chosenSpacing = chosenSymbolSize * spacingFactor;
		ReelData firstDef = currentSlotsData.CurrentReelData[0]; bool sizeMatches = Mathf.Approximately(firstDef.SymbolSize, chosenSymbolSize) && Mathf.Approximately(firstDef.SymbolSpacing, chosenSpacing);
		bool reelsCountMatches = reels.Count == currentSlotsData.CurrentReelData.Count; if (sizeMatches && reelsCountMatches) return;
		foreach (ReelData r in currentSlotsData.CurrentReelData) r.SetSymbolSize(chosenSymbolSize, chosenSpacing);
		// Use stable centered positions when updating transforms
		float step = chosenSpacing + chosenSymbolSize;
		int nCount = reels.Count;
		float start = -((nCount - 1) * step) / 2f;
		for (int i = 0; i < reels.Count; i++) { var reel = reels[i]; if (reel == null) continue; reel.UpdateSymbolLayout(chosenSymbolSize, chosenSpacing); reel.transform.localPosition = new Vector3(start + step * i, 0f, 0f); }
		int maxRowsNow = currentSlotsData.CurrentReelData.Max(x => x.SymbolCount);
		float startY = -((maxRowsNow - 1) * (chosenSymbolSize + chosenSpacing)) / 2f;
		if (currentReelsGroup != null) currentReelsGroup.transform.localPosition = new Vector3(0f, startY, 0f);
		RegenerateAllReelDummies();
	}

	/// <summary>
	/// Adjusts reel symbol size and spacing when a reel's height change affects the max height.
	/// This is called from GameReel.SetSymbolCount to trigger proper rescaling while preserving the selective dummy regeneration that was already done.
	/// </summary>
	public void AdjustReelSizeForHeightChange(float totalHeight, float totalWidth)
	{
		if (stateMachine.CurrentState == State.Spinning) throw new InvalidOperationException("Should not adjust reels while they are spinning!");
		if (reels == null || reels.Count == 0) return;
		
		int maxSymbols = currentSlotsData.CurrentReelData.Max(x => x.SymbolCount); int reelCount = currentSlotsData.CurrentReelData.Count; const float heightFill = 0.8f; const float widthFill = 0.95f; const float spacingFactor = 0.1f;
		float symbolMaxByHeight = float.MaxValue; if (totalHeight > 0f && maxSymbols > 0) { float stepPerSymbol = 1f + spacingFactor; symbolMaxByHeight = (totalHeight * heightFill) / (maxSymbols * stepPerSymbol); }
		float symbolMaxByWidth = float.MaxValue; if (totalWidth > 0f && reelCount > 0) { float denom = reelCount + (reelCount - 1) * spacingFactor; if (denom > 0f) symbolMaxByWidth = (totalWidth * widthFill) / denom; }
		float chosenSymbolSize = Math.Min(symbolMaxByHeight, symbolMaxByWidth); if (float.IsInfinity(chosenSymbolSize) || float.IsNaN(chosenSymbolSize) || chosenSymbolSize <= 0f) chosenSymbolSize = currentSlotsData.CurrentReelData[0].SymbolSize; float chosenSpacing = chosenSymbolSize * spacingFactor;
		ReelData firstDef = currentSlotsData.CurrentReelData[0]; bool sizeMatches = Mathf.Approximately(firstDef.SymbolSize, chosenSymbolSize) && Mathf.Approximately(firstDef.SymbolSpacing, chosenSpacing); bool reelsCountMatches = reels.Count == currentSlotsData.CurrentReelData.Count; if (sizeMatches && reelsCountMatches) return;
		
		foreach (ReelData r in currentSlotsData.CurrentReelData) r.SetSymbolSize(chosenSymbolSize, chosenSpacing);
		// Use lightweight resize and stable centered positions
		float step = chosenSpacing + chosenSymbolSize;
		int nCount = reels.Count;
		float start = -((nCount - 1) * step) / 2f;
		for (int i = 0; i < reels.Count; i++) { var reel = reels[i]; if (reel == null) continue; /* use lightweight resize to avoid regenerating dummies */ reel.ResizeVisuals(chosenSymbolSize, chosenSpacing); reel.transform.localPosition = new Vector3(start + step * i, 0f, 0f); }
		int maxRowsNow = currentSlotsData.CurrentReelData.Max(x => x.SymbolCount);
		float startY = -((maxRowsNow - 1) * (chosenSymbolSize + chosenSpacing)) / 2f;
		if (currentReelsGroup != null) currentReelsGroup.transform.localPosition = new Vector3(0f, startY, 0f);
		// Note: Do NOT call RegenerateAllReelDummies here - that was already done selectively by the caller
	}
	
	void OnReelCompleted(object obj)
	{
		try
		{
			// Consider a reel complete only when its motion is fully settled. This avoids reporting spin
			// completion while some reels are still running landing coroutines or suspended awaiting resume,
			// which could lead to presentation/state desync and visual offsets.
			bool allDone = reels != null && reels.Count > 0 && reels.All(r => r != null && r.IsMotionComplete());
			if (allDone && spinInProgress)
			{
				spinInProgress = false;
				// Reset started counter for next spin
				reelsStartedCount = 0;
				// Clear stop guard for next spin
				stopRequested = false;
				eventManager.BroadcastEvent(SlotsEvent.SpinCompleted);
			}
		}
		catch { }
	}

	/// <summary>
	/// Called when all reels have stopped spinning; evaluates wins through `WinEvaluator` and transitions to Presentation state.
	/// </summary>
	void OnSpinCompleted(object obj)
	{
		try
		{
			GameSymbol[] grid = GetCurrentSymbolGrid(); int columns = currentSlotsData != null && currentSlotsData.CurrentReelData != null ? currentSlotsData.CurrentReelData.Count : reels.Count; int[] rowsPerColumn = new int[columns];
			for (int i = 0; i < columns; i++) { if (currentSlotsData != null && currentSlotsData.CurrentReelData != null && i < currentSlotsData.CurrentReelData.Count) rowsPerColumn[i] = currentSlotsData.CurrentReelData[i].SymbolCount; else if (i < reels.Count && reels[i]?.CurrentReelData != null) rowsPerColumn[i] = reels[i].CurrentReelData.SymbolCount; else rowsPerColumn[i] = 1; }
			List<WinlineDefinition> winlineDefs = currentSlotsData?.WinlineDefinitions;
			if (WinEvaluator.Instance != null) { try { var wins = WinEvaluator.Instance.EvaluateWinsFromGameSymbols(grid, columns, rowsPerColumn, winlineDefs); if (WinEvaluator.Instance.LoggingEnabled) WinEvaluator.Instance.LogSpinResult(grid, columns, rowsPerColumn, winlineDefs ?? new List<WinlineDefinition>(), wins); } catch (Exception ex) { Debug.LogWarning($"Winline evaluation/logging failed: {ex.Message}"); } }
		}
		catch (Exception ex) { Debug.LogException(ex); }
		stateMachine.SetState(State.Presentation);
	}

	private void OnPresentationEnter(object obj)
	{
		foreach (GameReel gr in reels) gr.DimDummySymbols(); eventManager.BroadcastEvent(SlotsEvent.BeginSlotPresentation, this);
	}
	private void OnPresentationComplete(object obj) => stateMachine.SetState(State.Idle);

	// Global group-level presentation finished: transition to Idle if this engine is currently presenting
	private void OnPresentationCompleteGroup(object obj)
	{
		try
		{
			if (stateMachine != null && stateMachine.CurrentState == State.Presentation)
			{
				stateMachine.SetState(State.Idle);
			}
		}
		catch { }
	}

	/// <summary>
	/// Returns a row-major flattened grid of the current visible `GameSymbol`s across all reels.
	/// </summary>
	public GameSymbol[] GetCurrentSymbolGrid()
	{
		List<GameSymbol[]> reelSymbols = new List<GameSymbol[]>(); foreach (GameReel gameReel in reels) { var newList = new GameSymbol[gameReel.Symbols.Count]; for (int i = 0; i < gameReel.Symbols.Count; i++) newList[i] = gameReel.Symbols[i]; reelSymbols.Add(newList); } return Helpers.CombineColumnsToGrid(reelSymbols);
	}
	/// <summary>
	/// Broadcasts a slot-scoped event to listeners registered on this engine's `EventManager` instance.
	/// </summary>
	public void BroadcastSlotsEvent(SlotsEvent eventName, object value = null) => eventManager.BroadcastEvent(eventName, value);
	/// <summary>
	/// Let the engine highlight winning symbols. SlotsEngine controls symbol animations directly
	/// so we avoid broadcasting per-symbol events.
	/// </summary>
	public void HighlightWinningSymbols(GameSymbol[] grid, List<WinData> winData)
	{
		if (grid == null || winData == null) return;
		int gridLen = grid.Length;
		foreach (var w in winData)
		{
			if (w?.WinningSymbolIndexes == null) continue;
			foreach (int idx in w.WinningSymbolIndexes)
			{
				if (idx < 0 || idx >= gridLen) continue;
				var gs = grid[idx];
				if (gs == null) continue;
				Color hi = Color.green;
				if (gs.CurrentSymbolData != null)
				{
					switch (gs.CurrentSymbolData.WinMode)
					{
						case EvaluatorCore.SymbolWinMode.LineMatch: hi = Color.green; break;
						case EvaluatorCore.SymbolWinMode.SingleOnReel: hi = Color.yellow; break;
						case EvaluatorCore.SymbolWinMode.TotalCount: hi = Color.red; break;
					}
				}
				bool doShake = true;
				try { doShake = gs.OwnerReel != null ? gs.OwnerReel.OwnerEngine.IsPageActive : true; } catch { }
				try { gs.HighlightForWin(hi, doShake); } catch { }
			}
		}
	}

	/// <summary>
	/// Saves the current `SlotsData` through the persistence layer.
	/// </summary>
	public void SaveSlotsData() { SlotsDataManager.Instance.AddNewData(currentSlotsData); DataPersistenceManager.Instance.SaveGame(); }
	/// <summary>
	/// Allows external listeners to receive notifications when reels are added/removed. If called before initialization,
	/// the handler is cached and registered once the engine is ready.
	/// </summary>
	public void RegisterReelChanged(Action<object> handler)
	{
		if (handler == null) return; if (eventManager == null) { if (!pendingReelChangedHandlers.Contains(handler)) pendingReelChangedHandlers.Add(handler); return; } eventManager.RegisterEvent(SlotsEvent.ReelAdded, handler); eventManager.RegisterEvent(SlotsEvent.ReelRemoved, handler);
	}
	/// <summary>
	/// Unregisters a handler previously registered via `RegisterReelChanged`.
	/// </summary>
	public void UnregisterReelChanged(Action<object> handler)
	{
		if (handler == null) return; if (eventManager == null) { if (pendingReelChangedHandlers.Contains(handler)) pendingReelChangedHandlers.Remove(handler); return; } eventManager.UnregisterEvent(SlotsEvent.ReelAdded, handler); eventManager.UnregisterEvent(SlotsEvent.ReelRemoved, handler);
	}

	private void OnDestroy()
	{
		// ensure any stagger coroutine is stopped
		if (staggerSpinCoroutine != null) StopCoroutine(staggerSpinCoroutine);

		// Unregister global handlers
		try { GlobalEventManager.Instance?.UnregisterEvent(SlotsEvent.PresentationCompleteGroup, OnPresentationCompleteGroup); } catch { }
	}

	/// <summary>
	/// Adds a new reel to this engine at runtime. Creates a matching `ReelData`, spawns its GameReel and updates layout.
	/// </summary>
	public void AddReel()
	{
		if (currentSlotsData == null) return;

		// Create new ReelData using last reel as template if possible
		ReelData template = (currentSlotsData.CurrentReelData != null && currentSlotsData.CurrentReelData.Count > 0) ? currentSlotsData.CurrentReelData.Last() : null;
		ReelData newData;
		if (template != null)
		{
			// If the template has a runtime strip, create a fresh runtime strip instance (do NOT share the same instance)
			if (template.CurrentReelStrip != null)
			{
				try
				{
					var src = template.CurrentReelStrip;
					var newStrip = new ReelStripData(src.Definition, src.StripSize, src.SymbolDefinitions, null, null);
					newData = new ReelData(template.ReelSpinDuration, template.SymbolCount, null, template.BaseDefinition, newStrip);
				}
				catch (Exception)
				{
					// fallback to conservative clone without explicit strip
					newData = new ReelData(template.ReelSpinDuration, template.SymbolCount, null, template.BaseDefinition, null);
				}
			}
			else
			{
				// No template strip present; create a new ReelData using base definition
				newData = new ReelData(template.ReelSpinDuration, template.SymbolCount, null, template.BaseDefinition, null);
			}
		}
		else if (currentSlotsData.BaseDefinition != null && currentSlotsData.BaseDefinition.ReelDefinitions != null && currentSlotsData.BaseDefinition.ReelDefinitions.Length > 0)
		{
			newData = currentSlotsData.BaseDefinition.ReelDefinitions[0].CreateInstance();
		}
		else
		{
			// Fallback conservative defaults
			newData = new ReelData(0.5f, 3, null, null, null);
		}

		// Ensure any runtime ReelStripData that was created/assigned as part of the ReelData is registered
		try
		{
			var stripToRegister = newData.CurrentReelStrip ?? newData.DefaultReelStrip;
			if (stripToRegister != null && ReelStripDataManager.Instance != null && stripToRegister.AccessorId == 0)
			{
				ReelStripDataManager.Instance.AddNewData(stripToRegister);
			}
		}
		catch (Exception ex)
		{
			Debug.LogWarning($"SlotsEngine.AddReel: failed to register new reel strip with manager: {ex.Message}");
		}

		// NEW: register the ReelData itself so AddReel UI can list it when unassociated
		try
		{
			if (ReelDataManager.Instance != null && newData.AccessorId == 0)
			{
				ReelDataManager.Instance.AddNewData(newData);
			}
		}
		catch (Exception ex)
		{
			Debug.LogWarning($"SlotsEngine.AddReel: failed to register ReelData with manager: {ex.Message}");
		}

		// Add to data model
		currentSlotsData.AddNewReel(newData);

		// Spawn visual reel under existing group
		Transform parent = currentReelsGroup != null ? currentReelsGroup : reelsRootTransform;
		if (parent == null) parent = reelsRootTransform;
		GameObject g = Instantiate(reelPrefab, parent);
		var reel = g.GetComponent<GameReel>();
		int newIndex = currentSlotsData.CurrentReelData.Count - 1;
		try
		{
			reel.InitializeReel(newData, newIndex, eventManager, newData.CurrentReelStrip ?? newData.DefaultReelStrip, this);
		}
		catch
		{
			// best-effort: try other overload
			try { reel.InitializeReel(newData, newIndex, eventManager, newData.CurrentReelStrip, this); } catch { }
		}

		reels.Add(reel);

		// Prewarm and reposition
		reel.PrewarmPooledSymbols();
		RepositionReels();
		RegenerateAllReelDummies();

		// Inform listeners
		try { eventManager?.BroadcastEvent(SlotsEvent.ReelAdded, reel); } catch { }
	}

	public void RemoveReel(GameReel reel)
	{
		if (reel == null) return;
		int idx = reels.IndexOf(reel);
		if (idx < 0) return;
		if (stateMachine != null && stateMachine.CurrentState == State.Spinning) throw new InvalidOperationException("Cannot remove reel while spinning.");

		// Preserve any persisted symbol <-> reel-strip associations.
		// Removing a visual/data-only Reel should NOT remove persisted ReelStripData or SymbolData entries
		// because players may own symbols tied to those strips. Any manager-level cleanup that would
		// disassociate symbols must be performed explicitly elsewhere (e.g., when an entire slot is
		// intentionally deleted). Therefore do not call ReelDataManager.RemoveDataIfExists or
		// ReelStripDataManager.RemoveDataIfExists here.

		// Remove data model entry if present
		try
		{
			if (currentSlotsData != null && currentSlotsData.CurrentReelData != null && idx < currentSlotsData.CurrentReelData.Count)
			{
				var rd = currentSlotsData.CurrentReelData[idx];
				currentSlotsData.RemoveReel(rd);
				// NOTE: Intentionally do not remove rd from ReelDataManager or its symbols/strips here.
			}
		}
		catch (Exception ex) { Debug.LogWarning($"SlotsEngine.RemoveReel: data removal failed: {ex.Message}"); }

		// Remove visual
		reels.RemoveAt(idx);
		try { Destroy(reel.gameObject); } catch { }
		RepositionReels();
		RegenerateAllReelDummies();
		try { eventManager?.BroadcastEvent(SlotsEvent.ReelRemoved, reel); } catch { }
	}

	public bool TryApplySlotsDataUpdate(SlotsData newData)
	{
		if (newData == null) return false;
		if (this.CurrentState == State.Spinning)
		{
			throw new InvalidOperationException("Cannot apply SlotsData update while engine is spinning.");
		}

		var oldList = currentSlotsData?.CurrentReelData ?? new List<ReelData>();
		var newList = newData?.CurrentReelData ?? new List<ReelData>();
		int oldCount = oldList.Count;
		int newCount = newList.Count;
		int common = Math.Min(oldCount, newCount);
		int visualCount = reels?.Count ?? 0;

		Debug.Log($"[SlotsEngine] TryApplySlotsDataUpdate start oldCount={oldCount} newCount={newCount} reelsListCount={visualCount}");

		// Update existing visuals where the data reference has changed for indexes covered by both lists and visuals
		int updateLimit = Math.Min(common, visualCount);
		for (int i = 0; i < updateLimit; i++)
		{
			if (!ReferenceEquals(oldList[i], newList[i]))
			{
				try
				{
					var strip = newList[i]?.CurrentReelStrip ?? newList[i]?.DefaultReelStrip;
					if (i < reels.Count && reels[i] != null)
					{
						reels[i].InitializeReel(newList[i], i, eventManager, strip, this);
						Debug.Log($"[SlotsEngine] Updated existing reel index={i} stripAccessor={strip?.AccessorId} rdAccessor={newList[i]?.AccessorId}");
					}
					if (currentSlotsData != null && currentSlotsData.CurrentReelData != null && i < currentSlotsData.CurrentReelData.Count)
					{
						currentSlotsData.CurrentReelData[i] = newList[i];
					}
				}
				catch (Exception ex)
				{
					Debug.LogWarning($"[SlotsEngine] Failed to update reel at index {i}: {ex.Message}");
				}
			}
		}

		// If visuals have fewer reels than new data, append missing visuals regardless of oldCount
		if (visualCount < newCount)
		{
			for (int i = visualCount; i < newCount; i++)
			{
				try
				{
					var rd = newList[i];
					if (rd == null)
					{
						Debug.LogWarning($"[SlotsEngine] Skipping add at index={i}: newList entry is null");
						continue;
					}
					var strip = rd.CurrentReelStrip ?? rd.DefaultReelStrip;
					Transform parent = currentReelsGroup != null ? currentReelsGroup : reelsRootTransform;
					if (parent == null) parent = reelsRootTransform;
					var g = Instantiate(reelPrefab, parent);
					var reel = g.GetComponent<GameReel>();
					reel.InitializeReel(rd, i, eventManager, strip, this);
					reels.Add(reel);
					Debug.Log($"[SlotsEngine] Added missing visual reel index={i} rdAccessor={rd.AccessorId} stripAccessor={strip?.AccessorId}. reelsCountNow={reels.Count}");
				}
				catch (Exception ex)
				{
					Debug.LogWarning($"[SlotsEngine] Failed to add missing visual reel at index {i}: {ex.Message}");
				}
			}
		}

		// If visuals have more reels than new data, remove excess visuals
		visualCount = reels?.Count ?? 0;
		if (visualCount > newCount)
		{
			for (int i = visualCount - 1; i >= newCount; i--)
			{
				try
				{
					if (i >= 0 && i < reels.Count)
					{
						var reelToRemove = reels[i];
						RemoveReel(reelToRemove);
						Debug.Log($"[SlotsEngine] Removed excess visual reel index={i}. reelsCountNow={reels.Count}");
					}
				}
				catch (Exception ex)
				{
					Debug.LogWarning($"[SlotsEngine] Failed to remove visual reel at index {i}: {ex.Message}");
				}
			}
		}

		// Replace engine's SlotsData reference with newData so further operations reflect persisted model
		currentSlotsData = newData;

		try { RepositionReels(); RegenerateAllReelDummies(); } catch { }

		Debug.Log($"[SlotsEngine] TryApplySlotsDataUpdate end reelsCount={reels?.Count ?? 0} dataReels={currentSlotsData?.CurrentReelData?.Count ?? 0}");
		return true;
	}
}