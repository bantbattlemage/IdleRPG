using System.Collections.Generic;
using UnityEngine;

public class SlotsData : Data
{
	private int index;
	public int Index => index;

	private List<ReelData> currentReelData;
	public List<ReelData> CurrentReelData => currentReelData;

	private List<WinlineDefinition> winlineDefinitions;
	public List<WinlineDefinition> WinlineDefinitions => winlineDefinitions;

	private List<BetLevelDefinition> betLevelDefinitions;
	public List<BetLevelDefinition> BetLevelDefinitions => betLevelDefinitions;

	private SlotsDefinition baseDefinition;

	public SlotsData(int id, List<WinlineDefinition> winlines, List<BetLevelDefinition> bets)
	{
		index = id;
		currentReelData = new List<ReelData>();
		winlineDefinitions = winlines;
		betLevelDefinitions = bets;
	}

	public SlotsData(int id, List<ReelData> reelData, SlotsDefinition slotDefinition)
	{
		index = id;
		currentReelData = reelData;
		baseDefinition = slotDefinition;
	}

	public void AddNewReel(ReelData reelData)
	{
		currentReelData.Add(reelData);
	}
}
