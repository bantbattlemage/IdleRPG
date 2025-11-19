using UnityEngine;

public class BetLevelDefinition : BaseDefinition<BetLevelData>
{
	[SerializeField] private int creditCost;
	public int CreditCost => creditCost;

	public override BetLevelData CreateInstance()
	{
		throw new System.NotImplementedException();
	}
}
