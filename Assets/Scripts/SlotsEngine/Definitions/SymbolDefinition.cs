using UnityEngine;

public class SymbolDefinition : BaseDefinition<SymbolData>
{
	[SerializeField] private string symbolName;
	[SerializeField] private Sprite symbolSprite;
	[SerializeField] private int[] baseValueMultiplier;
	[SerializeField] private float weight = 1;

	public string SymbolName => symbolName;
	public Sprite SymbolSprite => symbolSprite;
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

	public override SymbolData CreateInstance()
	{
		return new SymbolData(symbolName, symbolSprite, baseValueMultiplier, weight);
	}
}
