using System;
using UnityEngine;

[Serializable]
public class SymbolData : Data
{
	[SerializeField] private string name;
	// Sprite must not be serialized into save data. Store a key instead and resolve at runtime.
	[NonSerialized] private Sprite sprite;
	[SerializeField] private string spriteKey;
	[SerializeField] private int baseValue = 0;
	[SerializeField] private int minWinDepth = -1;
	[SerializeField] private float weight = 1;
	[SerializeField] private PayScaling payScaling = PayScaling.DepthSquared;

	// Wild behavior
	[SerializeField] private bool isWild = false;
	[SerializeField] private bool allowWildMatch = true;

	// New: win mode and total count trigger
	[SerializeField] private SymbolWinMode winMode = SymbolWinMode.LineMatch;
	[SerializeField] private int totalCountTrigger = -1;

	// New: optional per-reel cap for this symbol during a single spin. -1 to ignore.
	[SerializeField] private int maxPerReel = -1;

	// New: integer match group identifier (set by definition). 0 indicates unknown/unset.
	[SerializeField] private int matchGroupId = 0;

	public string Name => name;
	public int MatchGroupId => matchGroupId;
	public Sprite Sprite
	{
		get
		{
			if (sprite == null && !string.IsNullOrEmpty(spriteKey))
			{
				sprite = AssetResolver.ResolveSprite(spriteKey);
			}
			return sprite;
		}
		set
		{
			sprite = value;
			spriteKey = value != null ? value.name : null;
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

	// Backwards-compatible constructor (previous signature)
	public SymbolData(string symbolName, Sprite symbolSprite, int baseVal, int minDepth, float symbolWeight, PayScaling scaling = PayScaling.DepthSquared, bool wild = false, bool allowWild = true)
		: this(symbolName, symbolSprite, baseVal, minDepth, symbolWeight, scaling, wild, allowWild, SymbolWinMode.LineMatch, -1, -1, 0)
	{
	}

	// New constructor using baseValue/minWinDepth/scaling and explicit mode/totalTrigger
	public SymbolData(string symbolName, Sprite symbolSprite, int baseVal, int minDepth, float symbolWeight, PayScaling scaling = PayScaling.DepthSquared, bool wild = false, bool allowWild = true, SymbolWinMode mode = SymbolWinMode.LineMatch, int totalTrigger = -1, int maxPerReelParam = -1, int matchGroup = 0)
	{
		name = symbolName;
		sprite = symbolSprite;
		spriteKey = symbolSprite != null ? symbolSprite.name : null;
		baseValue = baseVal;
		minWinDepth = minDepth;
		weight = symbolWeight;
		payScaling = scaling;
		isWild = wild;
		allowWildMatch = allowWild;
		winMode = mode;
		totalCountTrigger = totalTrigger;
		maxPerReel = maxPerReelParam;
		matchGroupId = matchGroup;
	}

	/// <summary>
	/// Determines if this symbol should be considered a match with <paramref name="other"/>.
	/// Matching rules:
	/// - Group id equality (non-zero) indicates match
	/// - Wild symbol rules remain: two wilds match; wild matches non-wild when allowed
	/// </summary>
	public bool Matches(SymbolData other)
	{
		if (other == null) return false;

		// Both wild -> match
		if (this.IsWild && other.IsWild) return true;

		// Group match: if both have a non-zero MatchGroupId and they are equal
		if (this.MatchGroupId != 0 && other.MatchGroupId != 0 && this.MatchGroupId == other.MatchGroupId) return true;

		// Wild substitution rules
		if (this.IsWild && other.AllowWildMatch) return true;
		if (other.IsWild && this.AllowWildMatch) return true;

		return false;
	}
}
