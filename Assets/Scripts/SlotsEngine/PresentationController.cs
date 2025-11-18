using DG.Tweening;
using System.Collections.Generic;
using System.Linq;

public class PresentationController : Singleton<PresentationController>
{
	private EventManager eventManager;
	private SlotsEngine slotsEngine;

	public void InitializeWinPresentation(EventManager slotsEventManager, SlotsEngine parentSlots)
	{
		eventManager = slotsEventManager;
		slotsEngine = parentSlots;
		slotsEventManager.RegisterEvent("PresentationEnter", OnPresentation);
	}

	private void OnPresentation(object obj)
	{
		var currentSymbolGrid = slotsEngine.GetCurrentSymbolGrid();
		List<WinData> winData = WinlineEvaluator.Instance.EvaluateWins(currentSymbolGrid.ToSymbolDefinitions(), slotsEngine.SlotsDefinition.WinlineDefinitions);

		if (winData.Count > 0)
		{
			PlayWinlines(currentSymbolGrid, winData);

			DOTween.Sequence().AppendInterval(1f).AppendCallback(CompletePresentation);
		}
		else
		{
			CompletePresentation();
		}
	}

	private void PlayWinlines(GameSymbol[] grid, List<WinData> winData)
	{
		foreach (WinData w in winData)
		{
			foreach (int index in w.WinningSymbolIndexes)
			{
				eventManager.BroadcastEvent("SymbolWin", grid[index]);
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

	private void CompletePresentation()
	{
		eventManager.BroadcastEvent("PresentationComplete");
	}
}
