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

		// Prevent sharing ReelData across multiple SlotsData: check global slots for references
		try
		{
			if (SlotsDataManager.Instance != null)
			{
				var allSlots = SlotsDataManager.Instance.GetAllData();
				if (allSlots != null)
				{
					foreach (var s in allSlots)
					{
						if (s == null || ReferenceEquals(s, this) || s.CurrentReelData == null) continue;
						foreach (var other in s.CurrentReelData)
						{
							if (other == null) continue;
							// If this reel is referenced by another SlotsData (by accessor or reference) prevent adding to expose upstream issues.
							if (reelData.AccessorId > 0 && other.AccessorId == reelData.AccessorId)
							{
								return;
							}
							if (ReferenceEquals(other, reelData))
							{
								return;
							}

							// Prevent sharing any SymbolData between reels/slots
							if (reelData.CurrentSymbolData != null && other.CurrentSymbolData != null)
							{
								for (int a = 0; a < reelData.CurrentSymbolData.Count; a++)
								{
									var symA = reelData.CurrentSymbolData[a];
									if (symA == null) continue;
									for (int b = 0; b < other.CurrentSymbolData.Count; b++)
									{
										var symB = other.CurrentSymbolData[b];
										if (symB == null) continue;
										if (symA.AccessorId > 0 && symA.AccessorId == symB.AccessorId)
										{
											return;
										}
										if (ReferenceEquals(symA, symB))
										{
											return;
										}
									}
								}
							}
						}
					}
				}
			}
		}
		catch (Exception) { }

		for (int i = 0; i < currentReelData.Count; i++)
		{
			var existing = currentReelData[i];
			if (existing == null) continue;
			// If adding a reel whose accessor matches an existing one, prevent adding to expose upstream issue
			if (reelData.AccessorId > 0 && existing.AccessorId == reelData.AccessorId)
			{
				return;
			}
			var r1 = reelData.CurrentReelStrip;
			var r2 = existing.CurrentReelStrip;
			// Only consider registered accessor ids for strip uniqueness. Do not rely on legacy string keys.
			if (r1 != null && r2 != null && r1.AccessorId > 0 && r1.AccessorId == r2.AccessorId)
			{
				// Prevent sharing the same strip instance
				return;
			}
			if (ReferenceEquals(existing, reelData))
			{
				return;
			}
		}

		currentReelData.Add(reelData);
	}

	public void InsertReelAt(int index, ReelData reelData)
	{
		if (currentReelData == null) currentReelData = new List<ReelData>();
		if (index < 0 || index > currentReelData.Count)
		{
			throw new ArgumentOutOfRangeException(nameof(index));
		}

		if (reelData == null)
		{
			currentReelData.Insert(index, reelData);
			return;
		}

		// Prevent sharing across slots similar to AddNewReel
		try
		{
			if (SlotsDataManager.Instance != null)
			{
				var allSlots = SlotsDataManager.Instance.GetAllData();
				if (allSlots != null)
				{
					foreach (var s in allSlots)
					{
						if (s == null || ReferenceEquals(s, this) || s.CurrentReelData == null) continue;
						foreach (var other in s.CurrentReelData)
						{
							if (other == null) continue;
							if (reelData.AccessorId > 0 && other.AccessorId == reelData.AccessorId) return;
							if (ReferenceEquals(other, reelData)) return;

							if (reelData.CurrentSymbolData != null && other.CurrentSymbolData != null)
							{
								for (int a = 0; a < reelData.CurrentSymbolData.Count; a++)
								{
									var symA = reelData.CurrentSymbolData[a];
									if (symA == null) continue;
									for (int b = 0; b < other.CurrentSymbolData.Count; b++)
									{
										var symB = other.CurrentSymbolData[b];
										if (symB == null) continue;
										if (symA.AccessorId > 0 && symA.AccessorId == symB.AccessorId) return;
										if (ReferenceEquals(symA, symB)) return;
									}
								}
							}
						}
					}
				}
			}
		}
		catch (Exception) { }

		for (int i = 0; i < currentReelData.Count; i++)
		{
			var existing = currentReelData[i];
			if (existing == null) continue;
			if (reelData.AccessorId > 0 && existing.AccessorId == reelData.AccessorId) return;
			var r1 = reelData.CurrentReelStrip;
			var r2 = existing.CurrentReelStrip;
			if (r1 != null && r2 != null && r1.AccessorId > 0 && r1.AccessorId == r2.AccessorId) return;
			if (ReferenceEquals(existing, reelData)) return;
		}

		currentReelData.Insert(index, reelData);
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
	}

	// Helper: deep-clone a ReelData (strip and symbol runtime lists) to ensure per-slot uniqueness
	private ReelData CloneReelData(ReelData src)
	{
		if (src == null) return null;
		ReelStripData clonedStrip = null;
		try
		{
			var s = src.CurrentReelStrip;
			if (s != null)
			{
				clonedStrip = new ReelStripData(s.Definition, s.StripSize, s.SymbolDefinitions, null, null, populateRuntimeSymbols: false);
				var existing = clonedStrip.RuntimeSymbols != null ? clonedStrip.RuntimeSymbols.Count : 0;
				for (int i = existing - 1; i >= 0; i--) clonedStrip.RemoveRuntimeSymbolAt(i);
				if (s.RuntimeSymbols != null)
				{
					foreach (var sym in s.RuntimeSymbols)
					{
						if (sym == null) { clonedStrip.AddRuntimeSymbol(null); continue; }
						var cs = new SymbolData(sym.Name, sym.Sprite, sym.BaseValue, sym.MinWinDepth, sym.Weight, sym.PayScaling, sym.IsWild, sym.AllowWildMatch, sym.WinMode, sym.TotalCountTrigger, sym.MaxPerReel, sym.MatchGroupId);
						cs.EventTriggerScript = sym.EventTriggerScript;
						clonedStrip.AddRuntimeSymbol(cs);
					}
				}
				if (clonedStrip.AccessorId == 0 && ReelStripDataManager.Instance != null) ReelStripDataManager.Instance.AddNewData(clonedStrip);
			}
		}
		catch (Exception) { clonedStrip = null; }

		var clone = new ReelData(src.ReelSpinDuration, src.SymbolCount, null, src.BaseDefinition, clonedStrip ?? src.CurrentReelStrip);
		if (ReelDataManager.Instance != null) ReelDataManager.Instance.AddNewData(clone);
		return clone;
	}
}
