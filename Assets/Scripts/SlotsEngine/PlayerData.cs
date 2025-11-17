using System;
using UnityEngine;

[Serializable]
public class PlayerData : Data
{
	[SerializeField] private int credits;
	public int Credits => credits;

	[SerializeField] private BetLevelDefinition currentBet;
	public BetLevelDefinition CurrentBet => currentBet;

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
