using DG.Tweening;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class PresentationController : Singleton<PresentationController>
{
	private List<SlotsPresentationData> slots = new List<SlotsPresentationData>();

	// Whether a grouped presentation session is currently active
	private bool presentationSessionActive = false;

	public void AddSlotsToPresentation(EventManager eventManager, SlotsEngine parentSlots)
	{
		SlotsPresentationData newData = new SlotsPresentationData()
		{
			slotsEventManager = eventManager,
			slotsEngine = parentSlots
		};
		slots.Add(newData);

		eventManager.RegisterEvent(SlotsEvent.BeginSlotPresentation, OnBeginSlotPresentation);
	}

	private void OnBeginSlotPresentation(object obj)
	{
		SlotsEngine slotsToPresent = (SlotsEngine)obj;
		if (slotsToPresent == null) return;

		var reels = slotsToPresent.CurrentReels;
		if (reels == null || reels.Count == 0) return;

		List<GameSymbol[]> visualColumns = new List<GameSymbol[]>();
		for (int c = 0; c < reels.Count; c++)
		{
			var symbolsList = reels[c].Symbols;
			visualColumns.Add(symbolsList != null ? symbolsList.ToArray() : new GameSymbol[0]);
		}

		int columns = visualColumns.Count;
		int[] rowsPerColumn = new int[columns];
		for (int c = 0; c < columns; c++) rowsPerColumn[c] = visualColumns[c]?.Length ?? 0;

		for (int c = 0; c < columns; c++)
		{
			int modelCount = c < slotsToPresent.CurrentSlotsData.CurrentReelData.Count ? slotsToPresent.CurrentSlotsData.CurrentReelData[c].SymbolCount : 0;
			if (modelCount != rowsPerColumn[c]) throw new System.InvalidOperationException($"Visual rows ({rowsPerColumn[c]}) differ from model rows ({modelCount}) for column {c}.");
		}

		GameSymbol[] currentSymbolGrid = Helpers.CombineColumnsToGrid(visualColumns);

		// Use a single source of truth for win information: prefer the wins computed by the evaluator at spin completion.
		List<WinData> winData = WinEvaluator.Instance != null && WinEvaluator.Instance.CurrentSpinWinData != null
			? WinEvaluator.Instance.CurrentSpinWinData
			: new List<WinData>();

		SlotsPresentationData slotsData = slots.FirstOrDefault(x => x.slotsEngine == slotsToPresent);
		if (slotsData == null) return;

		slotsData.SetCurrentWinData(winData);
		slotsData.SetCurrentGrid(currentSymbolGrid);
		slotsData.SetReadyToPresent(true);
		slotsData.SetPresentationCompleted(false);

		// Determine which slots are participating in this presentation group: those that are in Presentation state.
		var presentingEngines = slots.Where(s => s.slotsEngine != null && s.slotsEngine.CurrentState == State.Presentation).ToList();

		// If any presenting engine hasn't signaled readiness yet, wait until they do (they will call this handler when ready).
		bool allPresentingReady = presentingEngines.Count > 0 && presentingEngines.All(s => s.readyToPresent);

		if (!presentationSessionActive)
		{
			if (allPresentingReady)
			{
				StartGroupPresentation(presentingEngines);
			}
			// else: wait for remaining engines to call BeginSlotPresentation
		}
		else
		{
			// If session already active (shouldn't normally happen), start this slot immediately
			StartIndividualPresentation(slotsData);
		}
	}

	private void StartGroupPresentation(List<SlotsPresentationData> participants)
	{
		if (presentationSessionActive) return;
		if (participants == null || participants.Count == 0) return;

		presentationSessionActive = true;
		SlotConsoleController.Instance?.BeginPresentationSession();

		// For now start all participants simultaneously. Structure is in place to sequence or selectively play later.
		foreach (var p in participants)
		{
			StartIndividualPresentation(p);
		}
	}

	private void StartIndividualPresentation(SlotsPresentationData p)
	{
		if (p == null) return;
		if (p.presentationStarted) return;
		p.presentationStarted = true;

		if (p.currentWinData != null && p.currentWinData.Count > 0)
		{
			PlayWinlines(p.slotsEngine, p.currentGrid, p.currentWinData);
			StartCoroutine(DelayedCompletePresentation(p, 1f));
		}
		else
		{
			// No wins: mark completed immediately (but DO NOT broadcast PresentationComplete yet — wait for group completion)
			CompletePresentation(p);
		}
	}

	private IEnumerator<YieldInstruction> DelayedCompletePresentation(SlotsPresentationData slotsData, float delay)
	{
		if (delay > 0f) yield return new WaitForSeconds(delay);
		CompletePresentation(slotsData);
	}

	private void PlayWinlines(SlotsEngine slotsToPresent, GameSymbol[] grid, List<WinData> winData)
	{
		int gridLen = grid != null ? grid.Length : 0;
		foreach (WinData w in winData)
		{
			if (w.WinningSymbolIndexes != null)
			{
				foreach (int index in w.WinningSymbolIndexes)
				{
					if (index < 0 || index >= gridLen) continue; // skip invalid indexes
					var gs = grid[index]; if (gs == null) continue; // skip null slots
					slotsToPresent.BroadcastSlotsEvent(SlotsEvent.SymbolWin, gs);
				}
			}
			string individualMsg = w.LineIndex >= 0 ? $"Won {w.WinValue} on line {w.LineIndex}!" : (w.WinningSymbolIndexes?.Length <= 1 ? $"Won {w.WinValue}!" : $"Won {w.WinValue} ({w.WinningSymbolIndexes.Length} symbols)!");
			SlotConsoleController.Instance?.AppendPresentationMessage(slotsToPresent, individualMsg);
		}
		int totalWin = winData.Sum(x => x.WinValue);
		GamePlayer.Instance.AddCredits(totalWin);
		SlotConsoleController.Instance?.SetWinText(totalWin);
		string summary = (winData.Count == 1) ? $"Won {winData[0].WinValue} on line {winData[0].LineIndex}!" : $"Won {totalWin} on {winData.Count} line(s)!";
		SlotConsoleController.Instance?.AppendPresentationMessage(slotsToPresent, summary);
	}

	private void CompletePresentation(SlotsPresentationData slotsToComplete)
	{
		if (slotsToComplete == null) return;

		// Mark this slot as completed locally but DO NOT broadcast PresentationComplete yet — wait until group completes.
		slotsToComplete.SetPresentationCompleted(true);
		slotsToComplete.ClearCurrentWinData();

		// After marking, check if the overall group presentation can finish
		CheckGroupCompletion();
	}

	private void CheckGroupCompletion()
	{
		if (!presentationSessionActive) return;

		// Participants are those that were in Presentation state when the group started (readyToPresent was set)
		var participants = slots.Where(s => s.readyToPresent).ToList();
		if (participants.Count == 0)
		{
			// no participants — end session defensively
			presentationSessionActive = false;
			SlotConsoleController.Instance?.EndPresentationSession();
			return;
		}

		bool allDone = participants.All(p => p.presentationCompleted);
		if (!allDone) return;

		// All participants finished, now broadcast PresentationComplete for each and end session
		foreach (var p in participants)
		{
			try { p.slotsEngine.BroadcastSlotsEvent(SlotsEvent.PresentationComplete); } catch { }
		}

		presentationSessionActive = false;
		SlotConsoleController.Instance?.EndPresentationSession();

		// Reset per-participant staging flags so future spins can reuse structure
		foreach (var p in participants)
		{
			p.SetPresentationCompleted(false);
			p.SetReadyToPresent(false);
			p.presentationStarted = false;
			p.ClearCurrentGrid();
			p.ClearCurrentWinData();
		}
	}
}

class SlotsPresentationData
{
	public EventManager slotsEventManager;
	public SlotsEngine slotsEngine;
	public List<WinData> currentWinData;
	public GameSymbol[] currentGrid;
	public bool presentationCompleted;
	public bool readyToPresent;
	public bool presentationStarted;

	public void SetCurrentWinData(List<WinData> newWinData) { currentWinData = newWinData; }
	public void ClearCurrentWinData() { currentWinData = null; }
	public void SetPresentationCompleted(bool complete) { presentationCompleted = complete; }

	public void SetCurrentGrid(GameSymbol[] grid) { currentGrid = grid; }
	public void ClearCurrentGrid() { currentGrid = null; }
	public void SetReadyToPresent(bool ready) { readyToPresent = ready; }
}