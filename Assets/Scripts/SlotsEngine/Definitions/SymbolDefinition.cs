using UnityEngine;

public class SymbolDefinition : BaseDefinition<SymbolData>
{
	[SerializeField] private string symbolName;
	[SerializeField] private Sprite symbolSprite;
	[SerializeField] private int[] baseValueMultiplier;
	[SerializeField] private float weight = 1;

	// Wild behavior
	[SerializeField] private bool isWild = false;
	[SerializeField] private bool allowWildMatch = true;

	public string SymbolName => symbolName;
	public Sprite SymbolSprite => symbolSprite;
	
	/// <summary>
	/// The minimum number of consecutive matching symbols required for this symbol to trigger a win.
	/// Returns -1 if this symbol cannot trigger wins (all multipliers are 0).
	/// </summary>
	public int MinWinDepth
	{
		get
		{
			for (int i = 0; i < (baseValueMultiplier != null ? baseValueMultiplier.Length : 0); i++)
			{
				if (baseValueMultiplier[i] > 0) return i + 1; // Return count (1-based), not index
			}

			return -1;
		}
	}
	public int[] BaseValueMultiplier => baseValueMultiplier;
	public float Weight => weight;

	public bool IsWild => isWild;
	public bool AllowWildMatch => allowWildMatch;

	public override SymbolData CreateInstance()
	{
		return new SymbolData(symbolName, symbolSprite, baseValueMultiplier, weight, isWild, allowWildMatch);
	}
}
