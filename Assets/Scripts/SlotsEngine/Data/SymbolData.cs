using UnityEngine;

public class SymbolData : Data
{
	[SerializeField] private string name;
	[SerializeField] private Sprite sprite;
	[SerializeField] private int[] baseValueMultiplier;
	[SerializeField] private float weight = 1;

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
	public float Weight => weight;

	public SymbolData(string symbolName, Sprite symbolSprite, int[] values, float symbolWeight)
	{
		name = symbolName;
		sprite = symbolSprite;
		baseValueMultiplier = values;
		weight = symbolWeight;
	}
}
