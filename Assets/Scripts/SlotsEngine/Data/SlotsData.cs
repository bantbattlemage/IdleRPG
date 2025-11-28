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
		if (reelData == null) return;
		if (currentReelData == null) currentReelData = new List<ReelData>();

		for (int i = 0; i < currentReelData.Count; i++)
		{
			var existing = currentReelData[i];
			if (existing == null) continue;
			if (reelData.AccessorId > 0 && existing.AccessorId == reelData.AccessorId)
			{
				Debug.LogWarning($"[SlotsData] Duplicate add ignored for reelAccessor={reelData.AccessorId} on slotAccessor={AccessorId}");
				return;
			}
			var r1 = reelData.CurrentReelStrip;
			var r2 = existing.CurrentReelStrip;
			if (r1 != null && r2 != null && !string.IsNullOrEmpty(r1.InstanceKey) && r1.InstanceKey == r2.InstanceKey)
			{
				Debug.LogWarning($"[SlotsData] Duplicate add (strip instance) ignored for slotAccessor={AccessorId}, reelAccessor={reelData.AccessorId}");
				return;
			}
			if (ReferenceEquals(existing, reelData))
			{
				Debug.LogWarning($"[SlotsData] Duplicate add (reference) ignored for slotAccessor={AccessorId}, reelAccessor={reelData.AccessorId}");
				return;
			}
		}

		currentReelData.Add(reelData);
		Debug.Log($"[SlotsData] Added reelAccessor={reelData.AccessorId} to slotAccessor={AccessorId}. reelCount={currentReelData.Count}");
	}

	public void InsertReelAt(int index, ReelData reelData)
	{
		if (index < 0 || index > currentReelData.Count)
		{
			throw new ArgumentOutOfRangeException(nameof(index));
		}

		if (reelData != null)
		{
			for (int i = 0; i < currentReelData.Count; i++)
			{
				var existing = currentReelData[i];
				if (existing == null) continue;
				if (reelData.AccessorId > 0 && existing.AccessorId == reelData.AccessorId) { Debug.LogWarning($"[SlotsData] Duplicate insert ignored for reelAccessor={reelData.AccessorId} on slotAccessor={AccessorId}"); return; }
				var r1 = reelData.CurrentReelStrip;
				var r2 = existing.CurrentReelStrip;
				if (r1 != null && r2 != null && !string.IsNullOrEmpty(r1.InstanceKey) && r1.InstanceKey == r2.InstanceKey) { Debug.LogWarning($"[SlotsData] Duplicate insert (strip instance) ignored for slotAccessor={AccessorId}, reelAccessor={reelData.AccessorId}"); return; }
				if (ReferenceEquals(existing, reelData)) { Debug.LogWarning($"[SlotsData] Duplicate insert (reference) ignored for slotAccessor={AccessorId}, reelAccessor={reelData.AccessorId}"); return; }
			}
		}

		currentReelData.Insert(index, reelData);
		Debug.Log($"[SlotsData] Inserted reelAccessor={reelData.AccessorId} at index={index} for slotAccessor={AccessorId}. reelCount={currentReelData.Count}");
	}

	public void RemoveReel(ReelData reelData)
	{
		if (!currentReelData.Contains(reelData))
		{
			throw new Exception("tried to remove reel that isn't registered!");
		}

		if (currentReelData.Count <= 1)
		{
			throw new InvalidOperationException("Cannot remove the only reel from a slot. A slot must have at least one reel.");
		}

		currentReelData.Remove(reelData);
		Debug.Log($"[SlotsData] Removed reelAccessor={reelData.AccessorId} from slotAccessor={AccessorId}. reelCount={currentReelData.Count}");
	}
}
