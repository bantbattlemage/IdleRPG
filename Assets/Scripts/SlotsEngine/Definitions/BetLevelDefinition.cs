using UnityEngine;

[CreateAssetMenu(fileName = "Data", menuName = "ScriptableObjects/BetLevelDefinition")]
public class BetLevelDefinition : BaseDefinition<BetLevelData>
{
	[SerializeField] private int creditCost;
	public int CreditCost => creditCost;

	public override BetLevelData CreateInstance()
	{
		throw new System.NotImplementedException();
	}
}
