using DG.Tweening;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class PresentationController : Singleton<PresentationController>
{
	private List<SlotsPresentationData> slots = new List<SlotsPresentationData>();

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

		if (SlotConsoleController.Instance != null)
		{
			SlotConsoleController.Instance.BeginPresentationSession();
		}

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
		slotsData.SetCurrentWinData(winData);

		if (winData.Count > 0)
		{
			PlayWinlines(slotsToPresent, currentSymbolGrid, winData);
			// use coroutine instead of DOTween.Sequence to avoid allocations from Sequence/Callback closures
			StartCoroutine(DelayedCompletePresentation(slotsData, 1f));
		}
		else
		{
			CompletePresentation(slotsData);
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
		slotsToComplete.SetPresentationCompleted(true);
		slotsToComplete.slotsEngine.BroadcastSlotsEvent(SlotsEvent.PresentationComplete);
		slotsToComplete.ClearCurrentWinData();
		SlotConsoleController.Instance?.EndPresentationSession();
	}
}

class SlotsPresentationData
{
	public EventManager slotsEventManager;
	public SlotsEngine slotsEngine;
	public List<WinData> currentWinData;
	public bool presentationCompleted;
	public void SetCurrentWinData(List<WinData> newWinData) { currentWinData = newWinData; }
	public void ClearCurrentWinData() { currentWinData = null; }
	public void SetPresentationCompleted(bool complete) { presentationCompleted = complete; }
}