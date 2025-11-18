using DG.Tweening;
using System.Collections.Generic;
using System.Linq;

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

		eventManager.RegisterEvent("BeginSlotPresentation", OnBeginSlotPresentation);
	}

	private void OnBeginSlotPresentation(object obj)
	{
		SlotsEngine slotsToPresent = (SlotsEngine)obj;

		var currentSymbolGrid = slotsToPresent.GetCurrentSymbolGrid();
		List<WinData> winData = WinlineEvaluator.Instance.EvaluateWins(currentSymbolGrid.ToSymbolDefinitions(), slotsToPresent.SlotsDefinition.WinlineDefinitions);
		SlotsPresentationData slotsData = slots.FirstOrDefault(x => x.slotsEngine == slotsToPresent);
		slotsData.SetCurrentWinData(winData);

		if (winData.Count > 0)
		{
			PlayWinlines(slotsToPresent, currentSymbolGrid, winData);

			DOTween.Sequence().AppendInterval(1f).AppendCallback(() =>
			{
				CompletePresentation(slotsData);
			});
		}
		else
		{
			CompletePresentation(slotsData);
		}
	}

	private void PlayWinlines(SlotsEngine slotsToPresent, GameSymbol[] grid, List<WinData> winData)
	{
		foreach (WinData w in winData)
		{
			foreach (int index in w.WinningSymbolIndexes)
			{
				slotsToPresent.BroadcastSlotsEvent("SymbolWin", grid[index]);
			}
		}

		int totalWin = winData.Sum((x => x.WinValue));
		GamePlayer.Instance.AddCredits(totalWin);
		SlotConsoleController.Instance.SetWinText(totalWin);

		string winMessage = string.Empty;
		if (winData.Count == 1)
		{
			winMessage = $"Won {winData[0].WinValue} on line {winData[0].LineIndex}!";
		}
		else
		{
			winMessage = $"Won {totalWin} on {winData.Count} lines!";
		}

		SlotConsoleController.Instance.SetConsoleMessage(winMessage);
	}

	private void ClearAllCurrentWinData()
	{
		foreach (SlotsPresentationData slot in slots)
		{
			slot.SetPresentationCompleted(false);
			slot.ClearCurrentWinData();
		}
	}

	private void CompletePresentation(SlotsPresentationData slotsToComplete)
	{
		slotsToComplete.SetPresentationCompleted(true);
		slotsToComplete.slotsEngine.BroadcastSlotsEvent("PresentationComplete");
		slotsToComplete.ClearCurrentWinData();

		if (slots.TrueForAll(x => x.presentationCompleted))
		{

		}
	}
}

struct SlotsPresentationData
{
	public EventManager slotsEventManager;
	public SlotsEngine slotsEngine;
	public List<WinData> currentWinData;
	public bool presentationCompleted;

	public void SetCurrentWinData(List<WinData> newWinData)
	{
		currentWinData = newWinData;
	}

	public void ClearCurrentWinData()
	{
		currentWinData = null;
	}

	public void SetPresentationCompleted(bool complete)
	{
		presentationCompleted = complete;
	}
}