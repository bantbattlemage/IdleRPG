using UnityEngine;

public enum PayScaling
{
	DepthSquared = 0
}

public class SymbolDefinition : BaseDefinition<SymbolData>
{
	[SerializeField] private string symbolName;
	[SerializeField] private Sprite symbolSprite;
	[SerializeField] private int baseValue = 0;
	[SerializeField] private int minWinDepth = 3; // default for newly created definitions
	[SerializeField] private PayScaling payScaling = PayScaling.DepthSquared;
	[SerializeField] private float weight = 1;

	// Wild behavior
	[SerializeField] private bool isWild = false;
	[SerializeField] private bool allowWildMatch = true;

	public string SymbolName => symbolName;
	public Sprite SymbolSprite => symbolSprite;
	
	/// <summary>
	/// The minimum number of consecutive matching symbols required for this symbol to trigger a win.
	/// Returns -1 if this symbol cannot trigger wins.
	/// </summary>
	public int MinWinDepth => minWinDepth;

	public int BaseValue => baseValue;

	public PayScaling PayScaling => payScaling;
	public float Weight => weight;

	public bool IsWild => isWild;
	public bool AllowWildMatch => allowWildMatch;

	public override SymbolData CreateInstance()
	{
		return new SymbolData(symbolName, symbolSprite, BaseValue, MinWinDepth, weight, payScaling, isWild, allowWildMatch);
	}

	public override void InitializeDefaults()
	{
		// Ensure new assets created via editor get sensible defaults
		minWinDepth = 3;
		payScaling = PayScaling.DepthSquared;
		baseValue = 1;
		weight = 1f;
		isWild = false;
		allowWildMatch = true;
	}
}
