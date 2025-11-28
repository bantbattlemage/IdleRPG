using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class SlotsData : Data
{
	[SerializeField] private int index;
	public int Index { get => index; set => index = value; }

	[SerializeField] private List<ReelData> currentReelData;
	public List<ReelData> CurrentReelData => currentReelData;

	// store definition keys
	[SerializeField] private string[] winlineDefinitionKeys;
	[SerializeField] private string[] betLevelDefinitionKeys;

	[System.NonSerialized] private List<WinlineDefinition> winlineDefinitions;
	[System.NonSerialized] private List<BetLevelDefinition> betLevelDefinitions;

	[SerializeField] private string baseDefinitionKey;
	[System.NonSerialized] private SlotsDefinition baseDefinition;

	public List<WinlineDefinition> WinlineDefinitions
	{
		get
		{
			EnsureResolved();
			return winlineDefinitions;
		}
	}

	public List<BetLevelDefinition> BetLevelDefinitions
	{
		get
		{
			EnsureResolved();
			return betLevelDefinitions;
		}
	}

	public SlotsDefinition BaseDefinition
	{
		get
		{
			EnsureResolved();
			return baseDefinition;
		}
	}

	public SlotsData(int id, List<WinlineDefinition> winlines, List<BetLevelDefinition> bets)
	{
		index = id;
		currentReelData = new List<ReelData>();
		winlineDefinitions = winlines;
		betLevelDefinitions = bets;

		if (winlineDefinitions != null)
		{
			winlineDefinitionKeys = new string[winlineDefinitions.Count];
			for (int i = 0; i < winlineDefinitions.Count; i++) winlineDefinitionKeys[i] = winlineDefinitions[i].name;
		}

		if (betLevelDefinitions != null)
		{
			betLevelDefinitionKeys = new string[betLevelDefinitions.Count];
			for (int i = 0; i < betLevelDefinitions.Count; i++) betLevelDefinitionKeys[i] = betLevelDefinitions[i].name;
		}
	}

	public SlotsData(int id, List<ReelData> reelData, SlotsDefinition slotDefinition)
	{
		index = id;
		currentReelData = reelData;
		baseDefinition = slotDefinition;
		baseDefinitionKey = slotDefinition != null ? slotDefinition.name : null;
	}

	private void EnsureResolved()
	{
		if ((winlineDefinitions == null || winlineDefinitions.Count == 0) && winlineDefinitionKeys != null)
		{
			winlineDefinitions = new List<WinlineDefinition>();
			for (int i = 0; i < winlineDefinitionKeys.Length; i++)
			{
				var def = DefinitionResolver.Resolve<WinlineDefinition>(winlineDefinitionKeys[i]);
				if (def != null) winlineDefinitions.Add(def);
			}
		}

		if ((betLevelDefinitions == null || betLevelDefinitions.Count == 0) && betLevelDefinitionKeys != null)
		{
			betLevelDefinitions = new List<BetLevelDefinition>();
			for (int i = 0; i < betLevelDefinitionKeys.Length; i++)
			{
				var def = DefinitionResolver.Resolve<BetLevelDefinition>(betLevelDefinitionKeys[i]);
				if (def != null) betLevelDefinitions.Add(def);
			}
		}

		if (baseDefinition == null && !string.IsNullOrEmpty(baseDefinitionKey))
		{
			baseDefinition = DefinitionResolver.Resolve<SlotsDefinition>(baseDefinitionKey);
		}
	}

	public void AddNewReel(ReelData reelData)
	{
		currentReelData.Add(reelData);
	}

	public void InsertReelAt(int index, ReelData reelData)
	{
		if (index < 0 || index > currentReelData.Count)
		{
			throw new ArgumentOutOfRangeException(nameof(index));
		}

		currentReelData.Insert(index, reelData);
	}

	public void RemoveReel(ReelData reelData)
	{
		if (!currentReelData.Contains(reelData))
		{
			throw new Exception("tried to remove reel that isn't registered!");
		}

		// Guard: do not allow removing the last remaining reel
		if (currentReelData.Count <= 1)
		{
			throw new InvalidOperationException("Cannot remove the only reel from a slot. A slot must have at least one reel.");
		}

		currentReelData.Remove(reelData);
	}
}
