using DG.Tweening;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using System.Reflection;

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

		// Build per-column visual arrays from the runtime reels
		var reels = slotsToPresent.CurrentReels;
		if (reels == null || reels.Count == 0)
		{
			return;
		}

		List<GameSymbol[]> visualColumns = new List<GameSymbol[]>();
		for (int c = 0; c < reels.Count; c++)
		{
			var symbolsList = reels[c].Symbols;
			visualColumns.Add(symbolsList != null ? symbolsList.ToArray() : new GameSymbol[0]);
		}

		int columns = visualColumns.Count;
		int[] rowsPerColumn = new int[columns];
		for (int c = 0; c < columns; c++) rowsPerColumn[c] = visualColumns[c]?.Length ?? 0;

		// If visual/model counts differ, throw an error as requested
		for (int c = 0; c < columns; c++)
		{
			int modelCount = 0;
			if (c < slotsToPresent.CurrentSlotsData.CurrentReelData.Count)
			{
				modelCount = slotsToPresent.CurrentSlotsData.CurrentReelData[c].SymbolCount;
			}
			if (modelCount != rowsPerColumn[c])
			{
				throw new System.InvalidOperationException($"Visual rows ({rowsPerColumn[c]}) differ from model rows ({modelCount}) for column {c}.");
			}
		}

		// Build a combined grid for debug logging
		GameSymbol[] currentSymbolGrid = Helpers.CombineColumnsToGrid(visualColumns);

		// Debug logging to help diagnose presentation-time mismatches
		if ((Application.isEditor || Debug.isDebugBuild) && WinlineEvaluator.Instance != null && WinlineEvaluator.Instance.LoggingEnabled)
		{
			Debug.Log($"Presentation: columns={columns}, rowsPerColumn=[{string.Join(",", rowsPerColumn)}]");

			// print grid contents with indexes and symbol info
			for (int i = 0; i < currentSymbolGrid.Length; i++)
			{
				var gs = currentSymbolGrid[i];
				if (gs == null)
				{
					Debug.Log($"Grid idx={i}: null");
					continue;
				}
				var sd = gs.CurrentSymbolData;
				string name = sd != null ? sd.Name : "(null)";
				int min = sd != null ? sd.MinWinDepth : -999;
				int baseVal = sd != null ? sd.BaseValue : 0;
				string scaling = sd != null ? sd.PayScaling.ToString() : "(none)";
				Debug.Log($"Grid idx={i}: name={name} minWin={min} baseValue={baseVal} scaling={scaling} isWild={(sd!=null?sd.IsWild:false)} allowWild={(sd!=null?sd.AllowWildMatch:false)}");
			}

			// print each winline concrete indexes and corresponding grid names from assets
			var winlinesAsset = slotsToPresent.CurrentSlotsData.WinlineDefinitions;
			if (winlinesAsset != null)
			{
				for (int wi = 0; wi < winlinesAsset.Count; wi++)
				{
					var wl = winlinesAsset[wi];
					int[] concrete = wl.GenerateIndexes(columns, rowsPerColumn);
					if (concrete == null) concrete = new int[0];
					var names = concrete.Select(idx => (idx >= 0 && idx < currentSymbolGrid.Length && currentSymbolGrid[idx] != null) ? currentSymbolGrid[idx].CurrentSymbolData?.Name ?? "(null)" : "null").ToArray();
					Debug.Log($"Winline[{wi}] '{wl.name}' concrete=[{string.Join(",", concrete)}] names=[{string.Join(",", names)}]");
				}
			}
		}

		// Prepare evaluation winlines: include asset winlines and add temporary StraightAcross winlines for each visual row if missing
		List<WinlineDefinition> evalWinlines = new List<WinlineDefinition>();
		if (slotsToPresent.CurrentSlotsData.WinlineDefinitions != null)
			 evalWinlines.AddRange(slotsToPresent.CurrentSlotsData.WinlineDefinitions);

		int maxRows = rowsPerColumn.Max();
		for (int r = 0; r < maxRows; r++)
		{
			bool exists = evalWinlines.Any(w => w.Pattern == WinlineDefinition.PatternType.StraightAcross && w.RowIndex == r);
			if (!exists)
			{
				var temp = ScriptableObject.CreateInstance<WinlineDefinition>();
				var t = typeof(WinlineDefinition);
				var patternField = t.GetField("pattern", BindingFlags.Instance | BindingFlags.NonPublic);
				var rowField = t.GetField("rowIndex", BindingFlags.Instance | BindingFlags.NonPublic);
				var multField = t.GetField("winMultiplier", BindingFlags.Instance | BindingFlags.NonPublic);
				if (patternField != null) patternField.SetValue(temp, WinlineDefinition.PatternType.StraightAcross);
				if (rowField != null) rowField.SetValue(temp, r);
				if (multField != null) multField.SetValue(temp, 1);
				temp.name = $"__auto_Straight_row_{r}";
				evalWinlines.Add(temp);
				if ((Application.isEditor || Debug.isDebugBuild) && WinlineEvaluator.Instance != null && WinlineEvaluator.Instance.LoggingEnabled) Debug.Log($"Added temporary StraightAcross winline for row {r}");
			}
		}

		// Evaluate using the per-column visual arrays (handles varying column lengths)
		List<WinData> winData = WinlineEvaluator.Instance.EvaluateWinsFromColumns(visualColumns, evalWinlines);
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
				slotsToPresent.BroadcastSlotsEvent(SlotsEvent.SymbolWin, grid[index]);
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
		slotsToComplete.slotsEngine.BroadcastSlotsEvent(SlotsEvent.PresentationComplete);
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