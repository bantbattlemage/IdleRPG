using UnityEngine;

[CreateAssetMenu(fileName = "Data", menuName = "ScriptableObjects/BetLevelDefinition")]
public class BetLevelDefinition : ScriptableObject
{
	[SerializeField] private int creditCost;
	public int CreditCost => creditCost;

	//[SerializeField] private WinlineDefinition[] winLines;
	//public WinlineDefinition[] WinLines => winLines;
}
