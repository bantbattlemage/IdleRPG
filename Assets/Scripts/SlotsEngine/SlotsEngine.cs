using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// Top-level controller for a single slots instance. Manages reel GameObjects, state machine and
/// event wiring for spins and presentations. Responsible for creating/destroying reels and updating
/// layout when reels are added/removed.
/// </summary>
public class SlotsEngine : MonoBehaviour
{
	[SerializeField] private GameObject reelPrefab;

	private SlotsData currentSlotsData;
	public SlotsData CurrentSlotsData => currentSlotsData;

	private Transform reelsRootTransform;
	public Transform ReelsRootTransform => reelsRootTransform;

	private List<GameReel> reels = new List<GameReel>();
	public List<GameReel> CurrentReels => reels;

	private EventManager eventManager;
	private SlotsStateMachine stateMachine;

	// Tracks whether any reel has started spinning; used to coordinate Stop/Spin logic
	private bool spinInProgress = false;

	private Transform currentReelsGroup;

	public State CurrentState
	{
		get => stateMachine.CurrentState;
	}

	public void SetState(State state)
	{
		stateMachine.SetState(state);
	}

	/// <summary>
	/// Create slots engine runtime data from a definition asset and initialize reels.
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
	/// Initialize the engine with existing runtime data. Sets up the EventManager and state machine,
	/// registers internal event handlers and spawns visual reel GameObjects under the provided canvas.
	/// </summary>
	public void InitializeSlotsEngine(Transform canvasTransform, SlotsData data)
	{
		currentSlotsData = data;

		eventManager = new EventManager();
		// register any pending reel-changed handlers that were added early
		if (pendingReelChangedHandlers != null && pendingReelChangedHandlers.Count > 0)
		{
			foreach (var h in pendingReelChangedHandlers)
			{
				eventManager.RegisterEvent(SlotsEvent.ReelAdded, h);
				eventManager.RegisterEvent(SlotsEvent.ReelRemoved, h);
			}
			pendingReelChangedHandlers.Clear();
		}
		stateMachine = new SlotsStateMachine();
		stateMachine.InitializeStateMachine(this, eventManager);

		// subscribe to engine-local events using enum keys where available
		eventManager.RegisterEvent(SlotsEvent.SpinCompleted, OnSpinCompleted);
		eventManager.RegisterEvent(SlotsEvent.ReelSpinStarted, OnReelSpinStarted);
		eventManager.RegisterEvent(SlotsEvent.ReelCompleted, OnReelCompleted);

		// respond to the state machine entering the Presentation state
		eventManager.RegisterEvent(State.Presentation, "Enter", OnPresentationEnter);

		// presentation complete comes from presentation controller
		eventManager.RegisterEvent(SlotsEvent.PresentationComplete, OnPresentationComplete);

		reelsRootTransform = canvasTransform;

		SpawnReels(reelsRootTransform);
	}

	public void BeginSlots()
	{
		stateMachine.BeginStateMachine();
	}

	/// <summary>
	/// UI-level toggle used to either start spins or stop currently spinning reels.
	/// Decides action using local spinInProgress flag and state machine state.
	/// </summary>
	public void SpinOrStopReels(bool spin)
	{
		if (!spinInProgress && spin)
		{
			SpinAllReels();
		}
		else if (spinInProgress && !spin && stateMachine.CurrentState == State.Spinning)
		{
			StopAllReels();
		}
		else
		{
			Debug.Log("SpinOrStopReels called but no action could be taken.");

			if (GamePlayer.Instance.CurrentCredits < GamePlayer.Instance.CurrentBet.CreditCost)
			{
				SlotConsoleController.Instance.SetConsoleMessage("Not enough credits! Switch to a lower bet or add credits.");
			}
		}
	}

	/// <summary>
	/// Start spins on every reel. Generates a random solution for each reel if one isn't provided
	/// and instructs reels to BeginSpin with a small staggered fallout delay.
	/// </summary>
	private void SpinAllReels()
	{
		if (spinInProgress)
		{
			throw new InvalidOperationException("Spin already in progress!");
		}

		// Notify WinlineEvaluator that a new spin has started so it can clear the console
		// once per spin when logging is enabled. This keeps editor logs focused per-spin.
		try
		{
			if (WinlineEvaluator.Instance != null && WinlineEvaluator.Instance.LoggingEnabled)
			{
				WinlineEvaluator.Instance.NotifySpinStarted();
			}
		}
		catch (Exception)
		{
			// Best-effort; don't let logging failures prevent spins
		}

		float falloutDelay = 0.025f;

		for (int i = 0; i < reels.Count; i++)
		{
			List<SymbolData> testSolution = new List<SymbolData>();
			// track selections for this reel so MaxPerReel is enforced while constructing the solution
			var selectionsForReel = new List<SymbolData>();
			for (int k = 0; k < reels[i].CurrentReelData.SymbolCount; k++)
			{
				// Use the overload that accepts existing selections so MaxPerReel is honored
				var symbol = reels[i].GetRandomSymbolFromStrip(selectionsForReel);
				testSolution.Add(symbol);
				if (symbol != null) selectionsForReel.Add(symbol);
			}

			reels[i].CurrentReelData.SetCurrentSymbolData(testSolution);

			reels[i].BeginSpin(testSolution, falloutDelay);
			falloutDelay += 0.025f;
		}
	}

	private void OnReelSpinStarted(object obj)
	{
		// When any reel reports it has started spinning, enter the Spinning state if not already.
		if (!spinInProgress)
		{
			spinInProgress = true;
			stateMachine.SetState(State.Spinning);
			// Fallback: ensure any listeners receive the state enter event even if SetState's broadcast was missed
			try
			{
				eventManager.BroadcastEvent(State.Spinning, "Enter");
			}
			catch (Exception ex)
			{
				Debug.LogException(ex);
				throw;
			}

			return;
		}

		// If already in spinInProgress, no further action required.
	}

	private void StopAllReels()
	{
		// Only allow stopping once every reel is actively spinning
		if (!reels.TrueForAll(x => x.Spinning))
		{
			return;
		}

		float stagger = 0.025f;
		for (int i = 0; i < reels.Count; i++)
		{
			reels[i].StopReel(stagger);
			stagger += 0.025f;
		}

		eventManager.BroadcastEvent(SlotsEvent.StoppingReels);
	}

	/// <summary>
	/// Create visual reel GameObjects and initialize them using the runtime data model.
	/// If a group exists it will be destroyed and recreated to reflect the new data.
	/// </summary>
	private void SpawnReels(Transform gameCanvas)
	{
		if (currentReelsGroup != null)
		{
			Destroy(currentReelsGroup.gameObject);
			reels.Clear();
		}

		Transform reelsGroup = new GameObject("ReelsGroup").transform;
		reelsGroup.parent = gameCanvas.transform;
		reelsGroup.localScale = new Vector3(1, 1, 1);

		for (int i = 0; i < currentSlotsData.CurrentReelData.Count; i++)
		{
			ReelData data = currentSlotsData.CurrentReelData[i];
			GameObject g = Instantiate(reelPrefab, reelsGroup.transform);
			GameReel reel = g.GetComponent<GameReel>();

			if (data.CurrentReelStrip != null)
			{
				reel.InitializeReel(data, i, eventManager, data.CurrentReelStrip);
			}
			else
			{
				reel.InitializeReel(data, i, eventManager, data.DefaultReelStrip);
			}

			g.transform.localPosition = new Vector3((data.SymbolSpacing + data.SymbolSize) * i, 0, 0);
			reels.Add(reel);
		}

		// Compute center offsets so the group is positioned as expected in the parent canvas
		ReelData reelDefinition = currentSlotsData.CurrentReelData[0];

		int count = reels.Count;
		float totalWidth = (count * (reelDefinition.SymbolSize)) + ((count - 1) * reelDefinition.SymbolSpacing);
		float offset = totalWidth / 2f;
		float xPos = (-offset + (reelDefinition.SymbolSize / 2f));

		count = currentSlotsData.CurrentReelData.Max(x => x.SymbolCount);
		totalWidth = (count * (reelDefinition.SymbolSize)) + ((count - 1) * reelDefinition.SymbolSpacing);
		offset = totalWidth / 2f;
		float yPos = (-offset + (reelDefinition.SymbolSize / 2f));

		reelsGroup.transform.localPosition = new Vector3(xPos, yPos, 0);
		currentReelsGroup = reelsGroup;
	}

	/// <summary>
	/// Add a new reel at runtime. Reel must not be added while reels are spinning.
	/// Broadcasts an event and persists the data.
	/// </summary>
	public void AddReel(ReelData newReelData)
	{
		if (stateMachine != null && stateMachine.CurrentState == State.Spinning)
		{
			Debug.LogWarning("Cannot add reel while spinning.");
			return;
		}

		if (currentSlotsData == null)
		{
			throw new InvalidOperationException("CurrentSlotsData is null");
		}

		// Make sure the new reel matches current layout sizes if possible
		if (currentSlotsData.CurrentReelData != null && currentSlotsData.CurrentReelData.Count > 0)
		{
			var template = currentSlotsData.CurrentReelData[0];
			newReelData.SetSymbolSize(template.SymbolSize, template.SymbolSpacing);
		}

		// Add to data model and ensure it's persisted
		currentSlotsData.AddNewReel(newReelData);
		ReelDataManager.Instance.AddNewData(newReelData);
		SlotsDataManager.Instance.UpdateSlotsData(currentSlotsData);

		// Ensure reels group exists
		if (currentReelsGroup == null)
		{
			SpawnReels(reelsRootTransform);
			return;
		}

		// Instantiate new reel GameObject
		GameObject g = Instantiate(reelPrefab, currentReelsGroup);
		GameReel reel = g.GetComponent<GameReel>();
		int newIndex = reels.Count;

		if (newReelData.CurrentReelStrip != null)
		{
			reel.InitializeReel(newReelData, newIndex, eventManager, newReelData.CurrentReelStrip);
		}
		else
		{
			reel.InitializeReel(newReelData, newIndex, eventManager, newReelData.DefaultReelStrip);
		}

		reels.Add(reel);

		// Reposition all reels
		RepositionReels();

		// Broadcast event
		eventManager.BroadcastEvent(SlotsEvent.ReelAdded, reel);
	}

	/// <summary>
	/// Convenience overload to create a new ReelData from a definition and add it.
	/// </summary>
	public void AddReel(ReelDefinition definition)
	{
		if (definition == null) throw new ArgumentNullException(nameof(definition));
		AddReel(definition.CreateInstance());
	}

	/// <summary>
	/// Add a new reel at runtime using the template reel's BaseDefinition. Convenience when callers don't have a definition handy.
	/// </summary>
	public void AddReel()
	{
		if (stateMachine != null && stateMachine.CurrentState == State.Spinning)
		{
			Debug.LogWarning("Cannot add reel while spinning.");
			return;
		}

		if (currentSlotsData == null || currentSlotsData.CurrentReelData == null || currentSlotsData.CurrentReelData.Count == 0)
		{
			Debug.LogWarning("No template reel available to create a new reel.");
			return;
		}

		var template = currentSlotsData.CurrentReelData[0];
		var def = template.BaseDefinition;
		if (def != null)
		{
			AddReel(def);
		}
		else
		{
			Debug.LogWarning("Template reel does not have a BaseDefinition. Cannot create new reel.");
		}
	}

	/// <summary>
	/// Insert a reel into a specific index. Persists data and broadcasts event.
	/// After insertion, subsequent reels are refreshed so their IDs/positions remain consistent.
	/// </summary>
	public void InsertReelAt(int index, ReelData newReelData)
	{
		if (stateMachine != null && stateMachine.CurrentState == State.Spinning)
		{
			Debug.LogWarning("Cannot insert reel while spinning.");
			return;
		}

		if (currentSlotsData == null)
		{
			throw new InvalidOperationException("CurrentSlotsData is null");
		}

		if (index < 0 || index > currentSlotsData.CurrentReelData.Count)
		{
			throw new ArgumentOutOfRangeException(nameof(index));
		}

		// Make sure the new reel matches current layout sizes if possible
		if (currentSlotsData.CurrentReelData != null && currentSlotsData.CurrentReelData.Count > 0)
		{
			var template = currentSlotsData.CurrentReelData[0];
			newReelData.SetSymbolSize(template.SymbolSize, template.SymbolSpacing);
		}

		// Insert into data model and persist
		currentSlotsData.InsertReelAt(index, newReelData);
		ReelDataManager.Instance.AddNewData(newReelData);
		SlotsDataManager.Instance.UpdateSlotsData(currentSlotsData);

		// Instantiate new reel GameObject at position
		GameObject g = Instantiate(reelPrefab, currentReelsGroup);
		GameReel reel = g.GetComponent<GameReel>();

		// initialize with provided strip or default
		if (newReelData.CurrentReelStrip != null)
		{
			reel.InitializeReel(newReelData, index, eventManager, newReelData.CurrentReelStrip);
		}
		else
		{
			reel.InitializeReel(newReelData, index, eventManager, newReelData.DefaultReelStrip);
		}

		// Insert into runtime list at index
		reels.Insert(index, reel);

		// Refresh indices/initialization for subsequent reels
		RefreshReelsAfterModification();

		// Broadcast event
		eventManager.BroadcastEvent(SlotsEvent.ReelAdded, reel);
	}

	/// <summary>
	/// Remove a reel at runtime by index. Prevents removal during spin. Broadcasts event and persists data.
	/// </summary>
	public void RemoveReelAt(int index)
	{
		if (stateMachine != null && stateMachine.CurrentState == State.Spinning)
		{
			Debug.LogWarning("Cannot remove reel while spinning.");
			return;
		}

		if (index < 0 || index >= reels.Count)
		{
			throw new ArgumentOutOfRangeException(nameof(index));
		}

		// Remove from data model and persist
		var dataToRemove = currentSlotsData.CurrentReelData[index];
		currentSlotsData.RemoveReel(dataToRemove);
		// Also remove from ReelDataManager local store if exists
		ReelDataManager.Instance.RemoveDataIfExists(dataToRemove);
		SlotsDataManager.Instance.UpdateSlotsData(currentSlotsData);

		// Destroy visual reel and remove from list
		GameReel reel = reels[index];
		if (reel != null)
		{
			if (reel.gameObject != null) Destroy(reel.gameObject);
		}

		reels.RemoveAt(index);

		// Refresh remaining reels indices and positions
		RefreshReelsAfterModification();

		// Broadcast removed event
		eventManager.BroadcastEvent(SlotsEvent.ReelRemoved, reel);
	}

	/// <summary>
	/// Remove a reel by reference.
	/// </summary>
	public void RemoveReel(GameReel reelToRemove)
	{
		int idx = reels.IndexOf(reelToRemove);
		if (idx == -1) throw new ArgumentException("Reel not part of this SlotsEngine", nameof(reelToRemove));
		RemoveReelAt(idx);
	}

	/// <summary>
	/// Reinitialize remaining reels so their IDs and positions match list indices.
	/// This calls InitializeReel on each reel to update internal ids and layout values.
	/// </summary>
	private void RefreshReelsAfterModification()
	{
		for (int i = 0; i < reels.Count; i++)
		{
			var r = reels[i];
			if (r == null) continue;

			// Re-initialize reel to update its internal id and layout. Preserve its current strip.
			ReelData data = r.CurrentReelData;
			ReelStripData strip = r.ReelStrip;
			// Call appropriate initialize overload
			if (strip != null)
			{
				r.InitializeReel(data, i, eventManager, strip);
			}
			else
			{
				r.InitializeReel(data, i, eventManager, data.DefaultReelStrip);
			}
		}

		RepositionReels();
	}

	private void RepositionReels()
	{
		if (reels.Count == 0 || currentSlotsData.CurrentReelData.Count == 0) return;

		ReelData reelDefinition = currentSlotsData.CurrentReelData[0];

		for (int i = 0; i < reels.Count; i++)
		{
			var data = currentSlotsData.CurrentReelData[i];
			var g = reels[i].gameObject;
			if (g != null)
			{
				g.transform.localPosition = new Vector3((data.SymbolSpacing + data.SymbolSize) * i, 0, 0);
			}
		}

		int count = reels.Count;
		float totalWidth = (count * (reelDefinition.SymbolSize)) + ((count - 1) * reelDefinition.SymbolSpacing);
		float offset = totalWidth / 2f;
		float xPos = (-offset + (reelDefinition.SymbolSize / 2f));

		count = currentSlotsData.CurrentReelData.Max(x => x.SymbolCount);
		totalWidth = (count * (reelDefinition.SymbolSize)) + ((count - 1) * reelDefinition.SymbolSpacing);
		offset = totalWidth / 2f;
		float yPos = (-offset + (reelDefinition.SymbolSize / 2f));

		if (currentReelsGroup != null)
		{
			currentReelsGroup.transform.localPosition = new Vector3(xPos, yPos, 0);
		}
	}

	public void AdjustReelSize(float totalHeight, float totalWidth)
	{
		// Do not allow layout changes while reels are actively spinning
		if (stateMachine.CurrentState == State.Spinning)
		{
			throw new InvalidOperationException("Should not adjust reels while they are spinning!");
		}

		// If reels haven't been created yet - create them using the usual path.
		if (reels == null || reels.Count == 0)
		{
			int maxSymbolsPre = currentSlotsData.CurrentReelData.Max(x => x.SymbolCount);
			// fallback simple sizing when no runtime reels exist yet
			float availableStepPre = (totalHeight / maxSymbolsPre) * 0.8f;
			float spacingPre = availableStepPre * 0.2f;

			foreach (ReelData r in currentSlotsData.CurrentReelData)
			{
				r.SetSymbolSize(availableStepPre - spacingPre, spacingPre);
			}

			SpawnReels(reelsRootTransform);
			return;
		}

		// Determine counts
		int maxSymbols = currentSlotsData.CurrentReelData.Max(x => x.SymbolCount);
		int reelCount = currentSlotsData.CurrentReelData.Count;

		// Configuration: how much of the container to fill (leave margins)
		const float heightFill = 0.8f; // fraction of vertical space used
		const float widthFill = 0.95f; // fraction of horizontal space used
		const float spacingFactor = 0.1f; // spacing expressed as fraction of symbol size

		// Compute max symbol size allowed by height
		float symbolMaxByHeight = float.MaxValue;
		if (totalHeight > 0f && maxSymbols > 0)
		{
			// Each symbol uses (symbol + spacing) vertically; spacing = spacingFactor * symbol
			float stepPerSymbol = 1f + spacingFactor; // multiplier for symbol to get step
			symbolMaxByHeight = (totalHeight * heightFill) / (maxSymbols * stepPerSymbol);
		}

		// Compute max symbol size allowed by width
		float symbolMaxByWidth = float.MaxValue;
		if (totalWidth > 0f && reelCount > 0)
		{
			// total width = reelCount*symbol + (reelCount-1)*spacing
			// with spacing = spacingFactor * symbol, totalWidthNeeded = symbol * (reelCount + (reelCount-1)*spacingFactor)
			float denom = reelCount + (reelCount - 1) * spacingFactor;
			if (denom > 0f)
			{
				symbolMaxByWidth = (totalWidth * widthFill) / denom;
			}
		}

		// Choose the largest symbol size that fits both constraints
		float chosenSymbolSize = Math.Min(symbolMaxByHeight, symbolMaxByWidth);

		// Fallback guard
		if (float.IsInfinity(chosenSymbolSize) || float.IsNaN(chosenSymbolSize) || chosenSymbolSize <= 0f)
		{
			// no constraints available, keep existing first reel size
			chosenSymbolSize = currentSlotsData.CurrentReelData[0].SymbolSize;
		}

		float chosenSpacing = chosenSymbolSize * spacingFactor;

		// Quick exit: if the first reel data already matches desired sizes, assume no change needed.
		ReelData firstDef = currentSlotsData.CurrentReelData[0];
		bool sizeMatches = Mathf.Approximately(firstDef.SymbolSize, chosenSymbolSize) && Mathf.Approximately(firstDef.SymbolSpacing, chosenSpacing);
		bool reelsCountMatches = reels.Count == currentSlotsData.CurrentReelData.Count;
		if (sizeMatches && reelsCountMatches)
		{
			return;
		}

		// Apply new symbol size values to the data model
		foreach (ReelData r in currentSlotsData.CurrentReelData)
		{
			r.SetSymbolSize(chosenSymbolSize, chosenSpacing);
		}

		// Adjust existing reel GameObjects and their symbols in-place (no Destroy/Instantiate)
		for (int i = 0; i < reels.Count; i++)
		{
			var reel = reels[i];
			if (reel == null) continue;

			// Update the reel's internal layout (resizes symbols, repositions children and buffer offset)
			reel.UpdateSymbolLayout(chosenSymbolSize, chosenSpacing);

			// Reposition the reel X based on new symbol size + spacing
			float x = (chosenSpacing + chosenSymbolSize) * i;
			reel.transform.localPosition = new Vector3(x, 0f, 0f);
		}

		// Recompute group offsets and set currentReelsGroup position like SpawnReels did
		ReelData reelDefinition = currentSlotsData.CurrentReelData[0];

		int count = reels.Count;
		float totalWidthComputed = (count * (reelDefinition.SymbolSize)) + ((count - 1) * reelDefinition.SymbolSpacing);
		float offset = totalWidthComputed / 2f;
		float xPos = (-offset + (reelDefinition.SymbolSize / 2f));

		count = currentSlotsData.CurrentReelData.Max(x => x.SymbolCount);
		totalWidthComputed = (count * (reelDefinition.SymbolSize)) + ((count - 1) * reelDefinition.SymbolSpacing);
		offset = totalWidthComputed / 2f;
		float yPos = (-offset + (reelDefinition.SymbolSize / 2f));

		if (currentReelsGroup != null)
		{
			currentReelsGroup.transform.localPosition = new Vector3(xPos, yPos, 0);
		}
	}

	void OnReelCompleted(object obj)
	{
		if (reels.TrueForAll(x => !x.Spinning))
		{
			spinInProgress = false;
			eventManager.BroadcastEvent(SlotsEvent.SpinCompleted);
		}
	}

	void OnSpinCompleted(object obj)
	{
		try
		{
			// Gather current visual grid and slot metadata
			GameSymbol[] grid = GetCurrentSymbolGrid();
			int columns = currentSlotsData != null && currentSlotsData.CurrentReelData != null ? currentSlotsData.CurrentReelData.Count : reels.Count;
			int[] rowsPerColumn = new int[columns];

			// Fill rowsPerColumn defensively
			for (int i = 0; i < columns; i++)
			{
				if (currentSlotsData != null && currentSlotsData.CurrentReelData != null && i < currentSlotsData.CurrentReelData.Count)
				{
					rowsPerColumn[i] = currentSlotsData.CurrentReelData[i].SymbolCount;
				}
				else if (i < reels.Count && reels[i] != null && reels[i].CurrentReelData != null)
				{
					rowsPerColumn[i] = reels[i].CurrentReelData.SymbolCount;
				}
				else
				{
					rowsPerColumn[i] = 1; // fallback
				}
			}

			List<WinlineDefinition> winlineDefs = null;
			if (currentSlotsData != null)
			{
				winlineDefs = currentSlotsData.WinlineDefinitions;
			}

			// Evaluate wins using the evaluator and then write a detailed log
			if (WinlineEvaluator.Instance != null)
			{
				try
				{
					var wins = WinlineEvaluator.Instance.EvaluateWinsFromGameSymbols(grid, columns, rowsPerColumn, winlineDefs);
					if (WinlineEvaluator.Instance.LoggingEnabled)
					{
						WinlineEvaluator.Instance.LogSpinResult(grid, columns, rowsPerColumn, winlineDefs ?? new List<WinlineDefinition>(), wins);
					}
				}
				catch (Exception ex)
				{
					Debug.LogWarning($"Winline evaluation/logging failed: {ex.Message}");
				}
			}
		}
		catch (Exception ex)
		{
			Debug.LogException(ex);
		}

		stateMachine.SetState(State.Presentation);
	}

	private void OnPresentationEnter(object obj)
	{
		foreach (GameReel gr in reels)
		{
			gr.DimDummySymbols();
		}

		eventManager.BroadcastEvent(SlotsEvent.BeginSlotPresentation, this);
	}

	private void OnPresentationComplete(object obj)
	{
		stateMachine.SetState(State.Idle);
	}

	public GameSymbol[] GetCurrentSymbolGrid()
	{
		List<GameSymbol[]> reelSymbols = new List<GameSymbol[]>();
		foreach (GameReel gameReel in reels)
		{
			var newList = new GameSymbol[gameReel.Symbols.Count];
			for (int i = 0; i < gameReel.Symbols.Count; i++)
			{
				newList[i] = gameReel.Symbols[i];
			}
			reelSymbols.Add(newList);
		}

		return Helpers.CombineColumnsToGrid(reelSymbols);
	}

	public void BroadcastSlotsEvent(SlotsEvent eventName, object value = null)
	{
		eventManager.BroadcastEvent(eventName, value);
	}

	public void SaveSlotsData()
	{
		SlotsDataManager.Instance.AddNewData(currentSlotsData);
		DataPersistenceManager.Instance.SaveGame();
	}

	// Allow external managers to subscribe to reel add/remove notifications using the EventManager
	public void RegisterReelChanged(Action<object> handler)
	{
		if (handler == null) return;
		if (eventManager == null)
		{
			// queue until initialization completes
			if (pendingReelChangedHandlers == null) pendingReelChangedHandlers = new List<Action<object>>();
			pendingReelChangedHandlers.Add(handler);
			return;
		}
		eventManager.RegisterEvent(SlotsEvent.ReelAdded, handler);
		eventManager.RegisterEvent(SlotsEvent.ReelRemoved, handler);
	}

	public void UnregisterReelChanged(Action<object> handler)
	{
		if (handler == null) return;
		if (eventManager == null)
		{
			if (pendingReelChangedHandlers != null && pendingReelChangedHandlers.Contains(handler))
			{
				pendingReelChangedHandlers.Remove(handler);
			}
			return;
		}
		eventManager.UnregisterEvent(SlotsEvent.ReelAdded, handler);
		eventManager.UnregisterEvent(SlotsEvent.ReelRemoved, handler);
	}
}