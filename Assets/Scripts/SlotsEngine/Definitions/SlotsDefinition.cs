using UnityEngine;

[CreateAssetMenu(fileName = "Data", menuName = "ScriptableObjects/SlotsDefinition", order = 1)]
public class SlotsDefinition : ScriptableObject
{
	[SerializeField] private ReelDefinition[] reelDefinitions;
	public ReelDefinition[] ReelDefinitions => reelDefinitions;

	[SerializeField] private WinlineDefinition[] winlineDefinitions;
	public WinlineDefinition[] WinlineDefinitions => winlineDefinitions;

	[SerializeField] private BetLevelDefinition[] betLevelDefinitions;
	public BetLevelDefinition[] BetLevelDefinitions => betLevelDefinitions;
}
