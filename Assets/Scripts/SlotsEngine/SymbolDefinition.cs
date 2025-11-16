using UnityEngine;

[CreateAssetMenu(fileName = "Data", menuName = "ScriptableObjects/SymbolDefinition", order = 2)]
public class SymbolDefinition : ScriptableObject
{
	[SerializeField] private string name;
	[SerializeField] private Sprite sprite;

	public string Name => name;
	public Sprite Sprite => sprite;
}
