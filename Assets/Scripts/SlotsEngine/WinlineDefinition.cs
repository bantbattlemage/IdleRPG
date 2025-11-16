using UnityEngine;

[CreateAssetMenu(fileName = "Data", menuName = "ScriptableObjects/WinlineDefinition")]
public class WinlineDefinition : ScriptableObject
{
	[SerializeField] private float winMultiplier = 1;
	[SerializeField] private int[] symbolIndexes;

	public float WinMultiplier => winMultiplier;
	public int[] SymbolIndexes => symbolIndexes;
	public int Depth => symbolIndexes.Length;
}
