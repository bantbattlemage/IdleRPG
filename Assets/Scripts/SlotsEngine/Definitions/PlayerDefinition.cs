using UnityEngine;

public class PlayerDefinition : BaseDefinition<PlayerData>
{
	[SerializeField] private int credits;
	public int Credits => credits;

	[SerializeField] private BetLevelDefinition currentBet;
	public BetLevelDefinition CurrentBet => currentBet;

	public void SetCurrentBet(BetLevelDefinition bet)
	{
		currentBet = bet;
	}

	public void SetCurrentCredits(int c)
	{
		credits = c;
	}

	public override PlayerData CreateInstance()
	{
		PlayerData newPlayerData = new PlayerData(credits, currentBet);
		return newPlayerData;
	}
}
