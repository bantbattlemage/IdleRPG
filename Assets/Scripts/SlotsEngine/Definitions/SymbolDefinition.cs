using UnityEngine;
using EvaluatorCore;

/// <summary>
/// Authoring-time definition for a symbol's behavior, payout, and matching characteristics.
/// At runtime, `CreateInstance()` produces a `SymbolData` carrying the same parameters.
/// </summary>
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

	// New: mode for how this symbol can trigger wins
	[SerializeField] private SymbolWinMode winMode = SymbolWinMode.LineMatch;
	// For TotalCount mode: how many symbols (anywhere) are required to trigger
	[SerializeField] private int totalCountTrigger = -1;

	// New: per-reel maximum count allowed within a single spin. -1 to ignore.
	[SerializeField] private int maxPerReel = -1;

	// New: integer match group identifier. -1 = unset; when unset, a stable hash of the asset name is used at runtime.
	[SerializeField] private int matchGroupId = -1;

	public string SymbolName => symbolName;
	public Sprite SymbolSprite => symbolSprite;

	/// <summary>
	/// Returns an integer identifier used to group symbols for matching. If the serialized id is -1,
	/// a stable hash of the asset name is returned so symbols match themselves by default.
	/// </summary>
	public int MatchGroupId => matchGroupId != -1 ? matchGroupId : ComputeStableHash(this.name);

	/// <summary>
	/// Compute a deterministic 32-bit hash for a string. Uses a simple FNV-like algorithm to remain stable across runs.
	/// Public so other runtime code can compute the same stable id for legacy runtime SymbolData instances.
	/// </summary>
	public static int ComputeStableHash(string s)
	{
		if (string.IsNullOrEmpty(s)) return 0;
		unchecked
		{
			int hash = (int)2166136261u;
			for (int i = 0; i < s.Length; i++)
			{
				hash = (hash ^ s[i]) * 16777619;
			}
			return hash == 0 ? 1 : hash; // ensure non-zero when possible
		}
	}
	
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

	public SymbolWinMode WinMode => winMode;
	public int TotalCountTrigger => totalCountTrigger;

	public int MaxPerReel => maxPerReel;

	/// <summary>
	/// Create the runtime `SymbolData` equivalent of this authoring asset.
	/// </summary>
	public override SymbolData CreateInstance()
	{
		// pass match group id and definition asset name into runtime SymbolData
		return new SymbolData(symbolName, symbolSprite, BaseValue, MinWinDepth, weight, payScaling, isWild, allowWildMatch, winMode, totalCountTrigger, maxPerReel, MatchGroupId);
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
		winMode = SymbolWinMode.LineMatch;
		totalCountTrigger = -1;
		maxPerReel = -1;
		matchGroupId = -1;
	}
}
