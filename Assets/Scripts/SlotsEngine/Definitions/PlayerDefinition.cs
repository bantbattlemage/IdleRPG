using UnityEngine;

[CreateAssetMenu(fileName = "Data", menuName = "ScriptableObjects/PlayerDefinition")]
public class PlayerDefinition : ScriptableObject
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
}
