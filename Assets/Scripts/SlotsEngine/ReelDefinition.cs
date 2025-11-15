using UnityEngine;

[CreateAssetMenu(fileName = "Data", menuName = "ScriptableObjects/ReelDefinition", order = 1)]
public class ReelDefinition : ScriptableObject, IReel
{
	[SerializeField] private int symbolCount;
	[SerializeField] private int symbolSize;
	[SerializeField] private int symbolSpacing;
	[SerializeField] private int reelsSpacing;

	public int SymbolCount => symbolCount;
	public int SymbolSize => symbolSize;
	public int SymbolSpacing => symbolSpacing;
	public int ReelsSpacing => reelsSpacing;
}
