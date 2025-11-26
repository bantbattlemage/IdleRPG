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
	public void SetState(State state) => stateMachine.SetState(state);

	// NEW: page activation flag for suspension
	private bool pageActive = true;
	public void SetPageActive(bool active)
	{
		pageActive = active;
		foreach (var r in reels)
			try { r?.SetPageActive(active); } catch { }
	}
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
			try
			{
				// Schedule stop so it will not fire before the reel's own scheduled start.
				float delay = enginePageActive ? (ensureAllStartedDelay + baseStagger * i) : 0f;
				reels[i].StopReel(delay);
			}
			catch { }
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
			g.transform.localPosition = new Vector3((data.SymbolSpacing + data.SymbolSize) * i, 0, 0); reels.Add(reel);
		}
		// Prewarm per-reel pools immediately after creating reels to avoid gaps on first spins
		foreach (var r in reels) r?.PrewarmPooledSymbols();
		ReelData reelDef = currentSlotsData.CurrentReelData[0]; int count = reels.Count; float totalWidth = (count * reelDef.SymbolSize) + ((count - 1) * reelDef.SymbolSpacing); float offset = totalWidth / 2f; float xPos = (-offset + (reelDef.SymbolSize / 2f)); count = currentSlotsData.CurrentReelData.Max(x => x.SymbolCount); totalWidth = (count * reelDef.SymbolSize) + ((count - 1) * reelDef.SymbolSpacing); offset = totalWidth / 2f; float yPos = (-offset + (reelDef.SymbolSize / 2f)); reelsGroup.transform.localPosition = new Vector3(xPos, yPos, 0); currentReelsGroup = reelsGroup;
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

	private void RegenerateAllReelDummies()
	{
		foreach (var r in reels) if (r != null) r.RegenerateDummies();
	}

	/// <summary>
	/// Regenerates dummies for all reels when a height change in one reel affects the max height context.
	/// This is called from GameReel.SetSymbolCount when it detects a height change that impacts other reels.
	/// </summary>
	public void RegenerateAllReelDummiesForHeightChange()
	{
		foreach (var r in reels) if (r != null) r.RegenerateDummies();
	}

	/// <summary>
	/// Adds a new reel to the right side using the provided `ReelData` and repositions existing reels.
	/// Broadcasts a ReelAdded event.
	/// </summary>
	public void AddReel(ReelData newReelData)
	{
		if (stateMachine != null && stateMachine.CurrentState == State.Spinning) { Debug.LogWarning("Cannot add reel while spinning."); return; }
		if (currentSlotsData == null) throw new InvalidOperationException("CurrentSlotsData is null");
		if (currentSlotsData.CurrentReelData.Count > 0) { var template = currentSlotsData.CurrentReelData[0]; newReelData.SetSymbolSize(template.SymbolSize, template.SymbolSpacing); }
		currentSlotsData.AddNewReel(newReelData); ReelDataManager.Instance.AddNewData(newReelData); SlotsDataManager.Instance.UpdateSlotsData(currentSlotsData);
		if (currentReelsGroup == null) { SpawnReels(reelsRootTransform); return; }
		GameObject g = Instantiate(reelPrefab, currentReelsGroup); GameReel reel = g.GetComponent<GameReel>(); int newIndex = reels.Count;
		if (newReelData.CurrentReelStrip != null) reel.InitializeReel(newReelData, newIndex, eventManager, newReelData.CurrentReelStrip, this); else reel.InitializeReel(newReelData, newIndex, eventManager, newReelData.DefaultReelStrip, this);
		reels.Add(reel); RepositionReels();
		// When adding a reel, all existing reels need to check if their dummy counts should change due to new max height
		RegenerateAllReelDummies();
		eventManager.BroadcastEvent(SlotsEvent.ReelAdded, reel);
	}
	public void AddReel(ReelDefinition definition) { if (definition == null) throw new ArgumentNullException(nameof(definition)); AddReel(definition.CreateInstance()); }
	/// <summary>
	/// Adds a new reel using a template from current data/definition when available.
	/// </summary>
	public void AddReel()
	{
		if (stateMachine != null && stateMachine.CurrentState == State.Spinning) { Debug.LogWarning("Cannot add reel while spinning."); return; }
		if (currentSlotsData == null || currentSlotsData.CurrentReelData.Count == 0) { Debug.LogWarning("No template reel available to create a new reel."); return; }
		var template = currentSlotsData.CurrentReelData[0]; var def = template.BaseDefinition;
		if (def == null)
		{
			try { var slotsDef = currentSlotsData?.BaseDefinition; if (slotsDef?.ReelDefinitions?.Length > 0) { int idx = currentSlotsData.CurrentReelData.IndexOf(template); if (idx < 0 || idx >= slotsDef.ReelDefinitions.Length) idx = 0; def = slotsDef.ReelDefinitions[idx]; } } catch { }
		}
		if (def != null) AddReel(def); else Debug.LogWarning("Template reel does not have a BaseDefinition and no fallback was found. Cannot create new reel.");
	}

	/// <summary>
	/// Inserts a reel at the specified index and refreshes reel indices/positions. Broadcasts a ReelAdded event.
	/// </summary>
	public void InsertReelAt(int index, ReelData newReelData)
	{
		if (stateMachine != null && stateMachine.CurrentState == State.Spinning) { Debug.LogWarning("Cannot insert reel while spinning."); return; }
		if (currentSlotsData == null) throw new InvalidOperationException("CurrentSlotsData is null");
		if (index < 0 || index > currentSlotsData.CurrentReelData.Count) throw new ArgumentOutOfRangeException(nameof(index));
		if (currentSlotsData.CurrentReelData.Count > 0) { var template = currentSlotsData.CurrentReelData[0]; newReelData.SetSymbolSize(template.SymbolSize, template.SymbolSpacing); }
		currentSlotsData.InsertReelAt(index, newReelData); ReelDataManager.Instance.AddNewData(newReelData); SlotsDataManager.Instance.UpdateSlotsData(currentSlotsData);
		GameObject g = Instantiate(reelPrefab, currentReelsGroup); GameReel reel = g.GetComponent<GameReel>();
		if (newReelData.CurrentReelStrip != null) reel.InitializeReel(newReelData, index, eventManager, newReelData.CurrentReelStrip, this); else reel.InitializeReel(newReelData, index, eventManager, newReelData.DefaultReelStrip, this);
		reels.Insert(index, reel); RefreshReelsAfterModification();
		// When inserting a reel, all reels need to check if their dummy counts should change due to new max height
		RegenerateAllReelDummies();
		eventManager.BroadcastEvent(SlotsEvent.ReelAdded, reel);
	}

	/// <summary>
	/// Removes a reel at the specified index, destroys visuals, and repositions remaining reels. Broadcasts a ReelRemoved event.
	/// </summary>
	public void RemoveReelAt(int index)
	{
		if (stateMachine != null && stateMachine.CurrentState == State.Spinning) { Debug.LogWarning("Cannot remove reel while spinning."); return; }
		if (index < 0 || index >= reels.Count) throw new ArgumentOutOfRangeException(nameof(index));
		var dataToRemove = currentSlotsData.CurrentReelData[index]; currentSlotsData.RemoveReel(dataToRemove); ReelDataManager.Instance.RemoveDataIfExists(dataToRemove); SlotsDataManager.Instance.UpdateSlotsData(currentSlotsData);
		GameReel reel = reels[index]; if (reel != null && reel.gameObject != null) Destroy(reel.gameObject); reels.RemoveAt(index); RefreshReelsAfterModification();
		// When removing a reel, all remaining reels need to check if their dummy counts should change due to new max height
		RegenerateAllReelDummies();
		eventManager.BroadcastEvent(SlotsEvent.ReelRemoved, reel);
	}
	public void RemoveReel(GameReel reelToRemove) { int idx = reels.IndexOf(reelToRemove); if (idx == -1) throw new ArgumentException("Reel not part of this SlotsEngine", nameof(reelToRemove)); RemoveReelAt(idx); }

	private void RefreshReelsAfterModification()
	{
		for (int i = 0; i < reels.Count; i++)
		{
			var r = reels[i]; if (r == null) continue; ReelData data = r.CurrentReelData; ReelStripData strip = r.ReelStrip;
			if (strip != null) r.InitializeReel(data, i, eventManager, strip, this); else r.InitializeReel(data, i, eventManager, data.DefaultReelStrip, this);
		}
		RepositionReels();
	}

	/// <summary>
	/// Recomputes X positions for each reel using SymbolSize and SymbolSpacing and re-centers the reels group.
	/// </summary>
	private void RepositionReels()
	{
		if (reels.Count == 0 || currentSlotsData.CurrentReelData.Count == 0) return; ReelData reelDef = currentSlotsData.CurrentReelData[0];
		for (int i = 0; i < reels.Count; i++) { var data = currentSlotsData.CurrentReelData[i]; var g = reels[i].gameObject; if (g != null) g.transform.localPosition = new Vector3((data.SymbolSpacing + data.SymbolSize) * i, 0, 0); }
		int count = reels.Count; float totalWidth = (count * reelDef.SymbolSize) + ((count - 1) * reelDef.SymbolSpacing); float offset = totalWidth / 2f; float xPos = (-offset + (reelDef.SymbolSize / 2f)); count = currentSlotsData.CurrentReelData.Max(x => x.SymbolCount); totalWidth = (count * reelDef.SymbolSize) + ((count - 1) * reelDef.SymbolSpacing); offset = totalWidth / 2f; float yPos = (-offset + (reelDef.SymbolSize / 2f)); if (currentReelsGroup != null) currentReelsGroup.transform.localPosition = new Vector3(xPos, yPos, 0);
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
			int maxSymbolsPre = currentSlotsData.CurrentReelData.Max(x => x.SymbolCount); float availableStepPre = (totalHeight / maxSymbolsPre) * 0.8f; float spacingPre = availableStepPre * 0.2f; foreach (ReelData r in currentSlotsData.CurrentReelData) r.SetSymbolSize(availableStepPre - spacingPre, spacingPre); SpawnReels(reelsRootTransform); return;
		}
		int maxSymbols = currentSlotsData.CurrentReelData.Max(x => x.SymbolCount); int reelCount = currentSlotsData.CurrentReelData.Count; const float heightFill = 0.8f; const float widthFill = 0.95f; const float spacingFactor = 0.1f;
		float symbolMaxByHeight = float.MaxValue; if (totalHeight > 0f && maxSymbols > 0) { float stepPerSymbol = 1f + spacingFactor; symbolMaxByHeight = (totalHeight * heightFill) / (maxSymbols * stepPerSymbol); }
		float symbolMaxByWidth = float.MaxValue; if (totalWidth > 0f && reelCount > 0) { float denom = reelCount + (reelCount - 1) * spacingFactor; if (denom > 0f) symbolMaxByWidth = (totalWidth * widthFill) / denom; }
		float chosenSymbolSize = Math.Min(symbolMaxByHeight, symbolMaxByWidth); if (float.IsInfinity(chosenSymbolSize) || float.IsNaN(chosenSymbolSize) || chosenSymbolSize <= 0f) chosenSymbolSize = currentSlotsData.CurrentReelData[0].SymbolSize; float chosenSpacing = chosenSymbolSize * spacingFactor;
		ReelData firstDef = currentSlotsData.CurrentReelData[0]; bool sizeMatches = Mathf.Approximately(firstDef.SymbolSize, chosenSymbolSize) && Mathf.Approximately(firstDef.SymbolSpacing, chosenSpacing); bool reelsCountMatches = reels.Count == currentSlotsData.CurrentReelData.Count; if (sizeMatches && reelsCountMatches) return;
		foreach (ReelData r in currentSlotsData.CurrentReelData) r.SetSymbolSize(chosenSymbolSize, chosenSpacing);
		for (int i = 0; i < reels.Count; i++) { var reel = reels[i]; if (reel == null) continue; reel.UpdateSymbolLayout(chosenSymbolSize, chosenSpacing); float x = (chosenSpacing + chosenSymbolSize) * i; reel.transform.localPosition = new Vector3(x, 0f, 0f); }
		ReelData reelDef = currentSlotsData.CurrentReelData[0]; int count = reels.Count; float totalWidthComputed = (count * reelDef.SymbolSize) + ((count - 1) * reelDef.SymbolSpacing); float offset = totalWidthComputed / 2f; float xPos = (-offset + (reelDef.SymbolSize / 2f)); count = currentSlotsData.CurrentReelData.Max(x => x.SymbolCount); totalWidthComputed = (count * reelDef.SymbolSize) + ((count - 1) * reelDef.SymbolSpacing); offset = totalWidthComputed / 2f; float yPos = (-offset + (reelDef.SymbolSize / 2f)); if (currentReelsGroup != null) currentReelsGroup.transform.localPosition = new Vector3(xPos, yPos, 0);
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
		for (int i = 0; i < reels.Count; i++) { var reel = reels[i]; if (reel == null) continue; /* use lightweight resize to avoid regenerating dummies */ reel.ResizeVisuals(chosenSymbolSize, chosenSpacing); float x = (chosenSpacing + chosenSymbolSize) * i; reel.transform.localPosition = new Vector3(x, 0f, 0f); }
		ReelData reelDef = currentSlotsData.CurrentReelData[0]; int count = reels.Count; float totalWidthComputed = (count * reelDef.SymbolSize) + ((count - 1) * reelDef.SymbolSpacing); float offset = totalWidthComputed / 2f; float xPos = (-offset + (reelDef.SymbolSize / 2f)); count = currentSlotsData.CurrentReelData.Max(x => x.SymbolCount); totalWidthComputed = (count * reelDef.SymbolSize) + ((count - 1) * reelDef.SymbolSpacing); offset = totalWidthComputed / 2f; float yPos = (-offset + (reelDef.SymbolSize / 2f)); if (currentReelsGroup != null) currentReelsGroup.transform.localPosition = new Vector3(xPos, yPos, 0);
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
}