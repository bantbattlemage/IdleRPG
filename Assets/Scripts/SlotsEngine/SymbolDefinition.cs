using UnityEngine;
using System.Linq;

[CreateAssetMenu(fileName = "Data", menuName = "ScriptableObjects/SymbolDefinition")]
public class SymbolDefinition : ScriptableObject
{
	[SerializeField] private new string name;
	[SerializeField] private Sprite sprite;
	[SerializeField] private int[] baseValueMultiplier;

	public string Name => name;
	public Sprite Sprite => sprite;
	public int MinWinDepth
	{
		get
		{
			for (int i = 0; i < baseValueMultiplier.Length; i++)
			{
				if (baseValueMultiplier[i] > 0) return i;
			}

			return -1;
		}
	}
	public int[] BaseValueMultiplier => baseValueMultiplier;
}
