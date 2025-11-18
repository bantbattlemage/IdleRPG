using System;
using UnityEngine;

[Serializable]
public class PlayerData : Data
{
	[SerializeField] private int credits;
	public int Credits => credits;

	[SerializeField] private BetLevelDefinition currentBet;
	public BetLevelDefinition CurrentBet => currentBet;

	[SerializeField] private SlotsEngine[] currentSlots;
	public SlotsEngine[] CurrentSlots => currentSlots;

	public PlayerData(int c = 0, BetLevelDefinition bet = null)
	{
		credits = c;
		currentBet = bet;
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
