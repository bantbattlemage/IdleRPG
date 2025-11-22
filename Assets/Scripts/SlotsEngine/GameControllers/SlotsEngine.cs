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
		reelsRootTransform = canvasTransform;
		SpawnReels(reelsRootTransform);
	}

	public void BeginSlots() => stateMachine.BeginStateMachine();

	public void SpinOrStopReels(bool spin)
	{
		if (!spinInProgress && spin) SpinAllReels();
		else if (spinInProgress && !spin && stateMachine.CurrentState == State.Spinning) StopAllReels();
		else if (GamePlayer.Instance.CurrentCredits < GamePlayer.Instance.CurrentBet.CreditCost) SlotConsoleController.Instance.SetConsoleMessage("Not enough credits! Switch to a lower bet or add credits.");
	}

	private void SpinAllReels()
	{
		if (spinInProgress) throw new InvalidOperationException("Spin already in progress!");
		try { if (WinEvaluator.Instance != null && WinEvaluator.Instance.LoggingEnabled) WinEvaluator.Instance.NotifySpinStarted(); } catch { }
		float falloutDelay = 0.025f;
		for (int i = 0; i < reels.Count; i++)
		{
			List<SymbolData> testSolution = new List<SymbolData>(); var selectionsForReel = new List<SymbolData>();
			for (int k = 0; k < reels[i].CurrentReelData.SymbolCount; k++) { var symbol = reels[i].GetRandomSymbolFromStrip(selectionsForReel); testSolution.Add(symbol); if (symbol != null) selectionsForReel.Add(symbol); }
			reels[i].CurrentReelData.SetCurrentSymbolData(testSolution);
			reels[i].BeginSpin(testSolution, falloutDelay); falloutDelay += 0.025f;
		}
	}

	private void OnReelSpinStarted(object obj)
	{
		if (!spinInProgress)
		{
			spinInProgress = true; stateMachine.SetState(State.Spinning);
		}
	}

	private void StopAllReels()
	{
		if (!reels.TrueForAll(x => x.Spinning)) return;
		float stagger = 0.025f; for (int i = 0; i < reels.Count; i++) { reels[i].StopReel(stagger); stagger += 0.025f; }
		eventManager.BroadcastEvent(SlotsEvent.StoppingReels);
	}

	private void SpawnReels(Transform gameCanvas)
	{
		if (currentReelsGroup != null) { Destroy(currentReelsGroup.gameObject); reels.Clear(); }
		Transform reelsGroup = new GameObject("ReelsGroup").transform; reelsGroup.parent = gameCanvas.transform; reelsGroup.localScale = Vector3.one;
		for (int i = 0; i < currentSlotsData.CurrentReelData.Count; i++)
		{
			ReelData data = currentSlotsData.CurrentReelData[i]; GameObject g = Instantiate(reelPrefab, reelsGroup.transform); GameReel reel = g.GetComponent<GameReel>();
			if (data.CurrentReelStrip != null) reel.InitializeReel(data, i, eventManager, data.CurrentReelStrip, this); else reel.InitializeReel(data, i, eventManager, data.DefaultReelStrip, this);
			g.transform.localPosition = new Vector3((data.SymbolSpacing + data.SymbolSize) * i, 0, 0); reels.Add(reel);
		}
		ReelData reelDef = currentSlotsData.CurrentReelData[0]; int count = reels.Count; float totalWidth = (count * reelDef.SymbolSize) + ((count - 1) * reelDef.SymbolSpacing); float offset = totalWidth / 2f; float xPos = (-offset + (reelDef.SymbolSize / 2f)); count = currentSlotsData.CurrentReelData.Max(x => x.SymbolCount); totalWidth = (count * reelDef.SymbolSize) + ((count - 1) * reelDef.SymbolSpacing); offset = totalWidth / 2f; float yPos = (-offset + (reelDef.SymbolSize / 2f)); reelsGroup.transform.localPosition = new Vector3(xPos, yPos, 0); currentReelsGroup = reelsGroup;
		RegenerateAllReelDummies();
	}

	private void RegenerateAllReelDummies()
	{
		foreach (var r in reels) if (r != null) r.RegenerateDummies();
	}

	public void AddReel(ReelData newReelData)
	{
		if (stateMachine != null && stateMachine.CurrentState == State.Spinning) { Debug.LogWarning("Cannot add reel while spinning."); return; }
		if (currentSlotsData == null) throw new InvalidOperationException("CurrentSlotsData is null");
		if (currentSlotsData.CurrentReelData.Count > 0) { var template = currentSlotsData.CurrentReelData[0]; newReelData.SetSymbolSize(template.SymbolSize, template.SymbolSpacing); }
		currentSlotsData.AddNewReel(newReelData); ReelDataManager.Instance.AddNewData(newReelData); SlotsDataManager.Instance.UpdateSlotsData(currentSlotsData);
		if (currentReelsGroup == null) { SpawnReels(reelsRootTransform); return; }
		GameObject g = Instantiate(reelPrefab, currentReelsGroup); GameReel reel = g.GetComponent<GameReel>(); int newIndex = reels.Count;
		if (newReelData.CurrentReelStrip != null) reel.InitializeReel(newReelData, newIndex, eventManager, newReelData.CurrentReelStrip, this); else reel.InitializeReel(newReelData, newIndex, eventManager, newReelData.DefaultReelStrip, this);
		reels.Add(reel); RepositionReels(); RegenerateAllReelDummies(); eventManager.BroadcastEvent(SlotsEvent.ReelAdded, reel);
	}
	public void AddReel(ReelDefinition definition) { if (definition == null) throw new ArgumentNullException(nameof(definition)); AddReel(definition.CreateInstance()); }
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

	public void InsertReelAt(int index, ReelData newReelData)
	{
		if (stateMachine != null && stateMachine.CurrentState == State.Spinning) { Debug.LogWarning("Cannot insert reel while spinning."); return; }
		if (currentSlotsData == null) throw new InvalidOperationException("CurrentSlotsData is null");
		if (index < 0 || index > currentSlotsData.CurrentReelData.Count) throw new ArgumentOutOfRangeException(nameof(index));
		if (currentSlotsData.CurrentReelData.Count > 0) { var template = currentSlotsData.CurrentReelData[0]; newReelData.SetSymbolSize(template.SymbolSize, template.SymbolSpacing); }
		currentSlotsData.InsertReelAt(index, newReelData); ReelDataManager.Instance.AddNewData(newReelData); SlotsDataManager.Instance.UpdateSlotsData(currentSlotsData);
		GameObject g = Instantiate(reelPrefab, currentReelsGroup); GameReel reel = g.GetComponent<GameReel>();
		if (newReelData.CurrentReelStrip != null) reel.InitializeReel(newReelData, index, eventManager, newReelData.CurrentReelStrip, this); else reel.InitializeReel(newReelData, index, eventManager, newReelData.DefaultReelStrip, this);
		reels.Insert(index, reel); RefreshReelsAfterModification(); RegenerateAllReelDummies(); eventManager.BroadcastEvent(SlotsEvent.ReelAdded, reel);
	}

	public void RemoveReelAt(int index)
	{
		if (stateMachine != null && stateMachine.CurrentState == State.Spinning) { Debug.LogWarning("Cannot remove reel while spinning."); return; }
		if (index < 0 || index >= reels.Count) throw new ArgumentOutOfRangeException(nameof(index));
		var dataToRemove = currentSlotsData.CurrentReelData[index]; currentSlotsData.RemoveReel(dataToRemove); ReelDataManager.Instance.RemoveDataIfExists(dataToRemove); SlotsDataManager.Instance.UpdateSlotsData(currentSlotsData);
		GameReel reel = reels[index]; if (reel != null && reel.gameObject != null) Destroy(reel.gameObject); reels.RemoveAt(index); RefreshReelsAfterModification(); RegenerateAllReelDummies(); eventManager.BroadcastEvent(SlotsEvent.ReelRemoved, reel);
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

	private void RepositionReels()
	{
		if (reels.Count == 0 || currentSlotsData.CurrentReelData.Count == 0) return; ReelData reelDef = currentSlotsData.CurrentReelData[0];
		for (int i = 0; i < reels.Count; i++) { var data = currentSlotsData.CurrentReelData[i]; var g = reels[i].gameObject; if (g != null) g.transform.localPosition = new Vector3((data.SymbolSpacing + data.SymbolSize) * i, 0, 0); }
		int count = reels.Count; float totalWidth = (count * reelDef.SymbolSize) + ((count - 1) * reelDef.SymbolSpacing); float offset = totalWidth / 2f; float xPos = (-offset + (reelDef.SymbolSize / 2f)); count = currentSlotsData.CurrentReelData.Max(x => x.SymbolCount); totalWidth = (count * reelDef.SymbolSize) + ((count - 1) * reelDef.SymbolSpacing); offset = totalWidth / 2f; float yPos = (-offset + (reelDef.SymbolSize / 2f)); if (currentReelsGroup != null) currentReelsGroup.transform.localPosition = new Vector3(xPos, yPos, 0);
	}

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

	void OnReelCompleted(object obj)
	{
		if (reels.TrueForAll(x => !x.Spinning)) { spinInProgress = false; eventManager.BroadcastEvent(SlotsEvent.SpinCompleted); }
	}

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

	public GameSymbol[] GetCurrentSymbolGrid()
	{
		List<GameSymbol[]> reelSymbols = new List<GameSymbol[]>(); foreach (GameReel gameReel in reels) { var newList = new GameSymbol[gameReel.Symbols.Count]; for (int i = 0; i < gameReel.Symbols.Count; i++) newList[i] = gameReel.Symbols[i]; reelSymbols.Add(newList); } return Helpers.CombineColumnsToGrid(reelSymbols);
	}
	public void BroadcastSlotsEvent(SlotsEvent eventName, object value = null) => eventManager.BroadcastEvent(eventName, value);
	public void SaveSlotsData() { SlotsDataManager.Instance.AddNewData(currentSlotsData); DataPersistenceManager.Instance.SaveGame(); }
	public void RegisterReelChanged(Action<object> handler)
	{
		if (handler == null) return; if (eventManager == null) { if (!pendingReelChangedHandlers.Contains(handler)) pendingReelChangedHandlers.Add(handler); return; } eventManager.RegisterEvent(SlotsEvent.ReelAdded, handler); eventManager.RegisterEvent(SlotsEvent.ReelRemoved, handler);
	}
	public void UnregisterReelChanged(Action<object> handler)
	{
		if (handler == null) return; if (eventManager == null) { if (pendingReelChangedHandlers.Contains(handler)) pendingReelChangedHandlers.Remove(handler); return; } eventManager.UnregisterEvent(SlotsEvent.ReelAdded, handler); eventManager.UnregisterEvent(SlotsEvent.ReelRemoved, handler);
	}
}