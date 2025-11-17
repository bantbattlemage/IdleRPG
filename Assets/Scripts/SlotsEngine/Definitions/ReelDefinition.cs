using UnityEngine;

[CreateAssetMenu(fileName = "Data", menuName = "ScriptableObjects/ReelDefinition", order = 1)]
public class ReelDefinition : ScriptableObject
{
	[SerializeField] private float reelSpinDuration;
	[SerializeField] private int symbolCount;
	[SerializeField] private int symbolSize;
	[SerializeField] private int symbolSpacing;

	public float ReelSpinDuration => reelSpinDuration;
	public int SymbolCount => symbolCount;
	public int SymbolSize => symbolSize;
	public int SymbolSpacing => symbolSpacing;
}
