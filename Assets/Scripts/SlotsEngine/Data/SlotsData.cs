using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class SlotsData : Data
{
	[SerializeField] private int index;
	public int Index => index;

	[SerializeField] private List<ReelData> currentReelData;
	public List<ReelData> CurrentReelData => currentReelData;

	[SerializeField] private List<WinlineDefinition> winlineDefinitions;
	public List<WinlineDefinition> WinlineDefinitions => winlineDefinitions;

	[SerializeField] private List<BetLevelDefinition> betLevelDefinitions;
	public List<BetLevelDefinition> BetLevelDefinitions => betLevelDefinitions;

	[SerializeField] private SlotsDefinition baseDefinition;

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
