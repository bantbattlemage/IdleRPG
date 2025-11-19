using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class PlayerData : Data
{
	[SerializeField] private int credits;
	public int Credits => credits;

	[SerializeField] private BetLevelDefinition currentBet;
	public BetLevelDefinition CurrentBet => currentBet;

	[SerializeField] private List<SlotsData> currentSlots;
	public List<SlotsData> CurrentSlots => currentSlots;

	public PlayerData(int c = 0, BetLevelDefinition bet = null)
	{
		credits = c;
		currentBet = bet;
		currentSlots = new List<SlotsData>();
	}

	public void AddSlots(SlotsData slots)
	{
		currentSlots.Add(slots);
	}

	public void RemoveSlots(SlotsData slots)
	{
		if (!currentSlots.Contains(slots))
		{
			throw new Exception("tried to remove slot that isn't registered!");
		}

		currentSlots.Remove(slots);
	}

	public void SetCurrentBet(BetLevelDefinition bet)
	{
		currentBet = bet;
		DataPersistenceManager.Instance.SaveGame();
	}

	public void SetCurrentCredits(int c)
	{
		credits = c;
		DataPersistenceManager.Instance.SaveGame();
	}
}
