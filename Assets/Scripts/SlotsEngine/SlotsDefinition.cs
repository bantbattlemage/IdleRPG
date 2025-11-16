using UnityEngine;

[CreateAssetMenu(fileName = "Data", menuName = "ScriptableObjects/SlotsDefinition", order = 1)]
public class SlotsDefinition : ScriptableObject
{
	public string Name;
	public ReelDefinition[] ReelDefinitions;
	public WinlineDefinition[] WinlineDefinitions;
}
